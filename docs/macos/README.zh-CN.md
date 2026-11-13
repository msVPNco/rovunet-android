[English](README.md) · [فارسی](README.fa.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md)

[下载 0.1.7-beta](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.7-beta) · [固定源码](https://github.com/msVPNco/rovunet-app/tree/macos-v0.1.7-beta/macos) · [项目主页](../../README.zh-CN.md)

# Rovunet macOS 0.1.7-beta

**有限 Local Preview · 预发布 · 内部构建 0.1.7.14 · macOS 15+**

Apple Silicon 界面与本地 HTTP/SOCKS 代理已有正面测试证据。**Intel 尚未在真实 Intel Mac 上测试。** 全局 TUN、kill switch 和恢复仍为实验功能。公开版本接续 0.1.6-beta，内部构建号为 0.1.7.14。

## 下载

| Mac | 安装程序与校验和 | 测试范围 |
| --- | --- | --- |
| Apple Silicon · arm64 | [PKG](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-AppleSilicon-arm64.pkg) · [SHA-256](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/SHA256SUMS.txt) | 已在 Apple Silicon Mac 上测试界面和本地代理；全局 TUN 尚未验收 |
| Intel · x64 | [PKG · NOT-TESTED](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-Intel-x64-NOT-TESTED.pkg) · [SHA-256](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/SHA256SUMS-Intel.txt) | **未在真实 Intel Mac 上测试** |

M 系列选择 arm64，Intel Mac 选择 x64。Air/Pro 与屏幕尺寸不需要单独安装程序。本次提供 PKG，没有 DMG 文件。

应用/helper 使用本地 **ad hoc** 签名。PKG **没有 Developer ID Installer 签名，也未经过 Apple 公证**。不要关闭 Gatekeeper 或降低系统级安全保护。

## 此构建的功能

- 紧凑的 Mac 界面、三种主题、四种语言，以及内部页面上清晰的保存、应用和返回操作。
- 配置组控制、进度与结果反馈；手动连接不依赖 ping 或 DNS 扫描。
- 活动路径延迟显示，以及根据内容可见性调度的长文本滚动。
- HTTPS 订阅与导入；提供方报告的流量/到期信息与独立数据传输测试分开显示。
- SSH/Slipstream 首次身份信任、可选本地/系统代理，以及用户主动选择的实验性 TUN。

## 安装、更新与回退

1. 下载符合处理器的 PKG，并与对应校验和文件核对。
2. 正常断开 Rovunet，用 Command-Q 退出，覆盖安装后从 Applications 打开。安装/helper 或更改系统网络设置可能需要管理员授权。
3. 保留应用数据、订阅和 Keychain 项目；锁定时使用明确的 Unlock 操作。保留旧安装程序用于回退；重新安装前正常断开并退出，不要清除数据。

TUN 为可选功能。需要时使用随包提供的同架构 dev.4 helper；已有 dev.3 并不能单独证明此构建兼容或已验收。本地代理测试不需要开启 TUN。

## 验证与未解决事项

Apple Silicon 记录了 491 个已安装文件检查，以及界面、订阅、网络、领域逻辑、协议和代理共 2,163 项目标断言。代理在所有者 Mac 上通过直连 Wi-Fi 测试了 Canada HY2 的完整 HTTP/SOCKS 响应体，并另行进行了隔离数据包 DNS/HTTPS 测试。这些是代理测试结果，不是所有者的新确认，也不是全局 TUN 验收。

Intel 从同一固定应用源码交叉构建。全部 34 个 Mach-O 文件包含 x86_64；本地签名/资源及安装包解压后 492 个文件的精确比对通过。**尚未在真实 Intel Mac 上测试安装、启动、界面、网络和 TUN。** [Intel 验证记录](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/INTEL-VERIFICATION.json)。

用户报告的 VLESS WS TLS 故障仍在调查；后续 bootstrap 修复**不包含在此预发布中**。未宣称 DNS 传输普遍可用。普通 WireGuard、全局 TUN 恢复/kill switch 验收、面板 private status v2 兼容和长期能耗仍待完成。不承诺完整/多绑定 Mieru 文档、任意链路或任意应用强制绕过隧道。

## 源码与发布记录

[源码 ZIP](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-source.zip) · [发布说明与哈希](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.7-beta) · [从内部 0.1.7.13 开始的补丁](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-from-local-0.1.7.13.patch)

固定源码提交：`ab577440f17193fd43cfbe761f63e2d2bffdacd8`。公开 beta 编号与内部构建号不同。GitHub Latest 针对整个仓库；请选择 macOS 发布。不要公开私人配置链接或凭据。
