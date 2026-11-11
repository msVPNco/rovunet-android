//go:build darwin

// Unprivileged process owner. No network settings or credentials in arguments.
package main

import (
    "os"
    "os/exec"
    "os/signal"
    "syscall"
    "time"
)

func main() {
    if len(os.Args) < 2 { os.Exit(64) }
    parent := os.Getppid()
    if parent <= 1 { os.Exit(70) }
    child := exec.Command(os.Args[1], os.Args[2:]...)
    child.Stdin, child.Stdout, child.Stderr = os.Stdin, os.Stdout, os.Stderr
    child.SysProcAttr = &syscall.SysProcAttr{Setpgid: true}
    if child.Start() != nil { os.Exit(70) }
    finished := make(chan error, 1)
    go func() { finished <- child.Wait() }()
    quit := make(chan os.Signal, 1)
    signal.Notify(quit, syscall.SIGTERM, syscall.SIGINT, syscall.SIGHUP)
    ticker := time.NewTicker(200 * time.Millisecond)
    defer ticker.Stop()
    stop := func() {
        _ = syscall.Kill(-child.Process.Pid, syscall.SIGTERM)
        select { case <-finished: case <-time.After(1500*time.Millisecond): }
        // Also clean descendants if the immediate child has already exited.
        _ = syscall.Kill(-child.Process.Pid, syscall.SIGKILL)
    }
    for {
        select {
        case err := <-finished:
            _ = syscall.Kill(-child.Process.Pid, syscall.SIGKILL)
            if err != nil { os.Exit(1) }; return
        case <-quit: stop(); return
        case <-ticker.C:
            if os.Getppid() != parent { stop(); return }
        }
    }
}
