# Signing and upgrade guide / راهنمای امضا و ارتقا

**Alpha21 Full / code46 · prerelease:** Account/source binding and revision high-water, independent private/advisory cache, additive Room50→51, partial metadata without invented usage and diagnostic ownership. [Release, source and limitations](releases/alpha21.md). Same production signer; install over the app without uninstall/clear-data. Seven Room methods and174 JVM tests passed; real main-upgrade/Keystore and device/network/Doze gates remain open. This preview is not Latest; alpha20 is retained.


Alpha20 Full / code43 adds a themed Rovunet startup cover and removes redundant startup digest work; direct encrypted Mahsa fetching, public-source URL privacy, secure active-tunnel update downloads, bounded cancellable file import, red failure labels and small English legal attribution. It also fixes SOCKS domain-length handling in the constrained auxiliary engine. Alpha19 capabilities are retained; this version adds no new protocol.

Owner accepted exact code43 on 2026-11-13; no detailed physical timing/carrier/battery measurements supplied. [Release and limitations](releases/alpha20.md).

Existing capabilities retained in code43 include subscription updates on the collection card, optional names, provider-reported usage/expiry, supported `cs`/`fm`, Shadowsocks 2022, Hy2 port hopping and single-peer AmneziaWG import. It retains the calm Soft Pop themes, visible-only latency monitoring, bounded smart recovery, isolated Slipstream resolver workers and native stop improvements. Patterniha is removed from the built-in catalog; existing imports are preserved.

Automated local/emulator checks do not establish physical-phone battery savings, overnight/Doze stability, carrier reachability or repair of the reported panel WireGuard/Hy2 endpoints. Owner accepted this exact APK for publication; no detailed phone/carrier/battery measurements were supplied. AnyTLS, TUIC v5 and single-binding Mieru are included; see alpha20 details. No new Lite or desktop binary is published.

## Alpha16.1 after the repository rename

**Direct upgrade for public alpha14–alpha16 (through code32):** download the [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk) and install over the existing app **without uninstalling or clearing data**. The old updater embeds the former repository URL; web redirects cannot repair that check. Package and production signer are unchanged.

**ارتقای مستقیم برای آلفا۱۴ تا آلفا۱۶ عمومی (تا کد۳۲):** [APK کامل](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk) را **بدون حذف اپ یا پاک‌کردن داده‌ها** روی برنامهٔ فعلی نصب کنید. آپدیتر قدیمی نشانی قبلی مخزن را درون خود دارد و تغییر مسیر وب آن بررسی را اصلاح نمی‌کند. بسته و امضای تولیدی ثابت‌اند.

**公开 alpha14 至 alpha16（截至代码32）需要直接升级：**下载 [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk) 并覆盖安装，**不要卸载或清除数据**。旧更新器内置原仓库地址，网页重定向不能修复该检查。包名与生产签名不变。

**Для публичных alpha14–alpha16 (до кода32) нужна прямая установка:** скачайте [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk) и установите поверх **без удаления приложения или данных**. Старый обновлятор содержит прежний адрес; веб-перенаправление не исправляет встроенную проверку. Пакет и производственная подпись сохранены.

VersionCode 43 retains `app.veyra.client` and the canonical production certificate. Current Room schema is 50; existing account/profile records are preserved. Provider metadata uses the existing account-status table without a further schema migration. Code37/38 owner-review builds can upgrade to code43. Account metadata never gates manual connection. Android updates now select compatible APKs independently of desktop Latest. Historical alpha11 and independent candidates remain subject to the identity rules below.

## Historical alpha14 signing decision — 2026-11-06

The owner confirmed no deployed users and authorized a new production identity:

```text
1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416
```

Alpha14 Full keeps package `app.veyra.client` but cannot update the historical
alpha11 signer in place. Treat it as a fresh installation; future direct-APK
releases reuse the new managed key. The private candidate remains independent.
[Identity, recovery and no-regeneration policy](SIGNING-IDENTITY.md).

