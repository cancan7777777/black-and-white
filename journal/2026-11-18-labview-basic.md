# LabVIEW 基础上位机与离线验证

- **状态**：in-progress（2026-11-19 用户明确要求继续）
- **日期**：2026-11-18（Asia/Shanghai）
- **工作目录**：`D:\black&white`
- **关联任务**：既有 #9（基础 LabVIEW 界面/串口）与 #10（基础联调）；实际分支创建后补充。

## 目标与授权

用户要求继续完成 LabVIEW 工程，并询问是否可不依赖硬件。用户已明确本轮“先完成基础上位机”。本次实现棋盘显示/选择落子、串口参数/连接、GET/MOVE/STATE/ERROR 与 CRC、离线模拟下位机和基础状态反馈；不实现进阶/AI，不擅自变更终局规则。

基础 UI/规则/协议可在无板卡情况下开发与模拟验证，真实 UART、电气链路、实体输入/LED 同步仍需要后续实物测试，不能把模拟通过当作实机验收。

## 背景与历史

- 已回查根 AGENTS、`journal/2026-11-18-basic-audit-cubemx.md`、需求 B06/B07 与输入协议 V1。
- 当前分支 `task/20-basic-audit-cubemx`，最新提交 `f977338`，工作区干净；CubeMX PR #21 待合并。新任务分支从本次已存在的工程基线继续，不擅自合并旧 PR。
- 输入协议：ASCII+LF；CRC16/CCITT-FALSE；GET、MOVE(seq/revision/row/col)、STATE、ERROR；0/1/2 棋盘编码，STM32 为实局权威；任一方无合法步即结束。
- 本机实际 LabVIEW 是 `D:\Electronics Pioneer\LabVIEW 2018`（注册版本 18.0，32 位），已发现 NI-VISA；原方案中的“LabVIEW 9”不能冒充本机已验证版本。使用实际 2018 创建并注明版本，不声称已验证 2009 兼容。

## 方案

1. 核对原生 VI 创建/自动化与现有开发环境，建立 `.lvproj`、真实可运行 VI 和明确的源码/资源结构。
2. 用同一帧协议实现实机串口模式与离线模拟下位机；模拟模式清楚标识，不伪装硬件在线。
3. 实现基础棋盘、坐标/落子、串口全部参数、连接/断开/刷新、回合/计数/终局/错误。
4. 验证 CRC、开局、合法/非法/过期帧、模拟对局、错误连接和原生 VI 可运行性；实机项保留未测。
5. 更新 #9、Git 分支、PR、journal；后续若依赖未合并 PR #21，采用明确的依赖分支，不把其它变更重复混作本任务实现。

## 进度

### 2026-11-18 · 范围与环境核查

- 用户确认仅基础上位机，不启动暂停/模式/回放/排行榜等进阶。
- 本地尚无实际 `.vi` 或 `.lvproj`；只有输入包的设计文档。已定位已安装 LabVIEW 与 NI-VISA。
- 已查 NI 官方 ActiveX/VI scripting 文档，下一步检查本机实际可用 API，以生成和验证真实工程，不能只交付文档或空壳 VI。

### 2026-11-18 · 用户偏好与原生工程进展

- 用户要求“尽量不使用 computeruse，要不然 token 消耗太大了”。后续优先文件/API/脚本，不继续计算机界面操作；先前一次窗口检查被用户中断，不把它记为可视化验收。
- 已建立分支 `task/9-labview-basic`；通过 C# x86 + 官方 ActiveX 从内置模板创建并保存真实 `labview/Othello_Basic/Main.vi`（9764 bytes），以及 `.lvproj`。目前仍是模板，尚不能声称上位机 VI 已完成。
- 本机 PowerShell 对 LabVIEW COM 反射不完整；32 位文件脚本受执行策略限制，没有改变系统策略，改用 C# x86 官方 COM 接口。类型库只读探针确认 GetVIReference、SaveInstrument、Call/Run 等接口可用。初次 OpenFrontPanel 枚举 0 报无效输入，改用 FPWinOpen 属性打开项目自己的 VI，没有修改用户未保存的“未命名 1”VI。
- 已写协议解析、串口传输、原 C 下位机模拟包装、会话控制与可嵌入的棋盘组件；基础构建成功，初次自动测试因手写 GET CRC 测试向量不正确失败。用独立 Python `binascii.crc_hqx` 得到 @GET,1 的正确参考值 2792 并修正测试，不修改协议算法迎合错误向量。
- 用户工作区另新增 `firmware/Othello_Basic.zip` 和输入目录下的 `main.c_黑白棋基础功能.c`，并非本次工具生成；保留这些未跟踪文件，不纳入本次提交。
- 下一步：完成原生 VI 的组件/运行连线和自动测试；若原生 API 覆盖不足，准确记录限制，不将外部独立界面冒充 LabVIEW 前面板。

