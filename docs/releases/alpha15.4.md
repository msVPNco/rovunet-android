# Rovunet 0.3.0-alpha15.4 — Full

Package: `app.veyra.client` · versionCode **31** · Android 7/API 24+.
Application/build source: `78d1b14426e675c3e2361152a3f0102d31d22bfc`.
APK SHA-256: `0c215077f346ffe5aabb81dd6c586a0059434226f7f369853537c3e98f43ae5a`.
Certificate SHA-256: `1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416`.

<div dir="rtl">

## فارسی

آلفا۱۵.۴ اسکن Soar، ورود و آپدیت اشتراک، منوها، ورود تکراری، مهلت تست و نمایش وضعیت را اصلاح می‌کند. راهنمای انتخاب اتصال و اسکنر از منابع چهار زبان استفاده می‌کنند. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42395755189) با **۶۰ آزمون JVM، ۵۱ تست Debug شبیه‌ساز و ۲ تست JNI نسخهٔ Release فشرده** و بدون skip موفق شد. ۳۳ کتابخانهٔ بومی با بستهٔ تست ۱۵.۳ یکسان‌اند؛ بازسازی کامل native انجام نشده است. مالک انتشار همان APK بررسی‌شده را تأیید کرد؛ گزارش کمی تازه‌ای از شبکهٔ واقعی ارائه نشده است.

**ارتقا از آلفا۱۴/۱۵ عمومی:** آپدیتر قدیمی شمارهٔ اصلاحی اعشاری را نمی‌شناسد. این بار [APK کامل](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha15.4/Rovunet-v0.3.0-alpha15.4-Full.apk) را مستقیم دانلود و روی برنامهٔ فعلی نصب کنید؛ برنامه یا داده‌هایش را حذف نکنید. نسخهٔ ۱۵.۴ شماره‌های اصلاحی بعدی را می‌شناسد. بسته و امضای تولیدی همان قبلی‌اند.

اشتراک HTTPS دو دکمهٔ **آپدیت** و **ایمپورت** دارد. ایمپورت منبع تازه را با یک اقدام ثبت می‌کند و پس از موفقیت کل پنجره بسته می‌شود؛ خطا فرم را حفظ می‌کند و نشانی تکراری دوباره ثبت نمی‌شود. ویرایش‌های محلی حفظ می‌شوند. آپدیت منبع قبلی به قطع VPN نیاز دارد؛ کنارگذاشتن ورودی‌های قدیمی بازبینی می‌خواهد. تازه‌سازی خودکار اختیاری، تقریباً هر ۱۲ ساعت با شرایط مناسب شبکه و باتری انجام می‌شود. QR کل مجموعه و PNG برای مجموعهٔ کوچک وجود دارد؛ مجموعهٔ بزرگ از خروجی فایل استفاده می‌کند.

اسکن Soar چهار حالت سریع، پیشرفته، E2E و Prism دارد. سریع و Prism دست‌دهی معتبر خود Soar و کلید Ed25519 کانفیگ را بررسی می‌کنند؛ E2E پاسخ HTTP 204 یا حساب SSH با هویت ازقبل‌تأییدشده را نیز می‌سنجد. پیشرفته صرفاً تشخیص DNS است. UDP و TCP مستقل آزمایش می‌شوند و پیش‌فرض جست‌وجوی Soar هر دو است؛ سیاست ذخیره‌شدهٔ اتصال عوض نمی‌شود. کنارگذاشتن تأیید قدیمی، فهرست DNS واردشده را پاک نمی‌کند.

[Rovunet](https://github.com/msVPNco/rovunet-app/blob/main/README.fa.md)

</div>

## English

Alpha15.4 fixes Soar scanning, subscription actions, menus, duplicate imports, test budgets and status feedback. The connection-selection guide and scanner use four-language resources. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42395755189) passed **60 JVM, 51 Debug emulator and 2 minified Release JNI tests**, with no device skips. All 33 native libraries match the 15.3 reviewed APK; this is not a complete native rebuild. The owner approved publication of the exact reviewed APK; no detailed new physical-network measurements were supplied.

**Upgrading public alpha14/alpha15:** their older updater does not recognize dotted alpha revisions. Download the [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha15.4/Rovunet-v0.3.0-alpha15.4-Full.apk) directly and install over the existing app without uninstalling or clearing data. Alpha15.4 understands subsequent dotted revisions. Package and production signer are unchanged.

HTTPS subscriptions have **Update** and **Import** actions. Import adds a new source once and closes the dialog after success; errors preserve the form, duplicate source URLs are rejected, and updates preserve local changes. Existing-source updates require VPN disconnection; removals require review. Optional refresh runs approximately every 12 hours under suitable network/battery conditions. Collection QR/PNG sharing is supported when the payload fits; larger collections use files.

Soar scanning offers Quick, Advanced, E2E and Prism. Quick/Prism use authenticated Soar handshakes with the configured Ed25519 key; E2E also checks HTTP 204 or the SSH account with existing trust approval. Advanced is diagnostic DNS only. UDP and TCP are tested independently; Soar scanner defaults to both without rewriting saved connection choices. Imported candidates survive discarded foreign/legacy proof.

