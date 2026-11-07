<p align="center"><img src="assets/rovunet-logo.png" alt="Rovunet" width="180"></p>

[English](README.md) · [فارسی](README.fa.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md)

# Rovunet

**通过多种路径访问自有服务器的 Android 多协议 VPN 客户端。** Rovunet 集成 DNS 隧道、SSH 传输、HTTPS 代理和 Xray 配置。相关路径归入命名集合，可直接连接，也可单独运行经过真实隧道的 HTTPS 测试。

[发布与下载](https://github.com/msVPNco/rovunet-android/releases) · [Telegram](https://t.me/RovunetVPN) · [服务器面板](https://github.com/msVPNco/rovunet-panel) · [用户指南](docs/USER_GUIDE.zh-CN.md)

## 发布状态

当前版本为 **0.3.0-alpha15.4 Full，版本代码 31**，沿用 alpha14 的[生产签名](docs/SIGNING-IDENTITY.md)，可直接覆盖公开 alpha14，无需卸载。旧 alpha11 和私有 Rovunet Test 的身份不同。[下载 Full APK](https://github.com/msVPNco/rovunet-android/releases/latest/download/Rovunet-v0.3.0-alpha15.4-Full.apk) · [发布记录](docs/releases/alpha15.4.md) · [状态](docs/STATUS.md)。

alpha15.4 修复 Soar 扫描、订阅操作、菜单、重复导入、测试预算和状态显示。连接选择指南与扫描器使用四语言资源。[CI](https://github.com/msVPNco/rovunet-android/actions/runs/42395755189) 通过 **60 项 JVM、51 项 Debug 模拟器测试及 2 项精简 Release JNI 测试**，设备测试无跳过。33 个原生库与已审阅的 15.3 APK 相同，不宣称完整 native 重建。所有者批准发布同一个审阅 APK，未提供新的详细真实网络测量。

**从公开 alpha14/alpha15 升级：**旧更新器不识别带点的 alpha 修订号。请直接下载 [Full APK](https://github.com/msVPNco/rovunet-android/releases/download/v0.3.0-alpha15.4/Rovunet-v0.3.0-alpha15.4-Full.apk) 并覆盖安装，不卸载、不清除数据。alpha15.4 支持后续带点修订号，包名和生产签名保持不变。

## 订阅、分享与 Soar 扫描

HTTPS 订阅提供 **更新** 和 **导入**。新来源一次导入成功后关闭整个窗口；错误保留表单，重复来源地址被拒绝，本地编辑得到保留。更新已有来源前须断开 VPN；移除旧条目仍需审核。可选自动刷新约每 12 小时在合适的网络/电池条件下运行。小集合可用 QR/PNG 分享，过大集合使用文件。

Soar 扫描有快速、高级、E2E 和 Prism。快速/Prism 用配置的 Ed25519 密钥验证真实 Soar 握手；E2E 还验证 HTTP 204 或使用已批准服务器身份的 SSH 账户。高级模式只诊断 DNS。UDP/TCP 独立测试，Soar 扫描默认两者，不改写已保存的连接选择；丢弃旧协议的证明时保留导入的候选地址。

需要 Android 7.0/API 24 或更新版本。目标架构为 ARM32、ARM64、x86_64；继承的 Naive/Tor 二进制不提供 x86_64。不承诺 32 位 x86 或新的 Lite 包。尚无 Google Play、App Store 或 Rovunet 桌面图形版本。

## 功能

| 功能 | 当前行为 |
| --- | --- |
| 集合 | 按集合、账号及服务器元数据归组，保留明确路径选择与完成的测试结果。 |
| 直接连接 | 首次点击选择，再次点击已选路径连接。展开、编辑、菜单不触发连接，无需先测延迟或扫描解析器池。 |
| 测试 | 真实隧道内 HTTPS/204、证书验证、有限备用目标、成功比例和延迟波动；集合测试进入 DNS 回退前须同意。 |
| 界面 | 浅色、深色、AMOLED，清晰集合层级、当前网络有效 HTTPS 结果的绿色标记、四语言资源、快捷控制和流量计数。 |
| 配置 | 链接/文本/文件/QR 导入预览、分享、集合导出、密码加密备份及显式凭据恢复，保留分享与设备限制。 |
| 安全诊断 | SSH 身份确认、TLS 与保存的自签名身份验证、凭据存储加密、私有 Xray SOCKS 认证、可取消启动与本地安全报告。不自动上传；安全报告不含端点、账号或凭据字段。 |

主要界面、设置和编辑器资源支持波斯语、英语、简体中文和俄语。扫描器标签、映射错误及连接指南也已本地化；技术名、地址和语法保持原样。

## 协议

| 家族 | 路径 |
| --- | --- |
| SSH | 直接 SSH、兼容 TLS/STunTLS 的配置、HTTP CONNECT、WebSocket；密码或支持的密钥认证。 |
| DNS | DNSTT、NoizDNS、VayDNS、Slipstream、Soar 及支持的 SSH 组合；后端支持的解析器发现/调节。 |
| HTTPS/代理 | NaiveProxy、NaiveProxy + SSH、远程 SOCKS5、DoH。普通 SOCKS5 不加密传输；DoH 单独使用只加密 DNS。 |
| VLESS/内核 | VLESS WebSocket、REALITY、XHTTP、Hysteria2、通过集成内核的标准单对端 WireGuard。 |
| Tor | Full 中的 Tor 与支持的 Snowflake/obfs4/Meek/自定义网桥，取决于架构与后端。 |

**新建 Soar 和 Soar+SSH 均默认 UDP + TCP。** 已有/导入的选择保留；自动、UDP + TCP、仅 TCP、仅 UDP 是独立选项。SSH 叠加隧道还要求服务器允许其目的地址和端口。

支持 `rovunet://` 包、兼容 `veyra://`/`slipnet://` 别名、支持的 VLESS/Hysteria2 链接及单对端 WireGuard INI。原生导出 v33；未知未来版本明确拒绝，避免不完整导入。不宣称支持任意内核 JSON、AmneziaWG 或全部 WireGuard URI 格式。

## 使用与更新

检查导入预览，展开集合，选择路径并再次点击或使用集合按钮。返回应用时，记住的路径已选中。授予 VPN 权限；批准未知 SSH/自签名身份前应与管理员核对指纹。后台任务不自动批准新身份。

测试与连接独立，历史失败不阻止手动连接。“已连接”表示技术就绪，并非每项服务均可用。HTTPS 延迟不是下载速度或长期稳定性保证。

更新器在应用内下载兼容的 GitHub APK，验证哈希、包名、版本、签名后交给 Android 安装器；不会静默安装，不提供备用镜像。不同签名不能自动变成兼容升级。

**不要卸载唯一能读取配置的应用。** 公开 alpha11 与 Rovunet Test 身份不同。密码加密批量导出排除锁定配置；受保护/绑定设备的配置可能需要原链接或变更签名后的重新签发。[迁移](docs/adr/0002-signing-transition.md)。

## 面板与限制

[面板](https://github.com/msVPNco/rovunet-panel)管理支持的 Linux 服务并导出配置；客户端支持不等于面板自动安装，部分 `core-plan` 路径须手动部署。需自有服务器和有效凭据，不提供免费账号。

尚无集合自动故障切换、下载吞吐量测试或桌面/商店版本。Soar 自动模式差异和远程 SOCKS 数据路径仍需实测。任何协议均不保证在所有网络可用。

## 源码与支持

显式源码 ZIP 包含代码、资源、测试、许可证和可获取的固定原生源码。轻量包省略预编译 `.so`/`.aar`。`SOURCE-MANIFEST.json` 记录版本、复用二进制及无法获取的旧 `dnstt-mobile` 树；不宣称可独立完整重建全部原生组件。

[构建](docs/BUILDING.md) · [发布](docs/RELEASING.md) · [安全](SECURITY.md) · [内核格式](VEYRA-CORE.md)。

反馈请提供应用/Android 版本、协议、步骤和本地安全报告。不要公开配置链接、密码、私钥或设备标识。社区：[t.me/RovunetVPN](https://t.me/RovunetVPN)。

## 自愿支持开发

如愿帮助项目发展，可自愿捐助；使用应用不需要捐款。**USDT，仅限 Tron/TRC20：**

`TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

## 许可证与致谢

AGPL-3.0：[LICENSE](LICENSE)、[NOTICE](NOTICE.md)。Xray-core 为 MPL-2.0，版本与声明保留。

基于 Anonvector 的 [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full。Rovunet 是独立项目，与 SlipNet 无隶属关系。