## 结果与遗留

### 2026-11-18 · 用户暂停与恢复停点

- 用户明确要求：“先暂停吧，computeruse额度耗费太快了”。已立即停止界面输入和开发，不继续联线、测试、构建、GitHub 更新或推送。
- 已写入 `labview/Othello_Basic/src/` 的协议、串口、原 C 规则模拟包装、会话与棋盘组件；已实际通过 36 项基础自动检查，包括 CRC、分帧、过期状态、完整模拟对局和重连。无实机验收。
- 真实 `Main.vi` 和 `.lvproj` 已创建；Main.vi 仍为模板。界面上曾放置空 .NET 容器，尚未绑定组件、完成运行连线或保存该界面改动，不能宣称 LabVIEW 上位机已完成。
- 用户允许少量收尾界面操作后，曾尝试组件绑定；停在“选择 .NET 控件”对话框。sky 输入因模态窗口激活失败；cua 截图还出现了错误的窗口内容。没有继续点击，不操作用户其他窗口。
- 本轮新增/修改尚未 Git 提交或推送；保留在 `task/9-labview-basic` 工作区。用户新增 ZIP/MCU main 文件仍未纳入本任务。
- 恢复时先检查真实当前窗口与工作区，不复用截图、窗口句柄或 UI 坐标。优先文件/API；需要再用 computer-use 时先按用户新的额度/偏好确认范围。
- 待处理：完成原生 VI 组件绑定与运行连线；整理正式文档；核对 `tools/build-labview.ps1` 对新增 `System.Web.Extensions.dll` 自动化 helper 引用；完成真实 VI 运行验证；补 Git/Issue/PR 留痕。

真实板卡串口/点阵/键盘联调不在无硬件验证范围内。用户没有授权本轮暂停后继续实施，等待明确恢复。

### 2026-11-18 · 暂停期间的只读状态核查

- 用户询问：“main.c的文件已经替换。后续需要改吗？以及可视化界面完成了吗？”本次只核查，不恢复开发、不使用 computer-use、不修改程序。
- 实际工程 `firmware/Othello_Basic/Core/Src/main.c` 仍为 5233 bytes / 184 行的 CubeMX 初始化版，main 循环为空；Git 中该文件未出现修改。现有 ZIP 中对应 main.c 同样为 5233 bytes，尚无 OT_Init 等应用调用。
- 新输入 `基础功能部分1/main.c_黑白棋基础功能.c` 为 199 行，包含 OT_Init、OT_Place、OT_Protocol_*、Keypad_*、WS2812_* 调用与回调。这份文件不是当前工程 Core/Src/main.c 的内容；如用户替换的是其它目录，不能据此声称当前工作区已经替换。
- 新输入依赖 keypad.h/ws2812.h 及实现，当前工作区未找到这些模块；它使用大写 OT_* 接口，而既有核心是 ot_init/ot_move/ot_command 等小写接口，需统一数据类型/接口并补驱动后才能判断可编译和实际运行。未进行编译或硬件测试。
- 上位机 Main.vi 仍为 9764-byte 模板，原生组件绑定和运行连线未完成；36 项测试通过的是独立协议/模拟/组件逻辑，不是完整 LabVIEW 可视化界面验收。
- 为遵守每次核查留痕约定，仅提交这份日志，程序与未完成上位机代码继续保留本地，不纳入此次日志提交。整个开发任务仍为 paused。

