using System;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;

namespace Othello.Basic {
    public interface ITransport : IDisposable {
        event Action<string> Received;
        event Action<string> Failed;
        void Open();
        void Send(string frame);
    }
    public sealed class SimulatedTransport : ITransport {
        const string SimulatorLibrary = "OthelloSimulatorV3.dll";
        [DllImport("kernel32.dll", EntryPoint="LoadLibraryW", CharSet=CharSet.Unicode, SetLastError=true, ExactSpelling=true)]
        static extern IntPtr LoadLibrary(string path);
        static readonly Lazy<IntPtr> SimulatorModule = new Lazy<IntPtr>(LoadSimulator);
        [DllImport(SimulatorLibrary,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sim_create();
        [DllImport(SimulatorLibrary,CallingConvention=CallingConvention.Cdecl)] static extern void sim_destroy(IntPtr game);
        [DllImport(SimulatorLibrary,CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)] static extern int sim_exchange(IntPtr game,string request,StringBuilder response,uint capacity);
        IntPtr game;
        public event Action<string> Received;
        public event Action<string> Failed;
        static IntPtr LoadSimulator() {
            // LabVIEW hosts this assembly with a working directory outside the project.
            string assemblyPath = new Uri(typeof(SimulatedTransport).Assembly.CodeBase).LocalPath;
            string dllPath = Path.Combine(Path.GetDirectoryName(assemblyPath), "native", SimulatorLibrary);
            IntPtr handle = LoadLibrary(dllPath);
            if (handle == IntPtr.Zero) {
                var error = new Win32Exception(Marshal.GetLastWin32Error());
                throw new InvalidOperationException("无法加载离线模拟库，请先构建上位机。" + error.Message, error);
            }
            return handle;
        }
        public void Open() {
            if (game != IntPtr.Zero) throw new InvalidOperationException("模拟下位机已经连接");
            if (SimulatorModule.Value != IntPtr.Zero) game = sim_create();
            if (game == IntPtr.Zero) throw new InvalidOperationException("无法创建模拟下位机");
        }
        public void Send(string frame) {
            if(game==IntPtr.Zero) throw new InvalidOperationException("模拟下位机未连接");
            StringBuilder reply=new StringBuilder(Protocol.MaxLine);
            if(sim_exchange(game,frame,reply,Protocol.MaxLine)<=0) { if(Failed!=null) Failed("模拟下位机无法生成响应"); return; }
            // Split replies to exercise the exact same streaming receiver as real UART.
            string text=reply.ToString(); int half=text.Length/2;
            if(Received!=null) { Received(text.Substring(0,half)); Received(text.Substring(half)); }
        }
        public void Dispose() { if(game!=IntPtr.Zero) { sim_destroy(game); game=IntPtr.Zero; } }
    }
    public sealed class SerialSettings {
        public string Port="COM1";
        public int Baud=115200, DataBits=8;
        public Parity Parity=Parity.None;
        public StopBits StopBits=StopBits.One;
        public void Validate() {
            if(string.IsNullOrWhiteSpace(Port)) throw new ArgumentException("请选择串口");
            if(Baud<300 || Baud>2000000 || DataBits<5 || DataBits>8 || StopBits==StopBits.None)
                throw new ArgumentException("串口参数不在支持范围");
            if(StopBits==StopBits.OnePointFive && DataBits!=5) throw new ArgumentException("1.5 停止位需要 5 数据位");
        }
    }
    public sealed class SerialTransport : ITransport {
        readonly SerialSettings settings;
        SerialPort port;
        volatile bool disposed;
        public event Action<string> Received;
        public event Action<string> Failed;
        public SerialTransport(SerialSettings settings) { this.settings=settings; }
        public void Open() {
            settings.Validate();
            port=new SerialPort(settings.Port,settings.Baud,settings.Parity,settings.DataBits,settings.StopBits);
            port.Handshake=Handshake.None; port.Encoding=Encoding.ASCII; port.NewLine="\n";
            port.ReadTimeout=300; port.WriteTimeout=300; port.DtrEnable=false; port.RtsEnable=false;
            port.DataReceived+=OnData; port.ErrorReceived+=OnError;
            port.Open(); port.DiscardInBuffer(); port.DiscardOutBuffer();
        }
        void OnData(object sender,SerialDataReceivedEventArgs args) {
            try { if(!disposed) { string chunk=port.ReadExisting(); if(Received!=null && chunk.Length>0) Received(chunk); } }
            catch(Exception ex) { if(!disposed && Failed!=null) Failed(ex.Message); }
        }
        void OnError(object sender,SerialErrorReceivedEventArgs args) { if(!disposed && Failed!=null) Failed("串口错误："+args.EventType); }
        public void Send(string frame) { if(disposed || port==null || !port.IsOpen) throw new InvalidOperationException("串口已断开"); port.Write(frame); }
        public void Dispose() {
            disposed=true;
            if(port!=null) { port.DataReceived-=OnData; port.ErrorReceived-=OnError; port.Dispose(); port=null; }
        }
    }
}
