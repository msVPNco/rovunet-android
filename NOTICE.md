# Notice

Rovunet is a modified version of **SlipNet** (https://github.com/anonvector/SlipNet),
Copyright (C) 2026 Anonvector. It is not affiliated with or endorsed by the
SlipNet project.

Both SlipNet and Rovunet are licensed under the GNU Affero General Public
License v3.0; see [LICENSE](LICENSE).

For compatibility, the Kotlin package (`app.slipnet`), the `slipnet://`,
`slipnet-enc://` and `slipnet-bundle-enc://` config formats and the `slipnet`
CLI binary keep their original names. Rovunet's own formats are `rovunet://`,
`rovunet-enc://` and `rovunet-bundle-enc://`.

Third-party components keep their own licenses, including Xray-core
(MPL-2.0), slipstream-rust, dnstt, NoizDNS, VayDNS, Soar, lyrebird, Tor
components and hev-socks5-tunnel. Their sources are included as submodules
or under their original directories.

AmneziaWG profiles use a constrained Mihomo adapter (GPL-3.0), pinned to
`3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8`. Its adapter, build scripts, notices
and pinned upstream source are included with the release source. See
`app/src/main/assets/licenses/Rovunet-AWG-source.txt` and `Mihomo.txt`.
