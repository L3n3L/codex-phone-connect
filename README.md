# Codex Bridge Desktop

原生 Windows 桌面桥接程序，使用 C#、.NET 8 和 WPF。

## 当前阶段

当前版本包含：

- WPF 校准窗口；
- 输入框和发送按钮位置设置；
- 局域网 HTTP 服务骨架；
- `GET /api/status`；
- `POST /api/jobs`。

真正的剪贴板和 Codex 输入注入会在后续阶段接入，并在用户本机验证。

## 构建

```powershell
dotnet build .\CodexBridge.Desktop.csproj
dotnet run --project .\CodexBridge.Desktop.csproj
```

详细边界见 [docs/integration-review.md](docs/integration-review.md)。
