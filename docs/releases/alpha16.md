# Rovunet 0.3.0-alpha16 Full · code 32

## فارسی

آلفا۱۶ نتیجهٔ موفق UDP/TCP در Soar را زودتر نشان می‌دهد، کار اسکن را محدود می‌کند، نمونه‌گیری و نوشتن شمارنده‌ها در بیکاری را کاهش می‌دهد و توقف VLESS و پیروی از شبکهٔ پیش‌فرض اندروید را بهبود می‌دهد. حجم و زمان حساب پنل ۱.۱۹ داخل همان کارت مجموعه دیده می‌شود؛ زبان و راهنما پیش از اشتراک قرار دارند. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42471804819) با **۸۳ آزمون قابل‌حمل/JUnit، ۸۶ آزمون واحد Gradle، ۵۵ تست Debug شبیه‌ساز و ۲ تست JNI نسخهٔ Release فشرده** موفق شد. مالک انتشار همین APK را تأیید کرد. Xray پین‌شده و Hev/JNI پروژه بازسازی شدند؛ بقیهٔ موتورهای بومی بازاستفاده شده‌اند. صرفه‌جویی باتری و رفتار شبانه/جابه‌جایی شبکه هنوز اندازه‌گیری کمی نشده‌اند.

**ارتقا:** [APK کامل](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16/Rovunet-v0.3.0-alpha16-Full.apk) را بدون حذف اپ یا پاک‌کردن داده روی آلفا۱۴ عمومی یا جدیدتر نصب کنید. بسته، امضای تولیدی و پروفایل‌ها حفظ شده‌اند. اگر APK تأییدشدهٔ آلفا۱۶/کد۳۲ را دارید، همین فایل انتشار است و نصب دوباره لازم نیست.

وضعیت حساب پنل ۱.۱۹، حجم باقی‌مانده برحسب GiB و زمان را در همان کارت مجموعه نشان می‌دهد. زرد یعنی حداکثر ۲۰٪ حجم یا ۳ روز؛ قرمز یعنی حداکثر ۵٪ یا ۲۴ ساعت. نامحدود، نامشخص و منقضی جدا هستند. ۴۰۴ یا شمارش قدیمی/ناقص، اطلاعات نامعتبر است و انقضا را ثابت نمی‌کند. وضعیت فقط هنگام دیده‌شدن صفحهٔ اصلی تازه می‌شود و مانع اتصال دستی نیست. مرجع سهمیه و اعمال محدودیت، سرور است.

Prism بعد از رسیدن به آستانهٔ موفقیت انتخاب‌شده متوقف می‌شود؛ نتیجهٔ موفق UDP/TCP مستقل نمایش داده می‌شود. تعداد workerها و کلاینت‌ها محدود است و تشخیص پیشرفته یک مهلت مشترک برای هر resolver دارد. انتخاب صریح TCP و تأیید هویت حفظ می‌شوند. نمونه‌گیری در بیکاری با صفحهٔ خاموش کندتر می‌شود؛ تونل DNS و پوشش ترافیکی صریح حفاظت بیداری را نگه می‌دارند. اپ شبکهٔ پیش‌فرض اندروید را دنبال می‌کند و هیچ رادیویی را خاموش نمی‌کند. گزارش امن نسخهٔ ۵، زمان UTC و نمونهٔ شمارنده، مدت قفل‌ها و نوع شبکه را بدون نشانی یا اعتبار ثبت می‌کند.

## English

Alpha16 shows successful Soar UDP/TCP results sooner, bounds scan work, reduces idle polling and counter writes, improves VLESS WebSocket cleanup and follows Android’s selected default network. Panel v1.19 allowance and expiry appear inside existing collection cards; language and help precede subscriptions. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42471804819) passed **83 portable/JUnit, 86 Gradle unit, 55 Debug emulator and 2 minified Release JNI cases**. The owner accepted this exact APK for publication. Pinned Xray and project Hev/JNI were rebuilt; other native transports are reused. Battery savings and overnight/network-handoff behavior have not been quantitatively measured.

**Upgrade:** install the [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16/Rovunet-v0.3.0-alpha16-Full.apk) over public alpha14 or later, without uninstalling or clearing data. Package, production signer and saved profiles are retained. If the accepted alpha16/code32 review APK is already installed, it is this same binary; no reinstall is needed.

Panel v1.19 account status shows remaining GiB and time in the existing collection card. Amber means ≤20% remaining or ≤3 days; red means ≤5% or ≤24 hours. Unlimited, unknown and expired are distinct. A 404 or stale/incomplete counting is unavailable information, not proof of expiry. Status refresh runs while Home is visible and never blocks manual connection. Server-side metering and limits remain authoritative.

Prism stops after the configured passing threshold; successful UDP/TCP paths appear independently. Bounded workers, shared client limits and one Advanced resolver deadline reduce scan overhead. Saved TCP-only choices and trust approval remain intact. Screen-off idle statistics back off; DNS/explicit cover traffic keep wake protection. Android’s default physical network is followed; the app does not turn either radio off. Safe report v5 adds UTC/sample times, wake-lock durations and the selected network without addresses or credentials.

## 简体中文