<div dir="rtl">

مالک پروژه پیش از استقرار کاربران، تعویض امضای قدیمی با هویت تولیدی ثابت را تأیید
کرد. آلفا۱۴ روی آلفا۱۱ قدیمی ارتقای مستقیم نیست؛ نسخه‌های عمومی بعدی با همین
کلید تولیدی جدید به‌روز می‌شوند. اگر کانفیگی روی نسخهٔ قدیمی دارید، محدودیت‌های
پشتیبان و قید دستگاه در پایین همچنان برقرارند؛ ابتدا بازیابی‌پذیری را بررسی کنید.

</div>

生产签名已在部署前获批准。相对旧 alpha11 需全新安装；后续直接更新复用新密钥。

Постоянная подпись одобрена до развёртывания. Относительно старой alpha11 нужна
новая установка; будущие обновления используют тот же новый ключ.

## English

**Subscriptions:** choose Update on the collection card to update in place; progress blocks repeated clicks and the outcome stays on Home. Existing-profile replacement still requires a disconnected VPN. Names are optional: explicit/saved name, safe provider title, then hostname. `subscription-userinfo` reports upload + download, volume and expiry from the same response; missing fields stay unknown. Provider snapshots show their age and are not independent metering. No additional periodic full-feed downloads are introduced. Panel status retains its separate contract.

**Imports:** supported VLESS/VMess/Trojan `cs` and `fm` are preserved with verified TLS; unsupported masks/scripts/settings are rejected explicitly. Shadowsocks 2022 validates key lengths. Hy2 supports bounded `mport` lists and `hop-interval`. Standard WireGuard sharing URIs and single-peer AmneziaWG `.conf`/supported URI parameters are accepted. AWG runs in a separate authenticated userspace engine; one address per IP family, no scripts, multi-peer or ranged keepalive. Arbitrary core/Mihomo YAML is not executed.

The public alpha11 package is `app.veyra.client`, code 17, signer SHA-256:

```text
7d9eac2c155ae6c6d57dffa27e325858375016e88f9de1546a1ab97196a1d248
```

Historical private review candidates used `app.veyra.client.candidate` and this signer (ephemeral CI candidates may use another test key):

```text
6a0fd4885b0c01a4d36410b5c6d1e20eab2ea10549753672ac1a9269bd8193e3
```

Code 25 can update code 24 of the private candidate only when its package/signer match. It is not an alpha11 public update. A certificate labelled Android Debug does not imply a universal private key. The exact public alpha11 private key is not available in the current build workspace; an APK contains its public certificate, not the private signing key.

The production key and recovery backup are provisioned and its certificate digest is pinned. The new identity was explicitly authorized; it was not silently substituted. A normal same-package update needs a compatible signer; Android signing rotation requires appropriate old-key ownership and platform support. Do not promise it for all API 24+ devices without testing.

Keep the old app and verify readable exports before any uninstall. Bulk password-encrypted export excludes locked profiles. Sharing restrictions may forbid other export paths; obtain original authorized links or operator reissue. `ANDROID_ID` can change with the signing key on Android 8+, so device-bound profiles may need reissue. Android Keystore secrets are not portable after uninstall. Never automatically clear locks, expiry or device binding to make migration appear to work.

The new unsigned Release artifact is for a trusted offline signer. It cannot be installed and must not be named `Full.apk` or presented as the final public APK. Signed release builds still require `verifyPublicationSigning`; the explicit unsigned-preparation mode does not fall back to debug signing.

## فارسی

آلفا۱۱ عمومی با بستهٔ `app.veyra.client`، کد ۱۷ و اثر انگشت اولِ بالا منتشر شده است. بسته‌های خصوصی `app.veyra.client.candidate` اثر انگشت دوم را دارند. کد ۲۵ فقط با یکسان بودن بسته و امضا روی کد ۲۴ خصوصی ارتقا می‌یابد؛ آپدیت مستقیم آلفا۱۱ نیست.

