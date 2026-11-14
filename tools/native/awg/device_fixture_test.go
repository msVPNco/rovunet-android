package main

import (
	"crypto/ecdh"
	"crypto/rand"
	"crypto/rsa"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"encoding/pem"
	"fmt"
	"io"
	"math/big"
	"net"
	"net/netip"
	"os"
	"os/signal"
	"strconv"
	"syscall"
	"time"

	"github.com/metacubex/mihomo/config"
	C "github.com/metacubex/mihomo/constant"
	"github.com/metacubex/mihomo/hub/executor"
	"github.com/metacubex/mipstack"
	"github.com/metacubex/wireguard-go/conn"
	"github.com/metacubex/wireguard-go/device"
	"github.com/metacubex/wireguard-go/tun"
	logrus "github.com/sirupsen/logrus"
)

func udpFixturePort() (int, error) {
	socket, err := net.ListenPacket("udp", "127.0.0.1:0")
	if err != nil {
		return 0, err
	}
	defer socket.Close()
	return socket.LocalAddr().(*net.UDPAddr).Port, nil
}

// Built only into the host test executable, never into the shipped Android child.
func runUdpDeviceFixture() error {
	logrus.SetOutput(io.Discard)
	directory, err := os.MkdirTemp("", "rovunet-udp-device-")
	if err != nil {
		return err
	}
	defer os.RemoveAll(directory)
	C.SetHomeDir(directory)
	serverKey, err := ecdh.X25519().GenerateKey(rand.Reader)
	if err != nil {
		return err
	}
	clientKey, err := ecdh.X25519().GenerateKey(rand.Reader)
	if err != nil {
		return err
	}
	wrongKey, err := ecdh.X25519().GenerateKey(rand.Reader)
	if err != nil {
		return err
	}
	stack, err := mipstack.New(mipstack.Config{LocalAddresses: []netip.Prefix{netip.MustParsePrefix("10.77.0.1/32")}, MTU: 1280})
	if err != nil {
		return err
	}
	if err = stack.Start(); err != nil {
		return err
	}
	ft := &fixtureTun{Stack: stack, events: make(chan tun.Event, 1)}
	ft.events <- tun.EventUp
	peer := device.NewDevice(ft, fixtureBind{conn.NewDefaultBind()}, device.NewLogger(device.LogLevelSilent, ""), 2)
	defer peer.Close()
	wgPort, err := udpFixturePort()
	if err != nil {
		return err
	}
	ipc := "private_key=" + hex.EncodeToString(serverKey.Bytes()) + "\nlisten_port=" + strconv.Itoa(wgPort) + "\npublic_key=" + hex.EncodeToString(clientKey.PublicKey().Bytes()) + "\nallowed_ip=10.77.0.2/32\n"
	if err = peer.IpcSet(ipc); err != nil {
		return err
	}
	if err = peer.Up(); err != nil {
		return err
	}
	key, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		return err
	}
	template := &x509.Certificate{SerialNumber: big.NewInt(time.Now().UnixNano()), Subject: pkix.Name{CommonName: "Synthetic UDP device"}, DNSNames: []string{"localhost"}, NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().Add(time.Hour), KeyUsage: x509.KeyUsageDigitalSignature | x509.KeyUsageKeyEncipherment, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth}}
	der, err := x509.CreateCertificate(rand.Reader, template, template, &key.PublicKey, key)
	if err != nil {
		return err
	}
	certificate := string(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: der}))
	private := string(pem.EncodeToMemory(&pem.Block{Type: "RSA PRIVATE KEY", Bytes: x509.MarshalPKCS1PrivateKey(key)}))
	tuicPort, err := udpFixturePort()
	if err != nil {
		return err
	}
	hy2Port, err := udpFixturePort()
	if err != nil {
		return err
	}
	uuid, password := "00000000-0000-4000-8000-000000000001", "synthetic-only"
	listeners := []any{}
	for kind, port := range map[string]int{"tuic": tuicPort, "hysteria2": hy2Port} {
		users := map[string]string{"synthetic": password}
		if kind == "tuic" {
			users = map[string]string{uuid: password}
		}
		listeners = append(listeners, map[string]any{"name": kind, "type": kind, "listen": "0.0.0.0", "port": port, "users": users, "certificate": certificate, "private-key": private, "alpn": []string{"h3"}})
	}
	raw, _ := json.Marshal(map[string]any{"mode": "rule", "log-level": "silent", "dns": map[string]bool{"enable": false}, "listeners": listeners, "rules": []string{"MATCH,REJECT"}})
	cfg, err := config.Parse(raw)
	if err != nil {
		return err
	}
	executor.ApplyConfig(cfg, true)
	defer executor.Shutdown()
	wgLink := func(public []byte) string {
		return fmt.Sprintf("[Interface]\nPrivateKey = %s\nAddress = 10.77.0.2/32\n[Peer]\nPublicKey = %s\nEndpoint = 10.0.2.2:%d\nAllowedIPs = 0.0.0.0/0\n", base64.StdEncoding.EncodeToString(clientKey.Bytes()), base64.StdEncoding.EncodeToString(public), wgPort)
	}
	if err = json.NewEncoder(os.Stdout).Encode(map[string]any{"links": []string{wgLink(serverKey.PublicKey().Bytes()), fmt.Sprintf("tuic://%s:%s@10.0.2.2:%d?sni=localhost&alpn=h3", uuid, password, tuicPort), fmt.Sprintf("hy2://%s@10.0.2.2:%d?sni=localhost", password, hy2Port)}, "wrongWireguard": wgLink(wrongKey.PublicKey().Bytes())}); err != nil {
		return err
	}
	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGTERM)
	defer signal.Stop(stop)
	<-stop
	return nil
}
