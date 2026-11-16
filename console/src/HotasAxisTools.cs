using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// What a calibration run found out about one axis.
sealed class CalibrationResult {
 public int Center,Min,Max,Noise;
 public double RecommendedDeadzone;
 public bool Usable;
 public List<string> Notes=new List<string>();
}

// Measures an axis in two steps: let it rest (where is the centre, how much does it wobble) and sweep it (how far does it
// really travel). A lever has no rest step. Pure logic, fed one raw reading at a time.
sealed class AxisCalibrator {
 readonly List<int> rest=new List<int>();
 int sweepMin=Int32.MaxValue,sweepMax=Int32.MinValue;
 readonly bool lever;

 public AxisCalibrator(bool lever){this.lever=lever;}
 public int RestSamples{get{return rest.Count;}}
 public void FeedRest(int raw){rest.Add(raw);}
 public void FeedSweep(int raw){if(raw<sweepMin)sweepMin=raw;if(raw>sweepMax)sweepMax=raw;}

 public CalibrationResult Result() {
  var r=new CalibrationResult();
  if(sweepMax<sweepMin){r.Notes.Add("No movement was recorded.");return r;}
  int min=sweepMin,max=sweepMax;
  if(lever){
   r.Min=min;r.Max=max;r.Center=(min+max)/2;
   r.Usable=max-min>=8192;
   if(!r.Usable)r.Notes.Add("The lever barely moved. Move it from one end to the other.");
   r.RecommendedDeadzone=0.02;
   return r;
  }
  if(rest.Count<5){r.Notes.Add("The control was not held still long enough.");return r;}
  var sorted=rest.OrderBy(v=>v).ToList();
  r.Center=sorted[sorted.Count/2];
  r.Noise=sorted[sorted.Count-1]-sorted[0];
  min=Math.Min(min,sorted[0]);max=Math.Max(max,sorted[sorted.Count-1]);
  r.Min=min;r.Max=max;
  int low=r.Center-min,high=max-r.Center;
  r.Usable=low>=4096&&high>=4096&&max-min>=8192;
  if(!r.Usable){r.Notes.Add("The control did not travel far enough on both sides of its rest position. Sweep it fully in every direction.");return r;}
  int side=Math.Min(low,high);
  r.RecommendedDeadzone=Math.Max(0.02,Math.Min(0.25,(r.Noise/2.0*1.5+200)/side));
  r.RecommendedDeadzone=Math.Round(r.RecommendedDeadzone*100)/100;
  if(Math.Min(low,high)<19660)r.Notes.Add("One side only reaches "+(int)Math.Round(Math.Min(low,high)*100.0/32768)+"% of the normal range. Calibration will stretch it to full.");
  if(Math.Abs(r.Center)>6000)r.Notes.Add("This axis rests well away from the middle. Calibration will recentre it.");
  if(r.Noise>1500)r.Notes.Add("The reading wobbles by "+(int)Math.Round(r.Noise*100.0/65535)+"% at rest. A larger dead zone is recommended.");
  return r;
 }

 internal static bool SelfTest() {
  // A healthy stick: rests near the middle with a little noise and travels the full range.
  var c=new AxisCalibrator(false);
  var rng=new Random(7);
  for(int i=0;i<60;i++)c.FeedRest(200+rng.Next(-150,150));
  for(int v=-32768;v<=32767;v+=1000)c.FeedSweep(v);
  var r=c.Result();
  if(!r.Usable||Math.Abs(r.Center-200)>200||r.Min>-32000||r.Max<32000||r.Noise>300||r.RecommendedDeadzone<0.02||r.RecommendedDeadzone>0.04)return false;
  // A worn stick: off-centre, short on one side, jittery.
  c=new AxisCalibrator(false);
  for(int i=0;i<60;i++)c.FeedRest(3000+rng.Next(-1500,1500));
  for(int v=-20000;v<=28000;v+=500)c.FeedSweep(v);
  r=c.Result();
  if(!r.Usable||Math.Abs(r.Center-3000)>1500||r.Min>-19500||r.Max<27500||r.RecommendedDeadzone<0.08||r.Notes.Count<1)return false;
  // Resting but never swept: not usable, with a reason.
  c=new AxisCalibrator(false);
  for(int i=0;i<60;i++)c.FeedRest(0);
  var none=c.Result();
  if(none.Usable||none.Notes.Count==0)return false;
  c.FeedSweep(100);c.FeedSweep(-100);
  if(c.Result().Usable)return false;                   // a twitch is not a sweep
  // Too little rest.
  c=new AxisCalibrator(false);
  c.FeedRest(0);
  for(int v=-30000;v<=30000;v+=1000)c.FeedSweep(v);
  if(c.Result().Usable)return false;
  // A lever needs only a sweep.
  c=new AxisCalibrator(true);
  for(int v=-12000;v<=20000;v+=500)c.FeedSweep(v);
  r=c.Result();
  if(!r.Usable||r.Min!=-12000||r.Max!=20000||r.Center<3900||r.Center>4100)return false;
  c=new AxisCalibrator(true);
  c.FeedSweep(10);c.FeedSweep(40);
  return !c.Result().Usable;
 }
}

