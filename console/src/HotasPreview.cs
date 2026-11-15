using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// A live, rearrangeable picture of one HOTAS device: bars for axes, circles for buttons, dials for hats.
// Positions are fractions of the canvas so a layout survives resizing; the background picture is the player's own.
sealed class HotasCanvas : Control {
 internal sealed class Widget { public string Kind; public int Index; public RectangleF Rect; }

 public event Action<string,int,int> InputClicked;     // kind, index, hat direction (0 otherwise)
 public event Action LayoutChanged;

 public HardwareDevice Device;
 public DeviceLayout Arrangement;
 public bool Arrange;
 public bool Online;
 public Func<string,int,int> Read=delegate{return 0;};
 // For a hat the key is "Hat:index:direction"; otherwise "Kind:index".
 public Dictionary<string,string> Labels=new Dictionary<string,string>();
 public string HighlightKey;

 Image background;
 string backgroundPath;
 List<Widget> widgets=new List<Widget>();
 Widget dragging;
 PointF grabOffset;

 public HotasCanvas() {
  SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  BackColor=Color.FromArgb(8,10,7);
  MinimumSize=new Size(200,140);
 }

 // Rebuild the widget list from the device and any saved positions.
 public void Rebuild() {
  widgets.Clear();
  if(Device==null){Invalidate();return;}
  LoadBackground();
  var saved=new Dictionary<string,LayoutWidget>();
  if(Arrangement!=null&&Arrangement.Widgets!=null)foreach(var w in Arrangement.Widgets)saved[w.Kind+":"+w.Index]=w;
  var auto=AutoLayout();
  foreach(var input in Device.Inputs){
   string key=input.Kind+":"+input.Index;
   LayoutWidget stored;
   RectangleF rect;
   if(saved.TryGetValue(key,out stored))rect=new RectangleF((float)stored.X,(float)stored.Y,(float)stored.Width,(float)stored.Height);
   else if(!auto.TryGetValue(key,out rect))continue;
   widgets.Add(new Widget{Kind=input.Kind,Index=input.Index,Rect=rect});
  }
  Invalidate();
 }

 void LoadBackground() {
  string path=Arrangement==null?null:Arrangement.BackgroundImage;
  if(path==backgroundPath)return;
  backgroundPath=path;
  if(background!=null){background.Dispose();background=null;}
  try{if(!String.IsNullOrEmpty(path)&&File.Exists(path)){using(var original=Image.FromFile(path))background=new Bitmap(original);}}
  catch(Exception){background=null;}
 }

