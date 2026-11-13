# Capability scope — 0.1.7 Beta 1

Current additions: four-language About/channel/support/QR; direct compatible in-app update download; truthful red/green/old-result colors and stale probe protection; encrypted-store concurrency/failure protection; strict direct Mahsa source handling and hidden public source URLs. See STATUS.md, UPDATER.md and TESTING.md for their tested scope and limits.

Mahsa decryption uses ASCII key/IV, Base64 → AES-128-CBC → PKCS7 → strict UTF-8 JSON, mci/mtn config-only deduplication, then the existing Mac importer. Any failed/empty/unsafe result preserves the previous subscription. The live feed currently fails TLS policy; no full working-feed claim is made. Anonymous partial imports retain explicit review/consent.

The per-family matrix PROTOCOL-EVIDENCE.csv separates import/editor, core config, TCP, UDP, DNS, IPv6, reconnect and both Darwin architectures. Its native columns intentionally remain untested for this candidate. Current host tests total2057; physical results of older versions are not reassigned.

All eleven native engines and six dependencies per architecture retain the previous bytes and their existing limitations. The family table below is the retained dev.6 implementation scope, not a new native test. Current package/version/evidence takes precedence over its historical package labels. Open extensions are tracked in BACKLOG.md and TASK-REGISTRY.json.

## Retained family implementation detail

# جدول قابلیت‌های macOS — 0.1.0-dev.6

این جدول بین واردسازی، تنظیم، باینری واقعی Darwin و عبور داده فرق می‌گذارد.
خروجی این نسخه دو معماری Apple Silicon arm64 و Intel x64 دارد: DMG و PKG با مقصد Applications، هر دو با حداقل macOS 15. مالک اکنون اجرای کانفیگ‌ها، از جمله Soar،
را در dev.3 گزارش کرده است؛ این گزارش تأیید همهٔ پروتکل‌ها یا عبور سراسری داده در
TUN نیست. جزئیات سه ویدیو و ایرادهای باقی‌مانده در OWNER-REVIEW-DEV3.md ثبت شده‌اند.
گزارش اجرای dev.1 به این نسخه تعمیم داده نمی‌شود.

