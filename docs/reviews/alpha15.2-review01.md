# alpha15.2 / code 29 — owner review, unpublished

This combines the remaining owner-requested work. The owner explicitly chose
15.2 for this package, including subscription/QR additions. It updates the public
`app.veyra.client` with the fixed production certificate. No public release is
approved by compilation, emulator testing or delivery.

<div dir="rtl">

## فارسی

- ترجمه و کنترل متن خام به تمام صفحات رابط، خروجی، قفل کانفیگ، زنجیره‌ها و
  انتخاب برنامه‌ها گسترش یافت. عنوان‌های فنی و دادهٔ کاربر ترجمه نمی‌شوند.
- اشتراک HTTPS از ورودی متن یا QR پذیرفته می‌شود؛ پیش‌نمایش و تازه‌سازی یک
  منبع یا همه، ویرایش منبع و جداکردن منبع همراه حفظ کانفیگ‌ها وجود دارد.
- منبع اشتراک با Keystore محافظت می‌شود. نمایش عادی فقط میزبان را نشان می‌دهد.
  تازه‌سازی خودکار اختیاری، حدود هر ۱۲ ساعت روی شبکهٔ بدون هزینهٔ مصرف و با
  باتری مناسب است؛ زمان دقیق تضمین نمی‌شود. برای اعمال و اجرای خودکار، VPN قطع باشد. دریافتِ پیش‌نمایش در اتصال عادی از پروکسی همان اتصال انجام می‌شود و پس از شکست آن به اتصال مستقیم برنمی‌گردد؛ پیش‌نمایش زنجیره به قطع VPN نیاز دارد.
- قالب‌های ورودی: فهرست لینک‌های قابل ورود اپ، همان فهرست در Base64 استاندارد،
  و JSON نسخهٔ ۱ با items شامل id و link. JSON آینده و فیلد ناشناخته، HTML،
  ورودی ناقص/خالی و بیش از ۲ MiB رد می‌شود. خطا کانفیگ‌های قبلی را پاک نمی‌کند.
- حذف از منبع نیاز به بازبینی دارد و کانفیگ از گوشی پاک نمی‌شود؛ به مجموعهٔ محلی منتقل می‌شود. ویرایش محلی، پین، قفل و عضویت زنجیره حفظ
  می‌شوند. شناسهٔ محلی برای آیتم با هویت پایدار حفظ می‌شود. برای لینک‌های خام
  بدون id، تطبیق بر اساس نوع/مقصد/حساب و coreLink است و تضمین هویت کامل نیست.
- QR مجموعه خودبسنده و محلی است؛ ذخیره و اشتراک تصویر هم دارد. مجموعهٔ بزرگ
  به خروجی فایل راهنمایی می‌شود؛ QR چندبخشی یا لینک میزبانی‌شده ساخته نمی‌شود.
- بررسی HTTPS پس از اتصال، به همان اتصال وابسته است و هرگز آن را قطع نمی‌کند.
  این شاهد عبور از پروکسی است، نه اثبات سلامت همهٔ برنامه‌ها یا تمام مسیر TUN.
- موتور Hev و JNI آن از سورس بازسازی می‌شوند: بیدارکردن کارگرهای منتظر پیش از
  join، ثبت توقف زودهنگام و جلوگیری از انتظار برای initializer پایان‌یافته.
  MTU و مهلت‌های شبکه تغییر نکرده‌اند. باقی موتورهای native همان ورودی pin‌شده‌اند.
- گزینهٔ قدیمی پاک‌سازی مخرب دیتابیس ۱ تا ۴ حذف شد. دیتابیس‌های بسیار قدیمی
  نیازمند مسیر مهاجرت/پشتیبان سازگارند؛ داده به‌جای حذف حفظ می‌شود.
- سرویس گزارش خطای آنلاین، تغییر پنل یا تأیید بی‌صدای هویت سرور اضافه نشده است.

</div>

## English

Completes presentation localization and grouped actions; adds HTTPS subscriptions
with encrypted sources, reviewed/transactional refresh, local-edit preservation,
opt-in periodic unmetered updates, and detach without profile deletion. Supports
app-compatible link lists, standard Base64 and strict version-1 JSON with stable
item IDs. Unknown/partial feeds preserve the previous collection. Refresh never deletes saved profiles; removed source entries are retained locally after review. Applying a
refresh and automatic runs require a disconnected VPN. Manual previews on a single connected tunnel use its authenticated local SOCKS endpoint, with remote DNS and no direct fallback; chains require disconnection. Raw lists have documented identity limits.
Adds offline self-contained collection QR, PNG save/share and capacity handling.
Non-blocking session-bound HTTPS evidence checks the local proxy only, never gates
or disconnects a connection. Rebuilds the packet engine/JNI stop fixes from source;
other transports remain pinned upstream payloads. Removes inherited destructive
fallback for unsupported legacy database versions. Phone results remain required.

## 简体中文

完成界面翻译及菜单整理。新增 HTTPS 订阅、加密来源、预览及事务更新、保留本地
修改、可选的不计费网络定期更新，以及保留配置的取消订阅。支持链接列表、标准
Base64 和严格的第 1 版 JSON；未知或不完整数据不会清空旧配置。应用更新前需断开
VPN。新增本地集合二维码及图片保存/分享；过大集合使用文件导出。HTTPS 后台
验证仅针对代理，不阻止或断开连接。数据包引擎与 JNI 停止修复从源码重建，其他
原生传输保持固定版本。移除旧数据库的破坏性迁移回退。发布前需所有者手机确认。

## Русский

Завершены переводы интерфейса и упорядочены меню. Добавлены HTTPS-подписки с
зашифрованным источником, предпросмотром, транзакционным обновлением и сохранением
локальных правок; автоматическое обновление в безлимитной сети включается по выбору.
Поддерживаются списки ссылок, стандартный Base64 и строгий JSON версии 1.
Неполные данные не удаляют старые профили. Для применения VPN должен быть отключён.
Добавлены автономный QR коллекции и сохранение/отправка PNG. Фоновая HTTPS-проверка
касается только прокси и не блокирует подключение. Движок пакетов и JNI с исправлением
остановки собираются из исходников; остальные движки остаются фиксированными.
Удалена разрушительная миграция старых баз. Публикация требует проверки владельцем.

## Source JSON example (synthetic)

```json
{"version":1,"items":[{"id":"stable-item-id","link":"rovunet://..."}]}
```

The optional top-level `name` is informational; the owner's local collection name
is authoritative. Item IDs belong to this source; they are distinct from Android
row IDs. Credentials are never printed or submitted to a third-party QR service.

## Validation to record after the exact build

Actual commit, CI run, JVM/device test outcomes, rebuilt native payload hashes,
certificate, version code and APK SHA-256 belong in the delivery verification.
Until those checks finish, this document is an implementation note, not a pass.
Physical SOCKS/Soar/network results cannot be inferred from emulator fixtures.
