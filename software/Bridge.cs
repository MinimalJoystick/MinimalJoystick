using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
namespace MinimalJoystick {
    // Session-local IPC, accessible only by this Windows user. One process owns HID.
    public sealed class BridgeLink : IDisposable {
        public static readonly string Name="Local\\MinimalJoystickV2-"+WindowsIdentity.GetCurrent().User.Value;
        readonly MemoryMappedFile map;readonly MemoryMappedViewAccessor view;readonly Mutex gate;
        public BridgeLink(){
            SecurityIdentifier user=WindowsIdentity.GetCurrent().User;
            MemoryMappedFileSecurity acl=new MemoryMappedFileSecurity();acl.AddAccessRule(new AccessRule<MemoryMappedFileRights>(user,MemoryMappedFileRights.FullControl,AccessControlType.Allow));
            map=MemoryMappedFile.CreateOrOpen(Name,512,MemoryMappedFileAccess.ReadWrite,MemoryMappedFileOptions.None,acl,HandleInheritability.None);
            view=map.CreateViewAccessor();MutexSecurity ma=new MutexSecurity();ma.AddAccessRule(new MutexAccessRule(user,MutexRights.FullControl,AccessControlType.Allow));
            bool created;gate=new Mutex(false,Name+"-gate",out created,ma);
        }
        bool Lock(){try{return gate.WaitOne(100);}catch(AbandonedMutexException){return true;}}
        public void Command(int flags,ControlsState s){if(!Lock())return;try{view.Write(0,1);view.Write(4,flags);view.Write(8,s.Rudder);view.Write(10,s.Throttle);view.Write(12,s.Buttons);view.Write(16,DateTime.UtcNow.Ticks);}finally{gate.ReleaseMutex();}}
        public void Profile(Profile p){if(!Lock())return;try{view.Write(64,p.Min);view.Write(68,p.Center);view.Write(72,p.Max);view.Write(76,p.ThrottleMin);view.Write(80,p.ThrottleMax);view.Write(84,p.Deadzone);view.Write(92,p.Exponent);view.Write(100,p.InvertRudder);view.Write(101,p.InvertThrottle);view.Write(104,1);}finally{gate.ReleaseMutex();}}
        public Snapshot Read(){if(!Lock())return new Snapshot();try{return new Snapshot{Version=view.ReadInt32(0),Flags=view.ReadInt32(4),Raw=new ControlsState(view.ReadUInt16(8),view.ReadUInt16(10),view.ReadByte(12)),CommandTime=view.ReadInt64(16),Heartbeat=view.ReadInt64(24),Output=new ControlsState(view.ReadUInt16(32),view.ReadUInt16(34),view.ReadByte(36)),Connected=view.ReadBoolean(37),Transport=view.ReadInt32(40),Reports=view.ReadInt64(48)};}finally{gate.ReleaseMutex();}}
        public Profile ReadProfile(){if(!Lock())return new Profile();try{if(view.ReadInt32(104)!=1)return new Profile();Profile p=new Profile{Min=view.ReadInt32(64),Center=view.ReadInt32(68),Max=view.ReadInt32(72),ThrottleMin=view.ReadInt32(76),ThrottleMax=view.ReadInt32(80),Deadzone=view.ReadDouble(84),Exponent=view.ReadDouble(92),InvertRudder=view.ReadBoolean(100),InvertThrottle=view.ReadBoolean(101)};p.Validate();return p;}catch{return new Profile();}finally{gate.ReleaseMutex();}}
        public void Publish(ControlsState s,bool connected,int transport,long reports){if(!Lock())return;try{view.Write(24,DateTime.UtcNow.Ticks);view.Write(32,s.Rudder);view.Write(34,s.Throttle);view.Write(36,s.Buttons);view.Write(37,connected);view.Write(40,transport);view.Write(48,reports);}finally{gate.ReleaseMutex();}}
        public static bool Alive(Snapshot s){return s.Heartbeat>0 && Math.Abs(DateTime.UtcNow.Ticks-s.Heartbeat)<TimeSpan.TicksPerSecond*2;}
        public void Dispose(){view.Dispose();map.Dispose();gate.Dispose();}
    }
    public struct Snapshot {public int Version,Flags,Transport;public ControlsState Raw,Output;public long CommandTime,Heartbeat,Reports;public bool Connected;}
    public static class Bridge {
        public static System.Diagnostics.Process Start(){System.Diagnostics.ProcessStartInfo p=new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--bridge");p.UseShellExecute=false;p.CreateNoWindow=true;return System.Diagnostics.Process.Start(p);}
        public static void Run(){
            using(Mutex owner=new Mutex(false,BridgeLink.Name+"-owner")){
                bool owned;try{owned=owner.WaitOne(0);}catch(AbandonedMutexException){owned=true;}if(!owned)return;
                try{using(BridgeLink ipc=new BridgeLink())using(HidSink sink=new HidSink()){
                    InputMux mux=new InputMux();var clock=System.Diagnostics.Stopwatch.StartNew();long seq=0,nextOpen=0;
                    while(true){
                        Snapshot command=ipc.Read();if(command.Version==1&&(command.Flags&16)!=0)break;
                        long now=clock.ElapsedMilliseconds;ControlsState raw=command.Raw;
                        // Keep synthetic axes/demo alive when UI closes; never latch a UI button.
                        if(DateTime.UtcNow.Ticks-command.CommandTime>TimeSpan.TicksPerMillisecond*250)raw.Buttons=0;
                        if((command.Flags&8)!=0){raw.Rudder=(ushort)(16383.5+Math.Sin(now/1600.0)*14000);raw.Throttle=(ushort)(16383.5+Math.Sin(now/4000.0)*13000);}
                        ControlsState input=ipc.ReadProfile().Apply(raw);
                        mux.Usb.Connected=(command.Flags&1)!=0;mux.Ble.Connected=(command.Flags&2)!=0;
                        if((command.Flags&4)==0){if(mux.Usb.Connected)mux.Usb.Receive(now,seq,input);if(mux.Ble.Connected)mux.Ble.Receive(now,seq,input);}
                        ControlsState output=mux.Select(now);
                        if(!sink.Connected&&now>=nextOpen){nextOpen=now+3000;sink.Open();}
                        sink.Send(output);ipc.Publish(output,sink.Connected,mux.Active.StartsWith("USB")?1:mux.Active.StartsWith("Bluetooth")?2:0,++seq);Thread.Sleep(20);
                    }
                    Snapshot last=ipc.Read();sink.Send(new ControlsState(16384,last.Output.Throttle,0));ipc.Publish(new ControlsState(16384,last.Output.Throttle,0),false,0,seq);
                }}finally{owner.ReleaseMutex();}
            }
        }
    }
}
