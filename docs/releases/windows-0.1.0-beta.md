# Rovunet Windows 0.1.0 Beta

## English

First Windows x64 beta. Compact/wide four-language interface, touch-size cards, subscriptions with account usage/expiry, profile editors, local HTTP/SOCKS5, optional Windows proxy and staged DNS resolver scanning with stop/resume and multi-selection.

Download **Rovunet-Windows-0.1.0-Beta-x64-Setup.exe**. Target: Windows 10 22H2/build 19045 and Windows 11 x64. The installer is unsigned; verify SHA256SUMS.txt. Close the previous app from its tray menu before upgrading.

**Known limits:** Hysteria2 and WireGuard did not work in the maintainer's tests. No device-wide TUN or forced per-process routing. Public-feed downloads can fail intermittently; some custom options remain unsupported. TLS/SSH and protected-profile restrictions are preserved. Manual connection does not require a successful test.

**Evidence:** 386 fresh Windows/installer assertions and 239 domain tests passed. Connection implementation matches the reviewed preview20 baseline; four native engines are pinned reused binaries. The maintainer reports working traffic on tested paths except HY2/WireGuard, including Slipstream. A previous separate Slipstream HTTPS check exceeded 20 seconds. Actual beta elevation in Program Files and physical touch/DPI/suspend need broader validation. No fresh Android build or Windows GitHub Actions run is claimed.

Source commit: `562a3d71670ceb7de909146bf68116d5cc614496`. See VERIFICATION.json, SOURCE-MANIFEST.json and the explicit Windows source archive. Xray-core-source.zip contains the pinned upstream Xray source. This prerelease preserves the Android Latest channel.

## فارسی

اولین نسخهٔ **0.1.0 Beta ویندوز x64** با رابط چهارزبانه، نمای باریک و گسترده، کارت‌های مناسب لمس، حجم و انقضای اشتراک، ویرایش پیکربندی و جست‌وجوی مرحله‌ای ریزالور با توقف/ادامه و انتخاب چندتایی.

برای نصب، فایل **Rovunet-Windows-0.1.0-Beta-x64-Setup.exe** را بگیرید؛ Windows 10 22H2 یا Windows 11 x64 لازم است. نصب‌کننده امضای Windows ندارد؛ هش را تطبیق دهید و پیش از ارتقا، برنامهٔ قبلی را از منوی کنار ساعت ببندید.

**محدودیت‌ها:** Hysteria2 و WireGuard در آزمون مالک موفق نبودند. این نسخه پراکسی محلی/سیستم است و TUN یا مسیردهی اجباری برنامه‌ها ندارد. دریافت منابع عمومی گاهی ناموفق و بعضی تنظیمات سفارشی ناسازگارند. TLS/SSH و حفاظت حفظ شده‌اند؛ اتصال دستی به آزمون وابسته نیست.

۳۸۶ بررسی تازهٔ ویندوز/نصب و ۲۳۹ آزمون منطق موفق شدند. مالک برای مسیرهای آزمایش‌شده به‌جز آن دو پروتکل، اتصال و تبادل دادهٔ موفق گزارش کرده است. نصب واقعی Beta در Program Files/UAC و لمس/DPI/خواب سخت‌افزاری هنوز به بررسی گسترده‌تر نیاز دارند. جزئیات، سورس و SHA256 همراه فایل‌ها هستند.

## Русский

Первый выпуск Windows x64 0.1.0 Beta: четыре языка, узкий/широкий интерфейс, подписки, редакторы и поэтапный поиск DNS. Установщик без подписи; проверьте SHA256SUMS.txt. Windows 10 22H2 или Windows 11 x64.

Известные ограничения: Hysteria2/WireGuard не работали в тестах владельца; общего TUN и принудительной маршрутизации приложений нет. Загрузка публичных источников иногда не удаётся. TLS/SSH сохраняются. Пройдено 386 новых Windows/установочных проверок и 239 тестов логики; подробная область проверки и исходники приложены. Этот предварительный выпуск не заменяет Android Latest.

## 简体中文

首个 Windows x64 0.1.0 Beta：四种语言、窄窗/宽窗、订阅用量与到期信息、配置编辑及分阶段 DNS 扫描。安装程序未签名；请核对 SHA256SUMS.txt。目标 Windows 10 22H2 或 Windows 11 x64。

已知限制：所有者测试中 Hysteria2/WireGuard 未成功；尚无全设备 TUN 或强制按程序路由。公共源下载可能间歇失败，保留 TLS/SSH 和配置保护。386 项新的 Windows/安装检查及 239 项逻辑测试通过；详细范围、源代码和校验和随附。本预发布不会替换 Android Latest。
