using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms;
namespace MinimalJoystick {
    public sealed class HardwareUsbEditor : Form {
        readonly BridgeLink bridge;readonly ComboBox ports=new ComboBox();readonly Label live=new Label();
        readonly NumericUpDown[] fields=new NumericUpDown[5];readonly bool[] captured=new bool[5];
        readonly CheckBox invertR=new CheckBox(),invertT=new CheckBox();readonly NumericUpDown deadzone=new NumericUpDown(),curve=new NumericUpDown();
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();HardwareProfile current;
        public HardwareUsbEditor(BridgeLink link){bridge=link;current=bridge.ReadHardwareConfig();Text="MinimalJoystick · Physical USB calibration";ClientSize=new Size(680,650);MinimumSize=Size;BackColor=Color.FromArgb(24,32,44);ForeColor=Color.White;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;
            LabelAt("Physical USB controller",22,15,620,28);
            ports.SetBounds(22,50,160,30);ports.DropDownStyle=ComboBoxStyle.DropDownList;Controls.Add(ports);RefreshPorts();
            Add("Refresh ports",195,48,130,delegate{RefreshPorts();});
            Add("Connect diagnostics",338,48,155,delegate{Guard(delegate{RequireBridge();var p=Read(false);p.Enabled=true;p.Save(HardwareProfile.DefaultPath);bridge.ConfigureHardware(p);current=p;});});
            Add("Disconnect USB",505,48,150,delegate{Guard(delegate{var p=bridge.ReadHardwareConfig();p.Enabled=false;p.Save(HardwareProfile.DefaultPath);bridge.ConfigureHardware(p);});});
            live.SetBounds(22,94,630,95);Controls.Add(live);
            string[] names={"Rudder left stop","Rudder resting center","Rudder right stop","Throttle idle","Throttle full"};
            for(int i=0;i<5;i++){int n=i;LabelAt(names[i],22,200+i*42,285,30);fields[i]=new NumericUpDown{Minimum=0,Maximum=4095,Left=315,Top=198+i*42,Width=130};Controls.Add(fields[i]);Add("Capture",470,196+i*42,180,delegate{var s=bridge.ReadHardware();if(!s.Fresh||!s.Frame.ControlsValid){MessageBox.Show(this,"Wait for fresh, valid angle and ADC readings before capture.");return;}fields[n].Value=n<3?s.Frame.Angle:s.Frame.Adc;captured[n]=true;});}
            invertR.Text="Invert rudder";invertR.SetBounds(22,418,220,30);Controls.Add(invertR);invertT.Text="Invert throttle";invertT.SetBounds(315,418,220,30);Controls.Add(invertT);
            LabelAt("Dead zone (%)",22,460,160,25);deadzone.SetBounds(180,456,90,30);deadzone.Maximum=30;deadzone.DecimalPlaces=2;Controls.Add(deadzone);
            LabelAt("Response exponent",315,460,170,25);curve.SetBounds(510,456,100,30);curve.Minimum=0.2m;curve.Maximum=3;curve.DecimalPlaces=2;curve.Increment=0.05m;Controls.Add(curve);
            Add("Apply calibration",22,500,195,delegate{Guard(delegate{RequireBridge();for(int i=0;i<5;i++)if(!captured[i])throw new InvalidDataException("Capture all five physical positions before enabling simulator input.");var p=Read(true);p.Enabled=true;p.Save(HardwareProfile.DefaultPath);bridge.ConfigureHardware(p);current=p;MessageBox.Show(this,"Hardware calibration saved. Valid USB samples now feed the existing MinimalJoystick controller.");});});
            Add("Import JSON",238,500,195,delegate{using(var d=new OpenFileDialog{Filter="Hardware calibration|*.json"})if(d.ShowDialog(this)==DialogResult.OK)Guard(delegate{current=HardwareProfile.Load(d.FileName);Fill();});});
            Add("Export JSON",454,500,195,delegate{using(var d=new SaveFileDialog{Filter="Hardware calibration|*.json",FileName="MinimalJoystick-hardware-usb.json"})if(d.ShowDialog(this)==DialogResult.OK)Guard(delegate{Read(current.Calibrated).Save(d.FileName);});});
            LabelAt("Capture center with the rudder released. Then capture both stops and throttle endpoints.\nConnect diagnostics pauses gameplay input until calibration is applied.\nNo automatic port scanning; choose the board's COM port. BLE is not implemented.",22,554,630,82);
            Fill();timer.Interval=100;timer.Tick+=delegate{var s=bridge.ReadHardware();live.Text=s.Status+Environment.NewLine+(s.Frame==null?"No USB samples":String.Format("Raw angle {0} · ADC {1} · Buttons {2} · {3}\nMagnet 0x{4:X2} · AGC {5} · Battery {6}",s.Frame.Angle,s.Frame.Adc,s.Frame.Buttons,s.Fresh?"fresh":"stale",s.Frame.Magnet,s.Frame.Agc,(s.Frame.Flags&4)!=0?String.Format("{0} mV / {1:F1}%",s.Frame.Millivolts,s.Frame.Soc256/256.0):"unavailable"));};timer.Start();FormClosed+=delegate{timer.Dispose();};
        }
        void LabelAt(string text,int x,int y,int w,int h){Controls.Add(new Label{Text=text,Left=x,Top=y,Width=w,Height=h});}
        void Add(string text,int x,int y,int w,EventHandler handler){var b=new Button{Text=text,Left=x,Top=y,Width=w,Height=34,ForeColor=Color.FromArgb(20,30,40),BackColor=Color.FromArgb(225,232,238),UseVisualStyleBackColor=false};b.Click+=handler;Controls.Add(b);}
        void RequireBridge(){var state=bridge.Read();if(!BridgeLink.Alive(state)||state.BridgeVersion<3)throw new InvalidDataException("Restart the current USB-capable bridge from the main window before connecting hardware.");}
        void Guard(Action action){try{action();}catch(Exception e){MessageBox.Show(this,e.Message);}}
        void RefreshPorts(){string selected=ports.SelectedItem as string;ports.Items.Clear();string[] list=SerialPort.GetPortNames();Array.Sort(list);ports.Items.AddRange(list);string target=selected??current.Port;if(!string.IsNullOrEmpty(target)&&!ports.Items.Contains(target))ports.Items.Add(target);if(ports.Items.Contains(target))ports.SelectedItem=target;}
        void Fill(){int[] values={current.Left,current.Center,current.Right,current.ThrottleIdle,current.ThrottleFull};for(int i=0;i<5;i++){fields[i].Value=values[i];captured[i]=current.Calibrated;}invertR.Checked=current.InvertRudder;invertT.Checked=current.InvertThrottle;deadzone.Value=(decimal)(current.Deadzone*100);curve.Value=(decimal)current.Exponent;if(!string.IsNullOrEmpty(current.Port)){if(!ports.Items.Contains(current.Port))ports.Items.Add(current.Port);ports.SelectedItem=current.Port;}}
        HardwareProfile Read(bool calibrated){var p=new HardwareProfile{Port=ports.SelectedItem as string??"",Calibrated=calibrated,Left=(int)fields[0].Value,Center=(int)fields[1].Value,Right=(int)fields[2].Value,ThrottleIdle=(int)fields[3].Value,ThrottleFull=(int)fields[4].Value,Deadzone=(double)deadzone.Value/100,Exponent=(double)curve.Value,InvertRudder=invertR.Checked,InvertThrottle=invertT.Checked};p.Validate();return p;}
    }
}
