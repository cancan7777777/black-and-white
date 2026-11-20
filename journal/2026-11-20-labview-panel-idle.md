# LabVIEW 前面板按钮无响应

- 用户报告点击按钮显示 `delta（0,0）`。沿用上位机 [#12](https://github.com/cancan7777777/black-and-white/issues/12)，核查起点为 `646c7b9`。
- ActiveX 读取项目的 `Main.vi`，`EXEC_STATE=1` 表示 VI 空闲，符合前面板处于编辑状态的症状。启动原 VI 后复查为 `EXEC_STATE=2`。用户随后确认已恢复。[NI 执行状态说明](https://www.ni.com/docs/en-US/bundle/labview-api-ref/page/properties-and-methods/activex/enumerations/exec-state.html)
- 保留 `Main.vi` 的运行状态及原有未提交修改。再次遇到同样提示时，点击左上角运行箭头，运行后连接离线模拟或实际串口。

## 统一 AI 菜单后的运行说明

- 用户的新截图中，`Main.vi` 前面板显示「请运行 Main.vi」，左上角有可运行的单箭头。点箭头或按 Ctrl+R，再在「离线模拟」下点「连接」。出现开局四子后可选择「AI 对弈 → 设定模式」，在 D3 确认黑棋落子，观察 EN 白棋应手。核查时确认项目的 VI、最新组件和 V3 离线模拟库均在标准目录，保留截图中的现有工作。[NI 快捷键说明](https://www.ni.com/docs/en-US/bundle/labview/page/keyboard-shortcuts.html#execution-shortcuts)
