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
        readonly MemoryMappedFile map;readonly MemoryMappedViewAccessor view;readonly Mutex gate;HardwareProfile cachedHardware=new HardwareProfile();
        public BridgeLink(string testName=null){
            string name=testName??Name;
            SecurityIdentifier user=WindowsIdentity.GetCurrent().User;
            MemoryMappedFileSecurity acl=new MemoryMappedFileSecurity();acl.AddAccessRule(new AccessRule<MemoryMappedFileRights>(user,MemoryMappedFileRights.FullControl,AccessControlType.Allow));
            map=MemoryMappedFile.CreateOrOpen(name,512,MemoryMappedFileAccess.ReadWrite,MemoryMappedFileOptions.None,acl,HandleInheritability.None);
            view=map.CreateViewAccessor();MutexSecurity ma=new MutexSecurity();ma.AddAccessRule(new MutexAccessRule(user,MutexRights.FullControl,AccessControlType.Allow));
            bool created;gate=new Mutex(false,name+"-gate",out created,ma);
        }
        bool Lock(){try{return gate.WaitOne(100);}catch(AbandonedMutexException){return true;}}
        public void Command(int flags,ControlsState s){if(!Lock())return;try{view.Write(0,1);view.Write(4,flags);view.Write(8,s.Rudder);view.Write(10,s.Throttle);view.Write(12,s.Buttons);view.Write(16,DateTime.UtcNow.Ticks);}finally{gate.ReleaseMutex();}}
        public void Profile(Profile p){if(!Lock())return;try{view.Write(64,p.Min);view.Write(68,p.Center);view.Write(72,p.Max);view.Write(76,p.ThrottleMin);view.Write(80,p.ThrottleMax);view.Write(84,p.Deadzone);view.Write(92,p.Exponent);view.Write(100,p.InvertRudder);view.Write(101,p.InvertThrottle);view.Write(104,1);}finally{gate.ReleaseMutex();}}
        public Snapshot Read(){if(!Lock())return new Snapshot();try{return new Snapshot{Version=view.ReadInt32(0),Flags=view.ReadInt32(4),Raw=new ControlsState(view.ReadUInt16(8),view.ReadUInt16(10),view.ReadByte(12)),CommandTime=view.ReadInt64(16),Heartbeat=view.ReadInt64(24),Output=new ControlsState(view.ReadUInt16(32),view.ReadUInt16(34),view.ReadByte(36)),Connected=view.ReadBoolean(37),Transport=view.ReadInt32(40),BridgeVersion=view.ReadInt64(496)==view.ReadInt64(24)?view.ReadInt32(492):0,Reports=view.ReadInt64(48)};}finally{gate.ReleaseMutex();}}
        public Profile ReadProfile(){if(!Lock())return new Profile();try{if(view.ReadInt32(104)!=1)return new Profile();Profile p=new Profile{Min=view.ReadInt32(64),Center=view.ReadInt32(68),Max=view.ReadInt32(72),ThrottleMin=view.ReadInt32(76),ThrottleMax=view.ReadInt32(80),Deadzone=view.ReadDouble(84),Exponent=view.ReadDouble(92),InvertRudder=view.ReadBoolean(100),InvertThrottle=view.ReadBoolean(101)};p.Validate();return p;}catch{return new Profile();}finally{gate.ReleaseMutex();}}
        public void Publish(ControlsState s,bool connected,int transport,long reports){if(!Lock())return;try{long heartbeat=DateTime.UtcNow.Ticks;view.Write(24,heartbeat);view.Write(492,3);view.Write(496,heartbeat);view.Write(32,s.Rudder);view.Write(34,s.Throttle);view.Write(36,s.Buttons);view.Write(37,connected);view.Write(40,transport);view.Write(48,reports);}finally{gate.ReleaseMutex();}}
        public static bool Alive(Snapshot s){return s.Heartbeat>0 && Math.Abs(DateTime.UtcNow.Ticks-s.Heartbeat)<TimeSpan.TicksPerSecond*2;}
        void WriteText(int offset,int size,string value){byte[] data=new byte[size];byte[] encoded=System.Text.Encoding.UTF8.GetBytes(value??"");Array.Copy(encoded,data,Math.Min(size-1,encoded.Length));view.WriteArray(offset,data,0,size);}
        string ReadText(int offset,int size){byte[] data=new byte[size];view.ReadArray(offset,data,0,size);return System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0');}
        public void ConfigureHardware(HardwareProfile p){p.Validate();if(!Lock())return;try{view.Write(128,1);view.Write(132,p.Enabled);view.Write(133,p.Calibrated);view.Write(134,p.InvertRudder);view.Write(135,p.InvertThrottle);view.Write(136,p.Left);view.Write(140,p.Center);view.Write(144,p.Right);view.Write(148,p.ThrottleIdle);view.Write(152,p.ThrottleFull);view.Write(156,p.Deadzone);view.Write(164,p.Exponent);WriteText(172,32,p.Port);view.Write(208,view.ReadInt32(208)+1);}finally{gate.ReleaseMutex();}}
        public HardwareProfile ReadHardwareConfig(){if(!Lock())return cachedHardware;try{if(view.ReadInt32(128)!=1)return new HardwareProfile();var p=new HardwareProfile{Enabled=view.ReadBoolean(132),Calibrated=view.ReadBoolean(133),InvertRudder=view.ReadBoolean(134),InvertThrottle=view.ReadBoolean(135),Left=view.ReadInt32(136),Center=view.ReadInt32(140),Right=view.ReadInt32(144),ThrottleIdle=view.ReadInt32(148),ThrottleFull=view.ReadInt32(152),Deadzone=view.ReadDouble(156),Exponent=view.ReadDouble(164),Port=ReadText(172,32),Generation=view.ReadInt32(208)};p.Validate();cachedHardware=p;return p;}catch{cachedHardware=new HardwareProfile{Enabled=true};return cachedHardware;}finally{gate.ReleaseMutex();}}
        public void PublishHardware(UsbFrame f,string status,bool fresh,long ageMilliseconds=0){if(!Lock())return;try{view.Write(224,fresh?DateTime.UtcNow.Ticks-TimeSpan.TicksPerMillisecond*ageMilliseconds:0L);view.Write(244,f!=null);if(f!=null){view.Write(232,f.Angle);view.Write(234,f.Adc);view.Write(236,f.Buttons);view.Write(237,f.Flags);view.Write(238,f.Magnet);view.Write(239,f.Agc);view.Write(240,f.Millivolts);view.Write(242,f.Soc256);}WriteText(248,240,status);}finally{gate.ReleaseMutex();}}
        public HardwareSnapshot ReadHardware(){if(!Lock())return new HardwareSnapshot();try{return new HardwareSnapshot{Time=view.ReadInt64(224),Status=ReadText(248,240),Frame=view.ReadBoolean(244)?new UsbFrame{Angle=view.ReadUInt16(232),Adc=view.ReadUInt16(234),Buttons=view.ReadByte(236),Flags=view.ReadByte(237),Magnet=view.ReadByte(238),Agc=view.ReadByte(239),Millivolts=view.ReadUInt16(240),Soc256=view.ReadUInt16(242)}:null};}finally{gate.ReleaseMutex();}}
        public void Dispose(){view.Dispose();map.Dispose();gate.Dispose();}
    }
    public struct Snapshot {public int Version,Flags,Transport,BridgeVersion;public ControlsState Raw,Output;public long CommandTime,Heartbeat,Reports;public bool Connected;}
    public struct HardwareSnapshot {public long Time;public string Status;public UsbFrame Frame;public bool Fresh {get{return Frame!=null&&Time>0&&DateTime.UtcNow.Ticks-Time>=0&&DateTime.UtcNow.Ticks-Time<=TimeSpan.TicksPerMillisecond*250;}}}
    public static class Bridge {
        public static System.Diagnostics.Process Start(){System.Diagnostics.ProcessStartInfo p=new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--bridge");p.UseShellExecute=false;p.CreateNoWindow=true;return System.Diagnostics.Process.Start(p);}
        public static void Restart(BridgeLink ipc){
            Snapshot last=ipc.Read();ipc.Command(16,new ControlsState(16384,last.Output.Throttle,0));
            using(var owner=new Mutex(false,BridgeLink.Name+"-owner")){bool held;try{held=owner.WaitOne(2500);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("The existing bridge did not stop. Close the older app before restarting.");owner.ReleaseMutex();}
            ipc.Command(3,new ControlsState(16384,last.Output.Throttle,0));Start();
        }
        public static void Run(){
            using(Mutex owner=new Mutex(false,BridgeLink.Name+"-owner")){
                bool owned;try{owned=owner.WaitOne(0);}catch(AbandonedMutexException){owned=true;}if(!owned)return;
                try{using(BridgeLink ipc=new BridgeLink())using(HidSink sink=new HidSink())using(UsbTransport hardware=new UsbTransport()){
                    InputMux mux=new InputMux();var clock=System.Diagnostics.Stopwatch.StartNew();long seq=0,nextOpen=0,nextUsbOpen=0;bool wasHardware=false;int hardwareGeneration=-1;
                    while(true){
                        Snapshot command=ipc.Read();if(command.Version==1&&(command.Flags&16)!=0)break;
                        long now=clock.ElapsedMilliseconds;ControlsState raw=command.Raw;
                        // Keep synthetic axes/demo alive when UI closes; never latch a UI button.
                        if(DateTime.UtcNow.Ticks-command.CommandTime>TimeSpan.TicksPerMillisecond*250)raw.Buttons=0;
                        if((command.Flags&8)!=0){raw.Rudder=(ushort)(16383.5+Math.Sin(now/1600.0)*14000);raw.Throttle=(ushort)(16383.5+Math.Sin(now/4000.0)*13000);}
                        ControlsState input=ipc.ReadProfile().Apply(raw);
                        mux.Usb.Connected=(command.Flags&1)!=0;mux.Ble.Connected=(command.Flags&2)!=0;
                        if((command.Flags&4)==0){if(mux.Usb.Connected)mux.Usb.Receive(now,seq,input);if(mux.Ble.Connected)mux.Ble.Receive(now,seq,input);}
                        HardwareProfile hp=ipc.ReadHardwareConfig();ControlsState output;int transport;
                        if(hp.Enabled){
                            if(!wasHardware||hardwareGeneration!=hp.Generation){if(!wasHardware)hardware.Input.HoldThrottle(command.Output.Throttle);hardware.Dispose();mux.Usb.BeginSession();mux.Ble.BeginSession();nextUsbOpen=0;hardwareGeneration=hp.Generation;}
                            if(hardware.PortName!=hp.Port){hardware.Dispose();if(now>=nextUsbOpen){nextUsbOpen=now+2000;try{hardware.Open(hp.Port);}catch(Exception e){hardware.Error="USB unavailable: "+e.Message;}}}
                            hardware.Poll(now);output=hardware.Input.Select(now,hp);
                            bool fresh=hardware.Input.Fresh(now);transport=fresh&&hardware.Input.Latest.ControlsValid&&hp.Calibrated?3:0;
                            string detail=hardware.Error.Length>0?hardware.Error:!fresh?hardware.Input.Status+" / waiting or stale":hardware.Input.Status;
                            if(!hp.Calibrated)detail+="; calibrate to enable simulator input";
                            ipc.PublishHardware(hardware.Input.Latest,detail,fresh,Math.Max(0,now-hardware.Input.Received));
                        }else{if(wasHardware){hardware.Dispose();mux.Usb.BeginSession();mux.Ble.BeginSession();}output=mux.Select(now);transport=mux.Active.StartsWith("USB")?1:mux.Active.StartsWith("Bluetooth")?2:0;ipc.PublishHardware(null,"USB hardware mode off",false);}
                        wasHardware=hp.Enabled;
                        if(!sink.Connected&&now>=nextOpen){nextOpen=now+3000;sink.Open();}
                        sink.Send(output);ipc.Publish(output,sink.Connected,transport,++seq);Thread.Sleep(20);
                    }
                    ipc.PublishHardware(null,"Bridge stopped",false);Snapshot last=ipc.Read();sink.Send(new ControlsState(16384,last.Output.Throttle,0));ipc.Publish(new ControlsState(16384,last.Output.Throttle,0),false,0,seq);
                }}finally{owner.ReleaseMutex();}
            }
        }
    }
}
