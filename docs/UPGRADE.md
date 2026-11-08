# Signing and upgrade guide / راهنمای امضا و ارتقا

## Alpha16 from public alpha14 or later

**Upgrade:** install the [Full APK](https://github.com/msVPNco/rovunet-android/releases/download/v0.3.0-alpha16/Rovunet-v0.3.0-alpha16-Full.apk) over public alpha14 or later, without uninstalling or clearing data. Package, production signer and saved profiles are retained. If the accepted alpha16/code32 review APK is already installed, it is this same binary; no reinstall is needed.

**ارتقا:** [APK کامل](https://github.com/msVPNco/rovunet-android/releases/download/v0.3.0-alpha16/Rovunet-v0.3.0-alpha16-Full.apk) را بدون حذف اپ یا پاک‌کردن داده روی آلفا۱۴ عمومی یا جدیدتر نصب کنید. بسته، امضای تولیدی و پروفایل‌ها حفظ شده‌اند. اگر APK تأییدشدهٔ آلفا۱۶/کد۳۲ را دارید، همین فایل انتشار است و نصب دوباره لازم نیست.

**升级：**直接覆盖安装 [Full APK](https://github.com/msVPNco/rovunet-android/releases/download/v0.3.0-alpha16/Rovunet-v0.3.0-alpha16-Full.apk)，支持公开 alpha14 及更新版本；不要卸载或清除数据。包名、生产签名和配置保留。如已安装获批准的 alpha16/代码32 测试 APK，这就是同一个文件，无需重装。

**Обновление:** установите [Full APK](https://github.com/msVPNco/rovunet-android/releases/download/v0.3.0-alpha16/Rovunet-v0.3.0-alpha16-Full.apk) поверх публичной alpha14 или новее, не удаляя приложение и данные. Пакет, производственная подпись и профили сохранены. Если одобренный alpha16/код32 уже установлен, это тот же файл; повторная установка не нужна.

VersionCode 32 retains `app.veyra.client` and the canonical production certificate. Room upgrades 48 → 49 add a separate account-status cache; normal subscription updates preserve it. Account metadata does not gate connections. Historical alpha11 and independent candidates remain subject to the identity rules below.

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
