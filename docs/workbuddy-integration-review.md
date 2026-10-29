# WorkBuddy 智能体接入对接复盘

> 本文记录 WorkBuddy 接入。之后加入的 DeepSeek Harness（DSH）见 [`deepseek-harness-integration-review.md`](deepseek-harness-integration-review.md)。

更新时间：2026-10-26

## 目标

在 GPT / Claude 之外增加腾讯 WorkBuddy 桌面端。电脑与手机各增加一个可选目标；每个目标独立保存输入、发送、滚动校准点。手机请求继续带显式 `agentId`，由同一串行输入队列执行。

## 可复用的链路

```text
手机选择 WorkBuddy
    ↓ 每次发送 / 滚动明确带 agentId=workbuddy
BridgeWebServer 校验目标并在入队前锁定
    ↓
BridgeJobQueue 串行化桌面共享输入
    ↓
AgentInputAdapter 按进程 WorkBuddy 找窗口
    ↓
从该窗口归一化坐标定位输入框 / 发送 / 滚动
```

不接 WorkBuddy 内部协议或任务数据，不访问对话历史。WorkBuddy 的产品任务可能和普通聊天窗口表现不同，因此必须在实际桌面端重新校准并实测三个位置；代码不预设其输入框、按钮或滚动区域的坐标。

## 配置兼容

- 新增 `AgentId.WorkBuddy`，追加在现有枚举末尾，保证历史 GPT=0、Claude=1 序列化值不变。
- 配置继续沿用当前文件；新增 `WorkBuddy` 校准配置。旧 JSON 缺少该属性时由空配置初始化，GPT / Claude 的坐标保留。
- `AgentCatalog` 统一登记 Wire ID、界面名、进程名、品牌资源和选中颜色；手机菜单由目录生成，避免复制粘贴静态目标列表。
- `AgentInputAdapter` 只通过 `AgentCatalog` 中的 `WorkBuddy` 进程名匹配，不会把命令退回到前台的其他智能体。

## 图标与主题

- 使用本机 WorkBuddy 5.8.3 安装包自带的官方图标资源，分别提供 SVG（手机）和高分辨率 PNG（WPF）。
- 素材来源：安装包 `D:\WorkBuddy\resources\app.asar.unpacked\renderer\assets\workbuddy-app-icon-CEG80LXE.svg` 与 `D:\WorkBuddy\Assets\Square150x150Logo.scale-400.png`；检查后将素材复制进仓库，运行时不依赖安装路径。
- WorkBuddy 选中态沿用官方青绿识别色并使用浅绿渐变；手机页面给粒子场添加柔和青绿渐变光晕，发送按钮使用绿色渐变。
- 不改粒子运动、布朗扰动、形变算法、触摸采样或粒子布局；颜色只由外层主题控制。
- 不修改应用主图标或 GPT / Claude 品牌素材。

## 电脑端与手机端

- WPF 目标按钮由 `AgentCatalog.All` 生成，保持同高、同宽和同一校准表单。
- 手机按钮由同一目录生成，三个按钮等宽；连接状态和输入提示随选中目标更新。
- WorkBuddy 主题使用浅色页面与官方青绿点缀，不改变 GPT 蓝色和 Claude 深色暖橙主题。
- 手机请求显式发送 `agentId=workbuddy`；快捷切换不改变已进入队列的目标。

## 验证范围

- [x] Release 构建无警告；两个图标素材复制到构建输出。
- [ ] 用旧版配置验证 GPT / Claude 校准点保留，并验证 WorkBuddy 配置保存重载。
- [x] 手机菜单、WPF 按钮和请求目标均由 `AgentCatalog` 生成或读取；运行时真机页面尚未验证。
- [x] WorkBuddy 进程名已从运行中的应用确认为 `WorkBuddy`，目录匹配仅接受该目标进程。
- [ ] 在可见的 WorkBuddy 窗口重新校准三个点，实际验证发送和滚动。完成前不宣称输入链路通过用户验收。

## 实施结果

- Release 构建成功，0 警告、0 错误。输出到临时目录，避免覆盖仍在运行的旧版本。
- 工作区原先只有已提交版本；当前改动包括 agent 目录、独立配置、WPF/手机选择器、WorkBuddy 主题、图标素材和文档。
- WorkBuddy 窗口实际点击、发送和滚动仍需用户重新校准后验收。
