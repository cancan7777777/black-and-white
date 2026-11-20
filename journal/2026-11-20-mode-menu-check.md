# LabVIEW AI 与计时菜单核查

- 用户询问两个 AI 入口是否重复，以及计时模式是否来自任务书。沿用 [#12](https://github.com/cancan7777777/black-and-white/issues/12) 和分支 `task/11-challenge-ai-keys`。查阅按键实现记录、[任务书 A03](../docs/requirements.md)及 [进阶规则](../docs/protocol-advanced.md)。
- STM32 AI 与 Egaroucid 都是玩家执黑、电脑执白的一局人机对弈。前者由 STM32 固件执行，实体 S15 可切换，离线模拟运行同一棋规代码。后者由本机 LabVIEW 调用 Egaroucid 控制台求解。闯关也由 STM32 执白，另有四关及累计分规则。
- 任务书进阶下位机第（2）条要求计时模式计算规定时间内得分。当前实现为双人共享整局倒计时，LabVIEW 设置 1–3600 秒，默认 180 秒。新版实体 S11 使用最近设定时长。自然终局或时间到零时结算黑棋数，暂停和编辑冻结倒计时。
- 本次只读对照菜单、固件和说明，保留原有未提交修改。
