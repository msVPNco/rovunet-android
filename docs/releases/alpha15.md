# Rovunet 0.3.0-alpha15 — Full

Package: `app.veyra.client` · version code: **26** · Android 7/API 24+.
Application build/source: `908253b42082bbd80007677f0369792d324a378f`.
APK SHA-256: `3ea678b74212bbcda34ba5ed9dba7607323c0a1f828328170894ea9177dc1ab8`.
Certificate SHA-256: `1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416`.

## فارسی

رووُنت مسیرهای مختلف دسترسی به سرور شخصی را در مجموعه‌ها نگه می‌دارد: خانواده‌های DNS شامل DNSTT، NoizDNS، VayDNS، Slipstream و Soar، روش‌های SSH و ترکیب‌های پشتیبانی‌شده، NaiveProxy و SOCKS5، و کانفیگ‌های VLESS WS، REALITY، XHTTP، Hysteria2 و WireGuard تک‌همتا. بستهٔ Full مسیرهای Tor پشتیبانی‌شده را نیز دارد؛ دسترسی هر روش به سرور، backend و معماری وابسته است. [معرفی کامل](../../README.fa.md).

این انتشار نقص واقعی بسته‌بندی Go/JNI در Release را اصلاح می‌کند. بهینه‌ساز اعضایی را حذف می‌کرد که از native فراخوانی می‌شوند؛ قواعد استاندارد gomobile به AAR بازسازی‌شده و تنظیم Release برگردانده شدند. حذف R8، افزایش مهلت اتصال یا تغییر MTU راه‌حل این انتشار نیست؛ اتصال دستی همچنان تست اجباری ندارد و انتخاب صریح/واردشده و تأیید هویت سرور حفظ می‌شوند.

