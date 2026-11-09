<div dir="rtl">

<p align="center"><img src="assets/rovunet-logo.png" alt="Rovunet" width="140"></p>

<h1 align="center">رووُنت (Rovunet)</h1>

<p align="center"><b>چند مسیر به سرور خودتان، روی هر دستگاه</b><br>تونل‌های DNS، SSH، پروکسی‌های HTTPS و پروفایل‌های Xray در مجموعه‌های نام‌دار، با اتصال مستقیم و تست واقعی HTTPS.</p>

<p align="center" dir="ltr">
  <a href="https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha17"><img alt="Android release" src="https://img.shields.io/github/v/release/msVPNco/rovunet-app?sort=date&display_name=tag&filter=v*&label=android&color=0E7C86"></a>
  <img alt="Platforms" src="https://img.shields.io/badge/platforms-Android%20%C2%B7%20Windows%20%C2%B7%20Linux%20%C2%B7%20macOS-14304D">
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0-6D4AFF"></a>
  <a href="https://t.me/RovunetVPN"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-RovunetVPN-2F6FEB?logo=telegram&logoColor=white"></a>
</p>

<p align="center" dir="ltr"><a href="README.md">English</a> · <b>فارسی</b> · <a href="README.ru.md">Русский</a> · <a href="README.zh-CN.md">简体中文</a></p>

<p align="center">📥 <a href="#apps">دانلود</a> · 📘 <a href="docs/guides/Rovunet-App-Guide-FA.pdf">راهنمای جامع (PDF)</a> · <a href="docs/USER_GUIDE.md">راهنمای کاربر</a> · <a href="https://github.com/msVPNco/rovunet-panel">پنل سرور</a></p>

---

