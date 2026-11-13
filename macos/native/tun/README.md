# Native TUN adapter

Build inside the exact pinned Mihomo tree, using `-tags with_gvisor`. A build
without that tag compiles a stub that cannot start the chosen TUN stack.
`scripts/build.ps1` copies this directory and uses the required tag.

The ordinary native `--client` accepts only bounded start/status/stop messages
on inherited stdin. A launchd service authenticates its console UID, kernel PID
and hardened CodeDirectory hash using Security.framework. The service accepts no
profile, executable name, shell command, file path or general Mihomo config.
It owns one short-lived worker and stops it on socket EOF or missed heartbeat.

The worker creates utun, uses gVisor, forwards through authenticated loopback
SOCKS and hijacks DNS. Only the fixed bundled engines bypass their own TUN to
avoid a loop. All other traffic matches the selected proxy. DNS uses DoH through
that proxy with no direct/system fallback. Darwin's native DNS registration is
temporary and does not rewrite the user's persistent DNS configuration.

The GUI and managed Agent run as the user. Only this restricted service/worker
runs as root. Installer authentication is needed once per helper update, not
per connection. The login Keychain key never enters this helper.

Linux namespace tests exercise TCP/UDP over IPv4/IPv6, DNS hijack, SOCKS auth,
route cleanup and production-worker EOF. Their Xray fixture uses concrete UDP
source addresses for Linux process lookup. They do not validate macOS process
lookup, temporary DNS, Security.framework, Installer or signing trust.
UDP still requires support from the selected remote protocol. There is no
persistent kill switch. A DNS-only direct DoH profile is refused in VPN mode.

## Darwin development helper 0.1.0-dev.4

Before creating a TUN, the worker verifies its per-lease loopback SOCKS credentials. The service validates the native client's code identity using its kernel socket audit token. The engine's exact process path takes precedence over ordinary port 53 traffic. Regular DNS goes through the selected SOCKS relay. Recognized engines include rovunet-extra. The parent preserves bounded startup or restore errors from the worker.

Darwin packet tests use a datagram socket pair and the production gVisor and sing-tun backend. They install no routes or DNS settings. Build the Go tests in the pinned Mihomo tree with its vendor dependencies, CGO_ENABLED=0, with_gvisor and -ldflags=-checklinkname=0. Set ROVUNET_PACKET_TEST_XRAY to the packaged Xray executable. Build tests/Rovunet.TunFixture in Release and set ROVUNET_PACKET_CONFIG_DRIVER to its apphost (DOTNET_ROOT must identify the matching runtime). ROVUNET_PACKET_TRANSPORT accepts ws, xhttp, trojan or hy2 for the synthetic tests. TestOperationalDarwinPacketPath is an explicit opt-in for an owner-supplied profile on stdin and does not log or store the link. Its target is example.com over standard TLS. A full Darwin route/SCPreferences recovery test is a separate acceptance gate.
