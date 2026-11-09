# Rovunet user guide — alpha17 / راهنمای کاربر

Alpha17 Full / code 36 is owner-approved. Fresh installs ask language first and follow system theme. Soar scanning restores the alpha15.4 repeated-handshake/BOTH behaviour. Collections retain fresh HTTPS measurements and scoped choices; the main button stops the live connection even when another card is selected. Anonymous, Mahsa and Patterniha use consent and reviewed imports. Trojan, AEAD VMess, Shadowsocks and supported Xray/sing-box JSON connections are importable; unsupported effective settings and unverified TLS are rejected with per-protocol reasons. Optional failure recovery/smart selection stays bounded and manual by default. JSON does not apply external DNS, routing or listeners.

The 120-second physical-phone Hev stop is not proved fixed; false screen-off reporting was corrected. Physical-network availability, full overnight/Doze and battery savings are not established by emulator tests. No new Lite or desktop binary is published. Patterniha placement beside Anonymous/Mahsa is deferred to the next update.

[English](#english) · [فارسی](#فارسی) · [简体中文](USER_GUIDE.zh-CN.md) · [Русский](USER_GUIDE.ru.md)

## English

**Direct upgrade for public alpha14–alpha16 (through code32):** download the [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha17/Rovunet-v0.3.0-alpha17-Full.apk) and install over the existing app **without uninstalling or clearing data**. The old updater embeds the former repository URL; web redirects cannot repair that check. Package and production signer are unchanged. If the accepted alpha17/code36 review APK is installed, it is this same binary; no reinstall is needed.

Alpha16.1 fixes update compatibility after the repository was renamed to `rovunet-app`. Android now selects its newest compatible Full/Lite APK from the shared release feed, independently of Windows/macOS Latest and with the preview setting preserved. Repository/tag/filename ownership, checksum, package, version and signer checks remain enforced. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42626133278) passed **119 portable/JUnit cases, the Gradle unit-test task, 64 Debug emulator cases and 2 minified Release JNI cases**. The owner authorized publication of the exact reviewed APK. Current behaviour changes and limits are described in the alpha17 summary above.

HTTPS subscriptions have **Update** and **Import** actions. Import adds a new source once and closes the dialog after success; errors preserve the form, duplicate source URLs are rejected, and updates preserve local changes. Existing-source updates require VPN disconnection; removals require review. Optional refresh runs approximately every 12 hours under suitable network/battery conditions. Collection QR/PNG sharing is supported when the payload fits; larger collections use files.

Soar scanning offers Quick, Advanced, E2E and Prism. Quick/Prism use authenticated Soar handshakes with the configured Ed25519 key; E2E also checks HTTP 204 or the SSH account with existing trust approval. Advanced is diagnostic DNS only. UDP and TCP are tested independently; Soar scanner defaults to both without rewriting saved connection choices. Imported candidates survive discarded foreign/legacy proof.


Panel v1.19 account status shows remaining GiB and time in the existing collection card. Amber means ≤20% remaining or ≤3 days; red means ≤5% or ≤24 hours. Unlimited, unknown and expired are distinct. A 404 or stale/incomplete counting is unavailable information, not proof of expiry. Status refresh runs while Home is visible and never blocks manual connection. Server-side metering and limits remain authoritative.

Soar restores repeated protocol handshakes and BOTH waits for both UDP/TCP results. Preserve explicit TCP-only and trust checks. Scan success in local fixtures is not proof of resolver availability on a particular network.

### Installation and updates

