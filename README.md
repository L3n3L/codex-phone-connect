# Codex Phone Connect

> Control the Codex Desktop window from your phone — privately, over your local Wi‑Fi.
>
> 用手机给电脑上的 Codex Desktop 发消息，不需要云端账号、不需要中继服务器。

[English](#english) · [中文说明](#中文说明)

## English

### What is it?

Codex Phone Connect is a lightweight Windows bridge for sending text from a phone browser to the Codex Desktop window currently open on your PC.

It is designed for a simple workflow:

```text
Scan the QR code → type on your phone → tap Send → Codex receives it
```

The bridge runs locally on Windows. The phone page and the bridge communicate over your private Wi‑Fi network; the bridge then uses the calibrated Codex window coordinates to paste and send the message.

### Why this project?

- No cloud relay required.
- No Codex Thread or app-server integration required.
- Works with the Codex Desktop UI even when semantic UI controls are unavailable.
- Keeps the phone interface intentionally minimal: one input box and one Send button.
- Saves calibration locally and keeps operational logs available for debugging.

### Features

- Native C# / .NET 8 / WPF desktop application.
- Self-drawn light desktop UI with QR code pairing.
- Local phone page served by the desktop bridge.
- Two-point calibration for the Codex input box and Send button.
- Single-threaded message queue to avoid concurrent clicks.
- Clipboard save → paste → Send click → clipboard restore.
- DPI-aware coordinate conversion for Windows display scaling.
- Daily service logs with automatic 14-day cleanup.
- No message body is written to logs by default.

### Requirements

- Windows 10 or Windows 11.
- .NET 8 SDK or a published .NET 8 runtime.
- Codex Desktop installed and running.
- Phone and PC on a local network that allows device-to-device access.

Guest Wi‑Fi, VPN LAN blocking, and router AP/client isolation can prevent the phone from reaching the PC even when both devices show the same Wi‑Fi name.

### Quick start

```powershell
dotnet build .\CodexPhoneConnect.Desktop.csproj
dotnet run --project .\CodexPhoneConnect.Desktop.csproj
```

Then:

1. Open the address shown in the desktop window, or scan its QR code.
2. In the desktop window, click **设置** next to **输入框**.
3. Click the center of the Codex input box.
4. Click **设置** next to **发送按钮**.
5. Click the center of the Codex Send button.
6. Click **保存**.
7. Type a message on the phone and tap **发送**.

The calibration click is intercepted so choosing the Send button during calibration does not submit Codex content accidentally.

### Data flow

```text
Phone browser
    │  POST /api/jobs
    ▼
Codex Phone Connect
    │  single-threaded queue
    ▼
Codex Desktop window
    │  activate → click input → paste → click Send
    ▼
Current Codex conversation
```

The bridge does not identify or switch Codex Threads. It sends to the Codex window selected by the local input adapter at send time.

### Troubleshooting

#### The phone cannot open the address

Use the exact `http://...:8787` address shown by the desktop app. Then check:

- The phone is not using a VPN or private DNS that blocks local devices.
- The phone is not on a guest network.
- The router does not have AP/client isolation enabled.
- The phone and PC have compatible private IP addresses, such as `192.168.1.x`.

#### The click lands in the wrong place

Run both calibration steps again after changing the Codex window size, monitor, or Windows display scaling. The bridge invalidates coordinate data created by an older coordinate format.

#### Where are the logs?

During development:

```text
logs/bridge-YYYY-MM-DD.log
```

Configuration is stored in:

```text
%LOCALAPPDATA%\CodexPhoneConnect\settings.json
```

### Project layout

```text
CodexPhoneConnect.Desktop.csproj  WPF project
MainWindow.xaml                   Calibration UI
BridgeWebServer.cs                Phone page and local HTTP API
BridgeJobQueue.cs                 Single-threaded job queue
CodexInputAdapter.cs              Clipboard and Win32 input adapter
BridgeSettings.cs                 Calibration persistence
BridgeLog.cs                      Daily file logger
docs/                             Integration review and design notes
```

### Current boundaries

This project is intentionally a local UI bridge. It does not currently provide:

- Cloud relay or remote access over the public internet.
- Codex Thread listing, history rendering, or Thread switching.
- Codex response synchronization back to the phone.
- An installer or automatic firewall configuration.

Do not expose port `8787` directly to the public internet.

### Disclaimer

Codex Phone Connect is an unofficial community project and is not affiliated with OpenAI.

## 中文说明

### 这是什么？

Codex Phone Connect 是一个 Windows 桌面桥接程序，让手机浏览器可以通过局域网向电脑上当前打开的 Codex Desktop 发送文字。

```text
手机扫码 → 手机输入 → 点击发送 → Codex 收到消息
```

它不接入云端中继，也不读取 Codex 内部 Thread，而是由电脑端桥接程序操作当前 Codex 窗口：激活窗口、点击输入框、粘贴文字、点击发送按钮。

### 快速开始

```powershell
dotnet build .\CodexPhoneConnect.Desktop.csproj
dotnet run --project .\CodexPhoneConnect.Desktop.csproj
```

启动后：

1. 用手机扫描桌面窗口中的二维码；
2. 在桌面程序中设置“输入框”；
3. 点击 Codex 输入框中心；
4. 设置“发送按钮”；
5. 点击 Codex 发送按钮中心；
6. 点击“保存”；
7. 手机输入消息并发送。

### 注意事项

- 手机和电脑必须处于允许设备互访的局域网。
- 不要使用访客 Wi‑Fi。
- 如果手机开着 VPN，请关闭“禁止访问局域网”或暂时关闭 VPN。
- Codex 窗口大小、显示器或 Windows 缩放改变后，需要重新校准。
- 日志位于项目下的 `logs` 文件夹。
- 日志默认只记录任务 ID、字符数、状态和错误，不记录手机发送正文。

### 当前限制

- 只操作当前电脑上的 Codex Desktop 窗口。
- 不支持 Thread 切换和历史同步。
- 不支持云端中继。
- 不要把 `8787` 端口暴露到公网。

详细边界和对接复盘见 [`docs/integration-review.md`](docs/integration-review.md)。