| خانواده/قابلیت | واردسازی | ساخت/ویرایش | موتور macOS | عبور داده و آزمون | بسته |
|---|---|---|---|---|---|
| VLESS TCP/WS/REALITY/XHTTP/gRPC/HTTPUpgrade/mKCP | Core مرجع و افزونهٔ مک | فرم و متن؛ encryption، cs/fm، heartbeat، mtu/tti | Xray هر دو معماری | VLESS TCP واقعی، HTTP/SOCKS و IPv6 روی Windows؛ مک انجام نشده | DMG توسعه |
| VMess و Trojan، cs/fm و لهجهٔ VMess URI/gRPC | افزونهٔ سازگار اندروید | فرم، حفظ متن و گزینه‌ها | Xray Darwin | config موتور واقعی Windows تأیید؛ مک انجام نشده | DMG توسعه |
| Shadowsocks چهار روش قدیمی | مرجع Windows | فرم method/password | Xray Darwin | مک انجام نشده | DMG توسعه |
| SS2022 سه روش AES128/AES256/ChaCha20 | طول کلید و زنجیرهٔ کلید بررسی می‌شود | فرم مستقل، اشتراک/JSON/بستهٔ رمزدار | Xray Darwin | هر سه روش از HTTP و SOCKS با دادهٔ واقعی روی Windows موفق؛ مک نشده | DMG توسعه |
| Hysteria2، mport/hop-interval | محدوده و فاصلهٔ پرش معتبر | فرم استاندارد و فرم hopping | Xray Darwin | پیکربندی با موتور واقعی Windows تأیید؛ اتصال مک/پنل انجام نشده | DMG توسعه |
| WireGuard معمولی تک‌همتا | URI و INI | فرم | Xray Darwin | نتیجهٔ ناموفق مالک در Windows رفع‌شده فرض نشده؛ مک انجام نشده | DMG توسعه |
| AmneziaWG تک‌همتا، گزینه‌های سازگار ۱/۲/۳.۱ | URI و INI | فرم مستقل با پارامترهای بازشونده؛ حفظ کلیدها | موتور مستقل Mihomo/AWG، هر دو Darwin | TCP/UDP رمزدار با helper روی Windows موفق؛ زنجیرهٔ کامل Mac Agent و مک انجام نشده | DMG + سورس متناظر و وابستگی‌ها |
| SOCKS5 | Core مرجع | ساخت و endpoint و متن | Xray Darwin | ورودی SOCKS واقعی در fixtureهای Windows؛ مک نشده | DMG توسعه |
| SSH، DNS+SSH، Naive+SSH | Core مرجع؛ pin اجباری | فرم پایه/endpoint و متن پیشرفته؛ تمام کنترل‌های SSH هنوز هم‌سطح Windows نیستند | helper Darwin | fixtureهای Windows با دامنهٔ TESTING؛ مک نشده | DMG توسعه |
| DNSTT، NoizDNS، VayDNS، Soar | Core مرجع | فرم و گزینه‌ها؛ TCP صریح حفظ می‌شود | helper Darwin | آزمون‌های محلی worker؛ Mac E2E انجام نشده | DMG توسعه |
| NaiveProxy | Core مرجع | فرم و متن؛ فایل موقت خصوصی | Naive Darwin | مک انجام نشده | DMG توسعه |
| Slipstream و Slipstream+SSH | Core مرجع | دریافت خودکار گواهی سرور، تأیید اثرانگشت و pin دامنه؛ واردسازی فایل اختیاری | Darwin + OpenSSL خصوصی و helper دریافت هویت | دریافت واقعی گواهی از سرور رسمی در Linux محلی؛ انتقال دادهٔ مک انجام نشده | DMG و PKG توسعه |
| Tor/Snowflake و پل‌های lyrebird | envelope بومی + اعتبارسنجی پل | فرم پل؛ خالی یعنی پل پیش‌فرض رسمی | Tor/lyrebird هر دو Darwin | Tor واقعی Windows config را تأیید کرد؛ bootstrap/دادهٔ مک انجام نشده | DMG توسعه |
| DoH مستقل | envelope بومی | فرم HTTPS و هشدار DNS-only | DNS در Xray، دادهٔ مستقیم | config تأیید؛ مک نشده؛ VPN داده نیست | DMG توسعه |
| اشتراک/فایل/کلیپ‌بورد/QR/JSON | پیش‌نمایش و علت رد؛ گزینهٔ ناشناخته حذف نمی‌شود | ذخیرهٔ ناقص با انتخاب صریح؛ override و نام محفوظ | API بومی Avalonia و مبدل مک | ۱۴ بانک/feed، roundtrip و merge؛ dialog/clipboard مک نشده | DMG توسعه |
| بستهٔ رمزدار | AES-GCM مرجع با مبدل مک | محدودیت اشتراک حفظ می‌شود؛ INI چندخطی در bundle خطی رد می‌شود و JSON مسیر خروجی آن است | بدون موتور | roundtrip و رمز نادرست آزموده | DMG توسعه |
| لینک رمزدار تکی/قفل | PROTECTED_PROFILE؛ محدودیت دور زده نمی‌شود | ناتمام | — | انجام نشده | ندارد |
| شش بانک DNS، Global Resolver و DNS Pool | آفلاین؛ حداکثر ۲۰۴۸ آدرس در هر نوبت | چهار حالت، فرم خانواده، توقف/ادامه، چندانتخابی، فیلتر/خروجی؛ E2E با Agent جدا | helper Darwin | موتور واقعی روی loopback؛ دادهٔ تونل همهٔ خانواده‌ها روی مک تأیید نشده | DMG آزمایشی |
| پراکسی سیستم و بازیابی | — | فعال؛ مجوز macOS | SCPreferences + نگهبان + سابقهٔ رمزدار | ۲۲ آزمون ساختگی؛ API واقعی مک انجام نشده | DMG آزمایشی |
| TUN سراسری | انتخاب صریح در تنظیمات | سرویس محدود root با نصب یک‌باره؛ UI/Agent عادی | utun/gVisor، SOCKS دارای رمز، DNS موقت | TCP/UDP و IPv4/IPv6/DNS و EOF روی Linux ایزوله؛ مک نشده | DMG و Installer داخل برنامه |
| Kill Switch پایدار و تفکیک دلخواه برنامه‌ها | — | ناتمام | — | انجام نشده | ندارد |
| انتخاب دستی، هنگام خرابی و هوشمند | تنظیم مستقل هر مجموعه | زیرمنوی واحد، مسیر دستی محفوظ؛ حد پنج نامزد، پایش/توقف، ماندگاری و بازگشت | کنترل نشست روی موتورهای موجود؛ آزمون نوبتی در TUN | حالت‌ها و لغو/بازیابی با نشست شبیه‌سازی‌شده؛ جابه‌جایی مک انجام نشده | DMG و PKG توسعه |
| زنجیرهٔ دلخواه | ترکیب‌های آمادهٔ مرجع موجودند | ناتمام | نیاز به orchestration جدا | انجام نشده | ندارد |
| AnyTLS / TUIC / Mieru | URI سازگار با اندروید 5e66f167؛ Mieru تک‌اتصال با mierus:// | فرم و تنظیمات پیشرفته، حفظ گزینه‌ها، اشتراک/JSON/بستهٔ رمزدار | helper مستقل Mihomo Darwin arm64 و x64، متصل به پراکسی و TUN | دادهٔ رمزدار هر سه روی loopback در Windows موفق؛ مک انجام نشده | DMG و PKG همراه سورس متناظر |
| پراکسی برای برنامه‌های انتخابی | — | حالت محلی، پورت ثابت، تلگرام/فایرفاکس/سایر؛ پیوند تنظیم تلگرام | ورودی SOCKS5 و HTTP موجود | ذخیره و پیوند و نشانی واقعی آزموده؛ اجرای تلگرام مک انجام نشده؛ مسیریابی اجباری بر اساس فرایند ندارد | DMG و PKG توسعه |

