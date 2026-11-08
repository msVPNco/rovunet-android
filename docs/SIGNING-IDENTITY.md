# Rovunet production signing identity

Effective from **0.3.0-alpha14 / versionCode 25**, package `app.veyra.client`.

```text
Certificate SHA-256
1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416
Subject: CN=Rovunet Release, O=Rovunet
Key alias: rovunet-release
Algorithm: RSA-3072 / SHA256withRSA
```

Alpha16/code 32 retains this exact certificate and updates public alpha14/code 25 through alpha15.4/code 31 in place; no key rotation is performed. The publication reuses the exact owner-reviewed APK.

The certificate digest is public; the private key and its password are not. The
release build checks the versioned pin in
`config/release-signing-certificate.sha256`. An optional local/CI digest must
agree with this pin. A workspace reset is never a reason to generate another key.
Recover the same key from the owner's private backup instead.

The private recovery files are `Rovunet-production-signing-2026.p12` and
`Rovunet-production-signing-RECOVERY-PRIVATE.txt`. They are stored separately from
the public repository/release. They must never be uploaded as release assets,
committed, included in source archives, or put in logs. Verify the recovered
certificate against the pin before signing. The password is recorded only in
the private recovery document. Back up both files outside an ephemeral build
environment. See [release procedure](RELEASING.md).

## Decision and historical compatibility

On 2026-11-06 the owner confirmed that no users had deployed the previous public
release and explicitly authorized replacing the historical signing identity.
Alpha14 therefore starts a new managed direct-APK identity, without claiming an
in-place upgrade from alpha11's different signer. Private `Rovunet Test` packages
remain separate. Future direct APKs must reuse this exact production key unless
another explicit rotation and its installation consequences are reviewed.

Play App Signing is not configured. Before adding a store, decide its signing
model explicitly; do not assume the upload key is the installed app signing key.

## فارسی

از آلفا۱۴، کد ۲۵، هویت تولیدی بالا برای بستهٔ عمومی ثابت است. مالک پروژه تعویض
امضای قدیمی را پیش از استقرار کاربران تأیید کرده است. در نسخه‌های بعدی همین کلید
استفاده شود؛ حافظهٔ گفتگو یا باقی‌ماندن فایل روی ماشین ساخت، پشتیبان کلید نیست.
اگر محیط عوض شد، کلید را از دو فایل خصوصی بازیابی کنید و اثرانگشت را با مقدار
بالا بسنجید. کلید/رمز در گیت‌هاب یا سورس عمومی قرار نمی‌گیرد. نصب روی آلفا۱۱ با
امضای قبلی، ارتقای مستقیم محسوب نمی‌شود؛ راهنما در [UPGRADE](UPGRADE.md) است.

## 简体中文

从 alpha14/代码 25 起，公开包固定使用上述生产签名。项目所有者已在用户部署前
批准替换旧签名。后续直接 APK 必须复用此密钥；工作区重置不能触发重新生成。
从所有者私有备份恢复并核对证书摘要，不将私钥/密码提交到公开仓库或发行附件。
此签名不能原位升级旧 alpha11。Play App Signing 尚未配置。

## Русский

Начиная с alpha14/кода 25 публичный пакет использует указанную производственную
подпись. Владелец одобрил замену до развёртывания у пользователей. Будущие прямые
APK используют тот же ключ; после сброса рабочей среды его восстанавливают из
приватной копии и сверяют отпечаток. Приватный ключ/пароль не публикуются в репозитории
или релизе. Это не обновление старой alpha11 на месте. Play App Signing не настроен.
