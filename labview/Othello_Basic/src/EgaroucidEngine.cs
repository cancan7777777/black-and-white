using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Othello.Basic {
    internal sealed class PositionAnalysis {
        internal readonly int Cell;
        internal readonly int? BlackDiscDifference;
        internal readonly bool UsedFallback;
        internal PositionAnalysis(int cell,int? blackDiscDifference,bool usedFallback) {
            Cell=cell;BlackDiscDifference=blackDiscDifference;UsedFallback=usedFallback;
        }
    }

    internal sealed class EgaroucidEngine {
        internal static readonly string ConfigPath=Path.Combine(
            Path.GetDirectoryName(new Uri(typeof(EgaroucidEngine).Assembly.CodeBase).LocalPath),"EgaroucidPath.txt");
        static readonly Regex EngineReply=new Regex(@"\A([a-h])([1-8])(?:[ \t]+([+-]?\d+))?\z",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        const int MoveBudgetMs=6000, OutlookBudgetMs=1500, FallbackBudgetMs=4000;
        readonly string executable;
        readonly int level;
        internal EgaroucidEngine(string executable,int level) {
            if(string.IsNullOrWhiteSpace(executable)||!File.Exists(executable))
                throw new FileNotFoundException("请在「AI 设置」中选择 Egaroucid 控制台程序");
            this.executable=Path.GetFullPath(executable);
            this.level=level;
        }
        internal int FindWhiteMove(BoardState board,CancellationToken cancellation) {
            if(board.Status!=0||board.Turn!=2) throw new InvalidOperationException("棋盘当前不轮到白方");
            return Analyze(board,cancellation).Cell;
        }
        internal PositionAnalysis Analyze(BoardState board,CancellationToken cancellation) {
            if(board.Status!=0||(board.Turn!=1&&board.Turn!=2)) throw new InvalidOperationException("棋局没有待分析的回合");
            try {
                string reply=Run(board,cancellation,"-level "+level.ToString(CultureInfo.InvariantCulture),
                    board.Turn==2?MoveBudgetMs:OutlookBudgetMs);
                return ReadAnalysis(board,reply,false);
            } catch(TimeoutException) {
                cancellation.ThrowIfCancellationRequested();
                try {
                    return ReadAnalysis(board,Run(board,cancellation,"-time 2",FallbackBudgetMs),true);
                } catch(TimeoutException) {
                    throw new TimeoutException("Egaroucid 原等级及限时重试均已超时。");
                }
            }
        }
        string Run(BoardState board,CancellationToken cancellation,string searchOptions,int budgetMs) {
            var clock=Stopwatch.StartNew();
            string position=board.Cells.Replace('0','-').Replace('1','B').Replace('2','W');
            string arguments="-quiet -showvalue -mode 3 -nobook -threads 2 -noautopass "+searchOptions;
            var setup=new ProcessStartInfo(executable,arguments) {
                UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(executable),
                RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
                StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
            };
            cancellation.ThrowIfCancellationRequested();
            using(var process=new Process { StartInfo=setup }) {
                process.Start();
                try {
                    var output=process.StandardOutput.ReadToEndAsync();
                    var errors=process.StandardError.ReadToEndAsync();
                    var redirectedInput=process.StandardInput;
                    bool reservesFirstLine=redirectedInput.Encoding.GetPreamble().Length!=0;
                    using(var input=new StreamWriter(redirectedInput.BaseStream,new UTF8Encoding(false))) {
                        // Consume the .NET Framework stream preamble only when its encoding has one.
                        if(reservesFirstLine) input.WriteLine();
                        input.WriteLine("setboard "+position+(board.Turn==1?" B":" W"));
                        input.WriteLine("go");
                        input.WriteLine("quit");
                    }
                    var readers=new Task[]{output,errors};
                    while(!process.WaitForExit(40)||!Task.WaitAll(readers,40)) {
                        cancellation.ThrowIfCancellationRequested();
                        if(clock.ElapsedMilliseconds>=budgetMs) throw new TimeoutException();
                    }
                    cancellation.ThrowIfCancellationRequested();
                    if(clock.ElapsedMilliseconds>=budgetMs) throw new TimeoutException();
                    if(process.ExitCode!=0)
                        throw new InvalidOperationException("Egaroucid 退出代码 0x"+process.ExitCode.ToString("X8",CultureInfo.InvariantCulture));
                    string diagnostic=errors.Result.Replace(">","").Trim();
                    if(diagnostic.IndexOf("[ERROR]",StringComparison.OrdinalIgnoreCase)>=0)
                        throw new InvalidOperationException(FirstLine(diagnostic));
                    string reply=output.Result.Trim();
                    const string preambleError="[ERROR] command `\uFEFF` not found";
                    const string emptyCommandError="[ERROR] command `` not found";
                    string first=FirstLine(reply).TrimEnd();
                    if(reservesFirstLine && (first==preambleError || first==emptyCommandError))
                        reply=reply.Substring(first.Length).Trim();
                    if(reply.StartsWith("[ERROR]",StringComparison.Ordinal)) throw new InvalidOperationException(FirstLine(reply));
                    return reply;
                } finally {
                    try { if(!process.HasExited) process.Kill(); }
                    catch(InvalidOperationException) { }
                    catch(Win32Exception) { }
                }
            }
        }
        static string FirstLine(string text) {
            int newline=text.IndexOfAny(new[]{'\r','\n'});
            return newline<0?text:text.Substring(0,newline);
        }
        static PositionAnalysis ReadAnalysis(BoardState board,string reply,bool usedFallback) {
            var move=EngineReply.Match(reply);
            if(!move.Success) throw new InvalidOperationException("Egaroucid 没有返回有效坐标");
            int row=move.Groups[2].Value[0]-'1',col=char.ToLowerInvariant(move.Groups[1].Value[0])-'a';
            if(!CanFlip(board.Cells,row,col,board.Turn)) throw new InvalidOperationException("Egaroucid 给出的落子不合法");
            int value;
            int? difference=int.TryParse(move.Groups[3].Value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out value)
                && value>=-64 && value<=64?(int?)(board.Turn==1?value:-value):null;
            return new PositionAnalysis(row*8+col,difference,usedFallback);
        }
        static bool CanFlip(string cells,int row,int col,int turn) {
            if(cells[row*8+col]!='0') return false;
            char own=turn==1?'1':'2',opponent=turn==1?'2':'1';
            for(int dr=-1;dr<=1;dr++) {
                for(int dc=-1;dc<=1;dc++) {
                    if(dr==0&&dc==0) continue;
                    int r=row+dr,c=col+dc;
                    if(r<0||r>=8||c<0||c>=8||cells[r*8+c]!=opponent) continue;
                    do { r+=dr;c+=dc; }
                    while(r>=0&&r<8&&c>=0&&c<8&&cells[r*8+c]==opponent);
                    if(r>=0&&r<8&&c>=0&&c<8&&cells[r*8+c]==own) return true;
                }
            }
            return false;
        }
    }
}
