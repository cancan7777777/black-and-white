# 黑白棋基础功能第一阶段源码包

已实现可移植 C 核心：初始棋盘、合法落子、八方向翻转、回合、任务书结束规则、计数、CRC 串口帧解析及版本检查。没有 AI、进阶模式和任何创新功能。

- `Core/Inc`、`Core/Src`：可复制到 CubeMX 生成工程的 C 文件；不是完整固件。
- `docs/CubeMX配置指南.md`：逐步配置及候选引脚，硬件未确认前仅供离线配置参考。
- `docs/通信与LabVIEW.md`：V1协议、LabVIEW9 VI 设计与 HAL 接入契约。
- `tests/test.c`：核心测试。
- `EViews/`：可选离线统计，不参与运行。

PC测试命令（GCC）：
```
gcc -std=c99 -Wall -Wextra -Werror -I Core/Inc Core/Src/othello.c Core/Src/ot_protocol.c tests/test.c -o othello_test
./othello_test
```
Windows 下可在有 GCC 的终端生成 `othello_test.exe` 后运行。当前 Linux GCC 测试通过；未在 Keil、CubeMX、LabVIEW 9 或真实 STM32 板上验证，未生成可烧录固件。不能把本包直接烧录到 STM32。

待确认：LED 第三引脚、供电、电平转换、键盘排针、独立 USB-TTL 及板载晶振实际规格。确认后补齐 HAL LED、键盘和串口驱动、main.c 集成及烧录验证。
