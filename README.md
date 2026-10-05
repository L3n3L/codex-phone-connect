# Codex Phone Connect

> 用手机给电脑上的 Codex Desktop 发消息，局域网直连，轻量、私密、无需云端中继。

[English](#english)

## 中文说明

### 它解决什么问题？

Codex Phone Connect 是一个 Windows 桌面桥接程序，让手机浏览器通过局域网向当前电脑上的 Codex Desktop 发送文字。

```text
手机扫码 → 手机输入 → 点击发送 → Codex 收到消息
```

桥接程序运行在 Windows 电脑上；手机页面通过同一局域网访问电脑，电脑端再激活 Codex 窗口、点击输入框、粘贴文字并点击发送。

### 功能亮点

- C# / .NET 8 / WPF 原生 Windows 程序；
- 桌面端显示局域网地址和二维码；
- 手机页面只有输入框和发送按钮；
- 输入框、发送按钮两点校准；
- 单线程发送队列，避免多条消息同时点击；
- 保存剪贴板 → 粘贴 → 点击发送 → 恢复原剪贴板；
- 适配 Windows DPI 缩放；
- 按天写入服务日志并自动清理 14 天旧日志；
- 默认不把手机发送正文写入日志。

### 环境要求

- Windows 10/11；
- .NET 8 SDK 或 .NET 8 运行时；
- 已安装并运行 Codex Desktop；
- 手机和电脑处于允许设备互访的局域网。

访客 Wi-Fi、VPN 的“禁止访问局域网”、路由器无线隔离/AP 隔离都会阻止手机访问电脑，即使 Wi-Fi 名称相同。

### 快速开始

```powershell
dotnet build .\CodexPhoneConnect.Desktop.csproj
dotnet run --project .\CodexPhoneConnect.Desktop.csproj
```

启动后：

1. 用手机扫描桌面窗口中的二维码，或手动打开显示的 `http://电脑IP:8787`；
2. 点击“输入框”右侧的“设置”，再点击 Codex 输入框中心；
3. 点击“发送按钮”右侧的“设置”，再点击 Codex 发送按钮中心；
4. 点击“保存”；
5. 在手机页面输入消息并点击“发送”。

校准时的点击会被拦截，不会因为选择发送按钮而误发送 Codex 原有内容。

### 数据流

```text
手机浏览器
    │ POST /api/jobs
    ▼
Codex Phone Connect
    │ 单线程发送队列
    ▼
Codex Desktop 窗口
    │ 激活 → 点击输入框 → 粘贴 → 点击发送
    ▼
当前 Codex 会话
```

它不会读取、切换或识别 Codex Thread，发送目标是本机当前可找到的 Codex Desktop 窗口。

### 手机打不开怎么办？

1. 先关闭手机 VPN、私人 DNS 和移动数据；
2. 不要使用访客 Wi-Fi；
3. 确认手机和电脑 IP 在同一私有网段，例如都为 `192.168.1.x`；
4. 确认路由器没有开启“无线隔离 / AP 隔离 / 客户端隔离”；
5. 用 Chrome 或系统浏览器手动打开 `http://电脑IP:8787`，不要先用微信内置浏览器；
6. 如果仍不通，可以临时让电脑和手机连接到同一个手机热点。

### 点击位置不准怎么办？

如果改变了 Codex 窗口大小、显示器、Windows 显示缩放或布局，请重新校准两点。旧坐标格式会自动失效，避免误点击。

### 日志和配置

服务日志：

```text
logs/bridge-YYYY-MM-DD.log
```

配置文件：

```text
%LOCALAPPDATA%\CodexPhoneConnect\settings.json
```

日志包含任务 ID、字符数、状态、校准和错误信息，不默认记录手机正文。

### 项目结构

```text
CodexPhoneConnect.Desktop.csproj  WPF 项目
MainWindow.xaml                   校准窗口
BridgeWebServer.cs                手机页面和局域网 HTTP API
BridgeJobQueue.cs                 单线程任务队列
CodexInputAdapter.cs              剪贴板和 Win32 输入适配器
BridgeSettings.cs                 校准配置持久化
BridgeLog.cs                      日志系统
docs/                             对接复盘和设计文档
```

### 当前边界

当前项目暂不提供：

- 云端中继或公网访问；
- Codex Thread 列表、历史渲染和会话切换；
- Codex 回复同步回手机；
- 安装包和自动防火墙配置。

不要把 `8787` 端口直接暴露到公网。

### 项目状态

手机 → Bridge → Codex 的实际发送链路已经验证成功。后续可继续完善安装包、连接诊断和更安全的本地授权。

### 免责声明

Codex Phone Connect 是非官方社区项目，与 OpenAI 无隶属关系。

## English

> Control Codex Desktop from your phone over local Wi-Fi.

Codex Phone Connect is a small Windows WPF bridge that serves a minimal phone input page and forwards messages to the currently running Codex Desktop window through calibrated coordinates.

### Quick start

```powershell
dotnet build .\CodexPhoneConnect.Desktop.csproj
dotnet run --project .\CodexPhoneConnect.Desktop.csproj
```

1. Scan the QR code shown by the desktop app.
2. Calibrate the Codex input box.
3. Calibrate the Codex Send button.
4. Save the calibration.
5. Send a message from the phone.

### Security and limitations

- Local Wi-Fi only; no cloud relay.
- Do not expose port `8787` to the public internet.
- The bridge does not read or switch Codex Threads.
- Logs record operational metadata, not message bodies by default.
- This is an unofficial community project and is not affiliated with OpenAI.

详细设计和对接边界见 [`docs/integration-review.md`](docs/integration-review.md)。