حداقل macOS برابر 15 است. امضای توسعه با Apple Developer ID و notarization تفاوت
دارد؛ نصب کشیدنی و همهٔ پروتکل‌ها روی مک هنوز باید با همین خروجی آزموده شوند.

اسکن کامل بانک‌های بسیار بزرگ ویندوز هنوز هم‌سطح نشده است. سرویس TUN دسترسی Keychain ندارد؛ UDP به پروتکل سرور وابسته است. تأیید رمز مدیر برای نصب سرویس از مجوز login Keychain جداست.



در dev.5، جست‌وجوی آفلاین در ۶۰٬۳۷۰ نشانی بانک پیش‌فرض ممکن است؛ سقف هر آزمون ۲٬۰۴۸ مورد است. ادامهٔ آزمون، انتقال قبلی را دوباره نمی‌سنجد. نتیجه از مسیر کانفیگ، فهرست DNS سراسری یا مخزن به همان مقصد می‌رود. برنامه هنگام اجرا مجوز کلید موجود را تغییر نمی‌دهد و برای بازیابی پراکسی اقدام نیازمند رمز را فقط با انتخاب کاربر انجام می‌دهد. رفتار بومی این بسته روی مک هنوز آزمایش نشده است.

در dev.6 نشان کنار ساعت بر اساس وضعیت اتصال سفید، زرد، سبز یا قرمز می‌شود. گزارش مالک دربارهٔ عملکرد Tor، منو و DNS در dev.5 ثبت شد. رنگ‌های نشان جدید به بررسی روی مک مقصد نیاز دارند.


