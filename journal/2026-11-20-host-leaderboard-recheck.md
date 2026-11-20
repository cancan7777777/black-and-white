# LabVIEW 排行榜、回放及档案恢复复验

- 用户要求继续，随后明确本轮只看上位机，下位机已经完成调试。沿用排行榜 [#14](https://github.com/cancan7777777/black-and-white/issues/14) 与 [PR #30](https://github.com/cancan7777777/black-and-white/pull/30)，起点 `8803aaa`，参考[上位机验收](2026-11-20-host-formal-acceptance.md)和[胜方显示](2026-11-20-winner-labels.md)。
- 用最新版 LabVIEW 上位机组件和离线连接生成独立的验收档案。普通、闯关、计时、AI 对弈各 5 局，另保存一局主动退出。验证结果、排序、回放、CSV 和复制 XML 后恢复。离线验收包只含验证棋局，原有用户档案不参与生成或覆盖。

## 验收记录

- 已安装的 `Othello.Basic.dll` 与 `8803aaa` 的已验证构建 SHA-256 一致。用 32 位 .NET Framework 编译本地验收程序，通过 `HostSession`、`SimulatedTransport` 和 `GameArchive` 运行四种模式；AI 对弈 5 局均用本机 Egaroucid 执白。生成器首次在追加退出样例时给模式指令传了默认 0 秒，未开始新局。用有效的 1 秒参数单独生成退出样例，保留原有 20 局有效档案。
- 用新建的 `ArchiveWindow` 检查界面实际结果列。离线筛选下各模式 5 条，实体筛选下无样例。验证分数、用时、结束时间和稳定顺序，以及终局盘面与界面和 CSV 文案逐条一致。普通 304 帧、闯关 298 帧、计时 12 帧、AI 对弈 191 帧。20 条成绩合计 6 条黑棋胜、11 条白棋胜、3 条平局；另 1 条提前退出在回放标为「未完成」。
- 将 21 份 XML、4 份 CSV 和查看说明打包，解压到另一新目录，用新进程重新读取并复验相同的 20 条排行及回放。CSV 每种模式包含表头及 5 条成绩。复制 XML 可恢复档案；目前没有界面内 CSV 导入功能。
- 本地验收包 `build/labview/leaderboard-acceptance/Othello_LabVIEW_Leaderboard_2026-11-20.zip`，SHA-256 `B57A0134E69C48E1EAF245065F4CB5DA33AB37D8E61D80DB7FE5699BE81A5372`。
