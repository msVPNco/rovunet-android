# Rovunet 0.3.0-alpha19 Full · code 40

## فارسی

- واردسازی دوبارهٔ لینک اشتراکِ ذخیره‌شده، همان منبع را به‌روزرسانی می‌کند؛ دیگر برای این کار نیازی به حذف اشتراک نیست. شناسهٔ پروفایل‌ها، تغییرات محلی و تنظیم آپدیت خودکار حفظ می‌شوند. خطای دانلود یا ورودی ناسازگار اطلاعات قبلی را پاک نمی‌کند؛ خطاهای شناخته‌شده دقیق‌تر نمایش داده می‌شوند.
- آزمون کامل کارت ابتدا مسیرهای مستقیم را می‌سنجد. پس از موفقیت آن‌ها، درخواست تست DNS نمایش داده نمی‌شود. آزمون DNS فقط پس از شکست مسیرهای مستقیم و با تأیید کاربر است؛ نتیجهٔ قبلی Soar و سایر مسیرهای کنارگذاشته‌شده، تاریخی مشخص می‌شود.
- نوشته‌های بلندِ کارت و پروفایل در همان کادر ثابت حرکت می‌کنند؛ تک‌خطی می‌مانند و سه‌نقطه نمی‌شوند. متن کوتاه ثابت است؛ حرکت در پس‌زمینه و بیرون صفحه متوقف می‌شود.
- فرم دستی SS2022، پرش پورت Hy2، گزینه‌های سازگار cs/fm، ساخت AmneziaWG از صفر و تنظیمات VLESS تکمیل شده‌اند. لمس لینک WireGuard به واردسازی هدایت می‌شود. اطلاعات حسابِ خروجی `.windows.json` نیز از وضعیت مشترک پنل خوانده می‌شود.
- AnyTLS، TUIC v5 و Mieru با لینک سادهٔ تک‌پورت `mierus://`، فرم دستی و موتور مستقل اضافه شده‌اند. تنظیمات مؤثر ناسازگار صریحاً رد می‌شوند. برای AnyTLS و TUIC اعتبارسنجی TLS الزامی است؛ YAML دلخواه یا اسکریپت واردشده اجرا نمی‌شود. لینک باینری `mieru://`، چند اتصال/بازهٔ پورت Mieru و همهٔ گویش‌های سایر کلاینت‌ها پشتیبانی نمی‌شوند.

تست‌های محلی و شبیه‌ساز، تأیید شبکهٔ واقعی گوشی، مصرف باتری یا رفع WireGuard/Hy2 پنل نیستند. این موارد همچنان نیازمند بررسی روی دستگاه و شبکهٔ مربوط‌اند. این انتشار فقط اندروید Full است.

## English

Alpha19 refreshes an already saved subscription through Import without deleting its source, preserving profile identity, local edits and automatic-update preference. Failed downloads and incompatible feeds retain existing data; recognized errors have specific messages. Both panel feed variants can resolve shared account status.

Collection tests keep resolver transports deferred and finish after direct success. DNS fallback requires direct failure and consent; previous excluded results are historical. Overflowing card/profile text scrolls inside a fixed single line; short text stays still and background/offscreen motion stops.

Manual SS2022, Hy2 hopping, supported cs/fm, fresh AWG and VLESS controls are exposed; external WireGuard links open Import. AnyTLS, TUIC v5 and single-port Mieru `mierus://` have import, forms and constrained native execution. Unsupported effective settings are explicitly rejected. AnyTLS/TUIC require verified TLS; arbitrary YAML/scripts, binary `mieru://` and multi-binding Mieru dialects are not executed or silently converted.

Local/emulator tests do not establish physical-phone/carrier success, battery savings or a fix for the reported panel WireGuard/Hy2 endpoints. Android Full only.

## Русский

Alpha19 обновляет уже сохранённую подписку через «Импорт», сохраняя источник, идентификаторы профилей, локальные изменения и автообновление. Ошибки не удаляют данные; известные причины отображаются точнее. Оба варианта ленты панели используют общий статус аккаунта.

Проверка коллекции сначала использует прямые пути; после успеха предложение DNS отсутствует. DNS запускается только после неудачи прямых путей и согласия. Старые результаты отложенных путей помечаются историческими. Длинный текст движется внутри одной фиксированной строки, короткий остаётся неподвижным; в фоне и вне экрана движение останавливается.

Дополнены ручные поля SS2022, Hy2 hopping, cs/fm, AWG и VLESS; WireGuard-ссылки открывают импорт. Добавлены AnyTLS, TUIC v5 и однопортовый Mieru `mierus://`: импорт, формы и отдельный ограниченный процесс. Неподдерживаемые параметры отклоняются явно. Для AnyTLS/TUIC требуется проверенный TLS; произвольный YAML/скрипты, бинарный `mieru://` и несколько привязок Mieru не поддерживаются.

Локальные тесты и эмулятор не доказывают работу на сети телефона, экономию батареи или исправление WireGuard/Hy2 панели. Только Android Full.

## 简体中文

Alpha19 可以通过“导入”刷新已有订阅，无需先删除，保留来源、配置标识、本地修改和自动更新偏好。失败或不兼容的订阅不会删除原有数据；已知错误显示具体原因。面板两种订阅格式均可读取共享账户状态。

集合测试先检查直接连接；成功后不再提示 DNS。直接连接失败且用户同意后才检查 DNS，已延后的旧结果标记为历史。过长的卡片及配置文字在固定单行框内滚动，短文字保持静止，后台或屏幕外停止滚动。

补充 SS2022、Hy2 端口跳跃、兼容 cs/fm、全新 AWG 及 VLESS 手动设置；WireGuard 链接可打开导入。添加 AnyTLS、TUIC v5、单端口 Mieru `mierus://` 导入、手动表单及受限原生进程。明确拒绝不支持的有效参数。AnyTLS/TUIC 必须验证 TLS；不执行任意 YAML/脚本，不支持二进制 `mieru://` 或多端口绑定。

本地与模拟器测试不证明实体手机运营商连接、省电或已修复面板 WireGuard/Hy2。仅发布 Android Full。

## Upgrade and evidence

Install the signed Full APK over the existing production installation without uninstalling or clearing data. Package `app.veyra.client` and the production signing certificate remain unchanged; versionCode increases from 39 to 40. The final asset checksums, exact source, CI and native rebuild scope are recorded in the attached verification and source manifests. No new physical-device acceptance is claimed.

[Verified source and signing evidence](alpha19-provenance.json): 148 JVM, 151 Gradle unit, 75 Debug emulator and two minified Release JNI cases; 18 existing encrypted transport modes, AWG and the new auxiliary encrypted TCP/UDP fixtures. Phone/carrier and battery measurements remain separate.
