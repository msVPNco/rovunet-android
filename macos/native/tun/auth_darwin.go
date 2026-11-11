//go:build darwin

package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"net"
	"os"
	"path/filepath"
	"regexp"
	"runtime"
	"strings"
	"sync"
	"syscall"
	"unsafe"

	"golang.org/x/sys/unix"
)

type policy struct {
	Version        int
	ClientCDHashes []string
}

func secureRootFile(path string, directory bool) error {
	info, e := os.Lstat(path)
	if e != nil {
		return e
	}
	stat, ok := info.Sys().(*syscall.Stat_t)
	if !ok || stat.Uid != 0 || info.Mode().Perm()&0022 != 0 || info.IsDir() != directory || info.Mode()&os.ModeSymlink != 0 {
		return errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	return nil
}
func consoleUser(uid uint32) bool {
	info, e := os.Stat("/dev/console")
	if e != nil {
		return false
	}
	stat, ok := info.Sys().(*syscall.Stat_t)
	return ok && uid >= 500 && stat.Uid == uid
}
func socketCredentials(conn net.Conn) (uid uint32, pid int, e error) {
	c, ok := conn.(*net.UnixConn)
	if !ok {
		return 0, 0, errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	raw, e := c.SyscallConn()
	if e != nil {
		return 0, 0, e
	}
	var inner error
	e = raw.Control(func(fd uintptr) {
		cred, err := unix.GetsockoptXucred(int(fd), unix.SOL_LOCAL, unix.LOCAL_PEERCRED)
		if err != nil {
			inner = err
			return
		}
		uid = cred.Uid
		pid, inner = unix.GetsockoptInt(int(fd), unix.SOL_LOCAL, unix.LOCAL_PEERPID)
	})
	if e == nil {
		e = inner
	}
	return
}
func rootPeer(conn net.Conn) bool { uid, _, e := socketCredentials(conn); return e == nil && uid == 0 }
func executablePath(pid int) (string, error) {
	buf := make([]byte, 4096)
	_, _, err := syscall.Syscall6(syscall.SYS_PROC_INFO, 2, uintptr(pid), 11, 0, uintptr(unsafe.Pointer(&buf[0])), uintptr(len(buf)))
	if err != 0 {
		return "", err
	}
	return unix.ByteSliceToString(buf), nil
}
func authenticate(conn net.Conn) (peer, error) {
	uid, pid, e := socketCredentials(conn)
	if e != nil || !consoleUser(uid) || pid <= 0 {
		return peer{}, errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	if e = secureRootFile(policyPath, false); e != nil {
		return peer{}, e
	}
	data, e := os.ReadFile(policyPath)
	if e != nil || len(data) > 8192 {
		return peer{}, errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	var p policy
	if json.Unmarshal(data, &p) != nil || p.Version != 1 || len(p.ClientCDHashes) < 1 || len(p.ClientCDHashes) > 8 {
		return peer{}, errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	parts := []string{}
	valid := regexp.MustCompile(`^[0-9a-f]{40}$`)
	for _, hash := range p.ClientCDHashes {
		if !valid.MatchString(hash) {
			return peer{}, errors.New("MAC_TUN_NOT_AUTHORIZED")
		}
		parts = append(parts, `cdhash H"`+hash+`"`)
	}
	if e = checkGuest(pid, strings.Join(parts, " or ")); e != nil {
		return peer{}, e
	}
	path, e := executablePath(pid)
	if e != nil {
		return peer{}, e
	}
	if _, e = appBase(path); e != nil {
		return peer{}, e
	}
	return peer{UID: uid, Path: filepath.Clean(path)}, nil
}

var frameworkOnce sync.Once
var frameworkError error
var api struct{ stringCreate, numberCreate, dictionaryCreate, release, copyGuest, requirementCreate, checkValidity, pidKey, keyCallbacks, valueCallbacks uintptr }

func bindSecurity() error {
	frameworkOnce.Do(func() {
		cf, e := dlopen("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")
		if e != nil {
			frameworkError = e
			return
		}
		sec, e := dlopen("/System/Library/Frameworks/Security.framework/Security")
		if e != nil {
			frameworkError = e
			return
		}
		for _, symbol := range []struct {
			h      uintptr
			name   string
			target *uintptr
		}{{cf, "CFStringCreateWithCString", &api.stringCreate}, {cf, "CFNumberCreate", &api.numberCreate}, {cf, "CFDictionaryCreate", &api.dictionaryCreate}, {cf, "CFRelease", &api.release}, {cf, "kCFTypeDictionaryKeyCallBacks", &api.keyCallbacks}, {cf, "kCFTypeDictionaryValueCallBacks", &api.valueCallbacks}, {sec, "kSecGuestAttributePid", &api.pidKey}, {sec, "SecCodeCopyGuestWithAttributes", &api.copyGuest}, {sec, "SecRequirementCreateWithString", &api.requirementCreate}, {sec, "SecCodeCheckValidity", &api.checkValidity}} {
			*symbol.target, e = dlsym(symbol.h, symbol.name)
			if e != nil {
				frameworkError = e
				return
			}
		}
	})
	return frameworkError
}
func release(ref uintptr) {
	if ref != 0 {
		syscall_syscall(api.release, ref, 0, 0)
	}
}
func checkGuest(pid int, requirement string) error {
	if e := bindSecurity(); e != nil {
		return e
	}
	runtime.LockOSThread()
	defer runtime.UnlockOSThread()
	number := int32(pid)
	pidNumber, _, _ := syscall_syscall(api.numberCreate, 0, 3, uintptr(unsafe.Pointer(&number)))
	if pidNumber == 0 {
		return errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	defer release(pidNumber)
	key := *(*uintptr)(unsafe.Pointer(api.pidKey))
	keys := []uintptr{key}
	values := []uintptr{pidNumber}
	attributes, _, _ := syscall_syscall6(api.dictionaryCreate, 0, uintptr(unsafe.Pointer(&keys[0])), uintptr(unsafe.Pointer(&values[0])), 1, api.keyCallbacks, api.valueCallbacks)
	if attributes == 0 {
		return errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	defer release(attributes)
	var code uintptr
	status, _, _ := syscall_syscall6(api.copyGuest, 0, attributes, 0, uintptr(unsafe.Pointer(&code)), 0, 0)
	if int32(status) != 0 || code == 0 {
		return errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	defer release(code)
	text := append([]byte(requirement), 0)
	stringRef, _, _ := syscall_syscall(api.stringCreate, 0, uintptr(unsafe.Pointer(&text[0])), 0x08000100)
	if stringRef == 0 {
		return errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	defer release(stringRef)
	var req uintptr
	status, _, _ = syscall_syscall(api.requirementCreate, stringRef, 0, uintptr(unsafe.Pointer(&req)))
	if int32(status) != 0 || req == 0 {
		return errors.New("MAC_TUN_NOT_AUTHORIZED")
	}
	defer release(req)
	status, _, _ = syscall_syscall(api.checkValidity, code, 0, req)
	runtime.KeepAlive(text)
	runtime.KeepAlive(keys)
	runtime.KeepAlive(values)
	runtime.KeepAlive(number)
	if int32(status) != 0 {
		return fmt.Errorf("MAC_TUN_NOT_AUTHORIZED")
	}
	return nil
}
