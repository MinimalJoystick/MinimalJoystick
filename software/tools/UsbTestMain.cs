using System;
using System.IO;
namespace MinimalJoystick {
    public static class UsbTestMain {
        public static int Main(string[] args){try{if(args.Length!=1)throw new ArgumentException("Supply a test report path");UsbTests.Run(args[0]);Console.Write(File.ReadAllText(args[0]));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    }
}
