[English](README.md) · [فارسی](README.fa.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md)

Publication of 0.1.6 Beta is in preparation; installation assets have not been uploaded yet. / انتشار 0.1.6 Beta در حال آماده‌سازی است؛ فایل‌های نصب هنوز بارگذاری نشده‌اند. / Публикация готовится; установщики ещё не загружены. / 正在准备发布，安装包尚未上传。

[Source](https://github.com/msVPNco/rovunet-app/tree/macos/release-0.1.6-beta/macos)

# Rovunet macOS 0.1.6 Beta

**预发布 · macOS 15+ · Apple Silicon arm64 / Intel x64**

Apple Silicon 功能基础已由所有者在真实 Mac 上测试并认可。**Intel：有限测试**，包含主机测试与软件包检查，尚无真实 Intel Mac 运行记录。本次保留 dev.6 功能，将应用和安装程序版本更新为 0.1.6 Beta。

- 紧凑的 Mac 界面、三种主题、四种语言、固定保存/应用按钮、长文本滚动。菜单栏图标：断开为白色、连接中为黄色、已连接为绿色、错误为红色。
- 针对同一配置的 DNS 搜索：Simple、Advanced、E2E、Prism。DNS 应答、服务器可达性、可选 HTTPS 分别测量。第二阶段隧道测试可与搜索同时进行，选中结果直接应用到原配置。
- 可选全系统 TUN、系统代理和供 Telegram 等应用使用的本地代理。复用同架构已安装且兼容的 TUN 服务。
- SSH 与 Slipstream 自动获取服务器身份，首次使用需要确认信任。手动连接不依赖 ping 或扫描。
- HTTPS 订阅、导入、面板报告的流量/到期信息、手动/故障切换/智能选择；在原有协议之外提供 AnyTLS、TUIC 和单绑定 Mieru。

## 安装与更新

M1/M2/M3/M4 及更新的 M 系列使用 **AppleSilicon-arm64**；Intel Mac 使用 **Intel-x64**。均要求 macOS 15+。Air/Pro 与屏幕大小不需要单独安装包。

用 Command-Q 退出旧版，安装对应处理器的 PKG，然后从 Applications 打开 Rovunet。也可挂载 DMG，将 Rovunet 拖到 Applications 后推出磁盘。保留原有配置和 Keychain 项目。首次使用全系统 VPN 时，在设置中安装 TUN 服务，再启用 System-wide VPN (TUN)；兼容服务无需重复安装。安装和实际网络设置变更可能请求管理员授权。

从配置编辑器打开 DNS 搜索，选择结果并点击顶部固定勾选按钮；Command-S 也可保存表单。暂不包含完整/多绑定 Mieru 文档、任意链式连接及不支持代理应用的强制进程路由。具体文件与测试范围见 SHA256SUMS.txt、VERIFICATION.json 和对应源代码归档。
