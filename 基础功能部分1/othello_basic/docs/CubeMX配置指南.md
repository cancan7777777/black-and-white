# STM32F103C8T6 基础黑白棋：CubeMX 配置指南（第一阶段）

本阶段仅实现任务书基本要求，不包含 AI、闯关、计时、作弊、回放或排行榜。当前已确定：Blue Pill、8×8 WS2812B 集成点阵、4×4 键盘。照片中 V+、IN 可辨；第三脚需确认 GND。USB-TTL 型号/逻辑电平、键盘排针顺序、5V 供电尚待确认。以下外设引脚是设计候选，不是已确认的接线指令；先配置离线工程，不接线、不通电。

## 1. 新建工程
1. 打开 STM32CubeMX → New Project → MCU Selector，搜索 STM32F103C8T6，确认 LQFP48（以实际芯片为准）。Start Project。
2. System Core → SYS → Debug 选择 Serial Wire，保留 PA13、PA14。即使暂时通过串口下载，也保留调试功能。
3. System Core → RCC：若板载晶振已经确认是 8 MHz，则 HSE 选 Crystal/Ceramic Resonator；否则第一步可先保留 HSI，仅做规则与串口实验。不要在未知时钟下使用下面的 PWM 参数。
4. Clock Configuration：在 8 MHz HSE 确认的情况下，PLL Source HSE，PLL ×9，SYSCLK 72 MHz，AHB /1，APB1 /2，APB2 /1。确认 TIM1 timer clock 为 72 MHz，APB1 36 MHz。若保留 HSI，可按实际时钟另算 PWM，暂不初始化 LED 驱动。

## 2. USART1（以独立 USB-TTL 确认存在为前提）
Connectivity → USART1 → Asynchronous：PA9 TX、PA10 RX。Parameter Settings：115200 baud、8 bits、None parity、1 stop bit、TX/RX、None flow control。NVIC → USART1 global interrupt 勾选（若后续采用中断接收）；先不选 UART DMA，以避免不必要的资源冲突。

运行串口参数与下载时的 ROM Bootloader 参数不同。确认 USB-TTL 为 3.3V TTL 而非 RS-232，下载时 BOOT0=1、BOOT1=0，复位；用 STM32CubeProgrammer 的 UART 模式连接实际 COM 口并烧录。结束后断开下载工具，BOOT0=0，复位，LabVIEW 再打开同一 COM 口。**不能同时让下载工具和 LabVIEW 占用串口。** 如果没有独立 USB-TTL，不执行此步骤；原生 USB 插座不等于 USB-TTL，也不保证能首次下载。

## 3. LED 候选配置（暂不接硬件）
Timers → TIM1 → Clock Source Internal Clock（如果 UI 显示该选项），Channel1 = PWM Generation CH1（PA8）。仅启用 CH1，不启用 CH2/CH3，以免占用 USART1 的 PA9/PA10。

72 MHz 定时器时钟条件下：Prescaler=0，Counter Mode Up，Counter Period=89，Clock Division DIV1，Repetition Counter=0，PWM mode 1，Pulse=0，Polarity High，Fast Mode Disable。频率=72 MHz/(90)=800 kHz。候选 CCR=23（约0.319µs）与 58（约0.806µs），需按实物 LED 版本核对时序。复位低电平建议至少 300µs（例如 256 个 0 占空比周期，约320µs），具体依芯片资料。

DMA Settings：Add TIM1_CH1（F103 中密度芯片预期 DMA1 Channel 2，核对 CubeMX 实际映射），Memory to Peripheral、Normal、Peripheral increment Disable、Memory increment Enable、两侧 Half Word、Priority High。NVIC 中启用对应 DMA 通道中断。暂不启用额外定时器中断。后续 HAL PWM DMA 驱动需验证首尾位、CCR preload、复位间隔以及传输结束后引脚保持低电平，不能认为配置完成即驱动正确。

## 4. 键盘候选配置（仅在无源 8 线 4×4 矩阵确认后）

| 逻辑名称 | 候选 GPIO | 方向 |
|---|---|---|
| ROW0/1/2/3 | PB0/PB1/PB10/PB11 | Open Drain 输出、初始 High（释放）、No Pull |
| COL0/1/2/3 | PB12/PB13/PB14/PB15 | 输入、Pull-up |

扫描时只将一行拉低，其他行释放，读取低电平列；逐行扫描并软件消抖，单键操作，按住不连续重复落子。具体键盘排针哪根是行/列，必须根据丝印或通断测量确定，不根据外观猜测。此分配避开 PB2 启动引脚、PB3/PB4 JTAG 及 USART1。

建议键位：1/2/3/4/5/6/7/8 依次输入行、列；# 确认，* 取消；其余键留作后续进阶。实体光标/输入反馈可另设计。不要在基础代码中添加进阶模式。

## 5. 工程生成
Project Manager：Project Name `Othello_Basic`，Toolchain/IDE 选实际安装的 MDK-ARM 或 STM32CubeIDE，Code Generator 勾选 Keep User Code when re-generating。Generate Code。EViews 8 是统计软件，不能编译 STM32 程序；若你说的是 Keil uVision，请用实际 MDK 版本。

## 6. 加入本包代码
将本包 Core/Inc 与 Core/Src 中的 `othello.*` 和 `ot_protocol.*` 复制到生成工程对应目录；Keil 中 Add Existing Files to Group 并确保 include path 包含 Core/Inc。不要直接覆盖生成的 main.c、gpio.c 等文件。

本包提供的核心模块与 STM32 HAL 解耦，已用 PC GCC 进行单元测试。下一步确认硬件后才编写/启用 LED PWM DMA、矩阵扫描、UART HAL 接收和 main.c 整合。当前不提供声称可以直接烧录的 HEX 或 .ioc 文件。

## 7. 联调顺序
先编译并测试烧录 Blink（暂不接 LED）→ 验证 UART GET/STATE → 单独测试键盘 → 单独测试点阵像素映射及低亮度颜色 → 整合游戏 → LabVIEW 双向落子。点阵 V+ 应使用确认规格的独立稳压 5V 电源，所有信号端共地；避免从 USB-TTL、STM32 3.3V 或面包板电源轨直接承担整板负载。数据线保留串联电阻，按实际模块规格确认供电和 DIN 输入电平。不得凭照片猜测第三脚后直接通电。

## 8. 参考资料
- ST RM0008: https://www.st.com/resource/en/reference_manual/rm0008-stm32f101xx-stm32f102xx-stm32f103xx-stm32f105xx-and-stm32f107xx-advanced-armbased-32bit-mcus-stmicroelectronics.pdf
- ST AN2606: https://www.st.com/resource/en/application_note/an2606-introduction-to-system-memory-boot-mode-stmicroelectronics.pdf
- WS2812B V5 示例资料（实际模块版本未确认）：https://datasheet.lcsc.com/lcsc/2006151006_Worldsemi-WS2812B-B-W_C114586.pdf
