# 基础串口与 LabVIEW 9 设计

## 数据协议 V1
ASCII，每帧 LF 结束（接收可容许 CRLF）。CRC-16/CCITT-FALSE，初值 FFFF，多项式 1021，不反射，xorout 0000，覆盖从 @ 开始到 CRC 前最后一个逗号之前的字节（不包括该逗号）。CRC 为4位十六进制。参考测试串 123456789 → 29B1。

- 请求 `@GET,seq,CRC\n`，获取棋盘。
- 请求 `@MOVE,seq,revision,row,col,CRC\n`，落子。row/col 均 0~7，seq 为非零 U32。
- 响应 `@STATE,seq,revision,turn,status,black,white,board64,CRC\n`。
- 错误 `@ERROR,seq,reason,CRC\n`（CRC 错误时 seq=0）。
- 实体落子后主动发 STATE，seq=0。上电、重连时 GET。硬件适配尚未写入。

棋盘行优先：index=row*8+col。0空，1黑，2白。turn=0结束、1黑、2白；status=0运行、1结束。revision 从1开始，每次合法落子+1。错误原因 LENGTH/CRC/FORMAT/STALE/ILLEGAL/COMMAND。收到 STALE 时重新 GET，不能自动把相同落子应用到新局面。重启后 revision 恢复1；端口重连应清除旧的未决请求并同步状态。

实体 STM32 为棋盘权威源。上位机鼠标落子仅发送 MOVE，等待 STATE 后更新。非法落子不改变棋盘。任一方无合法落子即结束，按任务书字面实现，而不是标准跳过回合规则（待教师确认）。

## LabVIEW 9 基础 VI
前面板：2D Picture（虚拟棋盘/鼠标操作）、VISA Resource Name、波特率、数据位、停止位、校验位可选、连接/断开、刷新、当前回合、黑白棋数量、状态和错误提示。默认 115200 8N1。不做 AI 或进阶功能。下位机初始参数固定，修改上位机串口参数必须匹配实际固件。

程序框图：UI 事件循环 → 消息队列 → 串口工作循环；串口循环独占 VISA 引用，通过队列返回 STATE 给界面。VISA Configure Serial Port 开启 LF 终止符，VISA Read 使用有限超时/长度，维护跨读取缓冲并校验 CRC。VISA Write 必须包含实际 LF。退出 VISA Close。坐标从 Picture 控件鼠标位置减棋盘左上角并除以格宽/高，先检查边界，再取整。

建议 VI：Main.vi、Serial_Manager.vi、CRC16.vi、Frame_Parser.vi、Board_Render.vi、Board_Click.vi。当前未生成实际 .vi；在 LabVIEW 9 中搭建并实测。

## HAL 接入契约
main 初始化后调用 ot_init(&game)。UART 接收端按 LF 拼装完整帧；缓冲溢出丢弃至下一 LF；在主循环而非 ISR 调用 ot_command(&game,line,out,sizeof out)，正长度才发送。键盘确认坐标后主循环调用 ot_move；成功时调用 ot_state(&game,0,out,sizeof out) 并发送。两个来源均在同一主循环串行处理，避免共享数据竞态。每次 game.revision 变化时刷新点阵，状态到 LED 像素映射在硬件确认后定义。UART ISR 只负责收字节/入队，不调用阻塞发送。
