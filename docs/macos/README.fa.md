[English](README.md) · [فارسی](README.fa.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md)

[دانلود 0.1.7-beta](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.7-beta) · [سورس منجمد](https://github.com/msVPNco/rovunet-app/tree/macos-v0.1.7-beta/macos) · [صفحهٔ اصلی](../../README.fa.md)

# رووُنت برای مک — 0.1.7-beta

**پیش‌انتشار آزمایشی محدود · بیلد داخلی 0.1.7.14 · macOS 15 و بالاتر**

رابط و پراکسی محلی HTTP/SOCKS روی Apple Silicon شواهد مثبت دارند. **نسخهٔ اینتل روی مک اینتل واقعی تست نشده است.** TUN سراسری، kill switch و بازیابی هنوز آزمایشی‌اند. نسخهٔ عمومی پس از 0.1.6-beta به 0.1.7-beta رسیده؛ شمارهٔ بیلد داخلی 0.1.7.14 است.

## دانلود

| نوع مک | نصب‌کننده و هش | دامنهٔ آزمون |
| --- | --- | --- |
| Apple Silicon · arm64 | [PKG](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-AppleSilicon-arm64.pkg) · [SHA-256](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/SHA256SUMS.txt) | رابط و پراکسی محلی روی مک سری M بررسی شده؛ پذیرش TUN سراسری باز است |
| Intel · x64 | [PKG · NOT-TESTED](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-Intel-x64-NOT-TESTED.pkg) · [SHA-256](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/SHA256SUMS-Intel.txt) | **روی سخت‌افزار واقعی اینتل تست نشده** |

برای مک‌های سری M بستهٔ arm64 و برای مک اینتل بستهٔ x64 را بگیرید. Air/Pro و اندازهٔ نمایشگر بستهٔ جدا نمی‌خواهند. این انتشار نصب‌کنندهٔ PKG دارد؛ فایل DMG در این نسخه عرضه نشده است.

app/helper امضای محلی **ad hoc** دارند. بسته‌ها **امضای Developer ID Installer و notarization اپل ندارند**. Gatekeeper را خاموش نکنید و تنظیمات امنیتی کلی مک را تضعیف نکنید.

## قابلیت‌های این بیلد

- رابط جمع‌وجور مک، سه تم و چهار زبان؛ دکمه‌های مشخص ذخیره، اعمال و بازگشت در صفحات داخلی.
- کنترل مجموعه‌ها با نمایش پیشرفت و نتیجه؛ اتصال دستی به پینگ یا اسکن DNS وابسته نیست.
- نمایش تأخیر مسیر فعال و اجرای متن لغزان متناسب با محتوای قابل مشاهدهٔ برنامه.
- اشتراک HTTPS و ورود کانفیگ؛ حجم و انقضای گزارش‌شدهٔ ارائه‌دهنده از آزمون مستقل عبور داده جدا هستند.
- اعتماد اولیه به هویت SSH/Slipstream، پراکسی محلی/سیستم اختیاری و TUN آزمایشی با انتخاب کاربر.

## نصب، ارتقا و بازگشت

۱. PKG مناسب پردازنده را بگیرید و با فایل هش همان معماری تطبیق بدهید.

۲. اتصال Rovunet را عادی قطع کنید، با Command-Q برنامه را ببندید، روی نصب قبلی نصب کنید و از Applications باز کنید. نصب، راه‌اندازی helper یا تغییر تنظیمات شبکه ممکن است مجوز مدیر مک بخواهد.

۳. داده‌ها، اشتراک‌ها و Keychain را حذف نکنید. اگر برنامه قفل است، از دکمهٔ صریح بازکردن قفل استفاده کنید. نصب‌کنندهٔ قبلی را برای بازگشت نگه دارید؛ پیش از نصب دوباره، اتصال و برنامه را عادی ببندید و داده‌ها را پاک نکنید.

TUN اختیاری است. در صورت نیاز helper همراه dev.4 و همان معماری را به‌کار ببرید؛ وجود helper قدیمی dev.3 به‌تنهایی سازگاری یا پذیرش این بیلد را ثابت نمی‌کند. برای تست پراکسی محلی روشن‌کردن TUN لازم نیست.

## آزمون‌ها و موارد باز

برای Apple Silicon، ۴۹۱ فایل نصب‌شده و ۲۱۶۳ بررسی هدفمند رابط، اشتراک، شبکه، دامنه، پروتکل و پراکسی ثبت شده است. آزمون ایجنت روی مک مالک شامل عبور بدنهٔ کامل HTTP/SOCKS با HY2 کانادا روی وای‌فای مستقیم و آزمون جداگانهٔ packet ایزوله برای DNS/HTTPS بود. این نتیجهٔ ایجنت است؛ تأیید تازهٔ مالک یا پذیرش TUN سراسری نیست.

اینتل از همان سورس منجمد ساخته شده است. معماری x86_64 در هر ۳۴ فایل Mach-O، امضای محلی/resourceها و تطبیق دقیق ۴۹۲ فایل پس از بازکردن بسته بررسی شد. **نصب، اجرا، رابط، اتصال شبکه و TUN روی مک اینتل واقعی تست نشده‌اند.** [جزئیات بررسی Intel](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/INTEL-VERIFICATION.json).

خرابی VLESS WS TLS که مالک گزارش کرده هنوز در دست بررسی است؛ اصلاح بعدی bootstrap **داخل این پیش‌انتشار نیست**. پروتکل‌های DNS به‌طور عمومی تأیید نشده‌اند؛ WireGuard معمولی، پذیرش بازیابی/kill switch در TUN سراسری، سازگاری private status v2 پنل و مصرف انرژی بلندمدت باز هستند. سند کامل/چنداتصالی Mieru، زنجیرهٔ دلخواه و خارج‌کردن اجباری هر اپ از تونل تضمین نشده‌اند.

## سورس و سابقهٔ انتشار

[ZIP سورس](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-source.zip) · [یادداشت انتشار و هش‌ها](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.7-beta) · [پچ اختیاری از بیلد داخلی 0.1.7.13](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-from-local-0.1.7.13.patch)

مرجع سورس برنامه: `ab577440f17193fd43cfbe761f63e2d2bffdacd8`. شمارهٔ عمومی beta از شمارهٔ بیلد داخلی جداست. Latest گیت‌هاب برای کل مخزن است؛ همیشه انتشار مخصوص macOS را انتخاب کنید. لینک کانفیگ و اطلاعات ورود خصوصی را در گزارش عمومی نگذارید.
