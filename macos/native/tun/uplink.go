package main

import (
	"errors"
	"io"
	"net"
	"strconv"
	"time"
)

// Authenticate the exact local relay before changing routes or registering DNS.
// RFC 1929 does not require contacting the user's remote peer in this stage.
func checkUplink(r request) error {
	address := net.JoinHostPort("127.0.0.1", strconv.Itoa(r.SocksPort))
	conn, err := net.DialTimeout("tcp4", address, 3*time.Second)
	if err != nil {
		return errors.New("MAC_TUN_SOCKS_UNAVAILABLE")
	}
	defer conn.Close()
	return authenticateUplink(conn, r)
}

func authenticateUplink(conn net.Conn, r request) error {
	if err := conn.SetDeadline(time.Now().Add(3 * time.Second)); err != nil {
		return errors.New("MAC_TUN_SOCKS_UNAVAILABLE")
	}
	var reply [2]byte
	exchange := func(message []byte) error {
		n, err := conn.Write(message)
		if err != nil || n != len(message) {
			return errors.New("MAC_TUN_SOCKS_UNAVAILABLE")
		}
		if _, err = io.ReadFull(conn, reply[:]); err != nil {
			return errors.New("MAC_TUN_SOCKS_UNAVAILABLE")
		}
		return nil
	}
	if err := exchange([]byte{5, 1, 2}); err != nil {
		return err
	}
	if reply != [2]byte{5, 2} {
		return errors.New("MAC_TUN_SOCKS_AUTH_FAILED")
	}
	auth := make([]byte, 0, 3+len(r.SocksUser)+len(r.SocksPassword))
	auth = append(auth, 1, byte(len(r.SocksUser)))
	auth = append(auth, r.SocksUser...)
	auth = append(auth, byte(len(r.SocksPassword)))
	auth = append(auth, r.SocksPassword...)
	defer func() {
		for i := range auth {
			auth[i] = 0
		}
	}()
	if err := exchange(auth); err != nil {
		return err
	}
	if reply != [2]byte{1, 0} {
		return errors.New("MAC_TUN_SOCKS_AUTH_FAILED")
	}
	return nil
}

// Return only protocol codes to the ordinary user client. System errors can
// contain hostnames and private paths.
func workerError(err error) string {
	if err != nil {
		switch err.Error() {
		case "MAC_TUN_SOCKS_UNAVAILABLE", "MAC_TUN_SOCKS_AUTH_FAILED", "MAC_TUN_CONFIG_FAILED", "MAC_TUN_DEVICE_BUSY", "MAC_TUN_HELPER_REQUIRED", "MAC_TUN_NOT_AUTHORIZED", "MAC_TUN_RESTORE_PENDING":
			return err.Error()
		}
	}
	return "MAC_TUN_START_FAILED"
}
