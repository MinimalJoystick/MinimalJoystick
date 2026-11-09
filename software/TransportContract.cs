using System;
using System.IO;
namespace MinimalJoystick {
    // Proposed physical transport v2 reference core. Not connected to a radio or HID.
    public enum PhysicalTransport { Usb, Bluetooth }
    public sealed class MotionFrame {
        public uint Session,Uptime;
        public ushort Sequence,Angle,Adc;
        public byte Flags,Buttons,Battery=255;
        public bool ControlsValid {get{return (Flags&7)==7;}}
        public byte[] Encode(){
            if(Session==0||Angle>4095||Adc>4095||Buttons>15||(Flags&248)!=0||(Battery>100&&Battery!=255))throw new InvalidDataException("Invalid v2 motion frame.");
            byte[] p=new byte[20];p[0]=2;p[1]=Flags;
            Array.Copy(BitConverter.GetBytes(Sequence),0,p,2,2);Array.Copy(BitConverter.GetBytes(Session),0,p,4,4);Array.Copy(BitConverter.GetBytes(Uptime),0,p,8,4);
            Array.Copy(BitConverter.GetBytes(Angle),0,p,12,2);Array.Copy(BitConverter.GetBytes(Adc),0,p,14,2);p[16]=Buttons;p[17]=Battery;
            Array.Copy(BitConverter.GetBytes(UsbDecoder.Crc(p,18)),0,p,18,2);return p;
        }
        public static MotionFrame Decode(byte[] p){
            if(p==null||p.Length!=20||p[0]!=2||UsbDecoder.Crc(p,18)!=BitConverter.ToUInt16(p,18))throw new InvalidDataException("Invalid v2 length/version/CRC.");
            var f=new MotionFrame{Flags=p[1],Sequence=BitConverter.ToUInt16(p,2),Session=BitConverter.ToUInt32(p,4),Uptime=BitConverter.ToUInt32(p,8),Angle=BitConverter.ToUInt16(p,12),Adc=BitConverter.ToUInt16(p,14),Buttons=p[16],Battery=p[17]};
            f.Encode();return f;
        }
    }
    public sealed class PhysicalHandover {
        sealed class Channel {
            public bool Bound,Have,Qualified,Faulted;public uint Session,Uptime;public ushort Sequence;public long Arrival;
            public ControlsState Value;public bool Valid;
        }
        readonly Guid device;readonly Channel usb=new Channel(),ble=new Channel();ushort throttle;
        public string Active="Disconnected";
        public PhysicalHandover(Guid expectedDevice){if(expectedDevice==Guid.Empty)throw new ArgumentException("A selected device identity is required.");device=expectedDevice;}
        Channel Get(PhysicalTransport t){if(t==PhysicalTransport.Usb)return usb;if(t==PhysicalTransport.Bluetooth)return ble;throw new ArgumentOutOfRangeException("transport");}
        public void BeginSession(PhysicalTransport t,Guid identity,uint session){
            Channel c=Get(t);c.Bound=false;c.Have=false;c.Qualified=false;c.Faulted=false;c.Valid=false;
            if(identity!=device||session==0)throw new InvalidDataException("Physical device/session does not match the selected joystick.");c.Session=session;c.Bound=true;
        }
        public void Disconnect(PhysicalTransport t){Channel c=Get(t);c.Bound=false;c.Qualified=false;c.Valid=false;}
        public bool Receive(PhysicalTransport t,MotionFrame frame,ControlsState calibrated,long now){
            Channel c=Get(t);if(!c.Bound||c.Faulted||frame==null)return false;
            frame.Encode();if(calibrated.Rudder>32767||calibrated.Throttle>32767||calibrated.Buttons>15)throw new InvalidDataException("Invalid calibrated controls.");
            if(frame.Session!=c.Session){c.Faulted=true;c.Valid=false;return false;}
            if(c.Have){
                ushort seq=unchecked((ushort)(frame.Sequence-c.Sequence));uint ms=unchecked(frame.Uptime-c.Uptime);long host=now-c.Arrival;
                // Duplicate notifications do not renew freshness; reset/backlog latches this session.
                if(seq==0)return false;
                if(seq>=32768||ms==0||ms>=0x80000000||host<0||host-(long)ms>250){c.Faulted=true;c.Valid=false;return false;}
                if(host>=5)c.Qualified=true;
            }
            c.Have=true;c.Sequence=frame.Sequence;c.Uptime=frame.Uptime;c.Arrival=now;c.Valid=frame.ControlsValid;c.Value=calibrated;return true;
        }
        static bool Fresh(Channel c,long now){return c.Bound&&!c.Faulted&&c.Qualified&&c.Valid&&now>=c.Arrival&&now-c.Arrival<=250;}
        public ControlsState Select(long now){
            Channel c=Fresh(usb,now)?usb:Fresh(ble,now)?ble:null;
            Active=c==usb?"USB":c==ble?"Bluetooth":"Disconnected";
            if(c==null)return new ControlsState(16384,throttle,0);throttle=c.Value.Throttle;return c.Value;
        }
    }
    public static class TransportContractTests {
        static int checks;
        static void Check(bool value,string text){checks++;if(!value)throw new Exception("Transport contract: "+text);}
        static MotionFrame Frame(ushort seq,uint time){return new MotionFrame{Sequence=seq,Uptime=time,Session=7,Flags=7,Angle=4095,Adc=2048,Buttons=15};}
        public static void Run(string path){
            checks=0;var wire=Frame(65535,uint.MaxValue).Encode();Check(wire.Length==20,"default-MTU payload");var f=MotionFrame.Decode(wire);Check(f.Sequence==65535&&f.Uptime==uint.MaxValue&&f.Battery==255,"roundtrip");
            for(int bit=0;bit<160;bit++){byte[] corrupt=(byte[])wire.Clone();corrupt[bit/8]^=(byte)(1<<(bit%8));bool rejected=false;try{MotionFrame.Decode(corrupt);}catch(InvalidDataException){rejected=true;}Check(rejected,"single-bit corruption "+bit);}
            Guid id=Guid.NewGuid();var mux=new PhysicalHandover(id);bool wrong=false;try{mux.BeginSession(PhysicalTransport.Bluetooth,Guid.NewGuid(),7);}catch(InvalidDataException){wrong=true;}Check(wrong,"wrong unit refused");
            mux.BeginSession(PhysicalTransport.Usb,id,7);mux.BeginSession(PhysicalTransport.Bluetooth,id,7);
            var desired=new ControlsState(1234,5678,3);
            mux.Receive(PhysicalTransport.Usb,Frame(65535,uint.MaxValue-19),desired,0);Check(mux.Select(0).Buttons==0,"one queued frame cannot arm");
            mux.Receive(PhysicalTransport.Usb,Frame(0,0),desired,20);Check(mux.Select(20).Buttons==3,"sequence and uptime wrap");
            Check(!mux.Receive(PhysicalTransport.Usb,Frame(0,0),desired,240),"duplicate refused");Check(mux.Select(271).Buttons==0,"duplicate did not renew freshness");
            mux.BeginSession(PhysicalTransport.Usb,id,7);mux.BeginSession(PhysicalTransport.Bluetooth,id,7);
            for(int i=0;i<10000;i++){
                long now=i*60L;mux.BeginSession(PhysicalTransport.Usb,id,7);mux.BeginSession(PhysicalTransport.Bluetooth,id,7);
                foreach(PhysicalTransport t in new[]{PhysicalTransport.Usb,PhysicalTransport.Bluetooth}){mux.Receive(t,Frame(0,1),desired,now);mux.Receive(t,Frame(1,21),desired,now+20);}
                Check(mux.Select(now+20).Rudder==1234&&mux.Active=="USB","USB priority "+i);
                mux.Disconnect(PhysicalTransport.Usb);Check(mux.Select(now+20).Buttons==3&&mux.Active=="Bluetooth","same-unit BLE fallback "+i);
                mux.BeginSession(PhysicalTransport.Usb,id,7);
                mux.Receive(PhysicalTransport.Usb,Frame(0,1),desired,now+21);mux.Receive(PhysicalTransport.Usb,Frame(1,21),desired,now+41);
                Check(mux.Select(now+41).Throttle==5678&&mux.Active=="USB","USB recovery "+i);
                // Reset USB qualification for the next iteration; BLE remains monotonic.
                mux.BeginSession(PhysicalTransport.Usb,id,7);
            }
            Check(mux.Select(600300).Buttons==0&&mux.Select(600300).Rudder==16384&&mux.Select(600300).Throttle==5678,"both stale safe state");
            mux.BeginSession(PhysicalTransport.Bluetooth,id,7);mux.Receive(PhysicalTransport.Bluetooth,Frame(1,20),desired,0);
            Check(!mux.Receive(PhysicalTransport.Bluetooth,Frame(2,40),desired,500),"backlog refused");Check(mux.Select(500).Buttons==0,"backlog cannot arm");
            mux.BeginSession(PhysicalTransport.Bluetooth,id,7);var other=Frame(3,60);other.Session=8;Check(!mux.Receive(PhysicalTransport.Bluetooth,other,desired,520),"new boot needs handshake");
            File.WriteAllText(path,"PASS: "+checks+" protocol/transport checks; 20-byte frames, corruption rejection, device binding, wrap, duplicates, backlog and 10000 modeled handovers. No radio, COM, driver or simulator used.\n");
        }
    }
}
