# 实体键盘落子到 LabVIEW 的同步核查

- 关联 [#10](https://github.com/cancan7777777/black-and-white/issues/10)。用户询问当前连线和实体棋局同步能力。本轮核对代码、协议、引脚和离线行为，保留实板验收状态。
- 基于进阶分支的 `f2f3a8e`，使用 `task/10-stm32-labview-sync-audit` 记录。既有用户改动保留在工作区。
- 核查 STM32 USART1 的 PA9 TX、PA10 RX 与 115200 8N1、无流控。用已安装的编译器，离线验证实体键盘逻辑是否产生序号 0 主动上报，以及 LabVIEW 所用的 C# 会话是否接受这条帧。

## 核查与验证

- `main.c` 读取矩阵键盘后调用 `AppBasic_HandleKey`。按键 `5` 合法落子后，`AppBasic_RequestMove` 更新权威棋盘、标记点阵重绘和主动广播。`AppBasic_Tick` 发现 TX 可用时排入完整棋盘，进阶模式使用 `@GAME,0`。`usart.c` 和 STM32F103C8 [数据手册](https://www.st.com/resource/en/datasheet/stm32f103c8.pdf) 的默认 PA9/PA10 方向一致。
- `HostSession` 连接时发 `@SYNC` 读取当前棋盘。接收主动帧时按 CRC、棋子计数、修订号验证，接受序号 0，再由 `OthelloPanel.Render` 重绘棋盘。上位机无需先发落子请求或点刷新。串口接收的是完整棋盘快照，重连可重新同步当前盘面。
- 对生产版 `app_basic.c`、`ot_match.c`、`ot_protocol.c`、`othello.c` 使用 MSVC x86 编译本地临时检查，捕获其发送帧。确认初始坐标第 3 行第 4 列一次合法 `5` 按键更新到版本 2，TX 暂忙时暂存广播，恢复时发送一次，重复按同一格不产生新帧。C# 检查第一次编译因旧 `csc` 处理混合斜杠失败，改用 `Join-Path` 的绝对路径后通过。
- 把生产 C 代码生成的帧送进上位机真实 `HostSession` 的接收分帧逻辑。`PASS keypad -> unsolicited GAME,0 -> HostSession`。显示状态到版本 2，黑 4、白 1，轮白，期间主机只发过首次 `@SYNC`。
- 实物验收使用 3.3 V TTL USB-UART。PA9 接转换器 RXD，PA10 接 TXD，GND 共地。LabVIEW 选「真实串口」、实际 COM 号、115200、8 数据位、无校验、1 停止位。开局第 3 行第 4 列按 `5` 后应看到实体棋盘和上位机一致，通信日志应见 `RX @GAME,0,...`。本轮未连接板卡，真实 UART、键盘和实体点阵仍在 #10 待验收。
