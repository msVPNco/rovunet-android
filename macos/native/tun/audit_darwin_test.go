//go:build darwin

package main

import (
	"net"
	"os"
	"path/filepath"
	"testing"
)

// Verify a real peer's kernel token and Apple Security.framework requirement
// without connecting to the installed daemon or touching system settings.
func TestAuditTokenIdentifiesSocketPeer(t *testing.T) {
	const deny = "identifier \"org.rovunet.fixture.not-the-test-process\""
	path := filepath.Join(t.TempDir(), "c")
	if len(path) > 99 {
		t.Fatal("unexpected socket path length")
	}
	listener, err := net.Listen("unix", path)
	if err != nil {
		t.Fatal(err)
	}
	defer listener.Close()
	client, err := net.Dial("unix", path)
	if err != nil {
		t.Fatal(err)
	}
	defer client.Close()
	peer, err := listener.Accept()
	if err != nil {
		t.Fatal(err)
	}
	defer peer.Close()
	token, err := peerAuditToken(peer)
	if err != nil || int(token[5]) != os.Getpid() || int(token[1]) != os.Geteuid() {
		t.Fatalf("missing or unrelated kernel token: %v", err)
	}
	if err := checkGuest(token, "true"); err != nil {
		t.Fatalf("signed test process lookup failed: %v", err)
	}
	if checkGuest(token, deny) == nil {
		t.Fatal("unmatched code identity accepted")
	}
}
