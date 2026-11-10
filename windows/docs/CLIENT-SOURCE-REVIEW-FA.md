# بررسی سورس چهار کلاینت و کاربرد آن در Rovunet

بررسی فقط خواندنی در ۲۰۲۶-۱۱-۰۸ انجام شد. فایل‌های مرتبط خوانده شدند، نه اینکه بررسی
کامل همهٔ فایل‌های چهار مخزن ادعا شود. مسیر و SHA هر blob در CLIENT-SOURCE-REVIEW.json است.

| پروژه | بخش خوانده‌شده | تصمیم در Rovunet |
|---|---|---|
| [v2rayN](https://github.com/2dust/v2rayN) | MainWindow.xaml، StatusBarView.xaml، AddServerWindow.xaml | ورودی لینک/کلیپ‌بورد، QR و فرم دستی مجزا؛ نوار اتصال خارج از ScrollViewer |
| [Hiddify](https://github.com/hiddify/hiddify-app) | app_theme.dart، home_page.dart، connection_stats_card.dart | تم روشن و تاریک با ساختار مشترک؛ کنترل اصلی اتصال و آمار مستقل |
| [Clash Verge Rev](https://github.com/clash-verge-rev/clash-verge-rev) | home.tsx و enhanced-traffic-stats.tsx | سرعت ارسال/دریافت، شمارندهٔ مجموع و نمودار از دادهٔ واقعی |
| [FlClash](https://github.com/chen08209/FlClash) | home.dart و active_polling.dart | حفظ کنترل/فوکوس، نمونه‌برداری فقط در پنجرهٔ فعال، توقف هنگام پنهان/کوچک‌شدن |

رنگ و هویت بصری از دو تصویر روشن و تاریک مالک گرفته شده‌اند. WPF مستقل نوشته شده؛
هیچ کد، آیکن یا asset این چهار رابط کپی یا وارد محصول نشده است. Hiddify مجوز سفارشی
با شروط اضافه دارد و به‌عنوان GPL ساده ثبت نشده است. مجوزهای سه پروژهٔ دیگر GPL-3.0 هستند.
این گزارش ادعای انتقال قابلیت پروتکل این پروژه‌ها به Rovunet نیست.

## اجزایی که واقعاً استفاده می‌شوند

- Xray، Naive، Slipstream و ماژول‌های Go قبلی با pin و انتساب موجود؛ NOTICE.md،
  engines/transports-pins.json و licenses/Go/INDEX.json جزئیات هر منبع را نگه می‌دارند.
- ZXing.Net 0.16.11 برای QR، Apache-2.0؛ مجوز همراه و SHA512 بسته در SOURCE-REFERENCES.json.
- Vazirmatn v33.003، OFL؛ برای هماهنگی فارسی و لاتین.
- قرارداد protobuf آمار Xray خوانده و کلاینت محدود C# مستقل نوشته شده است؛
  فقط StatsService و شمارنده‌های proxy، بدون سرویس تغییر تنظیمات یا شناسایی کاربران.
- پنل خود Rovunet فقط برای قرارداد حساب/خروجی و منطق سمت سرور خوانده شده است؛
  کد نصب/مدیریت سرور از این چهار کلاینت یا پنل دیگری وارد Windows نشده است.

هر استفادهٔ آینده از کد باید به‌صورت «مخزن، commit یا نسخه، فایل، مجوز، تغییرات»
در همین پرونده و NOTICE ثبت شود. الهام از الگوی عمومی رابط با کپی واقعی کد یکی ثبت نمی‌شود.
