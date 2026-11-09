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
    public sealed class Prototype : Form {
        readonly BridgeLink bridge=new BridgeLink(); Profile profile=new Profile();
        readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly NotifyIcon tray=new NotifyIcon();
        readonly Label status=new Label(), active=new Label(), values=new Label();
        readonly TrackBar rudder=new TrackBar(), throttle=new TrackBar();
        readonly CheckBox usb=new CheckBox(), ble=new CheckBox(), freeze=new CheckBox(), demo=new CheckBox(), spring=new CheckBox();
        readonly Button[] buttons=new Button[4];
        readonly RichTextBox log=new RichTextBox();
        byte buttonMask; string previous="";
        static readonly Color Background=Color.FromArgb(17,23,33), PanelColor=Color.FromArgb(28,36,49), Accent=Color.FromArgb(78,213,180), Muted=Color.FromArgb(162,177,196);
        Label TextAt(string text,int x,int y,int width,int height,int size,Color color) {
            Label l=new Label();l.Text=text;l.SetBounds(x,y,width,height);l.Font=new Font("Segoe UI",size);l.ForeColor=color;Controls.Add(l);return l;
        }
        Button Action(string text,int x,int y,int w,EventHandler handler) {
            Button b=new Button();b.Text=text;b.SetBounds(x,y,w,36);b.BackColor=PanelColor;b.ForeColor=ForeColor;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(58,74,94);b.Click+=handler;Controls.Add(b);return b;
        }
        void Check(CheckBox c,string text,int x,int y,bool check) {c.Text=text;c.Checked=check;c.SetBounds(x,y,260,28);c.ForeColor=ForeColor;Controls.Add(c);}
        void Slider(TrackBar t,int x,int y,int value) {t.Minimum=0;t.Maximum=32767;t.Value=value;t.TickStyle=TickStyle.None;t.SmallChange=328;t.LargeChange=3277;t.SetBounds(x,y,330,42);Controls.Add(t);}
        public Prototype() {
            Text="MinimalJoystick — Software Prototype";ClientSize=new Size(960,720);MinimumSize=MaximumSize=Size;AutoScaleMode=AutoScaleMode.None;
            StartPosition=FormStartPosition.CenterScreen;BackColor=Background;ForeColor=Color.White;Font=new Font("Segoe UI",10);DoubleBuffered=true;
            TextAt("MinimalJoystick",32,24,600,44,26,Color.White);
            TextAt("USB BRIDGE CANDIDATE  /  0.3",34,72,600,25,10,Accent);
            Action("Physical USB / calibration",690,44,225,delegate {new HardwareUsbEditor(bridge).Show(this);});
            status.SetBounds(34,112,895,46);status.ForeColor=Muted;Controls.Add(status);
            TextAt("Controls",32,177,350,32,18,Color.White);
            TextAt("Transport laboratory",535,177,390,32,18,Color.White);
            TextAt("Rudder  ·  Rz axis",34,225,350,24,11,Muted);Slider(rudder,28,260,16384);
            Check(spring,"Spring return on mouse release",34,307,true);
            rudder.MouseUp+=delegate {if(spring.Checked)rudder.Value=16384;};
            TextAt("Throttle  ·  Slider axis",34,354,350,24,11,Muted);Slider(throttle,28,390,0);
            string[] names={"Gear · 1","Brake L · 2","Brake C · 3","Brake R · 4"};
            for(int i=0;i<4;i++) {
                int bit=1<<i;Button b=Action(names[i],34+(i%2)*190,451+(i/2)*48,178,delegate{});buttons[i]=b;
                b.MouseDown+=delegate {buttonMask|=(byte)bit;b.BackColor=Color.FromArgb(37,96,88);};
                b.MouseUp+=delegate {buttonMask&=(byte)~bit;b.BackColor=PanelColor;};
                b.MouseCaptureChanged+=delegate {if(!b.Capture){buttonMask&=(byte)~bit;b.BackColor=PanelColor;}};
                b.KeyDown+=delegate(object o,KeyEventArgs e){if(e.KeyCode==Keys.Space){buttonMask|=(byte)bit;e.Handled=true;}};
                b.KeyUp+=delegate(object o,KeyEventArgs e){if(e.KeyCode==Keys.Space){buttonMask&=(byte)~bit;e.Handled=true;}};
                b.LostFocus+=delegate {buttonMask&=(byte)~bit;};
            }
            Check(usb,"USB connected (simulated)",537,225,true);
            Check(ble,"Bluetooth connected (simulated)",537,265,true);
            Check(freeze,"Freeze packets / test timeout",537,305,false);
            Check(demo,"Run motion loop while flying",537,345,false);
            active.SetBounds(537,392,380,34);active.ForeColor=Accent;Controls.Add(active);
            values.SetBounds(537,428,380,60);values.ForeColor=Muted;Controls.Add(values);
            Action("Open Windows controller test",537,499,370,delegate {System.Diagnostics.Process.Start("control.exe","joy.cpl");});
            Action("Restart bridge",537,545,178,delegate {try{Bridge.Restart(bridge);}catch(Exception e){MessageBox.Show(e.Message);}});
            Action("Sim calibration",729,545,178,delegate {new ProfileEditor(profile,delegate{return new ControlsState((ushort)rudder.Value,(ushort)throttle.Value,buttonMask);},delegate(Profile p){profile=p;bridge.Profile(p);}).Show(this);});
            Action("Control diagram",34,545,178,delegate {new ControlDiagram().Show(this);});
            Action("Stop bridge",224,545,178,delegate {bridge.Command(16,new ControlsState(16384,(ushort)throttle.Value,0));});
            log.SetBounds(34,596,873,72);log.ReadOnly=true;log.BackColor=PanelColor;log.ForeColor=Muted;log.BorderStyle=BorderStyle.None;log.TabStop=false;Controls.Add(log);
            TextAt("One Windows identity · USB priority · 250 ms source timeout · driver watchdog",34,683,875,24,9,Muted);
            tray.Icon=SystemIcons.Application;tray.Text="MinimalJoystick input bridge";tray.Visible=true;
            ContextMenuStrip menu=new ContextMenuStrip();menu.Items.Add("Show controls",null,delegate {Show();WindowState=FormWindowState.Normal;Activate();});
            menu.Items.Add("Exit window (bridge continues)",null,delegate {Close();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate {Show();Activate();};
            FormClosed+=delegate {timer.Stop();bridge.Dispose();tray.Dispose();};
            try{if(File.Exists(Profile.DefaultPath))profile=Profile.Load(Profile.DefaultPath);}catch(Exception e){MessageBox.Show("Saved profile could not be loaded; defaults selected. "+e.Message);}
            if(BridgeLink.Alive(bridge.Read())&&bridge.Read().BridgeVersion<3){try{Bridge.Restart(bridge);}catch(Exception e){MessageBox.Show("The older bridge must stop before physical USB can be used. "+e.Message);}}
            if(!BridgeLink.Alive(bridge.Read())){try{bridge.ConfigureHardware(File.Exists(HardwareProfile.DefaultPath)?HardwareProfile.Load(HardwareProfile.DefaultPath):new HardwareProfile());}catch(Exception e){bridge.ConfigureHardware(new HardwareProfile());MessageBox.Show("Hardware USB settings could not be loaded; hardware mode is off. "+e.Message);}}
            bridge.Profile(profile);bridge.Command(3,new ControlsState(16384,0,0));Bridge.Start();
            timer.Interval=40;timer.Tick+=Tick;timer.Start();
        }
        void Tick(object sender,EventArgs args) {
            int flags=(usb.Checked?1:0)|(ble.Checked?2:0)|(freeze.Checked?4:0)|(demo.Checked?8:0);
            Snapshot before=bridge.Read();
            if((before.Flags&16)!=0){status.Text="Bridge stopped — use Restart bridge to resume";return;}
            bridge.Command(flags,new ControlsState((ushort)rudder.Value,(ushort)throttle.Value,buttonMask));
            Snapshot output=bridge.Read();bool alive=BridgeLink.Alive(output);
            status.Text=!alive?"Bridge offline — use Restart bridge":output.Connected?"MinimalJoystick connected · independent background bridge":"Bridge running · driver unavailable";
            status.ForeColor=alive&&output.Connected?Accent:Color.FromArgb(246,192,105);
            HardwareProfile hardware=bridge.ReadHardwareConfig();
            rudder.Enabled=throttle.Enabled=usb.Enabled=ble.Enabled=freeze.Enabled=demo.Enabled=spring.Enabled=!hardware.Enabled;foreach(Button b in buttons)b.Enabled=!hardware.Enabled;
            active.Text=output.Transport==3?"USB (physical)":hardware.Enabled?"USB hardware / waiting or fault":output.Transport==1?"USB (simulated)":output.Transport==2?"Bluetooth (simulated)":"Disconnected / stale";
            values.Text=String.Format("Raw R {0}  T {1}\nOutput R {2}  T {3} · Buttons {4}",rudder.Value,throttle.Value,output.Output.Rudder,output.Output.Throttle,output.Output.Buttons);

            if(hardware.Enabled){HardwareSnapshot h=bridge.ReadHardware();values.Text=h.Frame==null?h.Status:String.Format("Raw angle {0} · ADC {1}\nOutput R {2} T {3} · Buttons {4}",h.Frame.Angle,h.Frame.Adc,output.Output.Rudder,output.Output.Throttle,output.Output.Buttons);}
            string transition=active.Text+" | "+status.Text;
            if(transition!=previous){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+transition+Environment.NewLine);previous=transition;if(log.TextLength>12000)log.Text=log.Text.Substring(log.TextLength-6000);log.ScrollToCaret();}
        }
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
