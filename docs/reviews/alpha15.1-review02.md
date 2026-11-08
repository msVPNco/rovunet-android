# alpha15.1 review02 — unpublished, code 28

The owner reported that review01 only translated PRISM help, leaving the DNS
scanner largely English. It was not accepted for publication. This second
review addresses the scanner and results as complete workflows, not one label.

<div dir="rtl">

## فارسی

- عنوان، راهنما، حالت‌های اسکن، تنظیمات، شمارنده‌ها، بازه‌ها، پنجره‌های ادامه،
  نتایج، فیلترها و پیام‌های خطای اسکن به منابع چهارزبانه منتقل شدند.
- آزمون متن خام اکنون اسکنر و نتایج را هم پوشش می‌دهد؛ قبلاً فقط تنظیمات/About
  را بررسی می‌کرد. دادهٔ کاربر، IP، دامنه و نام پروتکل ترجمه نمی‌شوند.
- منوی مجموعه و کانفیگ به اتصال/آزمون، مدیریت و خروجی تقسیم شد؛ حذف جداست.
  مجموعه در منو هم کنترل صریح اتصال و باز/بسته‌کردن مسیرها دارد.
- گزارش محلی، انتظار runtime، قفل، توقف Hev، پل و بسته‌شدن TUN را جدا ثبت
  می‌کند. این ابزار تشخیص است؛ رفع تأخیر ۱۳۰ثانیه‌ای ادعا نمی‌شود.
- سابسکریپشن و QR کل مجموعه هنوز پیاده نشده‌اند. گزارش آنلاین خطا اضافه نشد.
- امضا و package ثابت‌اند؛ روی نسخهٔ عمومی آلفا۱۵ یا بستهٔ بررسی کد ۲۷ نصب شود.
  برنامه را حذف نکنید. انتشار عمومی فقط بعد از تأیید مالک است.

</div>

## English

Localizes the DNS scanner/results workflows and validation messages in four
languages. Extends raw-UI-text checks to those screens and adds real Compose
tests for Persian modes, locale changes and results. Reorganizes profile and
collection menus, preserving existing permissions and explicit connection.
Adds local teardown stage timing without weakening ownership or promising a
native stop fix. Subscription and collection QR remain pending. No online
reporting, probe gate, MTU change or native-engine patch is included.

## 简体中文

扫描设置、结果、筛选、计数及错误消息使用四语言资源；检查范围现包含
扫描界面，并增加真实 Compose 测试。配置与集合菜单按用途分组，删除独立。
本地诊断分别记录停止各阶段；不宣称已修复原生停止延迟。
订阅和整个集合的二维码尚未实现。仅在所有者手机测试确认后公开发布。

## Русский

Экраны DNS-сканирования, результатов и ошибок переведены на четыре языка.
Проверка строк охватывает эти экраны; добавлены Compose-тесты персидского
интерфейса и смены языка. Меню сгруппированы по назначению, удаление отделено.
Локальная диагностика измеряет этапы остановки; исправление задержки native
не заявляется. Подписки и QR всей коллекции ещё не реализованы.
Публикация — после подтверждения владельцем на телефоне.
