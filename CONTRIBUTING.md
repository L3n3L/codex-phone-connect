# 参与贡献

欢迎反馈问题、提出功能建议或提交改进。

## 提交前

- 先搜索现有 Issues，避免重复讨论。
- 大型功能或会改变手机交互的改动，请先开 Issue 讨论范围和验收方式。
- 当前支持目标是 Codex / ChatGPT Desktop 与 Claude Desktop；需要真实验证窗口识别、三点校准和发送/滚动路径。
- 不要提交日志、配置文件、访问令牌、个人 IP 或聊天内容。

## 本地验证

环境：Windows 10/11、.NET 8 SDK。

```powershell
dotnet build .\CodexPhoneConnect.Desktop.csproj --configuration Release
```

提交 Pull Request 时，请说明改动目的、验证步骤，以及对手机端或桌面校准流程的影响。涉及手机粒子交互的改动，请附真实设备上的录屏或截图，并注明浏览器与设备尺寸。

