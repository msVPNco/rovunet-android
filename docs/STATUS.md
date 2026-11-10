# Rovunet — current release / وضعیت فعلی

## English

Alpha18.1 Full / code 39 completes subscription updates on the collection card, optional names, provider-reported usage/expiry, supported `cs`/`fm`, Shadowsocks 2022, Hy2 port hopping and single-peer AmneziaWG import. It retains the calm Soft Pop themes, visible-only latency monitoring, bounded smart recovery, isolated Slipstream resolver workers and native stop improvements. Patterniha is removed from the built-in catalog; existing imports are preserved.

**Subscriptions:** choose Update on the collection card to update in place; progress blocks repeated clicks and the outcome stays on Home. Existing-profile replacement still requires a disconnected VPN. Names are optional: explicit/saved name, safe provider title, then hostname. `subscription-userinfo` reports upload + download, volume and expiry from the same response; missing fields stay unknown. Provider snapshots show their age and are not independent metering. No additional periodic full-feed downloads are introduced. Panel status retains its separate contract.

**Imports:** supported VLESS/VMess/Trojan `cs` and `fm` are preserved with verified TLS; unsupported masks/scripts/settings are rejected explicitly. Shadowsocks 2022 validates key lengths. Hy2 supports bounded `mport` lists and `hop-interval`. Standard WireGuard sharing URIs and single-peer AmneziaWG `.conf`/supported URI parameters are accepted. AWG runs in a separate authenticated userspace engine; one address per IP family, no scripts, multi-peer or ranged keepalive. Arbitrary core/Mihomo YAML is not executed.

Automated local/emulator checks do not establish physical-phone battery savings, overnight/Doze stability, carrier reachability or repair of the reported panel WireGuard/Hy2 endpoints. No new physical-phone acceptance is claimed. AnyTLS, TUIC and Mieru remain future work. No new Lite or desktop binary is published.

## فارسی

آلفا۱۸.۱ Full با کد ۳۹، آپدیت اشتراک روی همان کارت، نام اختیاری، نمایش مصرف و انقضای اعلام‌شدهٔ ارائه‌دهنده، گزینه‌های سازگار `cs`/`fm`، Shadowsocks 2022، جابه‌جایی پورت Hy2 و ورود AmneziaWG تک‌همتا را اضافه می‌کند. تم‌های آرام Soft Pop، سنجش تأخیر هنگام نمایش اپ، بازیابی هوشمند محدود، اسکنرهای مستقل اسلیپ‌استریم و اصلاح توقف بومی حفظ شده‌اند. پترنیها از فهرست داخلی حذف شده؛ ورودی‌های قبلی کاربر حفظ می‌شوند.

**اشتراک‌ها:** آپدیت روی کارت، همان‌جا انجام می‌شود؛ پیشرفت جلوی کلیک تکراری را می‌گیرد و نتیجه در صفحهٔ اصلی می‌ماند. جایگزینی پروفایل موجود همچنان به قطع VPN نیاز دارد. نام اختیاری است: نام واردشده یا ذخیره‌شده، سپس عنوان امن ارائه‌دهنده و در نهایت نام میزبان. هدر `subscription-userinfo` مجموع ارسال و دریافت، حجم و انقضا را از همان پاسخ می‌خواند؛ دادهٔ غایب «نامشخص» می‌ماند. سن داده و اعلامی‌بودن آن مشخص است؛ این شمارش مستقل اپ نیست. دانلود دوره‌ای تازه‌ای برای کل اشتراک اضافه نشده است. قرارداد وضعیت پنل جدا حفظ شده است.

**ورودی‌ها:** گزینه‌های سازگار `cs` و `fm` در VLESS/VMess/Trojan با TLS معتبر حفظ می‌شوند؛ ماسک، اسکریپت یا تنظیم ناسازگار صریحاً رد می‌شود. طول کلید Shadowsocks 2022 بررسی می‌شود. Hy2 از فهرست محدود `mport` و `hop-interval` پشتیبانی می‌کند. لینک‌های سازگار WireGuard و فایل `.conf` تک‌همتای AmneziaWG با پارامترهای سازگار وارد می‌شوند. AWG موتور فضای کاربر مستقل با احراز هویت دارد؛ یک آدرس برای هر خانوادهٔ IP؛ اسکریپت، چند همتا و keepalive بازه‌ای پشتیبانی نمی‌شوند. YAML دلخواه هسته/Mihomo اجرا نمی‌شود.

