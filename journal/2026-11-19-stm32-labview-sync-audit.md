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

## 同日 · LabVIEW 实物操作步骤核对

- 用户询问接入硬件后的 LabVIEW 操作。沿用 #10，按现有 `OthelloPanel.cs` 确认连接、落子与退出按钮名称。使用已烧录对应固件的板卡，在 `Othello_Basic.lvproj` 中打开 `Main.vi` 并点运行箭头。已连模拟时先点「断开」，将方式切到「真实串口」。插入 USB-UART 后在 Windows 端口列表确认实际 COM 号，本机同时列出多个端口，未推定哪个属于此板。
- 端口选择实际 COM，设置 115200、8 数据位、`None` 校验、1 停止位，点「连接」。首次读取收到 `RX @GAME,...` 且棋盘呈现下位机当前盘面才算同步成功。若下位机固件较旧，兼容帧为 `@STATE`。
- 新开局时，用实体键盘在第 3 行第 4 列按 `5`，预期 LabVIEW 显示黑 4 白 1、轮到白方。再在 LabVIEW 选第 3 行第 3 列（C3），点「确认落子」，预期两端黑 3 白 3、轮到黑方。点「断开」释放串口，点「结束上位机」停止 VI。上述为实物验收的预期结果，未对板卡进行操作。
- 若界面报告连接成功但棋盘仍为空，可点「刷新棋盘」看通信日志是否有 `RX` 响应。仅有 `TX` 时核对实际 COM、串口参数、板卡供电和固件、TX/RX 交叉、GND 共地。若串口提示被占用，关闭占用该端口的程序后重试。

## 同日 · 烧录入口核查

- 用户询问 HEX 烧录与 LabVIEW 的分工。沿用 #10，核对当前 CubeMX 工程下的 `firmware/Othello_Basic/MDK-ARM/Othello_Basic.uvprojx`。目标 STM32F103C8，Keil 工程启用 HEX 输出，烧录接口配置引用 ST-LINK 驱动。LabVIEW 在固件启动后以 USART1 的 COM 口进入实物对局。
- 运行 `tools/build-keil.ps1`，当前 `main.c`、`app_basic.c`、`ot_match.c`、`ot_protocol.c` 均编译入工程，结果 0 错误、0 警告。重新生成 `firmware/Othello_Basic/MDK-ARM/Othello_Basic/Othello_Basic.hex`，44 KB 级文件，核对 Intel HEX 扩展线性地址记录为 `0x0800`，末尾有 EOF。
- 用现有 Keil + ST-LINK（SWD）可从工程执行 Flash → Download。也可在支持 Intel HEX 的烧录软件中直接选择同一个 HEX，例如 STM32CubeProgrammer。本轮只在本机编译，没有写入板卡。