 // Axes down the left, hats along the top right, buttons in a grid beneath them.
 Dictionary<string,RectangleF> AutoLayout() {
  var result=new Dictionary<string,RectangleF>();
  float cw=Math.Max(1,ClientSize.Width),ch=Math.Max(1,ClientSize.Height);
  var axes=Device.Inputs.Where(i=>i.Kind=="Axis").ToList();
  var hats=Device.Inputs.Where(i=>i.Kind=="Hat").ToList();
  var buttons=Device.Inputs.Where(i=>i.Kind=="Button").ToList();
  // Room under each bar for the name of the action assigned to it.
  float pitch=Math.Min(0.115f,0.86f/Math.Max(axes.Count,1)),barH=pitch*0.6f;
  for(int i=0;i<axes.Count;i++)result["Axis:"+axes[i].Index]=new RectangleF(0.03f,0.06f+i*pitch,0.27f,barH);
  float dial=Math.Min(0.19f*cw,0.30f*ch);
  for(int i=0;i<hats.Count;i++)result["Hat:"+hats[i].Index]=new RectangleF(0.36f+i*(dial/cw+0.025f),0.06f,dial/cw,dial/ch);
  int n=buttons.Count;
  if(n>0){
   float areaX=0.36f,areaY=hats.Count>0?0.06f+dial/ch+0.06f:0.07f,areaW=0.61f,areaH=0.93f-areaY;
   int cols=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(n*(areaW*cw)/(areaH*ch))));
   int rows=(int)Math.Ceiling(n/(double)cols);
   float cell=Math.Min(Math.Min(areaW*cw/cols,areaH*ch/rows),0.2f*Math.Min(cw,ch));   // a lone button should not become a huge disc
   for(int i=0;i<n;i++)
    result["Button:"+buttons[i].Index]=new RectangleF(areaX+(i%cols)*cell/cw,areaY+(i/cols)*cell/ch,cell*0.86f/cw,cell*0.86f/ch);
  }
  return result;
 }

 RectangleF Pixels(Widget w){return new RectangleF(w.Rect.X*ClientSize.Width,w.Rect.Y*ClientSize.Height,w.Rect.Width*ClientSize.Width,w.Rect.Height*ClientSize.Height);}

 protected override void OnResize(EventArgs e){base.OnResize(e);if(Arrangement==null||Arrangement.Widgets==null||Arrangement.Widgets.Count==0)Rebuild();}

 static readonly Color Ink=Color.FromArgb(247,220,82),Dim=Color.FromArgb(179,157,77),Green=Color.FromArgb(102,231,82),Frame=Color.FromArgb(149,125,19);

 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e);
  var g=e.Graphics;
  g.SmoothingMode=SmoothingMode.AntiAlias;
  g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
  if(background!=null){
   var attributes=new ImageAttributes();
   attributes.SetColorMatrix(new ColorMatrix{Matrix33=0.5f});
   float scale=Math.Min((float)ClientSize.Width/background.Width,(float)ClientSize.Height/background.Height);
   int w=(int)(background.Width*scale),h=(int)(background.Height*scale);
   g.DrawImage(background,new Rectangle((ClientSize.Width-w)/2,(ClientSize.Height-h)/2,w,h),0,0,background.Width,background.Height,GraphicsUnit.Pixel,attributes);
   attributes.Dispose();
  }
  using(var frame=new Pen(Frame,1.3f))g.DrawRectangle(frame,0,0,Width-1,Height-1);
  if(Device==null){
   using(var font=new Font("Consolas",11))using(var brush=new SolidBrush(Dim))
    g.DrawString("Your controllers appear here once they are found."+Environment.NewLine+"Plug them in, then press Scan controllers.",font,brush,14,14);
   return;
  }
  using(var small=new Font("Consolas",8.5f,FontStyle.Bold))using(var tiny=new Font("Consolas",7.5f)){
   foreach(var w in widgets){
    var r=Pixels(w);
    int raw=Online?Read(w.Kind,w.Index):(w.Kind=="Axis"?0:0);
    string key=w.Kind+":"+w.Index;
    string label;
    bool assigned=Labels.TryGetValue(key,out label);
    if(w.Kind=="Hat"){
     assigned=false;
     var names=new List<string>();
     foreach(int bit in new[]{1,2,4,8}){string text;if(Labels.TryGetValue(key+":"+bit,out text)){assigned=true;names.Add(text);}}
     label=String.Join(", ",names);
    }
    bool highlight=HighlightKey==key||(w.Kind=="Hat"&&HighlightKey!=null&&HighlightKey.StartsWith(key+":"));
    if(w.Kind=="Axis")PaintAxis(g,r,w.Index,raw,assigned,highlight,label,small,tiny);
    else if(w.Kind=="Hat")PaintHat(g,r,w.Index,raw,assigned,highlight,key,small,tiny);
    else PaintButton(g,r,w.Index,raw!=0,assigned,highlight,label,small,tiny);
   }
  }
  if(Arrange)using(var brush=new SolidBrush(Color.FromArgb(220,236,120)))using(var font=new Font("Consolas",8.5f,FontStyle.Bold))
   g.DrawString("ARRANGE: drag controls into place",font,brush,Width-244,Height-20);
 }

 static string Short(string text,int max){return text.Length<=max?text:text.Substring(0,Math.Max(1,max-1))+"…";}

 void PaintAxis(Graphics g,RectangleF r,int index,int raw,bool assigned,bool highlight,string label,Font small,Font tiny) {
  using(var back=new SolidBrush(Color.FromArgb(14,14,5)))g.FillRectangle(back,r);
  float t=Math.Max(0,Math.Min(1,(raw+32768)/65535f));
  using(var fill=new SolidBrush(Online?Color.FromArgb(200,236,196,32):Color.FromArgb(60,Dim)))g.FillRectangle(fill,r.X,r.Y,r.Width*t,r.Height);
  using(var mid=new Pen(Color.FromArgb(90,Frame)))g.DrawLine(mid,r.X+r.Width/2,r.Y,r.X+r.Width/2,r.Bottom);
  using(var pen=new Pen(highlight?Color.White:assigned?Green:Frame,highlight||assigned?2f:1f))g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);
  using(var brush=new SolidBrush(Color.White))g.DrawString("AXIS "+(index+1),small,brush,r.X+4,r.Y+Math.Max(0,(r.Height-small.Height)/2));
  if(assigned){using(var brush=new SolidBrush(Green))g.DrawString(Short(label,Math.Max(6,(int)(r.Width/7.5f))),tiny,brush,r.X,r.Bottom+1);}
 }

 void PaintButton(Graphics g,RectangleF r,int index,bool down,bool assigned,bool highlight,string label,Font small,Font tiny) {
  float d=Math.Min(r.Width,r.Height);
  var c=new RectangleF(r.X+(r.Width-d)/2,r.Y+(r.Height-d)/2,d,d);
  using(var fill=new SolidBrush(down?Green:Color.FromArgb(110,24,12)))g.FillEllipse(fill,c);
  using(var pen=new Pen(highlight?Color.White:assigned?Color.FromArgb(120,255,240):Color.FromArgb(226,72,45),highlight||assigned?2.4f:1.4f))g.DrawEllipse(pen,c);
  var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
  using(var brush=new SolidBrush(down?Color.Black:Color.White))g.DrawString((index+1).ToString(),d>=22?small:tiny,brush,c,sf);
  if(assigned&&d>=22)using(var brush=new SolidBrush(Color.FromArgb(120,255,240)))g.DrawString(Short(label,Math.Max(4,(int)(r.Width/6.5f))),tiny,brush,new RectangleF(r.X-6,c.Bottom,r.Width+12,14),new StringFormat{Alignment=StringAlignment.Center});
 }

 void PaintHat(Graphics g,RectangleF r,int index,int raw,bool assigned,bool highlight,string key,Font small,Font tiny) {
  float d=Math.Min(r.Width,r.Height);
  var c=new RectangleF(r.X+(r.Width-d)/2,r.Y+(r.Height-d)/2,d,d);
  using(var pen=new Pen(highlight?Color.White:assigned?Green:Color.FromArgb(226,72,45),1.8f))g.DrawEllipse(pen,c);
  float cx=c.X+d/2,cy=c.Y+d/2,reach=d*0.34f;
  foreach(var dir in new[]{new{Bit=1,X=0f,Y=-1f,T="↑"},new{Bit=2,X=1f,Y=0f,T="→"},new{Bit=4,X=0f,Y=1f,T="↓"},new{Bit=8,X=-1f,Y=0f,T="←"}}){
   string text;
   bool mapped=Labels.TryGetValue(key+":"+dir.Bit,out text);
   bool hot=HighlightKey==key+":"+dir.Bit;
   using(var brush=new SolidBrush(hot?Color.White:mapped?Green:Color.FromArgb(120,Dim)))
    g.DrawString(dir.T,small,brush,new RectangleF(cx+dir.X*reach-9,cy+dir.Y*reach-9,18,18),new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center});
  }
  float dx=0,dy=0;
  if((raw&1)!=0)dy-=1;if((raw&4)!=0)dy+=1;if((raw&2)!=0)dx+=1;if((raw&8)!=0)dx-=1;
  using(var brush=new SolidBrush(raw!=0?Green:Color.FromArgb(150,Dim)))g.FillEllipse(brush,cx+dx*d*0.17f-5,cy+dy*d*0.17f-5,10,10);
  using(var brush=new SolidBrush(Color.White))g.DrawString("HAT "+(index+1),tiny,brush,c.X,c.Bottom+1);
 }

 Widget HitTest(Point p) {
  for(int i=widgets.Count-1;i>=0;i--)if(Pixels(widgets[i]).Contains(p))return widgets[i];
  return null;
 }

 protected override void OnMouseDown(MouseEventArgs e) {
  base.OnMouseDown(e);
  Focus();
  var hit=HitTest(e.Location);
  if(hit==null)return;
  var r=Pixels(hit);
  if(Arrange){dragging=hit;grabOffset=new PointF(e.X-r.X,e.Y-r.Y);Cursor=Cursors.SizeAll;return;}
  int direction=0;
  if(hit.Kind=="Hat"){
   float dx=e.X-(r.X+r.Width/2),dy=e.Y-(r.Y+r.Height/2);
   direction=Math.Abs(dx)>Math.Abs(dy)?(dx>0?2:8):(dy>0?4:1);
  }
  if(InputClicked!=null)InputClicked(hit.Kind,hit.Index,direction);
 }

 protected override void OnMouseMove(MouseEventArgs e) {
  base.OnMouseMove(e);
  if(dragging==null){Cursor=Arrange&&HitTest(e.Location)!=null?Cursors.SizeAll:!Arrange&&HitTest(e.Location)!=null?Cursors.Hand:Cursors.Default;return;}
  float x=(e.X-grabOffset.X)/ClientSize.Width,y=(e.Y-grabOffset.Y)/ClientSize.Height;
  var r=dragging.Rect;
  dragging.Rect=new RectangleF(Math.Max(0,Math.Min(1-r.Width,x)),Math.Max(0,Math.Min(1-r.Height,y)),r.Width,r.Height);
  Invalidate();
 }

 protected override void OnMouseUp(MouseEventArgs e) {
  base.OnMouseUp(e);
  if(dragging==null)return;
  var moved=dragging;
  dragging=null;
  Cursor=Cursors.Default;
  Store(moved);
  if(LayoutChanged!=null)LayoutChanged();
 }

 static double Clamp(double v){return Math.Max(0,Math.Min(1,v));}

 // Remember a widget's place in the profile.
 void Store(Widget w) {
  if(Arrangement==null||Arrangement.Widgets==null)return;
  var existing=Arrangement.Widgets.Find(x=>x.Kind==w.Kind&&x.Index==w.Index);
  if(existing==null){
   // First move on this device: keep every other control where it is on screen too.
   foreach(var other in widgets)
    if(!Arrangement.Widgets.Any(x=>x.Kind==other.Kind&&x.Index==other.Index))
     Arrangement.Widgets.Add(new LayoutWidget{Kind=other.Kind,Index=other.Index,X=Clamp(other.Rect.X),Y=Clamp(other.Rect.Y),Width=Math.Max(0.01,Clamp(other.Rect.Width)),Height=Math.Max(0.01,Clamp(other.Rect.Height))});
   return;
  }
  existing.X=Clamp(w.Rect.X);existing.Y=Clamp(w.Rect.Y);existing.Width=Math.Max(0.01,Clamp(w.Rect.Width));existing.Height=Math.Max(0.01,Clamp(w.Rect.Height));
 }

 // Drop every saved position and let the controls flow back into the automatic arrangement.
 public void ResetLayout() {
  if(Arrangement!=null&&Arrangement.Widgets!=null)Arrangement.Widgets.Clear();
  Rebuild();
 }

 protected override void Dispose(bool disposing){if(disposing&&background!=null)background.Dispose();base.Dispose(disposing);}
}