### 2026-11-18 · 上位机技术路线咨询（不启动迁移）

- 用户询问绑定/连线是否必须 computer-use，以及是否有 Agent 可直接操作的其它上位机方案。
- NI 官方资料确认 VI Scripting 可以程序化创建控件和连线；此前未完成的是本机原生 VI 生成/绑定链路，不能表述为 LabVIEW 技术上必须依赖界面自动化。
- 建议优先考虑独立 C# WinForms 上位机：可复用已有 C# 棋盘/串口/会话组件、原 C 模拟下位机及 36 项测试，直接通过源码和编译脚本维护，去掉 LabVIEW 容器绑定步骤。其它候选为 C#/WPF（XAML）与 Python/PySide6（QSerialPort）。
- 原任务书图片没有限定 LabVIEW，LabVIEW 属于参考方案选型；此处仅给出建议，没有批准或实施技术路线变更。
- 官方依据：NI https://www.ni.com/docs/en-US/bundle/labview/page/tutorial-wiring-objects-using-vi-scripting.html ；Microsoft https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/ 、https://learn.microsoft.com/en-us/dotnet/desktop/wpf/xaml/ ；Qt https://doc.qt.io/qtforpython-6/PySide6/QtSerialPort/QSerialPort.html 。
- 无 computer-use 调用、无程序修改、无依赖安装；开发继续暂停，仅提交咨询日志留痕。

### 2026-11-18 · PA8 与 LED 模块接线咨询

- 只读核对当前 .ioc：PA8 标记 LED_DATA，配置 TIM1_CH1 PWM，PSC=0、ARR=89，DMA1_Channel2；它是点阵数据输出，不是供电引脚。
- PA8 是数据输出，点阵信号接 DIN/IN。保留数据线串联电阻、独立 5V 供电与共地要求。实际模块的 DIN 输入门限仍待核验。
- 本次为只读接线咨询，未进行实物验证。

### 2026-11-19 · 恢复开发与设计变更

- 用户明确要求继续基础 LabVIEW。沿用 #9 和分支 `task/9-labview-basic`，保留用户新增的下位机改动。
- 按用户要求撤销此前指定的电平转换芯片选型，清理总体方案、CubeMX 指南和本日志的相关内容。仅改当前文件，不重写历史。实际 DIN 电平兼容性仍属实物核验项。
- 重新核对协议 V1，现有下位机文件与上位机都使用 GET、MOVE、STATE、ERROR。已有仿真和界面组件可复用，原生 Main.vi 绑定和运行仍待验收。

- 修复构建脚本对 LabVIEW 自动化工具缺少的 `System.Web.Extensions` 引用，模拟器静态链接 MSVC 运行库并从程序集目录加载。用当前工作区的棋规源文件重跑测试，在项目目录和独立构建目录下都通过 39 项检查。检查数从此前 36 增至 39，是因为当前工作区的棋规改动使遍历测试的对局更长。该棋规改动不计入本次提交。
- 使用 LabVIEW ActiveX 的 `OpenFrontPanel` 与标准窗口状态还原 Main.vi。通过界面工具打开本项目的“选择 .NET 控件”对话框，点击浏览时返回 `failed to activate captured window`。已请求用户完成这一处选择。界面绑定、连线和实际原生运行保持待验收。
- 开发中的源码和项目文件提交到 #9 的任务分支供恢复。PR 使用待审状态，依赖 CubeMX 分支，避免把 CubeMX 文件重复算作上位机改动。
- 从初始上位机提交 `2c7fde8` 导出干净副本，用相同脚本重新构建。以提交中的棋规源码通过 36 项自动检查，确认公开分支可独立重建。

### 2026-11-19 · 面板独立验证与原生绑定复查