// Live picture of one axis: where the reading is against its calibrated range, and the response curve it produces.
sealed class AxisPlot : Control {
 public HotasAxisSettings Settings=new HotasAxisSettings();
 public bool Lever;
 public string Caption;      // overrides the top line, for the game's own handling
 public int Raw;
 public bool Online;

 public AxisPlot() {
  SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  BackColor=Color.FromArgb(8,10,7);
 }

 static readonly Color Ink=Color.FromArgb(247,220,82),Dim=Color.FromArgb(150,134,60),Frame=Color.FromArgb(149,125,19),Green=Color.FromArgb(102,231,82);

 double Output(int raw) {return Lever?HotasAxisMath.Throttle(raw,Settings):HotasAxisMath.Bipolar(raw,Settings);}

 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e);
  var g=e.Graphics;
  g.SmoothingMode=SmoothingMode.AntiAlias;
  g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
  using(var frame=new Pen(Frame,1.3f))g.DrawRectangle(frame,0,0,Width-1,Height-1);
  using(var small=new Font("Consolas",8.5f,FontStyle.Bold))using(var ink=new SolidBrush(Ink))using(var dim=new SolidBrush(Dim)){
   // top strip: raw reading against the calibrated range
   int pad=14;float stripY=26,stripH=22,stripW=Width-pad*2;
   int min=Settings.Calibrated?Settings.CalMin:-32768,max=Settings.Calibrated?Settings.CalMax:32767;
   int center=Settings.Calibrated?Settings.CalCenter:(Lever?(min+max)/2:0);
   Func<int,float> at=delegate(int raw){return pad+stripW*((raw+32768)/65535f);};
   g.DrawString(Caption??(Settings.Calibrated?"Reading against the calibrated range":"Reading against the full range (not calibrated)"),small,dim,pad,6);
   using(var back=new SolidBrush(Color.FromArgb(14,14,5)))g.FillRectangle(back,pad,stripY,stripW,stripH);
   float x0=at(min),x1=at(max);
   using(var range=new SolidBrush(Color.FromArgb(50,Frame)))g.FillRectangle(range,x0,stripY,x1-x0,stripH);
   if(!Lever){
    float dz=(float)Settings.Deadzone;
    float lowEdge=at(center-(int)((center-min)*dz)),highEdge=at(center+(int)((max-center)*dz));
    using(var zone=new SolidBrush(Color.FromArgb(90,226,72,45)))g.FillRectangle(zone,lowEdge,stripY,highEdge-lowEdge,stripH);
   }
   using(var mid=new Pen(Dim))g.DrawLine(mid,at(center),stripY-3,at(center),stripY+stripH+3);
   using(var edge=new Pen(Ink,2f)){g.DrawLine(edge,x0,stripY-3,x0,stripY+stripH+3);g.DrawLine(edge,x1,stripY-3,x1,stripY+stripH+3);}
   if(Online)using(var marker=new SolidBrush(Green))g.FillRectangle(marker,at(Math.Max(-32768,Math.Min(32767,Raw)))-2,stripY-5,4,stripH+10);
   g.DrawString("-32768",small,dim,pad,stripY+stripH+6);
   g.DrawString("32767",small,dim,pad+stripW-44,stripY+stripH+6);
   if(Online)g.DrawString("raw "+Raw,small,ink,pad+stripW/2-30,stripY+stripH+6);

   // response graph
   float gy=stripY+stripH+34,gh=Height-gy-26,gw=Math.Min(stripW,gh*1.6f),gx=pad+(stripW-gw)/2;
   if(gh>40){
    using(var grid=new Pen(Color.FromArgb(60,Frame))){
     g.DrawRectangle(grid,gx,gy,gw,gh);
     if(!Lever)g.DrawLine(grid,gx+gw/2,gy,gx+gw/2,gy+gh);
     g.DrawLine(grid,gx,Lever?gy+gh:gy+gh/2,gx+gw,Lever?gy+gh:gy+gh/2);
    }
    var curve=Settings.Copy();curve.CalMin=curve.CalCenter=curve.CalMax=0;curve.Invert=false;
    var points=new List<PointF>();
    for(int i=0;i<=120;i++){
     double x=-1+2.0*i/120;
     double y=Lever?HotasAxisMath.Unipolar((int)Math.Round((x+1)/2*65535-32768),curve):HotasAxisMath.Bipolar((int)Math.Round(x*32767),curve);
     float px=gx+(float)((x+1)/2*gw),py=Lever?gy+gh-(float)(y*gh):gy+gh/2-(float)(y*gh/2);
     points.Add(new PointF(px,py));
    }
    using(var line=new Pen(Ink,2f))g.DrawLines(line,points.ToArray());
    g.DrawString(Lever?"in: lever position (low to high)   out: 0 to 100%":"in: stick travel (-1 to 1)   out: result",small,dim,gx,gy+gh+4);
    if(Online){
     // the live point: where the current reading lands on the curve (before invert, which only mirrors it)
     var live=Settings.Copy();live.CalMin=live.CalMax=live.CalCenter=0;live.Invert=false;
     int min2=Settings.Calibrated?Settings.CalMin:-32768,max2=Settings.Calibrated?Settings.CalMax:32767,c2=Settings.Calibrated?Settings.CalCenter:0;
     double nx;
     if(Lever){
      // The throttle runs the other way round by default (forward is full), so the point sits where the curve is read from.
      double u=max2==min2?0:Math.Max(0,Math.Min(1,(Raw-min2)/(double)(max2-min2)));
      bool flipped=!Settings.Invert;
      nx=(flipped?1-u:u)*2-1;
     }else{
      nx=Raw>=c2?(max2==c2?0:(Raw-c2)/(double)(max2-c2)):(c2==min2?0:(Raw-c2)/(double)(c2-min2));
      nx=Math.Max(-1,Math.Min(1,nx));
      if(Settings.Invert)nx=-nx;
     }
     double outv=Output(Raw);
     float px=gx+(float)((nx+1)/2*gw),py=Lever?gy+gh-(float)(outv*gh):gy+gh/2-(float)(outv*gh/2);
     using(var dot=new SolidBrush(Green))g.FillEllipse(dot,px-5,py-5,10,10);
     g.DrawString("out "+(Lever?(int)Math.Round(outv*100)+"%":(outv>=0?"+":"")+(int)Math.Round(outv*100)+"%"),small,ink,gx+gw-70,gy+4);
    }
   }
  }
 }
}

