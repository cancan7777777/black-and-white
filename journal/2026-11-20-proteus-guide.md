# Proteus 仿真电路连接指南

- 用户要求在项目根目录交付一份可按步骤操作的 Proteus 指南，涵盖器件命名、作用、接线与软件操作。对应 GitHub [#3](https://github.com/cancan7777777/black-and-white/issues/3)，起点 `d2c5285`。以发布分支的 `.ioc`、GPIO/点阵/键盘源码和当前 HEX 为准，参考[基础工程审查](2026-11-18-basic-audit-cubemx.md)及[最新固件](2026-11-20-unified-egaroucid.md)。
- 实施范围为指南、资料核对和离线验证。用本机 Proteus 8.17 SP2 库及示例确认搜索名和模型属性，分别覆盖键盘、点阵、串口和可选 LabVIEW 联调。现有未提交文件、实物接线、烧录和本机安装配置保留原状。

## 核对与验证

- 本机 `D:\Proteus 8 Professional\BIN\PDS.EXE` 版本为 8.17 SP2。只读核对 `CM3_STM32.LIB`、`DISPLAY.LIB`、`ACTIVE.LIB` 及安装示例，确认 `STM32F103C8`、`WS2812X8X8`、`WS2812X8`、`KEYPAD-SMALLCALC` 和 `COMPIM` 名称，确认主控的 `OSC Frequency` 属性、两个串口工具的波特率预设。用 Labcenter 官方教程和 ST/Worldsemi 数据手册核对供电及仿真步骤。
- 源码和指南逐项核对 11 路有效信号、按键行列、点阵颜色、PWM 周期、像素映射和相对链接。`build/Othello_Unified_EN.hex` 与 Keil 项目的 HEX SHA-256 一致。逐行检查 Intel HEX 校验、文件结束记录及 C8 Flash 地址范围 `0x08000000` 至 `0x08004137`。
- 首轮脚本计算串口 CRC 时未先扩展输入字节，改用项目实现复算，并通过 `LineBuffer` 拼合离线模拟器的分段回包。`@SYNC5,1,ADA1` 返回 `@GAME5`，`@GET,1,2792` 返回 `@STATE`。实测项目模拟器初始 2:2，D3 落子后 4:1，D4 由白变黑。
- 交付[根目录指南](../Proteus仿真电路连接指南.md)，在 `task/3-proteus-guide` 记录，仅含说明及本日志。文中的 Proteus 动画、波形与虚拟 COM 联动是验收步骤，尚未在 `.pdsprj` 中运行。