[Rovunet](https://github.com/msVPNco/rovunet-app/blob/main/README.md)

## 简体中文

alpha15.4 修复 Soar 扫描、订阅操作、菜单、重复导入、测试预算和状态显示。连接选择指南与扫描器使用四语言资源。[CI](https://github.com/msVPNco/rovunet-app/actions/runs/42395755189) 通过 **60 项 JVM、51 项 Debug 模拟器测试及 2 项精简 Release JNI 测试**，设备测试无跳过。33 个原生库与已审阅的 15.3 APK 相同，不宣称完整 native 重建。所有者批准发布同一个审阅 APK，未提供新的详细真实网络测量。

**从公开 alpha14/alpha15 升级：**旧更新器不识别带点的 alpha 修订号。请直接下载 [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha15.4/Rovunet-v0.3.0-alpha15.4-Full.apk) 并覆盖安装，不卸载、不清除数据。alpha15.4 支持后续带点修订号，包名和生产签名保持不变。

HTTPS 订阅提供 **更新** 和 **导入**。新来源一次导入成功后关闭整个窗口；错误保留表单，重复来源地址被拒绝，本地编辑得到保留。更新已有来源前须断开 VPN；移除旧条目仍需审核。可选自动刷新约每 12 小时在合适的网络/电池条件下运行。小集合可用 QR/PNG 分享，过大集合使用文件。

Soar 扫描有快速、高级、E2E 和 Prism。快速/Prism 用配置的 Ed25519 密钥验证真实 Soar 握手；E2E 还验证 HTTP 204 或使用已批准服务器身份的 SSH 账户。高级模式只诊断 DNS。UDP/TCP 独立测试，Soar 扫描默认两者，不改写已保存的连接选择；丢弃旧协议的证明时保留导入的候选地址。

[Rovunet](https://github.com/msVPNco/rovunet-app/blob/main/README.zh-CN.md)

## Русский

Alpha15.4 исправляет сканирование Soar, подписки, меню, повторный импорт, сроки проверок и индикацию. Руководство выбора подключения и сканер используют четыре языка. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42395755189): **60 JVM, 51 Debug-тест эмулятора и 2 JNI-теста минифицированного Release**, без пропусков. Все 33 native-библиотеки совпадают с проверенной 15.3; полной пересборки native нет. Владелец одобрил публикацию того же APK; новых подробных измерений реальной сети не предоставлено.

**Обновление публичных alpha14/alpha15:** старый обновлятор не распознаёт дробные alpha-ревизии. Скачайте [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha15.4/Rovunet-v0.3.0-alpha15.4-Full.apk) напрямую и установите поверх без удаления приложения или данных. Alpha15.4 распознаёт следующие дробные ревизии; пакет и производственная подпись прежние.

HTTPS-подписки имеют кнопки **Обновить** и **Импорт**. Новая подписка импортируется одним действием, после успеха окно закрывается. Ошибка сохраняет форму; повторный URL отклоняется, локальные изменения сохраняются. Для обновления существующего источника отключите VPN; исчезнувшие записи требуют проверки. Необязательное автообновление выполняется примерно раз в 12 часов при подходящих условиях сети и батареи. Небольшие коллекции передаются QR/PNG, крупные — файлом.

Сканер Soar: быстрый, расширенный, E2E и Prism. Быстрый/Prism проверяют настоящий handshake Soar с заданным ключом Ed25519; E2E дополнительно проверяет HTTP 204 либо SSH с ранее одобренной идентичностью. Расширенный режим — диагностика DNS. UDP/TCP проверяются независимо; по умолчанию сканер Soar использует оба, не переписывая сохранённый режим подключения. Старые доказательства отбрасываются без потери импортированных адресов.

[Rovunet](https://github.com/msVPNco/rovunet-app/blob/main/README.ru.md)

## Verification and limits

The approved signed APK is published unchanged, with v2/v3 signatures, verified package/version/certificate, exact unsigned-CI ZIP payload and 16KB alignment for all 21 ELF64 entries across 33 native libraries. Native binaries match the 15.3 review. The source and publication documentation revisions are recorded separately in SOURCE-MANIFEST.json and VERIFICATION.json. Owner authorization is not a claim of new measured results for every phone, network or Soar+SSH account.

Earlier fixes since public alpha15 include cancellable idle packet-engine shutdown, collection QR/PNG, subscription persistence, menu/localization cleanup, a session-bound three-second success note, state-specific R shields and supported accents, size-based collection-test budgets, real TCP targets and conservative import deduplication. Standard Android controls monochrome status-bar tint. Manual connections, imported choices, trust approval and profile restrictions remain effective.

No new Lite, OpenVPN/AmneziaWG engine, dedicated NPVT/NetMod/NPVS/Happ crypt5 engine, automatic path failover, throughput benchmark or store/desktop release is included. Plain SOCKS5 does not encrypt transport; DoH alone encrypts DNS.

Full APK, explicit full/light source ZIPs, Persian guide, verification/provenance JSON, public certificate, CI evidence, R8 mapping and checksums are attached. Private/unsigned APKs, real configurations, signing keys and recovery passwords are excluded. One historical dnstt-mobile source tree is unavailable; explicit archives record this and reuse only unchanged, hash-verified upstream trees. They are not a complete standalone native build kit.

AGPL-3.0: [LICENSE](https://github.com/msVPNco/rovunet-app/blob/main/LICENSE), [NOTICE](https://github.com/msVPNco/rovunet-app/blob/main/NOTICE.md). Independently based on SlipNet 2.5.6 Full by Anonvector; not affiliated. Xray-core: MPL-2.0. [User guide](https://github.com/msVPNco/rovunet-app/blob/main/docs/USER_GUIDE.md) · [Build](https://github.com/msVPNco/rovunet-app/blob/main/docs/BUILDING.md) · [Telegram](https://t.me/RovunetVPN).
