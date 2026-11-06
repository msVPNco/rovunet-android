# Rovunet 0.3.0-alpha14 — Full

Public version: **0.3.0-alpha14**, code **25**, package `app.veyra.client`.
Private validation: `0.3.0-alpha14-review02`, code 25, package `app.veyra.client.candidate`.

**Signing decision: accepted on 2026-11-06.** The owner confirmed no deployed users and authorized a new managed production identity before publication. The signed alpha14 Full APK uses the pinned certificate in [SIGNING-IDENTITY](../SIGNING-IDENTITY.md). It is a fresh installation relative to alpha11, not an in-place upgrade. Later direct APK updates must reuse the same key. Private/debug candidates and unsigned preparation artifacts are not the public release.

## Verification / بررسی / 验证 / Проверка

- Behavioural-test code: `852c6014af1d16d03deb7693007216c17e8369b3`; passing CI: `10c9b250a1fcc8e3af2f4cd55493a88189a71a8b`. Final production preparation changes signing pin/guard, CI and documentation only; Kotlin/native logic is unchanged. Final build/archive identities are recorded in SOURCE-MANIFEST.json and VERIFICATION.json.
- Local Gradle and real transport-fixture checks: **50 JUnit contracts**, zero failures/errors/skips (the same contracts run by two harnesses, not 100 distinct tests).
- Local API 30/x86_64 emulator gate: **29 tests**, zero failures/skips/missing tests; actual VPN/MTU data, SSH cancellation/trust, scanner lifecycle, credentials/migration and UI/RTL/history.
- Release R8/Lint and unsigned packaging passed. A locally validation-signed Release copy launched without a crash; it is not a publication artifact or production-signer test.
- Original local APK ZIP packaging was repaired without changing compiled payloads; all 33 native libraries are unchanged and all 21 ELF64 libraries retain 16 KB alignment. This is packaging correction, not a native rebuild.
- GitHub verification is recorded separately by run and commit in VERIFICATION.json. An earlier device job failed before app tests because the AVD home differed; setup was corrected. No failed/skipped job is counted as a pass.
- هویت تولیدی ثابت با تأیید مالک جایگزین امضای قدیمی شد؛ آزمون شبیه‌ساز تضمین شبکهٔ واقعی نیست.
- 固定生产签名已获所有者批准；模拟器不保证真实网络。
- Постоянная производственная подпись одобрена владельцем; эмулятор не гарантирует реальную сеть.

## فارسی

رووُنت یک کلاینت چندپروتکلی اندروید برای سرور شخصی است. کانفیگ‌های مرتبط را در مجموعه نگه می‌دارد، مسیر انتخاب‌شده را مستقیم وصل می‌کند و امکان مقایسهٔ جداگانهٔ HTTPS از داخل تونل واقعی را می‌دهد.

تغییرات این چرخه نسبت به انتشار عمومی آلفا۱۱:

- بازطراحی کارت مجموعه و مسیر: مجموعهٔ باز برجسته‌تر و پررنگ‌تر، زیرمجموعه باریک‌تر و روشن‌تر، تفکیک بهتر در تم روشن، تیره و AMOLED و نشان سبز تأخیر معتبر شبکهٔ فعلی.
- لمس اول مسیر را انتخاب و لمس مسیر انتخاب‌شده اتصال را آغاز می‌کند؛ باز/بسته کردن مجموعه، ویرایش، حذف و منو اتصال ناخواسته نمی‌سازند. انتخاب صریح و نتایج تکمیل‌شده پس از بازگشت حفظ می‌شوند.
- اتصال دستی پینگ یا اسکن اجباری ندارد. وضعیت اتصال زنده از نتیجهٔ تست قبلی جداست. لغو SSH سوکت در حال راه‌اندازی را می‌بندد؛ پاک‌سازی تلاش قبلی و بازیابی شبکه از تلاش تازه جدا شده‌اند.
- هماهنگی MTU اعمال‌شده میان رابط VPN و موتور، چرخهٔ عمر مستقل اجرای اسکن، مالکیت سرویس/WakeLock و محافظت در برابر توقف اعلان قدیمی اصلاح شده‌اند.
- پیش‌فرض Soar و Soar+SSH جدید هر دو UDP + TCP است؛ انتخاب صریح، کانفیگ موجود و مقدار واردشده حفظ می‌شوند. چهار حالت Soar برچسب متمایز و چیدمان مناسب فارسی دارند.
- رازهای ذخیره‌شده با Android Keystore رمز می‌شوند؛ رکورد ناخوانا برای بازیابی صریح جدا می‌شود و ذخیرهٔ ناقص آن را بی‌صدا پاک نمی‌کند. حذف بازبینی‌شده بدون رمزگشایی تکراری هر ردیف انجام می‌شود.
- هویت SSH در مسیر اسکن رعایت می‌شود، اعتماد TLS خودامضا از SPKI نسخه‌دار استفاده می‌کند و پراکسی خصوصی Xray پورت/اعتبارنامهٔ تصادفی دارد. کلید ناشناس پس‌زمینه بی‌صدا پذیرفته نمی‌شود.
- منابع تنظیمات و ویرایشگر، پیام‌های جدید و مستندات معرفی در فارسی، انگلیسی، چینی ساده و روسی یکپارچه شده‌اند. متن فنی و بعضی بخش‌های به‌ارث‌رسیدهٔ اسکنر/backend ممکن است انگلیسی بمانند.
- گزارش امن محلی نسخهٔ ۴ مرحله/کد، سیاست پیکربندی Soar و نمونهٔ شمارنده‌ها را بدون فیلد آدرس، حساب یا رمز ثبت می‌کند؛ ارسال خودکار ندارد. گزارشگر لاگ تفصیلی backend را با گزارش امن یکسان فرض نکنید.
- نسخهٔ آیندهٔ ناشناخته در import صریحاً رد می‌شود؛ نمونه‌های قدیمی و فیلدهای امنیتی با corpus نسخه‌دار بررسی می‌شوند. کد TUN کاتلینیِ بی‌مصرف حذف شده و آزمون‌های واحد در Gradle اجرا می‌شوند.
- در ساخت نهایی، خطای Release Lint مربوط به سه دامنهٔ loopback با ثبت صریح `includeSubdomains="false"` اصلاح شد؛ بررسی Lint خاموش نشده است.

