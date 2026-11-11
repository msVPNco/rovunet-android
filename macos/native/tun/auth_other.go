//go:build !darwin

package main

import (
	"errors"
	"net"
)

func authenticate(net.Conn) (peer, error) { return peer{}, errors.New("MACOS_REQUIRED") }
func rootPeer(net.Conn) bool              { return false }
func consoleUser(uint32) bool             { return true } // Only fake service tests call this on non-Mac hosts.
func secureRootFile(string, bool) error   { return errors.New("MACOS_REQUIRED") }
