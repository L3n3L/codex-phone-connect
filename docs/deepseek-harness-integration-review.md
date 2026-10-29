# DeepSeek Harness（DSH）接入复盘

更新时间：2026-10-30

## 目标

在 GPT、Claude 和 WorkBuddy 之外，加入 DeepSeek Harness 桌面窗口作为第四个可选目标。复用现有的选择、显式 `agentId` 路由、串行输入队列、窗口识别和三点归一化校准；手机不读取 DSH 的会话或回答。

## 数据流

```text
手机选择 DSH
    ↓ 每次发送 / 滚动显式携带 agentId=dsh
BridgeWebServer 校验并锁定目标
    ↓
BridgeJobQueue 串行处理桌面输入
    ↓
AgentInputAdapter 查找 DeepSeek Harness 窗口
    ↓
使用该目标自己的输入框 / 发送按钮 / 滚动区域比例坐标
```

输入继续使用现有方式：激活目标窗口、点击输入框、粘贴文本、点击发送位置；滚动只向校准位置注入滚轮。没有调用 DeepSeek API，也不访问 DSH 内部数据。

## 窗口识别与配置

- 新增 `AgentId.DeepSeekHarness`，放在现有枚举末尾，保持 GPT、Claude、WorkBuddy 的历史枚举数值不变。
- 目标 wire ID 为 `dsh`，别名为 `deepseek-harness`。
- 快捷方式目标为 `E:\DeepSeek Harness\DeepSeek Harness.exe`，文件产品名为 DeepSeek Harness；目录使用 `DeepSeek Harness` 作为候选进程名。
- 增加独立 `DeepSeekHarness` 校准配置。旧版设置缺少此属性时初始化为空，不重置其他 agent 的坐标。
- WPF 和手机选择按钮都由 `AgentCatalog` 生成；桌面和手机图标资源分别声明，支持同一 PNG 同时供两端使用。
- 实际校准时仅接受进程名匹配的窗口点击，窗口句柄只在运行时缓存；输入坐标按窗口客户区比例存储。

## 品牌资源与样式

- 复用本机安装包 `E:\DeepSeek Harness\resources\icon.png` 中的 DeepSeek 鲸鱼图标，复制到仓库 `assets/brand/deepseek-harness.png`；打包后不依赖用户本机的 DSH 安装目录。
- 电脑端和手机选择按钮显示 `DSH`，手机状态、输入和滑动提示也使用 `DSH`。
- DSH 手机主题采用浅色中性灰渐变，粒子场和发送按钮呼应鲸鱼图标；只在 `PhoneAgentShell` 外层改主题，不修改 `prototype/phone-scroll.html` 的粒子运动、触摸采样和形变逻辑。

## 验证状态

- [x] DSH 安装包图标已加入仓库，构建项目会复制品牌 PNG 资源。
- [x] DSH 使用独立 profile，并通过显式目标路由进入原有串行队列。
- [x] Release 构建无警告，WorkBuddy 与 DSH 两个图标资源均进入输出目录。
- [ ] 在实际运行的 DSH 窗口确认顶层窗口进程名，并验证校准点击可绑定。
- [ ] 在 DSH 中重新校准输入、发送、滚动三个位置，验证文本发送与滚动。

本机当前没有正在运行的 DSH 窗口，因此实际 `ProcessName` 和可见顶层窗口归属仍待确认。真实窗口行为尚未验收前，不宣称输入链路已通过测试。若 DSH 版本更换可见窗口对应进程名，应仅更新 `AgentCatalog` 中的匹配定义。
