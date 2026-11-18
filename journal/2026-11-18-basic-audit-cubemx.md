# 基础功能部分1审查与 STM32CubeMX 工程创建

- **状态**：done
- **开始**：2026-11-18（Asia/Shanghai）
- **最后更新**：2026-11-18（Asia/Shanghai）
- **工作目录**：`D:\black&white`

## 目标与授权

用户本次明确要求阅读“基础功能1”文件夹，检查基础功能缺失，然后创建 STM32CubeMX 项目和基础配置。实际文件夹是 `基础功能部分1/othello_basic`。

本次授权包括资料/源码审查、离线 CubeMX 工程和初始化代码生成、必要的编译验证、GitHub 任务/PR 与 Git 留痕；不据此执行接线、上电、刷机或实现完整 LED/键盘/LabVIEW 功能。既有“只规划”边界在本次离线工程创建范围内被用户的新指令明确扩展。

## 背景

- 已回查 `journal/2026-11-17-project-planning.md`、根 `AGENTS.md` 与需求基线。
- 初始分支 `main` 与 `origin/main` 同步；用户新增 `基础功能部分1/` 为未跟踪资料，不能擅自覆盖或清理。
- 输入包有两个可移植 C 模块、头文件、PC 测试、CubeMX 配置建议、LabVIEW 设计文字和可选 EViews 脚本；尚无 `.ioc`、HAL 工程或实际 LabVIEW VI。
- 资料中的“Linux GCC 测试通过”“Blue Pill 已确定”等表述是输入包声明，不当成本机编译或实物验收证据。
- 本机已找到 STM32CubeMX `D:\STM32\STM32CubeMX.exe`（日志显示 6.3.0）、现有 F1 固件包 1.8.6/1.8.7、Keil `C:\Keil_v5\UV4\UV4.exe`，不需要安装依赖。

## 方案

1. 审查基本要求 B01–B07 的实际实现覆盖和测试局限，写审查报告。
2. 核对本机 CubeMX 数据库与官方资料，冻结本次离线项目 MCU、时钟、GPIO、USART1、TIM1 PWM/DMA、NVIC 与工程生成选项。
3. 原板晶振未确认时，优先采用内部 HSI；已向用户询问是否确认 8 MHz HSE。等待期间继续不依赖回答的审查工作，不擅自将 HSE 写成事实。
4. 创建独立 `firmware/Othello_Basic` 工程，由本机 CubeMX 实际载入/生成，能编译时使用现有 Keil 验证；不把“文件写好”冒充工具验证。
5. 专用 Issue/任务分支记录过程，检查变更、提交、推送、创建 PR，报告真实验证结论和剩余缺项。

## 待定事项

- [ ] 外部晶振是否已确认 8 MHz：用户已收到可选问题；未确认可继续内部 HSI 离线配置。
- [ ] 点阵具体版本、DIN/GND、电平转换/供电、键盘排针和 USB-TTL 实物：仍未验证；不接硬件，不启动 PWM 传输。
- [ ] 规则/计分等原项目待定项不在本次擅自冻结；基础终局沿用任务书字面解释。

## 进度

### 2026-11-18 · 输入与环境核查

- 已读取输入 README、CubeMX 指南、通信设计、规则/协议源码与测试；核对用户目录名称差异。
- Git 初始状态：`## main...origin/main`，仅 `基础功能部分1/` 未跟踪。
- 规则代码存在开局、八方向翻转、单方无合法落子结束；协议存在 GET/MOVE、CRC 和 revision 检查。
- 初步发现：实体键盘扫描/消抖/坐标输入、WS2812 驱动/映射/刷新、UART HAL 接收/拼帧/上传与主循环集成、实际 LabVIEW VI/串口配置均未实现。EViews 不能替代上位机。
- 已查询官方 ST 文档核对 HSI PLL 与 TIM1_CH1 DMA 映射；下一步核对本机数据库和工具支持，建 Issue/分支并生成审查结论。

### 2026-11-18 · Issue、分支与用户指定参考

