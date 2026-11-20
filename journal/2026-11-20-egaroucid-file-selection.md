# EN 无有效坐标的文件选择排查

- 用户报告第一手黑棋确认后显示「Egaroucid 没有返回有效坐标」。沿用 AI [#17](https://github.com/cancan7777777/black-and-white/issues/17)，起点 `c5260d1`。
- 用户提供的 `@GAME5` 确认版本 6、白方回合、黑 4 白 1 和 EN 模式。当前 `bin/EgaroucidPath.txt` 指向 `PanelTests.exe`，即上位机测试程序。
- 核对用户本机已有的 Egaroucid Console 7.8.2 AMD AVX512 程序。用已安装的 x86 上位机组件解析用户提供的原帧，实际引擎给出合法白棋 C3。已修正并读取复核本地路径配置。
- 保留原 LabVIEW 会话和棋局。当前版本按「刷新棋盘」后会对同一棋局重新读引擎设置并计算。只调整忽略的本地配置，源码和 VI 保留原状。