خانواده‌های موجود شامل SSH و پوشش‌های آن، DNSTT/NoizDNS/VayDNS/Slipstream/Soar با ترکیب‌های پشتیبانی‌شدهٔ SSH، NaiveProxy، SOCKS5، DoH، VLESS WebSocket، REALITY، XHTTP، Hysteria2، WireGuard تک‌همتا و Tor/پل‌های پشتیبانی‌شده‌اند. دسترسی backend و معماری و تنظیم سرور تعیین‌کننده است؛ x86_64 باینری به‌ارث‌رسیدهٔ Naive/Tor ندارد. DoH به‌تنهایی تونل همهٔ ترافیک نیست و SOCKS5 ساده رمزنگاری انتقال ندارد.

بسته‌بندی Full، سورس کامل و سبک، راهنما و گزارش بررسی باید با هش و کامیت دقیق همراه شوند. نتیجهٔ هر آزمون در VERIFICATION.json دارایی‌های نهایی ثبت می‌شود؛ موفقیت کامپایل، شبیه‌ساز یا گزارش گوشیِ نسخهٔ قبلی تضمین همهٔ شبکه‌ها نیست. مسیر SOCKS سرور واقعی، تفاوت auto/on و مشاهدهٔ میدانی شمارنده‌ها هنوز بازند.

**ارتقا:** آلفا۱۱ عمومی و Rovunet Test امضای متفاوت دارند. قبل از حذف، اطلاعات قابل‌انتقال را واقعاً بازیابی‌پذیر کنید؛ پشتیبان گروهی رمزدار شامل کانفیگ قفل‌شده نیست. تغییر امضا ممکن است قید دستگاه را عوض کند. [راهنمای مهاجرت](../UPGRADE.md). فایل بدون امضا را به‌عنوان APK نهایی نصب یا توزیع نکنید.

## English

Rovunet combines several routes to a personal server in named collections: explicit direct connection, separate real-tunnel HTTPS/204 measurements and supported standard/native imports.

This cycle adds clearer raised collection/inset path surfaces and dark/AMOLED contrast, green current-network HTTPS badges, first-select/second-connect path controls, persistent explicit choices/results and independent nested actions. Manual connection remains free of a prerequisite ping or pool scan.

Startup cancellation, recovery ownership, effective TUN/native MTU and scanner-service lifecycle are repaired. New Soar and Soar+SSH profiles default to UDP + TCP while stored/imported choices remain intact. Stored credentials use Keystore encryption with explicit unreadable-row recovery; reviewed deletion avoids per-row decryption. SSH trust, versioned TLS SPKI, authenticated random private core endpoints, unknown-version rejection, Gradle unit tests and import corpus checks strengthen the existing paths.

Main/settings/editor resources and introductory documentation are coordinated across Persian, English, Simplified Chinese and Russian; inherited advanced scanner/backend text may remain English. Safe report v4 records typed stages/codes, requested/configured Soar policy and counter snapshots locally without endpoint/account/credential fields. It does not prove data passing; verbose backend logs need separate review.

Protocols include supported SSH wrappers, DNS families and SSH combinations, NaiveProxy, SOCKS5, DoH, VLESS WS, REALITY, XHTTP, Hysteria2, single-peer WireGuard and Full-package Tor bridges. Availability depends on server/backend/ABI; inherited Naive/Tor are absent on x86_64. DoH alone encrypts DNS, and plain SOCKS5 is not an encrypted transport.

