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
