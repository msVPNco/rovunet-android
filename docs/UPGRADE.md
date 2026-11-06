# Signing and upgrade guide / راهنمای امضا و ارتقا

## English

The public alpha11 package is `app.veyra.client`, code 17, signer SHA-256:

```text
7d9eac2c155ae6c6d57dffa27e325858375016e88f9de1546a1ab97196a1d248
```

Private candidates use `app.veyra.client.candidate`; their signer is:

```text
6a0fd4885b0c01a4d36410b5c6d1e20eab2ea10549753672ac1a9269bd8193e3
```

Code 25 can update code 24 of the private candidate only when its package/signer match. It is not an alpha11 public update. A certificate labelled Android Debug does not imply a universal private key. The exact public alpha11 private key is not available in the current build workspace; an APK contains its public certificate, not the private signing key.

Production publication needs a managed signing key, secure backup, approved certificate digest and an explicit migration plan. No unrelated key is silently substituted. A normal same-package update needs a compatible signer; Android signing rotation requires appropriate old-key ownership and platform support. Do not promise it for all API 24+ devices without testing.

Keep the old app and verify readable exports before any uninstall. Bulk password-encrypted export excludes locked profiles. Sharing restrictions may forbid other export paths; obtain original authorized links or operator reissue. `ANDROID_ID` can change with the signing key on Android 8+, so device-bound profiles may need reissue. Android Keystore secrets are not portable after uninstall. Never automatically clear locks, expiry or device binding to make migration appear to work.

The new unsigned Release artifact is for a trusted offline signer. It cannot be installed and must not be named `Full.apk` or presented as the final public APK. Signed release builds still require `verifyPublicationSigning`; the explicit unsigned-preparation mode does not fall back to debug signing.

## فارسی

آلفا۱۱ عمومی با بستهٔ `app.veyra.client`، کد ۱۷ و اثر انگشت اولِ بالا منتشر شده است. بسته‌های خصوصی `app.veyra.client.candidate` اثر انگشت دوم را دارند. کد ۲۵ فقط با یکسان بودن بسته و امضا روی کد ۲۴ خصوصی ارتقا می‌یابد؛ آپدیت مستقیم آلفا۱۱ نیست.

گواهی Android Debug کلید خصوصی همگانی نیست. کلید خصوصی دقیق آلفا۱۱ در محیط ساخت فعلی موجود نیست و از APK فقط گواهی عمومی استخراج می‌شود. انتشار تولیدی به کلید مدیریت‌شده، پشتیبان امن، اثر انگشت تأییدشده و تصمیم مهاجرت نیاز دارد. کلید دیگری بی‌صدا جایگزین نمی‌شود. چرخش امضا به مالکیت کلید قبلی و پشتیبانی نسخهٔ اندروید وابسته است و برای تمام دستگاه‌های اندروید ۷ به بالا تضمین نشده است.

اپ قبلی را نگه دارید و قبل از حذف، خروجی قابل‌خواندن را واقعاً وارد و بررسی کنید. پشتیبان گروهی رمزدار، کانفیگ قفل‌شده را کنار می‌گذارد؛ محدودیت اشتراک هم ممکن است خروجی دیگری را ممنوع کند. در این موارد لینک اصلیِ مجاز یا صدور دوبارهٔ مدیر لازم است. تغییر امضا در اندروید ۸ به بعد ممکن است شناسهٔ دستگاه را تغییر دهد و کانفیگ مقید به دستگاه به صدور دوباره نیاز پیدا کند. رازهای Keystore پس از حذف اپ پشتیبان قابل‌انتقال نیستند. قفل، انقضا یا قید دستگاه برای ظاهراً موفق‌کردن مهاجرت دور زده نمی‌شوند.

فایل Release بدون امضا فقط برای امضاکنندهٔ آفلاین معتبر آماده می‌شود؛ قابل نصب نیست و نباید Full.apk یا نسخهٔ عمومی نهایی معرفی شود. ساخت امضاشدهٔ عمومی همچنان کنترل `verifyPublicationSigning` را لازم دارد؛ حالت صریح آماده‌سازی بدون امضا، به امضای debug برنمی‌گردد.

## 简体中文

公开 alpha11 与私有 Rovunet Test 的包名和签名不同；代码 25 的私有包仅在身份相同时升级私有代码 24，不是公开 alpha11 的原位更新。当前环境没有 alpha11 的私钥，APK 只能提供公开证书。生产签名需受管理密钥、安全备份及已验证迁移方案。

保留旧安装并验证可读导出。密码批量导出排除锁定配置，分享限制仍有效；受保护/设备绑定配置可能需要原始授权链接或管理员重发。签名变化可能改变 Android 8+ 的 ANDROID_ID，卸载后 Keystore 不是便携备份。不要绕过锁定、过期或设备限制。未签名 Release 只供可信离线签名，不能安装或作为最终 Full APK 发布。

## Русский

Публичная alpha11 и приватная Rovunet Test имеют разные пакет и подпись. Приватный код 25 обновляет приватный код 24 только при совпадении идентичности и не является обновлением публичной alpha11. Приватного ключа alpha11 в этой среде нет; APK содержит только открытый сертификат. Нужны управляемый производственный ключ, безопасная копия и проверенная миграция.

Сохраните старую установку и проверьте читаемый экспорт. Массовый экспорт с паролем исключает заблокированные профили; ограничения обмена сохраняются. Защищённые/привязанные конфигурации могут потребовать исходной разрешённой ссылки или перевыпуска. Смена подписи может изменить ANDROID_ID на Android 8+, а Keystore после удаления не заменяет переносимую копию. Нельзя обходить замок, срок или привязку. Неподписанная Release-заготовка предназначена доверенному офлайн-подписанту, не устанавливается и не публикуется как финальный Full APK.

References: [Android app signing](https://developer.android.com/studio/publish/app-signing), [ANDROID_ID](https://developer.android.com/reference/android/provider/Settings.Secure#ANDROID_ID), [signing lineage](https://source.android.com/docs/security/features/apksigning/v3).