A fixed production identity was authorized before deployment; see the signing record. The unsigned Release cannot be installed; private candidates do not update the public alpha11. Bulk encrypted backup excludes locked profiles and signing changes may affect device binding. See [migration](../UPGRADE.md), final VERIFICATION.json and checksums. Real-network SOCKS/Soar differences and counters remain open. No subscription refresh, automatic collection failover, throughput benchmark or store/desktop release is added.

## 简体中文

Rovunet 将自有服务器的多种路径归入集合，支持直接连接、独立真实隧道 HTTPS/204 测量和支持的标准/原生导入。

本轮改进集合/路径层级、深色与 AMOLED 对比度、当前网络绿色 HTTPS 标记及先选后连操作，保留明确选择与完成结果，编辑/菜单不连接，手动连接无需强制测试。修复启动取消、恢复归属、TUN/引擎有效 MTU 与扫描服务生命周期。新建 Soar/Soar+SSH 默认 UDP + TCP，已有/导入选择保留。

凭据存储采用 Keystore 加密并支持显式恢复不可读记录；删除避免逐条解密。SSH 信任、版本化 TLS SPKI、私有核心端点随机认证、未来未知版本拒绝、Gradle 测试和导入样本集得到加强。主界面/设置/编辑器资源与介绍文档四语言统一；部分继承高级文本仍可能英语。安全报告 v4 本地记录类型化事件、Soar 配置策略和计数，不自动上传，不含端点/账号/凭据字段；详细后端日志须单独审阅。

支持的协议包括 SSH、DNS 家族及 SSH 组合、NaiveProxy、SOCKS5、DoH、VLESS WS、REALITY、XHTTP、Hysteria2、单对端 WireGuard 与 Full Tor 网桥，取决于服务器/后端/架构。x86_64 缺少继承 Naive/Tor；DoH 单独只加密 DNS，普通 SOCKS5 不加密传输。

所有者已批准固定生产签名，相对旧 alpha11 需要全新安装。未签名 Release 不能安装，私有包不能原位升级公开 alpha11。密码批量备份排除锁定配置，签名变化可能影响设备绑定。以最终验证与哈希记录为准，真实 SOCKS/Soar 差异与计数仍需实测。没有新增定期订阅刷新、自动路径切换、吞吐量测试、商店或桌面版本。

## Русский

Rovunet объединяет пути к собственному серверу в коллекции, предлагает прямое подключение, отдельное HTTPS/204-измерение внутри туннеля и поддерживаемый стандартный/родной импорт.

Улучшены иерархия коллекций/путей, тёмная/AMOLED-темы, зелёные актуальные измерения и выбор первым/подключение повторным нажатием. Сохраняются явный выбор и результаты, вложенные действия не подключают, предварительная проверка не обязательна. Исправлены отмена запуска, принадлежность восстановления, эффективный MTU TUN/движка и жизненный цикл сервиса сканирования. Новые Soar/Soar+SSH используют UDP + TCP, существующий/импортированный выбор сохраняется.

Секреты шифруются Keystore с явным восстановлением нечитаемых записей; удаление не расшифровывает каждую строку. Усилены SSH-доверие, версионированный TLS SPKI, случайная аутентификация приватных core-эндпоинтов, отклонение будущих форматов, Gradle-тесты и корпус импорта. Основные ресурсы/редактор/настройки и вводная документация согласованы на четырёх языках; часть унаследованных текстов остаётся английской. Безопасный отчёт v4 локально фиксирует типизированные события, режим Soar и счётчики без полей адресов/аккаунтов/секретов; подробные backend-логи проверяются отдельно.

Протоколы включают SSH, DNS-семейства и SSH-комбинации, NaiveProxy, SOCKS5, DoH, VLESS WS, REALITY, XHTTP, Hysteria2, WireGuard с одним peer и мосты Tor Full. Возможности зависят от сервера/движка/ABI, для x86_64 нет унаследованных Naive/Tor. DoH отдельно шифрует DNS, обычный SOCKS5 не шифрует транспорт.

Постоянная производственная подпись одобрена; относительно старой alpha11 нужна новая установка; неподписанный Release не устанавливается, приватный пакет не обновляет публичную alpha11. Массовая копия с паролем исключает заблокированные профили, смена подписи может изменить привязку. Ориентируйтесь на финальные проверки/хеши; реальные SOCKS/Soar и счётчики ещё требуют данных. Нет обновления подписок, автопереключения пути, теста пропускной способности, магазина или настольного выпуска.

## Community / حمایت / 支持 / Поддержка

[Telegram — RovunetVPN](https://t.me/RovunetVPN) · [Panel](https://github.com/msVPNco/rovunet-panel).

Optional help with development / کمک اختیاری به پیشرفت پروژه / 自愿支持开发 / Добровольная поддержка:
**USDT, Tron/TRC20 only** — `TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`.

AGPL-3.0; [LICENSE](../../LICENSE), [NOTICE](../../NOTICE.md). Based on SlipNet 2.5.6 Full by Anonvector, independently maintained and not affiliated. Xray-core: MPL-2.0.