بستهٔ AppleSilicon-arm64 از نظر معماری برای M2 هم مناسب است و macOS 15 یا جدیدتر می‌خواهد؛ اجرای M2 هنوز آزموده نشده است. تشخیص لینک HTTPS اشتراک در ورود عمومی مطابق موبایل شد. دریافت و واردسازی دو اشتراک مالک روی Windows با ۱۱ و ۱ کانفیگ موفق بود؛ اجرای همین کانفیگ‌ها روی مک تأیید نشده است.


بستهٔ Intel از همان سورس امکانات dev.6 ساخته شده است؛ تمام ۱۱ موتور x64 و کتابخانه‌هایشان از نظر معماری و فایل نصب بررسی شده‌اند. مدل‌های Air/Pro/iMac/mini/Studio/Pro با Apple Silicon از بستهٔ arm64 استفاده می‌کنند. مدل‌های Intel که macOS 15 را پشتیبانی می‌کنند از بستهٔ x64 استفاده می‌کنند. اجرای واقعی همین بسته‌ها روی مک و امضای Developer ID/notarization هنوز تأیید نشده‌اند.

## 2026-11-13 local-review evidence

| Capability | Status | Evidence scope |
|---|---|---|
| Native arm64 PKG upgrade | Observed on Mac | Stock 0.1.7.1, profiles retained |
| TUN HTTPS | Observed for REALITY 1 | Stock 0.1.7.1, HTTPS 200 and utun90; not all protocols |
| Active/recent card first | Implemented in Local Review 1 | 4 ordering regressions within 1442 passing headless checks; native review recorded in report |
| Installer version copy | Corrected locally | Stock mismatch observed in native Installer; local-review copy reviewed |
| Clean install / Intel | Pending | No new evidence |


## Native owner review — 2026-11-13, Local Review 3

Installed exact arm64 package: Rovunet-macOS-0.1.7.4-Local-Review-3-arm64.pkg.
This is a local modified review based on source snapshot 21d41a565acca8dad64ac9d17adefc3fd13f70ad, not a published release. macOS 27.2.1 (26B131), Apple M3 Pro / Mac15,6, arm64, 18 GiB.
Native/UI results belong to this package; older dev6 results are not reused as acceptance.
The staged, installed and expanded package app each contain 491 files with identical SHA256 content maps. Apple codesign --verify --deep --strict passes. Ad hoc integrity is not Developer ID trust or notarization.
Managed suites: 610 checks, zero failures. Final UI suite: 1539 checks, zero failures. Total deterministic suite: 2149; remote feed/path acceptance is separate.
24 supplied atlas paths were attempted with system-wide TUN; 9 passed both TLS-verified domain and pinned-address HTTPS via utun90. Pass: REALITY 1/2, VLESS Encryption, Naive SOCKS5/SSH, StunTLS SSH, direct SSH, AmneziaWG, Shadowsocks 2022. 14 other paths failed real traffic; Slipstream SSH failed handshake before TUN. These are configuration/network-specific observations, not proof entire protocol families are universally unsupported.
Group test ran only 14 non-DNS paths, 12 succeeded; the 10 DNS profiles were not rerun. Stop/cancel and update disabled/success/failure feedback observed natively. Headless tests also cover no-fast-success DNS consent, refusal, cancellation and stale-result suppression.
Normal disconnect restored en0; controlled UI SIGKILL restored en0 within the first ~1.02-second observation, DNS/proxy matched baseline, direct HTTPS 200. Two full quit/relaunch cycles preserved data without repeated password prompt. Login Keychain lock/deny, logout, sleep/Wi-Fi change and reboot remain untested.
Original beta1 DMG mounted read-only, native Finder displayed Rovunet and Applications icons; no drag replacement was performed over the final installed review. Original package welcome/readme stale 0.1.6 version corrected in source templates.
Live feeds: Mahsa TLS_UNVERIFIED safely rejected; Anonymous 108 accepted / 69 rejected. No TLS bypass. Four private supplied feed bodies imported 11/11/1/1 profiles with no rejects; private bodies are excluded from delivery.
Performance samples are short UI-process measurements under a review/automation session, not battery-life benchmarks. Charging was not altered. Sustained energy, large-list performance, Intel native acceptance, full IPv6/UDP remote parity, updater production publication and persistent kill switch remain open.
Formal Developer ID Application/Installer and notarization deferred: no valid certificate/account entitlement available, owner requested deferral if membership/country restrictions are relevant. Do not call the ad hoc build trusted distribution, and do not disable Gatekeeper.


