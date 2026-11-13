<div align="center">

# Rovunet · Windows

**0.1.2 Beta · Windows x64 · Pre-release**

[Download installer](https://github.com/msVPNco/rovunet-app/releases/download/windows-v0.1.2-beta/Rovunet-Windows-0.1.2-Beta-x64-Setup.exe) · [Release and checksums](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.2-beta) · [Exact source](https://github.com/msVPNco/rovunet-app/tree/windows-v0.1.2-beta/windows)

</div>

## What changed

- Compact closed collections and fixed 66-DIP profile cards, with 44-DIP action targets and lighter edges/shadows. Long text moves inside its bounds; short text stays still.
- At most one collection is open. Opening another closes the previous one; closing it allows all closed. Menu/arrow toggles, search, page return, language/theme changes and restart share the same saved state. Save failure rolls back the entire expansion change.
- One click selects only. Successful connection updates the persistent recent order; selection alone does not move profiles or collections.
- The independent Panel feed1/status2 adapter preserves identity, period/revision/confirmed high-water and shared aliases. Errors keep previous data labelled stale/unknown; unknown usage is not invented as zero/unlimited. Closed summaries retain cached/pending information. No automatic migration Apply or endpoint trust transfer.
- Earlier local consumer features, editors, supported resolver families, source handling, themes and bounded native engines are included. Compatibility depends on the protocol/dialect/peer; this is not a claim that every remote endpoint works.

## Install and practical tests

Download **Rovunet-Windows-0.1.2-Beta-x64-Setup.exe** from the release above. Preserve existing profiles and private data; do not uninstall/reset data merely to upgrade. Publication does not automatically replace the owner's installation.

The installer has **no trusted Authenticode signature**. Compare SHA-256 with the attached SHA256SUMS.txt; a checksum does not replace publisher signing.

Installer SHA-256: `21eb870fbb14db2928e726ad3565ffc43440bfa3a6ba8c03359012ec75afac5f`.

[Practical test/recovery guide in Persian](https://github.com/msVPNco/rovunet-app/releases/download/windows-v0.1.2-beta/QUICK-TEST-FA.txt) · [Four-language release notes](https://github.com/msVPNco/rovunet-app/releases/download/windows-v0.1.2-beta/RELEASE-NOTES.md) · [Build/source provenance](https://github.com/msVPNco/rovunet-app/releases/download/windows-v0.1.2-beta/WINDOWS-PROVENANCE.json)

## فارسی

نسخهٔ **0.1.2 Beta** انتشار آزمایشی ویندوز x64 پس از0.1.1 Beta است. کارت‌ها جمع‌وجورتر شده‌اند؛ حداکثر یک مجموعه باز می‌ماند و بستن آن اجازه می‌دهد همه بسته باشند. تک‌کلیک فقط انتخاب می‌کند؛ فقط اتصال موفق ترتیب اخیر را تغییر می‌دهد. نامعلوم، قدیمی، نامحدود و مقدار در انتظار از هم جدا می‌مانند. ارتقا نباید اشتراک‌ها، انتخاب، سنجاق‌ها، ویرایش‌های محلی یا اطلاعات معتبر حساب را پاک کند.

[دانلود مستقیم نصب‌کننده](https://github.com/msVPNco/rovunet-app/releases/download/windows-v0.1.2-beta/Rovunet-Windows-0.1.2-Beta-x64-Setup.exe) · [صفحهٔ ریلیز](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.2-beta) · [راهنمای آزمون و نگهداری داده](https://github.com/msVPNco/rovunet-app/releases/download/windows-v0.1.2-beta/QUICK-TEST-FA.txt)

## Appearance and verification

Actual native-WPF renders with synthetic profiles:

<img src="https://github.com/msVPNco/rovunet-app/releases/download/windows-v0.1.2-beta/Windows-0.1.2-Beta-compact.png" width="300" alt="Compact Windows view"/>
<img src="https://github.com/msVPNco/rovunet-app/releases/download/windows-v0.1.2-beta/Windows-0.1.2-Beta-wide.png" width="640" alt="Expanded Windows view"/>

[Short native-rendered demonstration](https://github.com/msVPNco/rovunet-app/releases/download/windows-v0.1.2-beta/Windows-0.1.2-Beta-demo-synthetic.mp4).

110 focused WPF/private-store checks and 10 connection-order checks passed on the final payload. Four languages, three themes, 480/1120-DIP widths; host DPI120(125%), exported client renders96DPI. These are synthetic/UI/store checks, not live Panel/network, physical-touch or mixed-monitor acceptance. Historical contract results retain their own version/source identity.

## Scope and known limits

This is the **proxy consumer**, not an operational native laboratory/service release. TUN/kill-switch/boot/WFP, native pilot18/tools4, service-specific DPAPI, independent AWG/SS2022 peers/cold payload and trusted-publisher/live-updater acceptance remain separate. Ordinary WireGuard is deferred; later owner-reported Hy2 success is retained without universal connectivity claims. Real migration Apply is absent. Current Panel output uses canonical integers; schema-permitted1.0/1e0 forms remain a nonblocking P2.

The release is **Pre-release**, not repository-wide Latest. Old tags/assets and other platforms remain unchanged. Exact consumer source/tag commit: `50402d653ed6e91934e80a8e4f4cbcdb3081a154`; the Windows subtree matches reviewed local source. The release includes explicit Windows source and pinned-native corresponding-source offers. No private profiles, subscription tokens, DPAPI stores, managerial archives or signing keys are public assets.

AGPL-3.0 · Based on SlipNet 2.5.6 Full by Anonvector; not affiliated. Native components retain their own licences and source offers.

---

<details>
<summary>Historical 0.1.1 Beta documentation — previous release, not current capability claims</summary>

<div align="center">

# Rovunet · Windows

**0.1.1 Beta · Windows x64**

[Local UI changes / تغییرات این پیش‌نمایش](docs/UI-0.1.1-preview.1.md). This Windows prerelease includes the approved UI, complete Default resolver inventory and streamlined installer.

[فارسی](#فارسی) · [English](#english) · [简体中文](#简体中文) · [Русский](#русский)

</div>

## فارسی

نسخهٔ ویندوز در مخزن مرکزی [rovunet-app](https://github.com/msVPNco/rovunet-app).
[دریافت 0.1.1 Beta](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.1-beta).
رابط چهارزبانهٔ روشن/تیره/AMOLED،نمای باریک/گسترده،کارت‌های مناسب لمس،اشتراک با حجم و انقضا،ویرایش پیکربندی و جست‌وجوی چندمرحله‌ای ریزالور دارد. پراکسی محلی HTTP/SOCKS5 و پراکسی اختیاری Windows ارائه می‌شود؛ TUN سراسری ندارد.

هدف Windows10 22H2/build19045 و Windows11 x64 است؛ نصب‌کننده امضای Windows ندارد. مالک اتصال و تبادل دادهٔ مسیرهای آزمایش‌شده را به‌جز Hysteria2/WireGuard تأیید کرده؛ این دو محدودیت شناخته‌شده‌اند. دریافت منابع عمومی گاهی ناموفق و بعضی تنظیمات سفارشی ناسازگارند. TLS/SSH و حفاظت حفظ می‌شوند. اتصال دستی به تست وابسته نیست.
[راهنما](docs/USER-GUIDE-FA.txt) · [وضعیت](docs/STATUS.md) · [آزمون‌ها](docs/TESTING.md) · [ساخت](docs/BUILDING.md)

## English

Windows client in the central Rovunet repository. [Download 0.1.1 Beta](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.1-beta).
WPF/.NET10, four languages, compact/wide layouts, touch-size cards, subscriptions/account quotas, profile editors and staged DNS scanning. Separate unprivileged agent, local HTTP/SOCKS5 and optional Windows system proxy; no device-wide TUN or forced per-process routing.

Target: Windows10 22H2/build19045 and Windows11 x64. Unsigned installer. The owner reports working traffic on tested paths except Hysteria2/WireGuard. Public downloads may fail intermittently; some custom options remain unsupported. TLS/SSH and protected restrictions stay enabled. Manual connect does not require a successful probe. See status/testing/build documentation for exact scope.

## 简体中文

Rovunet中央仓库中的Windows客户端。[下载0.1.1 Beta](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.1-beta)。
支持四种语言、窄窗与宽窗、触控按钮、订阅流量与到期信息、配置编辑和分阶段DNS扫描。独立普通权限代理、本地HTTP/SOCKS5及可选系统代理，尚无全设备TUN。

目标Windows10 22H2/build19045与Windows11 x64；安装程序未签名。所有者报告除Hysteria2/WireGuard外，测试连接均可传输数据；不保证所有服务器可用。公共源下载可能间歇失败，部分自定义参数不受支持。保留TLS/SSH验证和受保护配置限制；手动连接不依赖测试成功。

## Русский

Клиент Windows в центральном репозитории Rovunet. [Скачать0.1.1 Beta](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.1-beta).
Четыре языка, узкий/широкий интерфейс, сенсорные кнопки, подписки, объём/срок и поэтапный поиск DNS. Отдельный непривилегированный агент, локальные HTTP/SOCKS5 и системный прокси; общего TUN нет.

Цель: Windows10 22H2/build19045 и Windows11 x64; установщик без подписи. Владелец сообщил об успешном трафике на проверенных путях, кроме Hysteria2/WireGuard. Загрузка публичных источников иногда не удаётся; некоторые параметры не поддерживаются. ПроверкиTLS/SSH и защита профилей сохраняются; ручное подключение не зависит от теста.

---

[Architecture](docs/ARCHITECTURE.md) · [Build](docs/BUILDING.md) · [Backlog](docs/BACKLOG.md) · [License](LICENSE) · [Attribution](NOTICE.md)


</details>