[اجرای CI](https://github.com/msVPNco/rovunet-android/actions/runs/42215030490) روی سورس ساخت موفق است: ۵۰ آزمون JVM، ۳۱ آزمون Debug خصوصی و اجرای دو بررسی JNI روی Release خصوصیِ بهینه‌شده، بدون skip. اجراکنندهٔ Release از Instrumentation اندروید استفاده می‌کند و کپی رابط Go یا کتابخانهٔ AndroidX در APK تست ندارد. APK عمومی جداگانه از نظر بسته، نسخه، امضا، رابط DEX و هم‌ترازی بررسی شد؛ همهٔ ۳۳ کتابخانه حاضرند و هر ۲۱ ELF64 هم‌ترازی ۱۶KB دارند. موتورهای DNS همان باینری‌های قبلی‌اند؛ سه فایل Xray از سورس پین‌شده دوباره ساخته شده و هش متفاوت دارند. بازسازی کامل native ادعا نمی‌شود.

**ارتقا از آلفا۱۴:** فایل Full را بدون حذف برنامه نصب کنید؛ بسته و امضای تولیدی ثابت‌اند. آلفا۱۱ تاریخی و Rovunet Test هویت متفاوت دارند؛ [مهاجرت](../UPGRADE.md). آزمون شبیه‌ساز API 30/x86_64 تضمین عملکرد همهٔ شبکه‌ها/گوشی‌ها نیست و گزارش جدیدِ آزمون میدانی برای این APK ادعا نمی‌شود. مالک انتشار را تأیید کرده است. مشکل شمارنده به گزارش کاربر رفع‌شده است؛ پچ تازه‌ای برای شمارنده در این انتشار انجام نشده است.

## English

Rovunet groups routes to a personal server in collections, with direct manual connections, separate real-tunnel HTTPS/204 measurements, supported link/file/QR imports, encrypted credential storage/backups and local opt-in diagnostics. Families include DNS tunnels, supported SSH wrappers/combinations, NaiveProxy/SOCKS5, VLESS WS, REALITY, XHTTP, Hysteria2, single-peer WireGuard and supported Full Tor bridges. Main workflows/settings/editor resources support Persian, English, Simplified Chinese and Russian; inherited advanced text can remain English. [Features and limits](../../README.md).

Alpha15 fixes gomobile JNI members removed by Release/R8 packaging. One shared rule set is consumed by the reconstructed AAR and app minifier. R8 remains enabled; manual connections are not probe-gated, and MTU, network deadlines, explicit profile choices and trust approval are unchanged.

[Passing CI](https://github.com/msVPNco/rovunet-android/actions/runs/42215030490): 50 JVM tests, 31 private Debug device cases and two additional real JNI checks on minified private Release; zero skips. The framework-only Release driver contains no copied Go binding or AndroidX classes. Public APK checks cover package/code/signer, actual DEX ABI, ZIP integrity and all 21 ELF64 alignments across 33 libraries. DNS binaries are reused unchanged; three pinned-source Xray outputs differ from alpha14. This is not a fresh rebuild of every native engine. The final signed distribution APK itself has not been revalidated on the reported physical network.

Install over public alpha14 without uninstalling: package and production certificate match. Historical alpha11 and private candidates differ. See [upgrade](../UPGRADE.md). Publication was authorized by the owner; no new phone-test outcome is implied. The reported traffic-total issue is closed without a new counter change.

## 简体中文

Rovunet 将自有服务器的多种路径归入集合，支持直接连接、独立的真实隧道 HTTPS/204 测量、配置导入、凭据保护及本地可选诊断。DNS 隧道、SSH 及支持的组合、NaiveProxy、SOCKS5、VLESS WS、REALITY、XHTTP、Hysteria2、单 peer WireGuard 与 Full Tor 桥的可用性取决于服务器/后端/架构。[完整功能与限制](../../README.zh-CN.md)。

alpha15 修复 Release/R8 删除 Go/JNI 必需成员的打包问题；共用规则同时用于重建 AAR 和应用。保持 R8，不改变 MTU、连接期限、手动连接方式、已有选择或信任确认。CI 通过 50 项 JVM、31 项私有 Debug 设备测试及两项精简私有 Release JNI 检查，无跳过。Release 测试包不复制 Go 接口或 AndroidX 类。公开 APK 另经身份、DEX、ZIP 和 33 个原生库/21 个 ELF64 的 16KB 对齐检查。DNS 二进制不变，三个固定源码的 Xray 产物哈希有变；不宣称全部 native 重建。模拟器 API 30/x86_64 不等于真实网络测试。

直接覆盖公开 alpha14，无需卸载，包名和生产签名相同；旧 alpha11 和私有包不同。[升级](../UPGRADE.md)。所有者已批准发布，但不暗示新增真机测试。流量总量问题据用户反馈已恢复，本次未修改计数器。

## Русский

Rovunet объединяет пути к собственному серверу в коллекции: прямое подключение, отдельные HTTPS/204-измерения внутри туннеля, импорт, защита учётных данных и локальная диагностика по выбору пользователя. Доступны поддерживаемые DNS/SSH-семейства и комбинации, NaiveProxy, SOCKS5, VLESS WS, REALITY, XHTTP, Hysteria2, WireGuard с одним peer и мосты Full Tor; возможности зависят от сервера, движка и ABI. [Функции и ограничения](../../README.ru.md).

Исправлена упаковка Release/R8, удалявшая обязательные Go/JNI-члены. Общие правила включены в AAR и приложение. R8 остаётся включён; MTU, сетевые сроки, прямое подключение, явный выбор и подтверждение доверия сохранены. В CI прошли 50 JVM-тестов, 31 тест приватного Debug и две JNI-проверки минифицированного приватного Release без пропусков. В тестовом Release APK нет копий Go/AndroidX-классов. Публичный APK отдельно проверен на идентичность, DEX, ZIP и 16KB-выравнивание 21 ELF64 среди 33 библиотек. DNS-бинарники не изменены; три Xray-бинарника пересобраны из закреплённого исходника и имеют другие хеши. Полная native-пересборка не заявляется. Эмулятор API 30/x86_64 не заменяет реальную сеть.

Устанавливайте поверх публичной alpha14 без удаления: пакет и производственная подпись прежние. Старая alpha11 и приватные кандидаты отличаются. [Обновление](../UPGRADE.md). Владелец одобрил публикацию; новый результат тестирования на телефоне не подразумевается. По сообщению пользователя счётчик общего трафика восстановился, новой правки счётчика нет.

## Assets and attribution

Full APK, full/light explicit source ZIPs, Persian guide/verification, SOURCE-MANIFEST.json, VERIFICATION.json, public certificate, signer/APK hashes, CI evidence, fix patch, R8 mapping archive and SHA256SUMS.txt. Private keys/passwords and private/unsigned APKs are excluded.

[User guide](../USER_GUIDE.md) · [Build](../BUILDING.md) · [Security](../../SECURITY.md) · [Telegram](https://t.me/RovunetVPN) · [Panel](https://github.com/msVPNco/rovunet-panel).

AGPL-3.0: [LICENSE](../../LICENSE), [NOTICE](../../NOTICE.md). Independently based on SlipNet 2.5.6 Full by Anonvector; not affiliated. Xray-core: MPL-2.0.
