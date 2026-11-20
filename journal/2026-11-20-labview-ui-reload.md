# LabVIEW 界面重新加载核查

- 用户询问怎样更新已运行的上位机界面。沿用 [#12](https://github.com/cancan7777777/black-and-white/issues/12)，记录到 `task/11-challenge-ai-keys`，起点 `c11a298`。截图仍为旧布局。
- 核对标准目录 `labview/Othello_Basic/bin/Othello.Basic.dll` 与本轮测试通过的构建产物，SHA-256 一致。磁盘上的新版程序集于 11 月 20 日 10:18 构建，当前 LabVIEW 进程从 11 月 17 日开始运行。ActiveX 确认项目的 `Main.vi` 正在运行。
- LabVIEW 会保留已加载的 .NET Framework 程序集。建议先点「结束上位机」，保存工作并完整退出 LabVIEW，再重开项目的 `Main.vi`。运行后用「离线模拟」连接，检查深绿色标题栏、黑白棋数量卡片、AI 设置以及「新局模式」中的 STM32 AI 和 Egaroucid。若界面仍旧，在 View → .NET Assemblies in Memory 核对程序集来源。[NI 重新装载程序集说明](https://www.ni.com/docs/en-US/bundle/labview/page/loading-changes-to-a-net-assembly-in-memory.html)
- 此轮只读核查，保留已运行的 VI 和用户未提交修改。重启后的实际界面待用户操作后确认。
