<p align="center"><img src="assets/rovunet-logo.png" alt="Rovunet" width="140"></p>

<h1 align="center">Rovunet</h1>

<p align="center"><b>多条通往你自己服务器的路径，覆盖每一台设备</b><br>DNS 隧道、SSH、HTTPS 代理和 Xray 配置归入命名配置组，支持直接连接和真实 HTTPS 测试。</p>

<p align="center">
  <a href="https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha16.1"><img alt="Android release" src="https://img.shields.io/github/v/release/msVPNco/rovunet-app?sort=date&display_name=tag&filter=v*&label=android&color=0E7C86"></a>
  <img alt="Platforms" src="https://img.shields.io/badge/platforms-Android%20%C2%B7%20Windows%20%C2%B7%20Linux%20%C2%B7%20macOS-14304D">
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0-6D4AFF"></a>
  <a href="https://t.me/RovunetVPN"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-RovunetVPN-2F6FEB?logo=telegram&logoColor=white"></a>
</p>

<p align="center"><a href="README.md">English</a> · <a href="README.fa.md">فارسی</a> · <a href="README.ru.md">Русский</a> · <b>简体中文</b></p>

<p align="center">📥 <a href="#apps">下载</a> · 📘 <a href="docs/guides/Rovunet-App-Guide-ZH.pdf">完整指南（PDF）</a> · <a href="docs/USER_GUIDE.zh-CN.md">用户指南</a> · <a href="https://github.com/msVPNco/rovunet-panel">服务器面板</a></p>

---

**Rovunet 是一组 VPN 客户端，用多条不同路径连接你自己的服务器。** 它把 DNS 隧道、SSH、HTTPS 代理和基于 Xray 的配置整合到同一界面。相关路径按命名配置组放在一起：可直接连接选中的路径，也可单独测量经过真实隧道的 HTTPS。所有客户端都在本仓库；对应的服务器端是 [Rovunet Panel](https://github.com/msVPNco/rovunet-panel)。

<a id="apps"></a>

## 应用

| 平台 | 状态 | 下载与详情 |
| --- | --- | --- |
| **Android** | 公开版本：**0.3.0-alpha16.1 Full，代码 33** | [下载](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha16.1) · [Android 页面](docs/android/README.zh-CN.md) |
| **Windows** | 开发中；尚无经验证的公开版本 | 发布将使用标签 `windows-v<version>` |
| **Linux** | 计划中 | 暂无发布 |
| **macOS** | 计划中 | 发布将使用标签 `macos-v<version>` |

每个平台都有各自的标签、文件、校验和与签名。GitHub 的“Latest”针对整个仓库，并非各平台专属更新源，请从名称包含你所用平台的发布下载。标签与更新规则见[平台与发布渠道](docs/PLATFORMS.md)。在某个发布明确说明之前，此表中的任何内容都不代表桌面端的协议支持或安装测试结论。

## Rovunet 能做什么

以下目前针对 Android；其他平台有发布后才会加入此列表。

- **配置组**：按服务器和账户归并相关路径；记住明确选择和已完成的测量结果。
- **直接连接**：第一次点按选中路径，再点按即连接。连接前没有强制的 ping 或解析器扫描。
- **真实 HTTPS 测试**：通过隧道发出真实 HTTPS/204 请求，校验证书，并给出成功率和延迟离散度。以往测量失败不会阻止手动连接。
- **多种协议**：SSH、DNS 隧道（DNSTT、NoizDNS、VayDNS、Slipstream、Soar）、NaiveProxy、SOCKS5、DoH、VLESS WebSocket、REALITY、XHTTP、Hysteria2、WireGuard 和 Tor。
- **订阅与分享**：HTTPS 订阅、`rovunet://` 链接、二维码、文件和密码加密备份。
- **四种语言**：波斯语、英语、俄语和简体中文。

完整列表、限制与注意事项见 [Android 页面](docs/android/README.zh-CN.md)。需要服务器和有效凭据；应用不提供免费账户，也没有任何协议能保证在所有网络中可用。

## Android 快速开始

1. **下载**：从[发布页](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha16.1)获取 Full APK 并安装。公开 alpha14 及更新版本可直接覆盖安装，无需卸载。
2. **打开**：选择语言和主题，并在提示时授予 Android VPN 权限。
3. **导入**：订阅 → 粘贴 HTTPS 订阅链接 → **导入**；或 **+ → 导入** 链接、二维码或文件。
4. **连接**：展开配置组，点按路径选中，再点按一次连接。

## 指南

| | |
|---|---|
| 📘 完整指南（PDF） | [English](docs/guides/Rovunet-App-Guide-EN.pdf) · [فارسی](docs/guides/Rovunet-App-Guide-FA.pdf) · [Русский](docs/guides/Rovunet-App-Guide-RU.pdf) · [简体中文](docs/guides/Rovunet-App-Guide-ZH.pdf) |
| [用户指南](docs/USER_GUIDE.zh-CN.md) | 安装、导入、连接、测试、Soar、凭据与故障排查 |
| [Android 页面](docs/android/README.zh-CN.md) | 发布状态、功能、协议与限制 |
| [平台](docs/PLATFORMS.md) · [当前状态](docs/STATUS.md) · [升级](docs/UPGRADE.md) | 发布渠道、发布状态、签名与迁移说明 |

PDF 保留 alpha15.4 版本和版式；新的账户状态、菜单与空闲行为见当前用户指南及 [alpha16.1 说明](docs/releases/alpha16.md)。

## 源码与支持

[构建](docs/BUILDING.md) · [发布流程](docs/RELEASING.md) · [安全](SECURITY.md)。

寻求支持时，请提供应用与系统版本、协议、复现步骤和本地安全报告。不要公开配置链接、密码、私钥或设备标识。社区：[t.me/RovunetVPN](https://t.me/RovunetVPN)。

## 自愿支持开发

如愿帮助项目发展，可自愿捐助；使用应用不需要捐款。**USDT，仅限 Tron/TRC20：**

`TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

## 许可证与致谢

AGPL-3.0：[LICENSE](LICENSE)、[NOTICE](NOTICE.md)。Xray-core 为 MPL-2.0，版本与声明保留。

基于 Anonvector 的 [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full。Rovunet 是独立项目，与 SlipNet 无隶属关系。
