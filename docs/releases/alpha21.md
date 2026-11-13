# Rovunet 0.3.0-alpha21 Full · code46 · Public alpha release

## فارسی

آلفا۲۱ وضعیت خصوصی حساب را به منبع، سرور و حساب متصل می‌کند، high-water را در برابر پاسخ دیرهنگام یا تغییر هویت حفظ می‌کند و cache خصوصی و header عمومی را در مهاجرت افزایشی Room50→51 جدا نگه می‌دارد. total/expire مستقل پذیرفته می‌شوند؛ مصرف نامعلوم صفر و باقی‌ماندهٔ ساختگی نمایش داده نمی‌شود. مالکیت diagnostic و سازگاری API24 نیز اصلاح شده‌اند. این آلفا به‌صورت Release عمومی با prerelease=false منتشر می‌شود و Latest نمی‌شود.

نصب روی اپ اصلی با همان package و امضای تولیدی، بدون حذف اپ یا پاک‌کردن داده انجام شود. SourceID/ItemID، نام‌ها، ویرایش محلی، pin، اعتماد TLS، انتخاب‌ها، تنظیمات، سابقه و داده‌های رمز‌شده باید حفظ شوند. آزمون واقعی ارتقای main و Keystore گوشی هنوز انجام نشده است. برگشت اجباری از Room51 به نسخهٔ قدیمی Room50 مسیر آزموده‌شده‌ای نیست؛ داده را حفظ کنید و برای رفع مشکل از نسخهٔ اصلاحی با کد بالاتر یا بازیابی پشتیبانی‌شده و بررسی‌شده استفاده کنید.

## English

Alpha21 binds private account status to its saved source/server/account and preserves revision high-water across late or changed-identity responses. An additive Room50→51 migration separates private authority from public advisory cache. Partial volume/expiry headers retain unknown usage without invented remaining quota. Diagnostic ownership, API24 process compatibility and java.time desugaring are retained. This is a public alpha release with prerelease=false, not Latest. It is not a claim of completed stable/device acceptance.

## Русский

Alpha21 привязывает приватный статус к источнику, серверу и аккаунту, сохраняет верхнюю границу ревизий и разделяет приватный и публичный кеши миграцией Room50→51. Неполные заголовки объёма и срока не превращают неизвестный расход в ноль. Сохранены исправления владельца диагностики и совместимости API24. Это публичный alpha-релиз (prerelease=false), не Latest. Установка поверх основной версии с той же подписью, без удаления приложения и данных; реальное обновление/Keystore и безопасный откат на Room50 ещё не проверены.

## 简体中文

Alpha21 将私有账户状态绑定到来源、服务器和账户，保留修订高水位，并通过增量 Room50→51 迁移分离私有与公共建议缓存。不完整的流量或到期头不会把未知用量当作零，也不虚构剩余额度。保留诊断会话归属和 API24 兼容性修复。此版本为公开 alpha 发行（prerelease=false），不设为 Latest。使用原包名和签名覆盖安装，不卸载或清除数据；真实主应用升级、Keystore 和回退到 Room50 尚未验证。

## Identity and evidence

- Package `app.veyra.client`; version `0.3.0-alpha21`; versionCode `46`; minSdk24, targetSdk35, compileSdk36; Full Release with R8 minification.
- ABI: `arm64-v8a`, `armeabi-v7a`, `x86_64`.
- APK SHA256: `04ba1d0569ca6c994f11b17bfa79eb26ae70d48f2535f944c8ad3feed5635428`.
- Production certificate SHA256: `1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416`; v2/v3 signatures. Offline signing preserves every CI payload entry; no private key/password enters GitHub or assets.
- Application source: `36e5f13e6aa1aa78ba7ea164d95f9f426445930f`, tree `f7a07c83f1712822cea9c8e74013ad4b01d4e1a8`, [PR22](https://github.com/msVPNco/rovunet-app/pull/22). Publication-only documentation/tooling revisions are recorded separately in SOURCE-MANIFEST.
- [APK/JVM CI43504139712](https://github.com/msVPNco/rovunet-app/actions/runs/43504139712): verify and android-device passed; log reports **174 JVM tests**. Separate build job was skipped; android-device built the unsigned public Release APK. Actual checkout `d2130fed20445e355b584914f302dc7476a03c84` has exactly the same tree as the application head.
- [Room CI43504141963](https://github.com/msVPNco/rovunet-app/actions/runs/43504141963): **all seven required methods passed**, verified against raw RESULT and instrumentation method records on accelerated API30/x86_64, isolated Debug package. Actual checkout is the application head; raw metadata incorrectly records the PR merge ref via GITHUB_SHA. Checkout logs and corrected provenance distinguish both identities without editing raw evidence.
- Raw instrumentation SHA256: `75d1fb181a38d7905857f803e54274ffd59606627465320ab70a8310dd769344`. Room evidence, CI logs, R8 mapping, native inventory, full/light explicit sources and checksums accompany this release. Artifact ZIP digests are separate from the APK digest.
- All **39 native ELF hashes** match accepted code43: 14 ARM64, 14 ARMv7, 11 x86_64. The separate ARM64 security candidate is not included. Embedded Go/Brotli versions are recorded in the inventory; native advisory applicability and license/runtime audit remain open. Some inherited native source remains unavailable; no complete native reproducibility/security-clean claim.

## Remaining acceptance and next version

The owner authorized production-package publication after reporting successful isolated-review phone use, without identifying the installed hash, ABI or scenarios. This does not establish a production upgrade, real-device Keystore migration, all-ABI runtime, physical carrier or Doze/battery acceptance. Keep ACT03-ANDROID-01–04 partial and preserve all 119 baseline rows/16 actions. Earlier Mieru/onboarding fixture failures are not newly declared fixed. Owner-reported Hy2 success remains recorded and does not establish WireGuard, TUN or kill-switch acceptance.

Next version: independent private status2 model/parser/store and panel compatibility; at most one expanded subscription/collection card; native/Brotli applicability and runtime/license audit; device upgrade/recovery, network and screen-off/energy gates. Existing private production status remains v1. No panel, server, desktop, account or protocol-engine change is included in this publication. Existing public alpha20 assets and global Latest are preserved.
