# 黑白棋基础 LabVIEW 上位机

本项目面向 Windows 上的 LabVIEW 2018 32 位。`Othello_Basic.lvproj` 是工程入口，`Main.vi` 是前面板入口。当前已构建棋盘、串口会话及离线模拟组件，原生 VI 的绑定和运行验收仍在进行。

## 构建与验证

在仓库根目录用 PowerShell 7 执行以下命令。

```powershell
& .\tools\build-labview.ps1
```

脚本调用本机 x86 MSVC 和 .NET Framework 编译器，将程序集与模拟器输出到本工程的 `bin` 目录，并执行自动测试。若 Visual Studio 安装在其他目录，用 `-VisualStudioRoot` 指定现有安装目录。模拟器直接编译仓库里的 C 语言棋规和串口协议，因此它与当次构建使用的核心源码一致。

| 文件 | 用途 |
|---|---|
| `bin/Othello.Basic.dll` | LabVIEW .NET 容器绑定的程序集 |
| `bin/OthelloSimulator.dll` | 离线模拟下位机 |
| `bin/BasicTests.exe` | 在无板卡条件下测试协议、模拟对局和面板按钮 |

## 操作与验收

`Main.vi` 是可打开的 VI。`bin/Othello.Basic.dll` 是由 LabVIEW 的 .NET 容器加载的组件，双击 DLL 或在 LabVIEW 的「文件 → 打开」中选择它会报文件类型错误。

1. 在 `Main.vi` 的**前面板**右键点击灰色 .NET 容器，选择「插入 .NET 控件」。如果还没有容器，从「控件选板 → .NET 与 ActiveX → .NET 容器」放置一个。
2. 在「选择 .NET 控件」对话框中点「浏览」，选择本工程的 `bin/Othello.Basic.dll`。
3. 在控件列表中选 `OthelloPanel`（完整类型名 `Othello.Basic.OthelloPanel`），点「确定」。绑定成功后容器里会显示棋盘控件。

框图连线按以下步骤进行。原生绑定与连线待在当前安装环境验证。[NI 前面板 .NET 控件说明](https://www.ni.com/docs/en-US/bundle/labview/page/creating-net-controls-in-a-net-container.html)

1. 在框图中将 .NET 容器端子接到循环外的调用节点，选择 `Start()`。
2. 将控件引用和 `Start()` 的错误输出接进 While 循环。每轮等待 100 ms，并通过属性节点读取 `Stopped`。`Stopped` 或错误状态为真时退出。
3. 运行 VI 后点“离线模拟”旁的“连接”。操作完成后点“结束上位机”，按钮会断开会话并设置退出状态。

- 离线开局应显示黑白各两子。从默认开局选第 3 行第 4 列并确认，预期黑 4、白 1，轮到白方。非法落子后棋盘应保持原状。
- 实机模式填写串口参数并连接。默认值是 `115200 8N1`。棋盘通过 `GET` 同步，点击格子只选择坐标，确认后由 `MOVE` 请求落子，收到有效 `STATE` 才更新棋盘。
- 真实 USB-UART 连接、键盘输入与实体点阵的双端一致性由之后的实机联调验证。
