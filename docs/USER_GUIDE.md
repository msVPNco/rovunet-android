# Rovunet user guide — 0.3.0-alpha11 Full

[فارسی](#راهنمای-فارسی) · [Download](https://github.com/msVPNco/rovunet-android/releases/tag/v0.3.0-alpha11) · [Telegram](https://t.me/RovunetVPN)

## Install and update

Download **Rovunet-0.3.0-alpha11-Full.apk** from the release Assets. Minimum Android API is 24 (Android 7.0). The universal APK contains ARM32, ARM64 and x86_64 assets; inherited Naive/Tor availability is limited on x86_64. No Lite APK is published for alpha11.

Export a backup, then install over the previous Full build. Do not uninstall first if you want to retain profiles and saved identities. The package ID and signer are unchanged. Android controls installation and the VPN permission prompt. Verify the APK hash against the release checksums; an unknown-developer Play Protect warning by itself is not a security verdict.

In Settings, Check for updates reads this repository's **Latest** release. A newer compatible Full APK can be downloaded in the app; it is checked for hash, package, version and signer before opening Android's installer. Installation is never silent. A Lite installation does not have a new alpha11 Lite asset.

The file is a non-debuggable Release build, but retains the old debug signing certificate for upgrades. There is no current Rovunet Google Play, App Store or desktop GUI listing.

## Import and collections

Use **+ → Import** to paste text or import a supported file/QR. Supported links include native rovunet:// bundles, compatible slipnet:// and veyra:// aliases, supported standard VLESS/Hysteria2 links and single-peer WireGuard INI. Consult [core limitations](../VEYRA-CORE.md) before converting another app's format.

A collection groups related paths for one account/server. Prefer explicit collection metadata exported by the panel; legacy inference is best effort. Review the group and rename it if needed. Grouping is a UI association, not a guarantee that credentials can safely be shared.

Use the collection's three-dot menu for available operations. Export/sharing restrictions and protected chain members still apply. Export a backup before regrouping or bulk deletion. Duplicate removal has a preview and checks full connection identity rather than protocol name alone.

## Connect first; test separately

1. Select the collection and expand it.
2. Tap the desired profile, then **Connect**. It starts that profile directly, without a prerequisite ping/HTTPS/resolver scan.
3. Approve Android VPN permission if requested. For an unknown SSH key or self-signed TLS identity, compare the displayed fingerprint with your administrator before approving.
4. After protocol/VPN readiness, the app displays **Connected**. Open a normal site or app to check useful traffic.

If no specific profile is selected, Connect chooses a suitable non-DNS path from the selected collection using the existing order and cached measurements. DNS fallback still asks for consent. With DNS-only paths, consenting to Connect starts the configured path directly; it does not run the scanner first.

Connected is not a promise that every destination is reachable. Backend failure and stalled traffic are monitored after setup. A previous red HTTPS result is historical and does not prohibit manual connection. Idle traffic counters may legitimately be zero.

The previous service's destruction wait is bounded at 10 seconds. Technical startup budgets are 90 seconds for direct paths, 120 for DNS and 180 for Tor/unknown restored starts; chains follow their first layer. These are error deadlines, not expected connection times.

## Measure a selected collection

Use the explicit collection Ping/Test action while disconnected; it tests **only that selected collection**. It starts each path and performs HTTPS requests through the actual tunnel, with certificate validation and a fallback target. Protocol setup and request budgets differ by path.

Direct testing has a 90-second phase budget. If direct paths fail, DNS fallback asks for consent. Configured-resolver testing and subsequent resolver discovery each have an upper phase budget of 180 seconds, so a full fallback search can take several minutes. Cancel if you do not want it to continue.

Results show measured HTTPS latency, successful samples and variation. They are a short sample, not throughput or long-session stability. Plain TCP/DNS reachability is diagnostic only. The separate DNS scanner has its own configurable probe/HTTP criteria and is not equivalent to collection HTTPS validation.

## Settings, diagnostics and limitations

Settings and related dialogs are translated into Persian, English, Simplified Chinese and Russian. Language and layout direction update in place. Some inherited advanced editor/scanner text remains English.

For Connecting stalls, a Wait/Close dialog or an unexpected exit: open the stability report in Settings. It stores bounded local diagnostics and fixed connection-stage events. Sharing is explicit; no automatic diagnostic upload is performed. Review the report before sharing it.

A useful report includes app/Android versions, tunnel type, steps, connection-stage message and whether normal traffic works. Do not post config URIs, credentials, private keys, addresses or device identifiers in a public issue.

Known follow-ups: clearer update-version wording, reset-button alignment, advanced translation, physical-device testing, subscription refresh, collection failover and production signing. See [status](STATUS.md).

## Server and privacy

Use a compatible server from an administrator you trust, or consult [Rovunet Panel](https://github.com/msVPNco/rovunet-panel). Client protocol support is not a claim that the panel deploys every backend.

DoH-only mode protects DNS requests; it is not a general encrypted traffic tunnel. Standalone SOCKS5 does not provide transport encryption. SSH protects its channel but does not prove the server can reach every Internet destination. A pinned key identifies the endpoint, not its policy or trustworthiness.

An encrypted/locked profile with an embedded application key is a sharing-control mechanism, not guaranteed secrecy against someone who has the app. Use a strong password for password-derived encrypted backups. A shared WireGuard private key is not made safe for simultaneous devices; issue separate peer credentials.

[Security policy](../SECURITY.md) · [Source/build limitations](BUILDING.md)

# راهنمای فارسی

## نصب و ارتقا

از Assets انتشار، فایل **Rovunet-0.3.0-alpha11-Full.apk** را بگیرید. اندروید ۷ یا جدیدتر لازم است. برای حفظ کانفیگ و هویت‌های ذخیره‌شده، پس از گرفتن پشتیبان روی Full قبلی نصب کنید؛ برنامه را حذف نکنید. برای این نسخه APK از نوع Lite ارائه نشده است.

هش فایل را با SHA256SUMS.txt مقایسه کنید. هشدار ناشر ناشناس به‌تنهایی دربارهٔ امنیت فایل نتیجهٔ قطعی نمی‌دهد. نصب و اجازهٔ VPN در اختیار اندروید است. APK غیرقابل دیباگ است، اما گواهی debug قبلی را برای ارتقا حفظ کرده است؛ امضای تولید و نسخهٔ فروشگاهی هنوز وجود ندارند.

به‌روزرسانی در تنظیمات از GitHub Latest بررسی می‌شود. برای نسخهٔ جدید سازگار، دانلود و کنترل هش، بسته، کد نسخه و امضا داخل اپ انجام می‌شود؛ نصب به تأیید اندروید نیاز دارد. متن نمایش نسخه‌ها برای اصلاح بعدی ثبت شده است.

## ورود کانفیگ و مجموعه‌ها

از **+ ← ورود**، متن، فایل یا QR پشتیبانی‌شده را وارد کنید. قالب rovunet://، نام‌های قدیمی slipnet:// و veyra://، لینک‌های استاندارد پشتیبانی‌شدهٔ VLESS/Hysteria2 و فایل WireGuard تک‌همتا قابل استفاده‌اند. همهٔ قالب‌های دیگر اپ‌ها الزاماً پشتیبانی نمی‌شوند.

مجموعه برای مسیرهای یک حساب/سرور است. خروجی پنل با شناسهٔ صریح مجموعه دقیق‌تر از گروه‌بندی حدسی کانفیگ قدیمی است. نام و اعضا را بررسی کنید. گروه‌بندی به معنی امن‌بودن اشتراک عمومی اعتبارنامه‌ها نیست.

عملیات مجموعه در منوی سه‌نقطه است. محدودیت خروجی کانفیگ و محافظت اعضای زنجیره برقرار می‌مانند. حذف تکراری‌ها پیش‌نمایش دارد و هویت کامل اتصال را مقایسه می‌کند؛ قبل از تغییر عمده پشتیبان بگیرید.

## اتصال عادی

۱. مجموعه را باز و کانفیگ مورد نظر را انتخاب کنید.
۲. اتصال را بزنید؛ همان کانفیگ بدون پینگ، تست HTTPS یا اسکن اجباری رزولور شروع می‌شود.
۳. اجازهٔ VPN اندروید را بدهید. اگر کلید SSH یا گواهی خودامضا ناشناس است، اثر انگشت را با مدیر سرور تطبیق دهید.
۴. پس از آمادگی فنی تونل، «متصل» نمایش داده می‌شود؛ یک سایت یا اپ عادی باز کنید.

بدون انتخاب کانفیگ مشخص، مسیر مستقیم مناسبی از همان مجموعه برگزیده می‌شود. ورود به مسیرهای DNS با رضایت شماست. اتصال دستی از رزولور تنظیم‌شده استفاده می‌کند؛ اسکن بخشی از تست صریح است.

خطای قرمز تست قبلی مانع اتصال دستی نیست. وضعیت متصل، آمادگی تونل است و دسترسی به همهٔ سایت‌ها را تضمین نمی‌کند. آمار صفر هنگام بیکاری طبیعی است؛ کنترل سلامت و توقف تبادل داده پس از راه‌اندازی ادامه دارد.

انتظار پایان سرویس قبلی حداکثر ۱۰ ثانیه است. مهلت راه‌اندازی مستقیم ۹۰، DNS برابر ۱۲۰ و Tor/شروع ناشناخته ۱۸۰ ثانیه است. این‌ها مهلت خطا هستند، نه مدت معمول اتصال.

## پینگ و تست

عملیات تست/پینگ مجموعه فقط **مجموعهٔ انتخاب‌شده** را بررسی می‌کند. هنگام تست، اتصال عادی را قطع نگه دارید. سنجش HTTPS از داخل تونل و با اعتبارسنجی گواهی انجام می‌شود؛ اتصال TCP یا پاسخ DNS به‌تنهایی موفقیت نهایی نیست.

فاز مستقیم حداکثر ۹۰ ثانیه بودجه دارد. پس از شکست مسیرهای مستقیم، برای ورود به DNS از شما اجازه می‌خواهد. تست رزولورهای موجود و مرحلهٔ کشف رزولور هرکدام تا ۱۸۰ ثانیه بودجه دارند؛ روند کامل ممکن است چند دقیقه طول بکشد و قابل لغو است.

تأخیر HTTPS، نسبت موفقیت و پراکندگی پاسخ‌ها فقط نمونهٔ کوتاه‌اند؛ سرعت دانلود یا تضمین پایداری نیستند. اسکنر DNS معیار و نشانی تست جداگانه دارد و نباید نتیجهٔ آن را با اعتبارسنجی HTTPS مجموعه یکسان دانست.

## زبان، گزارش مشکل و امنیت

تنظیمات و پنجره‌های وابسته چهارزبانه‌اند؛ برخی متن‌های پیشرفتهٔ فرم‌ها و اسکنر هنوز انگلیسی هستند. تغییر زبان و جهت رابط بدون بازسازی صفحه انجام می‌شود.

اگر روی Connecting ماند، پیام صبر/بستن آمد یا اپ خارج شد، گزارش پایداری را از تنظیمات باز کنید. گزارش محلی و محدود است؛ مرحله‌های ثابت اتصال، آدرس، نام کاربری، رمز و نام کانفیگ ندارند. ارسال خودکار وجود ندارد؛ قبل از اشتراک گزارش را مرور کنید.

نسخهٔ اپ/اندروید، پروتکل، مراحل تکرار و بازشدن یا بازنشدن سایت عادی را گزارش کنید. لینک کانفیگ، کلید خصوصی، رمز و شناسهٔ دستگاه را عمومی نکنید.

DoH به‌تنهایی تونل همهٔ ترافیک نیست؛ SOCKS5 معمولی رمزنگاری انتقال ندارد. اتصال موفق SSH هم تضمین دسترسی سرور به هر سایت نیست. قفل کانفیگ با کلید جاسازی‌شده، محرمانگی قطعی در برابر دارندهٔ اپ ایجاد نمی‌کند؛ پشتیبان مبتنی بر رمز قوی و اعتبارنامهٔ WireGuard جدا برای هر دستگاه استفاده کنید.

[وضعیت و کارهای بعدی](STATUS.md) · [پنل سرور](https://github.com/msVPNco/rovunet-panel) · [گروه تلگرام](https://t.me/RovunetVPN)
