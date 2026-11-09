using System;
using System.IO;
namespace MinimalJoystick {
    public static class Reliability {
        public static void Integration(string path){
            using(BridgeLink link=new BridgeLink()){
                Check(!BridgeLink.Alive(link.Read()),"Stop the existing bridge before integration tests");
                link.ConfigureHardware(new HardwareProfile());link.Profile(new Profile{Deadzone=0});link.Command(3,new ControlsState(12000,22000,15));
                var process=Bridge.Start();
                try {
                    Wait(delegate{return BridgeLink.Alive(link.Read())&&link.Read().Connected;},"bridge connection");
                    using(HidSink probe=new HidSink()){
                        Check(probe.Open(),probe.Status);long reports=link.Read().Reports;
                        System.Threading.Thread.Sleep(650);Check(link.Read().Reports>reports,"bridge continues without UI updates");Check(link.Read().Output.Buttons==0,"UI failure releases buttons");
                        for(int i=0;i<30;i++){link.Command(i%2==0?1:2,new ControlsState(10000,20000,0));int transport=i%2==0?1:2;Wait(delegate{return link.Read().Transport==transport&&link.Read().Output.Throttle==20000;},"handover");Check(probe.ReadInput().Throttle==20000,"HID throttle handover");}
                        link.Command(7,new ControlsState(12000,22000,15));System.Threading.Thread.Sleep(600);Check(probe.ReadInput().Buttons==0,"packet freeze button release");
                    }
                    process.Kill();process.WaitForExit();link.Command(3,new ControlsState(15000,21000,0));process=Bridge.Start();Wait(delegate{return BridgeLink.Alive(link.Read())&&link.Read().Reports<50;},"crash recovery");
                    link.Command(16,new ControlsState(16384,21000,0));Check(process.WaitForExit(5000),"clean shutdown");
                    File.WriteAllText(path,"PASS: independent bridge, UI heartbeat loss/button release, 30 actual HID source handovers, frozen packets, forced bridge crash/restart and clean stop. No simulator was started.\n");
                }finally{if(!process.HasExited){process.Kill();process.WaitForExit();}process.Dispose();}
            }
        }
        static void Wait(Func<bool> condition,string name){var until=DateTime.UtcNow.AddSeconds(5);while(DateTime.UtcNow<until){if(condition())return;System.Threading.Thread.Sleep(25);}throw new Exception("Timed out: "+name);}
        static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
        public static void Run(string path){
            InputMux m=new InputMux();m.Usb.Connected=m.Ble.Connected=true;
            m.Usb.Receive(0,0,new ControlsState(10,20,15));m.Ble.Receive(0,0,new ControlsState(30,40,0));
            Check(m.Select(250).Buttons==15,"250ms boundary");Check(m.Select(251).Buttons==0,"251ms boundary");
            m.Usb.Receive(260,1,new ControlsState(10,20,15));m.Ble.Receive(270,1,new ControlsState(30,40,0));
            Check(m.Select(511).Rudder==30,"stale USB falls back to fresh BLE");
            m.Usb.BeginSession();m.Usb.Receive(600,0,new ControlsState(1,2,0));Check(m.Select(600).Rudder==1,"reconnect sequence reset");
            Random r=new Random(20261109);
            m.Usb.BeginSession();m.Ble.BeginSession();
            Profile profile=new Profile{Deadzone=0,Exponent=1};Check(profile.Apply(new ControlsState(0,0,0)).Rudder==0,"left endpoint");Check(profile.Apply(new ControlsState(32767,32767,0)).Rudder==32767,"right endpoint");
            profile.InvertThrottle=true;Check(profile.Apply(new ControlsState(16384,0,0)).Throttle==32767,"inversion");profile.Exponent=double.NaN;bool invalid=false;try{profile.Validate();}catch(InvalidDataException){invalid=true;}Check(invalid,"reject invalid profile");
            for(int i=1;i<=100000;i++){
                long t=1000+i*300L;byte b=(byte)r.Next(16);ushort rudder=(ushort)r.Next(32768),throttle=(ushort)r.Next(32768);
                m.Usb.Connected=i%3==0;m.Ble.Connected=i%3!=2;
                m.Usb.Receive(t,i,new ControlsState(rudder,throttle,b));m.Ble.Receive(t,i,new ControlsState(rudder,throttle,b));
                ControlsState actual=m.Select(t);
                if(m.Usb.Connected||m.Ble.Connected)Check(actual.Rudder==rudder&&actual.Throttle==throttle&&actual.Buttons==b,"handover "+i);
                Check(m.Select(t+251).Buttons==0,"stuck button "+i);
                m.Usb.Receive(t+252,i-1,new ControlsState(0,0,15));m.Ble.Receive(t+252,i-1,new ControlsState(0,0,15));
                Check(m.Select(t+252).Buttons==0,"replayed packet "+i);
            }
            File.WriteAllText(path,"PASS: timeout boundaries, fresh BLE fallback, sequence reset, 100000 deterministic handover/disconnect/drop/replay/button-release scenarios.\nSynthetic model tests; process/driver integration is recorded separately.\n");
        }
    }
}
