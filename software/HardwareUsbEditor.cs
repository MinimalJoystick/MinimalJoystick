using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms;
namespace MinimalJoystick {
    public sealed class HardwareUsbEditor : Form {
        readonly BridgeLink bridge;readonly bool preview;readonly ComboBox ports=new ComboBox();readonly Label live=new Label(),step=new Label();readonly ProgressBar progress=new ProgressBar();readonly Button[] captureButtons=new Button[5];
        readonly NumericUpDown[] fields=new NumericUpDown[5];readonly bool[] captured=new bool[5];
        readonly CheckBox invertR=new CheckBox(),invertT=new CheckBox();readonly NumericUpDown deadzone=new NumericUpDown(),curve=new NumericUpDown();
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();HardwareProfile current;
        public HardwareUsbEditor(BridgeLink link,bool designPreview=false){bridge=link;preview=designPreview;current=preview?new HardwareProfile{Port="COM7",Left=3755,Center=0,Right=341,ThrottleIdle=100,ThrottleFull=4000,Deadzone=0.03}:bridge.ReadHardwareConfig();Text="MinimalJoystick · USB setup & calibration";ClientSize=new Size(680,720);AutoScaleMode=AutoScaleMode.Font;MinimumSize=Size;BackColor=DesktopTheme.Background;ForeColor=Color.White;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;
            LabelAt("Physical USB controller",22,15,620,28);
            ports.SetBounds(22,50,160,30);ports.DropDownStyle=ComboBoxStyle.DropDownList;Controls.Add(ports);RefreshPorts();
            Add("Refresh ports",195,48,130,delegate{RefreshPorts();});
            Add("Connect diagnostics",338,48,155,delegate{Guard(delegate{RequireBridge();var p=Read(false);p.Enabled=true;p.Save(HardwareProfile.DefaultPath);bridge.ConfigureHardware(p);current=p;});});
            Add("Disconnect USB",505,48,150,delegate{Guard(delegate{var p=bridge.ReadHardwareConfig();p.Enabled=false;p.Save(HardwareProfile.DefaultPath);bridge.ConfigureHardware(p);});});
            step.SetBounds(22,90,630,30);step.ForeColor=DesktopTheme.Accent;Controls.Add(step);progress.SetBounds(22,122,630,5);progress.Maximum=5;Controls.Add(progress);
            live.SetBounds(22,140,630,70);live.ForeColor=DesktopTheme.Muted;Controls.Add(live);
            string[] names={"Rudder left stop","Rudder resting center","Rudder right stop","Throttle idle","Throttle full"};
            for(int i=0;i<5;i++){int n=i;LabelAt(names[i],22,220+i*42,285,30);fields[i]=new NumericUpDown{Minimum=0,Maximum=4095,Left=315,Top=218+i*42,Width=130};Controls.Add(fields[i]);captureButtons[i]=Add("Capture",470,216+i*42,180,delegate{if(preview)return;var s=bridge.ReadHardware();if(!s.Fresh||!s.Frame.ControlsValid){MessageBox.Show(this,"Wait for fresh, valid angle and ADC readings before capture.");return;}fields[n].Value=n<3?s.Frame.Angle:s.Frame.Adc;captured[n]=true;UpdateProgress();});}
            invertR.Text="Invert rudder";invertR.SetBounds(22,438,220,30);Controls.Add(invertR);invertT.Text="Invert throttle";invertT.SetBounds(315,438,220,30);Controls.Add(invertT);
            LabelAt("Dead zone (%)",22,480,160,25);deadzone.SetBounds(180,476,90,30);deadzone.Maximum=30;deadzone.DecimalPlaces=2;Controls.Add(deadzone);
            LabelAt("Response exponent",315,480,170,25);curve.SetBounds(510,476,100,30);curve.Minimum=0.2m;curve.Maximum=3;curve.DecimalPlaces=2;curve.Increment=0.05m;Controls.Add(curve);
            Add("Apply calibration",22,520,195,delegate{Guard(delegate{RequireBridge();for(int i=0;i<5;i++)if(!captured[i])throw new InvalidDataException("Capture all five physical positions before enabling simulator input.");var p=Read(true);p.Enabled=true;p.Save(HardwareProfile.DefaultPath);bridge.ConfigureHardware(p);current=p;MessageBox.Show(this,"Hardware calibration saved. Valid USB samples now feed the existing MinimalJoystick controller.");});});
            Add("Import JSON",238,520,195,delegate{using(var d=new OpenFileDialog{Filter="Hardware calibration|*.json"})if(d.ShowDialog(this)==DialogResult.OK)Guard(delegate{current=HardwareProfile.Load(d.FileName);Fill();});});
            Add("Export JSON",454,520,195,delegate{using(var d=new SaveFileDialog{Filter="Hardware calibration|*.json",FileName="MinimalJoystick-hardware-usb.json"})if(d.ShowDialog(this)==DialogResult.OK)Guard(delegate{Read(current.Calibrated).Save(d.FileName);});});
            Add("Restore saved backup",22,568,230,delegate{Guard(delegate{if(preview)return;current=HardwareProfile.Load(HardwareProfile.DefaultPath+".bak");Fill();MessageBox.Show(this,"Backup loaded for review. Choose Apply calibration to activate it.");});});
            LabelAt("Release the rudder for center. Capture both stops and both throttle endpoints.\nDiagnostics pauses physical gameplay until calibration is applied.\nBackups and imports must be reviewed before applying. Bluetooth setup is not available.",22,620,630,75);
            Fill();if(preview){captured[0]=captured[1]=true;UpdateProgress();live.Text="DESIGN PREVIEW · sample values only\nRaw angle 0 · ADC 100 · No physical port opened";}else{timer.Interval=100;timer.Tick+=delegate{var s=bridge.ReadHardware();live.Text=s.Status+Environment.NewLine+(s.Frame==null?"No USB samples":String.Format("Raw angle {0} · ADC {1} · Buttons {2} · {3}\nMagnet 0x{4:X2} · AGC {5} · Battery {6}",s.Frame.Angle,s.Frame.Adc,s.Frame.Buttons,s.Fresh?"fresh":"stale",s.Frame.Magnet,s.Frame.Agc,(s.Frame.Flags&4)!=0?String.Format("{0} mV / {1:F1}%",s.Frame.Millivolts,s.Frame.Soc256/256.0):"unavailable"));};timer.Start();}FormClosed+=delegate{timer.Dispose();};
        }
        void LabelAt(string text,int x,int y,int w,int h){Controls.Add(new Label{Text=text,Left=x,Top=y,Width=w,Height=h});}
        Button Add(string text,int x,int y,int w,EventHandler handler){var b=DesktopTheme.Button(text,handler,text=="Apply calibration");b.SetBounds(x,y,w,34);Controls.Add(b);return b;}
        void RequireBridge(){if(preview)throw new InvalidDataException("Design preview does not open hardware.");var state=bridge.Read();if(!BridgeLink.Alive(state)||state.BridgeVersion<3)throw new InvalidDataException("Restart the current USB-capable bridge from the main window before connecting hardware.");}
        void Guard(Action action){try{action();}catch(Exception e){MessageBox.Show(this,e.Message);}}
        void RefreshPorts(){string selected=ports.SelectedItem as string;ports.Items.Clear();string[] list=preview?new[]{"COM7"}:SerialPort.GetPortNames();Array.Sort(list);ports.Items.AddRange(list);string target=selected??current.Port;if(!string.IsNullOrEmpty(target)&&!ports.Items.Contains(target))ports.Items.Add(target);if(ports.Items.Contains(target))ports.SelectedItem=target;}
        void Fill(){int[] values={current.Left,current.Center,current.Right,current.ThrottleIdle,current.ThrottleFull};for(int i=0;i<5;i++){fields[i].Value=values[i];captured[i]=current.Calibrated;}invertR.Checked=current.InvertRudder;invertT.Checked=current.InvertThrottle;deadzone.Value=(decimal)(current.Deadzone*100);curve.Value=(decimal)current.Exponent;if(!string.IsNullOrEmpty(current.Port)){if(!ports.Items.Contains(current.Port))ports.Items.Add(current.Port);ports.SelectedItem=current.Port;}UpdateProgress();}
        void UpdateProgress(){int count=0;for(int i=0;i<5;i++){if(captured[i])count++;captureButtons[i].Text=captured[i]?"Captured · "+fields[i].Value:"Capture";captureButtons[i].ForeColor=captured[i]?DesktopTheme.Accent:Color.White;}progress.Value=count;step.Text=count==5?"5 / 5 positions ready · Review, then apply calibration":"1  Connect    2  Capture positions ("+count+" / 5)    3  Apply";}
        HardwareProfile Read(bool calibrated){var p=new HardwareProfile{Port=ports.SelectedItem as string??"",Calibrated=calibrated,Left=(int)fields[0].Value,Center=(int)fields[1].Value,Right=(int)fields[2].Value,ThrottleIdle=(int)fields[3].Value,ThrottleFull=(int)fields[4].Value,Deadzone=(double)deadzone.Value/100,Exponent=(double)curve.Value,InvertRudder=invertR.Checked,InvertThrottle=invertT.Checked};p.Validate();return p;}
    }
}
