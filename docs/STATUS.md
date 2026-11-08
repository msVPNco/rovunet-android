# Rovunet — وضعیت فعلی / Current status

[فارسی](../README.fa.md) · [English](../README.md) · [简体中文](../README.zh-CN.md) · [Русский](../README.ru.md)

<div dir="rtl">

## فارسی

‎**0.3.0-alpha16.1 Full · code 33 · `app.veyra.client`**‎

آلفا۱۶.۱ سازگاری آپدیتر پس از تغییر نام مخزن به `rovunet-app` را اصلاح می‌کند. اندروید اکنون جدیدترین APK سازگار Full/Lite خودش را مستقل از آخرین ریلیز ویندوز/مک و با رعایت گزینهٔ پیش‌انتشار انتخاب می‌کند. بررسی مالکیت نشانی/برچسب/نام فایل، چک‌سام، بسته، نسخه و امضا حفظ شده است. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487016569) با **۹۳ آزمون قابل‌حمل/JUnit، آزمون‌های واحد Gradle، ۵۵ تست Debug شبیه‌ساز و ۲ تست JNI نسخهٔ Release فشرده** موفق شد. مالک انتشار همین APK بررسی‌شده را تأیید کرد. رفتار تونل، باتری، اسکنر، کارت حساب و ترجمه‌ها تغییر نکرده است.

**ارتقای مستقیم برای آلفا۱۴ تا آلفا۱۶ عمومی (تا کد۳۲):** [APK کامل](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16.1/Rovunet-v0.3.0-alpha16.1-Full.apk) را **بدون حذف اپ یا پاک‌کردن داده‌ها** روی برنامهٔ فعلی نصب کنید. آپدیتر قدیمی نشانی قبلی مخزن را درون خود دارد و تغییر مسیر وب آن بررسی را اصلاح نمی‌کند. بسته و امضای تولیدی ثابت‌اند. اگر APK تأییدشدهٔ آلفا۱۶.۱/کد۳۳ نصب است، همین فایل انتشار را دارید و نصب دوباره لازم نیست.

وضعیت حساب پنل ۱.۱۹، حجم باقی‌مانده برحسب GiB و زمان را در همان کارت مجموعه نشان می‌دهد. زرد یعنی حداکثر ۲۰٪ حجم یا ۳ روز؛ قرمز یعنی حداکثر ۵٪ یا ۲۴ ساعت. نامحدود، نامشخص و منقضی جدا هستند. ۴۰۴ یا شمارش قدیمی/ناقص، اطلاعات نامعتبر است و انقضا را ثابت نمی‌کند. وضعیت فقط هنگام دیده‌شدن صفحهٔ اصلی تازه می‌شود و مانع اتصال دستی نیست. مرجع سهمیه و اعمال محدودیت، سرور است.

Prism بعد از رسیدن به آستانهٔ موفقیت انتخاب‌شده متوقف می‌شود؛ نتیجهٔ موفق UDP/TCP مستقل نمایش داده می‌شود. تعداد workerها و کلاینت‌ها محدود است و تشخیص پیشرفته یک مهلت مشترک برای هر resolver دارد. انتخاب صریح TCP و تأیید هویت حفظ می‌شوند. نمونه‌گیری در بیکاری با صفحهٔ خاموش کندتر می‌شود؛ تونل DNS و پوشش ترافیکی صریح حفاظت بیداری را نگه می‌دارند. اپ شبکهٔ پیش‌فرض اندروید را دنبال می‌کند و هیچ رادیویی را خاموش نمی‌کند. گزارش امن نسخهٔ ۵، زمان UTC و نمونهٔ شمارنده، مدت قفل‌ها و نوع شبکه را بدون نشانی یا اعتبار ثبت می‌کند.

</div>

## English

**0.3.0-alpha16.1 Full · code 33 · `app.veyra.client`**

Alpha16.1 fixes update compatibility after the repository was renamed to `rovunet-app`. Android now selects its newest compatible Full/Lite APK from the shared release feed, independently of Windows/macOS Latest and with the preview setting preserved. Repository/tag/filename ownership, checksum, package, version and signer checks remain enforced. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487016569) passed **93 portable/JUnit cases, the Gradle unit-test task, 55 Debug emulator cases and 2 minified Release JNI cases**. The owner authorized publication of the exact reviewed APK. Tunnel, battery, scanner, account-card and translation behavior are unchanged.

**Direct upgrade for public alpha14–alpha16 (through code32):** download the [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16.1/Rovunet-v0.3.0-alpha16.1-Full.apk) and install over the existing app **without uninstalling or clearing data**. The old updater embeds the former repository URL; web redirects cannot repair that check. Package and production signer are unchanged. If the accepted alpha16.1/code33 review APK is installed, it is this same binary; no reinstall is needed.

Panel v1.19 account status shows remaining GiB and time in the existing collection card. Amber means ≤20% remaining or ≤3 days; red means ≤5% or ≤24 hours. Unlimited, unknown and expired are distinct. A 404 or stale/incomplete counting is unavailable information, not proof of expiry. Status refresh runs while Home is visible and never blocks manual connection. Server-side metering and limits remain authoritative.

