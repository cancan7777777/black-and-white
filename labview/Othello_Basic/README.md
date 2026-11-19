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

1. 在 `Main.vi` 的**前面板**右键点击灰色 .NET 容器，选择「插入 .NET 控件」。如果还没有容器，从「控件选板 → .NET 与 ActiveX → .NET 容器」放置一个。
2. 在「选择 .NET 控件」对话框中点「浏览」，选择本工程的 `bin/Othello.Basic.dll`。
3. 在控件列表中选 `OthelloPanel`（完整类型名 `Othello.Basic.OthelloPanel`），点「确定」。绑定成功后容器里会显示棋盘控件。

如果对话框提到模拟 DLL 和“程序集清单”，关闭提示后回到「浏览」，选择 `bin/Othello.Basic.dll`。模拟库位于 `bin/native`，由程序自动加载。[Microsoft 关于程序集清单错误的说明](https://learn.microsoft.com/en-us/dotnet/api/system.badimageformatexception?view=netframework-4.8.1)

已保存的 `Main.vi` 包含以下连线。更换程序集路径时可参考上面的容器绑定步骤。[NI 前面板 .NET 控件说明](https://www.ni.com/docs/en-US/bundle/labview/page/creating-net-controls-in-a-net-container.html)

1. 在框图中将 .NET 容器端子接到循环外的调用节点，选择 `Start()`。
2. 将控件引用和 `Start()` 的错误输出接进 While 循环。每轮等待 100 ms，并通过属性节点读取 `Stopped`。`Stopped` 或错误状态为真时退出。
3. 运行 VI 后点“离线模拟”旁的“连接”。操作完成后点“结束上位机”，按钮会断开会话并设置退出状态。

- 离线开局应显示黑白各两子。从默认开局选第 3 行第 4 列并确认，预期黑 4、白 1，轮到白方。非法落子后棋盘应保持原状。
- 实机模式填写串口参数并连接。默认值是 `115200 8N1`。棋盘通过 `SYNC2` 同步，点击格子选择坐标，确认后由 `PLAY` 请求落子，收到有效棋盘帧后刷新。连接旧固件时可协商原有协议。
- 真实 USB-UART 连接、键盘输入与实体点阵的双端一致性由之后的实机联调验证。

## 进阶功能

- 连接后可开始、暂停、继续、结束对局、重开本局及切换普通、闯关和计时模式。设定模式会开始新局并清零累计成绩。计时时长支持 1–3600 秒，默认 180 秒。
- 闯关模式由玩家执黑，STM32 自动走白。离线模拟中电脑由同一固件逻辑驱动。「当前回合：电脑（白方）」出现时等待电脑落子。连上新版固件后计分栏显示「闯关（人机）」。
- 「开启编辑」可把所选格设为空、黑或白。编辑期间普通落子暂停，退出编辑后恢复进入前的状态。本局分按黑棋数显示，总分和结果由下位机确认。离线模拟使用同一 C 状态机。
- 「回放与排行」可逐步回看已保存棋局，按模式与来源浏览排行并导出 CSV。程序在终局保存 XML 到 `%LOCALAPPDATA%\Othello.Basic\Games`。连接中途加入或漏收动作时，回放标记为部分记录。编辑局和提前退出局可回看，排行统计正式结算的未编辑对局。
- LabVIEW 正在使用旧组件时请保存工程并重新打开。若仍显示旧按钮，重新启动 LabVIEW 会加载新组件。

规则和帧格式见[进阶规则与通信约定](../../docs/protocol-advanced.md)。
