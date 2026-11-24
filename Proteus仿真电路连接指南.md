# 黑白棋 Proteus 仿真电路连接指南

本指南适用于本项目的 **STM32F103C8、4×4 矩阵键盘、8×8 WS2812 点阵和 USART1**。以本机 **Proteus 8.17 SP2** 的器件库命名为例。先验证 STM32、键盘和点阵，再接串口。上电后按 S4 开局，用方向键移动光标，再按 S11 落子并验证串口同步。

## 1. 器件清单

| 位号 | Proteus 中搜索或选择 | 参数和作用 |
|---|---|---|
| `U1` | `STM32F103C8` | 主控，执行棋规、扫描键盘、产生点阵数据、与上位机交换状态。选具有 VSM 模型和 `Program File` 属性的器件。原型封装为 STM32F103C8T6 LQFP48，仿真按 `PAx/PBx` 等信号名接线。 |
| `X1` | `CRYSTAL` | 8 MHz HSE 晶体，固件用 PLL ×9 得到 72 MHz。 |
| `C1`、`C2` | `CAP` | 晶振两端各接一只到地，参考 20 pF。实体值按所用晶体的负载电容和板上杂散电容选取。 |
| `C3`～`C5`、`C6` | `CAP` | 三组数字电源旁路各 100 nF，另加一只 4.7～10 µF 作 3.3 V 电源储能。 |
| `C7`、`C8` | `CAP` | 100 nF 和 1 µF 并联，给模拟电源 `VDDA` 去耦。 |
| `R1`、`R2` | `RES` | 各 10 kΩ，分别给 `BOOT0` 和 `PB2/BOOT1` 下拉，启动时选择用户 Flash。 |
| `S_RST`、`C9` | `BUTTON`、`CAP` | 复位按键与 100 nF 电容，分别从 `NRST` 接地。按下复位，释放恢复运行。 |
| `S1`～`S16` | `BUTTON` | 16 个常开瞬时按键，组成 4×4 键盘。按左到右、上到下标注 `1`～`16`，与实体键盘编号一致。 |
| `LED1` | `WS2812X8X8` | 可寻址彩色 8×8 点阵，接收 64 个像素的串行数据。拆开验证可用一个 `WS2812`，或用八个 `WS2812X8` 级联。 |
| `R3` | `RES` | LED 数据串联电阻，图上可取 330 Ω，放在首像素 `DIN` 前。 |
| `C10` | `CAP-ELEC` | 实体点阵供电入口的储能电容，可从 470～1000 µF、耐压至少 10 V 选取。正端接 `+5V_LED`，负端接地。数据波形仿真可省略。 |
| `VS1`、`VS2` | `VDC`，以及 `Terminals` 的 `POWER`／`GROUND` | 3.3 V 给 MCU，5 V 给点阵，共用 `GND`。`+3V3_MCU` 与 `+5V_LED` 是两条不同网络。 |
| `LA1` 或 `OSC1` | `Virtual Instruments` → `Logic Analyser` 或 `Oscilloscope` | 检查 PA8 点阵帧、PB0～PB15 扫描信号和 PA9 串口位宽。 |
| `VT1` | `Virtual Instruments` → `Virtual Terminal` | 可选串口终端。用来查看或发送 ASCII 帧，按第 6 节先检查 115200 的支持情况。 |
| `P1` | `COMPIM` | 可选 Windows 串口桥，只有需要让 Proteus 和 LabVIEW 联动时才加。 |