- 用户再次要求继续 LabVIEW。核对本地及草稿 PR #24 的已有成果。`Main.vi` 仍是上次保存的模板，前面板被“选择 .NET 控件”的模态对话框禁用。
- 此次试用键盘定位同样报 `failed to activate captured window`。ActiveX 查询 `AllVIsInMemory` 报 LabVIEW `0x408` 访问拒绝，原生方法仍能查询项目 VI 的执行状态。已请用户只完成当前对话框的程序集选择，原生连线与运行验收继续待办。
- 将自动测试扩展到可嵌入的真实 .NET 棋盘控件，验证打开模拟会话、提交合法落子、刷新棋盘和结束会话。测试使用应用自己的按钮事件，无电脑界面操作。现有本地 DLL 与当前棋规通过 46 项，其中新增 7 项面板检查。
- 用上一次从提交导出并构建的上位机组件和原始棋规重编新版测试，43 项通过。工作区额外 3 项源于其棋规造成的模拟对局步数差异。
- 已在工程说明中补充启动节点、轮询退出条件和模拟验收步骤。组件测试不替代原生 Main.vi 运行和实机联调。

### 2026-11-19 · DLL 打开提示核查

- 用户截图显示 LabVIEW 对 `Othello.Basic.dll` 报“无法找到打开该类型文件的应用程序”，提示符合把组件当成工程文件直接打开的情况。
- 用 32 位 .NET 读取并加载实际程序集，确认 `Othello.Basic` 为 x86，`Othello.Basic.OthelloPanel` 是公开的 `System.Windows.Forms.UserControl`，具备公共无参构造函数。该验证尚未涉及 LabVIEW 容器绑定。
- README 增加精确选取路径和成功标志。用户应从前面板容器的「插入 .NET 控件 → 浏览」选择程序集。Main.vi 绑定、框图连线和实机联调仍待验证。

### 2026-11-19 · 区分原生模拟库与 .NET 棋盘组件

- 用户提供的最新截图已进入「选择 .NET 控件」。异常点名 `OthelloSimulator.dll`，提示模块应包含程序集清单。用程序集元数据读取验证 `Othello.Basic.dll` 可识别，`OthelloSimulator.dll` 触发同类异常，符合原生库被 .NET 加载器读取的情形。
- 在 #9 范围内调整构建输出，棋盘程序集留在 `bin`，模拟库移入 `bin/native`。离线模式按棋盘程序集的位置加载模拟库。若旧构建在 `bin` 顶层留有模拟 DLL，成功构建后把它移入忽略的 `build/labview` 备份。README 补充选取文件的说明。
- `tools/build-labview.ps1` 生成新的组件和模拟库。复查 `bin` 顶层只有一个 DLL，即 `Othello.Basic.dll`。从 `build/labview` 启动 `BasicTests.exe`，当前工作区棋规下的协议、模拟对局与面板交互共 46 项检查通过。本轮没有操作 LabVIEW 界面。Main.vi 绑定、运行和实机联调仍待验证。

### 2026-11-19 · 续做 Main.vi 连线与离线验收

- 用户确认已完成棋盘组件选择，授权继续 #9 的基础上位机任务。目标是在项目原有 Main.vi 中保存绑定，接入面板的启动和退出流程，并验证无板卡时可落子、退出和重新运行。
- 磁盘上的 Main.vi 仍是模板的大小和时间戳。以 ActiveX 查询到该路径的 VI 前面板在已打开状态。先核对并保留当前会话的编辑，再完成框图。优先脚本，必要时使用已获允许的少量界面操作。
- 前面板可访问性树出现「黑白棋 / OTHELLO」「请运行 Main.vi」及棋盘组件的串口控件，确认控件已在当前会话内实例化。核对时重新打开组件选择对话框，窗口被模态禁用，`Ctrl+S` 返回 `failed to activate captured window`。已请用户关闭该弹窗，以保留已绑定控件。
- 已备份磁盘上的 Main.vi，编译并发起 ActiveX `SaveInstrument`，待模态解除后复查。独立 `BasicTests.exe` 再次通过 46 项。此时结果仍不包含 VI 的框图运行验收。
- 复查弹窗确实选中 `Othello.Basic` 下的 `OthelloPanel`。窗口列表未提供可单独控制的弹窗，按弹窗截图点击「取消」仍返回 `failed to activate captured window`。磁盘文件仍为 9,764 字节。保留 LabVIEW 会话和待保存编辑；弹窗解除后先核实保存，再接 `Start()`、While 循环和 `Stopped`，最后执行离线运行验收。
