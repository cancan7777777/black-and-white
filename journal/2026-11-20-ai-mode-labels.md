# 统一 LabVIEW 的两个 AI 对弈入口

- 用户要求将现有两个入口统一命名并测试。使用「AI 对弈（STM32）」和「AI 对弈（EN）」，EN 沿用本机 Egaroucid。沿用上位机 [#12](https://github.com/cancan7777777/black-and-white/issues/12) 和分支 `task/11-challenge-ai-keys`，起点 `20eb885`。
- 更新新局菜单、对局状态和 STM32 排行标签，放宽模式下拉框。保留模式编号、引擎路径、固件和旧档案。通过嵌入式控件回归、真实引擎对弈和最小窗口预览验收。

## 验证

- x86 组件完整构建，439 项上位机检查通过。测试覆盖两个新名称、最小窗口中较长的选项完整显示、STM32 执白应手以及真实 Egaroucid 52 手对弈。原生棋规测试通过。
- 用真实 WinForms 控件以 1000×660 实际连接、切换和渲染两种模式。两个名称完整显示，EN 模式正确切换为「AI 等级」。截图 `build/labview/ai-mode-labels/preview/mode-comparison.png`。
- 新版 `Othello.Basic.dll` 和测试程序已写入标准目录并核对哈希。正在运行的 LabVIEW 占用原生模拟库，该库哈希仍匹配上一轮已验证版本，源码无改动。直接对安装目录的新面板及现有模拟库再次执行按键与应手检查，通过。保留当前 LabVIEW 会话和用户文件，完整退出并重开后显示新菜单。
