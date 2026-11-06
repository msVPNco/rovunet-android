# alpha14 — interface and diagnostic follow-up

Prepared application version: `0.3.0-alpha14`, version code **24**. The independently installable validation package uses `app.veyra.client.candidate`, label **Rovunet Test**, version `0.3.0-alpha14-review01`. This note does not establish that a public APK has been signed or published.

## Interface

- Expanded collection headers use a stronger violet surface and elevation over an inset tray. Paths use narrower, lighter surfaces and thinner outlines in light, dark and AMOLED appearances.
- First tap selects a path; tapping that selected path connects it directly, without a prerequisite ping. Tapping a connected path does not disconnect it. Busy tests/start/stop operations do not dispatch another connection.
- Editing and deleting have separate buttons. Connection cancellation/disconnection remains available through the collection power button, the main control and the path menu. Nested action buttons do not trigger the path's tap action.
- Verified HTTPS measurements have a green latency badge. Measurements from a previous network remain neutral; a latency result is not a download-speed measurement.
- Soar's actual `auto`, `on`, `only`, `off` values have distinct labels in a two-column selector: Automatic, UDP + TCP, TCP only, UDP only. The earlier renderer mistakenly compared those values with different names, displaying Automatic for all four options. Values and transport behavior remain unchanged.

## Diagnostic report

The supplied review05 phone report shows successful SSH and Soar+SSH establishment and bounded cancellation, plus failed remote SOCKS DNS requests. It does not establish server blocking, a particular policy rejection, or the cause of missing Soar traffic counters.

Safe report v3 adds greeting wait/failure and DNS TCP-ready/query-sent stages. A socket closed by the existing DNS deadline is identified as TIMEOUT instead of a generic failure. Per-attempt snapshots of the UI traffic counters help separate a counter problem from an unresponsive data path; these counters do not declare Internet access verified. No server address, account, key or configuration link is added. No mandatory connection test, DNS algorithm or MTU change is introduced.

## Publication

Public alpha11 used a debug signing identity. Public builds require the independently approved production certificate and keystore documented in `docs/RELEASING.md`; neither was available while preparing this revision. A production signer must not be silently substituted for the existing key: Android will reject an ordinary in-place update without a validated signing migration. An independently signed/private candidate is not a public update to alpha11. Publication and updater claims must match the actual signed artifact and migration decision.

نتیجهٔ آزمون‌ها، شناسهٔ دقیق سورس و هش APK در گزارش ساخت ثبت می‌شود. وجود این یادداشت به‌معنی انتشار عمومی یا تأیید مسیر SOCKS/Soar روی سرور واقعی نیست.

## Source follow-up after independent review

This source-only follow-up recommends UDP + TCP for newly created Soar and Soar+SSH
profiles. Existing/imported values remain unchanged. The automatic-mode hint in
all four languages now distinguishes gradual fallback from trying both transports
at startup. Safe report v4 records requested/configured Soar policy without claiming
that a listener, handshake or data path is verified. Existing typed SOCKS reply
events are regression-tested rather than duplicated. See
`docs/reviews/alpha14-independent-response-fa.md` for accepted and rejected claims,
test evidence and remaining field/signing limitations. These changes are not in the
previously supplied review01/code 24 APK, and do not constitute a new public release.

The subsequent product decision extends the initial Soar+SSH recommendation to
plain Soar too. Toggling SSH on a new profile keeps this default unless a user has
explicitly chosen another mode. Existing records and imported wire values keep
their policy; the legacy model/import fallback remains `auto` for compatibility.
See `docs/reviews/alpha14-field-test-boundaries-fa.md` for the remaining field-test
limitations and the decision to pause speculative source-review iterations.
