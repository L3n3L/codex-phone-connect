# Implementation Plan

- [x] 1. 固化需求和非目标
  - 明确手机不显示聊天记录，只发送输入和滚动控制。
  - 明确发送与滚动共用串行队列。
  - _Requirement: 1-8_

- [x] 2. 更新对接复盘和设计文档
  - 记录当前 app-server、UIA 和本地窗口输入链路的边界。
  - 记录手机遥控滚动的数据流、契约和风险。
  - _Requirement: 1-8_

- [x] 3. 增加滚动配置和聊天区域校准
  - 增加 `ScrollPoint` 和配置版本迁移。
  - 在 WPF 窗口增加“聊天区域”校准项。
  - _Requirement: 4_

- [x] 4. 增加滚轮输入适配器
  - 将手机 `deltaY` 转换为受限的 Windows 滚轮量。
  - 激活 Codex、定位校准点并注入滚轮。
  - _Requirement: 1, 2, 7_

- [x] 5. 扩展单线程命令队列和 HTTP 接口
  - 增加 `/api/control/scroll`。
  - 让发送和滚动不能并行注入输入事件。
  - _Requirement: 1, 5, 6_

- [x] 6. 调整手机遥控页面
  - 添加触摸滑动识别。
  - 保留底部输入框和发送按钮。
  - _Requirement: 1, 2, 3, 8_

- [x] 7. 编译和本机链路验收
  - 执行构建和静态检查。
  - 已手动验证上滑、下滑、滚动增量注入和发送回归。
  - _Requirement: 1-7_
