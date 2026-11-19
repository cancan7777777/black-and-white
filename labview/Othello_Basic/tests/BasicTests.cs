using System;
using System.IO.Ports;
using Othello.Basic;

class FakeTransport : ITransport {
    public string Last;
    public event Action<string> Received;
    public event Action<string> Failed;
    public void Open() { }
    public void Send(string frame) { Last=frame; }
    public void Inject(string frame) { if(Received!=null) Received(frame); }
    public void Fail(string error) { if(Failed!=null) Failed(error); }
    public void Dispose() { }
}
class BasicTests {
    static int checks;
    static void Check(bool value,string description) { checks++;if(!value) throw new Exception("FAIL "+description); }
    static void Reject(Action action,string description) { bool threw=false;try{action();}catch(ArgumentException){threw=true;}catch(FormatException){threw=true;}Check(threw,description); }
    static string InitialBoard() { char[] b=new string('0',64).ToCharArray();b[27]='2';b[28]='1';b[35]='1';b[36]='2';return new string(b); }
    static string State(uint seq,uint rev,string cells,int turn,int status,int black,int white) { return Protocol.Frame("@STATE,"+seq+","+rev+","+turn+","+status+","+black+","+white+","+cells); }
    static int Main() {
        try {
            Check(Protocol.Crc("123456789")==0x29B1,"independent CCITT-FALSE vector");
            Check(Protocol.Fields("@GET,1,2792\n")[0]=="@GET","independent Python binascii GET frame vector");
            Reject(delegate { Protocol.Fields("@GET,1,0000\n"); },"bad CRC");
            Reject(delegate { Protocol.Number("4294967296"); },"U32 overflow");
            Reject(delegate { Protocol.ParseState(Protocol.Fields(State(1,1,new string('0',64),1,0,2,2))); },"inconsistent counts");
            LineBuffer buffer=new LineBuffer();int complete=0,overflows=0;buffer.Line+=delegate {complete++;};buffer.Error+=delegate{overflows++;};
            string valid=State(1,1,InitialBoard(),1,0,2,2);
            buffer.Feed(valid.Substring(0,10));Check(complete==0,"partial frame");buffer.Feed(valid.Substring(10)+valid);Check(complete==2,"fragmented and coalesced frames");
            buffer.Feed(new string('x',300)+"\n"+valid);Check(overflows==1 && complete==3,"bounded receiver recovers after overflow");
            using(HostSession realProtocol=new HostSession()) {
                FakeTransport fake=new FakeTransport();realProtocol.Connect(fake);
                fake.Inject(valid);Check(realProtocol.State.Black==2 && !realProtocol.Waiting,"confirmed initial state");
                string original=realProtocol.State.Cells;
                fake.Inject(State(0,2,InitialBoard(),2,0,2,2).Replace(",2,2,",",3,2,"));Check(realProtocol.State.Cells==original,"corrupt response does not change board");
                realProtocol.Move(2,3);Check(realProtocol.State.Revision==1 && realProtocol.Waiting,"no optimistic move");
                fake.Inject(Protocol.Frame("@ERROR,2,STALE"));Check(fake.Last.StartsWith("@GET,3,"),"STALE sends one GET, never retries MOVE");
                fake.Inject(State(3,2,InitialBoard(),1,0,2,2));Check(realProtocol.State.Revision==2 && !realProtocol.Waiting,"refresh after stale handles reentrant receiver");
                fake.Inject(State(0,1,InitialBoard(),1,0,2,2));Check(realProtocol.State.Revision==2,"older revision ignored");
                realProtocol.Disconnect();Check(realProtocol.State==null && !realProtocol.Connected,"disconnect clears pending state");
            }
            using(HostSession simulation=new HostSession()) {
                simulation.Connect(new SimulatedTransport());
                Check(simulation.State.Cells==InitialBoard() && simulation.State.Revision==1,"native C simulator initial board");
                simulation.Move(2,3);Check(simulation.State.Black==4 && simulation.State.White==1 && simulation.State.Revision==2,"native C legal flip");
                string before=simulation.State.Cells;simulation.Move(0,0);Check(simulation.State.Cells==before && simulation.State.Revision==2,"illegal move unchanged");
                int moves=0;
                while(simulation.State.Status==0 && moves<60) {
                    uint revision=simulation.State.Revision;bool advanced=false;
                    for(int i=0;i<64;i++) { simulation.Move(i/8,i%8);if(simulation.State.Revision!=revision) {advanced=true;break;} }
                    Check(advanced,"one legal move available before terminal state");moves++;
                }
                Check(simulation.State.Status==1 && simulation.State.Turn==0,"full offline game reaches course terminal rule");
                simulation.Disconnect();simulation.Connect(new SimulatedTransport());Check(simulation.State.Revision==1 && simulation.State.Black==2,"reconnect clears old revision");
            }
            Reject(delegate { new SerialSettings { DataBits=9 }.Validate(); },"invalid data bits");
            Reject(delegate { new SerialSettings { DataBits=8,StopBits=StopBits.OnePointFive }.Validate(); },"invalid 1.5 stop bits combination");
            Console.WriteLine("PASS "+checks+" checks: CRC, framing, malformed/stale state, native simulator, complete game, reconnect, serial settings");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error);return 1; }
    }
}
