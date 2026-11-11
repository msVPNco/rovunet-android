# alpha13-review04 — private candidate / پیگیری خصوصی

Package: `app.veyra.client.candidate` (**Rovunet Test**), version `0.3.0-alpha13-review04`, code **22**. This is a private candidate, not a public GitHub release. It retains the private candidate's signing identity and does not upgrade the separate public Rovunet package. The delivered verification report records the exact source commit, APK hash, signer, tests actually completed and limits.

## Changes

- SSH startup resources belong to a single attempt and are registered before blocking I/O. Stop closes pending sockets as well as authenticated sessions; stale failures cannot stop a replacement or publish a late listener.
- The same ownership applies to direct SSH, raw DNS tunnels, SOCKS/Naive, HTTP CONNECT, WebSocket, TLS and payload wrapping. TLS validation, SNI, host-key approval, SSH cipher settings and WebSocket framing remain in their transport paths. Existing authenticated-session keepalive timeouts are left to JSch.
- DNS name lookup during startup has a bounded wait and bounded resolver workers. Stop releases the caller even if the OS resolver ignores interruption; an outstanding OS lookup cannot dial or publish a cancelled connection after it returns.
- The configured remote DNS fallback reaches the SOCKS bridge in single, wrapped/chain and reconnect call sites that use that bridge. It is tried at most once after a transport failure, invalid response, SERVFAIL or REFUSED. Valid NXDOMAIN is returned as a negative answer, not treated as a timeout. No new cleartext/direct DNS fallback is introduced.
- DNS-over-TCP accepts IPv4, IPv6 or server-resolved DNS hostnames, uses a socket deadline, avoids caching transient resolver failures, and cannot retry/cache results through a replacement bridge run.
- Connecting manually remains independent of ping/HTTPS tests. There is no new preflight gate or automatic connection rejection based on a test destination.

## Import compatibility / سازگاری ورود

Formats v1–v33 remain explicitly supported. Since the source hardening follow-up, an **unknown format version is rejected**, including inside an encrypted bundle. It is never partially interpreted using the v33 parser. A mixed import can retain supported rows with a warning. If a file was produced by a newer format, update the client from a trusted distribution; a malformed/nonnumeric version needs correction by its sender rather than a blind update.

نسخهٔ ناشناختهٔ کانفیگ دیگر ناقص وارد نمی‌شود؛ همراه هشدار کنار گذاشته می‌شود. برای قالبی که متعلق به نسخهٔ جدیدتر برنامه است، کلاینت را به‌روز کنید. فایل خراب یا برچسب نسخهٔ نامعتبر باید توسط فرستنده اصلاح شود. این تغییر رفتار در مقایسه با خواندن حدسیِ نسخه‌های آینده عمدی است.

## Verification contract

Run Gradle unit tests and the independent transport contracts. Build Full Debug candidate and its instrumentation APK from the recorded source revision; run `tools/veyra/run-device-gate.py` against those exact artifacts on a disposable emulator. The device gate requires the new `SshStartupCancellationDeviceTest` and rejects skipped, failed, missing or incomplete instrumentation results.

Linux transport tests exercise silent peers in nine SSH transport modes, stale-start replacement, no late listeners, idempotent Stop and descriptor counts after provider warmup. Real `FWD_UDP` fixtures exercise primary/fallback selection, fragmented replies, valid negative DNS answers, transient failures, cache transaction IDs and cancelled-run isolation. These are local fixtures, not the user's server or censorship conditions.

## Limits and next decision

The defects are established independently of the reported symptoms. It remains unproven that either caused the user's SOCKS no-data or Soar+SSH setup problem. Validate those two paths, direct SSH and STunTLS on the real phone/network before public publication. A reachable Soar listener does not establish that the server allows the requested SSH backend.

Four-state, session-scoped background data-path verification is a separate product change. A blanket 2–3 second deadline is inappropriate for slow DNS transports. Structured, actionable live-flow REP messages and broader UI error taxonomy are also separate work; existing SOCKS reply forwarding and debug diagnostics are retained in this candidate. Neither feature is being used as a new connection gate.

Native binaries are reused with recorded provenance; no fresh native rebuild, production signing migration, GitHub workflow run, public/store publication or physical-phone test is claimed by the build instructions themselves.

در این پیگیری، اولویت با بستن باگ قطعی توقف و وصل‌کردن DNS پشتیبان است. وضعیت چهارحالتهٔ تأیید عبور داده و توسعهٔ سراسری پیام‌های خطا به این اصلاحات اضافه نشده‌اند. نتیجهٔ گیت محلی جای آزمون گوشی و سرور واقعی را نمی‌گیرد؛ انتشار عمومی پس از بازخورد میدانی تصمیم‌گیری می‌شود.
