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
        public uint? BlackRemainingMs { get; set; }
        public uint? WhiteRemainingMs { get; set; }
        public bool Edited { get; set; }
        public bool Simulated { get; set; }
        public bool Complete { get; set; }
        public List<ReplayFrame> Frames { get; set; }
        public GameRecord() { Frames=new List<ReplayFrame>(); }
    }

    internal struct AiMatchStats {
        internal int BlackWins, WhiteWins, Draws;
        internal int Total { get { return BlackWins+WhiteWins+Draws; } }
    }

    public sealed class GameArchive {
        readonly string folder;
        readonly XmlSerializer serializer=new XmlSerializer(typeof(GameRecord));
        GameRecord current;
        uint currentRound, lastRevision, settledRound;
        public GameArchive(string folder) { this.folder=folder; }
        public void ResetConnection() { current=null; currentRound=lastRevision=settledRound=0; }

        public void Observe(BoardState state, bool simulated) {
            Observe(state,simulated,false);
        }
        internal bool Observe(BoardState state,bool simulated,bool legacyAi) {
            if(state==null || state.Match==null) return false;
            MatchState match=state.Match;
            if(current==null || currentRound!=match.Round || state.Revision<lastRevision) {
                if(match.Phase==GamePhase.Finished) return false;
                currentRound=match.Round;
                lastRevision=settledRound=0;
                current=new GameRecord { Id=Guid.NewGuid().ToString("N"), Mode=match.Mode, Simulated=simulated,
                    Complete=state.Black==2 && state.White==2 && state.Turn==1 && !match.Edited };
            }
            if(legacyAi) current.Mode=GameMode.ExternalAi;
            if(lastRevision!=state.Revision) {
                if(lastRevision!=0 && state.Revision!=lastRevision+1) current.Complete=false;
                current.Frames.Add(new ReplayFrame { Cells=state.Cells, Revision=state.Revision,
                    Turn=state.Turn, Status=state.Status, Black=state.Black, White=state.White });
                lastRevision=state.Revision;
            }
            if(match.Phase!=GamePhase.Finished || settledRound==match.Round) return false;
            if(current.EndedUtc==default(DateTime)) current.EndedUtc=DateTime.UtcNow;
            current.Result=match.Result; current.Score=match.Score; current.Total=match.TotalScore; current.Stage=match.Stage;
            current.ElapsedMs=match.ElapsedMs; current.Edited=match.Edited;
            current.BlackRemainingMs=match.BlackRemainingMs; current.WhiteRemainingMs=match.WhiteRemainingMs;
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
            return true;
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
                        if(record==null || record.Frames==null || record.Frames.Count==0 || record.Frames.Any(frame=>
                           frame==null || frame.Cells==null || frame.Cells.Length!=64 ||
                           frame.Cells.Any(cell=>cell!='0' && cell!='1' && cell!='2'))) unreadable++;
                        else records.Add(record);
                    }
                } catch(InvalidOperationException) { unreadable++; }
                catch(XmlException) { unreadable++; }
                catch(IOException) { unreadable++; }
                catch(UnauthorizedAccessException) { unreadable++; }
            }
            return records.OrderByDescending(record=>record.EndedUtc).ToList();
        }

        public static List<GameRecord> Rank(IEnumerable<GameRecord> records, GameMode mode) {
            return records.Where(record=>record.Mode==mode && !record.Edited && IsSettled(record.Result))
                .OrderByDescending(record=>record.Score).ThenBy(record=>record.ElapsedMs)
                .ThenBy(record=>record.EndedUtc).ThenBy(record=>record.Id,StringComparer.Ordinal).ToList();
        }

        static bool IsSettled(GameResult result) {
            return result==GameResult.Board || result==GameResult.Time || result==GameResult.Win || result==GameResult.Over;
        }
        internal AiMatchStats GetAiMatchStats() {
            int unreadable;
            return CountAiMatches(Load(out unreadable));
        }
        internal static AiMatchStats CountAiMatches(IEnumerable<GameRecord> records) {
            var stats=new AiMatchStats();
            foreach(var record in records) {
                if(record.Mode!=GameMode.ExternalAi || record.Edited || record.Result!=GameResult.Board) continue;
                int? difference=FinalDiscDifference(record);
                if(!difference.HasValue) continue;
                if(difference>0) stats.BlackWins++;
                else if(difference<0) stats.WhiteWins++;
                else stats.Draws++;
            }
            return stats;
        }
        internal static string Outcome(int black,int white,bool settled) {
            if(!settled) return "未完成";
            if(black==white) return "平局";
            return black>white?"黑棋胜":"白棋胜";
        }
        // 1 = 黑方超时, 2 = 白方超时, 0 = 无法从计时状态判定。超时的一方判负，与子数无关。
        internal static int TimeoutLoser(uint? blackRemaining,uint? whiteRemaining) {
            bool blackExpired=blackRemaining==0,whiteExpired=whiteRemaining==0;
            if(blackExpired==whiteExpired) return 0;
            return blackExpired?1:2;
        }
        internal static string TimedOutcome(int loser) {
            return loser==1?"白棋胜":loser==2?"黑棋胜":"平局";
        }
        internal static string Outcome(int black,int white,bool settled,GameResult result,uint? blackRemaining,uint? whiteRemaining) {
            if(!settled) return "未完成";
            if(result==GameResult.Time) {
                int loser=TimeoutLoser(blackRemaining,whiteRemaining);
                if(loser!=0) return TimedOutcome(loser);
            }
            return Outcome(black,white,true);
        }
        internal static string Outcome(GameRecord record) {
            int? difference=FinalDiscDifference(record);
            if(!difference.HasValue) return "未完成";
            return Outcome(difference.GetValueOrDefault(),0,true,record.Result,record.BlackRemainingMs,record.WhiteRemainingMs);
        }
        static int? FinalDiscDifference(GameRecord record) {
            var final=record.Frames==null?null:record.Frames.LastOrDefault();
            if(!IsSettled(record.Result) || final==null || final.Status!=1 || final.Cells==null) return null;
            return final.Cells.Count(cell=>cell=='1')-final.Cells.Count(cell=>cell=='2');
        }

        public static string ModeName(GameMode mode) {
            switch(mode) {
                case GameMode.Challenge: return "闯关";
                case GameMode.Timed: return "计时";
                case GameMode.Ai: return "旧版板载 AI";
                case GameMode.ExternalAi: return "AI 对弈";
                default: return "普通";
            }
        }
        public static void Export(string file, IEnumerable<GameRecord> records) {
            using(var writer=new StreamWriter(file,false,new UTF8Encoding(true))) {
                writer.WriteLine("模式,结束时间,本局得分(分),总分(分),用时(s),来源,结果");
                foreach(GameRecord record in records)
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0},{1:O},{2:0.#},{3:0.#},{4:F3},{5},{6}",
                        ModeName(record.Mode),record.EndedUtc.ToLocalTime(),record.Score,record.Total,
                        record.ElapsedMs/1000.0,record.Simulated?"离线模拟":"实体棋盘",Outcome(record)));
            }
        }
    }
}