گواهی Android Debug کلید خصوصی همگانی نیست. کلید خصوصی دقیق آلفا۱۱ در محیط ساخت فعلی موجود نیست و از APK فقط گواهی عمومی استخراج می‌شود. کلید تولیدی مدیریت‌شده و پشتیبان آن آماده و اثر انگشت در پروژه ثابت شده است؛ تعویض با تأیید صریح مالک انجام شد. چرخش امضا به مالکیت کلید قبلی و پشتیبانی نسخهٔ اندروید وابسته است و برای تمام دستگاه‌های اندروید ۷ به بالا تضمین نشده است.

اپ قبلی را نگه دارید و قبل از حذف، خروجی قابل‌خواندن را واقعاً وارد و بررسی کنید. پشتیبان گروهی رمزدار، کانفیگ قفل‌شده را کنار می‌گذارد؛ محدودیت اشتراک هم ممکن است خروجی دیگری را ممنوع کند. در این موارد لینک اصلیِ مجاز یا صدور دوبارهٔ مدیر لازم است. تغییر امضا در اندروید ۸ به بعد ممکن است شناسهٔ دستگاه را تغییر دهد و کانفیگ مقید به دستگاه به صدور دوباره نیاز پیدا کند. رازهای Keystore پس از حذف اپ پشتیبان قابل‌انتقال نیستند. قفل، انقضا یا قید دستگاه برای ظاهراً موفق‌کردن مهاجرت دور زده نمی‌شوند.

فایل Release بدون امضا فقط برای امضاکنندهٔ آفلاین معتبر آماده می‌شود؛ قابل نصب نیست و نباید Full.apk یا نسخهٔ عمومی نهایی معرفی شود. ساخت امضاشدهٔ عمومی همچنان کنترل `verifyPublicationSigning` را لازم دارد؛ حالت صریح آماده‌سازی بدون امضا، به امضای debug برنمی‌گردد.

## 简体中文

公开 alpha11 与私有 Rovunet Test 的包名和签名不同；代码 25 的私有包仅在身份相同时升级私有代码 24，不是公开 alpha11 的原位更新。当前环境没有 alpha11 的私钥，APK 只能提供公开证书。新生产签名、私有恢复备份和固定证书摘要已提供；这不等于原位升级旧版。

保留旧安装并验证可读导出。密码批量导出排除锁定配置，分享限制仍有效；受保护/设备绑定配置可能需要原始授权链接或管理员重发。签名变化可能改变 Android 8+ 的 ANDROID_ID，卸载后 Keystore 不是便携备份。不要绕过锁定、过期或设备限制。未签名 Release 只供可信离线签名，不能安装或作为最终 Full APK 发布。

## Русский

Публичная alpha11 и приватная Rovunet Test имеют разные пакет и подпись. Приватный код 25 обновляет приватный код 24 только при совпадении идентичности и не является обновлением публичной alpha11. Приватного ключа alpha11 в этой среде нет; APK содержит только открытый сертификат. Новый производственный ключ, приватная резервная копия и постоянный отпечаток подготовлены; это не даёт обновления старой версии на месте.

Сохраните старую установку и проверьте читаемый экспорт. Массовый экспорт с паролем исключает заблокированные профили; ограничения обмена сохраняются. Защищённые/привязанные конфигурации могут потребовать исходной разрешённой ссылки или перевыпуска. Смена подписи может изменить ANDROID_ID на Android 8+, а Keystore после удаления не заменяет переносимую копию. Нельзя обходить замок, срок или привязку. Неподписанная Release-заготовка предназначена доверенному офлайн-подписанту, не устанавливается и не публикуется как финальный Full APK.

References: [Android app signing](https://developer.android.com/studio/publish/app-signing), [ANDROID_ID](https://developer.android.com/reference/android/provider/Settings.Secure#ANDROID_ID), [signing lineage](https://source.android.com/docs/security/features/apksigning/v3).
