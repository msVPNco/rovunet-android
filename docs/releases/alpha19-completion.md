# Android alpha19 implementation and validation

Owner authorized implementation, self-testing and publication without a preview
hold on 2026-11-11. This applies to Android; panel and desktop work is separate.

## Confirmed subscription findings

The old Import path built a new source for an already saved URL; the transaction
rejected it with SOURCE_EXISTS. Import now resolves the existing source and uses
the same refresh/review transaction as Update. The repository also resolves the
source when called directly, and prevents a URL edit from taking another source's
URL. Local edits, row IDs, secrets, automatic-update choice and failed-download
preservation remain covered. Card and dialog share localized error mapping.

Both current owner-provided feeds were privately fetched successfully and parsed
with the actual alpha18.1 and alpha19 importers: 24/24 primary and 22/22 Windows
entries, no warnings. No private token, response, key or endpoint is copied into
public fixtures. This rules out current feed syntax as a sufficient explanation;
it does not establish why the owner's earlier update failed. The old generic
message alone is insufficient to assign a network, credential or panel root cause.
The Windows suffix is now mapped to the primary sibling account-status endpoint.

## Collection and labels

The published whole-card path explicitly enabled DNS prompting after direct
success. That branch is removed, including for the legacy boolean argument.
Soar/SOAR_SSH already belonged to the resolver tier; no evidence established a
phase-one Soar probe. Previously, excluded members could keep fresh cached
results. All collection members are now made historical before the selected
phase, and only tested candidates receive new results. Cancellation, budget,
identity review and manual DNS tests retain their separate behavior.

Marquee moves only overflowing labels within clipped, fixed single-line bounds.
Short labels stay still; lifecycle and visibility bound animation work. Card and
profile titles, destination/status and account summaries use the shared component.
Buttons and badges retain their own fixed layout. This is not a battery saving
measurement.

## Configuration capability matrix

| Capability | Import | Manual form | Runtime | Data-transfer evidence |
| --- | --- | --- | --- | --- |
| SS2022, Hy2 hopping, supported cs/fm, AWG | Existing support retained | Exposed/completed | Existing pinned engines | Existing interoperability regressions; reported panel WG/Hy2 failure remains separate |
| AnyTLS | Explicit URI subset | Yes | Pinned auxiliary child | Local encrypted TCP/UDP + shutdown |
| TUIC v5 | UUID/password URI subset | Yes | Pinned auxiliary child | Local encrypted TCP/UDP + shutdown |
| Mieru | Single-binding `mierus://` TCP/UDP | Yes | Pinned auxiliary child | Local encrypted TCP/UDP over both transports + shutdown |

The auxiliary engine remains Mihomo commit
`3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8`, GPL-3.0. Only whitelisted proxy settings
enter the child through stdin; no general YAML, scripts, providers/controllers or
second TUN is accepted. It uses authenticated loopback SOCKS, existing process
ownership and cleanup. AnyTLS/TUIC require system certificate validation. Mieru
binary links, repeated/ranged bindings and unknown effective options are rejected
instead of partially imported. Advanced options not shown as controls remain
preserved on a supported edit; unsupported options are rejected before editing.

## Evidence and limits

Before CI: 148 JVM tests pass. Native tests use actual Kotlin runtime generation
and real encrypted TCP/UDP relays for AnyTLS, TUIC and Mieru TCP/UDP, plus existing
AWG 1/2/3.1 tests. The fixture server alone disables its loopback route detector
because the test sandbox denies kernel route enumeration; production validation
and encrypted client traffic are unchanged. CI first builds Go before the Kotlin
JAR exists, then requires actual Kotlin input in the integration stage.

The first CI attempt caught a fixture-order error (the optional Kotlin JAR path
was set before the JAR existed); no production fallback was weakened. A separate
script mode regression in the first source tree was also corrected. Final CI,
artifact and signing identities will be recorded in release provenance.

No physical-phone/carrier, Wi-Fi/mobile handover, weaker-phone responsiveness or
screen-off/idle battery/data measurement was performed in this environment. The
owner's earlier AWG/SS2022/Trojan/VLESS field successes remain historical owner
reports. No new fix is claimed for the own-panel WireGuard/Hy2 failure.
