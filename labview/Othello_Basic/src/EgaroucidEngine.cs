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
    internal sealed class EgaroucidEngine {
        internal static readonly string ConfigPath=Path.Combine(
            Path.GetDirectoryName(new Uri(typeof(EgaroucidEngine).Assembly.CodeBase).LocalPath),"EgaroucidPath.txt");
        static readonly Regex Coordinate=new Regex(@"\A([a-h])([1-8])\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
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
            string position=board.Cells.Replace('0','-').Replace('1','B').Replace('2','W');
            string arguments="-quiet -mode 3 -nobook -threads 2 -noautopass -level "+level.ToString(CultureInfo.InvariantCulture);
            var setup=new ProcessStartInfo(executable,arguments) {
                UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(executable),
                RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
                StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
            };
            cancellation.ThrowIfCancellationRequested();
            using(var process=new Process { StartInfo=setup }) {
                var clock=Stopwatch.StartNew();
                process.Start();
                try {
                    var output=process.StandardOutput.ReadToEndAsync();
                    var errors=process.StandardError.ReadToEndAsync();
                    var redirectedInput=process.StandardInput;
                    bool reservesFirstLine=redirectedInput.Encoding.GetPreamble().Length!=0;
                    using(var input=new StreamWriter(redirectedInput.BaseStream,new UTF8Encoding(false))) {
                        // Consume the .NET Framework stream preamble only when its encoding has one.
                        if(reservesFirstLine) input.WriteLine();
                        input.WriteLine("setboard "+position+" W");
                        input.WriteLine("go");
                        input.WriteLine("quit");
                    }
                    var readers=new Task[]{output,errors};
                    while(!process.WaitForExit(40)||!Task.WaitAll(readers,40)) {
                        cancellation.ThrowIfCancellationRequested();
                        if(clock.ElapsedMilliseconds>=10000) throw new TimeoutException("Egaroucid 思考超过 10 秒，可降低 AI 等级后重试");
                    }
                    cancellation.ThrowIfCancellationRequested();
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
                    var move=Coordinate.Match(reply);
                    if(!move.Success) throw new InvalidOperationException(reply.StartsWith("[ERROR]",StringComparison.Ordinal)?
                        FirstLine(reply):"Egaroucid 没有返回有效坐标");
                    int row=move.Groups[2].Value[0]-'1',col=char.ToLowerInvariant(move.Groups[1].Value[0])-'a';
                    if(!CanFlipWhite(board.Cells,row,col)) throw new InvalidOperationException("Egaroucid 给出的白棋落子不合法");
                    return row*8+col;
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
        static bool CanFlipWhite(string cells,int row,int col) {
            if(cells[row*8+col]!='0') return false;
            for(int dr=-1;dr<=1;dr++) {
                for(int dc=-1;dc<=1;dc++) {
                    if(dr==0&&dc==0) continue;
                    int r=row+dr,c=col+dc;
                    if(r<0||r>=8||c<0||c>=8||cells[r*8+c]!='1') continue;
                    do { r+=dr;c+=dc; }
                    while(r>=0&&r<8&&c>=0&&c<8&&cells[r*8+c]=='1');
                    if(r>=0&&r<8&&c>=0&&c<8&&cells[r*8+c]=='2') return true;
                }
            }
            return false;
        }
    }
}