**رووُنت مجموعه‌ای از کلاینت‌های VPN برای دسترسی به سرور شخصی از چند مسیر متفاوت است.** تونل‌های DNS، روش‌های اتصال SSH، پراکسی‌های HTTPS و کانفیگ‌های مبتنی بر Xray را در یک رابط جمع می‌کند. مسیرهای مرتبط در مجموعه‌های نام‌دار کنار هم می‌مانند؛ مسیر دلخواه را مستقیم وصل کنید یا جداگانه HTTPS را از داخل تونل اندازه بگیرید. همهٔ کلاینت‌ها در همین یک مخزن‌اند و سمت سرور آن [پنل رووُنت](https://github.com/msVPNco/rovunet-panel) است.

<a id="apps"></a>

## برنامه‌ها

| پلتفرم | وضعیت | دانلود و جزئیات |
| --- | --- | --- |
| **اندروید** | انتشار عمومی: **آلفا۱۷ Full با کد نسخهٔ ۳۶** | [دانلود](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha17) · [صفحهٔ اندروید](docs/android/README.fa.md) |
| **ویندوز** | در حال توسعه؛ هنوز انتشار عمومی تأییدشده ندارد | انتشارها با برچسب `windows-v<version>` خواهند بود |
| **لینوکس** | برنامه‌ریزی‌شده | هنوز انتشاری ندارد |
| **مک** | برنامه‌ریزی‌شده | انتشارها با برچسب `macos-v<version>` خواهند بود |

هر پلتفرم برچسب، فایل، هش و امضای جداگانهٔ خودش را دارد. «آخرین انتشار» گیت‌هاب برای کل مخزن است و فید مخصوص هر پلتفرم نیست؛ پس همیشه از انتشاری دانلود کنید که نام پلتفرم شما را دارد. قاعدهٔ برچسب‌ها و به‌روزرسانی: [پلتفرم‌ها و کانال‌های انتشار](docs/PLATFORMS.md). تا وقتی یک انتشار صریحاً نگفته باشد، هیچ چیز در این جدول ادعای پشتیبانی پروتکل یا آزمون نصب روی دسکتاپ نیست.

## رووُنت چه می‌کند

توضیح‌ها فعلاً دربارهٔ اندروید است؛ پلتفرم‌های دیگر پس از داشتن انتشار به این فهرست اضافه می‌شوند.

- **مجموعه‌ها**: مسیرهای مرتبط را بر اساس سرور و حساب گروه می‌کنند؛ انتخاب صریح و نتیجهٔ تست‌های کامل‌شده حفظ می‌شود.
- **اتصال مستقیم**: لمس اول مسیر را انتخاب می‌کند و لمس دوم وصل می‌کند. پینگ یا اسکن اجباری پیش از اتصال نداریم.
- **تست واقعی HTTPS**: درخواست واقعی HTTPS با پاسخ ۲۰۴ از داخل تونل، همراه با بررسی گواهی، نسبت موفقیت و پراکندگی تأخیر. شکست یک اندازه‌گیری قبلی مانع اتصال دستی نیست.
- **پروتکل‌های متعدد**: SSH، تونل‌های DNS (DNSTT، NoizDNS، VayDNS، Slipstream و Soar)، NaiveProxy، SOCKS5، DoH، VLESS WebSocket، REALITY، XHTTP، Hysteria2، WireGuard و Tor.
- **اشتراک و اشتراک‌گذاری**: اشتراک HTTPS، لینک‌های `rovunet://`، QR، فایل و پشتیبان رمزدار.
- **چهار زبان**: فارسی، انگلیسی، روسی و چینی ساده.

فهرست کامل، محدودیت‌ها و هشدارها در [صفحهٔ اندروید](docs/android/README.fa.md) است. سرور و اعتبارنامهٔ معتبر لازم است؛ برنامه‌ها حساب رایگان نمی‌دهند و هیچ پروتکلی تضمین عبور از هر شبکه را ندارد.

## شروع سریع در اندروید

1. **دانلود**: APK کامل را از [صفحهٔ انتشار](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha17) بگیرید و نصب کنید. نسخه‌های عمومی آلفا۱۴ به بعد بدون حذف برنامه روی هم نصب می‌شوند.
2. **باز کردن**: زبان و تم را انتخاب کنید و مجوز VPN اندروید را بدهید.
3. **ورود کانفیگ**: اشتراک‌ها ← لینک اشتراک HTTPS را بچسبانید ← **ایمپورت**؛ یا **+ ← ورود** برای لینک، QR یا فایل.
4. **اتصال**: مجموعه را باز کنید، مسیر را یک بار لمس کنید تا انتخاب شود و دوباره لمس کنید تا وصل شود.

## راهنماها

| | |
|---|---|
| 📘 راهنمای جامع (PDF) | [English](docs/guides/Rovunet-App-Guide-EN.pdf) · [فارسی](docs/guides/Rovunet-App-Guide-FA.pdf) · [Русский](docs/guides/Rovunet-App-Guide-RU.pdf) · [简体中文](docs/guides/Rovunet-App-Guide-ZH.pdf) |
| [راهنمای کاربر](docs/USER_GUIDE.md) | نصب، ورود کانفیگ، اتصال، تست، Soar، اطلاعات ورود و عیب‌یابی |
| [صفحهٔ اندروید](docs/android/README.fa.md) | وضعیت انتشار، قابلیت‌ها، پروتکل‌ها و محدودیت‌ها |
| [پلتفرم‌ها](docs/PLATFORMS.md) · [وضعیت فعلی](docs/STATUS.md) · [ارتقا](docs/UPGRADE.md) | کانال‌های انتشار، وضعیت انتشار، امضا و مهاجرت |

قالب و ویرایش آلفا۱۵.۴ راهنماهای PDF حفظ شده است؛ رفتار تازهٔ وضعیت حساب، منو و بیکاری را در راهنمای کاربر فعلی و [یادداشت آلفا۱۷](docs/releases/alpha17.md) بخوانید.

## سورس و پشتیبانی

[ساخت](docs/BUILDING.md) · [انتشار](docs/RELEASING.md) · [امنیت](SECURITY.md).

برای گزارش مشکل، نسخهٔ برنامه و سیستم‌عامل، پروتکل، مراحل تکرار و گزارش امن محلی را بفرستید. لینک کانفیگ، رمز، کلید خصوصی و شناسهٔ دستگاه را عمومی نکنید. ارتباط: [گروه تلگرام Rovunet](https://t.me/RovunetVPN).

## حمایت اختیاری از توسعه

اگر مایل به کمک به پیشرفت رووُنت هستید، حمایت مالی کاملاً اختیاری است و استفاده از برنامه به آن وابسته نیست. **USDT فقط روی شبکهٔ Tron/TRC20:**

`TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

## مجوز و انتساب

- مجوز پروژه: **AGPL-3.0**؛ [مجوز](LICENSE) و [انتساب‌ها](NOTICE.md).
- مجوز هستهٔ Xray: **MPL-2.0**؛ نسخهٔ سورس و اعلان‌ها حفظ شده‌اند.
- پایهٔ اپ: [SlipNet نسخهٔ ۲٫۵٫۶ Full](https://github.com/anonvector/SlipNet)، ساختهٔ Anonvector. رووُنت پروژه‌ای مستقل و بدون وابستگی رسمی به SlipNet است.

</div>
