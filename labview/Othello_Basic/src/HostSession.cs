using System;
using System.Collections.Generic;

namespace Othello.Basic {
    public sealed class HostSession : IDisposable {
        ITransport transport;
        readonly LineBuffer lines=new LineBuffer();
        readonly Queue<string> log=new Queue<string>();
        uint nextSequence=1, pending;
        DateTime deadline;
        int generation;
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
                State=null; pending=0; lines.Clear(); Report("已连接，正在读取完整棋盘"); Refresh();
            } catch { device.Dispose(); transport=null; throw; }
        }
        public void Disconnect() {
            generation++; pending=0; State=null; lines.Clear();
            ITransport old=transport; transport=null;
            if(old!=null) old.Dispose();
            Message="未连接"; Notify();
        }
        void Request(string body) {
            if(transport==null) throw new InvalidOperationException("请先连接");
            if(pending!=0) throw new InvalidOperationException("正在等待下位机确认，请勿重复发送");
            uint seq=nextSequence++; if(nextSequence==0) nextSequence=1;
            pending=seq; deadline=DateTime.UtcNow.AddMilliseconds(1500);
            string frame=Protocol.Frame(body.Replace("{seq}",seq.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            AddLog("TX "+frame.TrimEnd());
            try { transport.Send(frame); Notify(); } catch { pending=0; throw; }
        }
        public void Refresh() { Request("@GET,{seq}"); }
        public void Move(int row,int col) {
            if(State==null) throw new InvalidOperationException("尚未同步棋盘");
            if(State.Status!=0) throw new InvalidOperationException("本局已结束");
            if(row<0||row>7||col<0||col>7) throw new ArgumentOutOfRangeException("row","坐标必须在 1–8");
            Request("@MOVE,{seq},"+State.Revision+","+row+","+col);
        }
        public void Tick() { if(pending!=0 && DateTime.UtcNow>=deadline) { pending=0; Report("等待响应超时：未自动重发落子，请刷新状态后重试"); } }
        void OnLine(string line) {
            AddLog("RX "+line.TrimEnd());
            try {
                string[] fields=Protocol.Fields(line);
                if(fields[0]=="@STATE") {
                    BoardState state=Protocol.ParseState(fields);
                    if(state.Sequence!=0 && state.Sequence!=pending) { Report("忽略不属于当前请求的 STATE"); return; }
                    if(State!=null && state.Revision<State.Revision) { Report("忽略过期棋盘版本；重连后重新同步"); return; }
                    State=state;
                    if(state.Sequence==pending) pending=0;
                    Message=state.Status==1 ? "本局结束（一方无合法位置）" : "已同步，等待"+(state.Turn==1?"黑方":"白方")+"落子";
                    Notify();
                } else if(fields[0]=="@ERROR" && fields.Length==3) {
                    uint seq=Protocol.Number(fields[1]);
                    if(seq!=pending && seq!=0) { Report("忽略旧请求 ERROR"); return; }
                    pending=0;
                    string reason=fields[2];
                    Report(reason=="ILLEGAL"?"非法落子：必须至少翻转对方 1 枚棋子":reason=="STALE"?"版本已过期，正在刷新棋盘":"下位机错误："+reason);
                    if(reason=="STALE") Refresh();
                } else Report("忽略未知或格式错误的响应");
            } catch(FormatException ex) { Report(ex.Message); }
        }
        public void Dispose() { Disconnect(); }
    }
}
