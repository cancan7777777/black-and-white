using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Othello.Basic {
    sealed class ArchiveWindow : Form {
        readonly BoardView board=new BoardView { Dock=DockStyle.Fill, Enabled=false };
        readonly ComboBox games=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Width=455 };
        readonly TrackBar step=new TrackBar { Dock=DockStyle.Fill, TickStyle=TickStyle.None };
        readonly Label frame=new Label { AutoSize=true, Dock=DockStyle.Fill };
        readonly List<GameRecord> records;
        readonly ComboBox mode=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Width=110 };
        readonly ListView standings=new ListView { Dock=DockStyle.Fill, View=View.Details, FullRowSelect=true };
        readonly Button export=new Button { Text="导出 CSV", AutoSize=true };
        readonly ComboBox source=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Width=110 };

        public ArchiveWindow(GameArchive archive) {
            Text="棋局回放与排行榜"; StartPosition=FormStartPosition.CenterParent;
            ClientSize=new Size(770,610); MinimumSize=new Size(600,510);
            int unreadable;
            records=archive.Load(out unreadable);
            if(unreadable>0) Text+="（跳过 "+unreadable+" 个无法读取的记录）";
            var tabs=new TabControl { Dock=DockStyle.Fill };
            var replay=new TabPage("棋局回放"); var ranking=new TabPage("排行榜");
            tabs.TabPages.Add(replay); tabs.TabPages.Add(ranking); Controls.Add(tabs);

            var layout=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=4, Padding=new Padding(9) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
            var toolbar=new FlowLayoutPanel { Dock=DockStyle.Fill, WrapContents=false };
            toolbar.Controls.Add(games);
            var previous=new Button { Text="上一步", AutoSize=true }; var next=new Button { Text="下一步", AutoSize=true };
            toolbar.Controls.Add(previous); toolbar.Controls.Add(next);
            layout.Controls.Add(toolbar,0,0); layout.Controls.Add(board,0,1); layout.Controls.Add(step,0,2); layout.Controls.Add(frame,0,3);
            replay.Controls.Add(layout);
            foreach(GameRecord game in records) games.Items.Add(string.Format("{0:g} · {1} · {2} · {3:0.#} 分{4}",
                game.EndedUtc.ToLocalTime(),GameArchive.ModeName(game.Mode)+(game.Stage>0?" 第 "+game.Stage+" 关":""),
                game.Simulated?"模拟":"实体",game.Score,game.Complete?"":" · 部分记录"));
            games.SelectedIndexChanged+=delegate { step.Maximum=records[games.SelectedIndex].Frames.Count-1; step.Value=0; RenderFrame(); };
            step.ValueChanged+=delegate { RenderFrame(); };
            previous.Click+=delegate { if(step.Value>0) step.Value--; };
            next.Click+=delegate { if(games.SelectedIndex>=0 && step.Value<step.Maximum) step.Value++; };
            if(games.Items.Count==0) { frame.Text="还没有已保存的棋局"; step.Enabled=previous.Enabled=next.Enabled=false; }
            else games.SelectedIndex=0;

            var filters=new FlowLayoutPanel { Dock=DockStyle.Top, Height=40, Padding=new Padding(4) };
            mode.Items.AddRange(new object[]{"普通","闯关","计时"}); source.Items.AddRange(new object[]{"实体棋盘","离线模拟"});
            filters.Controls.Add(mode); filters.Controls.Add(source); filters.Controls.Add(export);
            standings.Columns.Add("排名",60); standings.Columns.Add("结束时间",185); standings.Columns.Add("本局得分 (分)",105);
            standings.Columns.Add("总分 (分)",100); standings.Columns.Add("用时 (s)",85); standings.Columns.Add("结果",90);
            ranking.Controls.Add(standings); ranking.Controls.Add(filters);
            mode.SelectedIndexChanged+=delegate { RenderRank(); }; source.SelectedIndexChanged+=delegate { RenderRank(); };
            source.SelectedIndex=0; mode.SelectedIndex=0;
            export.Click+=delegate {
                using(var dialog=new SaveFileDialog { Filter="CSV 文件 (*.csv)|*.csv", FileName="黑白棋排行榜.csv" }) {
                    if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                    try { GameArchive.Export(dialog.FileName,CurrentRank()); }
                    catch(Exception error) { MessageBox.Show(this,error.Message,"导出失败"); }
                }
            };
        }

        void RenderFrame() {
            int index=games.SelectedIndex;
            if(index<0) return;
            GameRecord game=records[index];
            board.Board=game.Frames[step.Value].ToBoard(); board.Invalidate();
            frame.Text=string.Format("{0}/{1} · 黑 {2} 白 {3} · {4}",step.Value+1,game.Frames.Count,board.Board.Black,board.Board.White,
                game.Complete?"开局至终局":"连接期间记录，可能缺少步骤");
        }
        List<GameRecord> CurrentRank() {
            return GameArchive.Rank(records.Where(record=>record.Simulated==(source.SelectedIndex==1)),(GameMode)mode.SelectedIndex);
        }
        void RenderRank() {
            if(mode.SelectedIndex<0 || source.SelectedIndex<0) return;
            List<GameRecord> rank=CurrentRank();
            standings.BeginUpdate(); standings.Items.Clear();
            for(int i=0;i<rank.Count;i++) {
                GameRecord game=rank[i];
                standings.Items.Add(new ListViewItem(new[]{(i+1).ToString(),game.EndedUtc.ToLocalTime().ToString("g"),
                    game.Score.ToString("0.#"),game.Total.ToString("0.#"),(game.ElapsedMs/1000.0).ToString("F2"),game.Result.ToString()}));
            }
            standings.EndUpdate(); export.Enabled=rank.Count>0;
        }
    }
}
