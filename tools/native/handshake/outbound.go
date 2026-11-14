// Android quick probes use upstream handshakes without opening an external HTTP destination.
package outbound

import (
	"context"
	"errors"
	"strconv"
	"strings"
	"time"

	awgv3 "github.com/metacubex/amneziawg-go/device"
	awgv1 "github.com/metacubex/amneziawg-go/device_v1"
	wg "github.com/metacubex/wireguard-go/device"
)

func (p *autoCloseProxyAdapter) RovunetHandshake(ctx context.Context) error {
	handshaker, ok := p.ProxyAdapter.(interface{ RovunetHandshake(context.Context) error })
	if !ok {
		return errors.New("unsupported protocol handshake")
	}
	return handshaker.RovunetHandshake(ctx)
}

// RovunetHandshake authenticates the remote transport, not the Internet data path.
func (t *Tuic) RovunetHandshake(ctx context.Context) error {
	conn, err := t.dial(ctx)
	if err != nil {
		return err
	}
	defer conn.CloseWithError(0, "")
	select {
	case <-ctx.Done():
		return ctx.Err()
	case <-conn.Context().Done():
		return errors.New("QUIC connection closed")
	case <-conn.HandshakeComplete():
		return nil
	}
}

func (h *Hysteria2) RovunetHandshake(ctx context.Context) error {
	// ListenPacket performs Hysteria's own authenticated QUIC offer. No destination
	// packet or external HTTPS request is sent.
	conn, err := h.client.ListenPacket(ctx)
	if err != nil {
		return err
	}
	return conn.Close()
}

func (w *WireGuard) RovunetHandshake(ctx context.Context) error {
	if err := w.init(ctx); err != nil {
		return err
	}
	if err := w.device.(interface{ Up() error }).Up(); err != nil {
		return err
	}
	key := w.option.PublicKey
	if len(w.option.Peers) == 1 {
		key = w.option.Peers[0].PublicKey
	}
	if len(w.option.Peers) > 1 {
		return errors.New("one peer required")
	}
	var send func() error
	switch dev := w.device.(type) {
	case *wg.Device:
		var public wg.NoisePublicKey
		if err := public.FromHex(key); err != nil {
			return err
		}
		peer := dev.LookupPeer(public)
		if peer == nil {
			return errors.New("peer missing")
		}
		send = func() error { return peer.SendHandshakeInitiation(false) }
	case *awgv1.Device:
		var public awgv1.NoisePublicKey
		if err := public.FromHex(key); err != nil {
			return err
		}
		peer := dev.LookupPeer(public)
		if peer == nil {
			return errors.New("peer missing")
		}
		send = func() error { return peer.SendHandshakeInitiation(false) }
	case *awgv3.Device:
		var public awgv3.NoisePublicKey
		if err := public.FromHex(key); err != nil {
			return err
		}
		peer := dev.LookupPeer(public)
		if peer == nil {
			return errors.New("peer missing")
		}
		send = func() error { return peer.SendHandshakeInitiation(false) }
	default:
		return errors.New("unsupported WireGuard device")
	}
	if err := send(); err != nil {
		return err
	}
	ticker := time.NewTicker(25 * time.Millisecond)
	defer ticker.Stop()
	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-ticker.C:
			// This private UAPI snapshot includes key material: inspect it in memory only.
			status, err := w.device.(interface{ IpcGet() (string, error) }).IpcGet()
			if err != nil {
				return err
			}
			for _, line := range strings.Split(status, "\n") {
				if value, ok := strings.CutPrefix(line, "last_handshake_time_sec="); ok {
					seconds, err := strconv.ParseInt(value, 10, 64)
					if err == nil && seconds > 0 {
						return nil
					}
				}
			}
		}
	}
}
