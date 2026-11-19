using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Othello.Basic {
    public sealed class ReplayFrame {
        public string Cells { get; set; }
        public uint Revision { get; set; }
        public int Turn { get; set; }
        public int Status { get; set; }
        public int Black { get; set; }
        public int White { get; set; }
        public BoardState ToBoard() { return new BoardState(0,Revision,Turn,Status,Black,White,Cells); }
    }

    public sealed class GameRecord {
        public string Id { get; set; }
        public DateTime EndedUtc { get; set; }
        public GameMode Mode { get; set; }
        public GameResult Result { get; set; }
        public decimal Score { get; set; }
        public decimal Total { get; set; }
        public uint Stage { get; set; }
        public uint ElapsedMs { get; set; }
        public bool Edited { get; set; }
        public bool Simulated { get; set; }
        public bool Complete { get; set; }
        public List<ReplayFrame> Frames { get; set; }
        public GameRecord() { Frames=new List<ReplayFrame>(); }
    }

    public sealed class GameArchive {
        readonly string folder;
        readonly XmlSerializer serializer=new XmlSerializer(typeof(GameRecord));
        GameRecord current;
        uint currentRound, lastRevision, settledRound;
        public GameArchive(string folder) { this.folder=folder; }
        public void ResetConnection() { current=null; currentRound=lastRevision=settledRound=0; }

        public void Observe(BoardState state, bool simulated) {
            if(state==null || state.Match==null) return;
            MatchState match=state.Match;
            if(current==null || currentRound!=match.Round) {
                if(match.Phase==GamePhase.Finished) return;
                currentRound=match.Round;
                lastRevision=0;
                current=new GameRecord { Id=Guid.NewGuid().ToString("N"), Mode=match.Mode, Simulated=simulated,
                    Complete=state.Black==2 && state.White==2 && state.Turn==1 && !match.Edited };
            }
            if(lastRevision!=state.Revision) {
                if(lastRevision!=0 && state.Revision!=lastRevision+1) current.Complete=false;
                current.Frames.Add(new ReplayFrame { Cells=state.Cells, Revision=state.Revision,
                    Turn=state.Turn, Status=state.Status, Black=state.Black, White=state.White });
                lastRevision=state.Revision;
            }
            if(match.Phase!=GamePhase.Finished || settledRound==match.Round) return;
            if(current.EndedUtc==default(DateTime)) current.EndedUtc=DateTime.UtcNow;
            current.Result=match.Result; current.Score=match.Score; current.Total=match.TotalScore; current.Stage=match.Stage;
            current.ElapsedMs=match.ElapsedMs; current.Edited=match.Edited;
            Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,current.Id+".xml");
            if(!File.Exists(path)) {
                string temporary=path+".tmp";
                try {
                    using(var stream=File.Create(temporary)) serializer.Serialize(stream,current);
                    File.Move(temporary,path);
                } finally { if(File.Exists(temporary)) File.Delete(temporary); }
            }
            settledRound=match.Round;
        }

        public List<GameRecord> Load(out int unreadable) {
            var records=new List<GameRecord>();
            unreadable=0;
            if(!Directory.Exists(folder)) return records;
            var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=1024*1024 };
            foreach(string file in Directory.EnumerateFiles(folder,"*.xml")) {
                try {
                    using(var reader=XmlReader.Create(file,settings)) {
                        var record=(GameRecord)serializer.Deserialize(reader);
                        if(record.Frames.Count==0 || record.Frames.Any(frame=>frame.Cells==null || frame.Cells.Length!=64)) unreadable++;
                        else records.Add(record);
                    }
                } catch(InvalidOperationException) { unreadable++; }
                catch(XmlException) { unreadable++; }
                catch(IOException) { unreadable++; }
            }
            return records.OrderByDescending(record=>record.EndedUtc).ToList();
        }

        public static List<GameRecord> Rank(IEnumerable<GameRecord> records, GameMode mode) {
            return records.Where(record=>record.Mode==mode && !record.Edited && record.Result!=GameResult.Exit)
                .OrderByDescending(record=>record.Score).ThenBy(record=>record.ElapsedMs)
                .ThenBy(record=>record.EndedUtc).ThenBy(record=>record.Id,StringComparer.Ordinal).ToList();
        }

        public static string ModeName(GameMode mode) {
            return mode==GameMode.Challenge ? "闯关" : mode==GameMode.Timed ? "计时" : "普通";
        }
        public static void Export(string file, IEnumerable<GameRecord> records) {
            using(var writer=new StreamWriter(file,false,new UTF8Encoding(true))) {
                writer.WriteLine("模式,结束时间,本局得分(分),总分(分),用时(s),来源,结果");
                foreach(GameRecord record in records)
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0},{1:O},{2:0.#},{3:0.#},{4:F3},{5},{6}",
                        ModeName(record.Mode),record.EndedUtc.ToLocalTime(),record.Score,record.Total,
                        record.ElapsedMs/1000.0,record.Simulated?"离线模拟":"实体棋盘",record.Result));
            }
        }
    }
}