## Final local review 0.1.7.6 — 2026-11-13
Final UI1647 + latest managed639 =2286 checks, zero failures. Network26 includes real native Xray certificate pin correct/wrong/revoked and destination HTTPS validation. Source import and update flows fixed; Patterniha restored; observed persisted source counts Mahsa9/Anonymous166/Patterniha34. Mac-only per-profile certificate approval is bound to profile ID/content and certificate hash. AnyTLS/TUIC custom pin unsupported; no global TLS bypass.
Initial 24-path TUN matrix belongs to0.1.7.4; final0.1.7.6 retested12 paths,9 pass and WS/XHTTP/Trojan fail. Full DNS retest stopped at owner's request to preserve Codex connectivity. Final short local-only Soar test kept route=en0, received HTTPS200 headers but timed out at3s before body. No native Codex process exclusion implemented.
New build required owner-completed Keychain approval (SecurityAgent automation blocked); clean relaunch of the same final build preserved233 profiles without another prompt. Final long-session idle RSS1410MiB; fresh idle204MiB,CPU0.1% one core. Memory retention and active-session energy remain open.
Known visual issue: long unbroken certificate fingerprint can widen/clip RTL warning text. Owner explicitly queued7-second live latency for the next update; not implemented now. Formal Developer ID/notarization remains deferred. See NATIVE-REVIEW-20261113-FA.txt and UI-OWNER-REVIEW-FA.txt.

Local review 0.1.7.10, 2026-11-13. Completed settings/resolver/profile edits return to their origin after a successful save. Search uses close while empty or applied, and execute for a changed query, with Enter support. Import completion still goes Home. GUI retains a session AuthorizationRef for system proxy operations, passed only over inherited IPC to each guardian. Repeated external-form export/import and preferences reads passed in a signed test app, with no system writes. Actual prompt suppression across reconnect/expiry remains a native acceptance item. UI1707 and Network29 passed. The pinned Mihomo Darwin process-table offsets matched controlled TCP/UDP loopback sockets on this host. TUN data path and engine self-capture acceptance remain open.

Local review 0.1.7.11, 2026-11-13. Collection connection feedback is derived from the active, pending or failed child in that collection, updated without rebuilding the list. Add-existing and Update use the same state-bound removal plan and confirmation, with full encrypted-byte preservation on cancellation. Panel status delegates freshness to the shared StatusSnapshot evaluator, with sample time and monotonic session receipt; a restarted window treats old receipts as unverified. Runtime test assembly locations and hashes are recorded by the UI harness, with optional expected-hash assertions. Real TUN, repeated system-proxy authorization prompts, physical network recovery and battery acceptance remain open.

Local review 0.1.7.12, 2026-11-13. All macOS UI controls use the platform default font; technical labels and diagnostics use Menlo. Rendering preference is OpenGL with Software fallback. A native isolated rendering fixture showed correct full-width content and circular controls with Software and fresh Metal; the owner's long-lived Metal window had stretched content and a right-side gap. The fallback is a local mitigation pending extended resize/display/energy validation. TUN native binaries remain from the accepted same-architecture package. Supersedes the intermediate 0.1.7.10 and 0.1.7.11 installers locally.


