# Isolated Windows networking laboratory

This package is separate from the normal Rovunet installer. It is not enabled or advertised as a consumer VPN or certified kill switch. Every native write additionally requires an administrator-protected, machine-bound isolated-VM marker and a protected Program Files installation. Never run it on the owner's working Windows network or copy personal profiles into the lab.

## Implemented in preview.6

- An SCM LocalSystem service with only SeChangeNotifyPrivilege, SeLoadDriverPrivilege and SeImpersonatePrivilege. No SeDebugPrivilege. The GUI/controller may exit without terminating the service session. Loss of the verified upstream or TUN core ends live-interface permits and reports a failed service for SCM restart/recovery; stale process callbacks are ignored. Opted-in baseline protection remains until explicit recovery.
- Local authenticated named pipe, restricted ACL, enabled administrator token checks, owner SID, actual client/server PID and session, protected image path, SHA256 manifest and fixed bounded request schema. No command/executable/DLL/service/route injection API. Only the configured owner can start; a different elevated administrator may inspect/stop/recover.
- Fixed Xray/Wintun TUN, IPv4/IPv6 routes and owned-adapter DNS-over-HTTPS. Version2 explicitly enables UDP; version1 cannot enable it. UDP defaults off and requires a compatible selected SOCKS upstream. Before native mutations, a bounded loopback SOCKS5 UDP ASSOCIATE check and listener-PID recheck reject a TCP-only local listener. The Agent opt-in is currently limited to standalone SOCKS5; other protocol families/chains retain an explicit unverified-UDP gate pending their own end-to-end data tests. A TCP-only SSH/DNS transport does not become UDP-capable through this switch.
- Journal before native mutations, exact route reconciliation, protected machine/installation/GUID-bound adapter receipt and explicit recovery. Conflicting or ambiguous foreign state is retained for investigation, not overwritten.
- Persistent baseline WFP blocks and narrowly scoped selected-peer/engine permits. Live TUN permits use a dynamic WFP session and expire on broker crash; stale LUIDs do not retain a permit. Existing Windows Firewall policy remains effective.
- Service installation, controlled fresh-payload upgrade/rollback and removal scripts. No recovery files should be removed while owned protection or an unfinished journal remains.

## Evidence and limits

Actual Windows26100 QEMU/KVM VM trials have demonstrated synthetic TCP16KiB/UDP1200byte payloads in both IP families, validated DoH over HTTP/2, active direct-egress blocking and recovery, and service-crash protection/restart. Exact candidate builds, failed attempts and bounded captures are recorded separately in the version's acceptance report. Read-only/config checks are not those native tests. Recheck the delivered file hashes against the final acceptance report; a source/build label alone is insufficient.

TrafficVerified deliberately stays false: a local ready adapter is not a user Internet verification. TunReady, ProtectionInstalled, UdpForwarding and recovery-required are separate states. A persistent WFP filter is not a BOOTTIME filter. No guarantee is made for the interval before BFE starts, all IPv6 extensions, arbitrary helpers/peer discovery, or all hardware/network/sleep scenarios. Do not present this as complete production protection.

The initial upstream permit is the pinned Xray image at explicit bounded peer IPs. Other native helper executables, dynamic Tor peers and arbitrary chains need a separately constrained plan and acceptance. The real Agent can be exercised with a synthetic SOCKS profile by the protected lab controller; this does not enable the feature in the ordinary WPF interface.

## Controlled service workflow

After a fresh isolated VM snapshot, use synthetic endpoints, positive TCP/UDP controls and a VM-NIC capture. As VM administrator, run install-service-lab.ps1 -LabSource <fresh-verified-payload> -ConfirmIsolatedVm -OwnerSid <synthetic-test-account-SID>. The directory is fixed under Program Files; marker/policy are generated without UTF8 BOM for Windows PowerShell5 compatibility. A protected service policy is not part of the download manifest.

From that configured test account use Rovunet.NetworkLab.exe --service-start <SOCKS-port> <peer-IP[,IP]> --accept-isolated-lab [--udp] [--kill-switch]. Use --service-status, --service-stop or --service-recover from the same installation. An enabled administrator from another account may inspect/recover, but cannot start the owner's tunnel. Filtered administrator tokens do not gain that recovery role merely from group membership.

Upgrade only when inactive with no orphan protection or journal: update-service-lab.ps1 -LabSource <fresh-verified-payload> -ConfirmIsolatedVm. The source must be outside the installed root; the old protected directory is retained. -InjectFailureAfterSwap is an explicit VM-only rollback fixture. remove-service-lab.ps1 refuses active/ambiguous state unless successful explicit recovery is requested. Its optional -RemoveFiles targets only the verified exact lab root, never Program Files itself or profiles.

The legacy interactive --start/--status/--recover controller is still a developer-only same-SID elevated path. It is not the service API and does not provide cross-account elevation. Closing an opted-in controller does not authorize releasing protection.

## Pinned dependencies and provenance

Xray SHA256 d5b558191cccb1e92a5772b68b8cab67717b26f634b0f15153215f65ae982a7e. Wintun0.14.1 archive from https://www.wintun.net/builds/wintun-0.14.1.zip SHA256 07c256185d6ee3652e09fa55c0b673e2624b565e02c4b9091c79ca7d2f24ef51; unmodified amd64 DLL SHA256 e5da8447dc2c320edc0fc52fa01885c103de8c118481f683643cacc3220dafce. Windows verified the original WireGuard LLC signature. The full binary licence accompanies the lab. This does not sign Rovunet's installer or create a trusted Rovunet publisher. Native WFP identifiers are checked against an independently pinned Microsoft SDK snapshot included in the source.

Required acceptance still includes actual final-byte trials of ordinary/foreign identities, invalid IPC/manifest/ACLs, native data/leak controls, upstream/core/service loss, reboot/BFE, link/DHCP and hardware sleep, fresh adapter ownership, interrupted journal operations, upgrade rollback and uninstall. Missing measurements remain unaccepted; no pass is inferred from a mock, a paused VM, a listening socket or an unrelated historical build. Keep tokens, passwords, CA private keys, TPM/disk state and raw private reports outside public source and assets.