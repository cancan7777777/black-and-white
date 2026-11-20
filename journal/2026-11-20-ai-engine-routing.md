# AI 两个入口的引擎核查

- 用户询问两个 AI 模式的区别，以及是否应该共用 EN。沿用 [#17](https://github.com/cancan7777777/black-and-white/issues/17) 和分支 `task/11-challenge-ai-keys`，起点 `1e41b3d`。
- 「AI 对弈（STM32）」及实体 S15 进入固件的独立 AI 模式，由 `ot_match_tick` 运行内置走白算法。离线模拟运行同一段固件逻辑。「AI 对弈（EN）」由 LabVIEW 启动本机 Egaroucid，连接真机时经串口提交白棋坐标，由 STM32 校验并显示。
- 当初把 S15 设计成脱机可用的独立入口，因此有两套走白实现。若要两处使用同一棋力，可用 S15 通知 LabVIEW 启动 EN，LabVIEW 选单与按键共用同一模式。实板使用 EN 时需要 LabVIEW 与串口连接。是否保留离线时的内置 AI 属于行为选择。本次只核查走棋路径。