- 创建 [#20](https://github.com/cancan7777777/black-and-white/issues/20)，分支 `task/20-basic-audit-cubemx`；没有假定仓库下一个编号必为 #19，也不改动已有其他任务。
- 用户回答晶振问题：“参考D/STM32/second以及third里面的配置”。实际存在 `D:\STM32\second\second.ioc` 与 `D:\STM32\thrid\thrid.ioc`，不存在 third 目录；按两份实际配置读取，不改参考工程。
- 两份工程均配置 HSE + PLL×9、SYSCLK 72 MHz、APB1 36 MHz、APB2 72 MHz、F1 固件 1.8.6、MDK-ARM V5。以此作为本次获用户指定的配置依据，采用 8 MHz HSE / 72 MHz，不再使用此前的 HSI 备用建议。
- second 为 F103C8；thrid 为 F103CB 且 SYS 调试关闭。本工程继续 C8 的 64 KB Flash / 20 KB RAM，并保留 SWD，不照搬无关 ADC/定时器/LSE 或关闭调试配置。
- `ctx_execute_file` 无法读取工作区外的参考路径，错误为 outside project root；用户已明确指定这些参考，改用主机只读文件访问。ST 在线文档的 context fetch 失败，使用搜索工具打开官方 CLI 文档核对；不安装或修改工具。
- 下一步：使用已安装 CubeMX 创建原生 MCU 配置，再设置本项目专用外设并实际生成工程。

### 2026-11-18 · 核心测试和 CubeMX 路径问题

- 原输入包共 10 个文件，已记录 SHA-256；未修改源码。使用本机 MSVC 19.44、C11 编译并运行输入的 `tests/test.c`：`PASS: CRC, initial board, legal move, flipping, stale, illegal, end, multi-direction`。原测试的 `sprintf` 产生 MSVC C4996 建议警告；不为消除输入警告而修改原测试。
- 初次临时 CMD 运行因路径/编码解析失败（`The system cannot find the path specified` / `'4' is not recognized`）；改为 PowerShell 导入现有 VS 环境后直接调用 CL，测试通过。未安装 GCC 或其他工具。
- 第一次 CubeMX 空配置 `config save` 返回 `KO`，没有误记为成功。后续使用明确参数的种子 `.ioc`，由 CubeMX 6.3.0 实际 `config load` 与 `config save` 均返回 `OK`。
- 使用原工作区路径生成完整工程时报错：`The reference to entity "white" must end with the ';' delimiter` / `SAXParseException`。CubeMX 6.3.0 在中间 XML 中未转义 `D:\black&white` 的 `&`；即使 CLI 最后打印 OK，也不代表工程生成完成。
- 添加 `tools/cubemx/generate.ps1`，在未占用的临时盘符上映射同一工作区生成，结束后移除映射；不改文件夹名、不移动参考工程、不升级或安装软件。程序保留原始日志并检查必需工程文件，避免只看进程退出码。
- 旧 CubeMX 还打印若干第三方 PDSC 格式不支持的警告/错误；没有选择这些扩展包，后续以 F1 基础工程生成和编译结果判定。
- 下一步：绕开路径 XML 问题重新生成，并核对真正生成的外设初始化与 MDK 工程。

### 2026-11-18 · 生成落地与工程兼容性修正

- 临时 O: 映射生成成功，`config load`、`project generate`、`config save` 都返回 OK；生成了 main/gpio/dma/tim/usart、中断、启动与 MDK 项目。结束后 O: 已撤销。
- 检查真实生成代码：HSE/PLL×9、APB1 /2、APB2 /1；键盘行开漏初始高、列上拉输入；USART1 115200 8N1 + NVIC；TIM1 PSC=0、ARR=89、Pulse=0、DMA1_Channel2 半字/Normal/High；仅初始化，未启动 PWM 或 UART 接收。
- 将 PA8 复用输出速度明确设为 High，保持 800 kHz 定时器参数；不把候选 LED CCR/复位时序当作实物验收。
- 初次 MDK 编译报 14 个 Error #5，原因是 CubeMX 在先前失败生成后保留 `../O:/firmware/...` 源路径并重复列入文件，不是规则源码错误。补充生成后源码路径检查与最小的相对路径修正，去重相同源条目。
- 严格清理范围仅为本任务新生成工程内的 Core/Drivers/MDK-ARM/缓存，执行前核实绝对路径在 `D:\black&white\firmware\Othello_Basic\` 内；保留 .ioc/README 和用户输入目录。修正 CLI 参数引号 `SetCopyLibrary "copy only"` 后生成文件从约 68 MB/919 个收敛为约 3.9 MB/76 个（编译产物另计），`.ioc LibraryCopy=1`。
- 使用通用 XML 序列化重写 uvprojx 后，旧 uVision 返回 15（官方定义为 XML 导入读取错误），且没有更新编译日志。改为保留 CubeMX 原始 XML 格式，只做所需路径文本修正；构建脚本增加日志时间戳校验，避免沿用上一轮旧结果。
- 输入 10 文件再次校验 SHA-256 全部一致；现有 PC 核心测试已通过，不因工程工具问题反复扩大其测试范围。
- 下一步：验证修正后的 MDK 编译链接，保存最终配置说明、验证摘要与 PR。

### 2026-11-18 · Keil 编译通过与根因更正

- 精确定位 UV4 exit 15 / CubeMX XML prolog 错误根因：上一版 .NET XML Save 增加了 UTF-8 BOM（前缀 `EF BB BF`，首字符 U+FEFF），本机旧工具的文本读取器不接受它。这里更正上一步笼统“XML 格式不兼容”的描述。
- 原生 XML 文本格式保留后，显式移除旧文件已有 BOM；新生成辅助脚本使用 UTF-8 无 BOM 写回，仅做必要相对路径修正，不再用通用序列化重写整个工程。
- Keil 真实构建返回 0；ARMCC V5.06 update 6 build 750：`0 Error(s), 0 Warning(s)`。
- 链接结果：Code=6648、RO-data=312、RW-data=16、ZI-data=1848，符合 C8 64 KB Flash / 20 KB RAM 范围；这些是空应用初始化骨架的大小，不是完整游戏固件。
- 生成的 HEX/AXF 仅作本机编译产物，按 .gitignore 排除；没有烧录或上电。
- 生成脚本补充保留 ST/CMSIS 原始许可文件；下一步对最终脚本重生成一次并再次编译（因生成流程已有修正，需要验证可复现）。

### 2026-11-18 · 最终可复现验证

- 最终 `tools/cubemx/generate.ps1` 全程通过：load / project generate / config save 返回 OK；再次调用 `tools/build-keil.ps1` 返回 `KEIL_EXIT=0`，0 错误、0 警告，程序大小保持 Code=6648、RO-data=312、RW-data=16、ZI-data=1848。
- 可复现测试脚本 `tools/test-basic-core.ps1` 实际运行通过，仍仅保留原测试 `sprintf` 的 C4996 建议警告。
- 最终静态核对：.ioc 中 C8/HSE 8 MHz/PLL×9/SWD/DMA1_CH2/800 kHz/115200/必要库复制均符合预期；MDK 23 个源文件引用全部存在、无重复、无临时 O: 依赖、文件无 BOM。
- 10 个用户输入文件逐字节 SHA-256 全部保持原值，按原样纳入本任务 Git 基线；原规则、协议与测试没有被改写。新工程放在独立 firmware 目录。
- ST 包及 HAL/CMSIS 原始许可文件保留；文档本地链接无缺失，JSON 任务清单可解析为 19 项；O: 临时映射已移除。
- 根 README/AGENTS/阶段方案/任务表更新为本次用户已授权的离线配置范围，未关闭原 #3–#10 的确认/实现/联调任务。
- 下一步：暂存任务相关文件，检查原创文档/配置格式与敏感信息，提交/推送分支并创建关联 #20 的 PR。生成库、原输入作为保留来源不统一改写格式。

### 2026-11-18 · Git 提交、公开推送与 PR

- 本次原创文档、配置和工具的 `git diff --cached --check` 通过；没有为格式统一改写 ST/CMSIS 或用户输入。暂存内容未发现凭据格式；10 个输入文件的 Git blob SHA-256 也与原值一致。
- 提交 `f3c3c52`：`feat(#20): add CubeMX base project and basic-function audit`，正文 `Refs #20`；已推送 `origin/task/20-basic-audit-cubemx`。
- 创建并附加 [PR #21](https://github.com/cancan7777777/black-and-white/pull/21)，正文 `Closes #20`。实现交付保留在任务分支，未擅自合并 main；#20 留待 PR 合并关闭，原基础实现任务不关闭。
- 收尾发现 Keil 的启动汇编清单 `.lst` 位于 MDK 根目录而非输出目录，补充忽略并只从 Git 索引移除该生成清单，不删除本地编译证据、不改写已推送历史。
- 本次收尾提交记录 PR 与结果；最终提交 SHA、远端同步和工作区状态会在 #20 评论及最终回复给出，避免日志自引用自身提交哈希。

## 结果

- 需求审查：`docs/reviews/2026-11-18-basic-function-audit.md`，明确基础功能尚缺键盘/点阵/UART HAL 与应用集成、实际 LabVIEW VI；原规则/协议核心已通过本机现有测试。
- 工程：`firmware/Othello_Basic/Othello_Basic.ioc` 与 `MDK-ARM/Othello_Basic.uvprojx`。C8、用户参考的 HSE 8 MHz→72 MHz、SWD、USART1 115200 8N1、TIM1_CH1 800 kHz/DMA1_CH2、矩阵 GPIO/NVIC 均已配置并核对实际生成代码。
- 最终重生成 + ARMCC 5.06 构建均通过，0 errors / 0 warnings；原输入核心测试 PASS（其 C4996 警告已说明），10 份输入字节保持原样，临时盘符已清除。
- GitHub：[Issue #20](https://github.com/cancan7777777/black-and-white/issues/20) / [PR #21](https://github.com/cancan7777777/black-and-white/pull/21)；分支 `task/20-basic-audit-cubemx`，不自动合并 main。
- 完成的是离线工程创建和基础配置，不是可运行黑白棋系统，也没有实物验收或烧录。

## 遗留

- 原 #3/#4 的实物/接口确认，以及 #5–#10 的驱动、应用和上位机实现仍未完成；本次用户授权不扩展到这些完整实现。
- 实际 HSE 晶振、点阵供电/第三引脚/数据方向、键盘排针、USB-TTL 电平和 WS2812 版本/时序须在接线与运行前核实。
- 主循环为空，尚未接入原规则核心、启动 PWM/UART 接收或键盘扫描；所有实际基本功能验收仍需后续实施。
- PR #21 待合并；后续任务从确认后的基线开始，不在未授权情况下刷机或自动合并。

## 上下文恢复

先读本日志、`docs/reviews/2026-11-18-basic-function-audit.md`（创建后）、`firmware/Othello_Basic/README.md`（创建后）；保留用户未跟踪的输入目录。
