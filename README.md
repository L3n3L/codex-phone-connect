# Codex Bridge Desktop

原生 Windows 桌面桥接程序，使用 C#、.NET 8 和 WPF。

## 当前阶段

当前版本包含：

- WPF 校准窗口；
- 输入框和发送按钮位置设置；
- 局域网 HTTP 服务骨架；
- `GET /api/status`；
- `POST /api/jobs`；
- 服务日志写入 `logs/bridge-YYYY-MM-DD.log`，默认保留 14 天；
- 日志默认只记录任务 ID 和字符数，不记录手机发送正文。
- 手机任务单线程排队；
- 校准后通过剪贴板和 Win32 输入事件向当前 Codex 窗口粘贴并点击发送。

Codex 输入注入需要用户先在本机完成两个坐标校准，再进行实际验证。

## 构建

```powershell
dotnet build .\CodexBridge.Desktop.csproj
dotnet run --project .\CodexBridge.Desktop.csproj
```

详细边界见 [docs/integration-review.md](docs/integration-review.md)。

## 日志

开发运行时日志位于项目根目录的 `logs` 文件夹；发布后如果找不到项目文件，则位于程序旁边的 `logs` 文件夹。日志按天滚动，过期日志自动清理。
