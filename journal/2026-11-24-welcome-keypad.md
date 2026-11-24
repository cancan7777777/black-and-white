# 上电待机动画和实体键位调整

- 用户要求上电先显示启动界面、同一开关按一次开局再按一次关闭、预落子闪烁色接近实际棋子颜色，并指定 S10 上、S15 左、S12 下、S7 右、S11 落子。关联键盘 [#6](https://github.com/cancan7777777/black-and-white/issues/6) 和点阵 [#5](https://github.com/cancan7777777/black-and-white/issues/5)，基线 `32aedaf`，分支 `task/6-welcome-keypad`。对照[键帽与引脚记录](2026-11-21-numbered-proteus-keypad.md)和[闯关与 AI 验证](2026-11-24-challenge-tuning.md)。
- 使用 S4 切换待机和正常对局。待机显示青色播放图标与橙色跑马边框。对局中按 S4 先按主动退出结算，再回欢迎画面；下一次开机键打开普通新局。S1 切换 EN、S9 切换计时、S3 结束本局、S6 进入下一局或下一关。键盘扫描直接上报物理编号，行列引脚按原接线。
- 空格预落子用当前方的青色或橙色，预览级别 56、棋子常亮级别 48。点阵的每帧刷新继续使用定时器和 DMA，扫描和通信可在动画期间处理。
- `@SYNC7`／`@GAME7` 追加待机标志，LabVIEW 在实体待机时提示按 S4，固件仍响应查询并拒绝改局指令。已完成棋局的结果仍可保存，关掉进行中的棋局记为主动退出。协议回退和 LabVIEW 离线模拟的直接开局路径都已验证。

## 验证

- `tools/build-labview.ps1` 的原生回归覆盖上电空棋盘、待机拒绝串口开局与落子、S4 往返及新局、新方向和落子键、滚动边框像素及黑白预览颜色。原闯关 205.5 分通关与胜方爱心回归通过。上位机 492 项检查通过，包含新协议、待机控件状态、旧协议回退和 Egaroucid 真实整局 52 手。已将两个经校验的 DLL 安装到 `labview/Othello_Basic/bin`，原 DLL 在 `build/labview/welcome-keypad/previous-bin` 备份。
- `tools/build-keil.ps1` 编译 0 错误、0 警告。生成 `build/Othello_Welcome_Keypad.hex`，1111 条 Intel HEX 记录通过校验。SHA-256 为 `C4D9E19E637048B64265AC59F4518221300A7F6CF844CCBD4416AC9F136D5C04`。本次使用离线回归、编译和产物检查，实体上电和点阵观感待实板验证。
