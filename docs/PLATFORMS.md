# Shared Rovunet application repository

Canonical repository: **[msVPNco/rovunet-app](https://github.com/msVPNco/rovunet-app)**.
The original repository was renamed; its numeric identity remains 1445854036.

| Platform | Current status | Release channel | Installable asset |
| --- | --- | --- | --- |
| Android | alpha19/code40 public; owner-approved public subscriptions and broader imports | Existing `v<version>` tags | Signed Full/Lite `.apk`, when provided |
| Windows | Public **0.1.1 Beta x64** (pre-release); [scope and limits](../windows/docs/STATUS.md) | `windows-v0.1.1-beta` (prerelease) | [Windows installer/source](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.1-beta) |
| Linux | Planned for this repository | `linux-v<version>` (proposed; not yet used) | Linux-specific package, when implemented |
| macOS | **0.1.7-beta Local Preview**, internal build0.1.7.14; Apple Silicon local proxy/UI evidence; **Intel: not tested on physical hardware**; macOS 15+; global TUN experimental | `macos-v0.1.7-beta` (prerelease) | [arm64 / Intel PKG](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.7-beta) · [scope and installation](macos/README.md) |

This records repository organization, not a claim of desktop protocol support,
installation testing, code signing or a completed installer. Desktop work must
remain separate from the current Android application/build tree. Do not move
Android files merely to rename the repository. A platform-specific directory is
appropriate for new desktop code; coordinate its path with the existing work.

## Update and download rules

- GitHub Latest is shared by the whole repository; it is not a per-platform feed.
- Android checks published numeric Android releases with an unambiguous compatible
  APK. Windows/macOS releases, source archives, unsigned/test APKs and incompatible
  Full/Lite assets must not become an Android update. Preserve the preview setting.
- Keep source identity, version ordering, signing and checksums per platform.
  Use pinned platform download links in documentation; do not combine a fixed
  Android filename with `/releases/latest/download/`.
- Repository/asset URL checks remain narrow. Never accept another repository,
  disable checksum/signature verification or change the Android package/signer
  just to make a renamed download work.
- Existing alpha16/code32 embeds the former repository URL and needs one direct
  compatibility APK installation using the published alpha19 Full APK. Changing
  web links alone does not update an installed APK.
- Public alpha16 assets remain unchanged. Historical manifests, APK reconstruction
  records and archived PDF editions keep their original release identities.

<div dir="rtl">

## فارسی

همهٔ کلاینت‌ها در همین مخزن قرار می‌گیرند؛ مخزن مستقل ویندوز ساخته نمی‌شود.
توسعهٔ اندروید با دستور مالک از سر گرفته شده و آلفا۱۹ با کد ۴۰ شامل اصلاح اشتراک‌ها و سازگاری پروتکل‌هاست. نسخه‌های ویندوز و مک، برچسب، فایل و امضای مخصوص خود را دارند.
«آخرین انتشار» گیت‌هاب می‌تواند متعلق به هر پلتفرم باشد؛ آپدیتر و لینک دانلود هر
کلاینت باید نسخهٔ سازگار خودش را انتخاب کند. انتشار نسخهٔ تازه به تأیید مالک نیاز
دارد. قالب فعلی READMEها و سابقهٔ انتشارهای قبلی حفظ می‌شوند.

</div>

## Русский

Android, Windows и macOS находятся в одном репозитории. У каждой платформы
свои теги, файлы, хеши и подпись; общий Latest не является каналом Android.
Разработка Android приостановлена, кроме явно запрошенной совместимости с новым
именем. Новая публикация требует одобрения владельца; оформление README и история
старых выпусков сохраняются.

## 简体中文

Android、Windows 与 macOS 共用此仓库，各平台使用独立标签、文件、校验和及签名。
全仓库 Latest 不是 Android 专用更新渠道。所有者已恢复 Android 开发并批准 alpha19；后续反馈进入待办。新版本须经所有者批准发布，保留 README 版式与历史发布证据。

