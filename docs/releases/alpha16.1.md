# Rovunet 0.3.0-alpha16.1 Full · code 33

<div dir="rtl">

## فارسی

آلفا۱۶.۱ سازگاری آپدیتر پس از تغییر نام مخزن به `rovunet-app` را اصلاح می‌کند. اندروید اکنون جدیدترین APK سازگار Full/Lite خودش را مستقل از آخرین ریلیز ویندوز/مک و با رعایت گزینهٔ پیش‌انتشار انتخاب می‌کند. بررسی مالکیت نشانی/برچسب/نام فایل، چک‌سام، بسته، نسخه و امضا حفظ شده است. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487016569) با **۹۳ آزمون قابل‌حمل/JUnit، آزمون‌های واحد Gradle، ۵۵ تست Debug شبیه‌ساز و ۲ تست JNI نسخهٔ Release فشرده** موفق شد. مالک انتشار همین APK بررسی‌شده را تأیید کرد. رفتار تونل، باتری، اسکنر، کارت حساب و ترجمه‌ها تغییر نکرده است.

**ارتقای مستقیم برای آلفا۱۴ تا آلفا۱۶ عمومی (تا کد۳۲):** [APK کامل](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16.1/Rovunet-v0.3.0-alpha16.1-Full.apk) را **بدون حذف اپ یا پاک‌کردن داده‌ها** روی برنامهٔ فعلی نصب کنید. آپدیتر قدیمی نشانی قبلی مخزن را درون خود دارد و تغییر مسیر وب آن بررسی را اصلاح نمی‌کند. بسته و امضای تولیدی ثابت‌اند. اگر APK تأییدشدهٔ آلفا۱۶.۱/کد۳۳ نصب است، همین فایل انتشار را دارید و نصب دوباره لازم نیست.

این انتشار فقط سازگاری با تغییر نام مخزن را تکمیل می‌کند. توسعهٔ قابلیت‌های اندروید همچنان متوقف است. ویندوز و macOS آینده در همین مخزن، انتشارهای مخصوص خود را دارند؛ این انتشار اندروید نصب‌کنندهٔ دسکتاپ اضافه نمی‌کند. وضعیت حساب، Soar، اتصال و رفتار بیکاری الفا۱۶ حفظ شده‌اند.

Xray پین‌شده، راه‌انداز و Hev/JNI پروژه در CI اپ بازسازی شدند؛ بقیهٔ موتورهای بومی از نسخهٔ پین‌شدهٔ SlipNet 2.5.6 بازاستفاده شده‌اند. کار جداگانهٔ بازسازی کامل native/امضا اجرا نشده است. این نتیجه، بازسازی کامل همهٔ موتورها یا اندازه‌گیری کمی تازهٔ باتری و شبکهٔ گوشی نیست.

</div>

## English

Alpha16.1 fixes update compatibility after the repository was renamed to `rovunet-app`. Android now selects its newest compatible Full/Lite APK from the shared release feed, independently of Windows/macOS Latest and with the preview setting preserved. Repository/tag/filename ownership, checksum, package, version and signer checks remain enforced. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487016569) passed **93 portable/JUnit cases, the Gradle unit-test task, 55 Debug emulator cases and 2 minified Release JNI cases**. The owner authorized publication of the exact reviewed APK. Tunnel, battery, scanner, account-card and translation behavior are unchanged.

**Direct upgrade for public alpha14–alpha16 (through code32):** download the [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16.1/Rovunet-v0.3.0-alpha16.1-Full.apk) and install over the existing app **without uninstalling or clearing data**. The old updater embeds the former repository URL; web redirects cannot repair that check. Package and production signer are unchanged. If the accepted alpha16.1/code33 review APK is installed, it is this same binary; no reinstall is needed.

This is the repository-rename compatibility release. Android feature development remains paused. Windows and future macOS clients share this repository but use their own releases; no desktop installer is added by this Android release. The existing alpha16 account status, Soar, connection and idle behavior is retained.

Pinned Xray, the launcher and project Hev/JNI were rebuilt in application CI; other transports reuse pinned SlipNet 2.5.6 binaries. The separate complete native/signing job was skipped. This is not a complete native rebuild or a new quantitative phone battery/network measurement.

## 简体中文

alpha16.1 修复仓库更名为 `rovunet-app` 后的更新兼容。Android 从共享发布列表选择最新兼容 Full/Lite APK，不依赖 Windows/macOS 的全局 Latest，并保留预发布选项。仓库、标签、文件名、校验和、包名、版本及签名检查继续生效。[CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487016569) 通过 **93 项可移植/JUnit、Gradle 单元测试、55 项 Debug 模拟器及 2 项精简 Release JNI 测试**。所有者批准发布同一个已审阅 APK。隧道、电源策略、扫描器、账户卡片与翻译行为不变。

