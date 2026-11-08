using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// Original vector artwork inspired by the sparse line displays of classic
// space combat films. No game or film imagery is embedded in the utility.
sealed class TargetingTitle : Control {
    readonly Image logo;
    static readonly Color Gold = Color.FromArgb(247, 219, 49);
    static readonly Color Red = Color.FromArgb(239, 73, 48);
    static readonly Color Green = Color.FromArgb(111, 224, 91);
    public TargetingTitle() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Black;
        string path=ConsoleAsset.File(Path.Combine("Assets","FlightControl.png"));
        if(File.Exists(path)) logo=Image.FromFile(path);
    }
    protected override void Dispose(bool disposing) {if(disposing&&logo!=null)logo.Dispose();base.Dispose(disposing);}
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = Math.Max(.5f, Height / 86f);
        g.ScaleTransform(scale, scale);
        float width = Width / scale;
        using (var dim = new Pen(Color.FromArgb(103, 87, 20), 1))
        using (var bright = new Pen(Gold, 1.6f))
        using (var red = new Pen(Red, 1.5f))
        using (var green = new SolidBrush(Green))
        using (var gold = new SolidBrush(Gold))
        using (var title = new Font("Consolas", 24, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var small = new Font("Consolas", 10, FontStyle.Regular, GraphicsUnit.Pixel)) {
            if(logo!=null){float inset=logo.Width*0.08f;g.DrawImage(logo,new RectangleF(0,1,78,78),new RectangleF(inset,inset,logo.Width-2*inset,logo.Height-2*inset),GraphicsUnit.Pixel);}
            g.DrawString("FLIGHT CONTROL", title, gold, 84, 13);
            g.DrawString("TARGETING COMPUTER  /  HANDHELD CONFIGURATION", small, green, 86, 48);
            float right = width - 11;
            float left = Math.Max(395, right - 195);
            if (right - left > 130) {
                g.DrawRectangle(bright, left, 8, right-left, 54);
                g.DrawRectangle(dim, left+56, 21, right-left-112, 27);
                g.DrawLine(bright, left, 8, left+56, 21); g.DrawLine(bright, right, 8, right-56, 21);
                g.DrawLine(bright, left, 62, left+56, 48); g.DrawLine(bright, right, 62, right-56, 48);
                g.DrawLine(dim, left, 35, right, 35);
                float cx=(left+right)/2;
                g.DrawEllipse(red, cx-9, 26, 18, 18);
                g.DrawLine(red, cx, 19, cx, 51);
                g.DrawString("S H I E L D   5", small, green, left+36, 68);
            }
        }
    }
}

static class TargetingFrame {
    public static void Apply(Control control) {
        control.Paint += delegate(object sender, PaintEventArgs e) {
            var g=e.Graphics;
            var box=control.ClientRectangle;
            box.Inflate(-1,-1);
            if(box.Width<8 || box.Height<8)return;
            using(var border=new Pen(Color.FromArgb(125, 105, 24),1))
            using(var corner=new Pen(Color.FromArgb(246, 211, 49),2)) {
                g.DrawRectangle(border,box);
                int n=Math.Min(17,Math.Min(box.Width,box.Height)/8);
                g.DrawLine(corner,box.Left,box.Top,box.Left+n,box.Top);
                g.DrawLine(corner,box.Left,box.Top,box.Left,box.Top+n);
                g.DrawLine(corner,box.Right,box.Bottom,box.Right-n,box.Bottom);
                g.DrawLine(corner,box.Right,box.Bottom,box.Right,box.Bottom-n);
            }
        };
    }
}
