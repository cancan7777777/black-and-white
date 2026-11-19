# Othello_Basic：STM32CubeMX 基础工程

`main.c` 已接入基础驱动和进阶游戏调度。当前工程可用 Keil 编译，实体键盘、点阵和串口仍待联调。

本工程对应 GitHub [#20](https://github.com/cancan7777777/black-and-white/issues/20)，任务分支 `task/20-basic-audit-cubemx`。

用户已指定参考本机 `D:\STM32\second\second.ioc` 和 `D:\STM32\thrid\thrid.ioc`（用户所说 third 的实际目录拼作 thrid）。两份配置均采用外部 HSE、PLL×9、72 MHz、STM32CubeMX 6.3.0、STM32Cube F1 1.8.6、MDK-ARM V5。

本工程以 `second` 的 STM32F103C8 为目标，采用用户指定参考的 8 MHz HSE → 72 MHz 路线。`thrid` 实际为 F103CB，且关闭了调试；这些与本项目不同的配置不照搬，保留 C8 的 64 KB Flash / 20 KB RAM 和 SWD。

配置不代表实物晶振、接线、LED 时序和供电已验收。本次不接线、不上电、不刷机。

## 工程文件

- `Othello_Basic.ioc`：由本机 CubeMX 6.3.0 实际加载、保存、生成。
- `Core/`：CubeMX 生成的主程序、外设初始化、MSP、中断与系统文件。
- `Drivers/`：本机 STM32Cube F1 1.8.6 的必要 HAL/CMSIS 文件，附原始许可。
- `MDK-ARM/Othello_Basic.uvprojx`：可直接在 Keil 打开的工程，源文件路径为相对路径，不依赖临时盘符。
- `MDK-ARM/startup_stm32f103xb.s`：C8/CB 中等密度设备启动文件，工程存储区按 C8 限定。

## 已配置参数

| 项目 | 配置 |
|---|---|
| MCU | STM32F103C8T6，LQFP48；Flash 64 KB，SRAM 20 KB |
| 时钟 | HSE 晶体模式，8 MHz /1 → PLL×9；SYSCLK/HCLK 72 MHz；APB1 36 MHz、APB1 timer 72 MHz；APB2/TIM1 72 MHz |
| Flash / 时基 | 2 wait states；HAL SysTick 1 ms，SysTick IRQ 优先级 15 |
| 调试 | Serial Wire，保留 PA13/PA14 |
| 串口 | USART1 PA9 TX / PA10 RX，115200、8 数据位、无校验、1 停止位、TX/RX、无流控；USART1 IRQ 优先级 2 |
| 点阵信号 | PA8 = TIM1_CH1，复用推挽 High speed；PWM mode 1，PSC=0、ARR=89、初始 Pulse=0，800 kHz，自动重载预装载使能 |
| 点阵 DMA | TIM1_CH1 → DMA1 Channel 2；Memory-to-Peripheral、Normal、外设不增/内存增、双半字、High priority；DMA IRQ 优先级 1 |
| 键盘行 | PB0/PB1/PB10/PB11 = KEY_ROW0–3，开漏输出，初始 High（释放），无上下拉，Low speed |
| 键盘列 | PB12/PB13/PB14/PB15 = KEY_COL0–3，输入上拉 |
| 工程选项 | MDK-ARM V5，独立外设 `.c/.h`，保留 USER CODE，仅复制必要库文件；不启用无关 ADC、USB、LSE、TIM2/TIM3 |

初始化顺序为 HAL → SystemClock → GPIO → DMA → TIM1 → USART1，DMA 时钟先于 TIM1 的 DMA 初始化。

## 重新生成和编译

在仓库根目录的 PowerShell 运行：

```powershell
.\tools\cubemx\generate.ps1
.\tools\build-keil.ps1
```

默认使用现有 `D:\STM32`、F1 1.8.6 包和 `C:\Keil_v5\UV4\UV4.exe`，不安装工具。CubeMX 脚本暂时将工作区映射到空闲 O:，结束后撤销；若 O: 已被占用，用 `-DriveLetter P` 等空闲盘符。CubeMX 安装路径可通过 `-CubeMxRoot` 指定。

原因是本机 CubeMX 6.3.0 在含 `&` 的工作区生成中间 XML 会失败。可直接打开 `.ioc` 查看配置，但在本目录重新生成应使用上述脚本。脚本校验生成结果、修正旧工具遗留的盘符源路径、保留原生 XML 格式及 UTF-8 无 BOM；编译不再需要 O:。

日志位于忽略的 `build/cubemx-generate.log` 和 `build/keil-build.log`。KEIL 项目输出目录、HEX/AXF 与本机选项均忽略，不作为已验证可烧录游戏发布。

核心输入包的独立测试可运行：

```powershell
.\tools\test-basic-core.ps1
```

该脚本使用现有 Visual Studio/MSVC；它验证原规则/协议输入，不会把这些模块自动集成到 HAL 应用。

## 已完成的验证

- 原输入 `tests/test.c` 在本机 MSVC 19.44 / C11 编译运行通过；仅有原测试 `sprintf` 的 C4996 建议警告。
- CubeMX 实际生成了 GPIO、DMA、TIM1、USART1 与中断代码，逐项核对与上表一致。
- ARMCC V5.06 update 6 build 750 真实编译链接：**0 Error(s), 0 Warning(s)**。
- 当前初始化骨架大小：Code=6648、RO-data=312、RW-data=16、ZI-data=1848 bytes；不是完整游戏程序大小。
- 记录与后续完整重生成验证见 [任务日志](../../journal/2026-11-18-basic-audit-cubemx.md)。

## 进阶操作

上电默认普通模式。`2 / 4 / 6 / 8` 移动光标，`5` 确认落子。`A` 在结束后开始新局，`B` 暂停或继续，`C` 结束退出，`0` 重开本局，`D` 循环普通、闯关、计时模式。`*` 进入或退出编辑，编辑时 `5` 把当前格在空、黑、白之间循环。实体计时模式使用最近设定时长，默认 180 秒。

闯关结束时点阵滚动显示 `WIN` 或 `OVER`，单局棋盘仍通过串口返回。下位机在处理键盘和串口请求前结算超时。规则和串口扩展见[进阶规则与通信约定](../../docs/protocol-advanced.md)。保留[基础功能审查报告](../../docs/reviews/2026-11-18-basic-function-audit.md)作为实现前基线。
