# 手机遥控 Codex 滚动：设计

## 总体链路

```text
手机触摸滑动
    │ pointermove → 增量合并 → POST /api/control/scroll
    ▼
BridgeWebServer
    │ ScrollRequest
    ▼
BridgeJobQueue（单线程）
    ▼
CodexInputAdapter
    │ 激活 ChatGPT 窗口 → 定位聊天区域 → SendInput 鼠标滚轮
    ▼
电脑 Codex 已绑定窗口
```

## 模块边界

- `BridgeWebServer`：提供手机静态页面、发送接口和滚动接口；不读取聊天内容。
- `BridgeJobQueue`：统一串行发送文字和滚动指令，避免输入事件互相打断。
- `CodexInputAdapter`：查找 Codex 窗口、激活窗口、将相对客户区坐标转换为屏幕坐标，并注入滚轮。
- `BridgeSettings`：增加 `ScrollPoint`，与输入框、发送按钮一起保存。
- `MainWindow`：增加聊天区域校准入口和校准状态展示。

## HTTP 契约

### `POST /api/control/scroll`

请求：

```json
{
  "deltaY": -180,
  "requestId": "手机端唯一 ID",
  "token": "局域网配对码"
}
```

`deltaY` 使用手机相邻触摸位置的像素差：负数表示手机上滑，正数表示手机下滑。Bridge 将其限制在安全范围内，并反向换算为 Windows 鼠标滚轮量，使手机上滑时内容向下、手机下滑时内容向上。

正式页面实际发送的是相邻 `pointermove` 的增量。页面在滑动超过 36px 后开始发送，并保持最多一个请求在途；请求返回前产生的新增量进入浏览器端累加器，随后合并成下一条请求。这样能保持接近实时，又不会因为局域网延迟形成请求队列。

响应：

```json
{
  "commandId": "服务器生成的命令 ID",
  "status": "queued"
}
```

## 坐标和输入

保存相对于 Codex 顶层客户区的比例坐标：

```json
{
  "inputPoint": { "x": 0.50, "y": 0.92 },
  "sendPoint": { "x": 0.94, "y": 0.92 },
  "scrollPoint": { "x": 0.55, "y": 0.45 }
}
```

滚动不点击聊天区域，只把鼠标移动到校准点并发送 `MOUSEEVENTF_WHEEL`。窗口移动和 DPI 变化仍通过客户区比例换算处理。校准时记录的窗口句柄优先使用；句柄失效后才尝试当前前台或可见的 `ChatGPT` 主窗口，并拒绝非 Codex 进程。

## 手机界面

- 顶部和中部为空白遥控区；
- 首次进入时显示一行很淡的“上下滑动控制 Codex”，首次成功滑动后隐藏；
- 底部固定输入框和“发送”按钮；
- 页面设置 `touch-action: none`，阻止浏览器自身滚动；
- 手机不接收 Codex 内容。

## 并发和失败处理

- 发送和滚动共用单线程队列，确保剪贴板、键盘和鼠标输入不会并发注入；
- 滚动消息在手机端按帧合并，在桌面端按命令顺序执行；
- 滑动命令只记录方向、距离和命令 ID，不记录聊天正文；
- 未校准、找不到 Codex、无法激活或 `SendInput` 失败时返回失败状态并写入日志；
- 当前 LAN 服务仍不提供身份认证，后续应在开放滚动控制前增加配对 Token。

## 测试策略

- `dotnet build` 编译验证；
- HTTP 测试：空请求、边界距离、正常入队；
- 输入适配器手动验证：上滑、下滑、短滑动、非 Codex 前台窗口；
- 用户验收：手机上滑使 Codex 内容向下滚动，手机下滑使 Codex 内容向上滚动，发送功能不回归。
