package main

import (
	"context"
	"errors"
	"io"
	"net"
	"os"
	"os/exec"
	"sync"
	"syscall"
	"time"
)

type peer struct {
	UID  uint32
	Path string
}
type sessionWorker interface {
	io.Closer
	Done() <-chan struct{}
	Ready() reply
}
type service struct {
	lease chan struct{}
	spawn func(context.Context, request, string) (sessionWorker, error)
}

func (s *service) serve(ctx context.Context, conn net.Conn, p peer) {
	defer conn.Close()
	_ = conn.SetReadDeadline(time.Now().Add(10 * time.Second))
	var r request
	if readFrame(conn, &r) != nil || r.validate() != nil {
		_ = writeFrame(conn, reply{State: "error", Error: "MAC_TUN_INVALID_REQUEST"})
		return
	}
	if r.Action == "status" {
		_ = writeFrame(conn, reply{State: "available", Version: version})
		return
	}
	if r.Action != "start" {
		_ = writeFrame(conn, reply{State: "error", Error: "MAC_TUN_INVALID_REQUEST"})
		return
	}
	base, e := appBase(p.Path)
	if e != nil {
		_ = writeFrame(conn, reply{State: "error", Error: "MAC_TUN_APP_PATH_INVALID"})
		return
	}
	select {
	case s.lease <- struct{}{}:
		defer func() { <-s.lease }()
	default:
		_ = writeFrame(conn, reply{State: "error", Error: "MAC_TUN_BUSY"})
		return
	}
	owner, cancel := context.WithCancel(ctx)
	defer cancel()
	worker, e := s.spawn(owner, r, base)
	if e != nil {
		_ = writeFrame(conn, reply{State: "error", Error: workerError(e)})
		return
	}
	defer func() {
		if worker != nil {
			_ = worker.Close()
		}
	}()
	if writeFrame(conn, worker.Ready()) != nil {
		return
	}
	input := make(chan request, 1)
	go func() {
		for {
			_ = conn.SetReadDeadline(time.Now().Add(12 * time.Second))
			var next request
			if readFrame(conn, &next) != nil || next.validate() != nil {
				close(input)
				return
			}
			select {
			case input <- next:
			case <-owner.Done():
				return
			}
		}
	}()
	for {
		select {
		case <-ctx.Done():
			return
		case <-worker.Done():
			_ = writeFrame(conn, reply{State: "error", Error: "MAC_TUN_WORKER_EXITED"})
			return
		case next, ok := <-input:
			if !ok {
				return
			}
			if !consoleUser(p.UID) {
				_ = writeFrame(conn, reply{State: "error", Error: "MAC_TUN_CONSOLE_REQUIRED"})
				return
			}
			if next.Action == "stop" {
				e = worker.Close()
				worker = nil
				if e != nil {
					_ = writeFrame(conn, reply{State: "error", Error: "MAC_TUN_RESTORE_PENDING"})
				} else {
					_ = writeFrame(conn, reply{State: "disconnected"})
				}
				return
			}
			if next.Action != "status" {
				_ = writeFrame(conn, reply{State: "error", Error: "MAC_TUN_INVALID_REQUEST"})
				return
			}
			if writeFrame(conn, worker.Ready()) != nil {
				return
			}
		}
	}
}

type childWorker struct {
	cmd     *exec.Cmd
	input   io.WriteCloser
	done    chan struct{}
	waitErr error
	ready   reply
	once    sync.Once
}

func (c *childWorker) Done() <-chan struct{} { return c.done }
func (c *childWorker) Ready() reply          { return c.ready }
func (c *childWorker) Close() error {
	c.once.Do(func() { c.input.Close() })
	select {
	case <-c.done:
		if c.waitErr != nil {
			return errors.New("MAC_TUN_RESTORE_PENDING")
		}
		return nil
	case <-time.After(8 * time.Second):
		_ = c.cmd.Process.Kill()
		<-c.done
		return errors.New("MAC_TUN_RESTORE_PENDING")
	}
}
func spawnWorker(ctx context.Context, r request, base string) (sessionWorker, error) {
	exe, e := os.Executable()
	if e != nil {
		return nil, e
	}
	cmd := exec.CommandContext(ctx, exe, "--worker")
	cmd.Env = []string{"PATH=/usr/bin:/bin:/usr/sbin:/sbin", "SKIP_SYSTEM_IPV6_CHECK=1"}
	cmd.Stderr = io.Discard
	cmd.Cancel = func() error { return cmd.Process.Signal(syscall.SIGTERM) }
	cmd.WaitDelay = 3 * time.Second
	input, e := cmd.StdinPipe()
	if e != nil {
		return nil, e
	}
	output, e := cmd.StdoutPipe()
	if e != nil {
		return nil, e
	}
	if e = cmd.Start(); e != nil {
		return nil, e
	}
	child := &childWorker{cmd: cmd, input: input, done: make(chan struct{})}
	// Drain the readiness frame before Wait can close stdout on an early worker error.
	received := make(chan error, 1)
	go func() {
		received <- readFrame(output, &child.ready)
		_, _ = io.Copy(io.Discard, output)
		child.waitErr = cmd.Wait()
		close(child.done)
	}()
	if e = writeFrame(input, workerRequest{Proxy: r, Base: base}); e != nil {
		child.Close()
		return nil, e
	}
	select {
	case e = <-received:
	case <-ctx.Done():
		e = ctx.Err()
	case <-time.After(30 * time.Second):
		e = context.DeadlineExceeded
	}
	if e != nil {
		child.Close()
		return nil, errors.New("MAC_TUN_START_FAILED")
	}
	if child.ready.State != "active" {
		child.Close()
		return nil, errors.New(workerError(errors.New(child.ready.Error)))
	}
	return child, nil
}
