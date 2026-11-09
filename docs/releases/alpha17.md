# Rovunet 0.3.0-alpha17 Full — code 36

## فارسی

آلفا۱۷ Full با کد ۳۶ با تأیید مالک منتشر می‌شود. نصب تازه ابتدا زبان می‌پرسد و تم را از سیستم می‌گیرد. اسکن Soar به رفتار دست‌دهی تکراری و BOTH آلفا۱۵.۴ بازگشته است. نتایج تازهٔ HTTPS و انتخاب‌های هر مجموعه حفظ می‌شوند؛ دکمهٔ اصلی اتصال جاری را حتی با انتخاب کارت دیگر قطع می‌کند. انونیموس، مهسا و پترنیها با رضایت و بازبینی وارد می‌شوند. ورود Trojan، VMess با رمزگذاری AEAD، Shadowsocks و اتصال‌های JSON سازگار Xray/sing-box پشتیبانی می‌شود؛ تنظیمات مؤثر ناسازگار و TLS بدون اعتبارسنجی با دلیل تفکیک‌شده رد می‌شوند. بازیابی شکست و انتخاب هوشمند اختیاری و محدود است؛ پیش‌فرض دستی می‌ماند. DNS، مسیرها و درگاه‌های ورودی JSON خارجی اعمال نمی‌شوند.

رفع قطعی توقف ۱۲۰ثانیه‌ای Hev روی گوشی اثبات نشده؛ ثبت اشتباه خاموشی صفحه اصلاح شده است. تست شبیه‌ساز اثبات دسترسی شبکهٔ واقعی، آزمون کامل شبانه/Doze یا صرفه‌جویی باتری نیست. نسخهٔ Lite یا دسکتاپ تازه‌ای منتشر نمی‌شود. جابه‌جایی پترنیها کنار انونیموس و مهسا برای آپدیت بعدی ثبت شده است.

## English

Alpha17 Full / code 36 is owner-approved. Fresh installs ask language first and follow system theme. Soar scanning restores the alpha15.4 repeated-handshake/BOTH behaviour. Collections retain fresh HTTPS measurements and scoped choices; the main button stops the live connection even when another card is selected. Anonymous, Mahsa and Patterniha use consent and reviewed imports. Trojan, AEAD VMess, Shadowsocks and supported Xray/sing-box JSON connections are importable; unsupported effective settings and unverified TLS are rejected with per-protocol reasons. Optional failure recovery/smart selection stays bounded and manual by default. JSON does not apply external DNS, routing or listeners.

The 120-second physical-phone Hev stop is not proved fixed; false screen-off reporting was corrected. Physical-network availability, full overnight/Doze and battery savings are not established by emulator tests. No new Lite or desktop binary is published. Patterniha placement beside Anonymous/Mahsa is deferred to the next update.

## Русский

Alpha17 Full, код 36, одобрена владельцем. При первой установке сначала выбирается язык, тема следует системе. Сканирование Soar возвращает повторные рукопожатия/BOTH из alpha15.4. Коллекции сохраняют свежие HTTPS-измерения и свой выбор; главная кнопка отключает действующий сеанс независимо от выбранной карточки. Anonymous, Mahsa и Patterniha импортируются после согласия и просмотра. Поддержаны Trojan, AEAD VMess, Shadowsocks и совместимые подключения JSON Xray/sing-box; неподдерживаемые параметры и непроверенный TLS отклоняются с причинами по протоколам. Восстановление и умный выбор ограничены и включаются явно; по умолчанию режим ручной. Внешние DNS, маршруты и входящие порты JSON не применяются.

Исправление 120-секундной остановки Hev на телефоне не доказано; исправлена ложная запись о выключенном экране. Эмулятор не доказывает доступность сети, ночную работу/Doze или экономию батареи. Новых Lite/desktop-файлов нет. Перенос Patterniha рядом с Anonymous/Mahsa отложен до следующего обновления.

## 简体中文

Alpha17 Full（代码 36）已获所有者批准。首次安装先选择语言，主题默认跟随系统。Soar 扫描恢复 alpha15.4 的重复握手及 BOTH 行为。集合保留最新 HTTPS 测量和各自选择；即使选中另一张卡片，主按钮仍断开当前连接。Anonymous、Mahsa 和 Patterniha 在同意及预览后导入。支持 Trojan、AEAD VMess、Shadowsocks 和兼容的 Xray/sing-box JSON 连接；按协议说明不支持的参数及未验证 TLS 的拒绝原因。故障恢复与智能选择有次数限制且需主动启用，默认仍为手动。外部 JSON 的 DNS、路由及监听端口不会应用。

尚未证实已修复实体手机上 Hev 停止耗时 120 秒的问题；已修正错误的屏幕关闭记录。模拟器测试不证明实际网络可用、整夜/Doze 稳定或省电。未发布新的 Lite 或桌面安装包。将 Patterniha 移至 Anonymous/Mahsa 同组的调整留待下次更新。

## Verification and source

Owner accepted the exact delivered code36 APK on 2026-11-09. Package `app.veyra.client` and production certificate are unchanged. Install over the existing public alpha14 or later app without clearing data. If code36 review03 is already installed, it is the same binary.

Application source: `bbb30d7146c3f308f37943a0e3c9456b185167f0`. [Passing CI](https://github.com/msVPNco/rovunet-app/actions/runs/42626133278): 119 portable/JVM tests, Gradle unit task, 64 Debug emulator cases, 2 minified-Release JNI cases; 9 loopback TCP/UDP modes with 200 requests/concurrency 40 each. The parallel push run 42626123939 had one 8-second error-display UI timeout; the same source and test passed in the independent PR run. No test was removed.

APK SHA-256: `eb5ac7bd474b37e361e8ca126c8e2f7d445faaf876ddd7b9ecd158548324a871`. Publication does not rebuild or re-sign it. Pinned Xray/Hev/launcher were rebuilt in source CI; remaining pinned Full native engines were reused. This is not proof of every relay working on every network or measured battery savings.

Explicit full/light source archives contain tracked publication files, available pinned native trees and the pinned Xray source. The historical dnstt-mobile pin remains unavailable upstream; no replacement is substituted. No private key/recovery material is included. Full is the only published APK flavour. [Provenance](https://github.com/msVPNco/rovunet-app/blob/main/docs/releases/alpha17-provenance.json).
