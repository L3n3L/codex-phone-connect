# Codex Phone Connect

> 用手机控制电脑上的 GPT / Codex Desktop 和 Claude Desktop，局域网直连，轻量、私密、无需云端中继。

[English](#english)

## 中文说明

### 它解决什么问题？

Codex Phone Connect 是一个 Windows 桌面桥接程序，让手机浏览器通过局域网向电脑上的 GPT / Codex Desktop 或 Claude Desktop 发送文字，并控制对应桌面窗口滚动。

~~~text
手机扫码 → 选择 GPT / Claude → 输入或滑动 → 对应桌面智能体收到操作
~~~

电脑端负责激活对应桌面窗口、点击输入框、粘贴文字、点击发送或注入滚轮。手机端不读取聊天记录，也不连接智能体内部会话。

### 功能亮点

- C# / .NET 8 / WPF 原生 Windows 程序；
- 当前支持 GPT / Codex Desktop 与 Claude Desktop 两个目标；
- 电脑端和手机端都可以切换目标；
- 两个目标分别保存输入框、发送按钮、滚动区域三点校准；
- 手机粒子交互实时遥控对应桌面聊天区域滚动；
- 单线程输入队列，避免多个窗口同时抢焦点；
- 保存剪贴板 → 粘贴 → 点击发送 → 恢复原剪贴板；
- 归一化客户区坐标，适配窗口移动、尺寸变化和 DPI 缩放；
- 按天写入服务日志，默认不记录手机发送正文。

### 环境要求

- Windows 10/11；
- .NET 8 SDK 或 .NET 8 运行时；
- 已安装并运行 GPT / Codex Desktop 或 Claude Desktop；
- 手机和电脑处于允许设备互访的局域网。

访客 Wi-Fi、VPN 的“禁止访问局域网”、路由器无线隔离/AP 隔离都会阻止手机访问电脑，即使 Wi-Fi 名称相同。

### 快速开始

~~~powershell
dotnet build .\CodexPhoneConnect.Desktop.csproj
dotnet run --project .\CodexPhoneConnect.Desktop.csproj
~~~

启动后：

1. 用手机扫描桌面窗口中的二维码，或手动打开显示的 http://电脑IP:8787；
2. 在“控制目标”选择 GPT 或 Claude；
3. 点击“输入框”右侧的“设置”，再点击对应桌面窗口的输入框中心；
4. 点击“发送按钮”右侧的“设置”，再点击对应桌面窗口的发送按钮中心；
5. 点击“滚动区域”右侧的“设置”，再点击对应桌面窗口的聊天内容区域；
6. 点击“保存”；
7. 在手机页面选择目标后输入消息并点击“发送”，或在粒子区域上下滑动控制对应窗口滚动。

### 数据流

~~~text
手机浏览器
    ├─ POST /api/control/agent ──────┐  选择 GPT / Claude
    ├─ POST /api/jobs ───────────────┤  入队时锁定 agentId
    └─ POST /api/control/scroll ─────┤  入队时锁定 agentId
                                    ▼
Codex Phone Connect
    │ 单线程输入队列
    ▼
对应桌面窗口
    ├─ GPT / Codex Desktop
    └─ Claude Desktop
~~~

手机切换目标后，新请求使用新目标；已经进入队列的请求仍使用进入队列时的目标快照，避免切换造成竞态。程序不会读取、切换或识别 Codex Thread，也不会读取 Claude 会话数据。

### 手机打不开怎么办？

1. 关闭手机 VPN、私人 DNS 和移动数据；
2. 不要使用访客 Wi-Fi；
3. 确认手机和电脑 IP 在同一私有网段，例如都为 192.168.1.x；
4. 确认路由器没有开启“无线隔离 / AP 隔离 / 客户端隔离”；
5. 用 Chrome 或系统浏览器手动打开 http://电脑IP:8787，不要先用微信内置浏览器；
6. 如果仍不通，可以临时让电脑和手机连接到同一个手机热点。

### 点击位置不准怎么办？

如果改变了目标窗口大小、显示器、Windows 显示缩放或布局，请重新校准该目标的三个位置。GPT 与 Claude 的坐标互不覆盖。

### 日志和配置

服务日志：

~~~text
logs/bridge-YYYY-MM-DD.log
~~~

配置文件：

~~~text
%LOCALAPPDATA%\CodexPhoneConnect\settings.json
~~~

配置文件分别保存 GPT 与 Claude 的校准点和当前目标；旧版单 Codex 配置会自动迁移到 GPT 配置。

### 项目结构

~~~text
CodexPhoneConnect.Desktop.csproj  WPF 项目
MainWindow.xaml                   校准窗口和目标选择
AgentCatalog.cs                   GPT / Claude 目标定义和进程匹配
AgentInputAdapter.cs              剪贴板和 Win32 输入适配器
BridgeWebServer.cs                手机页面和局域网 HTTP API
PhoneAgentShell.cs                手机端目标选择外壳
BridgeJobQueue.cs                 单线程任务队列
BridgeSettings.cs                 按目标保存校准配置
BridgeLog.cs                      日志系统
assets/brand/                     本地品牌图标
docs/                             对接复盘和设计文档
~~~

### 当前边界

当前项目暂不提供：

- 云端中继或公网访问；
- Codex Thread 列表、历史渲染和会话切换；
- Claude 会话数据读取；
- 回复同步回手机；
- 安装包和自动防火墙配置。

当前只实现 GPT / Codex Desktop 与 Claude Desktop。未来增加目标需要实际开发对应的进程识别、校准策略和输入适配器，不承诺仅改配置即可支持。

不要把 8787 端口直接暴露到公网。

### 设计文档

详细的数据流、并发边界、冻结的手机粒子页和后续验收项见 docs/dual-agent-integration-review.md。

### 免责声明

Codex Phone Connect 是非官方社区项目，与 OpenAI 或 Anthropic 无隶属关系。

## English

> Control GPT / Codex Desktop and Claude Desktop from your phone over local Wi-Fi.

Codex Phone Connect is a small Windows WPF bridge. It serves a minimal phone control page and forwards text, clicks, and scroll commands to one of two calibrated desktop targets: GPT / Codex Desktop or Claude Desktop.

### Quick start

~~~powershell
dotnet build .\CodexPhoneConnect.Desktop.csproj
dotnet run --project .\CodexPhoneConnect.Desktop.csproj
~~~

1. Scan the QR code shown by the desktop app.
2. Select GPT or Claude.
3. Calibrate the input box, Send button, and scroll area for that target.
4. Save the settings.
5. Select a target on the phone, then send a message or swipe the particle area to control scrolling.

### Security and limitations

- Local Wi-Fi only; no cloud relay.
- Do not expose port 8787 to the public internet.
- The bridge does not read or switch Codex Threads or Claude sessions.
- Requests snapshot their target before entering the single input queue.
- Logs record operational metadata, not message bodies by default.
- This is an unofficial community project and is not affiliated with OpenAI or Anthropic.