تست‌های محلی و شبیه‌ساز، صرفه‌جویی باتری گوشی، پایداری شبانه/Doze، دسترسی اپراتور یا رفع مشکل WireGuard/Hy2 پنل گزارش‌شده را اثبات نمی‌کنند. تأیید تازهٔ گوشی واقعی ادعا نمی‌شود. AnyTLS، TUIC و Mieru برای مراحل بعد می‌مانند. بستهٔ Lite یا دسکتاپ تازه‌ای منتشر نمی‌شود.

## 简体中文

Alpha18.1 Full（版本代码 39）支持直接在集合卡片更新订阅、可选名称、提供方报告的流量和到期时间、兼容的 `cs`/`fm`、Shadowsocks 2022、Hy2 端口跳跃及单对端 AmneziaWG 导入。保留柔和 Soft Pop 主题、仅界面可见时的延迟测量、有次数限制的智能恢复、隔离的 Slipstream 检测及原生停止改进。内置列表移除了 Patterniha，既有用户导入保持不变。

**订阅：**在集合卡片选择更新即可原地更新；进度会阻止重复点击，结果留在首页。替换现有配置仍需断开 VPN。名称可留空：依次使用显式/已存名称、安全的提供方标题、主机名。同一响应的 `subscription-userinfo` 提供双向总流量、上限和到期时间；缺失字段保持未知，显示快照时间及来源，不是独立审计计量。没有新增定期下载整个订阅的任务。

**导入：**保留经验证 TLS 的兼容 VLESS/VMess/Trojan `cs`/`fm`；不支持的掩码、脚本和设置会明确拒绝。检查 Shadowsocks 2022 密钥长度；Hy2 支持有界 `mport` 和 `hop-interval`。接受兼容 WireGuard URI 与单对端 AmneziaWG `.conf`/URI 参数。AWG 使用独立、经认证的用户空间进程；每个 IP 地址族一个地址，不支持脚本、多对端或范围 keepalive。不执行任意内核/Mihomo YAML。

本地和模拟器测试不证明实体手机省电、整夜/Doze 稳定、运营商网络可达或已修复面板 WireGuard/Hy2 节点。未声称有新的实体手机验收。AnyTLS、TUIC 和 Mieru 留待后续；没有新的 Lite 或桌面安装包。

## Русский

Alpha18.1 Full, код 39: обновление подписки прямо на карточке, необязательное имя, расход/срок по данным провайдера, поддерживаемые `cs`/`fm`, Shadowsocks 2022, смена портов Hy2 и импорт AmneziaWG с одним peer. Сохранены спокойные темы Soft Pop, измерение задержки только при видимом приложении, ограниченное умное восстановление, изолированные проверки Slipstream и исправления нативной остановки. Patterniha удалён из встроенного каталога; прежние импорты сохраняются.

**Подписки:** Update на карточке обновляет её без перехода; повторные нажатия блокируются, результат остаётся на главной. Для замены профилей VPN должен быть отключён. Имя необязательно: введённое/сохранённое, затем безопасный заголовок провайдера, затем hostname. `subscription-userinfo` из того же ответа содержит суммарный расход, лимит и срок; отсутствующие поля неизвестны. Показываются давность и происхождение данных; это не независимый учёт. Нового периодического скачивания всей подписки нет.

**Импорт:** совместимые `cs`/`fm` VLESS/VMess/Trojan сохраняются при проверяемом TLS; неподдерживаемые настройки/скрипты отклоняются явно. Для Shadowsocks 2022 проверяется длина ключа; Hy2 поддерживает ограниченные `mport` и `hop-interval`. Поддержаны совместимые WireGuard URI и AmneziaWG `.conf`/параметры URI с одним peer. AWG использует отдельный аутентифицированный userspace-процесс; по одному адресу семейства IP, без скриптов, нескольких peer и диапазона keepalive. Произвольный YAML не выполняется.

Локальные тесты и эмулятор не доказывают экономию батареи телефона, ночную/Doze-стабильность, доступность оператора или исправление сообщённых узлов WireGuard/Hy2 панели. Новая приёмка на физическом телефоне не заявляется. AnyTLS, TUIC и Mieru остаются на будущее. Новых Lite/desktop-файлов нет.

[CI](https://github.com/msVPNco/rovunet-app/actions/runs/42905223390): 144 JVM tests; 18 real local encrypted TCP/UDP modes; AWG 1/2/3.1 TCP/UDP and shutdown fixtures. Android device/minified gates are recorded in [release provenance](releases/alpha18-1-provenance.json).
