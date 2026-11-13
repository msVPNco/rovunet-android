# Rovunet user guide — alpha20 / راهنمای کاربر

**Alpha21 Full / code46 · prerelease:** Account/source binding and revision high-water, independent private/advisory cache, additive Room50→51, partial metadata without invented usage and diagnostic ownership. [Release, source and limitations](releases/alpha21.md). Same production signer; install over the app without uninstall/clear-data. Seven Room methods and174 JVM tests passed; real main-upgrade/Keystore and device/network/Doze gates remain open. This preview is not Latest; alpha20 is retained.


**Alpha20:** Import refreshes an existing URL without deleting its source. Direct success finishes collection tests without a DNS prompt; resolver fallback requires failure and consent. Long labels move inside fixed single-line bounds. AnyTLS, TUIC v5 and single-binding Mieru `mierus://` have import, manual and runtime support. Unsupported effective settings are rejected explicitly. [Details](releases/alpha20.md).

Existing capabilities retained in code43 include subscription updates on the collection card, optional names, provider-reported usage/expiry, supported `cs`/`fm`, Shadowsocks 2022, Hy2 port hopping and single-peer AmneziaWG import. It retains the calm Soft Pop themes, visible-only latency monitoring, bounded smart recovery, isolated Slipstream resolver workers and native stop improvements. Patterniha is removed from the built-in catalog; existing imports are preserved.

Automated local/emulator checks do not establish physical-phone battery savings, overnight/Doze stability, carrier reachability or repair of the reported panel WireGuard/Hy2 endpoints. Owner accepted this exact APK for publication; no detailed phone/carrier/battery measurements were supplied. AnyTLS, TUIC v5 and single-binding Mieru are included; see alpha20 details. No new Lite or desktop binary is published.

