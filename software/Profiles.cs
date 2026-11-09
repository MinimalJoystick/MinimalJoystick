using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using System.Drawing;
namespace MinimalJoystick {
    [DataContract] public sealed class Profile {
        [DataMember] public int Version=1;
        [DataMember] public int Min=0,Center=16384,Max=32767,ThrottleMin=0,ThrottleMax=32767;
        [DataMember] public double Deadzone=0.03,Exponent=1.0;
        [DataMember] public bool InvertRudder,InvertThrottle;
        public void Validate(){if(Version!=1||Min<0||Max>32767||Center-Min<100||Max-Center<100||ThrottleMin<0||ThrottleMax>32767||ThrottleMax-ThrottleMin<100||double.IsNaN(Deadzone)||double.IsInfinity(Deadzone)||Deadzone<0||Deadzone>0.3||double.IsNaN(Exponent)||double.IsInfinity(Exponent)||Exponent<0.2||Exponent>3)throw new InvalidDataException("Invalid calibration: allow at least 100 counts per travel segment; dead zone 0–30%, curve 0.2–3.");}
        public ControlsState Apply(ControlsState raw){Validate();double x=raw.Rudder>=Center?(raw.Rudder-Center)/(double)(Max-Center):(raw.Rudder-Center)/(double)(Center-Min);x=Math.Max(-1,Math.Min(1,x));double a=Math.Abs(x);x=a<=Deadzone?0:Math.Sign(x)*Math.Pow((a-Deadzone)/(1-Deadzone),Exponent);if(InvertRudder)x=-x;double t=Math.Max(0,Math.Min(1,(raw.Throttle-ThrottleMin)/(double)(ThrottleMax-ThrottleMin)));if(InvertThrottle)t=1-t;return new ControlsState((ushort)Math.Round((x+1)*16383.5),(ushort)Math.Round(t*32767),raw.Buttons);}
        public static string DefaultPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MinimalJoystick","profile.json");}}
        public void Save(string path){Validate();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temp=path+".tmp-"+Guid.NewGuid().ToString("N");try{using(var f=File.Create(temp)){new DataContractJsonSerializer(typeof(Profile)).WriteObject(f,this);f.Flush(true);}if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
        public static Profile Load(string path){using(var f=File.OpenRead(path)){Profile p=(Profile)new DataContractJsonSerializer(typeof(Profile)).ReadObject(f);p.Validate();return p;}}
    }
    public sealed class ProfileEditor : Form {
        readonly NumericUpDown[] fields=new NumericUpDown[7];readonly CheckBox invR=new CheckBox(),invT=new CheckBox();
        readonly Func<ControlsState> raw;readonly Action<Profile> apply;Profile profile;
        public ProfileEditor(Profile initial,Func<ControlsState> read,Action<Profile> accept){raw=read;apply=accept;profile=initial;Text="MinimalJoystick · Calibration and profiles";ClientSize=new Size(580,500);BackColor=Color.FromArgb(24,32,44);ForeColor=Color.White;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;
            string[] labels={"Rudder left endpoint","Rudder center","Rudder right endpoint","Throttle minimum","Throttle maximum","Rudder dead zone (%)","Rudder response exponent"};
            for(int i=0;i<7;i++){int n=i;Label l=new Label{Text=labels[i],Left=24,Top=22+i*45,Width=250};Controls.Add(l);NumericUpDown f=new NumericUpDown{Left=280,Top=20+i*45,Width=130,Maximum=i<5?32767:i==5?30:3,DecimalPlaces=i<5?0:2,Minimum=i==6?0.2m:0,Increment=i<5?100:0.05m};fields[i]=f;Controls.Add(f);if(i<5){Button b=new Button{Text="Capture",Left=430,Top=18+i*45,Width=110};b.Click+=delegate{fields[n].Value=n<3?raw().Rudder:raw().Throttle;};Controls.Add(b);}}
            invR.Text="Invert rudder";invR.SetBounds(24,340,220,30);Controls.Add(invR);invT.Text="Invert throttle";invT.SetBounds(280,340,220,30);Controls.Add(invT);
            Add("Apply and save",24,390,delegate{try{Profile p=Read();p.Save(Profile.DefaultPath);apply(p);profile=p;MessageBox.Show(this,"Profile saved and applied to the bridge.");}catch(Exception e){MessageBox.Show(this,e.Message);}});
            Add("Import JSON",205,390,delegate{using(OpenFileDialog d=new OpenFileDialog{Filter="Profile JSON|*.json"})if(d.ShowDialog()==DialogResult.OK){try{profile=Profile.Load(d.FileName);Fill();}catch(Exception e){MessageBox.Show(this,e.Message);}}});
            Add("Export JSON",385,390,delegate{using(SaveFileDialog d=new SaveFileDialog{Filter="Profile JSON|*.json",FileName="MinimalJoystick-profile.json"})if(d.ShowDialog()==DialogResult.OK){try{Read().Save(d.FileName);}catch(Exception e){MessageBox.Show(this,e.Message);}}});
            Label note=new Label{Text="Move the raw controls in the main window, then capture each position.\nSettings affect device response; simulator bindings remain separate.",Left=24,Top=445,Width=535,Height=48};Controls.Add(note);Fill();
        }
        void Add(string text,int x,int y,EventHandler click){Button b=new Button{Text=text,Left=x,Top=y,Width=160,Height=38,ForeColor=Color.Black};b.Click+=click;Controls.Add(b);}
        void Fill(){decimal[] v={profile.Min,profile.Center,profile.Max,profile.ThrottleMin,profile.ThrottleMax,(decimal)(profile.Deadzone*100),(decimal)profile.Exponent};for(int i=0;i<7;i++)fields[i].Value=v[i];invR.Checked=profile.InvertRudder;invT.Checked=profile.InvertThrottle;}
        Profile Read(){Profile p=new Profile{Min=(int)fields[0].Value,Center=(int)fields[1].Value,Max=(int)fields[2].Value,ThrottleMin=(int)fields[3].Value,ThrottleMax=(int)fields[4].Value,Deadzone=(double)fields[5].Value/100,Exponent=(double)fields[6].Value,InvertRudder=invR.Checked,InvertThrottle=invT.Checked};p.Validate();return p;}
    }
}
