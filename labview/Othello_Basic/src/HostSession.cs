using System;
using System.Collections.Generic;
using System.Globalization;

namespace Othello.Basic {
    public sealed class HostSession : IDisposable {
        enum RequestKind { Command, Refresh, Poll }
        ITransport transport;
        readonly LineBuffer lines=new LineBuffer();
        readonly Queue<string> log=new Queue<string>();
        uint nextSequence=1, pending, supersededPoll;
        RequestKind pendingKind;
        DateTime deadline;
        DateTime nextPoll;
        int generation;
        string syncCommand;
        bool computerSupported, stagesSupported, keyboardAiSupported, externalAiSupported, needsSync;
        public BoardState State { get; private set; }
        public bool Connected { get { return transport!=null; } }
        public bool Waiting { get { return pending!=0 && pendingKind!=RequestKind.Poll; } }
        public bool Synchronized { get { return Connected && State!=null && !needsSync; } }
        public bool SupportsKeyboardAi { get { return Synchronized && keyboardAiSupported; } }
        public bool SupportsExternalAi { get { return Synchronized && externalAiSupported; } }
        public string Message { get; private set; }
        public event Action Changed;
        public Action<Action> Dispatch = delegate(Action action) { action(); };
        public HostSession() {
            lines.Line+=OnLine;
            lines.Error+=delegate(string error) { Report(error); };
            Message="未连接";
        }
        public string LogText { get { return string.Join(Environment.NewLine,log.ToArray()); } }
        void AddLog(string text) { log.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff")+" "+text); while(log.Count>150) log.Dequeue(); }
        void Notify() { if(Changed!=null) Changed(); }
        public void Report(string text) { Message=text; AddLog(text); Notify(); }
        public void Connect(ITransport device) {
            try { device.Open(); AttachOpened(device); } catch { device.Dispose(); throw; }
        }
        public void AttachOpened(ITransport device) {
            Disconnect();
            int session=++generation;
            try {
                transport=device;
                device.Received+=delegate(string chunk) { Dispatch(delegate { if(session==generation && transport==device) lines.Feed(chunk); }); };
                device.Failed+=delegate(string error) { Dispatch(delegate {
                    if(session!=generation) return;
                    needsSync=true;
                    Report(error);
                }); };
                State=null;
                computerSupported=stagesSupported=keyboardAiSupported=externalAiSupported=false;
                needsSync=true; syncCommand="@SYNC7";
                nextPoll=DateTime.UtcNow; lines.Clear();
                Report("已连接，正在读取完整棋盘"); Refresh();
            } catch { device.Dispose(); transport=null; throw; }
        }
        public void Disconnect() {
            generation++; pending=supersededPoll=0; State=null; needsSync=true; lines.Clear();
            ITransport old=transport; transport=null;
            if(old!=null) old.Dispose();
            Message="未连接"; Notify();
        }
        void Request(string body,RequestKind kind=RequestKind.Command) {
            if(transport==null) throw new InvalidOperationException("请先连接");
            if(Waiting || (kind==RequestKind.Poll && pending!=0))
                throw new InvalidOperationException("正在等待下位机确认，请勿重复发送");
            // A read-only poll can be superseded. Its reply must not replace the player's confirmed action.
            if(pending!=0) supersededPoll=pending;
            uint seq=nextSequence++; if(nextSequence==0) nextSequence=1;
            if(seq==supersededPoll) supersededPoll=0;
            pending=seq; pendingKind=kind; deadline=DateTime.UtcNow.AddMilliseconds(1500);
            string frame=Protocol.Frame(body.Replace("{seq}",seq.ToString(CultureInfo.InvariantCulture)));
            if(kind!=RequestKind.Poll) AddLog("TX "+frame.TrimEnd());
            try {
                transport.Send(frame);
                if(kind!=RequestKind.Poll) Notify();
            } catch { pending=0; needsSync=true; throw; }
        }
        public void Refresh() { Request(syncCommand+",{seq}",RequestKind.Refresh); }
        public void Move(int row,int col) {
            RequireSynchronized();
            if(State.Status!=0) throw new InvalidOperationException("本局已结束");
            if(State.Match!=null && State.Match.Phase!=GamePhase.Running) throw new InvalidOperationException("当前状态不能落子");
            if(State.ComputerTurn || State.ExternalAiTurn) throw new InvalidOperationException("请等待电脑走白");
            CheckCoordinates(row,col);
            Request(string.Format(CultureInfo.InvariantCulture,"{0},{{seq}},{1},{2},{3}",State.Match==null?"@MOVE":"@PLAY",State.Revision,row,col));
        }
        internal void EngineMove(int row,int col) {
            RequireSynchronized();
            if(!State.ExternalAiTurn) throw new InvalidOperationException("棋盘当前没有等待 EN 落子");
            CheckCoordinates(row,col);
            Request(string.Format(CultureInfo.InvariantCulture,"@AI,{{seq}},{0},{1},{2}",State.Revision,row,col));
        }
        static void CheckCoordinates(int row,int col) {
            if(row<0||row>7||col<0||col>7) throw new ArgumentOutOfRangeException("row","坐标必须在 1–8");
        }
        void RequireSynchronized() {
            if(!Synchronized) throw new InvalidOperationException("棋盘尚未同步，请先刷新棋盘");
        }
        void RequireAdvanced() {
            RequireSynchronized();
            if(State.Match==null) throw new InvalidOperationException("请连接支持进阶的下位机");
        }
        public void Control(GameControl command, GameMode mode=GameMode.Casual, int limitSeconds=0) {
            RequireAdvanced();
            if(command==GameControl.Mode && mode==GameMode.Ai && !SupportsKeyboardAi)
                throw new InvalidOperationException("当前固件不支持旧版板载 AI 模式");
            if(command==GameControl.Mode && mode==GameMode.ExternalAi && !SupportsExternalAi)
                throw new InvalidOperationException("请更新下位机固件以使用实体键启动 AI 对弈");
            Request(string.Format(CultureInfo.InvariantCulture,"@CTL,{{seq}},{0},{1},{2},{3}",
                State.Revision,command.ToString().ToUpperInvariant(),(int)mode,limitSeconds));
        }
        public void Edit(int row,int col,int piece) {
            RequireAdvanced();
            CheckCoordinates(row,col);
            if(piece<0||piece>2) throw new ArgumentOutOfRangeException("piece");
            Request(string.Format(CultureInfo.InvariantCulture,"@EDIT,{{seq}},{0},{1},{2},{3}",State.Revision,row,col,piece));
        }
        public void Tick() {
            DateTime now=DateTime.UtcNow;
            if(pending!=0 && now>=deadline) {
                if(pendingKind==RequestKind.Poll) supersededPoll=pending;
                pending=0;needsSync=true;
                Report("等待棋盘同步超时，请刷新棋盘或核对串口设置");
            }
            bool computer=State!=null && State.ComputerTurn;
            bool timed=State!=null && State.Match!=null && State.Match.Mode==GameMode.Timed && State.Match.Phase==GamePhase.Running;
            if(Connected && pending==0 && (computer || timed) && now>=nextPoll) {
                nextPoll=now.AddMilliseconds(computer?150:500);
                try { Request(syncCommand+",{seq}",RequestKind.Poll); } catch(Exception error) { Report(error.Message); }
            }
        }
        void OnLine(string line) {
            try {
                string[] fields=Protocol.Fields(line);
                if(fields[0]=="@GAME7" || fields[0]=="@GAME6" || fields[0]=="@GAME5" || fields[0]=="@GAME4" || fields[0]=="@GAME3" || fields[0]=="@GAME2" ||
                    fields[0]=="@GAME" || fields[0]=="@STATE") {
                    BoardState state=Protocol.ParseState(fields);
                    if(state.Sequence!=0 && state.Sequence==supersededPoll) return;
                    bool poll=state.Sequence!=0 && state.Sequence==pending && pendingKind==RequestKind.Poll;
                    bool announce=!poll || !Synchronized || state.Revision!=State.Revision;
                    if(announce) AddLog("RX "+line.TrimEnd());
                    if(state.Sequence!=0 && state.Sequence!=pending) { Report("忽略不属于当前请求的 STATE"); return; }
                    bool requestedRefresh=state.Sequence!=0 && pendingKind!=RequestKind.Command;
                    if(State!=null && state.Revision<State.Revision && !requestedRefresh) {
                        if(state.Sequence!=0) { pending=0;needsSync=true; }
                        Report("忽略旧棋盘版本，板卡重启后可刷新棋盘");
                        return;
                    }
                    if(state.Match==null && State!=null && State.Match!=null) return;
                    if(fields[0]=="@GAME7" || fields[0]=="@GAME6" || fields[0]=="@GAME5") externalAiSupported=stagesSupported=computerSupported=true;
                    if(fields[0]=="@GAME4" && !externalAiSupported) keyboardAiSupported=stagesSupported=computerSupported=true;
                    if(fields[0]=="@GAME3") stagesSupported=computerSupported=true;
                    if(fields[0]=="@GAME2") computerSupported=true;
                    state.ComputerOpponent=computerSupported && state.Match!=null &&
                        (state.Match.ActiveMode==GameMode.Challenge || state.Match.ActiveMode==GameMode.Ai);
                    if(state.Match!=null && state.Match.Result==GameResult.Time) {
                        int loser=GameArchive.TimeoutLoser(state.Match.BlackRemainingMs,state.Match.WhiteRemainingMs);
                        if(loser==0 && State!=null && State.Match!=null && State.Match.Mode==GameMode.Timed &&
                            State.Match.Round==state.Match.Round) {
                            if(State.Match.Result==GameResult.Time && State.Revision==state.Revision && State.Cells==state.Cells)
                                loser=State.Match.TimeoutSide;
                            else if(State.Match.Phase==GamePhase.Running)
                                loser=state.TurnBeforeTimeout(State);
                        }
                        state.Match.TimeoutSide=loser;
                    }
                    State=state;
                    needsSync=false;
                    if(state.Sequence!=0 && state.Sequence==pending) {
                        if(requestedRefresh) syncCommand=fields[0]=="@GAME7"?"@SYNC7":fields[0]=="@GAME6"?"@SYNC6":externalAiSupported?"@SYNC5":keyboardAiSupported?"@SYNC4":stagesSupported?"@SYNC3":
                            computerSupported?"@SYNC2":state.Match!=null?"@SYNC":"@GET";
                        pending=0;
                    }
                    if(announce) Message=Describe(state);
                    Notify();
                } else if(fields[0]=="@ERROR" && fields.Length==3) {
                    uint seq=Protocol.Number(fields[1]);
                    if(seq!=0 && seq==supersededPoll) return;
                    AddLog("RX "+line.TrimEnd());
                    if(seq!=pending && seq!=0) { Report("忽略旧请求 ERROR"); return; }
                    string reason=fields[2];
                    if(seq==0 && pending!=0) { Report("下位机报告无序号错误："+reason); return; }
                    if(seq!=0 && pendingKind!=RequestKind.Command && reason=="COMMAND" && syncCommand!="@GET") {
                        pending=0;
                        syncCommand=syncCommand=="@SYNC7"?"@SYNC6":syncCommand=="@SYNC6"?"@SYNC5":syncCommand=="@SYNC5"?"@SYNC4":syncCommand=="@SYNC4"?"@SYNC3":syncCommand=="@SYNC3"?"@SYNC2":
                            syncCommand=="@SYNC2"?"@SYNC":"@GET";
                        Refresh();
                        return;
                    }
                    pending=0;
                    if(reason=="STALE") needsSync=true;
                    Report(reason=="ILLEGAL"?"非法落子：必须至少翻转对方 1 枚棋子":reason=="STALE"?"版本已过期，正在刷新棋盘":
                        reason=="COMPUTER_TURN"?"请等待电脑走白":reason=="STANDBY"?"点阵待机，请按实体 S4 开局":"下位机错误："+reason);
                    if(reason=="STALE") Refresh();
                } else {
                    AddLog("RX "+line.TrimEnd());
                    Report("忽略未知或格式错误的响应");
                }
            } catch(FormatException ex) {
                AddLog("RX "+line.TrimEnd());
                Report(ex.Message);
            }
        }
        static string Describe(BoardState state) {
            if(state.Standby) return "点阵待机，请按实体 S4 开局";
            if(state.Match!=null) {
                string challengeExit=state.Match.ActiveMode==GameMode.Casual?"，闯关已退出":"";
                switch(state.Match.Result) {
                    case GameResult.Win: return "WIN · 已达 188 分"+challengeExit;
                    case GameResult.Over: return (state.Match.Losses==2 ? "OVER · 两连败" : "OVER · 四关未达 188 分")+challengeExit;
                    case GameResult.Time: {
                        int loser=state.Match.TimeoutSide;
                        return loser==1?"计时结束 · 黑方超时判负":loser==2?"计时结束 · 白方超时判负":"计时结束 · 胜方未知";
                    }
                    case GameResult.Exit: return "已结束退出，本局不计分";
                }
                if(state.Match.Phase==GamePhase.Paused) return "已暂停";
                if(state.Match.Phase==GamePhase.Editing) return "正在编辑棋盘，普通落子已暂停";
            }
            if(state.ComputerTurn) return "电脑执白，正在落子…";
            if(state.ExternalAiTurn) return "等待 Egaroucid 应手…";
            return state.Status==1 ? "本局结束（一方无合法位置）" : "已同步，等待"+(state.Turn==1?"黑方":"白方")+"落子";
        }
        public void Dispose() { Disconnect(); }
    }
}
