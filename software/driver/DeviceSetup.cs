// Root-device lifecycle helper. Does not change certificate trust or boot policy.
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
class DeviceSetup {
    [StructLayout(LayoutKind.Sequential)] struct DevInfo {public int Size;public Guid Class;public uint DevInst;public IntPtr Reserved;}
    [DllImport("setupapi.dll",SetLastError=true)] static extern IntPtr SetupDiCreateDeviceInfoList(ref Guid c,IntPtr parent);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiOpenDeviceInfo(IntPtr s,string id,IntPtr p,uint flags,ref DevInfo d);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiCreateDeviceInfo(IntPtr s,string id,ref Guid c,string desc,IntPtr p,uint flags,ref DevInfo d);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiSetDeviceRegistryProperty(IntPtr s,ref DevInfo d,uint prop,byte[] b,uint n);
    [DllImport("setupapi.dll",SetLastError=true)] static extern bool SetupDiCallClassInstaller(uint function,IntPtr s,ref DevInfo d);
    [DllImport("setupapi.dll",SetLastError=true)] static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);
    [DllImport("newdev.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool UpdateDriverForPlugAndPlayDevices(IntPtr p,string id,string inf,uint flags,out bool reboot);
    static void Check(bool success){if(!success)throw new Win32Exception(Marshal.GetLastWin32Error());}
    static int Main(string[] args){
        if(args.Length!=1){Console.Error.WriteLine("Usage: DeviceSetup.exe <absolute signed INF path>");return 2;}
        Guid cls=new Guid("745A17A0-74D3-11D0-B6FE-00A0C90F57DA");
        IntPtr set=SetupDiCreateDeviceInfoList(ref cls,IntPtr.Zero);if(set==new IntPtr(-1))return 1;
        bool created=false;DevInfo d=new DevInfo();d.Size=Marshal.SizeOf(d);
        try {
            if(!SetupDiOpenDeviceInfo(set,@"ROOT\MINIMALJOYSTICK\0000",IntPtr.Zero,0,ref d)){
                int openError=Marshal.GetLastWin32Error();
                if(openError!=433 && unchecked((uint)openError)!=0xE000020B)throw new Win32Exception(openError);
                Check(SetupDiCreateDeviceInfo(set,@"ROOT\MINIMALJOYSTICK\0000",ref cls,"MinimalJoystick",IntPtr.Zero,0,ref d));
                byte[] ids=Encoding.Unicode.GetBytes("root\\MinimalJoystick\0\0");
                Check(SetupDiSetDeviceRegistryProperty(set,ref d,1,ids,(uint)ids.Length));
                Check(SetupDiCallClassInstaller(0x19,set,ref d));created=true;
            }
            bool reboot;Check(UpdateDriverForPlugAndPlayDevices(IntPtr.Zero,@"root\MinimalJoystick",System.IO.Path.GetFullPath(args[0]),1,out reboot));
            Console.WriteLine("MinimalJoystick root device ready; instance ROOT\\MINIMALJOYSTICK\\0000. Reboot required: "+reboot);return reboot?3010:0;
        }catch(Exception e){if(created)SetupDiCallClassInstaller(5,set,ref d);Console.Error.WriteLine(e.Message);return 1;}
        finally {SetupDiDestroyDeviceInfoList(set);}
    }
}
