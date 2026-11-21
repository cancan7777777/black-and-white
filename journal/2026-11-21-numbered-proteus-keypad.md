# Proteus 键帽编号与实体键盘对齐

- 用户明确要求原理图上的按钮实际显示 `1`～`16`，与实体 `S1`～`S16` 同位置同动作。关联矩阵按键任务 [#6](https://github.com/cancan7777777/black-and-white/issues/6)，基线 `db7f23c`。
- 核对截图中计算器键帽和从左到右的 `4、3、2、1` 列端。只读查看已装 Proteus 的键盘模型、`app_platform.c` 扫描顺序和 `AppBasic_HandleKey` 键位。采用原生 `BUTTON` 加图面数字标注，复用原 4 行 4 列和现有固件。
- 在根目录指南写入新键帽编号、八条网络和防止左右列接反的检查。工作区内未找到截图的 `.pdsprj` 或 `.dsn`，本次交付为接线与绘图说明，仿真运行待在用户工程内核对。
- 用脚本逐项核验指南的编号按行递增为 1～16，八个 GPIO 网名与 `main.h` 一致，扫描顺序为 `scan_row * 4u + col`。`git diff --check` 通过。

## 现成仿真器件核查

- 用户澄清要查询能直接使用的单个 Proteus 器件。判断条件为 4×4 可点击矩阵、4 行加 4 列共 8 脚、键帽直接标 1～16。前面的 `BUTTON` 做法属于自制，撤回它作为本次问题的答复。
- 只读核对本机 Proteus 8.17 SP2，用 `rg --text` 检索安装库的键盘型号，并解析 `ACTIVE.LIB` 的按键交点配置。`KEYPAD-SMALLCALC` 符合 4×4 和 8 脚，键帽使用计算器字符。`KEYPAD-PHONE` 是 4×3，`KEYPAD-CALCULATOR` 是更大的计算器布局，`KEYPAD-TRELLIS-C1` 的按键交点使用 A～P 端和 1、2 端，无法作为 4 行加 4 列的对应器件。
- 核对第三方发布者的[膜键盘库说明](https://www.theengineeringprojects.com/2026/10/membrane-keypad-library-for-proteus-v1-0.html)。其 4×4 型号有 8 个行列端子，演示使用数字、A～D、`*` 和 `#`，资料未列出键帽为 1～16 的型号或设置。
- 本机标准库和已查的第三方资料均未确认有符合三个条件的现成器件。本次只提交核查日志。

## 实体键盘的 8 根信号线

- 用户询问现有 4×4 矩阵键盘的 8 根线是否应接连续的 PB 引脚。只读对照 `Othello_Basic.ioc`、`Core/Inc/main.h`、`Core/Src/app_platform.c` 和根目录仿真接线表，确认四行依次接 `PB0`、`PB1`、`PB10`、`PB11`，四列依次接 `PB12`、`PB13`、`PB14`、`PB15`。
- STM32 逐行驱动并读取带上拉的列，8 根信号线对应 4 路输出和 4 路输入。实体模块排针顺序需以其行列丝印或断电通断测量为准。本次只核对工程定义，未做实物测量。

## 空白键帽器件核查

- 用户放宽外观条件，接受空白键帽，仍需现成可点击的 4×4、4 行加 4 列八脚器件。再次对照 Proteus 8.17 SP2 的 `ACTIVE.LIB` 模型定义，并通过 Windows 的 HTML Help 存储接口读取本机 `HELP/KEYPAD.chm` 的 `Drawing Graphics` 和 `Keypad Properties` 两节。
- 官方帮助说明键帽文字画在元件的二维图形中，模型属性定义可点击区域和可选的电脑键盘快捷键。`KEYPAD-SMALLCALC` 的 16 项 `SQUARE` 属性只有坐标和尺寸，因此清空快捷键也不会隐藏原有计算器图案。
- 本机标准库未找到可直接选用的空白八脚型号。`KEYPAD-TRELLIS-C1` 的交点端子分组与四行四列不同，已查的第三方膜键盘说明也未列出空白键帽选项。本次只提交核查记录。
