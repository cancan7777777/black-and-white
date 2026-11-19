using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Othello.Basic {
    public enum GameMode { Casual, Challenge, Timed }
    public enum GamePhase { Running, Paused, Editing, Finished }
    public enum GameResult { None, Board, Time, Exit, Win, Over }
    public enum GameControl { Start, Pause, Resume, End, Restart, Mode, Edit }
    public sealed class MatchState {
        public GameMode Mode { get; internal set; }
        public GamePhase Phase { get; internal set; }
        public GameResult Result { get; internal set; }
        public uint Round { get; internal set; }
        public uint Total { get; internal set; }
        public uint Losses { get; internal set; }
        public uint RemainingMs { get; internal set; }
        public uint ElapsedMs { get; internal set; }
        public uint LimitSeconds { get; internal set; }
        public bool Edited { get; internal set; }
    }
    public sealed class BoardState {
        public uint Sequence { get; private set; }
        public uint Revision { get; private set; }
        public int Turn { get; private set; }
        public int Status { get; private set; }
        public int Black { get; private set; }
        public int White { get; private set; }
        public string Cells { get; private set; }
        public MatchState Match { get; internal set; }
        public bool ComputerOpponent { get; internal set; }
        public bool ComputerTurn { get { return ComputerOpponent && Turn==2 && Match.Phase==GamePhase.Running; } }
        public BoardState(uint seq, uint revision, int turn, int status, int black, int white, string cells) {
            Sequence=seq; Revision=revision; Turn=turn; Status=status; Black=black; White=white; Cells=cells;
        }
    }
    public static class Protocol {
        public const int MaxLine = 192;
        public static ushort Crc(string text) {
            ushort crc=0xffff;
            foreach (char ch in text) {
                if (ch>127) throw new FormatException("协议只接受 ASCII");
                crc ^= (ushort)(ch<<8);
                for(int i=0;i<8;i++) crc=(ushort)((crc&0x8000)!=0 ? (crc<<1)^0x1021 : crc<<1);
            }
            return crc;
        }
        public static string Frame(string payload) {
            return payload+","+Crc(payload).ToString("X4",CultureInfo.InvariantCulture)+"\n";
        }
        public static string[] Fields(string line) {
            if(line==null || line.Length>=MaxLine) throw new FormatException("帧长度错误");
            string text=line.TrimEnd('\r','\n');
            foreach(char ch in text) if(ch<32 || ch>126) throw new FormatException("帧包含非法字符");
            int comma=text.LastIndexOf(',');
            ushort expected;
            if(comma<1 || text.Length-comma-1!=4 || !ushort.TryParse(text.Substring(comma+1),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out expected))
                throw new FormatException("CRC 格式错误");
            string payload=text.Substring(0,comma);
            if(Crc(payload)!=expected) throw new FormatException("CRC 校验失败，棋盘未更新");
            return payload.Split(',');
        }
        public static uint Number(string text) {
            uint value;
            if(text.Length==0) throw new FormatException("缺少数字");
            foreach(char ch in text) if(ch<'0'||ch>'9') throw new FormatException("数字格式错误");
            if(!uint.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out value)) throw new FormatException("数字超出 U32");
            return value;
        }
        public static BoardState ParseState(string[] f) {
            bool advanced=f.Length==18 && (f[0]=="@GAME" || f[0]=="@GAME2");
            if(!advanced && (f.Length!=8 || f[0]!="@STATE")) throw new FormatException("棋盘帧字段错误");
            uint seq=Number(f[1]), rev=Number(f[2]), turn=Number(f[3]), status=Number(f[4]), black=Number(f[5]), white=Number(f[6]);
            string cells=f[7]; int actualBlack=0, actualWhite=0;
            if(cells.Length!=64 || rev==0 || status>1 || (status==0 && turn!=1 && turn!=2) || (status==1 && turn!=0))
                throw new FormatException("棋盘状态不合法");
            foreach(char ch in cells) { if(ch=='1') actualBlack++; else if(ch=='2') actualWhite++; else if(ch!='0') throw new FormatException("棋盘编码错误"); }
            if(black!=(uint)actualBlack || white!=(uint)actualWhite) throw new FormatException("棋子计数与棋盘不一致");
            var state=new BoardState(seq,rev,(int)turn,(int)status,(int)black,(int)white,cells);
            if(advanced) {
                uint mode=Number(f[8]), phase=Number(f[9]), round=Number(f[10]), total=Number(f[11]), losses=Number(f[12]);
                uint remaining=Number(f[13]), elapsed=Number(f[14]), edited=Number(f[15]), result=Number(f[16]), limit=Number(f[17]);
                if(mode>2 || phase>3 || round==0 || losses>2 || edited>1 || result>5 || limit<1 || limit>3600 ||
                    remaining>limit*1000 || (mode!=2 && (remaining!=0 || result==2)) ||
                    (mode!=1 && (losses!=0 || result>3)) || (phase==3)!=(status==1) || (phase==3)!=(result!=0))
                    throw new FormatException("进阶状态不合法");
                state.Match=new MatchState { Mode=(GameMode)mode, Phase=(GamePhase)phase, Round=round, Total=total,
                    Losses=losses, RemainingMs=remaining, ElapsedMs=elapsed, Edited=edited==1, Result=(GameResult)result, LimitSeconds=limit };
            }
            return state;
        }
    }
    public sealed class LineBuffer {
        readonly StringBuilder pending=new StringBuilder();
        bool dropping;
        public event Action<string> Line;
        public event Action<string> Error;
        public void Clear(){ pending.Length=0; dropping=false; }
        public void Feed(string chunk) {
            foreach(char ch in chunk) {
                if(ch=='\n') {
                    string completed=!dropping && pending.Length>0 ? pending.ToString()+"\n" : null;
                    pending.Length=0; dropping=false;
                    if(completed!=null && Line!=null) Line(completed);
                } else if(!dropping) {
                    if(pending.Length>=Protocol.MaxLine-2) { pending.Length=0; dropping=true; if(Error!=null) Error("接收帧过长，丢弃至下一个 LF"); }
                    else pending.Append(ch);
                }
            }
        }
    }
}
