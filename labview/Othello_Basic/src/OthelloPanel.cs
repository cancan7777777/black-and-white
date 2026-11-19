using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;

namespace Othello.Basic {
    public sealed class BoardView : Control {
        public BoardState Board;
        public int Row=2, Column=3;
        public event Action<int,int> Selected;
        public BoardView() { DoubleBuffered=true; BackColor=Color.FromArgb(18,83,63); MinimumSize=new Size(400,400); }
        RectangleF BoardBounds() { float side=Math.Min(ClientSize.Width-54,ClientSize.Height-54); return new RectangleF(34,20,side,side); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            RectangleF area=BoardBounds(); float cell=area.Width/8;
            using(Pen grid=new Pen(Color.FromArgb(95,173,134),1))
            using(Brush label=new SolidBrush(Color.FromArgb(220,239,228)))
            using(Font font=new Font("Segoe UI",10,FontStyle.Bold)) {
                for(int i=0;i<8;i++) { e.Graphics.DrawString(((char)('A'+i)).ToString(),font,label,area.Left+i*cell+cell*.38f,area.Bottom+7); e.Graphics.DrawString((i+1).ToString(),font,label,9,area.Top+i*cell+cell*.34f); }
                for(int i=0;i<=8;i++) { e.Graphics.DrawLine(grid,area.Left+i*cell,area.Top,area.Left+i*cell,area.Bottom); e.Graphics.DrawLine(grid,area.Left,area.Top+i*cell,area.Right,area.Top+i*cell); }
                if(Board!=null) for(int i=0;i<64;i++) if(Board.Cells[i]!='0') {
                    float x=area.Left+(i%8)*cell+cell*.14f, y=area.Top+(i/8)*cell+cell*.14f, diameter=cell*.72f;
                    using(Brush disc=new SolidBrush(Board.Cells[i]=='1'?Color.FromArgb(23,29,27):Color.FromArgb(245,246,240))) e.Graphics.FillEllipse(disc,x,y,diameter,diameter);
                    using(Pen rim=new Pen(Board.Cells[i]=='1'?Color.FromArgb(76,92,83):Color.White,1.5f)) e.Graphics.DrawEllipse(rim,x,y,diameter,diameter);
                }
            }
            using(Pen selected=new Pen(Color.FromArgb(245,192,74),3)) e.Graphics.DrawRectangle(selected,area.Left+Column*cell+3,area.Top+Row*cell+3,cell-6,cell-6);
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e); RectangleF area=BoardBounds();
            if(!area.Contains(e.Location)) return;
            int row=(int)((e.Y-area.Top)/(area.Height/8)),col=(int)((e.X-area.Left)/(area.Width/8));
            if(row<0||row>7||col<0||col>7) return;
            Row=row;Column=col;Invalidate();if(Selected!=null) Selected(row,col);
        }
    }
    public sealed class OthelloPanel : UserControl {
        readonly HostSession session=new HostSession();
        readonly GameArchive archive=new GameArchive(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Othello.Basic","Games"));
        readonly BoardView board=new BoardView();
        readonly ComboBox mode=new ComboBox(),ports=new ComboBox(),baud=new ComboBox(),bits=new ComboBox(),parity=new ComboBox(),stops=new ComboBox();
        readonly Button connect=new Button(),disconnect=new Button(),refresh=new Button(),move=new Button(),finish=new Button();
        readonly Button startGame=new Button(),pauseGame=new Button(),resumeGame=new Button(),endGame=new Button();
        readonly Button restartGame=new Button(),applyMode=new Button(),toggleEdit=new Button(),editCell=new Button(),history=new Button();
        readonly ComboBox gameMode=new ComboBox(),piece=new ComboBox();
        readonly NumericUpDown limit=new NumericUpDown { Minimum=1,Maximum=3600,Value=180,Width=68 };
        readonly NumericUpDown row=new NumericUpDown(),col=new NumericUpDown();
        readonly Label status=new Label(),counts=new Label(),turn=new Label(),connection=new Label();
        readonly Label scoring=new Label { AutoSize=true,Margin=new Padding(4,9,4,3) };
        readonly TextBox log=new TextBox();
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        volatile bool stopped=true;
        bool connecting,active;
        int connectionAttempt;
        public bool Stopped { get { return stopped; } }
        public OthelloPanel() {
            Size=new Size(1060,720);MinimumSize=new Size(1000,660);BackColor=Color.FromArgb(244,247,244);Font=new Font("Microsoft YaHei UI",9);
            Build();
            session.Dispatch=delegate(Action action) { if(!IsDisposed && IsHandleCreated) BeginInvoke(action); };
            session.Changed+=Render;
            timer.Interval=100;timer.Tick+=delegate { session.Tick(); };
            Render();
        }
        static Label Label(string text) { return new Label { Text=text,AutoSize=true,Margin=new Padding(4,8,4,3) }; }
        static void Combo(ComboBox box,int width,string[] values,int selected) { box.Width=width;box.DropDownStyle=ComboBoxStyle.DropDownList;box.Items.AddRange(values);box.SelectedIndex=selected;box.Margin=new Padding(2,4,10,4); }
        static void Button(Button button,string text,EventHandler handler) { button.Text=text;button.AutoSize=true;button.Height=32;button.Margin=new Padding(3,5,5,3);button.Click+=handler; }
        void Build() {
            TableLayoutPanel layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,86));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,82));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,26));Controls.Add(layout);
            FlowLayoutPanel title=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
            title.Controls.Add(new Label { Text="黑白棋 / OTHELLO",AutoSize=true,Font=new Font("Microsoft YaHei UI",19,FontStyle.Bold),ForeColor=Color.FromArgb(23,66,46),Margin=new Padding(0,0,18,0) });
            connection.AutoSize=true;connection.Margin=new Padding(3,13,0,0);title.Controls.Add(connection);layout.Controls.Add(title,0,0);
            FlowLayoutPanel serial=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=true,BackColor=Color.White,Padding=new Padding(4) };
            Combo(mode,110,new string[]{"离线模拟","真实串口"},0);mode.SelectedIndexChanged+=delegate { Render(); };
            Combo(baud,88,new string[]{"9600","19200","38400","57600","115200","230400"},4);
            Combo(bits,48,new string[]{"5","6","7","8"},3);Combo(parity,70,new string[]{"None","Odd","Even","Mark","Space"},0);Combo(stops,48,new string[]{"1","1.5","2"},0);
            ports.Width=100;ports.DropDownStyle=ComboBoxStyle.DropDown;ports.Text="COM1";ports.Margin=new Padding(2,4,8,4);ports.DropDown+=delegate { RefreshPorts(); };
            serial.Controls.Add(Label("方式"));serial.Controls.Add(mode);serial.Controls.Add(Label("端口"));serial.Controls.Add(ports);serial.Controls.Add(Label("波特率 (Bd)"));serial.Controls.Add(baud);serial.Controls.Add(Label("数据位"));serial.Controls.Add(bits);serial.Controls.Add(Label("校验"));serial.Controls.Add(parity);serial.Controls.Add(Label("停止位"));serial.Controls.Add(stops);
            Button(connect,"连接",OnConnect);Button(disconnect,"断开",delegate { Disconnect(); });Button(refresh,"刷新棋盘",delegate { Safe(session.Refresh); });
            serial.Controls.Add(connect);serial.Controls.Add(disconnect);serial.Controls.Add(refresh);layout.Controls.Add(serial,0,1);
            layout.Controls.Add(BuildGameControls(),0,2);
            TableLayoutPanel body=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,10,0,0) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,300));board.Dock=DockStyle.Fill;body.Controls.Add(board,0,0);
            TableLayoutPanel sidebar=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=8,ColumnCount=1,Padding=new Padding(12,0,0,0) };
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,37));sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,37));sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,64));sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,43));sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,44));sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,44));sidebar.RowStyles.Add(new RowStyle(SizeType.Percent,100));sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
            turn.Dock=counts.Dock=DockStyle.Fill;turn.Font=new Font(Font,FontStyle.Bold);sidebar.Controls.Add(turn,0,0);sidebar.Controls.Add(counts,0,1);
            status.Dock=DockStyle.Fill;status.ForeColor=Color.FromArgb(45,77,60);sidebar.Controls.Add(status,0,2);
            FlowLayoutPanel coordinates=new FlowLayoutPanel { Dock=DockStyle.Fill };row.Minimum=col.Minimum=1;row.Maximum=col.Maximum=8;row.Value=3;col.Value=4;row.Width=col.Width=52;
            coordinates.Controls.Add(Label("行"));coordinates.Controls.Add(row);coordinates.Controls.Add(Label("列"));coordinates.Controls.Add(col);sidebar.Controls.Add(coordinates,0,3);
            row.ValueChanged+=delegate { board.Row=(int)row.Value-1;board.Invalidate(); };col.ValueChanged+=delegate { board.Column=(int)col.Value-1;board.Invalidate(); };
            board.Selected+=delegate(int r,int c) { row.Value=r+1;col.Value=c+1; };
            Button(move,"确认落子",delegate { Safe(delegate { session.Move((int)row.Value-1,(int)col.Value-1); }); });move.Dock=DockStyle.Fill;sidebar.Controls.Add(move,0,4);
            var editTools=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
            Combo(piece,66,new[]{"空格","黑棋","白棋"},1);
            Button(editCell,"编辑选中格",delegate { Safe(delegate { session.Edit((int)row.Value-1,(int)col.Value-1,piece.SelectedIndex); }); });
            editTools.Controls.Add(piece); editTools.Controls.Add(editCell); sidebar.Controls.Add(editTools,0,5);
            log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.Dock=DockStyle.Fill;log.Font=new Font("Consolas",8);log.BackColor=Color.White;sidebar.Controls.Add(log,0,6);
            Button(finish,"结束上位机",delegate { Shutdown(); });finish.Dock=DockStyle.Fill;sidebar.Controls.Add(finish,0,7);
            body.Controls.Add(sidebar,1,0);layout.Controls.Add(body,0,3);
            layout.Controls.Add(new Label { Text="选中格子后确认落子。结算后可在「回放与排行」查看棋局。",AutoSize=true,ForeColor=Color.DimGray,Dock=DockStyle.Fill,Padding=new Padding(0,6,0,0) },0,4);
        }
        Control BuildGameControls() {
            var controls=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Color.White };
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
            Button(startGame,"开始",delegate { Command(GameControl.Start); });
            Button(pauseGame,"暂停",delegate { Command(GameControl.Pause); });
            Button(resumeGame,"继续",delegate { Command(GameControl.Resume); });
            Button(endGame,"结束对局",delegate { Command(GameControl.End); });
            Button(restartGame,"重开本局",delegate { Command(GameControl.Restart); });
            Button(toggleEdit,"开启编辑",delegate { Command(GameControl.Edit); });
            Button(history,"回放与排行",delegate { Safe(delegate { using(var window=new ArchiveWindow(archive)) window.ShowDialog(this); }); });
            actions.Controls.AddRange(new Control[]{startGame,pauseGame,resumeGame,endGame,restartGame,toggleEdit,history});
            var settings=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
            Combo(gameMode,73,new[]{"普通","闯关","计时"},0);
            limit.Margin=new Padding(2,8,2,0);
            Button(applyMode,"设定模式",delegate { Safe(delegate { session.Control(GameControl.Mode,(GameMode)gameMode.SelectedIndex,(int)limit.Value); }); });
            settings.Controls.AddRange(new Control[]{Label("模式"),gameMode,Label("时长 (s)"),limit,applyMode,scoring});
            controls.Controls.Add(actions,0,0); controls.Controls.Add(settings,0,1);
            controls.RowStyles.Add(new RowStyle(SizeType.Percent,50)); controls.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            return controls;
        }
        void Command(GameControl command) {
            Safe(delegate { session.Control(command); });
        }
        public void Start() {
            stopped=false;
            if(InvokeRequired) { BeginInvoke(new Action(Start)); return; }
            active=true;timer.Start();Render();
        }
        public void Shutdown() {
            stopped=true;
            if(InvokeRequired) { if(IsHandleCreated) BeginInvoke(new Action(Shutdown));return; }
            active=false;connectionAttempt++;connecting=false;timer.Stop();session.Disconnect();Render();
        }
        void Disconnect() { connectionAttempt++;connecting=false;Safe(session.Disconnect); }
        void RefreshPorts() { string current=ports.Text;ports.Items.Clear();ports.Items.AddRange(SerialPort.GetPortNames());ports.Text=current; }
        void OnConnect(object sender,EventArgs args) {
            if(connecting || !active) return;
            try {
                ITransport device;
                if(mode.SelectedIndex==0) device=new SimulatedTransport();
                else {
                    SerialSettings settings=new SerialSettings { Port=ports.Text.Trim(),Baud=int.Parse(baud.Text),DataBits=int.Parse(bits.Text),Parity=(Parity)Enum.Parse(typeof(Parity),parity.Text),StopBits=stops.Text=="1"?StopBits.One:stops.Text=="2"?StopBits.Two:StopBits.OnePointFive };
                    settings.Validate();device=new SerialTransport(settings);
                }
                int attempt=++connectionAttempt;connecting=true;Render();
                ThreadPool.QueueUserWorkItem(delegate {
                    Exception failure=null;
                    try { device.Open(); } catch(Exception ex) { failure=ex; }
                    if(IsDisposed || !IsHandleCreated) { device.Dispose();return; }
                    try { BeginInvoke(new Action(delegate {
                        if(attempt!=connectionAttempt || stopped) { device.Dispose();return; }
                        connecting=false;
                        if(failure!=null) { device.Dispose();session.Report("连接失败："+failure.Message); }
                        else Safe(delegate { session.AttachOpened(device); });
                        Render();
                    })); } catch(InvalidOperationException) { device.Dispose(); }
                });
            } catch(Exception ex) { connecting=false;session.Report(ex.Message);Render(); }
        }
        void Safe(Action action) { try { action(); } catch(Exception ex) { session.Report(ex.Message); } Render(); }
        void Render() {
            if(IsDisposed) return;
            if(InvokeRequired) { if(IsHandleCreated) BeginInvoke(new Action(Render));return; }
            bool connected=session.Connected;
            BoardState state=session.State;
            MatchState match=state==null?null:state.Match;
            if(!connected) archive.ResetConnection();
            string archiveError=null;
            try { archive.Observe(state,mode.SelectedIndex==0); }
            catch(IOException error) { archiveError=error.Message; }
            catch(UnauthorizedAccessException error) { archiveError=error.Message; }
            mode.Enabled=!connected&&!connecting&&active;ports.Enabled=!connected&&!connecting&&active&&mode.SelectedIndex==1;
            baud.Enabled=bits.Enabled=parity.Enabled=stops.Enabled=!connected&&!connecting&&active;
            connect.Enabled=active&&!connected&&!connecting;disconnect.Enabled=active&&(connected||connecting);refresh.Enabled=active&&connected&&!session.Waiting;
            move.Enabled=active&&connected&&!session.Waiting&&state!=null&&state.Status==0&&!state.ComputerTurn&&
                (match==null||match.Phase==GamePhase.Running);
            finish.Enabled=active; history.Enabled=active;
            bool ready=active&&connected&&!session.Waiting&&match!=null;
            startGame.Enabled=ready&&match.Phase==GamePhase.Finished;
            pauseGame.Enabled=ready&&match.Phase==GamePhase.Running;
            resumeGame.Enabled=ready&&match.Phase==GamePhase.Paused;
            endGame.Enabled=toggleEdit.Enabled=ready&&match.Phase!=GamePhase.Finished;
            restartGame.Enabled=applyMode.Enabled=gameMode.Enabled=limit.Enabled=ready;
            editCell.Enabled=piece.Enabled=ready&&match.Phase==GamePhase.Editing;
            toggleEdit.Text=match!=null&&match.Phase==GamePhase.Editing?"退出编辑":"开启编辑";
            connection.Text=connecting?"正在连接…":!active?"请运行 Main.vi":connected?(mode.SelectedIndex==0?"离线模拟 / 已连接":"真实串口 / 已连接"):"未连接";
            connection.ForeColor=connected?Color.FromArgb(22,109,74):Color.DimGray;
            board.Board=session.State;board.Invalidate();
            status.Text=archiveError==null?session.Message:session.Message+Environment.NewLine+"棋局保存失败："+archiveError;
            turn.Text=state==null?"当前回合：—":state.Status==1?"本局已结束":
                state.ComputerTurn?"当前回合：电脑（白方）":"当前回合："+(state.Turn==1?"黑方":"白方");
            counts.Text=session.State==null?"黑  —    白  —":"黑  "+session.State.Black+"    白  "+session.State.White+"    v"+session.State.Revision;
            scoring.Text=match==null?"本局 — 分  总分 — 分":(state.ComputerOpponent?"闯关（人机）":GameArchive.ModeName(match.Mode))+
                " · 本局 "+state.Black+" 分  总分 "+match.Total+" 分"+
                (match.Mode==GameMode.Timed?"  剩余 "+(match.RemainingMs/1000.0).ToString("F1")+" s":match.Mode==GameMode.Challenge?"  连败 "+match.Losses+" 局":"")+
                (match.Edited?"  已编辑":"");
            string text=session.LogText;if(log.Text!=text) { log.Text=text;log.SelectionStart=log.TextLength;log.ScrollToCaret(); }
        }
        protected override void Dispose(bool disposing) { if(disposing) { connectionAttempt++;stopped=true;timer.Dispose();session.Dispose(); }base.Dispose(disposing); }
    }
}
