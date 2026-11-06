# alpha13-review05 — private candidate / پیگیری خصوصی

Package `app.veyra.client.candidate` (**Rovunet Test**), version `0.3.0-alpha13-review05`, code **23**. This candidate updates review04 using the existing private signing identity. It installs separately from public Rovunet. Public publication requires a separate decision after the phone test.

## Connection lifecycle

The service invalidates the retiring startup's ownership token, closes its pending SSH I/O off Main, and **then** joins the cancelled startup worker. Previously, the service could wait for that worker before reaching the instance-level socket cleanup. Direct SSH, HTTP/SOCKS/WebSocket/TLS wrappers and chain SSH instances receive the same per-attempt token. Cancelled work cannot publish a late connection, and repeated Stop requests retain the ability to close pending sockets even if an earlier teardown action never ran. Authenticated sessions still use the existing normal cleanup path.

Manual connection remains independent of ping/HTTPS tests. No mandatory test destination or preflight was added before Connected. Native transport algorithms, DNS MTU rules, host-key verification and TLS validation remain in their existing paths.

## Interface

- A collection header or arrow only expands/collapses its paths. Opening/closing never connects. Each path has its own connect/cancel/disconnect action; connecting that action selects that exact path without requiring a second tap at the bottom of the screen.
- Collection power uses the remembered manual path, or opens the paths for an explicit choice when no path has been chosen. The saved choice, results and expanded collection survive returning from the editor.
- Cards are shorter. Editing, tests, detailed historical results, pinning, export/QR and delete are available in the path's menu. Historical failures do not replace the live Connected status.
- Protocol choices retain their three categories, use small inline icons and 48dp minimum targets, and reduce columns at larger font sizes. Long help is behind the help button.
- Explicit light/dark surface colors distinguish the background, cards and selected path. The palette uses violet, cyan and ink navy with contrasting text; existing appearance preferences remain available.

## Editor localization

Editor labels, hints, validation, dialogs and option display text use Android resource IDs in Persian, English, Simplified Chinese and Russian. Protocol names, actual parameter values and sample wire syntax stay intact. Technical inputs (addresses, keys, URLs, CIDRs, etc.) explicitly use LTR text; profile names follow the UI language. Plural messages use Android quantities. The UI is split into smaller composition boundaries without adding layout containers.

The editor's source-text guard runs with the independent JVM suite, alongside full resource/format/plural parity. This guard covers direct UI literals; it is not a claim that all inherited screens or arbitrary server error text have been translated.

## Diagnostics

The default report contains typed stages, relative timings, transport categories and numeric SOCKS reply codes. It retains eight attempts in memory and a bounded private cache for the previous process. Traffic events are aggregated so browsing cannot evict the setup/stop timeline. Cancellation is classified separately from authentication, TLS, DNS and timeout failures. Export/copy/share/save controls only use this safe report; no endpoint, account, password, key or configuration link is accepted by its event API. Raw technical logs are optional local viewing and carry a warning; these controls do not export them. Nothing is sent automatically.

SOCKS diagnostics separate TCP readiness, greeting/authentication, CONNECT policy replies and primary/fallback DNS failures. They improve diagnosis; they do not establish why the real server's SOCKS or Soar+SSH path failed. A reachable listener or SSH on another port cannot establish a separate backend's policy, authentication or data path.

## Verification and limits

Build Full Debug candidate plus its Android test APK from one recorded revision. Run Gradle unit tests, the independent real-transport fixtures, resource checks and the strict emulator gate. The gate additionally requires the Android-service cancellation test and four card/RTL editor tests. Its result, exact revision, APK hash and signer belong in the delivery report, not inferred from these instructions.

Native binaries retain their recorded upstream provenance; no fresh native rebuild, production signing migration, GitHub Actions execution, public release or physical-phone result is claimed here. Phone testing must still verify the real SOCKS and Soar+SSH routes, cancellation, DNS transport performance and the new layout before publication.

این نسخه برای نصب و آزمون خصوصی است. تست، شرط اتصال دستی نشده است. رفع کندی لغو SSH از مسیر خود سرویس انجام شده؛ تغییر رابط و ترجمه نیز باید روی گوشی شما بررسی شود. علت قطعی مشکل SOCKS و Soar+SSH روی سرور واقعی هنوز اثبات نشده و نتیجهٔ شبیه‌ساز جای آن را نمی‌گیرد.
