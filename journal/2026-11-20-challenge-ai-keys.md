# S16 闯关开关与 S15 实体 AI 对弈

- 用户授权 S16 开启或退出闯关、WIN／OVER 自动退出闯关，另增实体 AI 对弈键。沿用 [#11](https://github.com/cancan7777777/black-and-white/issues/11)、[AI #17](https://github.com/cancan7777777/black-and-white/issues/17) 及分支 `task/11-challenge-ai-keys`，起点 `ab00272`。
- S16 在普通和闯关之间开关。WIN／OVER 后退出闯关规则，保留最终盘面、计分和爱心／滚屏。按 S4 开始普通新局，S16 可直接开一轮新的四关闯关。S15 开关独立 STM32 AI 对弈，黑方玩家、白方电脑。用原空闲 S11 保留实体计时入口。LabVIEW 保留独立的 Egaroucid 模式。
- 验收覆盖实体按键处理、自动开局与结算、协议向下兼容、LabVIEW 和离线模拟。更新可用 HEX、运行回归并同步 GitHub。

## 实现与验证

- 固件保留已完成棋局的闯关模式字段，用活动模式判断自动退出及下次按键。在 WIN／OVER 状态下仍显示原终局，爱心和滚屏由原渲染逻辑接续。S4 或 S14 开普通新局，S16 开新的四关。S15 的 AI 对弈不使用关卡系数，S11 保留计时。
- 新协商使用 `@SYNC4`／`@GAME4` 识别独立 STM32 AI 和已退出的闯关，LabVIEW 为 STM32 AI 提供模式及独立排行。旧协商格式可继续读取棋盘；旧上位机把独立 AI 局显示为普通模式，白方仍由 STM32 控制。
- 原生测试覆盖 S16、S15、S11、S6 和暂停键的处理及主动上报，已验证两连败、四关分数不足和 188 分达标自动退出，以及保留分数、进入普通新局。现有爱心测试通过。
- 完整 LabVIEW x86 回归 437 项通过，包含独立 AI 应手、协议回退、实体按键事件模拟、四类模式的保存/排行和真实 Egaroucid 52 手对弈。将本轮组件复制到标准 `labview/Othello_Basic/bin` 目录并核对哈希。本机引擎设置保持已配置状态。
- Keil 编译 0 错误、0 警告。已校验新版 `build/Othello_Challenge_AI_Keys.hex` 的 1035 条记录及复制哈希，SHA-256 为 `D4EDFEDB52B5F2C78AD9B1EBDD49FF482C5FB3E95027BC7A731B404DE95BDBDC`。实板烧录、按键和 USB 串口联动待验。
