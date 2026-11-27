using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Othello.Basic {
    internal sealed class GameAnalysisWindow : Form {
        readonly BoardView board=new BoardView { Dock=DockStyle.Fill, Enabled=false };
        readonly ListView moves=new ListView { Dock=DockStyle.Fill, View=View.Details,
            FullRowSelect=true, HideSelection=false, MultiSelect=false };
        readonly Label status=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft };
        readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        readonly List<BoardState> positions=new List<BoardState>();
        readonly GameRecord game;
        readonly Func<string> enginePath;

        internal GameAnalysisWindow(GameRecord game,Func<string> enginePath) {
            this.game=game;this.enginePath=enginePath;
            Text="对局分析 · "+GameArchive.ModeName(game.Mode);
            StartPosition=FormStartPosition.CenterParent;
            ClientSize=new Size(1040,540);MinimumSize=new Size(990,460);
            Font=new Font("Microsoft YaHei UI",10);
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(12) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
            var summary=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,
                Text=game.EndedUtc.ToLocalTime().ToString("g")+" · "+GameArchive.Outcome(game)+
                    (game.Complete?"":" · 部分棋谱")+(game.Edited?" · 含棋盘编辑":"")+
                    "     胜率来自棋盘模拟，子差正值表示黑方领先" };
            layout.Controls.Add(summary,0,0);layout.SetColumnSpan(summary,2);
            layout.Controls.Add(board,0,1);layout.Controls.Add(moves,1,1);
            layout.Controls.Add(status,0,2);layout.SetColumnSpan(status,2);
            Controls.Add(layout);
            moves.Columns.Add("局面",96);moves.Columns.Add("实战落点",86);moves.Columns.Add("引擎建议",86);
            moves.Columns.Add("黑方子差",86);moves.Columns.Add("黑胜",61);moves.Columns.Add("白胜",61);moves.Columns.Add("平局",61);

            foreach(var frame in game.Frames) {
                var current=frame.ToBoard();
                if(positions.Count>0) {
                    var last=positions[positions.Count-1];
                    if(last.Cells==current.Cells && last.Turn==current.Turn && last.Status==current.Status) continue;
                }
                positions.Add(current);
                moves.Items.Add(new ListViewItem(new[]{current.Status==1?"终局":
                    positions.Count+" · "+(current.Turn==1?"黑方":"白方"),"—","—","—","—","—","—"}));
            }
            moves.SelectedIndexChanged+=delegate {
                if(moves.SelectedIndices.Count!=0) ShowPosition(moves.SelectedIndices[0]);
            };
            Shown+=Analyze;
        }

        void ShowPosition(int index) {
            board.Board=positions[index];
            var square=moves.Items[index].SubItems[1].Text;
            board.Column=square.Length==2?square[0]-'A':-1;
            board.Row=square.Length==2?square[1]-'1':-1;
            board.Invalidate();
        }

        async void Analyze(object sender,EventArgs args) {
            if(moves.Items.Count==0) return;
            moves.Items[0].Selected=true;
            ShowPosition(0);
            CancellationToken token=cancellation.Token;
            EgaroucidEngine engine=null;
            if(enginePath!=null) {
                try { engine=new EgaroucidEngine(enginePath(),5); }
                catch(IOException) { }
                catch(UnauthorizedAccessException) { }
                catch(ArgumentException) { }
            }
            string engineStatus=engine==null?" · 可在 AI 设置中选择引擎查看建议":" · Egaroucid 等级 5";
            for(int index=0;index<positions.Count;index++) {
                var current=positions[index];
                if(current.Status!=0 || (current.Turn!=1 && current.Turn!=2)) continue;
                var next=index+1<positions.Count?positions[index+1]:null;
                status.Text="正在分析 "+(index+1)+"/"+positions.Count+" 个局面"+engineStatus;
                try {
                    var result=await Task.Run(delegate {
                        token.ThrowIfCancellationRequested();
                        int actual=game.Edited||next==null?-1:SimulatedTransport.RecordedMove(current,next);
                        var rates=SimulatedTransport.Estimate(current);
                        PositionAnalysis advice=null;
                        if(engine!=null) {
                            try { advice=engine.Analyze(current,token); }
                            catch(InvalidOperationException) { }
                            catch(TimeoutException) { }
                            catch(System.ComponentModel.Win32Exception) { }
                            catch(IOException) { }
                        }
                        return Tuple.Create(actual,rates,advice);
                    },token);
                    if(token.IsCancellationRequested || IsDisposed) return;
                    var columns=moves.Items[index].SubItems;
                    columns[1].Text=Coordinate(result.Item1);
                    var ratesResult=result.Item2;
                    if(ratesResult!=null) {
                        columns[4].Text=ratesResult.Percent(ratesResult.Black);
                        columns[5].Text=ratesResult.Percent(ratesResult.White);
                        columns[6].Text=ratesResult.Percent(ratesResult.Draw);
                    }
                    var adviceResult=result.Item3;
                    if(adviceResult!=null) {
                        columns[2].Text=Coordinate(adviceResult.Cell);
                        if(adviceResult.BlackDiscDifference.HasValue)
                            columns[3].Text=adviceResult.BlackDiscDifference.Value.ToString("+0;-0;0",CultureInfo.InvariantCulture);
                    }
                    if(moves.Items[index].Selected) ShowPosition(index);
                } catch(OperationCanceledException) { return; }
                catch(Exception error) {
                    if(!IsDisposed) status.Text="分析中断 · "+error.Message;
                    return;
                }
            }
            status.Text="分析完成 · 每个局面模拟 256 局。缺少实战落点或引擎评估时显示 —"+engineStatus;
        }

        static string Coordinate(int square) {
            return square<0||square>=64?"—":((char)('A'+square%8)).ToString()+(square/8+1);
        }

        protected override void Dispose(bool disposing) {
            if(disposing && !IsDisposed) { cancellation.Cancel();cancellation.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
