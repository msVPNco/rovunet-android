# Rovunet macOS workspace

Keep macOS source, work, evidence and deliveries under this Mac workspace.
The existing Windows and Android projects are read-only references. Never write
macOS outputs into their folders. The portable Core is compiled through links
inside macos/, so its obj/bin output also remains inside macos/.

GitHub is read-only: no push, remote modification, PR, workflow dispatch, merge,
tag or publication. Work only in the existing msVPNco/rovunet-app repository's
macos/ directory. Preserve all other tracked platform files.

Use Windows source 562a3d71670ceb7de909146bf68116d5cc614496 for layout and behavior;
Android APK source e6359aae2e2e19164ad7e9ec87267f09a0c1152d for compatibility gaps.
Three private theme references are in reference/themes. Do not put their endpoint
addresses or private images in shipped packages or source archives.

Primary target: Apple Silicon. Record Intel separately. The host is Windows;
cross compilation and headless UI checks never establish actual macOS behavior.
Keep secrets in Keychain-encrypted storage and IPC, not argv or logs. Manual
connection is independent of ping/probes/scans. Preserve TLS/SSH, locks and TCP.

Update macos/docs/STATUS.md, TESTING.md, BACKLOG.md and capability matrix on handoff.
Do not claim complete parity, working TUN, successful macOS network traffic,
signed installation or notarization without evidence from that exact package.
