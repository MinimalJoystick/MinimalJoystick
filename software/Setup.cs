using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
namespace MinimalJoystick {
 public sealed class Setup:Form {
  readonly Label state=new Label();
  public Setup(){Text="MinimalJoystick Setup";ClientSize=new Size(640,360);BackColor=Color.FromArgb(17,23,33);ForeColor=Color.White;Font=new Font("Segoe UI",11);StartPosition=FormStartPosition.CenterScreen;
   Controls.Add(new Label{Text="MinimalJoystick",Font=new Font("Segoe UI",26),Left=30,Top=25,Width=580,Height=50});
   state.SetBounds(32,100,580,70);Controls.Add(state);
   Controls.Add(new Label{Text="Local development edition. Install/repair asks for administrator approval\nand uses this package's local test certificate. Public signing is pending.",Left=32,Top=185,Width=580,Height=60});
   Add("Install / repair",32,260,delegate{Run("Install-LocalPrototype.ps1"," -TrustLocalTestCertificate");});
   Add("Uninstall",235,260,delegate{if(MessageBox.Show(this,"Remove the controller, driver and recorded local test trust?","Uninstall",MessageBoxButtons.YesNo)==DialogResult.Yes)Run("Uninstall-LocalPrototype.ps1","");});
   Add("Refresh status",438,260,delegate{RefreshStatus();});RefreshStatus();
  }
  void Add(string name,int x,int y,EventHandler action){Button b=new Button{Text=name,Left=x,Top=y,Width=170,Height=45,ForeColor=Color.Black};b.Click+=action;Controls.Add(b);}
  void RefreshStatus(){using(HidSink s=new HidSink()){s.Open();state.Text=s.Status+"\nOne persistent controller · USB / Bluetooth input bridge";}}
  void Run(string script,string extra){try{string path=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..",script));if(!File.Exists(path))throw new FileNotFoundException("Keep Setup in software/build inside the supplied package.");var start=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+path+"\""+extra){UseShellExecute=true,Verb="runas"};using(Process p=Process.Start(start)){p.WaitForExit();MessageBox.Show(this,p.ExitCode==0?"Completed. Refresh status to verify.":"Failed ("+p.ExitCode+"). Review installer logs; do not change security settings.");}RefreshStatus();}catch(Exception e){MessageBox.Show(this,e.Message);}}
 }
}
