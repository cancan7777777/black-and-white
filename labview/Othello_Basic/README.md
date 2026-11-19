# 黑白棋 LabVIEW 上位机

本项目面向 Windows 上的 LabVIEW 2018 32 位。打开 `Othello_Basic.lvproj` 下的 `Main.vi`，最大化前面板即可完整显示棋盘和按钮。首次从仓库下载时，先按下文构建 VI 使用的 32 位组件。

## 构建与验证

在仓库根目录用 PowerShell 7 执行以下命令。

```powershell
& .\tools\build-labview.ps1
```

脚本调用本机 x86 MSVC 和 .NET Framework 编译器，将程序集与模拟器输出到本工程的 `bin` 目录，并执行自动测试。若 Visual Studio 安装在其他目录，用 `-VisualStudioRoot` 指定现有安装目录。模拟器直接编译仓库里的 C 语言棋规和串口协议，因此它与当次构建使用的核心源码一致。

| 文件 | 用途 |
|---|---|
| `bin/Othello.Basic.dll` | LabVIEW .NET 容器绑定的程序集 |
| `bin/native/OthelloSimulatorV2.dll` | 原生模拟库，由棋盘组件在离线模式下加载 |
| `bin/BasicTests.exe` | 在无板卡条件下测试协议、模拟对局和面板按钮 |

## 操作与验收

`Main.vi` 是可打开的 VI。`bin/Othello.Basic.dll` 是由 LabVIEW 的 .NET 容器加载的组件，双击 DLL 或在 LabVIEW 的「文件 → 打开」中选择它会报文件类型错误。

顶部显示连接方式、COM 号和同步状态，右侧集中显示回合、双方棋子数、得分和操作结果。「新局模式」用于选择下一局的模式，旁边的「设定模式」会开新局。点击左侧棋盘或调节右侧行列选中棋格，再点「确认落子」。闯关计分显示关卡、系数、本关得分和总分。

1. 在 `Main.vi` 的**前面板**右键点击灰色 .NET 容器，选择「插入 .NET 控件」。如果还没有容器，从「控件选板 → .NET 与 ActiveX → .NET 容器」放置一个。
2. 在「选择 .NET 控件」对话框中点「浏览」，选择本工程的 `bin/Othello.Basic.dll`。
3. 在控件列表中选 `OthelloPanel`（完整类型名 `Othello.Basic.OthelloPanel`），点「确定」。绑定成功后容器里会显示棋盘控件。

如果对话框提到模拟 DLL 和“程序集清单”，关闭提示后回到「浏览」，选择 `bin/Othello.Basic.dll`。模拟库位于 `bin/native`，由程序自动加载。[Microsoft 关于程序集清单错误的说明](https://learn.microsoft.com/en-us/dotnet/api/system.badimageformatexception?view=netframework-4.8.1)

已保存的 `Main.vi` 包含以下连线。更换程序集路径时可参考上面的容器绑定步骤。[NI 前面板 .NET 控件说明](https://www.ni.com/docs/en-US/bundle/labview/page/creating-net-controls-in-a-net-container.html)

1. 在框图中将 .NET 容器端子接到循环外的调用节点，选择 `Start()`。
2. 将控件引用和 `Start()` 的错误输出接进 While 循环。每轮等待 100 ms，并通过属性节点读取 `Stopped`。`Stopped` 或错误状态为真时退出。
3. 运行 VI 后点“离线模拟”旁的“连接”。操作完成后点“结束上位机”，按钮会断开会话并设置退出状态。

- 离线开局应显示黑白各两子。从默认开局选第 3 行第 4 列并确认，预期黑 4、白 1，轮到白方。非法落子后棋盘应保持原状。
- 实机模式填写串口参数并连接。默认值是 `115200 8N1`。棋盘通过 `SYNC3` 同步，点击格子选择坐标，确认后由 `PLAY` 请求落子，收到有效棋盘帧后刷新。连接旧固件时可协商原有协议。
- 顶部显示「棋盘待同步」时，会保留最后确认的棋盘供查看。点击「刷新棋盘」重新同步后可恢复落子和对局操作。板卡重启后也可用「刷新棋盘」读取新的开局。
- 真实 USB-UART 连接、键盘输入与实体点阵的双端一致性由之后的实机联调验证。

## 进阶功能

- 连接后可开始、暂停、继续、结束对局、重开本局及切换普通、闯关和计时模式。设定模式会开始新局并清零累计成绩。计时时长支持 1–3600 秒，默认 180 秒。
- 闯关模式由玩家执黑，STM32 自动走白。离线模拟中电脑由同一固件逻辑驱动。「当前回合：电脑（白方）」出现时等待电脑落子。连上新版固件后，计分栏显示关卡、系数、本关分与累计分。回放和排行榜保留半分精度。
- 「开启编辑」可把所选格设为空、黑或白。编辑期间普通落子暂停，退出编辑后恢复进入前的状态。本局分按黑棋数显示，总分和结果由下位机确认。离线模拟使用同一 C 状态机。
- 「回放与排行」可逐步回看已保存棋局，按模式与来源浏览排行并导出 CSV。程序在终局保存 XML 到 `%LOCALAPPDATA%\Othello.Basic\Games`。连接中途加入或漏收动作时，回放标记为部分记录。编辑局和提前退出局可回看，排行统计正式结算的未编辑对局。
- LabVIEW 正在使用旧组件时请保存工程并重新打开。若仍显示旧按钮，重新启动 LabVIEW 会加载新组件。

规则和帧格式见[进阶规则与通信约定](../../docs/protocol-advanced.md)。

## Egaroucid 本机 AI 对弈

1. 运行 `Main.vi`，选「离线模拟」并连接。用实体板时，选择「真实串口」并连接支持进阶命令的固件。
2. 在「AI 设置」中选本机的 Egaroucid Console `.exe`。保留可执行文件和同目录的 `resources`。路径会保存在本地 `bin/EgaroucidPath.txt`。
3. 把「模式」选为 **Egaroucid**，确认 AI 等级，点「设定模式」开始新局。玩家执黑。每次黑方落子确认后，Egaroucid 执白自动应手。AI 等级默认 5，可选 0–60。等级越高，可能越容易超过 10 秒等待上限。
4. 暂停可中断思考，继续时按当前棋盘重新计算。搜索出错或超时会显示原因并恢复手动落子。刷新棋盘后，可重新选择 Egaroucid 模式开始新局。

该选项用普通棋局的规则和存档分类。连接 STM32 时，AI 仍通过原有串口落子命令，由 STM32 核对版本和合法性。用实体键盘时请执黑，等候白方自动应手。切到固件闯关模式后，由 STM32 接管白方。正式棋局沿用一方无合法位置即结束的规则。

本机验证版本是 Egaroucid for Console 7.8.2 Windows x64 AMD AVX512。上位机以进程方式调用引擎，LabVIEW 可继续使用现有 x86 组件。引擎及其 GPL-3.0 资源由用户本地提供。 [Egaroucid 控制台文档](https://www.egaroucid.nyanyan.dev/en/console/) · [7.8.2 发布页](https://github.com/Nyanyan/Egaroucid/releases/tag/console_v7.8.2)

可在原有构建命令上加 `-EgaroucidExecutable <控制台程序完整路径>`，运行实际引擎的整局离线回归测试。
