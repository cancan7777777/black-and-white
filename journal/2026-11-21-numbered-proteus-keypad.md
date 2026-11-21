# Proteus 键帽编号与实体键盘对齐

- 用户明确要求原理图上的按钮实际显示 `1`～`16`，与实体 `S1`～`S16` 同位置同动作。关联矩阵按键任务 [#6](https://github.com/cancan7777777/black-and-white/issues/6)，基线 `db7f23c`。
- 核对截图中计算器键帽和从左到右的 `4、3、2、1` 列端。只读查看已装 Proteus 的键盘模型、`app_platform.c` 扫描顺序和 `AppBasic_HandleKey` 键位。采用原生 `BUTTON` 加图面数字标注，复用原 4 行 4 列和现有固件。
- 在根目录指南写入新键帽编号、八条网络和防止左右列接反的检查。工作区内未找到截图的 `.pdsprj` 或 `.dsn`，本次交付为接线与绘图说明，仿真运行待在用户工程内核对。
- 用脚本逐项核验指南的编号按行递增为 1～16，八个 GPIO 网名与 `main.h` 一致，扫描顺序为 `scan_row * 4u + col`。`git diff --check` 通过。