**公开 alpha14 至 alpha16（截至代码32）需要直接升级：**下载 [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16.1/Rovunet-v0.3.0-alpha16.1-Full.apk) 并覆盖安装，**不要卸载或清除数据**。旧更新器内置原仓库地址，网页重定向不能修复该检查。包名与生产签名不变。若已安装获批准的 alpha16.1/代码33 测试 APK，就是同一个文件，无需重装。

本次仅完成仓库更名兼容，Android 新功能开发继续暂停。Windows 与未来 macOS 在同一仓库使用各自的发布渠道；本次 Android 发布不增加桌面安装包。保留 alpha16 的账户状态、Soar、连接与空闲行为。

应用 CI 重建固定版本 Xray、launcher 和项目 Hev/JNI，其余传输复用固定版本 SlipNet 2.5.6。单独的完整 native/签名构建未运行，不宣称全部原生引擎重建或新的手机电池、网络定量测量。

## Русский

Alpha16.1 исправляет обновление после переименования репозитория в `rovunet-app`. Android выбирает самый новый совместимый Full/Lite APK из общего списка независимо от Latest Windows/macOS и сохраняет настройку предварительных выпусков. Проверки репозитория, тега, имени файла, хеша, пакета, версии и подписи действуют. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487016569): **93 переносимых/JUnit, модульные тесты Gradle, 55 Debug-тестов эмулятора и 2 JNI-теста минифицированного Release**. Владелец разрешил публикацию того же проверенного APK. Туннели, энергополитика, сканер, карточки и переводы не менялись.

**Для публичных alpha14–alpha16 (до кода32) нужна прямая установка:** скачайте [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16.1/Rovunet-v0.3.0-alpha16.1-Full.apk) и установите поверх **без удаления приложения или данных**. Старый обновлятор содержит прежний адрес; веб-перенаправление не исправляет встроенную проверку. Пакет и производственная подпись сохранены. Установленная проверенная alpha16.1/код33 уже является этим файлом; переустановка не нужна.

Это выпуск совместимости с переименованием. Разработка новых функций Android остаётся приостановленной. Windows и будущая macOS используют тот же репозиторий со своими выпусками; этот Android-релиз не добавляет настольный установщик. Функции аккаунта, Soar, подключения и простоя alpha16 сохранены.

В CI пересобраны закреплённый Xray, launcher и Hev/JNI проекта; остальные транспорты взяты из закреплённой SlipNet 2.5.6. Отдельная полная сборка native/подписи пропущена. Полной пересборки всех движков и новых количественных измерений батареи/сети телефона не заявляется.

## Artifact and provenance

The owner authorized this exact delivered APK on 2026-11-08. SHA-256: **`133a0a4f1aa97ddd9eb42c8ec5b0be8ca53c00ed5e33811e0a585ab773e3fc28`**, size **97,282,769 bytes**, package `app.veyra.client`, versionCode **33**. Production certificate SHA-256: `1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416`. The accepted APK is neither rebuilt nor re-signed for publication.

Application source: `af237e5e53692862dffb82fe3033e16e9fc83204`; publication/archive identity is recorded separately. Both application CI runs passed. V2/v3 signatures, exact unsigned-CI payload equality, 16KB ZIP/ELF64 alignment, JNI definitions and canonical repository strings were verified. The separate complete native/signing build was skipped; no complete native rebuild or new detailed phone battery/overnight/network measurements are claimed.

Full APK, full/light explicit source ZIPs, Persian guide, public certificate, CI evidence, R8 mapping, verification/provenance and checksums are attached. Source ZIPs expand available pinned native trees and disclose the unavailable historical dnstt-mobile tree. They are not a complete standalone native build kit. No new Lite APK, desktop installer, protocol, importer, scanner, power-policy or UI redesign is included. Public alpha16/code32 artifacts remain unchanged.

AGPL-3.0 · [LICENSE](https://github.com/msVPNco/rovunet-app/blob/main/LICENSE) · [NOTICE](https://github.com/msVPNco/rovunet-app/blob/main/NOTICE.md). Based on SlipNet 2.5.6 Full by Anonvector; not affiliated. Xray-core: MPL-2.0. [User guide](https://github.com/msVPNco/rovunet-app/blob/main/docs/USER_GUIDE.md) · [Telegram](https://t.me/RovunetVPN).
