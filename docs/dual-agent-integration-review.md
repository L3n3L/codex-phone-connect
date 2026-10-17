# 双智能体对接复盘与执行方案

更新时间：2026-10-17

## 1. 本轮目标

在现有 Codex Phone Connect 的局域网手机遥控链路上，增加两个桌面目标的选择入口：

- GPT / Codex Desktop
- Claude Desktop

本轮正式接入两个目标：GPT / Codex Desktop 与 Claude Desktop。扩展性只体现在公共边界清晰，不提前引入插件系统、动态加载或第三个适配器。

## 2. 已确认的设计边界

### 手机粒子样本冻结

`prototype/phone-scroll.html` 是已经验证过的触摸粒子样本。本阶段将它视为不可修改的核心资产：

- 不修改粒子生成、布朗运动、形变和回弹逻辑。
- 不修改触摸事件、滑动阈值、滚动反向和请求合并逻辑。
- 不修改现有粒子的颜色、整体渐变和动画节奏。
- 不把 GPT/Claude 判断写进粒子算法。

后续手机端新增智能体选择时，只能通过外层组合或独立页面接入，保证已经验证过的交互不会被新功能回归破坏。

### 视觉方向

电脑端设置窗口保持现有白色 Codex Bridge 风格：白色背景、浅灰面板、细边框、紧凑布局。

手机端保留当前样本中已经认可的两类色彩语言：

- GPT：中间粒子团的蓝色渐变，保持轻盈、冷静的感觉。
- Claude：独立外壳使用非常轻的暖橙渐变，作为 Claude 模式的识别提示；不覆盖或改写冻结粒子样本的颜色和动画。

## 3. 品牌图标方案

不再手工绘制近似图形，统一使用本地 SVG 资产，运行时不依赖外网：

- GPT：OpenAI Blossom 符号。使用前遵守 [OpenAI Design Guidelines](https://openai.com/brand/) 的比例、留白和商标使用要求。
- Claude：Claude 官方站点提供的 SVG favicon，来源为 [claude.com/favicon.svg](https://claude.com/favicon.svg)。

图标文件位于 `assets/brand/`，UI 只引用文件，不把 SVG 路径复制到多个页面中。以后替换品牌资产只需要替换资源文件，不需要改 WPF 或手机页面逻辑。

## 4. 目标架构

```text
AgentDefinition
  ├─ id: gpt | claude
  ├─ displayName
  ├─ windowMatcher
  ├─ brandAsset
  └─ visualMode

AgentProfile
  ├─ inputPoint
  ├─ sendPoint
  ├─ scrollPoint
  └─ runtimeWindowHandle（只在运行时保存，不落盘）

AgentSelectionState
  ├─ activeAgent
  └─ selectionRevision

UI / HTTP 请求
  └─ 在进入队列时快照 agentId
       └─ BridgeJobQueue
            └─ AgentInputAdapter
                 └─ 对应窗口 + 对应校准坐标
```

核心原则：

1. **定义与配置分离**：GPT/Claude 的名称、图标、窗口匹配方式放在定义层；用户校准坐标放在配置层。
2. **请求锁定目标**：手机切换智能体后，新请求使用新目标；已经入队的请求继续使用入队时快照的目标，避免切换造成竞态。
3. **全局操作串行化**：鼠标、键盘和剪贴板仍然只有一个队列，避免两个桌面窗口同时抢焦点。
4. **运行时句柄不持久化**：窗口句柄重启后会变化，配置文件只保存窗口匹配信息和归一化坐标。
5. **粒子交互独立**：手机粒子场只负责触摸反馈和滚动增量，不知道当前是 GPT 还是 Claude。

当前实现对应关系：

- `AgentCatalog`：只登记 GPT 和 Claude 的显示名、进程名、品牌资源。
- `BridgeSettings`：分别保存 `Gpt` 与 `Claude` 的三点归一化坐标，并迁移旧版单目标配置。
- `BridgeWebServer`：手机切换通过 `/api/control/agent`，发送与滚动请求在进入队列前解析并快照 `agentId`。
- `BridgeJobQueue`：仍然只有一个串行队列，确保鼠标、键盘、剪贴板不会跨窗口并发争抢。
- `AgentInputAdapter`：按目标查找窗口、激活窗口、点击归一化坐标、粘贴发送、注入滚轮。
- `PhoneAgentShell`：只叠加目标选择器，不修改冻结的 `prototype/phone-scroll.html`。

## 5. 界面结构

### 电脑端

在“设置点击位置”标题下增加 GPT / Claude 两个目标按钮。选中目标后，下面仍然只显示三项通用校准：

- 输入框
- 发送按钮
- 滚动区域

状态只显示“已设置 / 未设置”，不重复写“GPT 设置”或“Claude 设置”。用户通过上方已选图标和软件名称判断当前目标。

### 手机端

手机端的智能体选择入口是外层控件，负责：

- 显示真实品牌图标和名称。
- 发送请求时带上 `agentId`。
- 切换 Claude 时选择对应目标和外层主题。

冻结的粒子场不承担模式切换、不承担目标选择，也不承担网络请求，这样可以避免样本代码继续膨胀。

## 6. 执行顺序

### 已开始

- [x] 建立本复盘文档。
- [x] 下载并保存 GPT/OpenAI 与 Claude 的 SVG 资产。
- [x] 样式样本改为引用本地真实图标。
- [x] 删除样本中重复的目标状态文字。
- [x] WPF 设置窗口增加 GPT / Claude 目标选择，并显示对应品牌图标。
- [x] 校准配置按目标分组保存，旧版单 Codex 配置自动迁移到 GPT。
- [x] HTTP 请求、队列命令和执行日志增加目标快照。
- [x] 手机正式服务叠加目标选择器，静态粒子触摸页保持原文件不变。
- [x] 增加 GPT / Claude 窗口进程匹配和独立三点校准路径。
- [x] 完成 `dotnet build --no-restore` 编译验证：0 警告、0 错误。

### 下一步

- [ ] 在真实 Claude Desktop 窗口上完成三点校准并验证输入、发送和滚动。
- [ ] 在手机上切换 GPT / Claude，验证切换后新请求路由正确。
- [ ] 验证“切换前已入队、切换后新请求”的串行执行和日志可追踪性。
- [ ] 发布包验收：品牌 PNG/SVG、手机页和日志目录完整存在。

## 7. 验收标准

- 手机粒子样本的触摸、形变、颜色和动画与当前验证版本一致。
- GPT 与 Claude 的图标来自本地 SVG，不使用手工近似图形或 emoji。
- 设置窗口只显示当前目标对应的通用三项校准，不重复显示“GPT 设置”等文字。
- GPT 与 Claude 的坐标互不覆盖。
- 手机切换目标时，已入队任务不会被改投到另一个窗口。
- WPF、HTTP、队列和手机页面不出现重复的目标判断逻辑。

## 8. 当前不做的事情

- 不读取 Codex 或 Claude 的内部会话数据。
- 不改动 `prototype/phone-scroll.html`。
- 不把 Claude Desktop 当成终端处理。
- 不在手机端渲染聊天记录。
- 不引入第三方 UI 框架或复杂状态管理库。
- 当前只实现 GPT 和 Claude；新增目标时需要实际开发对应窗口匹配和输入策略，不承诺仅改配置即可支持。