[English](#english) · [فارسی](#فارسی) · [简体中文](USER_GUIDE.zh-CN.md) · [Русский](USER_GUIDE.ru.md)

## English

Alpha20 Full / code43 adds a themed Rovunet startup cover and removes redundant startup digest work; direct encrypted Mahsa fetching, public-source URL privacy, secure active-tunnel update downloads, bounded cancellable file import, red failure labels and small English legal attribution. It also fixes SOCKS domain-length handling in the constrained auxiliary engine. Alpha19 capabilities are retained; this version adds no new protocol.

**Subscriptions:** choose Update on the collection card to update in place; progress blocks repeated clicks and the outcome stays on Home. Existing-profile replacement still requires a disconnected VPN. Names are optional: explicit/saved name, safe provider title, then hostname. `subscription-userinfo` reports upload + download, volume and expiry from the same response; missing fields stay unknown. Provider snapshots show their age and are not independent metering. No additional periodic full-feed downloads are introduced. Panel status retains its separate contract.

**Imports:** supported VLESS/VMess/Trojan `cs` and `fm` are preserved with verified TLS; unsupported masks/scripts/settings are rejected explicitly. Shadowsocks 2022 validates key lengths. Hy2 supports bounded `mport` lists and `hop-interval`. Standard WireGuard sharing URIs and single-peer AmneziaWG `.conf`/supported URI parameters are accepted. AWG runs in a separate authenticated userspace engine; one address per IP family, no scripts, multi-peer or ranged keepalive. Arbitrary core/Mihomo YAML is not executed.

**Direct upgrade for public alpha14–alpha16 (through code32):** download the [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk) and install over the existing app **without uninstalling or clearing data**. The old updater embeds the former repository URL; web redirects cannot repair that check. Package and production signer are unchanged.

Exact current checks and artifact identities: [alpha20 provenance](releases/alpha20-provenance.json).

HTTPS subscriptions have **Update** and **Import** actions. Import adds a new source once and closes the dialog after success; errors preserve the form, reimporting a saved URL refreshes the existing source, and updates preserve local changes. Existing-source updates require VPN disconnection; removals require review. Optional refresh runs approximately every 12 hours under suitable network/battery conditions. Collection QR/PNG sharing is supported when the payload fits; larger collections use files.

Soar scanning offers Quick, Advanced, E2E and Prism. Quick/Prism use authenticated Soar handshakes with the configured Ed25519 key; E2E also checks HTTP 204 or the SSH account with existing trust approval. Advanced is diagnostic DNS only. UDP and TCP are tested independently; Soar scanner defaults to both without rewriting saved connection choices. Imported candidates survive discarded foreign/legacy proof.


Panel v1.19 account status shows remaining GiB and time in the existing collection card. Amber means ≤20% remaining or ≤3 days; red means ≤5% or ≤24 hours. Unlimited, unknown and expired are distinct. A 404 or stale/incomplete counting is unavailable information, not proof of expiry. Status refresh runs while Home is visible and never blocks manual connection. Server-side metering and limits remain authoritative.

Soar restores repeated protocol handshakes and BOTH waits for both UDP/TCP results. Preserve explicit TCP-only and trust checks. Scan success in local fixtures is not proof of resolver availability on a particular network.

### Installation and updates

The version is **0.3.0-alpha20 Full, code 43**. Download the signed [published Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk). Public alpha14 can be updated in place: package and production signer are unchanged. Do not uninstall as part of this update. Historical alpha11 and private Rovunet Test have different identities; see [migration](UPGRADE.md). Unsigned preparation files are not installable.

Android 7.0/API 24 is the minimum. ARM32/ARM64 and x86_64 are build targets, but inherited Naive/Tor binaries are absent on x86_64. No new Lite APK is promised. Google Play, App Store and Rovunet desktop GUI versions are not available.

Before an upgrade, read its signer notes and retain your readable installation. Password-encrypted bulk export excludes locked profiles; unshareable/device-bound profiles may require original links or operator reissue. Do not clear application data or uninstall as a routine troubleshooting step. [Migration](UPGRADE.md).

Check for updates in Settings downloads a compatible published GitHub APK inside the app, verifies checksum/package/version/signer, then asks Android to install it. GitHub must be reachable. Installing is never silent, and an unrelated signer is rejected. Public preview inclusion is optional; private candidates are delivered separately.

### Import and collections

Use **+ → Import** for text, supported files or QR, review the preview and confirm. Supported formats include native `rovunet://`, compatible `veyra://`/`slipnet://`, supported VLESS/Hysteria2 links and single-peer WireGuard INI. Native export is v33. Unknown future versions are rejected: update the client instead of editing away version/security fields.

Collections retain related paths, explicit choices and completed measurements. Tap the collection header/chevron to expand or collapse. Tap a path once to select it and again to connect; a remembered path is already selected after returning. Edit/delete/menu controls are separate. Collection power starts its remembered path or an eligible initial path, without an automatic ping. No manual choice is silently replaced by another protocol.

### Connect, trust and stop

Grant the Android VPN permission when requested. Connect starts the selected transport directly: no ping, HTTPS test or resolver-pool scan is required. For an unknown SSH or self-signed identity, compare the fingerprint with the operator before approval. Background tests do not approve identities automatically; a changed identity is not silently accepted.

Use the main/collection power control or path menu to cancel Connecting or disconnect. Previous setup is cancelled and I/O closed before replacement. Report persistent stopping rather than repeatedly tapping Connect. Connected means the transport/VPN is ready; it is not proof of access to every site. Idle zero counters alone are not a reason to disconnect.

### Tests, DNS and Soar

Run an explicit collection test while disconnected. It checks HTTPS/204 through each actual tunnel, with certificate checks and a limited fallback target. Measurements show last-test time, success and latency spread. They are not download-speed measurements; a historical failure does not block manual connection. Results from another network are identified separately.

Direct paths come before UDP/DNS fallback. DNS testing/discovery can take minutes and is cancellable; consent is required before entering the DNS fallback phase. Manual DNS paths use configured resolvers without a prerequisite pool scan.

New **Soar and Soar+SSH default to UDP + TCP**. Existing/imported values remain unchanged. Automatic starts with UDP and can later add TCP for repeatedly failing unverified paths; it is not equivalent to UDP + TCP from startup. Keep the mode that works on your network. SSH-over-Soar needs server permission for the SSH destination/port in addition to a working base tunnel.

### Credentials, sharing and appearance

Stored secrets use Android Keystore-backed encryption. If a record becomes unreadable, it is marked for explicit credential recovery rather than silently erased; recover from a matching original configuration/operator. Keystore storage is not a portable backup after uninstall.

Locked profiles and collection sharing restrictions remain effective in supported export paths; a lock is a client policy, not a guarantee against a modified client. Use a strong password for portable backups. Verify a backup can actually be imported before relying on it.

Choose Persian/English/Simplified Chinese/Russian and light/dark/AMOLED in Settings. Main settings/editor resources are translated; scanner labels, mapped errors and the connection-selection guide use localized resources. Protocol identifiers and addresses stay unchanged. The notification/widget/quick-settings controls depend on Android permissions and launcher behavior.

### Troubleshooting and limits

Alpha15 fixes Go/JNI members removed by Release shrinking. If a fresh crash occurs after updating, reopen the app and share the safe report with its timestamp; historical exit records can remain visible after an update.

Include app/Android versions, protocol, reproduction steps and whether ordinary pages open. The safe connection report records typed stages/codes, configured Soar mode and UI traffic snapshots without address/account/credential fields. It is local and shared only by user action. Detailed inherited debug logs can contain sensitive data; review/redact them before sharing. Do not post real config links, passwords, private keys or device IDs in public Telegram/issues.

Remote SOCKS data flow and Soar auto/on differences still need field diagnosis. SOCKS CONNECT success is not proof of payload transfer; DNS failure does not prove censorship or server ACL. DoH alone encrypts DNS; ordinary SOCKS5 does not encrypt transport. Automatic recovery is opt-in and bounded; manual mode never switches. Throughput benchmarks are not included. [Status](STATUS.md) · [Telegram](https://t.me/RovunetVPN).

<div dir="rtl">

## فارسی

آلفا۲۰ Full، کد ۴۳: صفحهٔ آغاز برند Rovunet در سه تم و چهار زبان، کاهش کار اضافی هنگام خواندن پروفایل‌های ذخیره‌شده، دریافت مستقیم و رمزگشایی منبع مهسا، پنهان‌شدن نشانی خام منابع عمومی، دانلود امن آپدیت از تونل فعال، واردسازی فایل قابل لغو و محدود، نمایش قرمز خطا و متن حقوقی کوچک انگلیسی. اصلاح طول دامنه در SOCKS موتور کمکی نیز اعمال شده است. قابلیت‌های آلفا۱۹ حفظ شده‌اند؛ این نسخه پروتکل تازه‌ای اضافه نمی‌کند.

قابلیت‌های حفظ‌شده در آلفا۲۰ شامل آپدیت اشتراک روی همان کارت، نام اختیاری، نمایش مصرف و انقضای اعلام‌شدهٔ ارائه‌دهنده، گزینه‌های سازگار `cs`/`fm`، Shadowsocks 2022، جابه‌جایی پورت Hy2 و ورود AmneziaWG تک‌همتا را اضافه می‌کند. تم‌های آرام Soft Pop، سنجش تأخیر هنگام نمایش اپ، بازیابی هوشمند محدود، اسکنرهای مستقل اسلیپ‌استریم و اصلاح توقف بومی حفظ شده‌اند. پترنیها از فهرست داخلی حذف شده؛ ورودی‌های قبلی کاربر حفظ می‌شوند.

**اشتراک‌ها:** آپدیت روی کارت، همان‌جا انجام می‌شود؛ پیشرفت جلوی کلیک تکراری را می‌گیرد و نتیجه در صفحهٔ اصلی می‌ماند. جایگزینی پروفایل موجود همچنان به قطع VPN نیاز دارد. نام اختیاری است: نام واردشده یا ذخیره‌شده، سپس عنوان امن ارائه‌دهنده و در نهایت نام میزبان. هدر `subscription-userinfo` مجموع ارسال و دریافت، حجم و انقضا را از همان پاسخ می‌خواند؛ دادهٔ غایب «نامشخص» می‌ماند. سن داده و اعلامی‌بودن آن مشخص است؛ این شمارش مستقل اپ نیست. دانلود دوره‌ای تازه‌ای برای کل اشتراک اضافه نشده است. قرارداد وضعیت پنل جدا حفظ شده است.

**ورودی‌ها:** گزینه‌های سازگار `cs` و `fm` در VLESS/VMess/Trojan با TLS معتبر حفظ می‌شوند؛ ماسک، اسکریپت یا تنظیم ناسازگار صریحاً رد می‌شود. طول کلید Shadowsocks 2022 بررسی می‌شود. Hy2 از فهرست محدود `mport` و `hop-interval` پشتیبانی می‌کند. لینک‌های سازگار WireGuard و فایل `.conf` تک‌همتای AmneziaWG با پارامترهای سازگار وارد می‌شوند. AWG موتور فضای کاربر مستقل با احراز هویت دارد؛ یک آدرس برای هر خانوادهٔ IP؛ اسکریپت، چند همتا و keepalive بازه‌ای پشتیبانی نمی‌شوند. YAML دلخواه هسته/Mihomo اجرا نمی‌شود.

تست‌های محلی و شبیه‌ساز، صرفه‌جویی باتری گوشی، پایداری شبانه/Doze، دسترسی اپراتور یا رفع مشکل WireGuard/Hy2 پنل گزارش‌شده را اثبات نمی‌کنند. مالک همین APK را برای انتشار تأیید کرده است؛ اندازه‌گیری تازهٔ زمان آغاز، شبکه و باتری گزارش نشده است. AnyTLS، TUIC v5 و Mieru تک‌پورت اضافه شده‌اند؛ محدودیت‌ها در یادداشت آلفا۲۰ آمده است. بستهٔ Lite یا دسکتاپ تازه‌ای منتشر نمی‌شود.

وضعیت حساب پنل ۱.۱۹، حجم باقی‌مانده برحسب GiB و زمان را در همان کارت مجموعه نشان می‌دهد. زرد یعنی حداکثر ۲۰٪ حجم یا ۳ روز؛ قرمز یعنی حداکثر ۵٪ یا ۲۴ ساعت. نامحدود، نامشخص و منقضی جدا هستند. ۴۰۴ یا شمارش قدیمی/ناقص، اطلاعات نامعتبر است و انقضا را ثابت نمی‌کند. وضعیت فقط هنگام دیده‌شدن صفحهٔ اصلی تازه می‌شود و مانع اتصال دستی نیست. مرجع سهمیه و اعمال محدودیت، سرور است.

Soar دست‌دهی‌های تکراری را حفظ می‌کند و حالت BOTH منتظر نتیجهٔ UDP و TCP می‌ماند. انتخاب صریح «فقط TCP» و بررسی اعتماد تغییر نمی‌کنند. موفقیت آزمون محلی، دسترسی ریزالور روی شبکهٔ واقعی را اثبات نمی‌کند.


**ارتقا:** [APK کامل](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk) را بدون حذف اپ یا پاک‌کردن داده روی آلفا۱۴ عمومی یا جدیدتر نصب کنید. بسته، امضای تولیدی و پروفایل‌ها حفظ شده‌اند.

اشتراک HTTPS دو دکمهٔ **آپدیت** و **ایمپورت** دارد. ایمپورت منبع تازه را با یک اقدام ثبت می‌کند و پس از موفقیت کل پنجره بسته می‌شود؛ خطا فرم را حفظ می‌کند و واردسازی دوبارهٔ نشانی ذخیره‌شده همان منبع را آپدیت می‌کند. ویرایش‌های محلی حفظ می‌شوند. آپدیت منبع قبلی به قطع VPN نیاز دارد؛ کنارگذاشتن ورودی‌های قدیمی بازبینی می‌خواهد. تازه‌سازی خودکار اختیاری، تقریباً هر ۱۲ ساعت با شرایط مناسب شبکه و باتری انجام می‌شود. QR کل مجموعه و PNG برای مجموعهٔ کوچک وجود دارد؛ مجموعهٔ بزرگ از خروجی فایل استفاده می‌کند.

اسکن Soar چهار حالت سریع، پیشرفته، E2E و Prism دارد. سریع و Prism دست‌دهی معتبر خود Soar و کلید Ed25519 کانفیگ را بررسی می‌کنند؛ E2E پاسخ HTTP 204 یا حساب SSH با هویت ازقبل‌تأییدشده را نیز می‌سنجد. پیشرفته صرفاً تشخیص DNS است. UDP و TCP مستقل آزمایش می‌شوند و پیش‌فرض جست‌وجوی Soar هر دو است؛ سیاست ذخیره‌شدهٔ اتصال عوض نمی‌شود. کنارگذاشتن تأیید قدیمی، فهرست DNS واردشده را پاک نمی‌کند.

### نصب و به‌روزرسانی

نسخه **آلفا۲۰ Full، کد ۴۳** است. [APK کاملِ انتشار عمومی](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk) را نصب کنید. بسته و امضای تولیدی نسبت به آلفا۱۴ عوض نشده‌اند؛ برای این به‌روزرسانی برنامه را حذف نکنید. آلفا۱۱ تاریخی و Rovunet Test هویت متفاوت دارند؛ [راهنمای مهاجرت](UPGRADE.md) را بخوانید. فایل بدون امضا قابل نصب نیست.

اندروید ۷ یا جدیدتر لازم است. هدف ساخت ARM32، ARM64 و x86_64 است؛ Naive/Tor به‌ارث‌رسیده برای x86_64 وجود ندارند. فایل Lite جدید وعده داده نمی‌شود و انتشار گوگل‌پلی، اپ‌استور یا رابط دسکتاپ نداریم.

پیش از ارتقا، توضیح امضا را بخوانید و نسخهٔ قابل‌خواندن را حفظ کنید. خروجی گروهی رمزدار، کانفیگ قفل‌شده را شامل نمی‌شود؛ کانفیگ غیرقابل‌اشتراک یا مقید به دستگاه ممکن است به لینک اصلی یا صدور دوبارهٔ مدیر نیاز داشته باشد. حذف اپ یا پاک‌کردن داده، راهکار معمول عیب‌یابی نیست. [مهاجرت](UPGRADE.md).

بررسی به‌روزرسانی در تنظیمات، APK سازگار را داخل اپ از گیت‌هاب دانلود و هش، بسته، نسخه و امضا را کنترل می‌کند؛ سپس نصب را به اندروید می‌سپارد. گیت‌هاب باید در دسترس باشد. نصب بی‌صدا نیست و امضای نامرتبط رد می‌شود. کانال پیش‌انتشار عمومی اختیاری است؛ نسخه‌های خصوصی جدا تحویل می‌شوند.

### ورود کانفیگ و مجموعه‌ها

از **+ ← ورود** برای متن، فایل یا QR پشتیبانی‌شده استفاده کنید؛ پیش‌نمایش را ببینید و تأیید کنید. فرمت‌ها شامل `rovunet://`، نام‌های سازگار `veyra://` و `slipnet://`، لینک‌های پشتیبانی‌شدهٔ VLESS/Hysteria2 و INI تک‌همتای WireGuard هستند. خروجی اختصاصی نسخهٔ ۳۳ است. نسخهٔ آیندهٔ ناشناخته رد می‌شود؛ به‌جای حذف شمارهٔ نسخه یا فیلد امنیتی، اپ را به‌روز کنید.

مجموعه مسیرهای مرتبط، انتخاب صریح و نتیجه‌های تکمیل‌شده را نگه می‌دارد. سرتیتر یا فلش، مجموعه را باز/بسته می‌کند. لمس اول کانفیگ را انتخاب و لمس دوبارهٔ کانفیگ انتخاب‌شده وصل می‌کند؛ انتخاب ذخیره‌شده هنگام بازگشت از قبل فعال است. ویرایش، حذف و منو کنترل جدا دارند. پاور مجموعه مسیر به‌یادمانده یا مسیر اولیهٔ مجاز را بدون پینگ اجباری راه می‌اندازد؛ انتخاب صریح بی‌صدا با پروتکل دیگری عوض نمی‌شود.

### اتصال، اعتماد و توقف

مجوز VPN اندروید را بدهید. اتصال دستی مسیر انتخاب‌شده را مستقیم راه می‌اندازد و پینگ، تست HTTPS یا اسکن مجموعهٔ resolver پیش‌نیاز نیست. برای SSH ناشناس یا گواهی خودامضا، اثر انگشت را قبل از تأیید با مدیر سرور تطبیق دهید. کار پس‌زمینه هویت ناشناس را خودکار تأیید و هویت تغییریافته را بی‌صدا قبول نمی‌کند.

از پاور اصلی/مجموعه یا منوی کانفیگ برای لغو Connecting و قطع استفاده کنید. پیش از جایگزینی، راه‌اندازی قبلی لغو و I/O بسته می‌شود. اگر توقف طولانی شد گزارش بدهید و مکرر اتصال نزنید. «متصل» یعنی آمادگی فنی مسیر/VPN، نه تضمین دسترسی به هر سایت؛ صفر بودن مصرفِ بی‌کاری به‌تنهایی علت قطع نیست.

### تست، DNS و Soar

تست صریح مجموعه را در حالت قطع اجرا کنید. HTTPS با پاسخ ۲۰۴ از داخل تونل واقعی، اعتبارسنجی گواهی و مقصد جایگزین محدود بررسی می‌شود. زمان تست، نسبت موفقیت و پراکندگی تأخیر نمونهٔ کوتاه‌اند، نه سرعت دانلود. شکست قبلی مانع اتصال دستی نیست؛ نتیجهٔ شبکهٔ دیگر جدا مشخص می‌شود.

مسیرهای مستقیم پیش از UDP و جایگزین DNS می‌آیند. تست/کشف DNS ممکن است چند دقیقه طول بکشد و قابل لغو است؛ ورود به فاز جایگزین DNS رضایت کاربر می‌خواهد. اتصال دستی DNS از resolverهای تنظیم‌شده استفاده می‌کند و اسکن اجباری ندارد.

پیش‌فرض **Soar و Soar+SSH جدید، UDP + TCP** است. حالت کانفیگ موجود یا واردشده حفظ می‌شود. خودکار با UDP شروع و ممکن است پس از شکست تکراری مسیر تأییدنشده TCP اضافه کند؛ با اجرای هر دو از ابتدا یکسان نیست. حالت کارآمد شبکه‌تان را نگه دارید. برای SSH روی Soar، سرور علاوه بر تونل پایه باید مقصد و پورت SSH را مجاز بداند.

### اطلاعات ورود، اشتراک و ظاهر

رازهای ذخیره‌شده با کلید Android Keystore رمز می‌شوند. رکورد ناخوانا برای بازیابی صریح علامت می‌خورد و بی‌صدا خالی نمی‌شود؛ از کانفیگ اصلیِ منطبق یا مدیر سرور بازیابی کنید. Keystore پس از حذف اپ جایگزین پشتیبان قابل‌انتقال نیست.

قفل و محدودیت اشتراک در مسیرهای خروجی پشتیبانی‌شده حفظ می‌شوند؛ قفل سیاست کلاینت است، نه تضمین در برابر کلاینت تغییریافته. برای پشتیبان قابل‌انتقال رمز قوی بگذارید و قبل از اتکا، ورود واقعی آن را بررسی کنید.

زبان فارسی/انگلیسی/چینی ساده/روسی و تم روشن/تیره/AMOLED در تنظیمات انتخاب می‌شوند. منابع اصلی تنظیمات و ویرایشگر ترجمه شده‌اند؛ عنوان‌ها و توضیح‌های اسکنر، خطاهای نگاشت‌شده و راهنمای انتخاب اتصال نیز منابع ترجمه دارند. نام پروتکل و آدرس تغییر نمی‌کنند. کنترل اعلان، ویجت و تنظیمات سریع به مجوز اندروید و رفتار لانچر وابسته است.

### عیب‌یابی و محدودیت

آلفا۱۵ حذف‌شدن اعضای لازم رابط Go/JNI در Release را اصلاح می‌کند. اگر پس از ارتقا خروج تازه رخ داد، اپ را دوباره باز کنید و گزارش امن را با زمان رخداد بفرستید؛ وجود سابقهٔ قدیمی خروج پس از به‌روزرسانی به‌تنهایی نشانهٔ کرش تازه نیست.

نسخهٔ اپ/اندروید، پروتکل، مراحل تکرار و بازشدن یا بازنشدن صفحهٔ عادی را گزارش کنید. گزارش امن اتصال، مرحله/کد نوع‌دار، سیاست Soar و نمونهٔ آمار رابط را بدون فیلد آدرس/حساب/اعتبارنامه ثبت می‌کند؛ محلی و اشتراک آن اختیاری است. لاگ دیباگ تفصیلیِ به‌ارث‌رسیده ممکن است حساس باشد؛ قبل از ارسال مرور و پاک‌سازی کنید. لینک واقعی، رمز، کلید خصوصی و شناسهٔ دستگاه را در تلگرام یا issue عمومی نگذارید.

مسیر دادهٔ SOCKS و تفاوت auto/on در Soar روی شبکهٔ گزارش‌شده هنوز بررسی میدانی می‌خواهند. پذیرش CONNECT اثبات عبور داده نیست؛ خطای DNS هم سانسور یا ACL را قطعی نمی‌کند. DoH به‌تنهایی فقط DNS را رمز می‌کند و SOCKS5 معمولی رمزنگاری انتقال ندارد. تعویض خودکار مسیر و سنجش نرخ دانلود اضافه نشده‌اند. [وضعیت](STATUS.md) · [تلگرام](https://t.me/RovunetVPN).

</div>

## نکات آلفا۲۰

واردسازی لینک ذخیره‌شده همان اشتراک را آپدیت می‌کند. آزمون کارت پس از موفقیت مستقیم سراغ DNS نمی‌رود؛ این مرحله فقط پس از شکست و تأیید شماست. خود نوشتهٔ بلند داخل کادر تک‌خطی حرکت می‌کند. AnyTLS، TUIC v5 و Mieru تک‌پورت اضافه شده‌اند؛ تنظیم ناسازگار صریحاً رد می‌شود. [جزئیات](releases/alpha20.md).
