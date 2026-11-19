using System;
using System.Collections.Generic;
using System.Globalization;

namespace Othello.Basic {
    public sealed class HostSession : IDisposable {
        ITransport transport;
        readonly LineBuffer lines=new LineBuffer();
        readonly Queue<string> log=new Queue<string>();
        uint nextSequence=1, pending;
        DateTime deadline;
        DateTime nextPoll;
        int generation;
        string syncCommand;
        bool pendingRefresh, computerSupported;
        public BoardState State { get; private set; }
        public bool Connected { get { return transport!=null; } }
        public bool Waiting { get { return pending!=0; } }
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
                device.Failed+=delegate(string error) { Dispatch(delegate { if(session==generation) Report(error); }); };
                State=null; pending=0; pendingRefresh=false; computerSupported=false; syncCommand="@SYNC2";
                nextPoll=DateTime.UtcNow; lines.Clear();
                Report("已连接，正在读取完整棋盘"); Refresh();
            } catch { device.Dispose(); transport=null; throw; }
        }
        public void Disconnect() {
            generation++; pending=0; State=null; lines.Clear();
            ITransport old=transport; transport=null;
            if(old!=null) old.Dispose();
            Message="未连接"; Notify();
        }
        void Request(string body,bool refreshing=false) {
            if(transport==null) throw new InvalidOperationException("请先连接");
            if(pending!=0) throw new InvalidOperationException("正在等待下位机确认，请勿重复发送");
            uint seq=nextSequence++; if(nextSequence==0) nextSequence=1;
            pending=seq; pendingRefresh=refreshing; deadline=DateTime.UtcNow.AddMilliseconds(1500);
            string frame=Protocol.Frame(body.Replace("{seq}",seq.ToString(CultureInfo.InvariantCulture)));
            AddLog("TX "+frame.TrimEnd());
            try { transport.Send(frame); Notify(); } catch { pending=0; throw; }
        }
        public void Refresh() { Request(syncCommand+",{seq}",true); }
        public void Move(int row,int col) {
            if(State==null) throw new InvalidOperationException("尚未同步棋盘");
            if(State.Status!=0) throw new InvalidOperationException("本局已结束");
            if(State.Match!=null && State.Match.Phase!=GamePhase.Running) throw new InvalidOperationException("当前状态不能落子");
            if(State.ComputerTurn) throw new InvalidOperationException("请等待电脑走白");
            CheckCoordinates(row,col);
            Request(string.Format(CultureInfo.InvariantCulture,"{0},{{seq}},{1},{2},{3}",State.Match==null?"@MOVE":"@PLAY",State.Revision,row,col));
        }
        static void CheckCoordinates(int row,int col) {
            if(row<0||row>7||col<0||col>7) throw new ArgumentOutOfRangeException("row","坐标必须在 1–8");
        }
        void RequireAdvanced() {
            if(State==null || State.Match==null) throw new InvalidOperationException("请连接支持进阶的下位机");
        }
        public void Control(GameControl command, GameMode mode=GameMode.Casual, int limitSeconds=0) {
            RequireAdvanced();
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
            if(pending!=0 && now>=deadline) { pending=0; Report("等待响应超时，刷新后可核对当前棋盘"); }
            bool computer=State!=null && State.ComputerTurn;
            bool timed=State!=null && State.Match!=null && State.Match.Mode==GameMode.Timed && State.Match.Phase==GamePhase.Running;
            if(Connected && pending==0 && (computer || timed) && now>=nextPoll) {
                nextPoll=now.AddMilliseconds(computer?150:500);
                try { Refresh(); } catch(Exception error) { Report(error.Message); }
            }
        }
        void OnLine(string line) {
            AddLog("RX "+line.TrimEnd());
            try {
                string[] fields=Protocol.Fields(line);
                if(fields[0]=="@GAME2" || fields[0]=="@GAME" || fields[0]=="@STATE") {
                    BoardState state=Protocol.ParseState(fields);
                    if(state.Sequence!=0 && state.Sequence!=pending) { Report("忽略不属于当前请求的 STATE"); return; }
                    if(State!=null && state.Revision<State.Revision) { Report("忽略过期棋盘版本；重连后重新同步"); return; }
                    if(state.Match==null && State!=null && State.Match!=null) return;
                    if(fields[0]=="@GAME2") computerSupported=true;
                    state.ComputerOpponent=computerSupported && state.Match!=null && state.Match.Mode==GameMode.Challenge;
                    State=state;
                    if(state.Sequence!=0 && state.Sequence==pending) {
                        if(pendingRefresh) syncCommand=computerSupported?"@SYNC2":state.Match!=null?"@SYNC":"@GET";
                        pending=0;
                    }
                    Message=Describe(state);
                    Notify();
                } else if(fields[0]=="@ERROR" && fields.Length==3) {
                    uint seq=Protocol.Number(fields[1]);
                    if(seq!=pending && seq!=0) { Report("忽略旧请求 ERROR"); return; }
                    string reason=fields[2];
                    if(seq==0 && pending!=0) { Report("下位机报告无序号错误："+reason); return; }
                    if(seq!=0 && pendingRefresh && reason=="COMMAND" && syncCommand!="@GET") {
                        pending=0;
                        syncCommand=syncCommand=="@SYNC2" ? "@SYNC" : "@GET";
                        Refresh();
                        return;
                    }
                    pending=0;
                    Report(reason=="ILLEGAL"?"非法落子：必须至少翻转对方 1 枚棋子":reason=="STALE"?"版本已过期，正在刷新棋盘":
                        reason=="COMPUTER_TURN"?"请等待电脑走白":"下位机错误："+reason);
                    if(reason=="STALE") Refresh();
                } else Report("忽略未知或格式错误的响应");
            } catch(FormatException ex) { Report(ex.Message); }
        }
        static string Describe(BoardState state) {
            if(state.Match!=null) {
                switch(state.Match.Result) {
                    case GameResult.Win: return "WIN · 闯关总分恰好 188 分";
                    case GameResult.Over: return "OVER · 连续输 2 局";
                    case GameResult.Time: return "计时结束，已结算";
                    case GameResult.Exit: return "已结束退出，本局不计分";
                }
                if(state.Match.Phase==GamePhase.Paused) return "已暂停";
                if(state.Match.Phase==GamePhase.Editing) return "正在编辑棋盘，普通落子已暂停";
            }
            if(state.ComputerTurn) return "电脑执白，正在落子…";
            return state.Status==1 ? "本局结束（一方无合法位置）" : "已同步，等待"+(state.Turn==1?"黑方":"白方")+"落子";
        }
        public void Dispose() { Disconnect(); }
    }
}
