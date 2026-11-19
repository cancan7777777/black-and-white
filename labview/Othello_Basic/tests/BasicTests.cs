using System;
using System.Diagnostics;
using System.IO.Ports;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Othello.Basic;

class FakeTransport : ITransport {
    public string Last;
    public event Action<string> Received;
    public event Action<string> Failed;
    public void Open() { }
    public void Send(string frame) { Last=frame; }
    public void Inject(string frame) { if(Received!=null) Received(frame); }
    public void Fail(string error) { if(Failed!=null) Failed(error); }
    public void Dispose() { }
}
class BasicTests {
    static int checks;
    static void Check(bool value,string description) { checks++;if(!value) throw new Exception("FAIL "+description); }
    static void Reject(Action action,string description) { bool threw=false;try{action();}catch(ArgumentException){threw=true;}catch(FormatException){threw=true;}Check(threw,description); }
    static string InitialBoard() { char[] b=new string('0',64).ToCharArray();b[27]='2';b[28]='1';b[35]='1';b[36]='2';return new string(b); }
    static string State(uint seq,uint rev,string cells,int turn,int status,int black,int white) { return Protocol.Frame("@STATE,"+seq+","+rev+","+turn+","+status+","+black+","+white+","+cells); }
    static T FindControl<T>(Control parent, Predicate<T> match) where T : Control {
        foreach (Control child in parent.Controls) {
            T typed = child as T;
            if (typed != null && match(typed)) return typed;
            T nested = FindControl(child, match);
            if (nested != null) return nested;
        }
        return null;
    }
    static void WaitForPanel(Func<bool> predicate, string description) {
        var elapsed = Stopwatch.StartNew();
        while (!predicate() && elapsed.ElapsedMilliseconds < 3000) {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Check(predicate(), description);
    }
    static void CheckPanel() {
        using (var panel = new OthelloPanel()) {
            panel.CreateControl();
            var connect = FindControl<Button>(panel, button => button.Text == "连接");
            var move = FindControl<Button>(panel, button => button.Text == "确认落子");
            var finish = FindControl<Button>(panel, button => button.Text == "结束上位机");
            var board = FindControl<BoardView>(panel, control => true);
            Check(panel.IsHandleCreated && board != null && connect != null && move != null && finish != null,
                "embedded control instantiates the board and actions");
            panel.Size=panel.MinimumSize;
            Check(connect.Height >= 40 && move.Height >= 40 && finish.Height >= 40,
                "primary actions retain large hit areas at the minimum panel size");
            Check(panel.ClientRectangle.Contains(panel.RectangleToClient(finish.Parent.RectangleToScreen(finish.Bounds))),
                "exit remains fully visible at the minimum panel size");
            Check(panel.Stopped && !connect.Enabled && !move.Enabled, "controls are disabled until VI calls Start");
            panel.Start();
            connect.PerformClick();
            WaitForPanel(() => board.Board != null && move.Enabled, "panel connect displays a confirmed simulated board");
            Check(board.Board.Cells == InitialBoard(), "panel renders the initial game state");
            move.PerformClick();
            WaitForPanel(() => board.Board.Revision == 2, "panel move displays a confirmed change");
            Check(board.Board.Black == 4 && board.Board.White == 1, "panel refresh shows the flipped counts");
            var pause = FindControl<Button>(panel, button => button.Text == "暂停");
            var resume = FindControl<Button>(panel, button => button.Text == "继续");
            pause.PerformClick();
            WaitForPanel(() => board.Board.Match.Phase == GamePhase.Paused && !move.Enabled && resume.Enabled,
                "embedded pause disables moves and offers resume");
            resume.PerformClick();
            WaitForPanel(() => board.Board.Match.Phase == GamePhase.Running && move.Enabled,"embedded resume restores moves");
            var modes=FindControl<ComboBox>(panel, control=>control.Items.Contains("闯关"));
            var apply=FindControl<Button>(panel, button=>button.Text=="设定模式");
            modes.SelectedIndex=1; apply.PerformClick();
            WaitForPanel(()=>board.Board.ComputerOpponent && board.Board.Match.Stage==1 && board.Board.Match.Score==1m && move.Enabled,
                "challenge panel starts at stage one with the half-point coefficient");
            move.PerformClick();
            WaitForPanel(()=>board.Board.ComputerTurn && !move.Enabled,"panel waits for the computer after the black move");
            WaitForPanel(()=>board.Board.Turn==1 && board.Board.Black==3 && board.Board.White==3 && move.Enabled,
                "panel receives the automatic white move without another click");
            finish.PerformClick();
            Check(panel.Stopped && board.Board == null && !move.Enabled, "panel finish stops and clears the session");
        }
    }

    static void CheckAdvanced() {
        using(var host=new HostSession()) {
            var legacy=new FakeTransport();
            host.Connect(legacy);
            Check(legacy.Last.StartsWith("@SYNC3,1,"),"new host probes staged scoring");
            legacy.Inject(Protocol.Frame("@ERROR,1,COMMAND"));
            Check(legacy.Last.StartsWith("@SYNC2,2,"),"stage negotiation falls back to computer opponent");
            legacy.Inject(Protocol.Frame("@ERROR,2,COMMAND"));
            Check(legacy.Last.StartsWith("@SYNC,3,"),"computer negotiation falls back to previous extended protocol");
            legacy.Inject(Protocol.Frame("@ERROR,3,COMMAND"));
            Check(legacy.Last.StartsWith("@GET,4,"),"legacy negotiation falls back to GET");
            legacy.Inject(State(4,1,InitialBoard(),1,0,2,2));
            host.Move(2,3);
            Check(legacy.Last.StartsWith("@MOVE,5,"),"legacy host move format preserved");
        }
        using(var host=new HostSession()) {
            host.Connect(new SimulatedTransport());
            Check(host.State.Match.Mode==GameMode.Casual,"advanced default boot game");
            host.Control(GameControl.Mode,GameMode.Challenge,180);
            Check(host.State.ComputerOpponent && host.State.Match.Stage==1 && host.State.Match.TotalScore==0m,
                "human-versus-computer challenge confirmed");
            host.Control(GameControl.Edit);
            host.Edit(2,3,1);
            Check(host.State.Match.Edited && host.State.Black==3 && host.State.Cells[19]=='1',"edit changes confirmed cell and counts");
            host.Control(GameControl.Edit);
            host.Control(GameControl.End);
            Check(host.State.Match.Result==GameResult.Exit && host.State.Match.Score==0m && host.State.Match.TotalScore==0m,
                "early exit does not score");
            host.Control(GameControl.Start);
            Check(host.State.Cells==InitialBoard() && !host.State.Match.Edited,"start initializes a fresh round");
            host.Control(GameControl.Mode,GameMode.Timed,1);
            host.Control(GameControl.Pause);
            uint paused=host.State.Match.RemainingMs;
            Thread.Sleep(1100);
            host.Refresh();
            Check(host.State.Match.RemainingMs==paused,"paused timer remains frozen past the limit");
            host.Control(GameControl.Resume);
            Thread.Sleep(1100);
            host.Tick();
            Check(host.State.Match.Result==GameResult.Time && host.State.Match.Total==2 && host.State.Match.ElapsedMs==1000,
                "timer settles exactly at limit and credits the current round");
        }
    }

    static void CheckArchive() {
        string folder=Path.Combine(Path.GetDirectoryName(typeof(BasicTests).Assembly.Location),"archive-test-"+Guid.NewGuid().ToString("N"));
        var archive=new GameArchive(folder);
        try {
            using(var host=new HostSession()) {
                host.Changed+=delegate { archive.Observe(host.State,true); };
                host.Connect(new SimulatedTransport());
                host.Move(2,3);
                host.Control(GameControl.End);
                host.Refresh();
                foreach(GameMode mode in Enum.GetValues(typeof(GameMode))) {
                    for(int trial=0;trial<5;trial++) {
                        host.Control(GameControl.Mode,mode,1);
                        if(mode==GameMode.Timed) {
                            if(trial%2==1) host.Move(2,3);
                            Thread.Sleep(1050);
                            host.Refresh();
                        } else {
                            for(int moves=0;host.State.Status==0 && moves<60;moves++) {
                                uint revision=host.State.Revision;
                                if(host.State.ComputerTurn) {
                                    Thread.Sleep(275);
                                    host.Refresh();
                                    if(host.State.Revision==revision) throw new Exception("Computer did not answer the black move");
                                    continue;
                                }
                                for(int i=0;i<64 && host.State.Revision==revision;i++) {
                                    int square=(i+trial*7)%64;
                                    host.Move(square/8,square%8);
                                }
                                if(host.State.Revision==revision) throw new Exception("Archive test could not find a legal move");
                            }
                        }
                        if(host.State.Status!=1) throw new Exception("Archive test round did not finish");
                    }
                }
            }
            int unreadable;
            var reopened=new GameArchive(folder).Load(out unreadable);
            Check(unreadable==0 && reopened.Count==16,"all real simulated rounds reopen with no duplicate on refresh");
            GameRecord exited=reopened.Find(record=>record.Result==GameResult.Exit);
            Check(exited!=null && exited.Complete && exited.Frames.Count==3 && exited.Frames[0].Cells==InitialBoard() &&
                exited.Frames[1].ToBoard().White==1,"replay preserves opening, move and finish");
            foreach(GameMode mode in Enum.GetValues(typeof(GameMode))) {
                var edited=new GameRecord { Mode=mode, Result=GameResult.Board, Score=64, Edited=true };
                reopened.Add(edited);
                var rank=GameArchive.Rank(reopened,mode);
                Check(rank.Count==5 && !rank.Contains(edited) && rank[0].Score>=rank[4].Score && rank.TrueForAll(game=>game.Complete),
                    "per-mode leaderboard uses five complete simulated rounds and excludes edits and exits");
                reopened.Remove(edited);
                string export=Path.Combine(folder,"rank-"+mode+".csv");
                GameArchive.Export(export,rank);
                Check(File.ReadAllLines(export).Length==6,"rank export contains header and five records");
                File.Delete(export);
            }
            var stagedArchive=new GameArchive(folder);
            var opening=Protocol.ParseState(Protocol.Fields(Protocol.Frame("@GAME3,1,1,1,0,2,2,"+InitialBoard()+",1,0,91,142,0,0,0,0,0,180,3,6,0")));
            string finishPayload="@GAME3,0,2,0,1,31,0,"+new string('1',31)+new string('0',33)+",1,3,91,188,0,0,430,0,4,180,3,93,1";
            var settled=Protocol.ParseState(Protocol.Fields(Protocol.Frame(finishPayload)));
            Check(settled.Match.Stage==3 && settled.Match.Score==46.5m && settled.Match.TotalScore==188.5m,
                "staged wire protocol preserves fractional scores exactly");
            Reject(()=>Protocol.ParseState(Protocol.Fields(Protocol.Frame(finishPayload.Replace(",3,93,1",",3,94,1")))),
                "staged wire protocol rejects wrong coefficient scores");
            stagedArchive.Observe(opening,true); stagedArchive.Observe(settled,true);
            string legacyPath=Path.Combine(folder,"legacy.xml");
            File.WriteAllText(legacyPath,"<GameRecord><Id>legacy</Id><Mode>Challenge</Mode><Score>25</Score><Total>100</Total>"+
                "<Frames><ReplayFrame><Cells>"+InitialBoard()+"</Cells></ReplayFrame></Frames></GameRecord>");
            var loaded=stagedArchive.Load(out unreadable);
            var fractional=loaded.Find(record=>record.Stage==3);
            var legacyRecord=loaded.Find(record=>record.Id=="legacy");
            Check(unreadable==0 && fractional!=null && fractional.Score==46.5m && fractional.Total==188.5m &&
                legacyRecord!=null && legacyRecord.Score==25m && legacyRecord.Total==100m,"archive preserves new fractions and reads old integer records");
            string fractionalCsv=Path.Combine(folder,"fractional.csv");
            GameArchive.Export(fractionalCsv,new[]{fractional});
            Check(File.ReadAllLines(fractionalCsv)[1].Contains(",46.5,188.5,"),"CSV preserves both half-point values");
        } finally {
            if(Directory.Exists(folder)) {
                foreach(string file in Directory.GetFiles(folder)) File.Delete(file);
                Directory.Delete(folder);
            }
        }
    }
    [STAThread]
    static int Main() {
        try {
            Check(Protocol.Crc("123456789")==0x29B1,"independent CCITT-FALSE vector");
            Check(Protocol.Fields("@GET,1,2792\n")[0]=="@GET","independent Python binascii GET frame vector");
            Reject(delegate { Protocol.Fields("@GET,1,0000\n"); },"bad CRC");
            Reject(delegate { Protocol.Number("4294967296"); },"U32 overflow");
            Reject(delegate { Protocol.ParseState(Protocol.Fields(State(1,1,new string('0',64),1,0,2,2))); },"inconsistent counts");
            LineBuffer buffer=new LineBuffer();int complete=0,overflows=0;buffer.Line+=delegate {complete++;};buffer.Error+=delegate{overflows++;};
            string valid=State(1,1,InitialBoard(),1,0,2,2);
            buffer.Feed(valid.Substring(0,10));Check(complete==0,"partial frame");buffer.Feed(valid.Substring(10)+valid);Check(complete==2,"fragmented and coalesced frames");
            buffer.Feed(new string('x',300)+"\n"+valid);Check(overflows==1 && complete==3,"bounded receiver recovers after overflow");
            using(HostSession realProtocol=new HostSession()) {
                FakeTransport fake=new FakeTransport();realProtocol.Connect(fake);
                fake.Inject(valid);Check(realProtocol.State.Black==2 && !realProtocol.Waiting,"confirmed initial state");
                string original=realProtocol.State.Cells;
                fake.Inject(State(0,2,InitialBoard(),2,0,2,2).Replace(",2,2,",",3,2,"));Check(realProtocol.State.Cells==original,"corrupt response does not change board");
                realProtocol.Move(2,3);Check(realProtocol.State.Revision==1 && realProtocol.Waiting,"no optimistic move");
                fake.Inject(Protocol.Frame("@ERROR,2,STALE"));Check(fake.Last.StartsWith("@GET,3,"),"STALE sends one GET, never retries MOVE");
                fake.Inject(State(3,2,InitialBoard(),1,0,2,2));Check(realProtocol.State.Revision==2 && !realProtocol.Waiting,"refresh after stale handles reentrant receiver");
                fake.Inject(State(0,1,InitialBoard(),1,0,2,2));Check(realProtocol.State.Revision==2,"older revision ignored");
                realProtocol.Disconnect();Check(realProtocol.State==null && !realProtocol.Connected,"disconnect clears pending state");
            }
            using(HostSession simulation=new HostSession()) {
                simulation.Connect(new SimulatedTransport());
                Check(simulation.State.Cells==InitialBoard() && simulation.State.Revision==1,"native C simulator initial board");
                simulation.Move(2,3);Check(simulation.State.Black==4 && simulation.State.White==1 && simulation.State.Revision==2,"native C legal flip");
                string before=simulation.State.Cells;simulation.Move(0,0);Check(simulation.State.Cells==before && simulation.State.Revision==2,"illegal move unchanged");
                int moves=0;
                while(simulation.State.Status==0 && moves<60) {
                    uint revision=simulation.State.Revision;bool advanced=false;
                    for(int i=0;i<64;i++) { simulation.Move(i/8,i%8);if(simulation.State.Revision!=revision) {advanced=true;break;} }
                    Check(advanced,"one legal move available before terminal state");moves++;
                }
                Check(simulation.State.Status==1 && simulation.State.Turn==0,"full offline game reaches course terminal rule");
                simulation.Disconnect();simulation.Connect(new SimulatedTransport());Check(simulation.State.Revision==1 && simulation.State.Black==2,"reconnect clears old revision");
            }
            Reject(delegate { new SerialSettings { DataBits=9 }.Validate(); },"invalid data bits");
            Reject(delegate { new SerialSettings { DataBits=8,StopBits=StopBits.OnePointFive }.Validate(); },"invalid 1.5 stop bits combination");
            CheckPanel();
            CheckAdvanced();
            CheckArchive();
            Console.WriteLine("PASS "+checks+" checks: framing, modes, editor, timers, replay, ranking, reconnect, panel actions");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error);return 1; }
    }
}
