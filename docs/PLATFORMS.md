# Shared Rovunet application repository

Canonical repository: **[msVPNco/rovunet-app](https://github.com/msVPNco/rovunet-app)**.
The original repository was renamed; its numeric identity remains 1445854036.

| Platform | Current status | Release channel | Installable asset |
| --- | --- | --- | --- |
| Android | alpha16.1/code33 public; repository-rename compatibility released | Existing `v<version>` tags | Signed Full/Lite `.apk`, when provided |
| Windows | Development in this repository; no validated public GUI release recorded here | `windows-v<version>` | Windows-specific installer/portable package |
| Linux | Planned for this repository | `linux-v<version>` (proposed; not yet used) | Linux-specific package, when implemented |
| macOS | Planned for this repository | `macos-v<version>` | macOS-specific package, when implemented |

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
  compatibility APK installation using the published alpha16.1 Full APK. Changing
  web links alone does not update an installed APK.
- Public alpha16 assets remain unchanged. Historical manifests, APK reconstruction
  records and archived PDF editions keep their original release identities.

<div dir="rtl">

## فارسی

همهٔ کلاینت‌ها در همین مخزن قرار می‌گیرند؛ مخزن مستقل ویندوز ساخته نمی‌شود.
توسعهٔ عمومی اندروید فعلاً متوقف است و فقط سازگاریِ صریحاً درخواست‌شده با تغییر نام
در دست بررسی است. نسخه‌های ویندوز و مک، برچسب، فایل و امضای مخصوص خود را دارند.
«آخرین انتشار» گیت‌هاب می‌تواند متعلق به هر پلتفرم باشد؛ آپدیتر و لینک دانلود هر
کلاینت باید نسخهٔ سازگار خودش را انتخاب کند. انتشار نسخهٔ تازه به تأیید مالک نیاز
دارد. قالب فعلی READMEها و سابقهٔ انتشارهای قبلی حفظ می‌شوند.

</div>

## Русский

Android, Windows и будущая macOS находятся в одном репозитории. У каждой платформы
свои теги, файлы, хеши и подпись; общий Latest не является каналом Android.
Разработка Android приостановлена, кроме явно запрошенной совместимости с новым
именем. Новая публикация требует одобрения владельца; оформление README и история
старых выпусков сохраняются.

## 简体中文

Android、Windows 与未来 macOS 共用此仓库，各平台使用独立标签、文件、校验和及签名。
全仓库 Latest 不是 Android 专用更新渠道。Android 常规开发暂停，更名兼容已在 alpha16.1 完成，本次不恢复常规功能开发。新版本须经所有者批准发布，保留 README 版式与历史发布证据。