Prism stops after the configured passing threshold; successful UDP/TCP paths appear independently. Bounded workers, shared client limits and one Advanced resolver deadline reduce scan overhead. Saved TCP-only choices and trust approval remain intact. Screen-off idle statistics back off; DNS/explicit cover traffic keep wake protection. Android’s default physical network is followed; the app does not turn either radio off. Safe report v5 adds UTC/sample times, wake-lock durations and the selected network without addresses or credentials.

## 简体中文

**0.3.0-alpha16.1 Full · code 33 · `app.veyra.client`**

alpha16.1 修复仓库更名为 `rovunet-app` 后的更新兼容。Android 从共享发布列表选择最新兼容 Full/Lite APK，不依赖 Windows/macOS 的全局 Latest，并保留预发布选项。仓库、标签、文件名、校验和、包名、版本及签名检查继续生效。[CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487016569) 通过 **93 项可移植/JUnit、Gradle 单元测试、55 项 Debug 模拟器及 2 项精简 Release JNI 测试**。所有者批准发布同一个已审阅 APK。隧道、电源策略、扫描器、账户卡片与翻译行为不变。

**公开 alpha14 至 alpha16（截至代码32）需要直接升级：**下载 [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16.1/Rovunet-v0.3.0-alpha16.1-Full.apk) 并覆盖安装，**不要卸载或清除数据**。旧更新器内置原仓库地址，网页重定向不能修复该检查。包名与生产签名不变。若已安装获批准的 alpha16.1/代码33 测试 APK，就是同一个文件，无需重装。

Panel v1.19 状态在原卡片中显示剩余 GiB 与时间。黄色：≤20% 或 ≤3 天；红色：≤5% 或 ≤24 小时。不限量、未知和过期分别显示。404、过旧或不完整的计量不是过期证明。仅主页可见时刷新状态，不阻止手动连接；实际计量和限制以服务器为准。

Prism 达到设定的成功阈值即停止；UDP/TCP 成功结果独立显示。工作线程和客户端有共享上限，高级模式对每个 resolver 共用一次截止时间。保留明确的 TCP-only 和身份确认。关屏空闲时降低统计频率；DNS 与明确启用的掩护流量保留唤醒保护。跟随 Android 默认物理网络，不关闭无线电。安全报告 v5 增加 UTC/采样时间、唤醒锁时长与网络类型，不含地址或凭据。

## Русский

**0.3.0-alpha16.1 Full · code 33 · `app.veyra.client`**

Alpha16.1 исправляет обновление после переименования репозитория в `rovunet-app`. Android выбирает самый новый совместимый Full/Lite APK из общего списка независимо от Latest Windows/macOS и сохраняет настройку предварительных выпусков. Проверки репозитория, тега, имени файла, хеша, пакета, версии и подписи действуют. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487016569): **93 переносимых/JUnit, модульные тесты Gradle, 55 Debug-тестов эмулятора и 2 JNI-теста минифицированного Release**. Владелец разрешил публикацию того же проверенного APK. Туннели, энергополитика, сканер, карточки и переводы не менялись.

**Для публичных alpha14–alpha16 (до кода32) нужна прямая установка:** скачайте [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16.1/Rovunet-v0.3.0-alpha16.1-Full.apk) и установите поверх **без удаления приложения или данных**. Старый обновлятор содержит прежний адрес; веб-перенаправление не исправляет встроенную проверку. Пакет и производственная подпись сохранены. Установленная проверенная alpha16.1/код33 уже является этим файлом; переустановка не нужна.

Статус Panel v1.19 показывает остаток GiB и времени в прежней карточке. Жёлтый: ≤20% или ≤3 дней; красный: ≤5% или ≤24 часов. Безлимит, неизвестное значение и истечение различаются. 404 и устаревший/неполный учёт не доказывают истечение. Обновление статуса работает при открытой главной странице и не блокирует ручное соединение. Учёт и ограничения определяет сервер.

Prism заканчивает проверку после заданного порога; UDP/TCP показываются независимо. Число рабочих задач/клиентов ограничено, Advanced использует общий срок на resolver. Явный TCP-only и проверка доверия сохранены. В простое с выключенным экраном статистика опрашивается реже; DNS и явно включённый маскирующий трафик сохраняют защиту бодрствования. Приложение следует основной сети Android и не выключает радиомодули. Безопасный отчёт v5 содержит UTC/время выборки, длительность блокировок и тип сети без адресов и секретов.

Application source: `af237e5e53692862dffb82fe3033e16e9fc83204`. [Release record](releases/alpha16.1.md) · [Provenance](releases/alpha16.1-provenance.json) · [Signing](SIGNING-IDENTITY.md).

Both recorded application CI runs passed without source changes. Publication-only documentation/tooling does not rebuild the accepted APK or count as a new Android test run. Full overnight/Doze, physical-network handoff and battery energy remain unmeasured. No new Lite or desktop installer, protocol/import engine, automatic failover or throughput benchmark is added.
