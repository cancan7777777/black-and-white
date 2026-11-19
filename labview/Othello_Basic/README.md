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
| `bin/BasicTests.exe` | 在无板卡条件下测试协议和模拟对局 |

## 操作与验收

绑定时，在 LabVIEW 前面板的 .NET 容器中插入 `bin/Othello.Basic.dll` 里的 `Othello.Basic.OthelloPanel`。运行框图应先调用控件的 `Start()`，然后定时读取 `Stopped` 作为循环退出条件。面板的“结束上位机”按钮断开会话并设置退出状态。绑定与连线待在当前安装环境验证。 [NI 前面板 .NET 控件说明](https://www.ni.com/docs/en-US/bundle/labview/page/creating-net-controls-in-a-net-container.html)

- 离线模拟下选择“连接”，开局应显示黑白各两子。从默认开局选第 3 行第 4 列并确认，预期黑 4、白 1，轮到白方。非法落子后棋盘应保持原状。
- 实机模式填写串口参数并连接。默认值是 `115200 8N1`。棋盘通过 `GET` 同步，点击格子只选择坐标，确认后由 `MOVE` 请求落子，收到有效 `STATE` 才更新棋盘。
- 真实 USB-UART 连接、键盘输入与实体点阵的双端一致性由之后的实机联调验证。
