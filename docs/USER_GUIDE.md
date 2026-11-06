# Rovunet user guide — alpha14 / راهنمای کاربر

[English](#english) · [فارسی](#فارسی) · [简体中文](USER_GUIDE.zh-CN.md) · [Русский](USER_GUIDE.ru.md)

## English

### Installation and updates

The version is **0.3.0-alpha14 Full, code 25**. Download installable files only from the actual published [release](https://github.com/msVPNco/rovunet-android/releases). The owner authorized the new managed production signer before deployment; alpha14 requires a fresh installation relative to old alpha11. Later direct APK updates reuse this production identity. Unsigned preparation artifacts cannot be installed. A private `Rovunet Test` package is separate from public `app.veyra.client` and is not its in-place update.

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

Choose Persian/English/Simplified Chinese/Russian and light/dark/AMOLED in Settings. Main settings/editor resources are translated; some inherited advanced scanner text and backend errors may remain English. Protocol identifiers and addresses stay unchanged. The notification/widget/quick-settings controls depend on Android permissions and launcher behavior.

### Troubleshooting and limits

Include app/Android versions, protocol, reproduction steps and whether ordinary pages open. The safe connection report records typed stages/codes, configured Soar mode and UI traffic snapshots without address/account/credential fields. It is local and shared only by user action. Detailed inherited debug logs can contain sensitive data; review/redact them before sharing. Do not post real config links, passwords, private keys or device IDs in public Telegram/issues.

Remote SOCKS data flow, Soar auto/on differences and counters in the reported network still need field diagnosis. SOCKS CONNECT success is not proof of payload transfer; DNS failure does not prove censorship or server ACL. DoH alone encrypts DNS; ordinary SOCKS5 does not encrypt transport. Subscription refresh, automatic path failover and throughput benchmarks are not included. [Status](STATUS.md) · [Telegram](https://t.me/RovunetVPN).

## فارسی

### نصب و به‌روزرسانی

نسخه **آلفا۱۴ Full، کد ۲۵** است. فایل نصب را از [انتشار واقعی گیت‌هاب](https://github.com/msVPNco/rovunet-android/releases) بگیرید. تعویض امضای تولیدی با تأیید مالک و پیش از استقرار کاربران انجام شد؛ نسبت به آلفا۱۱ نصب تازه لازم است و نسخه‌های بعدی همین هویت را حفظ می‌کنند. فایل بدون امضای مرحلهٔ آماده‌سازی قابل نصب نیست. بستهٔ خصوصی **Rovunet Test** مستقل است و روی آلفا۱۱ عمومی به‌عنوان آپدیت نصب نمی‌شود.

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

زبان فارسی/انگلیسی/چینی ساده/روسی و تم روشن/تیره/AMOLED در تنظیمات انتخاب می‌شوند. منابع اصلی تنظیمات و ویرایشگر ترجمه شده‌اند؛ بعضی متن‌های پیشرفتهٔ اسکنر و خطاهای backend ممکن است انگلیسی باشند. نام پروتکل و آدرس تغییر نمی‌کنند. کنترل اعلان، ویجت و تنظیمات سریع به مجوز اندروید و رفتار لانچر وابسته است.

### عیب‌یابی و محدودیت

نسخهٔ اپ/اندروید، پروتکل، مراحل تکرار و بازشدن یا بازنشدن صفحهٔ عادی را گزارش کنید. گزارش امن اتصال، مرحله/کد نوع‌دار، سیاست Soar و نمونهٔ آمار رابط را بدون فیلد آدرس/حساب/اعتبارنامه ثبت می‌کند؛ محلی و اشتراک آن اختیاری است. لاگ دیباگ تفصیلیِ به‌ارث‌رسیده ممکن است حساس باشد؛ قبل از ارسال مرور و پاک‌سازی کنید. لینک واقعی، رمز، کلید خصوصی و شناسهٔ دستگاه را در تلگرام یا issue عمومی نگذارید.

مسیر دادهٔ SOCKS، تفاوت auto/on در Soar و شمارنده‌ها روی شبکهٔ گزارش‌شده هنوز بررسی میدانی می‌خواهند. پذیرش CONNECT اثبات عبور داده نیست؛ خطای DNS هم سانسور یا ACL را قطعی نمی‌کند. DoH به‌تنهایی فقط DNS را رمز می‌کند و SOCKS5 معمولی رمزنگاری انتقال ندارد. اشتراک دوره‌ای، تعویض خودکار مسیر و سنجش نرخ دانلود اضافه نشده‌اند. [وضعیت](STATUS.md) · [تلگرام](https://t.me/RovunetVPN).