partial class ControllerSetup {
 static readonly string[] AxisPlotOrder={"Pitch","Yaw","Roll","Absolute throttle","Relative throttle"};

 // The flight axes dialog: how they are handled (the game's own, or Flight Control's precision mode), and for each axis
 // its dead zone, saturation, curve and calibration, with a live picture.
 // Picture of the dialog for documentation: --render-axes <png> [game | lever]
 static int RenderAxesCli(string[] args) {
  int at=Array.IndexOf(args,"--render-axes");
  if(at<0||at+1>=args.Length)return 1;
  using(var form=new ControllerSetup()){
   form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2000,-2000);form.ShowInTaskbar=false;form.Show();
   form.HotasDemo();
   form.hotasTimer.Stop();
   var profile=form.hotasProfile;
   bool game=args.Length>at+2&&args[at+2]=="game";
   bool lever=args.Length>at+2&&args[at+2]=="lever";
   profile.AxisMode=game?"Game":"Precision";
   profile.FlightAxes["Pitch"].Shape=new HotasAxisSettings{Deadzone=0.12,Saturation=0.9,Curve=0.3,CalMin=-30000,CalCenter=500,CalMax=31000,Noise=420};
   profile.FlightAxes["Pitch"].Invert=true;
   profile.FlightAxes["Absolute throttle"].Shape=new HotasAxisSettings{Deadzone=0.02,Curve=0,CalMin=-30000,CalCenter=0,CalMax=29000};
   form.demoValues["0:Axis1"]=-14000;
   form.demoValues["0:Axis3"]=-9000;
   using(var dialog=new AxisDialog(form)){
    dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-2000,-2000);
    dialog.Show();
    dialog.axes.SelectedItem=lever?"Absolute throttle":"Pitch";
    for(int i=0;i<6;i++){dialog.Tick();Application.DoEvents();System.Threading.Thread.Sleep(30);}
    using(var bitmap=new Bitmap(dialog.ClientSize.Width,dialog.ClientSize.Height)){
     dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));
     bitmap.Save(args[at+1],System.Drawing.Imaging.ImageFormat.Png);
    }
    dialog.Close();
   }
   form.Close();
  }
  return 0;
 }

 internal void ShowAxisSettings() {
  if(hotasProfile==null||hotasProfile.FlightAxes==null||hotasProfile.FlightAxes.Count==0)
   throw new InvalidOperationException("Assign at least one flight axis (Pitch, Yaw, Roll or a throttle) first.");
  using(var dialog=new AxisDialog(this))dialog.ShowDialog(this);
 }

 sealed class AxisDialog : Form {
  readonly ControllerSetup host;
  internal readonly ListBox axes=new ListBox{Dock=DockStyle.Fill,BackColor=Color.Black,ForeColor=Color.FromArgb(247,220,82),Font=new Font("Segoe UI",10.5f),BorderStyle=BorderStyle.FixedSingle};
  readonly AxisPlot plot=new AxisPlot{Dock=DockStyle.Fill};
  readonly TrackBar deadzone=new TrackBar{Minimum=0,Maximum=40,TickFrequency=5,Dock=DockStyle.Fill,BackColor=Color.Black};
  readonly TrackBar saturation=new TrackBar{Minimum=50,Maximum=100,TickFrequency=10,Dock=DockStyle.Fill,BackColor=Color.Black};
  readonly TrackBar curve=new TrackBar{Minimum=-100,Maximum=100,TickFrequency=25,Dock=DockStyle.Fill,BackColor=Color.Black};
  readonly Label deadzoneValue,saturationValue,curveValue,status,modeNote,calNote;
  readonly CheckBox invert=new CheckBox{Text="Invert this axis",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(247,220,82),Font=new Font("Segoe UI",10.5f)};
  readonly Button gameMode,precisionMode,calibrate,resetCalibration,useRecommended,resetAxis;
  readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=40};
  bool loading;
  readonly List<Control> sliderRows=new List<Control>();
  // calibration run
  AxisCalibrator calibrator;
  string calibrating;
  int calibrationTicks;
  CalibrationResult lastResult;

  public AxisDialog(ControllerSetup host) {
   this.host=host;
   Text="Flight Control | Flight axes";
   StartPosition=FormStartPosition.CenterParent;
   FormBorderStyle=FormBorderStyle.FixedDialog;
   MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
   ClientSize=new Size(940,724);
   BackColor=Color.Black;ForeColor=Color.FromArgb(247,220,82);Font=new Font("Segoe UI",10);
   var root=Grid(1,6);root.Padding=new Padding(16);Rows(root,30,44,56,-1,0,44);
   root.Controls.Add(LabelText("FLIGHT AXES",11,Color.FromArgb(249,223,54)),0,0);
   var modes=Grid(2,1);modes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));modes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   gameMode=host.ActionButton("Game (the engine handles the axes)",delegate{SetMode("Game");});
   precisionMode=host.ActionButton("Precision (experimental)",delegate{SetMode("Precision");});
   modes.Controls.Add(gameMode,0,0);modes.Controls.Add(precisionMode,1,0);
   root.Controls.Add(modes,0,1);
   modeNote=LabelText("",9.5f,Color.FromArgb(179,157,77));modeNote.AutoEllipsis=false;
   root.Controls.Add(modeNote,0,2);

   var body=Grid(2,1);body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,270));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   var left=Grid(1,2);Rows(left,26,-1);left.Margin=new Padding(0,0,12,0);
   left.Controls.Add(LabelText("AXIS",9.5f,Color.FromArgb(249,223,54)),0,0);
   left.Controls.Add(axes,0,1);
   body.Controls.Add(left,0,0);
   var right=Grid(1,8);Rows(right,24,220,40,40,40,40,30,44);
   status=LabelText("",9.5f,Color.FromArgb(105,224,91));status.AutoEllipsis=false;
   right.Controls.Add(status,0,0);
   right.Controls.Add(plot,0,1);
   calNote=LabelText("",9.5f,Color.FromArgb(239,101,66));calNote.AutoEllipsis=false;
   right.Controls.Add(calNote,0,2);
   deadzoneValue=new Label();saturationValue=new Label();curveValue=new Label();
   sliderRows.Add(SliderRow("Dead zone",deadzone,deadzoneValue));
   sliderRows.Add(SliderRow("Full deflection at",saturation,saturationValue));
   sliderRows.Add(SliderRow("Curve",curve,curveValue));
   for(int i=0;i<3;i++)right.Controls.Add(sliderRows[i],0,3+i);
   right.Controls.Add(invert,0,6);
   var buttons=Grid(4,1);for(int i=0;i<4;i++)buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
   calibrate=host.ActionButton("Calibrate…",delegate{BeginCalibration();});
   resetCalibration=host.ActionButton("Reset calibration",delegate{ResetCalibration();});
   useRecommended=host.ActionButton("Use recommended dead zone",delegate{UseRecommended();});
   resetAxis=host.ActionButton("Reset this axis",delegate{ResetAxis();});
   buttons.Controls.Add(calibrate,0,0);buttons.Controls.Add(resetCalibration,1,0);buttons.Controls.Add(useRecommended,2,0);buttons.Controls.Add(resetAxis,3,0);
   right.Controls.Add(buttons,0,7);
   body.Controls.Add(right,1,0);
   root.Controls.Add(body,0,3);
   var close=host.ActionButton("Close",delegate{Close();});
   var bottom=Grid(2,1);bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));
   bottom.Controls.Add(LabelText("Changes save automatically. Apply to pilot, with the game closed, to use them.",9,Color.FromArgb(179,157,77)),0,0);
   bottom.Controls.Add(close,1,0);
   root.Controls.Add(bottom,0,5);
   Controls.Add(root);

   foreach(string name in AxisPlotOrder)if(host.hotasProfile.FlightAxes.ContainsKey(name))axes.Items.Add(name);
   axes.SelectedIndexChanged+=delegate{LoadAxis();};
   deadzone.ValueChanged+=delegate{Changed();};saturation.ValueChanged+=delegate{Changed();};curve.ValueChanged+=delegate{Changed();};
   invert.CheckedChanged+=delegate{Changed();};
   timer.Tick+=delegate{Tick();};
   Shown+=delegate{timer.Start();};
   FormClosed+=delegate{timer.Stop();timer.Dispose();};
   ShowMode();
   if(axes.Items.Count>0)axes.SelectedIndex=0;
  }

  Control SliderRow(string title,TrackBar bar,Label value) {
   var row=Grid(3,1);row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,80));
   row.Controls.Add(LabelText(title,10,Color.FromArgb(247,220,82)),0,0);
   row.Controls.Add(bar,1,0);
   value.Dock=DockStyle.Fill;value.TextAlign=ContentAlignment.MiddleLeft;value.Font=new Font("Consolas",10.5f,FontStyle.Bold);value.ForeColor=Color.FromArgb(105,224,91);
   row.Controls.Add(value,2,0);
   return row;
  }

  bool Precision{get{return host.hotasProfile.AxisMode=="Precision";}}
  string Current{get{return axes.SelectedItem as string;}}
  bool IsLever{get{return Current=="Absolute throttle";}}
  FlightAxisBinding Binding{get{return Current==null?null:host.hotasProfile.FlightAxes[Current];}}

  HotasAxisSettings Shape(bool create) {
   var b=Binding;
   if(b==null)return null;
   if(b.Shape==null&&create)b.Shape=new HotasAxisSettings{Deadzone=IsLever?0.02:0.05};
   return b.Shape??new HotasAxisSettings{Deadzone=IsLever?0.02:0.05};
  }

  void SetMode(string mode) {
   host.hotasProfile.AxisMode=mode;
   host.SaveHotasWorkingProfile();
   ShowMode();
   LoadAxis();
  }

  void ShowMode() {
   host.TabLook(gameMode,!Precision);host.TabLook(precisionMode,Precision);
   modeNote.Text=Precision
    ?"Flight Control reads these axes itself and steers the ship, so each axis gets its own dead zone, curve and calibration. The game's own axis bindings are cleared on Apply. Experimental: it needs the Flight Control launcher, and if its data stops the game goes back to its own handling."
    :"The game handles these axes, with one dead zone and one sensitivity for all of them (the Flight response tab). Axis settings below show what each axis does and can be measured, but only Precision applies them per axis.";
   bool on=Precision;
   // In Game mode the per-axis sliders would promise something the game does not do, so they are hidden.
   foreach(var row in sliderRows)row.Visible=on;
   invert.Visible=on;
   resetAxis.Enabled=on;
   useRecommended.Text=on?"Use recommended dead zone":"Use as the game's dead zone";
  }

  void LoadAxis() {
   loading=true;
   var shape=Shape(false);
   if(shape==null){loading=false;return;}
   var b=Binding;
   plot.Lever=IsLever;
   deadzone.Value=Math.Max(0,Math.Min(40,(int)Math.Round(shape.Deadzone*100)));
   saturation.Value=Math.Max(50,Math.Min(100,(int)Math.Round(shape.Saturation*100)));
   curve.Value=Math.Max(-100,Math.Min(100,(int)Math.Round(shape.Curve*100)));
   invert.Checked=b.Invert;
   saturation.Enabled=Precision&&!IsLever;
   loading=false;
   UpdateLabels();
  }

  void UpdateLabels() {
   var shape=Shape(false);
   if(shape==null)return;
   deadzoneValue.Text=deadzone.Value+"%";
   saturationValue.Text=saturation.Value+"%";
   curveValue.Text=(curve.Value>0?"+":"")+curve.Value;
   var b=Binding;
   string device=b.DeviceIndex<host.hotasProfile.Devices.Count?HotasModel.ShortName(host.hotasProfile.Devices[b.DeviceIndex].Name)+HotasModel.TwinSuffix(host.hotasProfile,b.DeviceIndex):"?";
   status.Text=Current+"  |  "+device+" / axis "+(b.InputIndex+1)+"  |  "+(shape.Calibrated?"calibrated "+shape.CalMin+" / "+shape.CalCenter+" / "+shape.CalMax:"not calibrated")+(shape.Noise>0?"  |  noise "+shape.Noise:"");
   if(Precision){
    plot.Settings=shape.Copy();plot.Settings.Invert=b.Invert;
    plot.Caption=null;
   }else{
    // As the game sees it: the one global dead zone, no per-axis calibration or curve.
    plot.Settings=new HotasAxisSettings{Deadzone=(double)host.deadzone.Value/100,Saturation=1,Curve=0,Invert=b.Invert};
    plot.Caption="The game's own handling: one dead zone ("+host.deadzone.Value+"%) for every axis";
   }
   plot.Invalidate();
  }

  void Changed() {
   if(loading||Binding==null)return;
   var shape=Shape(true);
   shape.Deadzone=deadzone.Value/100.0;
   shape.Saturation=saturation.Value/100.0;
   shape.Curve=curve.Value/100.0;
   Binding.Invert=invert.Checked;
   host.SaveHotasWorkingProfile();
   UpdateLabels();
  }

  int ReadRaw(out bool online) {
   online=false;
   var b=Binding;
   if(b==null||b.DeviceIndex>=host.hotasMatches.Count)return 0;
   var live=host.hotasMatches[b.DeviceIndex];
   if(live==null||!live.Connected)return 0;
   var input=live.Inventory.Inputs.Find(i=>i.Kind=="Axis"&&i.Index==b.InputIndex);
   if(input==null)return 0;
   online=true;
   return live.ReadRaw(input);
  }

  internal void Tick() {
   if(host.hotasSession!=null)host.hotasSession.Poll();
   bool online;
   int raw=ReadRaw(out online);
   plot.Raw=raw;plot.Online=online;
   plot.Invalidate();
   if(calibrating==null)return;
   if(!online){calibrating=null;calNote.Text="The controller was disconnected. Calibration stopped.";calibrate.Text="Calibrate…";return;}
   calibrationTicks++;
   if(calibrating=="rest"){
    calibrator.FeedRest(raw);
    calNote.Text="Step 1 of 2: leave the control alone, resting where it rests. "+Math.Max(0,(60-calibrationTicks)*0.04).ToString("F1")+" s";
    if(calibrationTicks>=60){calibrating="sweep";calibrationTicks=0;}
   }else{
    calibrator.FeedSweep(raw);
    calNote.Text="Step "+(IsLever?"1 of 1":"2 of 2")+": move it through its whole range, to every end, a few times. "+Math.Max(0,(150-calibrationTicks)*0.04).ToString("F1")+" s";
    if(calibrationTicks>=150)FinishCalibration();
   }
  }

  void BeginCalibration() {
   if(calibrating!=null){calibrating=null;calNote.Text="Calibration cancelled.";calibrate.Text="Calibrate…";return;}
   bool online;
   ReadRaw(out online);
   if(!online)throw new InvalidOperationException("This axis's controller is not connected. Plug it in and press Scan controllers.");
   calibrator=new AxisCalibrator(IsLever);
   calibrating=IsLever?"sweep":"rest";
   calibrationTicks=0;
   calibrate.Text="Cancel";
  }

  void FinishCalibration() {
   calibrating=null;
   calibrate.Text="Calibrate…";
   var result=calibrator.Result();
   lastResult=result;
   if(!result.Usable){calNote.Text=String.Join(" ",result.Notes);return;}
   var shape=Shape(true);
   shape.CalMin=result.Min;shape.CalCenter=result.Center;shape.CalMax=result.Max;shape.Noise=result.Noise;
   if(!shape.Valid()){shape.CalMin=shape.CalCenter=shape.CalMax=0;calNote.Text="That calibration was not usable. Try again with a wider sweep.";return;}
   host.SaveHotasWorkingProfile();
   LoadAxis();
   calNote.Text="Calibrated. "+(IsLever?"":"Recommended dead zone: "+(int)Math.Round(result.RecommendedDeadzone*100)+"%. ")+String.Join(" ",result.Notes);
  }

  void ResetCalibration() {
   var shape=Shape(true);
   shape.CalMin=shape.CalCenter=shape.CalMax=0;shape.Noise=0;
   host.SaveHotasWorkingProfile();
   lastResult=null;
   LoadAxis();
   calNote.Text="Calibration cleared: the full range is used.";
  }

  void UseRecommended() {
   var shape=Shape(false);
   double recommended=lastResult!=null&&lastResult.Usable?lastResult.RecommendedDeadzone:(shape.Noise>0&&shape.Calibrated?Math.Max(0.02,Math.Min(0.25,Math.Round((shape.Noise/2.0*1.5+200)/Math.Max(1,Math.Min(shape.CalCenter-shape.CalMin,shape.CalMax-shape.CalCenter))*100)/100)):0);
   if(recommended<=0)throw new InvalidOperationException("Calibrate this axis first: that measures how much it wobbles at rest.");
   if(Precision){
    deadzone.Value=Math.Max(0,Math.Min(40,(int)Math.Round(recommended*100)));
    calNote.Text="Dead zone set to "+deadzone.Value+"%.";
   }else{
    // The game has one dead zone for every axis, set on the Flight response tab: use the largest need across the axes.
    int percent=Math.Min((int)host.deadzone.Maximum,(int)Math.Round(recommended*100));
    host.deadzone.Value=Math.Max(host.deadzone.Value,(decimal)percent);
    calNote.Text="The game's dead zone is now "+host.deadzone.Value+"% (Flight response tab). Apply it there with APPLY WITH BACKUP.";
   }
  }

  // Test-only: drives the dialog the way a player would, against the stand-in controllers. Returns null when all is well.
  internal static string SelfCheck(ControllerSetup host) {
   using(var d=new AxisDialog(host)){
    if(d.axes.Items.Count==0)return "FAIL: the axes dialog lists no axes";
    d.axes.SelectedItem="Pitch";
    d.SetMode("Precision");
    if(host.hotasProfile.AxisMode!="Precision")return "FAIL: precision mode was not stored";
    d.deadzone.Value=20;d.curve.Value=40;d.saturation.Value=80;d.invert.Checked=true;
    var b=host.hotasProfile.FlightAxes["Pitch"];
    if(b.Shape==null||Math.Abs(b.Shape.Deadzone-0.2)>1e-9||Math.Abs(b.Shape.Curve-0.4)>1e-9||Math.Abs(b.Shape.Saturation-0.8)>1e-9||!b.Invert)return "FAIL: the sliders did not reach the profile";
    HardwareInspector.Validate(host.hotasProfile);
    // Calibrate: rest with a little wobble around 140, then sweep the full range.
    host.demoValues["0:Axis1"]=140;
    d.BeginCalibration();
    for(int i=0;i<60;i++){host.demoValues["0:Axis1"]=140+(i%5)*30-60;d.Tick();}
    if(d.calibrating!="sweep")return "FAIL: calibration did not move on to the sweep";
    for(int i=0;i<150;i++){host.demoValues["0:Axis1"]=(int)(Math.Sin(i/12.0)*30000);d.Tick();}
    if(d.calibrating!=null)return "FAIL: calibration did not finish";
    if(!b.Shape.Calibrated||b.Shape.CalMin>-29000||b.Shape.CalMax<29000||Math.Abs(b.Shape.CalCenter-140)>100||b.Shape.Noise<100)return "FAIL: calibration values are wrong ("+b.Shape.CalMin+"/"+b.Shape.CalCenter+"/"+b.Shape.CalMax+")";
    HardwareInspector.Validate(host.hotasProfile);
    d.deadzone.Value=0;d.UseRecommended();
    if(d.deadzone.Value<2)return "FAIL: the recommended dead zone was not used";
    d.ResetCalibration();
    if(b.Shape.Calibrated)return "FAIL: reset did not clear the calibration";
    d.ResetAxis();
    if(b.Shape!=null||b.Invert)return "FAIL: reset axis did not restore the defaults";
    // Game mode: the recommendation goes to the game's own dead zone instead.
    d.SetMode("Game");
    bool refused=false;
    try{d.UseRecommended();}catch(InvalidOperationException){refused=true;}
    if(!refused)return "FAIL: a recommendation without a measurement should be refused";
    host.deadzone.Value=0;
    d.BeginCalibration();
    for(int i=0;i<60;i++){host.demoValues["0:Axis1"]=140+(i%5)*40-80;d.Tick();}
    for(int i=0;i<150;i++){host.demoValues["0:Axis1"]=(int)(Math.Sin(i/12.0)*30000);d.Tick();}
    d.UseRecommended();
    if(host.deadzone.Value<2)return "FAIL: the game's dead zone was not set";
    host.demoValues["0:Axis1"]=-24000;
    d.SetMode("Precision");
    host.hotasProfile.AxisMode=null;
    return null;
   }
  }

  void ResetAxis() {
   var b=Binding;
   b.Shape=null;b.Invert=false;
   host.SaveHotasWorkingProfile();
   LoadAxis();
   calNote.Text="This axis is back to its defaults.";
  }
 }
}
