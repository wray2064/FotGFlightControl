using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Windows.Forms;
class GamepadView : Control {
 public event Action<string> Selected; public bool Connected;public float Deadzone;Pad pad;string selection="";float scale=1,ox,oy;
 class Hit {public string Name;public RectangleF Rect;public int Mask;public bool Round;public Hit(string n,float x,float y,float w,float h,int m,bool r){Name=n;Rect=new RectangleF(x,y,w,h);Mask=m;Round=r;}}
 List<Hit> hits=new List<Hit>{new Hit("LT",85,15,95,30,0,false),new Hit("RT",420,15,95,30,0,false),new Hit("LB",70,57,125,30,256,false),new Hit("RB",405,57,125,30,512,false),new Hit("Left stick",105,113,76,76,64,true),new Hit("Right stick",348,214,76,76,128,true),new Hit("D-pad up",218,212,32,30,1,false),new Hit("D-pad down",218,268,32,30,2,false),new Hit("D-pad left",190,240,30,30,4,false),new Hit("D-pad right",248,240,30,30,8,false),new Hit("Y",445,103,38,38,32768,true),new Hit("X",407,141,38,38,16384,true),new Hit("B",483,141,38,38,8192,true),new Hit("A",445,179,38,38,4096,true),new Hit("Back",242,149,52,25,32,false),new Hit("Start",306,149,52,25,16,false)};
 public GamepadView(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Cursor=Cursors.Hand;TabStop=true;AccessibleName="Live gamepad preview";}
 public void UpdatePad(Pad value){ushort pressed=(ushort)(value.buttons & ~pad.buttons);pad=value;foreach(var h in hits)if(h.Mask!=0&&(pressed&h.Mask)!=0){Choose(h.Name);break;}Invalidate();}
 void Choose(string n){selection=n;if(Selected!=null)Selected(n);Invalidate();}
 public static string Description(string name){switch(name){case "Left stick":return "Analog X / Y travel. Click stick: L3.";case "Right stick":return "Analog X / Y travel. Click stick: R3.";case "LT":case "RT":return "Analog trigger: 0 - 255. Squeeze to check its full range.";case "Back":return "View / Select button. Identified here; no binding changes applied.";case "Start":return "Menu / Start button. Tap pauses; hold cycles camera when enabled.";default:return "Digital input. Press to test; green confirms input is received.";}}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);PointF p=new PointF((e.X-ox)/scale,(e.Y-oy)/scale);foreach(var h in hits)if(h.Rect.Contains(p)){Choose(h.Name);break;}}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Left){int i=hits.FindIndex(h=>h.Name==selection);i=(i+(e.KeyCode==Keys.Right?1:hits.Count-1)+hits.Count)%hits.Count;Choose(hits[i].Name);e.Handled=true;}}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;scale=Math.Min(Width/600f,Height/350f);if(scale<=0)return;ox=(Width-600*scale)/2;oy=(Height-350*scale)/2;g.TranslateTransform(ox,oy);g.ScaleTransform(scale,scale);
 using(var grid=new Pen(Color.FromArgb(41,35,6))){for(int x=0;x<600;x+=30)g.DrawLine(grid,x,0,x,350);for(int y=0;y<350;y+=30)g.DrawLine(grid,0,y,600,y);}
 using(var frame=new Pen(Color.FromArgb(149,125,19),1.3f))using(var inner=new Pen(Color.FromArgb(78,66,11),1)){
  g.DrawRectangle(frame,12,8,576,332);g.DrawRectangle(inner,208,104,184,141);
  g.DrawLine(inner,12,8,208,104);g.DrawLine(inner,588,8,392,104);
  g.DrawLine(inner,12,340,208,245);g.DrawLine(inner,588,340,392,245);
  g.DrawLine(inner,300,8,300,104);g.DrawLine(inner,300,245,300,340);
 }
 PointF[] outline={new PointF(103,94),new PointF(202,84),new PointF(247,104),new PointF(353,104),new PointF(398,84),new PointF(497,94),new PointF(537,149),new PointF(564,304),new PointF(531,328),new PointF(485,310),new PointF(428,269),new PointF(172,269),new PointF(115,310),new PointF(69,328),new PointF(36,304),new PointF(63,149)};
 using(var path=new GraphicsPath()){path.AddClosedCurve(outline,.16f);using(var brush=new SolidBrush(Color.FromArgb(12,12,3)))g.FillPath(brush,path);using(var pen=new Pen(Color.FromArgb(236,204,38),2))g.DrawPath(pen,path);}
 using(var font=new Font("Segoe UI",13,FontStyle.Bold))using(var small=new Font("Consolas",11,FontStyle.Bold))using(var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}){
 foreach(var h in hits){bool on=Connected&&((h.Mask!=0&&(pad.buttons&h.Mask)!=0)||(h.Name=="LT"&&pad.lt>30)||(h.Name=="RT"&&pad.rt>30));Color accent=on?Color.FromArgb(102,231,82):selection==h.Name?Color.FromArgb(243,75,48):Color.FromArgb(214,184,37);using(var fill=new SolidBrush(on?Color.FromArgb(16,62,13):Color.FromArgb(7,8,2)))using(var pen=new Pen(accent,selection==h.Name?3:1.5f)){if(h.Round){g.FillEllipse(fill,h.Rect);g.DrawEllipse(pen,h.Rect);}else{g.FillRectangle(fill,h.Rect);g.DrawRectangle(pen,h.Rect.X,h.Rect.Y,h.Rect.Width,h.Rect.Height);}}
 if(h.Name.Contains("stick")){bool left=h.Name=="Left stick";float x=(left?pad.lx:pad.rx)/32768f,y=(left?pad.ly:pad.ry)/32768f;float cx=h.Rect.X+38,cy=h.Rect.Y+38;using(var pen=new Pen(Color.FromArgb(132,69,24),1)){g.DrawEllipse(pen,cx-Deadzone*32,cy-Deadzone*32,Deadzone*64,Deadzone*64);}using(var brush=new SolidBrush(accent))g.FillEllipse(brush,cx+x*24-12,cy-y*24-12,24,24);using(var brush=new SolidBrush(Color.FromArgb(235,208,80)))g.DrawString(left?"L3":"R3",small,brush,new RectangleF(h.Rect.X,h.Rect.Bottom+3,76,20),sf);
 }else{string text=h.Name.Replace("D-pad ","");if(text=="Back")text="VIEW";if(text=="Start")text="MENU";if(text=="up")text="↑";if(text=="down")text="↓";if(text=="left")text="←";if(text=="right")text="→";using(var brush=new SolidBrush(accent))g.DrawString(text,(h.Name=="Back"||h.Name=="Start")?small:font,brush,h.Rect,sf);if(h.Name=="LT"||h.Name=="RT"){float v=(h.Name=="LT"?pad.lt:pad.rt)/255f;using(var brush=new SolidBrush(Color.FromArgb(102,231,82)))g.FillRectangle(brush,h.Rect.X,h.Rect.Bottom+3,h.Rect.Width*v,4);}}
 }
 using(var red=new Pen(Color.FromArgb(226,72,45),1.5f))using(var brush=new SolidBrush(Color.FromArgb(102,231,82))){g.DrawEllipse(red,286,192,28,28);g.DrawLine(red,300,185,300,227);g.DrawLine(red,279,206,321,206);g.DrawString("FOTG",small,brush,new RectangleF(264,225,72,18),sf);}
 }
 }
}

