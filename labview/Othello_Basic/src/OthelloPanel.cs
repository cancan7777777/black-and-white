using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: InternalsVisibleTo("BasicTests")]

namespace Othello.Basic {
    public sealed class BoardView : Control {
        public BoardState Board;
        public int Row=2, Column=3;
        public event Action<int,int> Selected;
        public BoardView() {
            DoubleBuffered=true;
            BackColor=Color.FromArgb(17,55,47);
            MinimumSize=new Size(320,320);
        }
        RectangleF BoardBounds() {
            float side=Math.Max(0,Math.Min(ClientSize.Width,ClientSize.Height)-58);
            return new RectangleF((ClientSize.Width-side)/2,(ClientSize.Height-side)/2,side,side);
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            RectangleF area=BoardBounds(); float cell=area.Width/8;
            if(cell<=6) return;
            using(Brush light=new SolidBrush(Color.FromArgb(46,117,95)))
            using(Brush dark=new SolidBrush(Color.FromArgb(36,101,83)))
            using(Pen grid=new Pen(Color.FromArgb(111,159,140)))
            using(Brush label=new SolidBrush(Color.FromArgb(214,234,226)))
            using(Brush black=new SolidBrush(Color.FromArgb(26,31,31)))
            using(Brush white=new SolidBrush(Color.FromArgb(246,249,246)))
            using(Pen blackRim=new Pen(Color.FromArgb(235,148,73),2))
            using(Pen whiteRim=new Pen(Color.FromArgb(108,225,228),2))
            using(Font font=new Font("Segoe UI",10,FontStyle.Bold))
            using(StringFormat centered=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center }) {
                for(int r=0;r<8;r++) {
                    for(int c=0;c<8;c++)
                        e.Graphics.FillRectangle((r+c)%2==0?light:dark,area.Left+c*cell,area.Top+r*cell,cell,cell);
                }
                for(int i=0;i<=8;i++) {
                    e.Graphics.DrawLine(grid,area.Left+i*cell,area.Top,area.Left+i*cell,area.Bottom);
                    e.Graphics.DrawLine(grid,area.Left,area.Top+i*cell,area.Right,area.Top+i*cell);
                }
                for(int i=0;i<8;i++) {
                    e.Graphics.DrawString(((char)('A'+i)).ToString(),font,label,
                        new PointF(area.Left+(i+.5f)*cell,area.Top-14),centered);
                    e.Graphics.DrawString((i+1).ToString(),font,label,
                        new PointF(area.Left-14,area.Top+(i+.5f)*cell),centered);
                }
                if(Board!=null) for(int i=0;i<64;i++) {
                    char piece=Board.Cells[i];
                    if(piece=='0') continue;
                    float x=area.Left+(i%8+.13f)*cell,y=area.Top+(i/8+.13f)*cell,diameter=cell*.74f;
                    e.Graphics.FillEllipse(piece=='1'?black:white,x,y,diameter,diameter);
                    e.Graphics.DrawEllipse(piece=='1'?blackRim:whiteRim,x,y,diameter,diameter);
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
        const int EgaroucidMode=3;
        static readonly Color Ink=Color.FromArgb(26,52,46),Muted=Color.FromArgb(72,98,89);
        static readonly Color Canvas=Color.FromArgb(233,241,237),Tint=Color.FromArgb(240,247,243);
        readonly HostSession session=new HostSession();
        readonly GameArchive archive;
        readonly BoardView board=new BoardView();
        readonly ComboBox mode=new ComboBox(),ports=new ComboBox(),baud=new ComboBox(),bits=new ComboBox(),parity=new ComboBox(),stops=new ComboBox();
        readonly Button connect=new Button(),disconnect=new Button(),refresh=new Button(),move=new Button(),finish=new Button();
        readonly Button startGame=new Button(),pauseGame=new Button(),resumeGame=new Button(),endGame=new Button();
        readonly Button restartGame=new Button(),applyMode=new Button(),toggleEdit=new Button(),editCell=new Button(),history=new Button();
        readonly Button aiSettings=new Button();
        readonly ComboBox gameMode=new ComboBox(),piece=new ComboBox();
        readonly NumericUpDown limit=new NumericUpDown { Minimum=1,Maximum=3600,Value=180 };
        readonly NumericUpDown row=new NumericUpDown(),col=new NumericUpDown();
        readonly Label status=new Label(),counts=new Label(),turn=new Label(),connection=new Label();
        readonly Label scoring=new Label();
        readonly Label limitLabel=Label("时长 (s)");
        readonly TextBox log=new TextBox();
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        volatile bool stopped=true;
        bool connecting,active;
        int connectionAttempt;
        bool selectingAiLevel;
        int savedSeconds=180,savedLevel=5;
        EgaroucidEngine egaroucid,requestedEngine;
        BoardState modeBeforeRequest,searchPosition,submittedPosition;
        CancellationTokenSource searchCancellation;
        Task<int> search;
        public bool Stopped { get { return stopped; } }
        public OthelloPanel() : this(new GameArchive(Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),"Othello.Basic","Games"))) { }
        internal OthelloPanel(GameArchive archive) {
            this.archive=archive;
            Size=new Size(1060,720);MinimumSize=new Size(1000,660);
            BackColor=Canvas;ForeColor=Ink;Font=new Font("Microsoft YaHei UI",10);
            Build();
            session.Dispatch=delegate(Action action) { if(!IsDisposed && IsHandleCreated) BeginInvoke(action); };
            session.Changed+=Render;
            timer.Interval=100;timer.Tick+=delegate { session.Tick(); TickEgaroucid(); };
            Render();
        }
        static Label Label(string text) {
            return new Label { Text=text,Dock=DockStyle.Fill,ForeColor=Muted,TextAlign=ContentAlignment.MiddleLeft };
        }
        static void Combo(ComboBox box,string[] values,int selected) {
            box.DropDownStyle=ComboBoxStyle.DropDownList;
            box.Items.AddRange(values);
            box.SelectedIndex=selected;
        }
        internal static void StyleButton(Button button,bool primary=false) {
            button.FlatStyle=FlatStyle.Flat;
            button.UseVisualStyleBackColor=false;
            button.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(18,86,63):Color.FromArgb(222,237,229);
            Action applyColors=delegate {
                bool accent=primary&&button.Enabled;
                button.BackColor=accent?Color.FromArgb(23,108,78):button.Enabled?Tint:Color.FromArgb(239,242,240);
                button.ForeColor=accent?Color.White:button.Enabled?Ink:Muted;
                button.FlatAppearance.BorderColor=accent?button.BackColor:Color.FromArgb(193,209,202);
            };
            applyColors();
            button.EnabledChanged+=delegate { applyColors(); };
        }
        static void Button(Button button,string text,EventHandler handler,bool primary=false) {
            button.Text=text;
            button.Dock=DockStyle.Fill;
            button.Margin=new Padding(3,2,3,2);
            StyleButton(button,primary);
            button.Click+=handler;
        }
        static TableLayoutPanel Grid(int columns,params int[] heights) {
            var grid=new TableLayoutPanel { Dock=DockStyle.Fill,Margin=Padding.Empty,
                ColumnCount=columns,RowCount=heights.Length };
            for(int c=0;c<columns;c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/columns));
            foreach(int height in heights)
                grid.RowStyles.Add(height>0?new RowStyle(SizeType.Absolute,height):new RowStyle(SizeType.Percent,100));
            return grid;
        }
        static void AddField(TableLayoutPanel grid,int column,string caption,Control input) {
            AddField(grid,column,Label(caption),input);
        }
        static void AddField(TableLayoutPanel grid,int column,Label label,Control input) {
            label.Margin=new Padding(4,0,4,0);
            grid.Controls.Add(label,column,0);
            input.Dock=DockStyle.Fill;
            input.Margin=new Padding(4,1,4,1);
            grid.Controls.Add(input,column,1);
        }
        void Build() {
            var layout=Grid(1,62,78,60,0);
            layout.Padding=new Padding(12);
            Controls.Add(layout);

            var title=Grid(2,0);
            title.BackColor=Color.FromArgb(17,55,47);
            title.Padding=new Padding(17,0,17,0);
            title.Margin=new Padding(0,0,0,8);
            title.ColumnStyles[0].Width=65;title.ColumnStyles[1].Width=35;
            title.Controls.Add(new Label { Text="黑白棋 / OTHELLO",Dock=DockStyle.Fill,
                TextAlign=ContentAlignment.MiddleLeft,Font=new Font(Font.FontFamily,19,FontStyle.Bold),
                ForeColor=Color.White },0,0);
            connection.Dock=DockStyle.Fill;
            connection.TextAlign=ContentAlignment.MiddleRight;
            title.Controls.Add(connection,1,0);
            layout.Controls.Add(title,0,0);

            layout.Controls.Add(BuildConnectionControls(),0,1);
            layout.Controls.Add(BuildGameControls(),0,2);
            var body=Grid(2,0);
            body.ColumnStyles[1]=new ColumnStyle(SizeType.Absolute,338);
            board.Dock=DockStyle.Fill;board.Margin=new Padding(0,0,10,0);
            body.Controls.Add(board,0,0);
            body.Controls.Add(BuildSidebar(),1,0);
            layout.Controls.Add(body,0,3);
        }
        Control BuildConnectionControls() {
            var serial=Grid(2,0);
            serial.BackColor=Color.White;
            serial.Padding=new Padding(9,9,9,8);
            serial.Margin=new Padding(0,0,0,8);
            serial.ColumnStyles[1]=new ColumnStyle(SizeType.Absolute,306);
            var fields=Grid(6,20,0);
            fields.ColumnStyles[0].Width=22;fields.ColumnStyles[1].Width=18;fields.ColumnStyles[2].Width=21;
            fields.ColumnStyles[3].Width=12;fields.ColumnStyles[4].Width=15;fields.ColumnStyles[5].Width=12;
            Combo(mode,new[]{"离线模拟","真实串口"},0);mode.SelectedIndexChanged+=delegate { Render(); };
            Combo(baud,new[]{"9600","19200","38400","57600","115200","230400"},4);
            Combo(bits,new[]{"5","6","7","8"},3);Combo(parity,new[]{"None","Odd","Even","Mark","Space"},0);
            Combo(stops,new[]{"1","1.5","2"},0);
            ports.DropDownStyle=ComboBoxStyle.DropDown;ports.Text="COM1";ports.DropDown+=delegate { RefreshPorts(); };
            AddField(fields,0,"方式",mode);AddField(fields,1,"端口",ports);AddField(fields,2,"波特率 (Bd)",baud);
            AddField(fields,3,"数据位",bits);AddField(fields,4,"校验",parity);AddField(fields,5,"停止位",stops);
            serial.Controls.Add(fields,0,0);
            var actions=Grid(3,0);
            Button(connect,"连接",OnConnect,true);
            Button(disconnect,"断开",delegate { Disconnect(); });
            Button(refresh,"刷新棋盘",delegate { Safe(session.Refresh); });
            actions.Controls.Add(connect,0,0);actions.Controls.Add(disconnect,1,0);actions.Controls.Add(refresh,2,0);
            serial.Controls.Add(actions,1,0);
            return serial;
        }
        Control BuildSidebar() {
            var sidebar=Grid(1,28,23,47,57,41,49,44,0,44);
            sidebar.Padding=new Padding(12,9,12,8);
            sidebar.BackColor=Color.White;
            turn.Dock=counts.Dock=DockStyle.Fill;
            turn.TextAlign=counts.TextAlign=ContentAlignment.MiddleLeft;
            turn.Font=new Font(Font.FontFamily,12,FontStyle.Bold);
            counts.ForeColor=Muted;
            sidebar.Controls.Add(turn,0,0);sidebar.Controls.Add(counts,0,1);
            scoring.Dock=DockStyle.Fill;
            scoring.BackColor=Tint;scoring.Padding=new Padding(7,2,6,2);
            scoring.TextAlign=ContentAlignment.MiddleLeft;
            sidebar.Controls.Add(scoring,0,2);
            var settings=Grid(3,19,0);
            Combo(gameMode,new[]{"普通","闯关","计时","Egaroucid"},0);
            gameMode.SelectedIndexChanged+=delegate { UpdateModeInput(); };
            AddField(settings,0,"模式",gameMode);
            AddField(settings,1,limitLabel,limit);
            Button(applyMode,"设定模式",delegate { Safe(ApplyMode); });
            settings.Controls.Add(applyMode,2,0);
            settings.SetRowSpan(applyMode,2);
            sidebar.Controls.Add(settings,0,3);
            status.Dock=DockStyle.Fill;
            status.ForeColor=Muted;status.AutoEllipsis=true;
            status.TextAlign=ContentAlignment.MiddleLeft;
            sidebar.Controls.Add(status,0,4);

            var coordinates=Grid(3,19,0);
            coordinates.ColumnStyles[0]=new ColumnStyle(SizeType.Absolute,58);
            coordinates.ColumnStyles[1]=new ColumnStyle(SizeType.Absolute,58);
            row.Minimum=col.Minimum=1;row.Maximum=col.Maximum=8;row.Value=3;col.Value=4;
            AddField(coordinates,0,"行",row);AddField(coordinates,1,"列",col);
            row.ValueChanged+=delegate { board.Row=(int)row.Value-1;board.Invalidate(); };col.ValueChanged+=delegate { board.Column=(int)col.Value-1;board.Invalidate(); };
            board.Selected+=delegate(int r,int c) { row.Value=r+1;col.Value=c+1; };
            Button(move,"确认落子",delegate { Safe(delegate { session.Move((int)row.Value-1,(int)col.Value-1); }); },true);
            coordinates.Controls.Add(move,2,0);coordinates.SetRowSpan(move,2);
            sidebar.Controls.Add(coordinates,0,5);

            var editTools=Grid(2,0);
            editTools.ColumnStyles[0]=new ColumnStyle(SizeType.Absolute,116);
            Combo(piece,new[]{"空格","黑棋","白棋"},1);
            piece.Dock=DockStyle.Fill;piece.Margin=new Padding(4,10,4,0);
            Button(editCell,"编辑选中格",delegate { Safe(delegate { session.Edit((int)row.Value-1,(int)col.Value-1,piece.SelectedIndex); }); });
            editTools.Controls.Add(piece,0,0);editTools.Controls.Add(editCell,1,0);sidebar.Controls.Add(editTools,0,6);
            log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.Dock=DockStyle.Fill;
            log.Font=new Font("Consolas",9);log.BackColor=Tint;log.BorderStyle=BorderStyle.FixedSingle;
            sidebar.Controls.Add(log,0,7);
            Button(finish,"结束上位机",delegate { Shutdown(); });sidebar.Controls.Add(finish,0,8);
            return sidebar;
        }
        Control BuildGameControls() {
            var actions=Grid(8,0);
            actions.BackColor=Color.White;
            actions.Padding=new Padding(8,2,8,2);
            actions.Margin=new Padding(0,0,0,8);
            Button(startGame,"开始",delegate { Command(GameControl.Start); });
            Button(pauseGame,"暂停",delegate { Command(GameControl.Pause); });
            Button(resumeGame,"继续",delegate { Command(GameControl.Resume); });
            Button(endGame,"结束对局",delegate { Command(GameControl.End); });
            Button(restartGame,"重开本局",delegate { Command(GameControl.Restart); });
            Button(toggleEdit,"开启编辑",delegate { Command(GameControl.Edit); });
            Button(history,"回放与排行",delegate { Safe(delegate { using(var window=new ArchiveWindow(archive)) window.ShowDialog(this); }); });
            Button(aiSettings,"AI 设置",delegate { Safe(SelectEgaroucid); });
            actions.Controls.AddRange(new Control[]{startGame,pauseGame,resumeGame,endGame,restartGame,toggleEdit,history,aiSettings});
            return actions;
        }
        void UpdateModeInput() {
            bool useLevel=gameMode.SelectedIndex==EgaroucidMode;
            if(useLevel==selectingAiLevel) return;
            selectingAiLevel=useLevel;
            if(useLevel) {
                savedSeconds=(int)limit.Value;
                limit.Minimum=0;limit.Maximum=60;limit.Value=savedLevel;limitLabel.Text="AI 等级";
            } else {
                savedLevel=(int)limit.Value;
                limit.Maximum=3600;limit.Minimum=1;limit.Value=savedSeconds;limitLabel.Text="时长 (s)";
            }
        }
        static string ReadEgaroucidPath() {
            return File.Exists(EgaroucidEngine.ConfigPath)?File.ReadAllText(EgaroucidEngine.ConfigPath).Trim():"";
        }
        void SelectEgaroucid() {
            using(var picker=new OpenFileDialog { Title="选择 Egaroucid 控制台程序",Filter="控制台程序 (*.exe)|*.exe",
                CheckFileExists=true,FileName=ReadEgaroucidPath() }) {
                if(picker.ShowDialog(this)!=DialogResult.OK) return;
                File.WriteAllText(EgaroucidEngine.ConfigPath,picker.FileName);
                session.Report("已选择 "+Path.GetFileName(picker.FileName));
            }
        }
        void ApplyMode() {
            if(gameMode.SelectedIndex!=EgaroucidMode) {
                StopEgaroucid();
                session.Control(GameControl.Mode,(GameMode)gameMode.SelectedIndex,(int)limit.Value);
                return;
            }
            var engine=new EgaroucidEngine(ReadEgaroucidPath(),(int)limit.Value);
            var before=session.State;
            StopEgaroucid();
            session.Control(GameControl.Mode,GameMode.Casual,savedSeconds);
            requestedEngine=engine;
            modeBeforeRequest=before;
        }
        static bool Casual(BoardState state) {
            return state!=null && state.Match!=null && state.Match.Mode==GameMode.Casual;
        }
        bool EgaroucidTurn(BoardState state) {
            return egaroucid!=null && Casual(state) && state.Match.Phase==GamePhase.Running &&
                state.Status==0 && state.Turn==2;
        }
        static bool SamePosition(BoardState first,BoardState next) {
            return next!=null && first.Revision==next.Revision && first.Cells==next.Cells && first.Turn==next.Turn;
        }
        void CancelSearch() {
            if(search==null) return;
            var cancellation=searchCancellation;
            cancellation.Cancel();
            search.ContinueWith(finished => {
                if(finished.IsFaulted) finished.Exception.Handle(error => true);
                cancellation.Dispose();
            },TaskScheduler.Default);
            search=null;searchPosition=null;searchCancellation=null;
        }
        void StopEgaroucid() {
            CancelSearch();
            egaroucid=requestedEngine=null;
            submittedPosition=modeBeforeRequest=null;
        }
        void EgaroucidFailed(string reason) {
            StopEgaroucid();
            session.Report("Egaroucid 未落子，已恢复手动。"+reason);
        }
        void TickEgaroucid() {
            var state=session.State;
            if(requestedEngine!=null && !session.Waiting) {
                if(Casual(state) && state.Match.Phase==GamePhase.Running && state.Match.Round!=modeBeforeRequest.Match.Round)
                    egaroucid=requestedEngine;
                requestedEngine=null;modeBeforeRequest=null;
                Render();
            }
            if(egaroucid==null) return;
            if(!Casual(state)) {
                StopEgaroucid();
                Render();
                return;
            }
            if(submittedPosition!=null) {
                if(session.Waiting) return;
                bool unconfirmed=SamePosition(submittedPosition,state);
                submittedPosition=null;
                if(unconfirmed) {
                    EgaroucidFailed("棋局未确认 AI 落子，请刷新棋盘。");
                    return;
                }
            }
            bool whiteTurn=EgaroucidTurn(state);
            if(search!=null && (!whiteTurn || !SamePosition(searchPosition,state))) CancelSearch();
            if(!whiteTurn || session.Waiting) return;
            if(search==null) {
                var engine=egaroucid;
                searchPosition=state;
                searchCancellation=new CancellationTokenSource();
                var cancellation=searchCancellation.Token;
                search=Task.Factory.StartNew(()=>engine.FindWhiteMove(state,cancellation),cancellation,
                    TaskCreationOptions.None,TaskScheduler.Default);
                Render();
                return;
            }
            if(!search.IsCompleted) return;
            var completed=search;
            searchCancellation.Dispose();
            search=null;searchPosition=null;searchCancellation=null;
            try {
                int cell=completed.GetAwaiter().GetResult();
                submittedPosition=state;
                session.Move(cell/8,cell%8);
            } catch(Exception error) {
                EgaroucidFailed(error.Message);
            }
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
            active=false;connectionAttempt++;connecting=false;timer.Stop();StopEgaroucid();session.Disconnect();Render();
        }
        void Disconnect() { connectionAttempt++;connecting=false;StopEgaroucid();Safe(session.Disconnect); }
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
            bool egaroucidTurn=EgaroucidTurn(state);
            move.Enabled=active&&connected&&!session.Waiting&&state!=null&&state.Status==0&&!state.ComputerTurn&&!egaroucidTurn&&
                (match==null||match.Phase==GamePhase.Running);
            finish.Enabled=active;history.Enabled=active;aiSettings.Enabled=active;
            bool ready=active&&connected&&!session.Waiting&&match!=null;
            startGame.Enabled=ready&&match.Phase==GamePhase.Finished;
            pauseGame.Enabled=ready&&match.Phase==GamePhase.Running;
            resumeGame.Enabled=ready&&match.Phase==GamePhase.Paused;
            endGame.Enabled=toggleEdit.Enabled=ready&&match.Phase!=GamePhase.Finished;
            restartGame.Enabled=applyMode.Enabled=gameMode.Enabled=limit.Enabled=ready;
            editCell.Enabled=piece.Enabled=ready&&match.Phase==GamePhase.Editing;
            toggleEdit.Text=match!=null&&match.Phase==GamePhase.Editing?"退出编辑":"开启编辑";
            connection.Text=connecting?"正在连接…":!active?"请运行 Main.vi":connected?(mode.SelectedIndex==0?"离线模拟 / 已连接":"真实串口 / 已连接"):"未连接";
            connection.ForeColor=connected?Color.FromArgb(151,239,194):Color.FromArgb(219,233,226);
            board.Board=session.State;board.Invalidate();
            string message=egaroucidTurn?"Egaroucid 正在思考…":session.Message;
            status.Text=archiveError==null?message:message+Environment.NewLine+"棋局保存失败："+archiveError;
            turn.Text=state==null?"当前回合：—":state.Status==1?"本局已结束":
                egaroucidTurn?"当前回合：Egaroucid（白方）":
                state.ComputerTurn?"当前回合：电脑（白方）":"当前回合："+(state.Turn==1?"黑方":"白方");
            counts.Text=session.State==null?"黑  —    白  —":"黑  "+session.State.Black+"    白  "+session.State.White+"    v"+session.State.Revision;
            string stage=match!=null&&match.Stage!=0 ? " · 第 "+match.Stage+"/4 关 ×"+(match.Stage/2m).ToString("0.#") : "";
            string scoreLabel=stage.Length!=0 ? "本关" : "本局";
            scoring.Text=match==null?"本局 — 分  总分 — 分":(egaroucid!=null&&Casual(state)?"Egaroucid · 玩家执黑":
                state.ComputerOpponent?"闯关（人机）":GameArchive.ModeName(match.Mode))+
                stage+Environment.NewLine+scoreLabel+" "+match.Score.ToString("0.#")+" 分  总分 "+match.TotalScore.ToString("0.#")+" 分"+
                (match.Mode==GameMode.Timed?"  剩余 "+(match.RemainingMs/1000.0).ToString("F1")+" s":match.Mode==GameMode.Challenge?"  连败 "+match.Losses+" 局":"")+
                (match.Edited?"  已编辑":"");
            string text=session.LogText;if(log.Text!=text) { log.Text=text;log.SelectionStart=log.TextLength;log.ScrollToCaret(); }
        }
        protected override void Dispose(bool disposing) { if(disposing) { connectionAttempt++;stopped=true;timer.Dispose();StopEgaroucid();session.Dispose(); }base.Dispose(disposing); }
    }
}
