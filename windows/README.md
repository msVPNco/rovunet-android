<div align="center">

# Rovunet · Windows

**0.1.0 Beta · Windows x64**

[فارسی](#فارسی) · [English](#english) · [简体中文](#简体中文) · [Русский](#русский)

</div>

## فارسی

نسخهٔ ویندوز در مخزن مرکزی [rovunet-app](https://github.com/msVPNco/rovunet-app).
[دریافت0.1.0 Beta](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.0-beta).
رابط چهارزبانهٔ روشن/تیره/AMOLED،نمای باریک/گسترده،کارت‌های مناسب لمس،اشتراک با حجم و انقضا،ویرایش پیکربندی و جست‌وجوی چندمرحله‌ای ریزالور دارد. پراکسی محلی HTTP/SOCKS5 و پراکسی اختیاری Windows ارائه می‌شود؛ TUN سراسری ندارد.

هدف Windows10 22H2/build19045 و Windows11 x64 است؛ نصب‌کننده امضای Windows ندارد. مالک اتصال و تبادل دادهٔ مسیرهای آزمایش‌شده را به‌جز Hysteria2/WireGuard تأیید کرده؛ این دو محدودیت شناخته‌شده‌اند. دریافت منابع عمومی گاهی ناموفق و بعضی تنظیمات سفارشی ناسازگارند. TLS/SSH و حفاظت حفظ می‌شوند. اتصال دستی به تست وابسته نیست.
[راهنما](docs/USER-GUIDE-FA.txt) · [وضعیت](docs/STATUS.md) · [آزمون‌ها](docs/TESTING.md) · [ساخت](docs/BUILDING.md)

## English

Windows client in the central Rovunet repository. [Download0.1.0 Beta](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.0-beta).
WPF/.NET10, four languages, compact/wide layouts, touch-size cards, subscriptions/account quotas, profile editors and staged DNS scanning. Separate unprivileged agent, local HTTP/SOCKS5 and optional Windows system proxy; no device-wide TUN or forced per-process routing.

Target: Windows10 22H2/build19045 and Windows11 x64. Unsigned installer. The owner reports working traffic on tested paths except Hysteria2/WireGuard. Public downloads may fail intermittently; some custom options remain unsupported. TLS/SSH and protected restrictions stay enabled. Manual connect does not require a successful probe. See status/testing/build documentation for exact scope.

## 简体中文

Rovunet中央仓库中的Windows客户端。[下载0.1.0 Beta](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.0-beta)。
支持四种语言、窄窗与宽窗、触控按钮、订阅流量与到期信息、配置编辑和分阶段DNS扫描。独立普通权限代理、本地HTTP/SOCKS5及可选系统代理，尚无全设备TUN。

目标Windows10 22H2/build19045与Windows11 x64；安装程序未签名。所有者报告除Hysteria2/WireGuard外，测试连接均可传输数据；不保证所有服务器可用。公共源下载可能间歇失败，部分自定义参数不受支持。保留TLS/SSH验证和受保护配置限制；手动连接不依赖测试成功。

## Русский

Клиент Windows в центральном репозитории Rovunet. [Скачать0.1.0 Beta](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.0-beta).
Четыре языка, узкий/широкий интерфейс, сенсорные кнопки, подписки, объём/срок и поэтапный поиск DNS. Отдельный непривилегированный агент, локальные HTTP/SOCKS5 и системный прокси; общего TUN нет.

Цель: Windows10 22H2/build19045 и Windows11 x64; установщик без подписи. Владелец сообщил об успешном трафике на проверенных путях, кроме Hysteria2/WireGuard. Загрузка публичных источников иногда не удаётся; некоторые параметры не поддерживаются. ПроверкиTLS/SSH и защита профилей сохраняются; ручное подключение не зависит от теста.

---

[Architecture](docs/ARCHITECTURE.md) · [Build](docs/BUILDING.md) · [Backlog](docs/BACKLOG.md) · [License](LICENSE) · [Attribution](NOTICE.md)