# LabVIEW 上位机正式测试验收

- 用户授权进行上位机验收，沿用 [#15](https://github.com/cancan7777777/black-and-white/issues/15) 和分支 `task/15-labview-acceptance`。验收起点 `2fee3de`，参考[上轮功能测试](2026-11-19-labview-acceptance.md)和[任务书逐项核查](2026-11-20-taskbook-audit.md)。
- 复跑完整构建与自动回归，验证 LabVIEW 原生 VI 的可执行性和可独立验收的上位机功能。以隔离目录保存实际离线棋局进行回放、排行与导出验收，核对本机既有成绩。实物串口只在找到可识别的设备时测试；以真实观测记录对应条款状态。
- 当前发现 LabVIEW 正在运行。只操作本项目中处于空闲的 VI，保留原生工程及原有未提交修改。首轮 PnP 检查只见八个蓝牙串口，未找到可确认属于 STM32 的适配器。

## 执行记录

- 运行 `tools/build-labview.ps1 -OutputDirectory build/labview/formal-acceptance/bin -EgaroucidExecutable <本机控制台程序>`，x86 上位机及原生模拟库重新构建成功。原生棋规、四关结算和点阵动画检查通过。429 项上位机自动检查通过。使用本机 Egaroucid 完成 52 手，终局黑 23、白 33。
- 通过 LabVIEW ActiveX 查询原 `Main.vi` 为顶层运行状态。创建同内容的验收副本，初次同步调用会阻塞至 VI 结束，在指定副本路径确认状态后停止副本，单独复跑异步启动。独立复测观测到运行状态 `1 → 2 → 1`，即空闲、运行、空闲；原 VI 仍为 `2`。原文件和测试副本哈希一致。[NI 状态枚举说明](https://www.ni.com/docs/en-US/bundle/labview-api-ref/page/properties-and-methods/activex/enumerations/exec-state.html)
- 运行隔离的 `RecordEvidence.exe`，用实际固件模拟和上位机存档模块保存 15 份完整棋局。普通模式 5 条、闯关模式 5 条、计时模式 5 条。重新读取全部 XML 与各模式排行，三个 CSV 均包含表头和 5 条成绩。ZIP 复读校验包含 15 份 XML 和 3 个 CSV。
- 用已安装的上位机组件只读核对 Windows 用户存档。正式排行已有普通-离线 1 条、闯关-实体 1 条，其余组合 0 条。验收样本保存在隔离目录，生产档案未写入。

## 正式判定

| 验收项 | 结果 |
|---|---|
| LabVIEW VI 运行 | 原生运行检查通过。独立副本启动和停止，原会话保留。 |
| B02–B04 棋盘与棋规 | 离线通过。包括四子开局、翻子、非法落子及终局。 |
| B05–B07 串口设置和棋盘同步 | 软件通过。模拟串口覆盖五项参数、下位机广播、超时、迟到帧、刷新、重连和板卡重启恢复。实体通信与时延未验。 |
| A05 对局操作与得分 | 离线通过。开始、暂停、继续、结束、重开、模式、编辑、计时及四关计分通过。 |
| A06–A07 棋局与排行榜 | 离线通过。回放、分模式及来源、每模式 5 条成绩和 CSV 均有隔离证据。本机现有排行 2 条。 |
| C01–C03 界面与演示 | 1000×660 布局与离线连续运行通过。实体键盘、点阵和独立演示待验。实体光标与点阵动画的逐帧同步是已知功能缺口。 |
| X01 Egaroucid | 真实引擎离线对弈通过。 |

本地验收包 `build/labview/formal-acceptance/Othello_Offline_Acceptance_2026-11-20.zip`，包含 CSV、XML 和查看说明。结论为上位机软件验收通过，STM32 实机联调未完成，验收 Issue 保持开放。
