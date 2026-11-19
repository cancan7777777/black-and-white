using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Othello.Basic {
    public sealed class BoardState {
        public uint Sequence { get; private set; }
        public uint Revision { get; private set; }
        public int Turn { get; private set; }
        public int Status { get; private set; }
        public int Black { get; private set; }
        public int White { get; private set; }
        public string Cells { get; private set; }
        public BoardState(uint seq, uint revision, int turn, int status, int black, int white, string cells) {
            Sequence=seq; Revision=revision; Turn=turn; Status=status; Black=black; White=white; Cells=cells;
        }
    }
    public static class Protocol {
        public const int MaxLine = 160;
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
            if(f.Length!=8 || f[0]!="@STATE") throw new FormatException("STATE 字段错误");
            uint seq=Number(f[1]), rev=Number(f[2]), turn=Number(f[3]), status=Number(f[4]), black=Number(f[5]), white=Number(f[6]);
            string cells=f[7]; int actualBlack=0, actualWhite=0;
            if(cells.Length!=64 || rev==0 || status>1 || (status==0 && turn!=1 && turn!=2) || (status==1 && turn!=0))
                throw new FormatException("棋盘状态不合法");
            foreach(char ch in cells) { if(ch=='1') actualBlack++; else if(ch=='2') actualWhite++; else if(ch!='0') throw new FormatException("棋盘编码错误"); }
            if(black!=(uint)actualBlack || white!=(uint)actualWhite) throw new FormatException("棋子计数与棋盘不一致");
            return new BoardState(seq,rev,(int)turn,(int)status,(int)black,(int)white,cells);
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
