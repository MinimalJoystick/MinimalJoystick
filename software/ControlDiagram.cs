using System.Drawing;
using System.Windows.Forms;
namespace MinimalJoystick {
    public sealed class ControlDiagram : Form {
        public ControlDiagram(){Text="MinimalJoystick · Control reference";ClientSize=new Size(640,500);BackColor=Color.FromArgb(17,23,33);ForeColor=Color.White;DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(Font title=new Font("Segoe UI",22))using(Font text=new Font("Segoe UI",11))using(Pen pen=new Pen(Color.FromArgb(78,213,180),3)){
            g.DrawString("MinimalJoystick",title,Brushes.White,30,20);g.DrawRectangle(pen,200,100,190,300);g.DrawEllipse(pen,235,125,120,120);g.DrawLine(pen,295,185,335,155);g.DrawRectangle(pen,230,280,130,12);g.FillRectangle(Brushes.White,285,270,20,32);g.DrawEllipse(pen,275,335,35,35);
            g.DrawString("Rudder · Rz",text,Brushes.White,30,170);g.DrawLine(pen,150,184,230,184);g.DrawString("Throttle · Slider",text,Brushes.White,415,277);g.DrawLine(pen,360,286,405,286);g.DrawString("Gear · Button 1",text,Brushes.White,410,345);g.DrawLine(pen,313,352,400,352);
            g.DrawString("BACK · Brake L / Center / R · Buttons 2 / 3 / 4",text,Brushes.White,100,420);g.DrawString("FRONT · USB-C",text,Brushes.White,230,75);g.DrawString("Control-location schematic · not a mechanical drawing",text,Brushes.Gray,95,463);
        }}
    }
}
