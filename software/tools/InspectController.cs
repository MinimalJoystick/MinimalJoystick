using System;
using System.Runtime.InteropServices;
using System.Text;
class InspectController {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Caps {
        public ushort Mid,Pid;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Name;
        public uint Xmin,Xmax,Ymin,Ymax,Zmin,Zmax,Buttons,PeriodMin,PeriodMax,Rmin,Rmax,Umin,Umax,Vmin,Vmax,Flags,MaxAxes,Axes,MaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Registry;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Oem;
    }
    [DllImport("winmm.dll")] static extern uint joyGetNumDevs();
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] static extern uint joyGetDevCaps(UIntPtr id,out Caps caps,uint size);
    static int Main(string[] args) {
        StringBuilder report=new StringBuilder();int count=0;
        for(uint i=0;i<joyGetNumDevs();i++){
            Caps c;if(joyGetDevCaps(new UIntPtr(i),out c,(uint)Marshal.SizeOf(typeof(Caps)))!=0)continue;
            report.AppendLine(String.Format("Windows joystick {0}: {1}; axes={2}; buttons={3}; VID={4:X4}; PID={5:X4}",i,c.Name,c.Axes,c.Buttons,c.Mid,c.Pid));
            if(c.Mid==0x1209 && c.Pid==0x4D4A) {count++;if(c.Axes!=2 || c.Buttons!=4)throw new Exception("Unexpected capabilities");}
        }
        report.AppendLine(count==1?"PASS: Windows multimedia joystick API sees exactly one prototype VID/PID with two axes and four buttons. Its legacy driver label is not the DirectInput product name.":"FAIL: prototype count="+count);
        Console.Write(report.ToString());if(args.Length==1)System.IO.File.WriteAllText(args[0],report.ToString());return count==1?0:1;
    }
}