器件参考位号用于图纸和调试，`S_RST` 是单独的复位按键。物理引脚、上电和去耦参考 [STM32F103x8/xB 数据手册](https://www.st.com/resource/en/datasheet/stm32f103c8.pdf)及 [ST AN2586](https://www.st.com/resource/en/application_note/an2586-getting-started-with-stm32f10xxx-hardware-development-stmicroelectronics.pdf)。

## 2. STM32 最小系统

| STM32 信号 | LQFP48 针脚 | 接到哪里 |
|---|---:|---|
| `VDD_1`, `VDD_2`, `VDD_3` | 24、36、48 | `+3V3_MCU`，每组附近放一只 100 nF。`C6` 跨接 3.3 V 和地。 |
| `VSS_1`, `VSS_2`, `VSS_3` | 23、35、47 | `GND`。 |
| `VDDA`, `VSSA` | 9、8 | 分别接 `+3V3_MCU` 和 `GND`，`C7`、`C8` 跨接。 |
| `VBAT` | 1 | 无独立后备电池时接 `+3V3_MCU`。 |
| `PD0/OSC_IN`, `PD1/OSC_OUT` | 5、6 | 晶体 `X1` 跨接两端。`C1` 从 `OSC_IN` 到地，`C2` 从 `OSC_OUT` 到地。 |
| `NRST` | 7 | `S_RST` 与 `C9` 各自接到 `GND`，利用芯片内部上拉。可选外接 10 kΩ 上拉到 3.3 V。 |
| `BOOT0` | 44 | 经 `R1` 接地，运行程序时保持低。 |
| `PB2/BOOT1` | 20 | 经 `R2` 接地。本固件不把 PB2 用于对局。 |
| `PA13`, `PA14` | 34、37 | `SWDIO`、`SWCLK`，需要实体调试接口时使用，Proteus 中可预留。 |

**若看不到电源脚**，右键 `U1` → `Edit Properties`，检查可用的 `Hidden Pins` 或电源网络属性，再到 `Design` → `Configure Power Rails` 核对电源分配。把 MCU 的 `VDD/VDDA` 对应到 3.3 V，`VSS/VSSA` 对应到地。Proteus 有可能默认把 `VCC` 和 `VDD` 分在同一供电组，点阵若使用隐藏电源脚，务必把其 5 V 单列。原理图上的电源标签表示网络，实际电压由电源或电源轨配置给出。开发板模型应以板上标注和内部连接为准，避免外接重复晶体或把 5 V 接到 3.3 V 电源脚。[Labcenter 电源轨与隐藏引脚教程](https://www.labcenter.com/downloads/Tutorials.pdf)

## 3. 键盘接线

代码把四行设为开漏输出，分时拉低一行，把四列设为上拉输入。松手时列为高，按下某键时，对应扫描行把该列拉低。每行驻留约 1 ms，扫完 4 行约 4 ms，软件去抖设为 20 ms。使用整块键盘时，按符号的 `R1～R4/C1～C4` 顺序接线。16 只独立按钮的每一只只连接一行和一列，不另接电源或地。

| 网络 | MCU（LQFP48 针脚） | 按钮一侧 |
|---|---|---|
| `KEY_ROW0` | `PB0`（18） | `S1`、`S2`、`S3`、`S4` |
| `KEY_ROW1` | `PB1`（19） | `S5`、`S6`、`S7`、`S8` |
| `KEY_ROW2` | `PB10`（21） | `S9`、`S10`、`S11`、`S12` |
| `KEY_ROW3` | `PB11`（22） | `S13`、`S14`、`S15`、`S16` |
| `KEY_COL0` | `PB12`（25） | `S1`、`S5`、`S9`、`S13` |
| `KEY_COL1` | `PB13`（26） | `S2`、`S6`、`S10`、`S14` |
| `KEY_COL2` | `PB14`（27） | `S3`、`S7`、`S11`、`S15` |
| `KEY_COL3` | `PB15`（28） | `S4`、`S8`、`S12`、`S16` |

下表数字是仿真图上要显示的键帽编号。左上角的 `1` 对应实体 `S1`，右下角的 `16` 对应实体 `S16`。

| 行 | 第 1 列 | 第 2 列 | 第 3 列 | 第 4 列 |
|---|---|---|---|---|
| 1 | **1** 未用 | **2** 上 | **3** 未用 | **4** 开始／下一关 |
| 2 | **5** 左 | **6** 落子／编辑选中格 | **7** 右 | **8** 暂停／继续 |
| 3 | **9** 未用 | **10** 下 | **11** 开启／退出计时 | **12** 结束退出 |
| 4 | **13** 开启／退出编辑 | **14** 重开 | **15** 开启／退出 AI 对弈 | **16** 开启／退出闯关 |

### 将计算器键盘换成 1～16 的键帽

1. 停止仿真，在 `Pick Devices` 搜索 `BUTTON`，将 16 个常开按钮排成四行四列，器件位号设为 `S1`～`S16`。在每个开关旁用 `2D Graphics Text` 放一个独立数字，使图上直接显示 `1`～`16`。[Labcenter 原理图编辑教程](https://www.labcenter.com/downloads/Tutorials.pdf)
2. 每个按钮一端接所在行，另一端接所在列。用相同网络标签把四个行网和四个列网接回 MCU，总共 8 条信号。`S1` 接 `PB0` 和 `PB12`，`S6` 接 `PB1` 和 `PB13`，`S16` 接 `PB11` 和 `PB15`。现有计算器图的列脚从左到右标作 `4、3、2、1`，换装时按新键盘的数字位置接线。新布局最左列的 `1、5、9、13` 接 `PB12`。
3. 使用当前 HEX 运行，按 `2、10、5、7` 验证上、下、左、右。按 `14` 重开后按 `6`，可以验证开局 D3 落子。数字是原理图标注，固件继续按行列交点识别按键。

仿真时一次只按一个键，保持至少约 50 ms 仿真时间，再完全放开后按下一键。若所用模型的列输入在松手时显示未定义，可各加一只 10 kΩ 上拉到 3.3 V，保留固件原有设置。空闲的行是开漏释放状态，用示波器验证行扫描时可临时加上拉观察高低切换。

## 4. RGB 点阵接线与像素方向

```text
STM32 PA8 / TIM1_CH1 (29) ── R3 330Ω ── LED1 DIN/DI
+5V_LED ────────────────────────────── LED1 VDD/5V
GND     ────────────────────────────── LED1 VSS/GND
LED1 DOUT/DO ───────────────────────── 单模块时留空
```

- `WS2812X8X8` 有电源脚时按图接电源。模型隐藏电源脚时用第 2 节检查；若模型只有信号脚且没有电源网络属性，按该模型的数字行为运行，图上另外标注实体模块的 5 V 和共地。`DIN` 朝 MCU，`DOUT` 只给后续像素。
- 当前固件 `APP_LED_SERPENTINE=0`，以直行顺序发送。第 0～7 像素为 A1～H1，第 8～15 像素为 A2～H2，最后一个是 H8。用八条 `WS2812X8` 替代时八条均同向摆放，每条的 `DOUT` 接下一条 `DIN`。要逐像素画图，也按这个顺序接相邻 `DOUT → DIN`。如果整块模型的第二行左右反转，先核对模型像素顺序，再用八条同向级联验证当前固件。
- PA8 是 `TIM1_CH1` 的 PWM/DMA 输出。7200 万计数/秒、分频 `PSC=0`、周期寄存器 `ARR=89`，每位周期约 **1.25 µs**。高电平约 **0.32 µs** 表示 `0`，约 **0.81 µs** 表示 `1`。一帧是 **64×24 位**，颜色顺序 **GRB、最高位先发**，发送后有约 **320 µs 或更长**的低电平。PA8 在两帧间保持低电平，因此用上升沿触发测量。
- 仿真连线可直接检查 PA8 至 `DIN` 的数字信号。实体上 3.3 V 是否达到 5 V 点阵的输入高阈值取决于实际批次。若规格要求 `VIH ≥ 0.7 × VDD`，在 5 V 供电时需至少 3.5 V，3.3 V 直驱没有规格保证。供电从独立 5 V 电源到点阵并与 MCU 共地，按模块最大电流选电源容量，点阵电流不经过 MCU 的 3.3 V 电源脚。[Worldsemi WS2812B-V5/W 数据手册](https://www.world-semi.com/web/userfiles/productfile/WS2812B_V5WDatasheet_V6.1_EN.pdf) · [Worldsemi 不同版本输入规格说明](https://www.world-semi.com/web/index.php?classid=22&id=587&lanstr=en&topclassid=19)

## 5. Proteus 操作步骤

1. 打开 Proteus，选 `New Project`。给工程命名 `Othello_Proteus`，选择新建 `Schematic Design`。PCB 页选 `Do not create a PCB layout`，固件页选 `No Firmware Project`。本项目从 Keil 载入已有固件。
2. 进入 `Schematic Capture`，切到 `Component Mode`，按 **P** 或点 `Pick Devices`。先添加 `STM32F103C8`、`CRYSTAL`、电阻、电容、`BUTTON` 和 `WS2812X8X8`。再在 `Terminals Mode` 放电源、地和标签，在 `Virtual Instruments` 放示波器或逻辑分析仪。
3. 先画并检查第 2 节最小系统，再按第 3 节逐行画键盘，最后连 PA8 和点阵。布线时从引脚端点开始，在目标引脚端点结束。T 形分支检查有接点，避免把交叉线误当成连接。长线用完全同名的网络标签，例如 `KEY_ROW0`，可以点击或高亮网络核查。
4. 双击 `U1`，在 **Program File** 选择 `D:\black&white\build\Othello_Dual_Clock.hex`。本机 Keil 的对应产物为 `D:\black&white\firmware\Othello_Basic\MDK-ARM\Othello_Basic\Othello_Basic.hex`。如果换了电脑或 HEX 尚未生成，打开 `firmware/Othello_Basic/MDK-ARM/Othello_Basic.uvprojx`，在 `Options for Target` → `Output` 确认 `Create HEX File`，点 `Build`，用该目标目录生成的 `Othello_Basic.hex`。在 Proteus 加载 HEX 即可开始仿真，无需通过 ATK-XISP 或 LabVIEW 写入。[Labcenter 固件加载与编译器支持](https://www.labcenter.com/compilers/)
5. `STM32F103C8` 模型的 **OSC Frequency** 设为 **8 MHz**，与 HSE 配置匹配。**72 MHz 是固件 PLL 配置后的 SYSCLK**，不能填在 OSC 输入频率字段。`OSC32 Frequency` 保持默认，本工程没有外部 32.768 kHz 时钟。若换用了名称不同的 VSM 模型，先确认字段描述的是外部晶振还是 CPU 时钟，再用第 7 节的位宽核对结果。
6. 保存 `.pdsprj`，点击左下角 **Run ▶**。点阵先显示播放图标和跑马边框。按 **S4** 开局，中间四子为 D4、E5 青色，E4、D5 橙色，D3 为闪烁橙色光标。用 **S10／S12／S15／S7** 试上下左右，按 S14 重开，再按 **S11** 在 D3 下第一枚黑棋，D4 应由青变橙。再次按 S4 可回待机。修改连线或换 HEX 前点 **Stop ■**，保存后再运行。
7. 若找不到 `STM32F103C8` 的 VSM 模型，先核查 ARM Cortex-M3 仿真组件及许可。仅有 EDA 原理图符号的库件无法执行 HEX。若缺的是 `WS2812X8X8`，先搜 `WS2812X8` 或 `WS2812`；连一个像素时 A1 开局为空，先按 **S4** 开局，再按 **S13** 开启编辑，把光标移到 A1 并按 **S11** 写入黑棋。没有可用 LED 模型时，用 PA8 波形和串口状态验证，普通 RGB LED 无法解码这条单线的数据帧。[Labcenter VSM 模型与仪器](https://www.labcenter.com/peripherals/)

## 6. USART1 与 LabVIEW

串口设置为 **115200 / 8 数据位 / 无校验 / 1 停止位 / 无流控**。两个方向分别连线，逻辑波形空闲为高。串口的 TTL 信号与外部 DB9 的 RS-232 电压不同，按模型端的实际接口区分。[Labcenter UART 接线与电平](https://www.labcenter.com/blog/sim-uart/)

| STM32 | 对端 | 用途 |
|---|---|---|
| `PA9 / USART1_TX`（30） | 接收 MCU 数据的输入端。Virtual Terminal 标为 `RXD` | 棋盘回复和主动上报。 |
| `PA10 / USART1_RX`（31） | 向 MCU 发数据的输出端。Virtual Terminal 标为 `TXD` | 接收同步请求、落子和控制。 |
| `GND` | 对端逻辑参考地（若模型引出该脚） | 共参考地。`RTS/CTS` 保持未连接。 |

### 优先用 Proteus 内部仪器验证

1. 给 PA9 接示波器或逻辑分析仪。复位、S4 开局或退出、S11 成功落子都会触发上报。量最窄的单比特脉冲，理论位宽 **8.68 µs**。如果要验证命令收发，还要给 PA10 接能够按同一参数发送的终端。
2. 双击 `VT1` 查看 `Baud Rate` 是否可手填 `115200`。本机库预设列表到 `57600`，Labcenter 的仪器说明也把 Virtual Terminal 上限列为 `57600`。只有本机模型接受 `115200` 且实测能正确解码时，才用该终端验收原版固件。若不支持，按步骤 1 测波形，或在兼容版本用支持 115200 的桥接方式验证。仅把终端改成 57600 会与固件不匹配。[Labcenter Virtual Terminal 规格](https://www.labcenter.com/instruments/)
3. 终端支持时，发送下列**整行 ASCII**，结尾附 `LF`（`0x0A`）。新版应返回 `@GAME7,1,...`，其中有棋盘、两方时钟和待机标志。回车若只发 CR，需要在发送模式中显式开启 LF。不要连同反引号或字面字符 `\n` 一起发送。

   ```text
   @SYNC7,1,C3C1
   ```

   备用基础查询 `@GET,1,2792` 同样以 LF 结束，返回 `@STATE,1,...`。上电主动帧的序号为 0，协商 `SYNC7` 后主动帧使用 `@GAME7`。`CRC` 错误优先检查原样发送的字符和帧末尾。方向键只移动点阵光标，验收同步时先按 S4 开局，再用 **S11 合法落子**触发棋盘变化。[项目串口协议](docs/protocol-advanced.md)

### 可选联动 LabVIEW

1. Windows 若已经有一对连通的虚拟 COM，记作 `COM_A`、`COM_B`。它们必须是不同端口且成对互通。Proteus 的 `Virtual Terminal` 自己不会创建 Windows COM。
2. 在 Proteus 添加 `COMPIM`，将 `Physical Port` 设为 `COM_A`。检查 `Physical` 和 `Virtual` 两侧能否都设为 115200 / 8N1 且关闭硬件、软件流控。本机模型的波特率预设也是到 57600，能否手填并运行要以本机验证为准。接 **PA9 → COMPIM 的串行输入、COMPIM 的串行输出 → PA10**。识别方向时可先不接 MCU，使 LabVIEW 在 `COM_B` 点击连接，示波器上出现同步请求的 `COMPIM` 端就是输出。若模型使用 RS-232 电平端，按模型说明加 RS-232／3.3 V UART 转换。PA10 保持只有一个发送端，接 COMPIM 之前先撤掉原 Virtual Terminal 的 TXD。
3. 运行 Proteus，再运行 `labview/Othello_Basic/Main.vi`。连接方式选「真实串口」，端口选配对的 `COM_B`，设为 `115200、8、None、1` 后点「连接」。界面提示待机时，在 Proteus 按 S4，LabVIEW 应同步为 2:2 开局。保留初始光标并按 S11，LabVIEW 应显示 4:1。再在 LabVIEW 选一个合法白棋位置并确认，仿真点阵应更新。用「刷新棋盘」可重发同步请求。S1 AI 对弈需要 LabVIEW 已配置本机 Egaroucid。单独演示板载闯关时按 S16。

## 7. 排查与验收记录

| 现象或验收项 | 操作和判据 |
|---|---|
| 点运行就报无模型或无程序 | 核对 `STM32F103C8` 是否为 VSM 器件、许可是否可用、`Program File` 路径是否存在。若使用当前 HEX，目标 Flash 起始地址应是 `0x08000000`。 |
| MCU 无运行迹象 | 测 3.3 V、共地、`BOOT0=0`、`NRST` 非低电平，再核对 `OSC Frequency=8MHz`。用 PB0～PB11 的扫描或 PA9 的初始上报区分启动问题和 LED 模型问题。 |
| 点阵黑屏或显示位置错 | PA8 用上升沿触发，先以约 `0.5 µs/div` 看 0/1 的高脉宽，再以约 `0.5 ms/div` 看整帧。若信号正确，查 `DIN/DOUT`、点阵电源与像素级联顺序。用 S13 编辑 A1、H1、A2 检查行起点与第二行方向。 |
| 方向或确认键不对 | 确认 S4 已开局，再从 `ROW/COL` 交点核对 S10 上、S15 左、S12 下、S7 右、S11 落子。使用单个短按，确认松开后列能回到高电平。 |
| 串口乱码或 LabVIEW 未同步 | 查 RX/TX 方向、8.68 µs 位宽、终端是否支持 115200、LF 是否发出、两个应用各占不同的配对 COM。收到 `@GAME6` 后再验收双向落子。 |
| 终局和模式演示 | 普通模式下以实际结算触发胜方颜色爱心，平局或主动退出不触发爱心。S16 闯关结束时可观察 `WIN/OVER` 滚动和自动回普通模式。滚动和爱心属于点阵显示，串口同步验收以确认的棋盘、计数和终局状态为准。 |

建议随仿真记录保存原理图全图、D3 落子前后画面、PA8 点阵位宽和 PA9 串口上报，有条件时再附 LabVIEW 的双向同步截图。指南已核对工程、HEX 和本机模型库，表中运行现象为待在 Proteus 中复核的验收判据。

工程依据见 [CubeMX 引脚与时钟](firmware/Othello_Basic/Othello_Basic.ioc)、[键盘扫描与点阵驱动](firmware/Othello_Basic/Core/Src/app_platform.c)、[像素顺序与脉宽常量](firmware/Othello_Basic/Core/Inc/app_config.h)、[键位与使用说明](firmware/Othello_Basic/README.md)。
