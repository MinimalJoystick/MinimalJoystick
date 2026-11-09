using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace MinimalJoystick {
    public struct ControlsState {
        public ushort Rudder, Throttle;
        public byte Buttons;
        public ControlsState(ushort r, ushort t, byte b) { Rudder=r; Throttle=t; Buttons=b; }
        public byte[] Feature() { return new byte[] {2,1,(byte)Rudder,(byte)(Rudder>>8),(byte)Throttle,(byte)(Throttle>>8),Buttons,0}; }
    }
    public sealed class Source {
        public bool Connected;
        public long Received=-10000;
        public long Sequence=-1;
        public ControlsState Value;
        public void BeginSession() { Sequence=-1; Received=-10000; }
        public void Receive(long time, long sequence, ControlsState value) {
            if(sequence<=Sequence || value.Rudder>32767 || value.Throttle>32767 || value.Buttons>15) return;
            Sequence=sequence; Received=time; Value=value;
        }
    }
    public sealed class InputMux {
        public const string Identity="MinimalJoystick / MJ-PROTOTYPE-0001";
        public readonly Source Usb=new Source(), Ble=new Source();
        public string Active="Disconnected";
        private ushort lastThrottle;
        public ControlsState Select(long now) {
            Source s=null;
            if(Usb.Connected && now-Usb.Received>=0 && now-Usb.Received<=250) { s=Usb; Active="USB (simulated)"; }
            else if(Ble.Connected && now-Ble.Received>=0 && now-Ble.Received<=250) { s=Ble; Active="Bluetooth (simulated)"; }
            else Active="Disconnected / stale";
            if(s==null) return new ControlsState(16384,lastThrottle,0);
            lastThrottle=s.Value.Throttle; return s.Value;
        }
    }
    // The physical transport never determines the Windows controller identity.
    // This handle targets the one root-enumerated MinimalJoystick UMDF device.
    public sealed class HidSink : IDisposable {
        private SafeFileHandle handle;
        public string Status="Driver not connected";
        [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public int Size; public Guid Class; public int Flags; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] struct Attributes { public int Size; public ushort Vendor, Product, Version; }
        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid g);
        [DllImport("hid.dll",SetLastError=true)] static extern bool HidD_GetAttributes(SafeFileHandle h,ref Attributes a);
        [DllImport("hid.dll",SetLastError=true)] static extern bool HidD_GetProductString(SafeFileHandle h,byte[] b,int n);
        [DllImport("hid.dll",SetLastError=true)] static extern bool HidD_GetSerialNumberString(SafeFileHandle h,byte[] b,int n);
        [DllImport("hid.dll",SetLastError=true)] static extern bool HidD_SetFeature(SafeFileHandle h,byte[] b,int n);
        [DllImport("hid.dll",SetLastError=true)] static extern bool HidD_GetFeature(SafeFileHandle h,byte[] b,int n);
        [DllImport("hid.dll",SetLastError=true)] static extern bool HidD_GetInputReport(SafeFileHandle h,byte[] b,int n);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid g,string e,IntPtr p,int flags);
        [DllImport("setupapi.dll",SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr dev,ref Guid g,int i,ref InterfaceData data);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData data,IntPtr detail,int size,out int needed,IntPtr dev);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr sec,uint creation,uint flags,IntPtr template);
        public bool Connected { get { return handle!=null && !handle.IsInvalid && !handle.IsClosed; } }
        public bool Open() {
            Dispose(); Guid g; HidD_GetHidGuid(out g);
            IntPtr set=SetupDiGetClassDevs(ref g,null,IntPtr.Zero,18);
            if(set==new IntPtr(-1)) {Status="HID enumeration failed";return false;}
            try {
                for(int i=0;;i++) {
                    InterfaceData d=new InterfaceData(); d.Size=Marshal.SizeOf(d);
                    if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref g,i,ref d)) break;
                    int size; SetupDiGetDeviceInterfaceDetail(set,ref d,IntPtr.Zero,0,out size,IntPtr.Zero);
                    if(size<=0) continue;
                    IntPtr buffer=Marshal.AllocHGlobal(size);
                    try {
                        Marshal.WriteInt32(buffer,IntPtr.Size==8?8:6);
                        if(!SetupDiGetDeviceInterfaceDetail(set,ref d,buffer,size,out size,IntPtr.Zero)) continue;
                        string path=Marshal.PtrToStringUni(IntPtr.Add(buffer,4));
                        using(SafeFileHandle candidate=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                            Attributes a=new Attributes();a.Size=Marshal.SizeOf(a);
                            if(candidate.IsInvalid || !HidD_GetAttributes(candidate,ref a) || a.Vendor!=0x1209 || a.Product!=0x4D4A) continue;
                            byte[] name=new byte[256], serial=new byte[256];
                            if(!HidD_GetProductString(candidate,name,name.Length) || Encoding.Unicode.GetString(name).TrimEnd('\0')!="MinimalJoystick") continue;
                            if(!HidD_GetSerialNumberString(candidate,serial,serial.Length) || Encoding.Unicode.GetString(serial).TrimEnd('\0')!="MJ-PROTOTYPE-0001") continue;
                        }
                        handle=CreateFile(path,0xC0000000,3,IntPtr.Zero,3,0,IntPtr.Zero);
                        if(!Connected) { Dispose();Status="MinimalJoystick found; cannot open configuration channel ("+Marshal.GetLastWin32Error()+")";return false; }
                        byte[] version=new byte[8];version[0]=2;
                        if(!HidD_GetFeature(handle,version,8) || version[1]!=1) {Dispose();Status="Driver protocol mismatch";return false;}
                        Status="MinimalJoystick driver connected"; return true;
                    } finally { Marshal.FreeHGlobal(buffer); }
                }
            } finally { SetupDiDestroyDeviceInfoList(set); }
            Status="Driver missing — preview only; flight sims receive no input";return false;
        }
        public bool Send(ControlsState state) {
            if(!Connected) return false;
            byte[] b=state.Feature();
            if(HidD_SetFeature(handle,b,b.Length)) return true;
            int error=Marshal.GetLastWin32Error();Dispose();Status="Driver write failed ("+error+")";return false;
        }
        public ControlsState ReadInput() {
            byte[] b=new byte[6];b[0]=1;
            if(!Connected || !HidD_GetInputReport(handle,b,b.Length) || b[0]!=1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return new ControlsState(BitConverter.ToUInt16(b,1),BitConverter.ToUInt16(b,3),b[5]);
        }
        public void Dispose() { if(handle!=null) {handle.Dispose();handle=null;} }
    }
    static class Program {
        static void Assert(bool condition,string message) {if(!condition)throw new Exception(message);}
        static void Tests(string path) {
            InputMux m=new InputMux();m.Usb.Connected=m.Ble.Connected=true;
            m.Usb.Receive(0,1,new ControlsState(100,200,1));m.Ble.Receive(0,1,new ControlsState(300,400,2));
            Assert(m.Select(0).Rudder==100,"USB priority");m.Usb.Connected=false;
            Assert(m.Select(10).Rudder==300,"BLE fallback");
            Assert(m.Select(251).Buttons==0,"stale buttons released");Assert(m.Select(251).Rudder==16384,"stale rudder centered");Assert(m.Select(251).Throttle==400,"stale throttle held");
            m.Ble.Receive(260,2,new ControlsState(500,600,8));m.Ble.Receive(261,1,new ControlsState(999,999,15));
            Assert(m.Select(261).Rudder==500,"out of order packet rejected");
            m.Ble.Receive(262,3,new ControlsState(65535,0,0));Assert(m.Select(262).Rudder==500,"invalid range rejected");
            byte[] packet=new ControlsState(0x1234,0x5678,15).Feature();Assert(BitConverter.ToString(packet)=="02-01-34-12-78-56-0F-00","wire ABI");
            string identity=InputMux.Identity;
            for(int i=0;i<1000;i++){m.Usb.Connected=i%2==0;m.Usb.Receive(1000+i,i+10,new ControlsState(1000,2000,0));m.Ble.Receive(1000+i,i+10,new ControlsState(1000,2000,0));Assert(m.Select(1000+i).Rudder==1000,"handover value");Assert(InputMux.Identity==identity,"identity invariant");}
            File.WriteAllText(path,"PASS: USB priority, BLE fallback, stale input, throttle hold, packet ordering, bounds, wire ABI, 1000 handovers.\nDriver installation, actual HID enumeration and simulator binding persistence require integration testing.\n");
        }
        static void DriverTest(string path) {
            using(HidSink sink=new HidSink()) {
                Assert(sink.Open(),sink.Status);InputMux m=new InputMux();m.Ble.Connected=true;
                for(int i=0;i<100;i++) {
                    m.Usb.Connected=i%2==0;
                    ControlsState desired=new ControlsState((ushort)(i*300),(ushort)(32767-i*300),(byte)(i%16));
                    m.Usb.Receive(i,i,desired);m.Ble.Receive(i,i,desired);
                    Assert(sink.Send(m.Select(i)),sink.Status);ControlsState actual=sink.ReadInput();
                    Assert(actual.Rudder==desired.Rudder && actual.Throttle==desired.Throttle && actual.Buttons==desired.Buttons,"Driver input roundtrip "+i);
                }
                sink.Send(new ControlsState(16384,0,0));
                File.WriteAllText(path,"PASS: branded HID product and serial matched; feature protocol v1; 100 USB/BLE source handovers round-tripped through one open HID device handle.\nSimulator enumeration and binding persistence must still be checked in the simulator.\n");
            }
        }
        [STAThread] static int Main(string[] args) {
            if(args.Length==2&&args[0]=="--ui-preview"){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Prototype.SavePreviews(args[1]);return 0;}
            if(args.Length==2&&args[0]=="--transport-test"){try{TransportContractTests.Run(args[1]);return 0;}catch(Exception e){File.WriteAllText(args[1],e.ToString());return 1;}}
            if(args.Length==2&&args[0]=="--usb-driver-test"){try{UsbTests.Driver(args[1]);return 0;}catch(Exception e){File.WriteAllText(args[1],e.ToString());return 1;}}
            if(args.Length==2&&args[0]=="--usb-test"){try{UsbTests.Run(args[1]);return 0;}catch(Exception e){File.WriteAllText(args[1],e.ToString());return 1;}}
            if(args.Length==2&&args[0]=="--bridge-test"){try{Reliability.Integration(args[1]);return 0;}catch(Exception e){File.WriteAllText(args[1],e.ToString());return 1;}}
            if((args.Length==1&&args[0]=="--setup")||Path.GetFileNameWithoutExtension(Application.ExecutablePath)=="MinimalJoystickSetup"){Application.EnableVisualStyles();Application.Run(new Setup());return 0;}
            if(args.Length==1 && args[0]=="--bridge"){Bridge.Run();return 0;}
            if(args.Length==2 && args[0]=="--reliability-test") {try {Reliability.Run(args[1]);return 0;}catch(Exception e){File.WriteAllText(args[1],e.ToString());return 1;}}
            if(args.Length==2 && args[0]=="--self-test") {try {Tests(args[1]);return 0;}catch(Exception e){File.WriteAllText(args[1],e.ToString());return 1;}}
            if(args.Length==2 && args[0]=="--driver-test") {try {DriverTest(args[1]);return 0;}catch(Exception e){File.WriteAllText(args[1],e.ToString());return 1;}}
            using(Mutex single=new Mutex(false,"Local\\MinimalJoystickPrototypeBridge")) {
                if(!single.WaitOne(0,false)){MessageBox.Show("MinimalJoystick is already running. Open it from the notification area.");return 1;}
                try {Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Prototype());}
                finally {single.ReleaseMutex();}
            }return 0;
        }
    }
}
