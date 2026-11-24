# 计时模式超时方判负

- 用户反馈计时模式结果错误：应当超时的一方判负，与盘面上哪种颜色子多无关。关联闯关与对局控制 [#11](https://github.com/cancan7777777/black-and-white/issues/11)，基线 `4f0619d`，分支 `task/11-timeout-loss`。参考[分棋钟实现](2026-11-20-separate-game-clocks.md)、[胜方标签](2026-11-20-winner-labels.md)、[计时面板轮询](2026-11-21-timed-panel-polling.md)和[闯关难度调整](2026-11-24-easier-opening-stages.md)。
- 旧规则在[分棋钟](2026-11-20-separate-game-clocks.md)中记录为「任一时钟到零立即结算，继续按用户已确认的子数判定胜负」。本次用户明确改为超时方判负，该条旧结论作废；其余结束方式仍按子数判定，闯关计分仍使用 `game.black`。
- 根因在胜方判定只看子数：`app_led_winner()` 用 `game.black` 与 `game.white` 比较，`OT_RESULT_TIME` 与普通终局走同一条路径。因此超时方子数多时，点阵爱心颜色和上位机文案都会指向超时的一方。

## 方案

- 固件新增 `ot_match_winner()`，把「谁赢」收敛到一处：仅在 `OT_FINISHED` 且结果不是 `NONE`／`EXIT` 时给出胜方。`OT_RESULT_TIME` 比较 `remaining_ms[0]`、`remaining_ms[1]` 哪个为 0，双方同时到零按平局；其他结果仍比较子数。删除头文件里的 `app_led_winner()`，改由快照字段传递，避免显示层各写一份规则。
- 上位机新增 `GameArchive.TimeoutLoser()`、`TimedOutcome()` 和带棋钟参数的 `Outcome()` 重载，面板状态行、终局展望、`HostSession` 结算提示、回放标签、排行榜与 CSV 的「结果」列统一走这一套。
- 回放需要历史棋钟，`GameRecord` 增加 `BlackRemainingMs`／`WhiteRemainingMs`，在 `Observe` 唯一落盘点写入。旧 XML 记录缺少该字段时反序列化为 null，回退到子数判定，不会把老数据判错。
- 协议不改：`@GAME6`／`@GAME7` 已同时携带两个棋钟，超时结算时恰好一个为 0，上位机可自行推导超时方。

## 验证

- `tools/build-labview.ps1` 通过。原生回归 `PASS native match and winning LED heart` 覆盖黑棋 40:20 超时判白胜、双零平局、重新开局恢复 1000 ms、白棋 20:40 超时判黑胜，以及到期前棋钟不被提前清零。上位机 `PASS 159 checks`，新增面板级断言：注入黑棋 40:20 且黑方超时的 `@GAME6` 终局帧后，状态行显示「白棋胜」、棋钟显示「黑棋 00:00    白棋 00:01」；同时校验该局落盘时两个棋钟都写入存档，且 `GameArchive.Outcome()` 从存档还原出「白棋胜」而不是按 40:20 判黑胜。`CheckOutcomes()` 补充等子数超时、子数占优仍超时判负、双零回退子数、旧记录无棋钟回退平局四类用例。
- `tools/build-keil.ps1` 编译 0 错误、0 警告，`Code=17224 RO-data=728 RW-data=96 ZI-data=8128`。
- 已更新 `build/Othello_Welcome_Keypad.hex`，1131 条 Intel HEX 记录校验全部通过，SHA-256 为 `6CC44231AC9C57B591D7E9C1DE60C22819735DDFE3AEFBBB9559713AF097AF75`，文件长度 50811 字节。

## 遗留

- 实体板需重新烧录本 HEX 后观察：计时到零时点阵爱心应为未超时方的颜色，上位机状态行与终局展望应显示「X 方超时判负」。当前仅有离线回归与编译产物验证，未经实板确认。
