# alpha15.1 review01 — unpublished

Public channel remains alpha15/code 26. This review targets versionCode 27,
package `app.veyra.client`, with the existing production certificate. Deliver
the signed Full APK privately to the owner; do not publish it before approval.

<div dir="rtl">

## فارسی

- نام نمایشی Lite و معرفی هسته به Rovunet اصلاح شد.
- لینک راهنمای نصب سرور و راهنمای PRISM به پنل Rovunet منتقل شد؛ متن PRISM
  در چهار زبان منابع مستقل دارد. انتساب و مجوز upstream حفظ شده‌اند.
- مقایسهٔ نسخه، اصلاحیه‌هایی مثل 15.1 و 15.10 را عددی می‌خواند.
- این اصلاح در APK جدید به‌تنهایی باعث کشف آن توسط آلفا۱۵ قدیمی نمی‌شود؛
  نصب این بررسی دستی است و مسیر اعلان انتشار عمومی هنوز باید حل شود.
- تغییرات شبکه، MTU، هسته‌ها و توقف native در این بسته انجام نشده‌اند؛
  مشکل توقف طولانی یا SOCKS بدون داده با این تغییرات رفع‌شده اعلام نمی‌شود.

</div>

## English

Product-facing Lite/core naming and server setup/PRISM links now use Rovunet.
PRISM help has four-language resources. Attribution and technical namespaces
remain intact. Version comparison supports numeric alpha maintenance revisions.
Older alpha15 cannot discover a dotted revision by this change alone; this
review is installed manually. No native/network lifecycle fix is claimed.

## 简体中文

Lite 和核心介绍使用 Rovunet；服务器安装与 PRISM 链接指向 Rovunet Panel。
PRISM 帮助提供四种语言。保留原作者声明及技术命名空间。
支持按数值比较 alpha 维护版本；旧 alpha15 仍需手动安装本次测试版。
本次未修改网络或原生引擎停止流程。未经所有者手机测试确认，不公开发布。

## Русский

Название Lite, описание движка и ссылки настройки сервера/PRISM приведены к
Rovunet. Справка PRISM переведена на четыре языка. Атрибуция сохранена.
Сравнение версий поддерживает числовые исправления alpha. Старый alpha15
требует ручной установки этой проверки. Сетевая логика и остановка native
не изменены. Публикация — только после подтверждения владельцем.

## Owner checks

1. Install over public alpha15 without uninstalling; profiles and trust survive.
2. Open the server setup guide and the PRISM panel link.
3. Switch FA/EN/ZH/RU and check PRISM spacing, RTL and links.
4. Connect/disconnect a previously working profile; report any regression.

Build/test revision, signer verification, native parity and actual test results
must accompany the delivered APK. No new phone result is implied by these notes.