## Review 0.1.7.12 handoff, 2026-11-13

Exact installed bundle 0.1.7.12 remains a partial local review. Package SHA256 7e82768015643420d4bc160e54bab571e59c307cf85efe343804ce75c42feaad; source ZIP a4fc62b7c5eae0d5002a3ea0631a1956d6b24b411dae698228b0497484d33046. All 491 installed/staged/expanded payload files match; ad hoc deep/strict integrity passes, not Developer ID/notarization. Source snapshot contains 1358 files and matched the checkout before this handoff and subsequent owner follow-up.

Fresh positive scope: 2387 checks across nine suites, plus 99 additional warning-layout checks (actual localized warning, four languages/three themes/320 width) and 3 installer architecture checks =2489. Headless/fixture/TestVault results are not physical UI, operational panel or Keychain acceptance. Intentional wrong assembly hash exits 1; initial harness loader/path/signature and wrong warning-key attempts are retained and excluded from acceptance.

MAC-0179-REMOVE-01, FRESHNESS-01 and PROVENANCE-01 are fixed with scoped evidence. Full endpoint/override/clone/reappearance binding remains open. ACT03-MACOS-01/02/03/04 remain partial. During an already active owner session, Trojan produced valid HTTPS bodies through utun90 and its local HTTP proxy; this is limited fresh evidence, not a full TUN matrix or HY2 acceptance. Initial short Telegram website TLS timeout and later same-session success are retained separately. No test started/stopped system TUN or wrote system proxy preferences.

System-proxy repeated authorization is OPEN. The owner's password dialog requests changes to system settings. Read-only authdb and authd show system.services.systemconfiguration.network -> authenticate-admin-nonshared timeout30; credentials expired at31.434/35.368 seconds. Retaining AuthorizationRef does not solve expiry. Touch ID versus password cause remains unproven; no policy, password storage or Keychain protection was changed.

Native UI performance remains OPEN: home CPU delta41.39%, settings1.23%, home repeat41.45% of one core with the same active connection. Two-minute RSS541-544MiB; sample physical footprint672MiB/peak698MiB. This is not a battery benchmark, leak diagnosis or proof of a renderer cause. Long-duration energy, display changes, recovery/sleep/Wi-Fi/reboot/Keychain deny, Intel, kill switch and native Codex exclusion remain open.

Owner follow-up after freezing the 0.1.7.12 report: pause marquee animation/shared clock without window focus and preserve position on return. Live latency was initially off; it is now observed enabled with refreshed active-card values. Fresh GUI probes were not propagated to the connection-status banner, explaining contradictory unverified text. These code fixes belong to candidate0.1.7.13, not retroactive 0.1.7.12 success. Native acceptance/install of that candidate must be recorded separately; preserve the active connection.


## Installed follow-up 0.1.7.13, 2026-11-13
The owner disconnected the active tunnel and authorized installation. Bundle/receipt now identify0.1.7.13. All491 installed/staged/expanded payload files match, ad hoc deep/strict integrity passes, native helper unchanged. Exact staged DLL UI suite1750 passes; 14 added checks cover focus/clock lifecycle and session-bound live status. Native observation showed live Trojan1239ms at19:03:46 and matching verified hero/collection/footer. Later owner TUN activation was observed, not initiated by the test harness. Marquee pause is installed; native focus/energy A/B remains an acceptance gate. An older network-change notice was also still present and is an open follow-up. Main report0.1.7.12 evidence and its source ZIP are frozen separately; this install does not retroactively fix that artifact.

## 0.1.7.14 observed paths

The US HY2 profile and Canadian HY2, Trojan TLS, VLESS WS and VLESS XHTTP profiles transferred a matching HTTPS body through isolated Darwin packet processing. Ordinary WireGuard remained deferred. These checks concern the specific client profiles and target. System-wide settings, UDP and IPv6 on the production servers, Telegram's native protocol and recovery have separate evidence requirements.
