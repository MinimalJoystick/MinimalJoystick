using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
namespace MinimalJoystick {
    public sealed class UsbFrame {
        public uint Sequence,Uptime;
        public ushort Angle,Adc,Millivolts,Soc256;
        public byte Buttons,Flags,Magnet,Agc;
        public bool ControlsValid {get{return (Flags&27)==27 && (Magnet&56)==32;}}
    }
    public sealed class UsbDecoder {
        readonly List<byte> buffer=new List<byte>();
        public long Rejected;
        public static ushort Crc(byte[] b,int count){ushort c=65535;for(int i=0;i<count;i++){c^=(ushort)(b[i]<<8);for(int n=0;n<8;n++)c=(ushort)((c&32768)!=0?(c<<1)^0x1021:c<<1);}return c;}
        static ushort U16(byte[] p,int n){return (ushort)(p[n]|p[n+1]<<8);}
        public List<UsbFrame> Feed(byte[] data,int count){
            if(count<0||count>data.Length||count>4096)throw new ArgumentOutOfRangeException("count");
            for(int i=0;i<count;i++)buffer.Add(data[i]);
            var frames=new List<UsbFrame>();
            while(buffer.Count>=26){byte[] p=buffer.GetRange(0,26).ToArray();
                if(p[0]!=77||p[1]!=74||p[2]!=1||p[3]!=26||Crc(p,24)!=U16(p,24)||U16(p,12)>4095||U16(p,14)>4095||p[16]>15||(p[17]&224)!=0||((p[17]&4)!=0&&U16(p,22)>25600)){buffer.RemoveAt(0);Rejected++;continue;}
                frames.Add(new UsbFrame{Sequence=BitConverter.ToUInt32(p,4),Uptime=BitConverter.ToUInt32(p,8),Angle=U16(p,12),Adc=U16(p,14),Buttons=p[16],Flags=p[17],Magnet=p[18],Agc=p[19],Millivolts=U16(p,20),Soc256=U16(p,22)});buffer.RemoveRange(0,26);
            }return frames;
        }
    }
    [DataContract] public sealed class HardwareProfile {
        [DataMember] public int Version=1;
        [DataMember] public string Port="";
        [DataMember] public bool Enabled,Calibrated,InvertRudder,InvertThrottle;
        [DataMember] public int Left,Center,Right,ThrottleIdle,ThrottleFull;
        [DataMember] public double Deadzone=0.03,Exponent=1;
        public int Generation;
        public static int Wrap(int value){return ((value+2048)%4096+4096)%4096-2048;}
        public void Validate(){
            if(Version!=1||Port==null||Port.Length>16||(Port.Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(Port,"^COM[1-9][0-9]{0,3}$")))throw new InvalidDataException("Select an explicit COM port.");
            if(Enabled&&Port.Length==0)throw new InvalidDataException("USB mode requires a COM port.");
            foreach(int n in new[]{Left,Center,Right,ThrottleIdle,ThrottleFull})if(n<0||n>4095)throw new InvalidDataException("Raw calibration must be 0..4095.");
            if(double.IsNaN(Deadzone)||double.IsInfinity(Deadzone)||Deadzone<0||Deadzone>0.3||double.IsNaN(Exponent)||double.IsInfinity(Exponent)||Exponent<0.2||Exponent>3)throw new InvalidDataException("Invalid dead zone or response curve.");
            if(Calibrated){int l=Wrap(Left-Center),r=Wrap(Right-Center);if(Math.Abs(l)<32||Math.Abs(r)<32||Math.Abs(l)>=2048||Math.Abs(r)>=2048||Math.Sign(l)==Math.Sign(r)||Math.Abs(ThrottleFull-ThrottleIdle)<100)throw new InvalidDataException("Capture both sides of center (at least 32 angle counts each) and throttle endpoints (at least 100 ADC counts apart).");}
        }
        public ControlsState Apply(UsbFrame f){Validate();if(!Calibrated||!f.ControlsValid)throw new InvalidDataException("Valid sensors and complete hardware calibration are required.");
            int left=Wrap(Left-Center),right=Wrap(Right-Center),raw=Wrap(f.Angle-Center);double directed=raw*Math.Sign(right);
            double x=directed>=0?directed/Math.Abs(right):directed/Math.Abs(left);x=Math.Max(-1,Math.Min(1,x));double a=Math.Abs(x);x=a<=Deadzone?0:Math.Sign(x)*Math.Pow((a-Deadzone)/(1-Deadzone),Exponent);if(InvertRudder)x=-x;
            double t=Math.Max(0,Math.Min(1,(f.Adc-ThrottleIdle)/(double)(ThrottleFull-ThrottleIdle)));if(InvertThrottle)t=1-t;
            return new ControlsState((ushort)Math.Round((x+1)*16383.5),(ushort)Math.Round(t*32767),f.Buttons);
        }
        public static string DefaultPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MinimalJoystick","hardware-usb.json");}}
        public void Save(string path){Validate();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string tmp=path+".tmp-"+Guid.NewGuid().ToString("N");try{using(var f=File.Create(tmp)){new DataContractJsonSerializer(typeof(HardwareProfile)).WriteObject(f,this);f.Flush(true);}if(File.Exists(path))File.Replace(tmp,path,path+".bak");else File.Move(tmp,path);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
        public static HardwareProfile Load(string path){using(var f=File.OpenRead(path)){var p=(HardwareProfile)new DataContractJsonSerializer(typeof(HardwareProfile)).ReadObject(f);p.Validate();return p;}}
    }
    // Host-monotonic time governs freshness; device time validates stream progress.
    public sealed class HardwareInput {
        public UsbFrame Latest;public long Received=-10000;public string Status="Waiting for USB packets";
        bool haveSequence,faulted,qualified;uint sequence,uptime;long lastArrival;ushort lastThrottle;
        public void HoldThrottle(ushort value){lastThrottle=value;}
        public void Invalidate(){Latest=null;Received=-10000;}
        public void BeginSession(){Invalidate();haveSequence=false;faulted=false;qualified=false;Status="Waiting for USB packets";}
        public bool Receive(UsbFrame f,long now){
            if(faulted)return false;
            if(haveSequence){uint delta=unchecked(f.Sequence-sequence),elapsed=unchecked(f.Uptime-uptime);if(delta==0||delta>=0x80000000||elapsed==0||elapsed>=0x80000000){Status="Reset/replay detected: disconnect and reconnect USB in the app";Invalidate();faulted=true;return false;}}
            if(haveSequence){long hostGap=now-lastArrival;uint deviceGap=unchecked(f.Uptime-uptime);if(hostGap-deviceGap>250){Status="Delayed USB stream: reconnect diagnostics";Invalidate();faulted=true;return false;}if(hostGap>=5)qualified=true;}
            haveSequence=true;lastArrival=now;sequence=f.Sequence;uptime=f.Uptime;Latest=f;Received=now;Status=f.ControlsValid?"USB sensors valid":"Sensor fault: controls released";return true;
        }
        public bool Fresh(long now){return qualified&&Latest!=null&&now-Received>=0&&now-Received<=250;}
        public ControlsState Select(long now,HardwareProfile p){
            if(Fresh(now)&&Latest.ControlsValid&&p.Calibrated){ControlsState value=p.Apply(Latest);lastThrottle=value.Throttle;return value;}
            return new ControlsState(16384,lastThrottle,0);
        }
    }
    public sealed class UsbTransport : IDisposable {
        SerialPort port;UsbDecoder decoder=new UsbDecoder();
        public readonly HardwareInput Input=new HardwareInput();
        public string PortName {get{return port==null?"":port.PortName;}}
        public string Error="";
        public void Open(string name){Dispose();var candidate=new SerialPort(name,115200){DtrEnable=true,ReadTimeout=10,WriteTimeout=100,ReadBufferSize=4096};try{candidate.Open();candidate.DiscardInBuffer();port=candidate;decoder=new UsbDecoder();Input.BeginSession();Error="";}catch{candidate.Dispose();throw;}}
        public void Poll(long now){if(port==null)return;try{
            // Drop an OS backlog, rather than timestamping queued old controls as fresh.
            if(port.BytesToRead>104){port.DiscardInBuffer();decoder=new UsbDecoder();Input.Invalidate();Error="USB backlog discarded; waiting for live samples";return;}
            int count=Math.Min(port.BytesToRead,104);if(count==0)return;byte[] b=new byte[count];int n=port.Read(b,0,count);var frames=decoder.Feed(b,n);
            foreach(var frame in frames)Input.Receive(frame,now);
            if(frames.Count>0)Error="";
        }catch(TimeoutException){}catch(Exception e){Error="USB disconnected: "+e.Message;Dispose();}}
        public void Dispose(){if(port!=null){port.Dispose();port=null;}Input.BeginSession();}
    }
}
