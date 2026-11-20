using System;
using System.Diagnostics;
using System.IO.Ports;
using System.IO;
using System.Text;
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
public class EngineConfigProbe : MarshalByRefObject {
    public string AssemblyLocation() { return typeof(OthelloPanel).Assembly.Location; }
    public string ConfigLocation() { return EgaroucidEngine.ConfigPath; }
}
class BasicTests {
    static int checks;
    static void Check(bool value,string description) { checks++;if(!value) throw new Exception("FAIL "+description); }
    static void Reject(Action action,string description) { bool threw=false;try{action();}catch(ArgumentException){threw=true;}catch(FormatException){threw=true;}Check(threw,description); }
    static string InitialBoard() { char[] b=new string('0',64).ToCharArray();b[27]='2';b[28]='1';b[35]='1';b[36]='2';return new string(b); }
    static string State(uint seq,uint rev,string cells,int turn,int status,int black,int white) { return Protocol.Frame("@STATE,"+seq+","+rev+","+turn+","+status+","+black+","+white+","+cells); }
    static string ExternalState(uint seq,BoardState board) {
        return Protocol.Frame("@GAME5,"+seq+","+board.Revision+","+board.Turn+",0,"+board.Black+","+board.White+","+
            board.Cells+","+(int)board.Match.Mode+",0,"+board.Match.Round+",0,0,0,0,0,0,180,0,"+board.Black*2+",0");
    }
    static T FindControl<T>(Control parent, Predicate<T> match) where T : Control {
        foreach (Control child in parent.Controls) {
            T typed = child as T;
            if (typed != null && match(typed)) return typed;
            T nested = FindControl(child, match);
            if (nested != null) return nested;
        }
        return null;
    }
    static void WaitForPanel(Func<bool> predicate, string description, int timeoutMs=3000) {
        var elapsed = Stopwatch.StartNew();
        while (!predicate() && elapsed.ElapsedMilliseconds < timeoutMs) {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Check(predicate(), description);
    }

    static void WaitForUi(int milliseconds) {
        var elapsed=Stopwatch.StartNew();
        while(elapsed.ElapsedMilliseconds<milliseconds) {
            Application.DoEvents();
            Thread.Sleep(5);
        }
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
            Check(modes.Items.Count==4 && modes.Items.Contains("AI 对弈"),
                "there is one AI entry alongside casual, challenge and timed modes");
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

    static void CheckSerialPanel() {
        var serial=new FakeTransport();
        SerialSettings actual=null;
        string folder=Path.Combine(Path.GetTempPath(),"Othello-Serial-"+Guid.NewGuid().ToString("N"));
        using(var panel=new OthelloPanel(new GameArchive(folder),settings=>{ actual=settings;return serial; })) {
            panel.CreateControl();
            panel.Start();
            var method=FindControl<ComboBox>(panel,c=>c.Items.Contains("真实串口"));
            var port=FindControl<ComboBox>(panel,c=>c.DropDownStyle==ComboBoxStyle.DropDown);
            var baud=FindControl<ComboBox>(panel,c=>c.Items.Contains("57600"));
            var bits=FindControl<ComboBox>(panel,c=>c.Items.Contains("7"));
            var parity=FindControl<ComboBox>(panel,c=>c.Items.Contains("Even"));
            var stops=FindControl<ComboBox>(panel,c=>c.Items.Contains("1.5"));
            Check(!port.Enabled && !baud.Enabled,"offline panel does not suggest applying COM settings");
            method.SelectedItem="真实串口";
            Check(port.Enabled && baud.Enabled && bits.Enabled && parity.Enabled && stops.Enabled,
                "choosing real serial exposes all five serial settings");
            port.Text="COM37";baud.SelectedItem="57600";bits.SelectedItem="7";parity.SelectedItem="Even";stops.SelectedItem="2";
            var connect=FindControl<Button>(panel,b=>b.Text=="连接");
            var move=FindControl<Button>(panel,b=>b.Text=="确认落子");
            var refresh=FindControl<Button>(panel,b=>b.Text=="刷新棋盘");
            var disconnect=FindControl<Button>(panel,b=>b.Text=="断开");
            var board=FindControl<BoardView>(panel,b=>true);
            connect.PerformClick();
            WaitForPanel(()=>serial.Last!=null,"serial panel starts handshake");
            Check(actual!=null && actual.Port=="COM37" && actual.Baud==57600 && actual.DataBits==7 &&
                actual.Parity==Parity.Even && actual.StopBits==StopBits.Two,"serial panel passes the selected parameters to its transport");
            Check(!move.Enabled && board.Board==null &&
                FindControl<Label>(panel,l=>l.Text=="COM37 · 正在同步")!=null,"opening COM does not confirm a board");
            WaitForPanel(()=>refresh.Enabled && FindControl<Label>(panel,l=>l.Text=="COM37 · 棋盘待同步")!=null,
                "handshake timeout is shown separately from an open port");
            refresh.PerformClick();
            serial.Inject(State(2,1,InitialBoard(),1,0,2,2));
            WaitForPanel(()=>move.Enabled && board.Board!=null,"the first valid refresh unlocks moves");
            Check(FindControl<Label>(panel,l=>l.Text=="COM37 · 棋盘已同步")!=null,"link badge identifies the synchronized COM port");
            move.PerformClick();
            WaitForPanel(()=>refresh.Enabled && !move.Enabled,"move timeout leaves the board read-only");
            Check(board.Board.Cells==InitialBoard() && serial.Last.StartsWith("@MOVE,3,1,2,3,"),
                "the timed-out command is not sent twice or applied optimistically");
            serial.Inject(State(3,1,InitialBoard(),1,0,2,2));
            WaitForUi(50);
            Check(!move.Enabled,"late reply to an expired sequence cannot unlock the board");
            refresh.PerformClick();
            serial.Inject(State(4,1,InitialBoard(),1,0,2,2));
            WaitForPanel(()=>move.Enabled,"explicit refresh restores the previous position safely");
            serial.Fail("串口异常");
            WaitForPanel(()=>!move.Enabled && refresh.Enabled,"transport failure requires resynchronization");
            serial.Inject(State(0,1,InitialBoard(),1,0,2,2));
            WaitForPanel(()=>move.Enabled,"valid spontaneous board transmission restores synchronization");
            serial.Inject(State(0,9,InitialBoard(),1,0,2,2));
            WaitForPanel(()=>board.Board.Revision==9,"serial panel receives a board broadcast");
            serial.Inject(State(0,1,InitialBoard(),1,0,2,2));
            WaitForUi(50);
            Check(board.Board.Revision==9,"old unsolicited state cannot roll back the board");
            refresh.PerformClick();
            serial.Inject(State(5,1,InitialBoard(),1,0,2,2));
            WaitForPanel(()=>board.Board.Revision==1 && move.Enabled,"an explicit fresh snapshot recovers after board reboot");
            serial.Inject(Protocol.Frame("@GAME4,0,2,1,0,2,2,"+InitialBoard()+",3,0,2,0,0,0,0,0,0,180,0,4,0"));
            WaitForPanel(()=>board.Board.Match!=null && board.Board.Match.Mode==GameMode.Ai && board.Board.ComputerOpponent,
                "the panel recognizes an unsolicited AI mode change from the device");
            disconnect.PerformClick();
            Check(board.Board==null && port.Enabled && port.Text=="COM37","disconnect clears the board and preserves serial settings");
        }
    }

    static void CheckShadowCopy() {
        string bin=Path.GetDirectoryName(typeof(BasicTests).Assembly.Location);
        string cache=Path.Combine(bin,"shadow-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        AppDomain domain=null;
        try {
            domain=AppDomain.CreateDomain("OthelloShadowCopy",null,new AppDomainSetup {
                ApplicationBase=bin,ShadowCopyFiles="true",CachePath=cache,ApplicationName="OthelloTest" });
            var probe=(EngineConfigProbe)domain.CreateInstanceAndUnwrap(typeof(EngineConfigProbe).Assembly.FullName,
                typeof(EngineConfigProbe).FullName);
            Check(!string.Equals(Path.GetDirectoryName(probe.AssemblyLocation()),bin,StringComparison.OrdinalIgnoreCase),
                "the separate AppDomain loads the component from a shadow-copy cache");
            Check(string.Equals(probe.ConfigLocation(),Path.Combine(bin,"EgaroucidPath.txt"),StringComparison.OrdinalIgnoreCase),
                "shadow-copied component reads the original project engine configuration");
        } finally {
            if(domain!=null) AppDomain.Unload(domain);
            Directory.Delete(cache,true);
        }
    }

    static void CheckPhysicalEgaroucid(string enginePath,GameArchive archive) {
        var serial=new FakeTransport();
        using(var model=new HostSession())
        using(var panel=new OthelloPanel(archive,settings=>serial)) {
            model.Connect(new SimulatedTransport());
            panel.CreateControl();
            panel.Start();
            FindControl<ComboBox>(panel,c=>c.Items.Contains("真实串口")).SelectedItem="真实串口";
            FindControl<ComboBox>(panel,c=>c.DropDownStyle==ComboBoxStyle.DropDown).Text="COM37";
            FindControl<Button>(panel,b=>b.Text=="连接").PerformClick();
            var move=FindControl<Button>(panel,b=>b.Text=="确认落子");
            var refresh=FindControl<Button>(panel,b=>b.Text=="刷新棋盘");
            var board=FindControl<BoardView>(panel,b=>true);
            WaitForPanel(()=>serial.Last!=null && serial.Last.StartsWith("@SYNC6,1,"),"serial panel requests the current firmware protocol");
            serial.Inject(Protocol.Frame("@ERROR,1,COMMAND"));
            WaitForPanel(()=>serial.Last.StartsWith("@SYNC5,2,"),"serial panel can fall back to the previous AI protocol");
            serial.Inject(ExternalState(2,model.State));
            WaitForPanel(()=>board.Board!=null && move.Enabled,"serial panel confirms the ordinary boot board");

            File.WriteAllText(EgaroucidEngine.ConfigPath,Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".exe"));
            model.Control(GameControl.Mode,GameMode.ExternalAi,180);
            serial.Inject(ExternalState(0,model.State));
            WaitForPanel(()=>board.Board.Match.Mode==GameMode.ExternalAi,"S15 mode broadcast enters the same AI mode");
            model.Move(2,3);
            serial.Inject(ExternalState(0,model.State));
            WaitForPanel(()=>board.Board.ExternalAiTurn && !move.Enabled &&
                FindControl<Label>(panel,l=>l.Text.Contains("EN 未落子"))!=null,"without EN the physical white turn stays reserved");
            Check(serial.Last.StartsWith("@SYNC5,2,") && board.Board.Revision==3,"missing engine sends no fallback move");

            File.WriteAllText(EgaroucidEngine.ConfigPath,enginePath);
            refresh.PerformClick();
            serial.Inject(ExternalState(3,model.State));
            WaitForPanel(()=>serial.Last.StartsWith("@AI,4,3,"),"real EN returns a versioned white move after S15",12000);
            string[] command=Protocol.Fields(serial.Last);
            uint before=model.State.Revision;
            model.EngineMove((int)Protocol.Number(command[3]),(int)Protocol.Number(command[4]));
            Check(model.State.Revision!=before && model.State.Turn==1,"the real device state machine accepts the EN move");
            serial.Inject(ExternalState(4,model.State));
            WaitForPanel(()=>board.Board.Turn==1 && board.Board.Black==3 && board.Board.White==3 && move.Enabled,
                "the verified reply returns control to black");
            model.Control(GameControl.Mode,GameMode.Casual,180);
            serial.Inject(ExternalState(0,model.State));
            WaitForPanel(()=>board.Board.Match.Mode==GameMode.Casual && move.Enabled,"S15 exit restores ordinary play");
        }
    }

    static void CheckEngineInputEncodings(string enginePath) {
        char[] cells=InitialBoard().ToCharArray();
        cells[19]=cells[27]='1';
        var board=new BoardState(0,8,2,0,4,1,new string(cells));
        var original=Console.InputEncoding;
        try {
            foreach(var encoding in new Encoding[]{Encoding.GetEncoding(936),new UTF8Encoding(true)}) {
                Console.InputEncoding=encoding;
                int cell=new EgaroucidEngine(enginePath,5).FindWhiteMove(board,CancellationToken.None);
                Check(cell==18||cell==20||cell==34,"real EN returns a legal white move with input code page "+encoding.CodePage);
            }
        } finally {
            Console.InputEncoding=original;
        }
    }

    static void CheckEgaroucid(string enginePath) {
        string config=EgaroucidEngine.ConfigPath;
        byte[] previousConfig=File.Exists(config)?File.ReadAllBytes(config):null;
        string folder=Path.Combine(Path.GetTempPath(),"Othello-Egaroucid-"+Guid.NewGuid().ToString("N"));
        try {
            var archive=new GameArchive(folder);
            using(var panel=new OthelloPanel(archive)) {
                panel.CreateControl();
                panel.Start();
                var connect=FindControl<Button>(panel,b=>b.Text=="连接");
                var disconnect=FindControl<Button>(panel,b=>b.Text=="断开");
                var apply=FindControl<Button>(panel,b=>b.Text=="设定模式");
                var move=FindControl<Button>(panel,b=>b.Text=="确认落子");
                var pause=FindControl<Button>(panel,b=>b.Text=="暂停");
                var resume=FindControl<Button>(panel,b=>b.Text=="继续");
                var start=FindControl<Button>(panel,b=>b.Text=="开始");
                var board=FindControl<BoardView>(panel,b=>true);
                var modes=FindControl<ComboBox>(panel,c=>c.Items.Contains("AI 对弈"));
                var row=FindControl<NumericUpDown>(panel,n=>n.Maximum==8);
                var col=FindControl<NumericUpDown>(panel,n=>n.Maximum==8&&n!=row);
                connect.PerformClick();
                WaitForPanel(()=>board.Board!=null && apply.Enabled,"offline board connects before Egaroucid");
                modes.SelectedItem="AI 对弈";
                File.WriteAllText(config,Path.Combine(folder,"missing.exe"));
                uint openingRevision=board.Board.Revision;
                apply.PerformClick();
                Check(board.Board.Revision==openingRevision && move.Enabled &&
                    FindControl<Label>(panel,l=>l.Text.Contains("选择 Egaroucid 控制台程序"))!=null,
                    "missing engine leaves the existing manual game usable");

                File.WriteAllText(config,enginePath);
                apply.PerformClick();
                WaitForPanel(()=>board.Board.Match.Mode==GameMode.ExternalAi &&
                    FindControl<Label>(panel,l=>l.Text.Contains("AI 对弈 · 玩家执黑"))!=null,
                    "the single menu entry starts EN on the authoritative game");
                move.PerformClick();
                WaitForPanel(()=>board.Board.ExternalAiTurn && !move.Enabled,"white belongs to Egaroucid");
                WaitForUi(160);
                pause.PerformClick();
                WaitForPanel(()=>board.Board.Match.Phase==GamePhase.Paused && resume.Enabled,"pause reaches the match while AI mode is enabled");
                string pausedCells=board.Board.Cells;
                WaitForUi(850);
                Check(board.Board.Cells==pausedCells && !move.Enabled,"no AI move is applied during pause");
                resume.PerformClick();
                try {
                    WaitForPanel(()=>board.Board.Turn==1 && board.Board.White==3 && board.Board.Black==3 && move.Enabled,
                        "actual Egaroucid engine responds with one confirmed white move",12000);
                } catch(Exception error) {
                    var log=FindControl<TextBox>(panel,t=>t.Multiline&&t.ReadOnly);
                    throw new InvalidOperationException("Egaroucid opening diagnostic:"+Environment.NewLine+log.Text,error);
                }
                Check(board.Board.Cells[18]=='2'||board.Board.Cells[20]=='2'||board.Board.Cells[34]=='2',
                    "opening answer uses a valid white coordinate in host orientation");

                int fullGameMoves=2;
                while(board.Board.Status==0 && fullGameMoves<60) {
                    uint revision=board.Board.Revision;
                    for(int cell=0;cell<64;cell++) {
                        if(board.Board.Cells[cell]!='0') continue;
                        row.Value=cell/8+1;col.Value=cell%8+1;
                        move.PerformClick();
                        WaitForPanel(()=>board.Board.Revision!=revision || move.Enabled,"black attempt settles");
                        if(board.Board.Revision!=revision) break;
                    }
                    Check(board.Board.Revision!=revision,"a valid black move is accepted before terminal state");
                    fullGameMoves++;
                    if(board.Board.Turn==2) {
                        uint whiteRevision=board.Board.Revision;
                        WaitForPanel(()=>board.Board.Revision!=whiteRevision && (board.Board.Status==1 || move.Enabled),
                            "real engine reply is confirmed before the next black turn",12000);
                        fullGameMoves++;
                    }
                }
                Check(board.Board.Status==1 && board.Board.Turn==0 && board.Board.Match.Result==GameResult.Board,
                    "Egaroucid match terminates using course rules");
                WaitForUi(600);
                Check(board.Board.Black+board.Board.White==4+fullGameMoves && !move.Enabled,
                    "terminal board has one new piece per confirmed move and stays locked");
                string outcome=GameArchive.Outcome(board.Board.Black,board.Board.White,true);
                Check(FindControl<Label>(panel,l=>l.Text==outcome)!=null,"live terminal panel displays the board outcome");
                Console.WriteLine("Egaroucid offline match: "+fullGameMoves+" moves, black "+board.Board.Black+", white "+board.Board.White);

                start.PerformClick();
                WaitForPanel(()=>board.Board.Cells==InitialBoard()&&move.Enabled,"a new AI round can start");
                row.Value=3;col.Value=4;move.PerformClick();
                WaitForPanel(()=>board.Board.Turn==2&&disconnect.Enabled,"disconnect can interrupt the AI turn");
                disconnect.PerformClick();
                connect.PerformClick();
                WaitForPanel(()=>board.Board!=null && board.Board.Cells==InitialBoard() && move.Enabled,
                    "reconnect has an independent fresh manual game");
                WaitForUi(850);
                Check(board.Board.Cells==InitialBoard() && board.Board.Turn==1,"no stale AI result reaches the new connection");
            }
            CheckPhysicalEgaroucid(enginePath,archive);
        } finally {
            if(previousConfig!=null) File.WriteAllBytes(config,previousConfig);
            else if(File.Exists(config)) File.Delete(config);
            if(Directory.Exists(folder)) {
                foreach(string file in Directory.GetFiles(folder)) File.Delete(file);
                Directory.Delete(folder);
            }
        }
    }

    static void CheckAdvanced() {
        using(var host=new HostSession()) {
            var legacy=new FakeTransport();
            host.Connect(legacy);
            var probes=new[]{"@SYNC6","@SYNC5","@SYNC4","@SYNC3","@SYNC2","@SYNC","@GET"};
            for(int i=0;i<probes.Length;i++) {
                int sequence=i+1;
                Check(legacy.Last.StartsWith(probes[i]+","+sequence+","),"negotiation tries "+probes[i]);
                if(i<probes.Length-1) legacy.Inject(Protocol.Frame("@ERROR,"+sequence+",COMMAND"));
            }
            legacy.Inject(State(7,1,InitialBoard(),1,0,2,2));
            host.Move(2,3);
            Check(legacy.Last.StartsWith("@MOVE,8,"),"legacy host move format preserved");
        }
        using(var host=new HostSession()) {
            host.Connect(new SimulatedTransport());
            Check(host.State.Match.Mode==GameMode.Casual && host.SupportsExternalAi,"advanced default boot game with current capabilities");
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
            host.Move(2,3);
            uint stageRound=host.State.Match.Round;
            host.Control(GameControl.Restart);
            Check(host.State.Cells==InitialBoard() && host.State.Match.Round!=stageRound && host.State.Match.Stage==1,
                "restart restores the initial setup and keeps the current challenge stage");
            host.Control(GameControl.Mode,GameMode.Timed,1);
            Check(host.State.Match.BlackRemainingMs==1000 && host.State.Match.WhiteRemainingMs==1000,"new host receives two full clocks");
            host.Control(GameControl.Pause);
            uint paused=host.State.Match.RemainingMs;
            Thread.Sleep(1100);
            host.Refresh();
            Check(host.State.Match.RemainingMs==paused,"paused timer remains frozen past the limit");
            host.Control(GameControl.Resume);
            Thread.Sleep(1100);
            host.Tick();
            Check(host.State.Match.Result==GameResult.Time && host.State.Match.Total==2 && host.State.Match.ElapsedMs==1000 &&
                host.State.Match.BlackRemainingMs==0 && host.State.Match.WhiteRemainingMs==1000,
                "timer settles exactly at limit and credits the current round");
        }
    }

    static void CheckClockPanel() {
        var serial=new FakeTransport();
        using(var panel=new OthelloPanel(new GameArchive(Path.GetTempPath()),settings=>serial)) {
            panel.CreateControl();panel.Start();
            FindControl<ComboBox>(panel,c=>c.Items.Contains("真实串口")).SelectedItem="真实串口";
            FindControl<ComboBox>(panel,c=>c.DropDownStyle==ComboBoxStyle.DropDown).Text="COM37";
            FindControl<Button>(panel,b=>b.Text=="连接").PerformClick();
            WaitForPanel(()=>serial.Last!=null&&serial.Last.StartsWith("@SYNC6,1,"),"clock panel requests split-clock frames");
            string fields=",2,1,0,2,2,"+InitialBoard()+",2,0,2,0,0,";
            const string scores=",0,0,0,180,0,4,0,";
            serial.Inject(Protocol.Frame("@GAME6,1"+fields+"178800"+scores+"178800,180000"));
            WaitForPanel(()=>FindControl<Label>(panel,l=>l.Text=="黑棋 02:59    白棋 03:00")!=null,"separate clocks are visible");
            var clock=FindControl<Label>(panel,l=>l.Text.StartsWith("黑棋 02:59"));
            var scoring=FindControl<Label>(panel,l=>l.Text.Contains("本局 2 分"));
            var board=FindControl<BoardView>(panel,view=>true);
            Check(clock.Height>0,"clock is allotted space in the sidebar");
            int clockChanges=0,scoreChanges=0;
            clock.TextChanged+=delegate { clockChanges++; };
            scoring.TextChanged+=delegate { scoreChanges++; };
            serial.Inject(Protocol.Frame("@GAME6,0"+fields+"178100"+scores+"178100,180000"));
            WaitForPanel(()=>board.Board.Match.BlackRemainingMs==178100,"panel processes a second sample in the same displayed second");
            Check(clockChanges==0 && scoreChanges==0,"same displayed second does not rewrite the timer or score");
            serial.Inject(Protocol.Frame("@GAME6,0"+fields+"177900"+scores+"177900,180000"));
            WaitForPanel(()=>clock.Text=="黑棋 02:58    白棋 03:00","new second reaches the clock asynchronously");
            Check(clockChanges==1 && scoreChanges==0,
                "only the active clock text updates across a second boundary");
        }
    }

    static void CheckOutcomes() {
        var frame=new ReplayFrame { Status=1, Cells=new string('1',30)+new string('2',16)+new string('0',18) };
        var record=new GameRecord { Result=GameResult.Over };
        record.Frames.Add(frame);
        Check(GameArchive.Outcome(record)=="黑棋胜","terminal cells determine black victory even for a partial replay and challenge OVER");
        frame.Cells=new string('2',30)+new string('1',16)+new string('0',18);
        record.Result=GameResult.Win;
        Check(GameArchive.Outcome(record)=="白棋胜","terminal cells determine white victory independently of challenge WIN");
        frame.Cells=InitialBoard();
        record.Result=GameResult.Time;
        Check(GameArchive.Outcome(record)=="平局","equal counts at timed settlement produce a draw");
        frame.Cells=new string('1',30)+new string('2',16)+new string('0',18);
        record.Result=GameResult.Exit;
        Check(GameArchive.Outcome(record)=="未完成","exiting with more pieces does not award a win");
        record.Result=GameResult.Board;
        frame.Status=0;
        Check(GameArchive.Outcome(record)=="未完成","a replay without a terminal frame has no outcome");
    }

    static void CheckArchive() {
        string folder=Path.Combine(Path.GetDirectoryName(typeof(BasicTests).Assembly.Location),"archive-test-"+Guid.NewGuid().ToString("N"));
        var archive=new GameArchive(folder);
        var modes=new[]{GameMode.Casual,GameMode.Challenge,GameMode.Timed,GameMode.ExternalAi};
        try {
            using(var host=new HostSession()) {
                host.Changed+=delegate { archive.Observe(host.State,true); };
                host.Connect(new SimulatedTransport());
                host.Move(2,3);
                host.Control(GameControl.End);
                host.Refresh();
                foreach(GameMode mode in modes) {
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
                                    if(host.State.ExternalAiTurn) host.EngineMove(square/8,square%8);
                                    else host.Move(square/8,square%8);
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
            int savedCount=1+5*modes.Length;
            Check(unreadable==0 && reopened.Count==savedCount,"all real simulated rounds reopen with no duplicate on refresh");
            GameRecord exited=reopened.Find(record=>record.Result==GameResult.Exit);
            Check(exited!=null && exited.Complete && exited.Frames.Count==3 && exited.Frames[0].Cells==InitialBoard() &&
                exited.Frames[1].ToBoard().White==1,"replay preserves opening, move and finish");
            using(var replay=new ArchiveWindow(archive)) {
                replay.CreateControl();
                var board=FindControl<BoardView>(replay,b=>true);
                var step=FindControl<TrackBar>(replay,t=>true);
                var previous=FindControl<Button>(replay,b=>b.Text=="上一步");
                var next=FindControl<Button>(replay,b=>b.Text=="下一步");
                var game=FindControl<ComboBox>(replay,c=>c.Items.Count==savedCount);
                var source=FindControl<ComboBox>(replay,c=>c.Items.Contains("离线模拟"));
                var rank=FindControl<ListView>(replay,t=>true);
                Check(board.Board.Cells==InitialBoard() && !previous.Enabled && next.Enabled,"replay opens at the first confirmed frame");
                step.Value=step.Maximum;
                Check(board.Board.Status==1 && previous.Enabled && !next.Enabled,"replay ends at the confirmed terminal frame");
                game.SelectedIndex=game.Items.Count-1;
                Check(step.Value==0 && board.Board.Cells==InitialBoard(),"changing record resets the replay cursor");
                Check(Convert.ToString(game.SelectedItem).Contains("未完成"),"the replay list labels an exited round as unfinished");
                step.Value=step.Maximum;
                Check(FindControl<Label>(replay,l=>l.Text.EndsWith(" · 未完成"))!=null,
                    "the last frame of an exited round shows unfinished");
                Check(rank.Items.Count==0,"real and simulated results are filtered independently");
                source.SelectedItem="离线模拟";
                Check(rank.Items.Count==5 && rank.Items[0].SubItems.Count==6,"ranking window shows simulated scores and timing columns");
                Check(rank.Items[0].SubItems[5].Text==GameArchive.Outcome(GameArchive.Rank(reopened,GameMode.Casual)[0]),
                    "the ranking result column shows the final board outcome");
            }
            foreach(GameMode mode in modes) {
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
            var autoExit=Protocol.ParseState(Protocol.Fields(Protocol.Frame(finishPayload.Replace("@GAME3","@GAME4"))));
            Check(settled.Match.ActiveMode==GameMode.Challenge && autoExit.Match.ActiveMode==GameMode.Casual &&
                autoExit.Match.Mode==GameMode.Challenge && autoExit.Match.TotalScore==188.5m,
                "new firmware exits active challenge without losing the archived challenge result");
            Reject(()=>Protocol.ParseState(Protocol.Fields(Protocol.Frame(finishPayload.Replace(",3,93,1",",3,94,1")))),
                "staged wire protocol rejects wrong coefficient scores");
            stagedArchive.Observe(opening,true); stagedArchive.Observe(settled,true);
            string legacyPath=Path.Combine(folder,"legacy.xml");
            File.WriteAllText(legacyPath,"<GameRecord><Id>legacy</Id><Mode>Challenge</Mode><Score>25</Score><Total>100</Total>"+
                "<Frames><ReplayFrame><Cells>"+InitialBoard()+"</Cells></ReplayFrame></Frames></GameRecord>");
            File.WriteAllText(Path.Combine(folder,"broken.xml"),"<GameRecord><Frames><ReplayFrame><Cells>"+
                new string('X',64)+"</Cells></ReplayFrame></Frames></GameRecord>");
            var loaded=stagedArchive.Load(out unreadable);
            var fractional=loaded.Find(record=>record.Stage==3);
            var legacyRecord=loaded.Find(record=>record.Id=="legacy");
            Check(unreadable==1 && fractional!=null && fractional.Score==46.5m && fractional.Total==188.5m &&
                legacyRecord!=null && legacyRecord.Score==25m && legacyRecord.Total==100m,"archive preserves new fractions and reads old integer records");
            Check(!GameArchive.Rank(loaded,GameMode.Challenge).Contains(legacyRecord),"unfinished legacy data is excluded from the standings");
            string fractionalCsv=Path.Combine(folder,"fractional.csv");
            GameArchive.Export(fractionalCsv,new[]{fractional});
            Check(File.ReadAllLines(fractionalCsv)[1].Contains(",46.5,188.5,"),"CSV preserves both half-point values");
            Check(File.ReadAllLines(fractionalCsv)[1].EndsWith(",黑棋胜"),"CSV exports the final board outcome");
        } finally {
            if(Directory.Exists(folder)) {
                foreach(string file in Directory.GetFiles(folder)) File.Delete(file);
                Directory.Delete(folder);
            }
        }
    }
    [STAThread]
    static int Main(string[] args) {
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
            if(args.Length!=0) {
                CheckEngineInputEncodings(args[0]);
                CheckEgaroucid(args[0]);
            }
            CheckPanel();
            CheckSerialPanel();
            CheckShadowCopy();
            CheckAdvanced();
            CheckClockPanel();
            CheckOutcomes();
            CheckArchive();
            Console.WriteLine("PASS "+checks+" checks: framing, modes, editor, timers, replay, ranking, reconnect, panel actions");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error);return 1; }
    }
}
