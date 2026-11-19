# V1/V2 固件与基础主程序对照

- 状态：已完成代码核对和验证
- 任务：#22，继续使用 task/22-basic-main 与 PR #23
- 日期：2026-11-19（Asia/Shanghai）

## 目标与依据

用户要求结合 D:/V1.hex 和 D:/V2.hex 修改本地 firmware 的 main.c。两份 HEX 作为固件数据做静态比对。回查 11 月 18 日基础主程序和 CubeMX 记录，以及本地《调试记录》，逐项核对源码与镜像。采用 V2 的终局行为，保留本地已有的直排点阵和配色修正。编译与回归通过后，将修正记录到现有固件 PR。

## 进度

### 对照镜像和工作区

- Intel HEX 记录校验通过。V1 与 V2 分别覆盖 0x08000000 起的 13536 / 13540 字节。
- 本地编译产物中 main 的 112 字节与 V2 完全一致。V1 的对应函数在 0x08002C1C，V2 在 0x08002C28，差一字节位于 Thumb BL 指令低半字，对应函数位置变化。SystemClock_Config 也与 V2 一致。
- 本地编译产物的 AppBasic_HandleKey 与 ot_move 与 V2 完全一致。V2 新增终局后除重开键外的按键门控，并仅检查新行棋方还能否落子。源码已在本地 firmware 中存在，但固件 PR 分支仍是旧实现。
- V1 和 V2 均采用直排点阵。本地另有配色、光标提亮修正，其 LED 渲染代码与参考镜像不同。保留这组本地参数及可配置映射。
- 本地 task/9-labview-basic 有正在进行的变更。直接在本地 main 的 USER CODE 区说明调度职责，将已核对的四个固件模块同步到独立 task/22-basic-main，避开上位机和原始参考包的改动。

### 代码与验证

- 在 main.c 的 USER CODE 区增加分层说明，原调度顺序保留。同步 app_config.h、app_platform.c、app_basic.c 和 othello.c 现有修正，五个文件在本地和任务 worktree 的 SHA-256 一致。
- 主机应用与模拟外设测试通过。新增非对称残局回归，分别验证刚走完一方无棋可走时继续，以及轮到的一方无棋可走时终局。验证终局锁定方向和落子键、重开键仍生效。
- 本地 Keil 编译 0 错误、0 警告，Code 13068、RO 416、RW 64、ZI 6872。重新生成的 Othello_Basic.hex 与此前本地固件 SHA-256 相同。
- 本轮产物的 main、SystemClock_Config、AppBasic_HandleKey、ot_move 与 V2 对应机器码一致。LED 渲染保留本地修正，整份 HEX 与 V2 有差异。
- V1/V2 源文件哈希复核一致。实机显示、键盘排针和串口联调仍需实测。