The version is **0.3.0-alpha17 Full, code 36**. Download the signed [published Full APK](https://github.com/msVPNco/rovunet-app/releases/latest). Public alpha14 can be updated in place: package and production signer are unchanged. Do not uninstall as part of this update. Historical alpha11 and private Rovunet Test have different identities; see [migration](UPGRADE.md). Unsigned preparation files are not installable.

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

Remote SOCKS data flow and Soar auto/on differences still need field diagnosis. SOCKS CONNECT success is not proof of payload transfer; DNS failure does not prove censorship or server ACL. DoH alone encrypts DNS; ordinary SOCKS5 does not encrypt transport. Automatic path failover and throughput benchmarks are not included. [Status](STATUS.md) · [Telegram](https://t.me/RovunetVPN).

<div dir="rtl">

## فارسی

وضعیت حساب پنل ۱.۱۹، حجم باقی‌مانده برحسب GiB و زمان را در همان کارت مجموعه نشان می‌دهد. زرد یعنی حداکثر ۲۰٪ حجم یا ۳ روز؛ قرمز یعنی حداکثر ۵٪ یا ۲۴ ساعت. نامحدود، نامشخص و منقضی جدا هستند. ۴۰۴ یا شمارش قدیمی/ناقص، اطلاعات نامعتبر است و انقضا را ثابت نمی‌کند. وضعیت فقط هنگام دیده‌شدن صفحهٔ اصلی تازه می‌شود و مانع اتصال دستی نیست. مرجع سهمیه و اعمال محدودیت، سرور است.

Soar restores repeated protocol handshakes and BOTH waits for both UDP/TCP results. Preserve explicit TCP-only and trust checks. Scan success in local fixtures is not proof of resolver availability on a particular network.


**ارتقا:** [APK کامل](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha17/Rovunet-v0.3.0-alpha17-Full.apk) را بدون حذف اپ یا پاک‌کردن داده روی آلفا۱۴ عمومی یا جدیدتر نصب کنید. بسته، امضای تولیدی و پروفایل‌ها حفظ شده‌اند. اگر APK تأییدشدهٔ آلفا۱۷/کد۳۶ را دارید، همین فایل انتشار است و نصب دوباره لازم نیست.

اشتراک HTTPS دو دکمهٔ **آپدیت** و **ایمپورت** دارد. ایمپورت منبع تازه را با یک اقدام ثبت می‌کند و پس از موفقیت کل پنجره بسته می‌شود؛ خطا فرم را حفظ می‌کند و نشانی تکراری دوباره ثبت نمی‌شود. ویرایش‌های محلی حفظ می‌شوند. آپدیت منبع قبلی به قطع VPN نیاز دارد؛ کنارگذاشتن ورودی‌های قدیمی بازبینی می‌خواهد. تازه‌سازی خودکار اختیاری، تقریباً هر ۱۲ ساعت با شرایط مناسب شبکه و باتری انجام می‌شود. QR کل مجموعه و PNG برای مجموعهٔ کوچک وجود دارد؛ مجموعهٔ بزرگ از خروجی فایل استفاده می‌کند.

اسکن Soar چهار حالت سریع، پیشرفته، E2E و Prism دارد. سریع و Prism دست‌دهی معتبر خود Soar و کلید Ed25519 کانفیگ را بررسی می‌کنند؛ E2E پاسخ HTTP 204 یا حساب SSH با هویت ازقبل‌تأییدشده را نیز می‌سنجد. پیشرفته صرفاً تشخیص DNS است. UDP و TCP مستقل آزمایش می‌شوند و پیش‌فرض جست‌وجوی Soar هر دو است؛ سیاست ذخیره‌شدهٔ اتصال عوض نمی‌شود. کنارگذاشتن تأیید قدیمی، فهرست DNS واردشده را پاک نمی‌کند.

### نصب و به‌روزرسانی

نسخه **آلفا۱۷ Full، کد ۳۶** است. [APK کاملِ انتشار عمومی](https://github.com/msVPNco/rovunet-app/releases/latest) را نصب کنید. بسته و امضای تولیدی نسبت به آلفا۱۴ عوض نشده‌اند؛ برای این به‌روزرسانی برنامه را حذف نکنید. آلفا۱۱ تاریخی و Rovunet Test هویت متفاوت دارند؛ [راهنمای مهاجرت](UPGRADE.md) را بخوانید. فایل بدون امضا قابل نصب نیست.

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
