# ممیزی خانواده‌های پروتکل — preview.6

کد، cross-build، اجرای Linux و اجرای Windows چهار ادعای جدا هستند.
هیچ ردیف زیر ادعای اجرای این نسخه روی Windows یا شبکهٔ عمومی کاربر ندارد.

| خانواده | موتور و مجوز | ساخت/دریافت | آزمون عبور داده | محدودیت فعلی |
|---|---|---|---|---|
| VLESS WS معمولی | Xray 26.10.31، MPL-2.0 | Windows/Linux رسمی، ZIP و EXE با SHA256 | دو HTTPS در Linux، UUID غلط رد، پورت بسته | پروکسی TCP، TUN نیست |
| REALITY / Vision | همان Xray | همان | HTTPS محلی Linux و اعتبار غلط | شبکهٔ عمومی/Windows باقی |
| XHTTP | همان Xray | همان | packet-up با TLS معتبر در Linux | extra با allow-list دقیق حفظ می‌شود؛ downloadSettings/تغییر endpoint رد می‌شود |
| Hysteria2 | Xray hysteria v2 | مبدل C# و Xray رسمی | HTTPS با Salamander، اعتبار غلط رد | insecure، port hopping و گزینه‌های ناشناخته رد؛ UDP ورودی محلی فعال نیست |
| WireGuard استاندارد | Xray userspace | مبدل و همان موتور | HTTPS، کلید غلط رد | یک peer؛ noKernelTun؛ DNS/AllowedIPs حفظ؛ AmneziaWG یا تونل دستگاه نیست |
| SOCKS5 | Xray outbound | مبدل و موتور | در زنجیرهٔ helper/Naive استفاده و HTTPS آزموده شد | رمزنگاری مستقل SOCKS ادعا نمی‌شود |
| SSH | x/crypto/ssh، BSD-3-Clause؛ میزبان AGPL-3.0 | Go از source، Windows x64 و Linux | SSH password، TLS مقصد معتبر، unknown/changed key رد، قطع تمیز در Linux | TLS/WS/CONNECT ترکیب‌ها نوشته شده‌اند؛ آزمون مستقل کامل هر ترکیب هنوز باز است |
| DNSTT | fork پین‌شده AGPL-3.0، noizdns/mobile با MIT | source در deps/vendor؛ Go cross-build | HTTPS محلی Linux با کلید Noise واقعی | UDP و TCP مستقیم و SSH در Linux داده عبور دادند. مسیر کامل DoT/DoH روی Windows باز است |
| NoizDNS | pin 17ac5cf، MIT + DNSTT AGPL | همان | Noiz/stealth و HTTPS محلی Linux روی UDP و TCP | انتخاب resolver، transport، payload و RR حفظ؛ اسکن پیش‌شرط نیست |
| DNSTT/NoizDNS + SSH | همان‌ها + SSH | supervisor مشترک | HTTPS از هر دو زنجیره روی Linux | کلید SSH جدا از کلید DNS تأیید می‌شود |
| Soar / Soar+SSH | wrapper پروژه AGPL، getlantern/soar v0.2.4 Apache-2.0 | source و Go cross-build | HTTPS هر دو حالت، TCP-only با شمارندهٔ UDP=0 | discovery با فهرست app و Prism با دست‌دهی واقعی اضافه شده‌اند. آزمون Windows و SOCKS auth اضافی باز است |
| NaiveProxy / Naive+SSH | رسمی v154.0.8037.49-5، BSD/Chromium | Windows و Linux با دو هش | HTTPS از HTTP/2 و از زنجیرهٔ SSH در Linux، TLS معتبر | ACL فایل موقت و runtime Windows نیازمند آزمون واقعی؛ منبع باینری بازسازی نشده |
| VLESS WS اختصاصی | میزبان Go، AGPL؛ Gorilla BSD | source/cross-build | TLS+SNI split و micro، obfuscation/padding، HTTPS و UUID غلط در Linux | fake/disorder و MSS مثبت Windows آماده نیستند؛ fragmentهای دیگر کد دارند ولی شواهد جدا باز است |
| VayDNS / +SSH | client اصلی CC0-1.0، بدون wrapper موبایل | Go win-x64 از سورس | هر دو مسیر با سرور واقعی، HTTPS روی UDP و روی TCP در Linux | یک resolver؛ سقف payload/MTU سفارشی باز است |
| Slipstream / +SSH | Mygod slipstream-rust v0.1.1، Apache-2.0 و اجزای همراه | باینری رسمی Windows x64 با دو هش | HTTPS در هر دو مسیر با سرور مرجع راهنمای پنل؛ گواهی متفاوت، عبور را رد کرد؛ تکرار کامل preview.6 برای +SSH شکست خورد | گواهی عمومی هر دامنه و VC++ x64 لازم‌اند؛ در ۵۰۰۰ ms کند و وابسته به نسخهٔ سرور |
| DOH تنها / Tor | هنوز موتور پراکسی برای این dialect واردشده انتخاب نشده | هیچ | هیچ | ذخیره می‌شود؛ اتصال غیرفعال و صریح |

پین‌های دقیق Xray و Naive در engines/*pins.json هستند. NoizDNS:
17ac5cf4e45cd09fa6f4bde09362e334dc4b8b55؛ DNSTT:
51bdba3948d44b3175bc88ea40b2116bb9daff07. کد وابستگی‌های ساخته‌شده در transports/deps
و transports/vendor، نسخه‌ها در vendor/modules.txt و انتساب‌ها در licenses/Go است.
پین‌های تاریخی Android (موتور Slipstream ویندوز جداگانه در transports-pins.json ثبت شده): VayDNS 45f18f16af40b6ad666e8119be08701b21395ee2،
vaydns-mobile b7da42d82e204059eb4b240bb806f8ffd2ed909f،
Slipstream 60a5e38c7414b35e4c2fddf8d4d89a4341470c6a.
وجود CLI با هدف Windows اثبات پشتیبانی همهٔ خانواده‌ها نیست؛ CLI قدیمی اجرا نشده است.
