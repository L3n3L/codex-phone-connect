# 手机到 GPT / Claude 的目标路由对接执行复盘

> 本文记录双目标路由实现。WorkBuddy 和 DSH 继续复用同一显式 `agentId` 路由与串行队列，分别见 [`workbuddy-integration-review.md`](workbuddy-integration-review.md) 与 [`deepseek-harness-integration-review.md`](deepseek-harness-integration-review.md)。

更新时间：2026-10-17

## 1. 本次变更结论

本次将手机端的发送和滚动操作改为“每条请求携带明确目标”的路由方式：

```text
手机当前选择
    ↓
外层桥接壳记录 selectedAgent
    ↓
每条 /api/jobs、/api/control/scroll 请求补充 agentId
    ↓
BridgeWebServer 校验并在入队时锁定目标
    ↓
BridgeJobQueue 串行执行
    ↓
AgentInputAdapter 操作对应桌面窗口
```

`activeAgent` 不再是新请求判断目标的唯一依据，只保留三项职责：

1. 页面初始化和电脑端 UI 展示当前选择；
2. 保存用户下次启动时的默认目标；
3. 兼容没有携带 `agentId` 的旧版手机请求。

## 2. 为什么要改

旧版单智能体页面发送的是：

```json
{
  "text": "你好"
}
```

旧版滚动请求发送的是：

```json
{
  "deltaY": 80,
  "requestId": "gesture-1"
}
```

这两个请求都没有目标字段。双智能体接入后，桥接程序只能读取全局 `activeAgent` 来补目标：

```text
手机切换 Claude
    ↓
POST /api/control/agent
    ↓
activeAgent = Claude
    ↓
手机发送旧格式消息
    ↓
服务端读取 activeAgent
```

切换和发送是两个独立的 HTTP 请求，用户快速操作时存在先后顺序竞态；多个手机同时连接时，也会共同修改同一个全局状态。

## 3. 当前实现

### 3.1 冻结粒子页面不修改

`prototype/phone-scroll.html` 继续保持原样。它仍然只负责：

- 粒子运动和触摸形变；
- 滑动增量合并；
- 调用原有 `/api/jobs` 和 `/api/control/scroll`；
- 底部输入框和发送按钮。

目标路由不写进粒子算法，也不改动触摸时序。

### 3.2 外层壳在 HTTP 边界补充目标

正式服务加载冻结页面后，会注入 `PhoneAgentShell`。外层壳包装浏览器的 `fetch`，只拦截两个桥接接口：

- `/api/jobs`
- `/api/control/scroll`

它会把当前手机选择补进 JSON：

```json
{
  "text": "帮我分析这段代码",
  "agentId": "claude"
}
```

或：

```json
{
  "deltaY": 80,
  "requestId": "gesture-1",
  "agentId": "claude"
}
```

这样不需要修改已经验证过的粒子页面。

### 3.3 服务端入队时锁定目标

服务端按以下顺序解析：

```text
请求中有合法 agentId
    ↓ 使用请求中的目标

请求中没有 agentId
    ↓ 兼容旧客户端，使用 activeAgent

agentId 非法
    ↓ 拒绝请求
```

解析完成后立即生成不可变的命令对象：

```csharp
BridgeJob(jobId, text, AgentId.Claude)
BridgeScrollCommand(commandId, deltaY, AgentId.Claude)
```

命令进入队列后，后续切换 GPT 不会改变已经入队命令的目标。

### 3.4 日志可区分路由来源

发送和滚动日志现在记录：

```text
agent=claude source=explicit
```

表示请求主动携带了 `agentId`。

旧客户端日志会显示：

```text
agent=claude source=active
```

表示请求没有目标字段，服务端使用了兼容性的 `activeAgent`。

## 4. 真实案例：快速切换时的链路

假设用户先选择 Claude，发送代码分析；随后切换 GPT，再发送第二条消息。

手机端产生两条请求：

```json
{
  "text": "分析这段 Python 代码",
  "agentId": "claude",
  "requestId": "send-001"
}
```

```json
{
  "text": "帮我重构这个函数",
  "agentId": "gpt",
  "requestId": "send-002"
}
```

桥接程序处理为：

```text
send-001 → BridgeJob(..., Claude) → Claude 窗口
send-002 → BridgeJob(..., GPT)    → GPT 窗口
```

即使两条请求在网络层的到达时间非常接近，目标也不会通过全局状态重新推断。

仍然保留一个全局串行队列，因为剪贴板、鼠标、键盘和窗口焦点属于同一台电脑上的共享输入资源：

```text
Claude 命令
    ↓ 执行完成
GPT 命令
    ↓ 执行完成
下一条命令
```

显式 `agentId` 解决“发给谁”，串行队列解决“避免桌面输入互相抢占”。

## 5. `activeAgent` 的新定位

它仍然可以被 `/api/status` 返回，用于手机首次打开时恢复界面选择；电脑端点击 GPT / Claude 时也会更新它。

但新页面的核心请求不依赖它：

```text
activeAgent = 界面和兼容状态
agentId     = 当前命令的真实路由
```

未来淘汰旧版页面后，可以把没有 `agentId` 的请求改为直接返回 `400 agent_required`，彻底关闭兼容回退。

## 6. 本次范围和非目标

本次完成：

- 手机发送请求显式路由到 GPT 或 Claude；
- 手机滚动请求显式路由到 GPT 或 Claude；
- 服务端入队时锁定目标；
- 保留旧请求兼容回退；
- 日志记录目标来源；
- 不修改冻结粒子页面。

本次不包含：

- 读取聊天记录或 Thread；
- 手机端渲染桌面聊天内容；
- 接入 Codex / Claude 内部协议；
- 多手机会话隔离；
- 发送成功回执和桌面端执行状态查询；
- 局域网身份认证。

## 7. 验证标准

- 构建必须通过且无警告；
- 静态粒子页面内容不发生变化；
- GPT 发送请求日志显示 `source=explicit`；
- Claude 发送请求日志显示 `source=explicit`；
- GPT / Claude 滚动请求日志显示对应的显式目标；
- 切换目标后，已经进入队列的命令保持原目标；
- 未携带 `agentId` 的旧请求仍可通过 `source=active` 兼容执行；
- 非法 `agentId` 被拒绝，不得进入输入队列。