alpha16 更早显示 Soar 已验证的 UDP/TCP 路径，限制扫描工作量，减少空闲轮询和计数器写入，改进 VLESS 资源清理并跟随 Android 选择的默认网络。Panel v1.19 的剩余流量和有效期显示在原有配置组卡片中；语言、帮助排在订阅前。[CI](https://github.com/msVPNco/rovunet-app/actions/runs/42471804819) 通过 **83 项可移植/JUnit、86 项 Gradle 单元、55 项 Debug 模拟器及 2 项精简 Release JNI 测试**。所有者批准发布同一个 APK。重建固定版本 Xray 和项目 Hev/JNI，其余原生传输复用。尚未定量测量省电幅度、整夜连接及网络切换表现。

**升级：**直接覆盖安装 [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16/Rovunet-v0.3.0-alpha16-Full.apk)，支持公开 alpha14 及更新版本；不要卸载或清除数据。包名、生产签名和配置保留。如已安装获批准的 alpha16/代码32 测试 APK，这就是同一个文件，无需重装。

Panel v1.19 状态在原卡片中显示剩余 GiB 与时间。黄色：≤20% 或 ≤3 天；红色：≤5% 或 ≤24 小时。不限量、未知和过期分别显示。404、过旧或不完整的计量不是过期证明。仅主页可见时刷新状态，不阻止手动连接；实际计量和限制以服务器为准。

Prism 达到设定的成功阈值即停止；UDP/TCP 成功结果独立显示。工作线程和客户端有共享上限，高级模式对每个 resolver 共用一次截止时间。保留明确的 TCP-only 和身份确认。关屏空闲时降低统计频率；DNS 与明确启用的掩护流量保留唤醒保护。跟随 Android 默认物理网络，不关闭无线电。安全报告 v5 增加 UTC/采样时间、唤醒锁时长与网络类型，不含地址或凭据。

## Русский

Alpha16 раньше показывает успешный UDP/TCP-путь Soar, ограничивает работу сканера, сокращает опрос и запись счётчиков в простое, улучшает завершение VLESS и следует выбранной Android основной сети. Остаток трафика и срок из Panel v1.19 видны в прежней карточке; язык и справка стоят перед подписками. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42471804819): **83 переносимых/JUnit, 86 модульных Gradle, 55 Debug-тестов эмулятора и 2 JNI-теста минифицированного Release**. Владелец одобрил тот же APK. Пересобраны закреплённый Xray и Hev/JNI проекта; остальные native-транспорты повторно использованы. Экономия батареи и ночная работа/смена сети количественно не измерены.

**Обновление:** установите [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha16/Rovunet-v0.3.0-alpha16-Full.apk) поверх публичной alpha14 или новее, не удаляя приложение и данные. Пакет, производственная подпись и профили сохранены. Если одобренный alpha16/код32 уже установлен, это тот же файл; повторная установка не нужна.

Статус Panel v1.19 показывает остаток GiB и времени в прежней карточке. Жёлтый: ≤20% или ≤3 дней; красный: ≤5% или ≤24 часов. Безлимит, неизвестное значение и истечение различаются. 404 и устаревший/неполный учёт не доказывают истечение. Обновление статуса работает при открытой главной странице и не блокирует ручное соединение. Учёт и ограничения определяет сервер.

Prism заканчивает проверку после заданного порога; UDP/TCP показываются независимо. Число рабочих задач/клиентов ограничено, Advanced использует общий срок на resolver. Явный TCP-only и проверка доверия сохранены. В простое с выключенным экраном статистика опрашивается реже; DNS и явно включённый маскирующий трафик сохраняют защиту бодрствования. Приложение следует основной сети Android и не выключает радиомодули. Безопасный отчёт v5 содержит UTC/время выборки, длительность блокировок и тип сети без адресов и секретов.

## Artifact and verification

The owner accepted this exact production-signed APK on 2026-11-08. Package `app.veyra.client`, versionCode **32**, certificate SHA-256 `1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416`. APK SHA-256: **`3950b602865c55a6dfc8bec4cbb13da4585dca0c28ad4842afd6d8b273618b7a`**. Publication does not rebuild or re-sign it. Application source: `ea5558fea6c6a2e9f2b94ef1ce8a230bd2308ba4`; publication/archive revision is recorded separately in attached manifests. V2/v3 signatures, the matching unsigned-CI payload, JNI bindings, 16KB ZIP/ELF64 alignment and native hashes passed.

One initial PR Soar HTTP fixture assertion failed; source CI and the unchanged PR recheck passed. The cause is not established. The separate complete native/signing build job was skipped; pinned Xray, launcher and Hev/JNI were rebuilt, other transports reused from pinned SlipNet 2.5.6. Battery percentage savings, overnight/Doze reliability and physical Wi-Fi/mobile handoff remain unmeasured. Owner acceptance does not convert these into test passes.

Full APK, explicit full/light source ZIPs, Persian guide, R8 mapping, CI evidence, public certificate, provenance and checksums are attached. Source ZIPs retain available pinned native sources and disclose the unavailable historical dnstt-mobile tree; they are not a complete standalone native build kit. No new Lite, proprietary importer, OpenVPN/AmneziaWG, automatic collection failover, throughput benchmark, store or desktop release is included. Manual connection, stored TCP choices and separate identity approval remain intact.

AGPL-3.0 · [LICENSE](https://github.com/msVPNco/rovunet-app/blob/main/LICENSE) · [NOTICE](https://github.com/msVPNco/rovunet-app/blob/main/NOTICE.md). Based on SlipNet 2.5.6 Full by Anonvector; not affiliated. Xray-core: MPL-2.0. [User guide](https://github.com/msVPNco/rovunet-app/blob/main/docs/USER_GUIDE.md) · [Telegram](https://t.me/RovunetVPN).
