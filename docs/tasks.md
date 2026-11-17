# 任务清单与 GitHub 索引

仓库：[cancan7777777/black-and-white](https://github.com/cancan7777777/black-and-white)（公开）。

任务元数据见 [`.github/project-tasks.json`](../.github/project-tasks.json)，包含每项任务的目标、依赖、交付与验收标准。GitHub Issues 为任务状态入口，本文件保留本地索引。

**本次仅执行 T00 建项；其他任务仅创建，不启动实现。** T00 完成不能代替 T01–T14 的澄清、实现和验收。

## 里程碑

| 编号 | 目标 | 完成门槛 |
|---|---|---|
| [M0](https://github.com/cancan7777777/black-and-white/milestone/1) | 需求与基线 | 建项、规则、硬件环境、键位/状态/接口均明确 |
| [M1](https://github.com/cancan7777777/black-and-white/milestone/2) | 下位机基础 | 点阵/键盘/规则/UART 通过基础用例 |
| [M2](https://github.com/cancan7777777/black-and-white/milestone/3) | 上位机与基础联调 | 双向落子、串口配置和棋盘一致性通过 |
| [M3](https://github.com/cancan7777777/black-and-white/milestone/4) | 进阶与记录 | 双端控制、闯关/计时/作弊、历史与排行通过 |
| [M4](https://github.com/cancan7777777/black-and-white/milestone/5) | 完整验收 | 全部 B/A/C 要求有真实验收和交付证据 |
| [M5](https://github.com/cancan7777777/black-and-white/milestone/6) | 可选 AI 扩展 | 在 M4 完成后另行启动，不作为课程必做门槛 |

## 任务依赖与状态

| 任务 | 内容 | 里程碑 | 前置 | 当前状态 | GitHub |
|---|---|---|---|---|---|
| T00 | 需求、方案、GitHub 与留痕基线 | M0 | 无 | 已完成（仅规划建项） | [#1](https://github.com/cancan7777777/black-and-white/issues/1) |
| T01 | 规则：终局、计分、188、连败、计时 | M0 | T00 | 待确认 | [#2](https://github.com/cancan7777777/black-and-white/issues/2) |
| T02 | 硬件、供电、引脚和软件环境 | M0 | T00 | 未开始 | [#3](https://github.com/cancan7777777/black-and-white/issues/3) |
| T03 | 键位、状态、数据和串口协议 | M0 | T01、T02 | 未开始 | [#4](https://github.com/cancan7777777/black-and-white/issues/4) |
| T04 | 点阵驱动与棋盘映射 | M1 | T02、T03 | 未开始 | [#5](https://github.com/cancan7777777/black-and-white/issues/5) |
| T05 | 键盘输入与实体控制入口 | M1 | T02、T03 | 未开始 | [#6](https://github.com/cancan7777777/black-and-white/issues/6) |
| T06 | 黑白棋规则与基础状态 | M1 | T01、T03 | 未开始 | [#7](https://github.com/cancan7777777/black-and-white/issues/7) |
| T07 | 下位机 UART、应答、状态上传 | M1 | T03、T06 | 未开始 | [#8](https://github.com/cancan7777777/black-and-white/issues/8) |
| T08 | LabVIEW 棋盘、串口与落子 | M2 | T02、T03 | 未开始 | [#9](https://github.com/cancan7777777/black-and-white/issues/9) |
| T09 | 双端基础联调与一致性 | M2 | T04–T08 | 未开始 | [#10](https://github.com/cancan7777777/black-and-white/issues/10) |
| T10 | 下位机模式、控制、计时与编辑 | M3 | T01、T03–T07 | 未开始 | [#11](https://github.com/cancan7777777/black-and-white/issues/11) |
| T11 | 上位机模式、得分与编辑 | M3 | T09、T10 | 未开始 | [#12](https://github.com/cancan7777777/black-and-white/issues/12) |
| T12 | 保存棋局与历史回看 | M3 | T09、T11 | 未开始 | [#13](https://github.com/cancan7777777/black-and-white/issues/13) |
| T13 | 分模式排行、记录与导出 | M3 | T01、T11、T12 | 未开始 | [#14](https://github.com/cancan7777777/black-and-white/issues/14) |
| T14 | 完整验收、稳定性、报告与演示 | M4 | T09–T13 | 未开始 | [#15](https://github.com/cancan7777777/black-and-white/issues/15) |
| T15 | 可选：AI 版本/许可/规则兼容 | M5 | T14 | 可选未开始 | [#16](https://github.com/cancan7777777/black-and-white/issues/16) |
| T16 | 可选：桥接、推荐、人机对弈 | M5 | T15 | 可选未开始 | [#17](https://github.com/cancan7777777/black-and-white/issues/17) |
| T17 | 可选研究：分析、复盘与胜率校准 | M5 | T16 | 可选未开始 | [#18](https://github.com/cancan7777777/black-and-white/issues/18) |
| T18 | LabVIEW 路线可行性、硬件对应机制与课时化制作方案 | M0 | T00 | 方案待确认 | [#19](https://github.com/cancan7777777/black-and-white/issues/19) |

## 每个任务的共同完成条件

1. 满足该任务的业务验收，不以“建好 Issue”当作实现完成。
2. 有 journal 记录授权、步骤、验证、结果和遗留。
3. 有引用 Issue 的 Git 提交并推送，核对远端；实现任务有关联 PR。
4. 有可复核证据；若受阻则保持未完成并记录原因。

优先级：P0 为课程必做与前置澄清，P2 为可选 AI，P3 为可选研究。尚未提供截止时间和团队分工，不编造日期和负责人。

## GitHub Projects 看板

当前账号 API token 缺少 Projects 权限，`gh project list` 返回缺少 `read:project`。本次以公开仓库、Issues、里程碑和本地任务表组织工作；**未声称创建了 Projects 看板**。如后续需要看板，需具备相应 Projects 读写权限后再创建/导入现有 Issues，本次不触发账号重新授权流程。
