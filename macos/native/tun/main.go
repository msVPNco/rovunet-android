package main

import (
	"context"
	"errors"
	"io"
	"net"
	"os"
	"os/signal"
	"path/filepath"
	"runtime"
	"syscall"
	"time"

	C "github.com/metacubex/mihomo/constant"
	logrus "github.com/sirupsen/logrus"
)

func runClient() error {
	conn, e := net.DialTimeout("unix", socketPath, 4*time.Second)
	if e != nil {
		return errors.New("MAC_TUN_HELPER_REQUIRED")
	}
	defer conn.Close()
	if !rootPeer(conn) {
		return errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	// The signed native client has no shell/config-loader interface. Stdin EOF
	// closes its authenticated socket even if the managed Agent/UI crashes.
	go func() {
		defer conn.Close()
		for {
			var r request
			if readFrame(os.Stdin, &r) != nil || r.validate() != nil {
				return
			}
			if writeFrame(conn, r) != nil {
				return
			}
		}
	}()
	for {
		var r reply
		if e = readFrame(conn, &r); e != nil {
			return e
		}
		if e = writeFrame(os.Stdout, r); e != nil {
			return e
		}
		if r.State == "error" || r.State == "disconnected" || r.State == "available" {
			return nil
		}
	}
}
func runWorker(ctx context.Context) (result error) {
	if os.Geteuid() != 0 {
		return errors.New("MAC_TUN_PERMISSION_REQUIRED")
	}
	var r workerRequest
	if e := readFrame(os.Stdin, &r); e != nil {
		return e
	}
	dir, e := os.MkdirTemp("", "rovunet-tun-worker-")
	if e != nil {
		return e
	}
	defer os.RemoveAll(dir)
	C.SetHomeDir(dir)
	plane, ready, e := startDataPlane(r.Proxy, r.Base, nil)
	if e != nil {
		return e
	}
	defer func() {
		if err := plane.Close(); err != nil && result == nil {
			result = errors.New("MAC_TUN_RESTORE_PENDING")
		}
	}()
	if e = writeFrame(os.Stdout, ready); e != nil {
		return e
	}
	eof := make(chan struct{})
	go func() { var scratch [1]byte; _, _ = os.Stdin.Read(scratch[:]); close(eof) }()
	tick := time.NewTicker(time.Second)
	defer tick.Stop()
	for {
		select {
		case <-eof:
			return nil
		case <-ctx.Done():
			return nil
		case <-tick.C:
			if _, e = net.InterfaceByName(ready.Interface); e != nil {
				return errors.New("MAC_TUN_WORKER_EXITED")
			}
		}
	}
}
func runDaemon(ctx context.Context) error {
	if runtime.GOOS != "darwin" || os.Geteuid() != 0 {
		return errors.New("MAC_TUN_PERMISSION_REQUIRED")
	}
	if e := secureServiceDirectory(); e != nil {
		return e
	}
	if e := os.Remove(socketPath); e != nil && !os.IsNotExist(e) {
		return e
	}
	listener, e := net.Listen("unix", socketPath)
	if e != nil {
		return e
	}
	defer listener.Close()
	defer os.Remove(socketPath)
	if e = os.Chmod(socketPath, 0666); e != nil {
		return e
	}
	go func() { <-ctx.Done(); listener.Close() }()
	service := &service{lease: make(chan struct{}, 1), spawn: spawnWorker}
	capacity := make(chan struct{}, 4)
	for {
		conn, e := listener.Accept()
		if e != nil {
			return nil
		}
		select {
		case capacity <- struct{}{}:
			go func() {
				defer func() { <-capacity }()
				p, e := authenticate(conn)
				if e != nil {
					_ = writeFrame(conn, reply{State: "error", Error: "MAC_TUN_NOT_AUTHORIZED"})
					conn.Close()
					return
				}
				service.serve(ctx, conn, p)
			}()
		default:
			conn.Close()
		}
	}
}
func main() {
	logrus.SetOutput(io.Discard)
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()
	var e error
	if len(os.Args) != 2 {
		e = errors.New("MAC_TUN_INVALID_REQUEST")
	} else {
		switch os.Args[1] {
		case "--daemon":
			e = runDaemon(ctx)
		case "--worker":
			e = runWorker(ctx)
		case "--client":
			e = runClient()
		default:
			e = errors.New("MAC_TUN_INVALID_REQUEST")
		}
	}
	if e != nil {
		_ = writeFrame(os.Stdout, reply{State: "error", Error: workerError(e)})
		os.Exit(1)
	}
}
func secureServiceDirectory() error {
	dir := filepath.Dir(socketPath)
	if e := os.MkdirAll(dir, 0755); e != nil {
		return e
	}
	return secureRootFile(dir, true)
}
