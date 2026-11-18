# 基础功能部分1审查报告

- 日期：2026-11-18（Asia/Shanghai）
- 任务：[#20](https://github.com/cancan7777777/black-and-white/issues/20)
- 输入：`基础功能部分1/othello_basic`，原始 10 个文件，按原样纳入 Git 作为审查基线。
- 范围：任务书基本要求 B01–B07，及完成这些功能所需的工程基础；不把进阶或 AI 缺失算作本阶段缺项。

## 结论

输入包是**可移植规则/协议核心与设计说明**，还不是完整 STM32 固件或完整基本功能系统。开局、合法性、八方向翻转、回合、任务书字面终局、CRC 与 GET/MOVE 协议已有源码；实体键盘、点阵、UART HAL 接入、应用主循环和实际 LabVIEW VI 尚缺。

本次补齐的范围是 **CubeMX 配置和可生成的基础 HAL/MDK 工程**。它不等于补齐以下硬件驱动与上位机功能，也不代表实物验收通过。

## 逐项审查

| 要求 | 输入中已有内容 | 缺少的基础内容 | 本次处理 / 后续任务 |
|---|---|---|---|
| B01：4×4 矩阵键盘控制、8×8 全彩点阵 | 配置指南给出候选引脚和行列输入方案 | 没有扫描/消抖、按键事件、坐标输入、确认/取消、光标反馈；没有 WS2812 编码、PWM DMA 发送/收尾、像素映射与棋盘刷新 | 本次只配 GPIO、TIM1/DMA，驱动与交互继续由 #5、#6 实现 |
| B02：开局中心 2 黑 2 白 | `ot_init` 正确设置索引 27/28/35/36，黑白各 2、黑先手、revision=1 | 没有将初始化状态真正显示在 LED 上，未集成 main | 保留现有核心，不声称实体显示已完成；后续 #5、#7 |
| B03：至少翻 1 子、正确翻子 | `ray`/`ot_legal`/`ot_move` 逐方向判断并翻转，非法落子不改状态 | 两个实际输入来源尚未接入；已有测试并非八方向/边界的完整穷尽覆盖 | 本机运行原测试通过；#7 后续补规则边界与真实输入验证 |
| B04：一方无合法位置即结束 | `ot_move` 在落子后检查双方任一方无合法落子即设置结束，未擅自改为 pass | 未在实物/上位机展示终局；教师对字面规则的解释仍待 #2 确认 | 保留现有任务书字面语义，不在工程配置阶段改规则 |
| B05：下位机串口实时上传点阵状态 | 纯 C `ot_state`、`ot_command` 实现 STATE/GET/MOVE/ERROR、CRC、revision 检查 | 没有 UART 接收中断重装、环形/行缓冲、超长帧丢弃、发送队列、错误恢复、实体落子后的主动 STATE 或实际延迟证据 | 本次生成 USART1/NVIC 初始化；运行层仍属 #8 |
| B06：上位机点阵界面、实时同步、按钮落子 | `docs/通信与LabVIEW.md` 描述 VI 划分和坐标/协议契约 | 没有实际 `.vi`、图形棋盘、鼠标/按钮事件、VISA 通信循环、CRC 与 STATE 解析 | 未用 EViews 或 C 核心冒充上位机，继续 #9、#10 |
| B07：上位机端口/波特率/数据位/停止位/校验位可设 | 文档列出全部参数项和默认 115200 8N1 | 没有可操作的 VISA 配置界面、参数校验、连接/断开和不匹配反馈 | 本次下位机固定 115200 8N1 只是基线，不能替代上位机要求；继续 #9 |

## 还必须完成的基础集成

1. 建立应用主循环，调用 `ot_init`，串行处理实体和远程落子，避免共享 `OtGame` 竞态。
2. UART ISR 只接收字节/入队，主循环按 LF 拼帧、检查长度并调用协议，不能在 ISR 中执行阻塞发送或完整规则计算。
3. 所有合法状态变更统一触发点阵刷新和 STATE 上传；非法请求保留原棋盘，并反馈错误。
4. 明确单局内 revision、防重、重连丢弃旧请求的行为；当前 V1 不能跨复位仅凭 revision=1 识别旧会话。后续接入时应按文档清空旧请求，增加会话标识的变更另行冻结。
5. 完成键盘行列/排针与 LED 方向/蛇形映射的实物确认，再验证 64 格对应、颜色和低电平复位时序。
6. 实际 LabVIEW 工程独占 VISA，采用有界缓冲和超时；GET/STATE 同步完成后再允许落子。

这些缺项均已在原有 #5–#10 的验收范围内，本次不重复创建六个新实现任务、不关闭它们。

## 本机测试证据与局限

使用已经安装的 Visual Studio 2022 MSVC 19.44，以 C11 / W3 编译输入包的两份 C 源码和 `tests/test.c`，运行结果：

```text
PASS: CRC, initial board, legal move, flipping, stale, illegal, end, multi-direction
```

测试覆盖已给出的 CRC 参考向量、开局合法步、一次翻转与计数、STALE/ILLEGAL/CRC 错误、终局和多方向翻转。原测试的 `sprintf` 有 MSVC C4996 建议警告，测试仍正常运行，未改原文件。

这不是完整的八方向、所有边界、串口抗干扰或实物测试。输入 README 的“Linux GCC 通过”属于输入声明；本报告新增的独立证据是上述本机 MSVC 运行结果。

## 本次工程的配置依据

- 用户指定参考 `D:\STM32\second` 和 third；实际第三个目录为 `D:\STM32\thrid`。
- 两个 `.ioc` 都是 CubeMX 6.3.0、F1 1.8.6、HSE→PLL×9→72 MHz、MDK-ARM V5。
- `second` 是 STM32F103C8；`thrid` 是 F103CB，并关闭调试。本项目采用 C8，不照搬 CB 的存储容量、无关 ADC/TIM2/TIM3 和关闭 SWD 的设置。
- 保留 PA13/PA14 SWD，USART1 使用 PA9/PA10；本项目专用 TIM1_CH1/PA8 与 DMA1_Channel2，键盘用 PB0/PB1/PB10/PB11 和 PB12–PB15。
- 8 MHz HSE 是按用户指定参考采用的工程假设，不等于对实际板卡晶振的测量或上电验收。

工程的最终参数、重生成方式与真实编译结果见 [工程说明](../../firmware/Othello_Basic/README.md)。

## 官方核对资料

- [ST RM0008 参考手册](https://www.st.com/resource/en/reference_manual/rm0008-stm32f103xx-advanced-armbased-32bit-mcus-stmicroelectronics.pdf)：核对 STM32F1 时钟、TIM1 与 DMA1 通道 2 映射；本机 CubeMX 数据库和实际生成代码用于核对配置落地。
- [ST CubeMX CLI 官方说明](https://dev.st.com/stm32cube-docs/stm32cubemx/6.18.0/en/docs/markup/CubeMX_CLI.html)：核对 load、project generate、配置保存等工作流。该网页版本新于本机 6.3.0，不能替代本机实际生成验证。
