using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// The HOTAS page of the Console: find the controllers, press a control to assign it, watch it live on a
// preview you can rearrange, then apply. The working profile saves itself beside the pilot's data.
partial class ControllerSetup {
 sealed class HotasRow {
  public HotasModel.Group Group;
  public string Name;
  public string Label;
 }

 // How the gamepad controls Flight Control reads are described to someone holding a stick.
 static readonly Dictionary<string,string> VirtualLabels=new Dictionary<string,string>{
  {"Wheel step clockwise","Wheel: step clockwise"},{"Wheel step counter-clockwise","Wheel: step anticlockwise"},
  {"Wheel select","Wheel: select"},{"Wheel cancel","Wheel: cancel"},
  {"Dpad up","Wheel: D-pad up"},{"Dpad down","Wheel: D-pad down (squad)"},{"Dpad left","Wheel: D-pad left"},{"Dpad right","Wheel: D-pad right (macros)"},
  {"A","Select / confirm (A)"},{"B","Cancel / back (B)"},{"X","X button action"},{"Y","Y button action"},
  {"LB","Left bumper action"},{"RB","Right bumper action"},{"L3","Left stick click (L3)"},{"R3","Right stick click (R3)"},
  {"Start","Menu / pause (Start)"},{"Back","View (Back)"},
  {"Left trigger","Left trigger (secondary fire)"},{"Right trigger","Right trigger (primary fire)"},
  {"Left stick X","Left stick X (roll)"},{"Left stick Y","Left stick Y (throttle)"},
  {"Right stick X","Right stick X (yaw / wheel aim)"},{"Right stick Y","Right stick Y (pitch / wheel aim)"}
 };

 Panel hotasPage;
 Control hotasFooter;
 bool hotasShowSetup;
 string hotasSlotNote;
 int hotasBarWidth;
 ListView hotasList;
 TextBox hotasFilter;
 Label hotasGuide,hotasDetail,hotasIssues;
 HotasCanvas hotasCanvas;
 FlowLayoutPanel hotasDeviceBar;
 Button hotasAssign,hotasArrange;
 CheckBox hotasInvert;
 Timer hotasTimer;
 FlightDevices.HardwareSession hotasSession;
 readonly List<FlightDevices.HardwareSession.Device> hotasMatches=new List<FlightDevices.HardwareSession.Device>();
 HardwareProfile hotasProfile;
 int hotasView;
 string hotasNotice;
 bool hotasLoading;
 HotasRow hotasListening;
 bool hotasProbing;
 InputCapture hotasCapture;
 string hotasStableKey;
 int hotasStable,hotasListenTicks;
 const int ListenLimit=500; // ticks at 40 ms: about 20 seconds

 internal string HotasProfilePath(){return Path.Combine(prefs,"data","players","fotg-hotas-profile.hotas");}

 Control BuildHotasPage() {
  var page=new Panel{Dock=DockStyle.Fill,BackColor=Surface};
  hotasPage=page;
  var shell=Grid(1,2);shell.Padding=new Padding(14,14,14,0);Rows(shell,62,-1);
  page.Controls.Add(shell);

  // Guidance: always says what to do next.
  var top=Grid(2,1);top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));
  hotasGuide=LabelText("",11,Ink);hotasGuide.AutoEllipsis=false;
  top.Controls.Add(hotasGuide,0,0);
  top.Controls.Add(ActionButton("Scan controllers",delegate{HotasScan();}),1,0);
  shell.Controls.Add(top,0,0);

  var main=Grid(2,1);main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
  shell.Controls.Add(main,0,1);

  // Left: what to assign.
  var left=Grid(1,5);Rows(left,28,34,-1,76,46);left.Margin=new Padding(0,0,10,0);
  left.Controls.Add(LabelText("01 / PICK AN ACTION, THEN PRESS A CONTROL",10,Cyan),0,0);
  hotasFilter=new TextBox{Dock=DockStyle.Fill,BackColor=Night,ForeColor=Ink,Font=new Font("Segoe UI",11),BorderStyle=BorderStyle.FixedSingle};
  hotasFilter.TextChanged+=delegate{RebuildHotasList();};hotasFilter.Tag="filter";
  left.Controls.Add(hotasFilter,0,1);
  hotasList=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,HeaderStyle=ColumnHeaderStyle.None,
   BackColor=Night,ForeColor=Ink,Font=new Font("Segoe UI",10),BorderStyle=BorderStyle.FixedSingle,ShowGroups=false};
  hotasList.Columns.Add("Action",250);hotasList.Columns.Add("Input",190);
  hotasList.SelectedIndexChanged+=delegate{ShowHotasSelection();};
  hotasList.DoubleClick+=delegate{ToggleListening();};
  hotasList.Resize+=delegate{FitHotasColumns();};
  left.Controls.Add(hotasList,0,2);
  var detail=Grid(2,1);detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));detail.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));
  hotasDetail=LabelText("",9.5f,Ink);hotasDetail.AutoEllipsis=false;
  detail.Controls.Add(hotasDetail,0,0);
  hotasInvert=new CheckBox{Text="Invert axis",Dock=DockStyle.Fill,ForeColor=Ink,Font=new Font("Segoe UI",10),Enabled=false};
  hotasInvert.CheckedChanged+=delegate{ToggleInvert();};
  detail.Controls.Add(hotasInvert,1,0);
  left.Controls.Add(detail,0,3);
  var leftActions=Grid(2,1);leftActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));leftActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
  hotasAssign=ActionButton("Assign: press a control",delegate{ToggleListening();});
  leftActions.Controls.Add(hotasAssign,0,0);
  leftActions.Controls.Add(ActionButton("Clear",delegate{ClearSelected();}),1,0);
  left.Controls.Add(leftActions,0,4);
  main.Controls.Add(left,0,0);

  // Right: the live picture.
  var right=Grid(1,3);Rows(right,64,-1,46);
  var head=Grid(1,2);Rows(head,28,34);
  head.Controls.Add(LabelText("02 / YOUR CONTROLLERS, LIVE",10,Cyan),0,0);
  hotasDeviceBar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoScroll=false,Margin=Padding.Empty};
  hotasDeviceBar.Resize+=delegate{if(hotasProfile!=null&&hotasDeviceBar.ClientSize.Width>50&&Math.Abs(hotasBarWidth-hotasDeviceBar.ClientSize.Width)>8){hotasBarWidth=hotasDeviceBar.ClientSize.Width;BuildHotasDeviceBar();}};
  head.Controls.Add(hotasDeviceBar,0,1);
  right.Controls.Add(head,0,0);
  hotasCanvas=new HotasCanvas{Dock=DockStyle.Fill};
  hotasCanvas.Read=ReadHotasInput;
  hotasCanvas.InputClicked+=HotasCanvasClicked;
  hotasCanvas.LayoutChanged+=delegate{SaveHotasWorkingProfile();};
  right.Controls.Add(hotasCanvas,0,1);
  var tools=Grid(4,1);for(int i=0;i<4;i++)tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
  hotasArrange=ActionButton("Arrange preview",delegate{hotasCanvas.Arrange=!hotasCanvas.Arrange;hotasArrange.Text=hotasCanvas.Arrange?"Done arranging":"Arrange preview";TabLook(hotasArrange,hotasCanvas.Arrange);hotasCanvas.Invalidate();});
  tools.Controls.Add(hotasArrange,0,0);
  tools.Controls.Add(ActionButton("Background image…",delegate{ChooseHotasBackground();}),1,0);
  tools.Controls.Add(ActionButton("Reset layout",delegate{ResetHotasLayout();}),2,0);
  tools.Controls.Add(ActionButton("Control not listed?",delegate{StartProbe();}),3,0);
  right.Controls.Add(tools,0,2);
  main.Controls.Add(right,1,0);

  // Footer: profile files, backup and apply.
  var footer=Grid(7,1);foreach(int w in new[]{124,124,140,120,140})footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,w));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,176));
  footer.Padding=new Padding(0,14,0,0);footer.Visible=false;
  footer.Controls.Add(ActionButton("Open profile",delegate{OpenHotasProfile();}),0,0);
  footer.Controls.Add(ActionButton("Save profile",delegate{SaveHotasProfileAs();}),1,0);
  footer.Controls.Add(ActionButton("Restore backup",delegate{RestoreHotas();}),2,0);
  footer.Controls.Add(ActionButton("Game / pilot",delegate{hotasShowSetup=!hotasShowSetup;ShowHotasSetup();}),3,0);
  footer.Controls.Add(ActionButton("Gamepad mode",delegate{ReturnToGamepad();hotasSlotNote=HotasSlotNote();RefreshHotasStatus();}),4,0);
  hotasIssues=LabelText("",9,Color.FromArgb(239,101,66));hotasIssues.AutoEllipsis=false;hotasIssues.Padding=new Padding(8,0,8,0);
  footer.Controls.Add(hotasIssues,5,0);
  var apply=ActionButton("APPLY TO PILOT",delegate{ApplyHotasProfile();});
  apply.BackColor=Color.FromArgb(110,88,8);apply.FlatAppearance.BorderColor=Color.FromArgb(255,236,120);apply.Font=new Font("Consolas",11,FontStyle.Bold);
  footer.Controls.Add(apply,6,0);
  // The page's own action bar takes the footer slot of the Console while this page is showing.
  hotasFooter=footer;
  if(cockpitShell!=null)cockpitShell.Controls.Add(footer,0,4);

  hotasTimer=new Timer{Interval=40};
  hotasTimer.Tick+=delegate{try{HotasTick();}catch(Exception e){hotasTimer.Stop();hotasNotice=e.Message;RefreshHotasStatus();}};
  RebuildHotasList();
  return page;
 }

 void FitHotasColumns() {
  if(hotasList==null||hotasList.Columns.Count<2)return;
  int width=Math.Max(200,hotasList.ClientSize.Width-4);
  hotasList.Columns[0].Width=(int)(width*0.42);
  hotasList.Columns[1].Width=width-hotasList.Columns[0].Width;
 }

 // ----- device session and profile -----

 // The game folder and pilot panel is hidden here to give the controls room, unless something still needs choosing.
 void ShowHotasSetup() {
  if(cockpitShell==null||cockpitSetup==null)return;
  cockpitSetup.Visible=hotasShowSetup;
  cockpitShell.RowStyles[1]=new RowStyle(SizeType.Absolute,hotasShowSetup?170:0);
  cockpitSetup.Margin=hotasShowSetup?new Padding(0,0,0,14):Padding.Empty;
  FitCockpit();
  cockpitShell.PerformLayout();
 }

 void EnterHotas() {
  hotasSlotNote=HotasSlotNote();
  hotasShowSetup=String.IsNullOrWhiteSpace(root.Text)||pilots.SelectedItem==null;
  ShowHotasSetup();
  hotasTimer.Start();
  if(hotasSession==null)HotasScan();
 }

 // Closing the window: release the controllers without touching the layout.
 void CloseHotas() {
  if(hotasTimer!=null)hotasTimer.Stop();
  if(hotasSession!=null){hotasSession.Dispose();hotasSession=null;}
 }

 void LeaveHotas() {
  hotasShowSetup=true;
  ShowHotasSetup();
  if(hotasTimer!=null)hotasTimer.Stop();
  StopListening();
  if(hotasSession!=null){hotasSession.Dispose();hotasSession=null;}
 }

 void HotasScan() {
  StopListening();
  hotasNotice=null;
  if(hotasSession!=null){hotasSession.Dispose();hotasSession=null;}
  try{hotasSession=new FlightDevices.HardwareSession(root.Text);}
  catch(Exception e){hotasNotice=e.Message;}
  if(hotasProfile==null)LoadHotasWorkingProfile();
  UseHotasSession();
 }

 // Match what is plugged in to the saved devices, adding any that are new.
 void UseHotasSession() {
  hotasLoading=true;
  try{
   if(hotasSession!=null){
    if(hotasProfile==null)hotasProfile=new HardwareProfile{SchemaVersion=HardwareInspector.CurrentSchema,Backend="SDL3",Devices=new List<HardwareDevice>()};
    MatchHotasDevices();
    foreach(var live in hotasSession.Devices.Where(d=>!hotasMatches.Contains(d)).ToList()){
     hotasProfile.Devices.Add(live.Inventory);
     MatchHotasDevices();
    }
    // A device that now reports more inputs (another mode) keeps what it had and gains the rest.
    for(int i=0;i<hotasMatches.Count;i++){
     if(hotasMatches[i]==null)continue;
     foreach(var input in hotasMatches[i].Inventory.Inputs)
      if(!hotasProfile.Devices[i].Inputs.Any(x=>x.Kind==input.Kind&&x.Index==input.Index))
       hotasProfile.Devices[i].Inputs.Add(new HardwareInput{Kind=input.Kind,Index=input.Index});
    }
   }else if(hotasProfile!=null)MatchHotasDevices();
   if(hotasView>=(hotasProfile==null?0:hotasProfile.Devices.Count))hotasView=0;
  }finally{hotasLoading=false;}
  BuildHotasDeviceBar();
  ShowHotasDevice();
  RebuildHotasList();
  RefreshHotasStatus();
 }

 static bool SameHotasDevice(HardwareDevice saved,HardwareDevice live) {
  return String.Equals(saved.Guid,live.Guid,StringComparison.OrdinalIgnoreCase)&&
   (String.IsNullOrEmpty(saved.Serial)||saved.Serial==live.Serial);
 }

 void MatchHotasDevices() {
  hotasMatches.Clear();
  if(hotasProfile==null)return;
  foreach(var saved in hotasProfile.Devices){
   FlightDevices.HardwareSession.Device match=null;
   if(hotasSession!=null){
    match=hotasSession.Devices.Find(live=>ReferenceEquals(saved,live.Inventory));
    if(match==null){
     var live=hotasSession.Devices.Where(d=>SameHotasDevice(saved,d.Inventory)).ToList();
     var twins=hotasProfile.Devices.Where(o=>SameHotasDevice(saved,o)).ToList();
     // Identical devices without serials are told apart by the order the system lists them.
     if(live.Count>0&&live.Count==twins.Count)match=live[twins.IndexOf(saved)];
    }
   }
   hotasMatches.Add(match!=null&&!hotasMatches.Contains(match)?match:null);
  }
 }

 void LoadHotasWorkingProfile() {
  try{
   string path=HotasProfilePath();
   if(!File.Exists(path))return;
   var loaded=new JavaScriptSerializer().Deserialize<HardwareProfile>(File.ReadAllText(path));
   HardwareInspector.Validate(loaded);
   hotasProfile=loaded;
  }catch(Exception){hotasProfile=null;}
 }

 void SaveHotasWorkingProfile() {
  if(hotasLoading||hotasProfile==null||String.IsNullOrEmpty(prefs))return;
  try{
   hotasProfile.SchemaVersion=HardwareInspector.CurrentSchema;
   Directory.CreateDirectory(Path.GetDirectoryName(HotasProfilePath()));
   File.WriteAllText(HotasProfilePath(),new JavaScriptSerializer().Serialize(hotasProfile));
  }catch(Exception e){hotasNotice="Could not save the working profile: "+e.Message;}
 }

 // A profile from someone else: checked, stripped of anything personal or risky, and your own is kept as a backup first.
 void OpenHotasProfile() {
  using(var dialog=new OpenFileDialog{Filter="Flight Control HOTAS profile (*.fchotas)|*.fchotas|Earlier HOTAS exports (*.json)|*.json",Title="Open a HOTAS profile"}){
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   LoadSharedHotasProfile(dialog.FileName);
  }
 }

 // Reads a profile file from someone else (or an older copy of your own): size, shape and contents are checked,
 // and anything personal or risky is stripped. Nothing changes if it is refused.
 internal HardwareProfile ReadHotasProfile(string file) {
  if(new FileInfo(file).Length>2*1024*1024)throw new InvalidDataException("That file is too large to be a HOTAS profile.");
  HardwareProfile opened;
  try{opened=new JavaScriptSerializer().Deserialize<HardwareProfile>(File.ReadAllText(file));}
  catch(Exception e){throw new InvalidDataException("That is not a valid HOTAS profile: "+e.Message);}
  if(opened!=null&&opened.Format!=null&&opened.Format!=ProfileFormat)throw new InvalidDataException("That file is not a Flight Control HOTAS profile.");
  HardwareInspector.Validate(opened);
  return ShareableCopy(opened);
 }

 internal const string ProfileFormat="FlightControlHotas";

 internal void LoadSharedHotasProfile(string file) {
  var opened=ReadHotasProfile(file);
  StopListening();
  BackUpHotasWorkingProfile();
  hotasProfile=opened;
  hotasNotice=null;
  UseHotasSession();
  int offline=hotasMatches.Count(m=>m==null);
  hotasNotice="Loaded "+Path.GetFileName(file)+". "+(offline==0?"All its controllers are connected.":offline+" of its controllers are not connected: right-click a controller above to use one of yours instead.")+" Your previous profile was kept as a backup.";
  SaveHotasWorkingProfile();
  RefreshHotasStatus();
 }

 // Writes a copy that is safe to share: no device serials, no paths from this PC.
 internal void WriteHotasProfile(string file) {
  if(hotasProfile==null)throw new InvalidOperationException("Scan your controllers first.");
  HardwareInspector.Validate(hotasProfile);
  hotasProfile.SchemaVersion=HardwareInspector.CurrentSchema;
  var copy=ShareableCopy(hotasProfile);
  copy.Format=ProfileFormat;
  File.WriteAllText(file,new JavaScriptSerializer().Serialize(copy),new System.Text.UTF8Encoding(false));
 }

 void BackUpHotasWorkingProfile() {
  try{
   string path=HotasProfilePath();
   if(String.IsNullOrEmpty(prefs)||!File.Exists(path))return;
   File.Copy(path,path+".bak-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),true);
   foreach(string old in Directory.GetFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".bak-*").OrderByDescending(f=>f).Skip(5))File.Delete(old);
  }catch(Exception){}
 }

 void SaveHotasProfileAs() {
  if(hotasProfile==null)throw new InvalidOperationException("Scan your controllers first.");
  using(var dialog=new SaveFileDialog{Filter="Flight Control HOTAS profile (*.fchotas)|*.fchotas",FileName="my-hotas.fchotas",DefaultExt="fchotas",AddExtension=true})
   if(dialog.ShowDialog(this)==DialogResult.OK)WriteHotasProfile(dialog.FileName);
 }

 // Use one of your connected controllers in place of a saved one (a shared profile from another model, or a replaced device).
 void RemapHotasDevice(int index,FlightDevices.HardwareSession.Device live) {
  var saved=hotasProfile.Devices[index];
  saved.Name=live.Inventory.Name;saved.Guid=live.Inventory.Guid;saved.Serial=live.Inventory.Serial;
  foreach(var input in live.Inventory.Inputs)
   if(!saved.Inputs.Any(x=>x.Kind==input.Kind&&x.Index==input.Index))saved.Inputs.Add(new HardwareInput{Kind=input.Kind,Index=input.Index});
  // The entry added automatically when this controller was first seen is now a duplicate: drop it if nothing uses it.
  for(int j=hotasProfile.Devices.Count-1;j>=0;j--){
   if(j==index||!SameHotasDevice(hotasProfile.Devices[j],live.Inventory))continue;
   if(HotasModel.Groups(hotasProfile).Any(p=>p.Value!=null&&p.Value.Values.Any(b=>b.DeviceIndex==j)))continue;
   hotasProfile.Devices.RemoveAt(j);
   foreach(var pair in HotasModel.Groups(hotasProfile))if(pair.Value!=null)foreach(var b in pair.Value.Values)if(b.DeviceIndex>j)b.DeviceIndex--;
   if(hotasProfile.Layouts!=null){
    hotasProfile.Layouts.RemoveAll(l=>l.DeviceIndex==j);
    foreach(var l in hotasProfile.Layouts)if(l.DeviceIndex>j)l.DeviceIndex--;
   }
   if(index>j)index--;
  }
  hotasView=index;
  UseHotasSession();
  SaveHotasWorkingProfile();
 }

 // ----- the list of actions -----

 Dictionary<string,FlightAxisBinding> HotasSet(HotasModel.Group group,bool create) {
  if(hotasProfile==null)return null;
  switch(group){
   case HotasModel.Group.NativeAxis:if(hotasProfile.FlightAxes==null&&create)hotasProfile.FlightAxes=new Dictionary<string,FlightAxisBinding>();return hotasProfile.FlightAxes;
   case HotasModel.Group.NativeAction:if(hotasProfile.FlightButtons==null&&create)hotasProfile.FlightButtons=new Dictionary<string,FlightAxisBinding>();return hotasProfile.FlightButtons;
   default:if(hotasProfile.Virtual==null&&create)hotasProfile.Virtual=new Dictionary<string,FlightAxisBinding>();return hotasProfile.Virtual;
  }
 }

 FlightAxisBinding HotasBinding(HotasRow row) {
  var set=HotasSet(row.Group,false);
  FlightAxisBinding binding;
  return set!=null&&set.TryGetValue(row.Name,out binding)?binding:null;
 }

 IEnumerable<HotasRow> HotasRows() {
  foreach(string name in HotasAxisNames)yield return new HotasRow{Group=HotasModel.Group.NativeAxis,Name=name,Label="GAME  /  Flight axes"};
  foreach(string category in HotasCategories)
   foreach(var pair in HotasButtonActions.Where(p=>HotasCategory(p.Key)==category))
    yield return new HotasRow{Group=HotasModel.Group.NativeAction,Name=pair.Key,Label="GAME  /  "+category};
  foreach(string name in HotasPad.WheelOnlyControls.Concat(HotasPad.ButtonBits.Keys))
   yield return new HotasRow{Group=HotasModel.Group.Virtual,Name=name,Label="FLIGHT CONTROL  /  Wheels, menus and buttons"};
  foreach(string name in HotasPad.AnalogControls)
   yield return new HotasRow{Group=HotasModel.Group.Virtual,Name=name,Label="FLIGHT CONTROL  /  Triggers and sticks"};
 }

 static string HotasRowTitle(HotasRow row) {
  string title;
  return row.Group==HotasModel.Group.Virtual&&VirtualLabels.TryGetValue(row.Name,out title)?title:row.Name;
 }

 string HotasRowKey(HotasRow row){return row==null?null:(int)row.Group+":"+row.Name;}

 void RebuildHotasList() {
  if(hotasList==null)return;
  string keep=HotasRowKey(SelectedHotasRow());
  string filter=(hotasFilter.Text??"").Trim();
  hotasList.BeginUpdate();
  hotasList.Items.Clear();
  string lastHeader=null;
  ListViewItem pendingHeader=null;
  foreach(var row in HotasRows()){
   string title=HotasRowTitle(row);
   var binding=HotasBinding(row);
   string assigned=binding==null?"—":HotasModel.Describe(hotasProfile,binding,row.Group);
   if(filter.Length>0&&title.IndexOf(filter,StringComparison.OrdinalIgnoreCase)<0&&assigned.IndexOf(filter,StringComparison.OrdinalIgnoreCase)<0)continue;
   // A section header goes in only once something under it is listed.
   if(row.Label!=lastHeader){
    lastHeader=row.Label;
    pendingHeader=new ListViewItem(new[]{"— "+row.Label.ToUpperInvariant()+" —",""}){ForeColor=Color.FromArgb(150,134,60),BackColor=Color.FromArgb(18,18,8)};
   }
   if(pendingHeader!=null){hotasList.Items.Add(pendingHeader);pendingHeader=null;}
   var item=new ListViewItem(new[]{title,assigned}){Tag=row};
   item.ForeColor=binding!=null?Color.FromArgb(102,231,82):Ink;
   hotasList.Items.Add(item);
   if(HotasRowKey(row)==keep)item.Selected=true;
  }
  hotasList.EndUpdate();
  FitHotasColumns();
  ShowHotasSelection();
 }

 HotasRow SelectedHotasRow(){return hotasList!=null&&hotasList.SelectedItems.Count>0?hotasList.SelectedItems[0].Tag as HotasRow:null;}

 void ShowHotasSelection() {
  var row=SelectedHotasRow();
  var binding=row==null?null:HotasBinding(row);
  if(row==null)hotasDetail.Text="Choose an action on the list.";
  else hotasDetail.Text=HotasRowTitle(row)+"\n"+(binding==null?"Not assigned yet.":HotasModel.Describe(hotasProfile,binding,row.Group))+FlightMeaning(row);
  bool axis=binding!=null&&(HotasModel.KindOf(row.Group,binding)==HotasModel.Axis);
  hotasLoading=true;
  hotasInvert.Enabled=axis;
  hotasInvert.Checked=axis&&binding.Invert;
  hotasLoading=false;
  hotasCanvas.HighlightKey=binding==null?null:CanvasKey(binding,row.Group);
  hotasCanvas.Invalidate();
  if(binding!=null&&binding.DeviceIndex!=hotasView&&hotasProfile!=null&&binding.DeviceIndex<hotasProfile.Devices.Count&&hotasListening==null&&!hotasProbing){
   hotasView=binding.DeviceIndex;
   BuildHotasDeviceBar();
   ShowHotasDevice();
  }
 }

 // What a Flight Control button does in flight (no wheel open), from the Buttons tab. The same physical button can
 // also drive a wheel role, so it behaves differently by state, as on the gamepad.
 string FlightMeaning(HotasRow row) {
  if(row.Group!=HotasModel.Group.Virtual)return "";
  if(row.Name==HotasPad.WheelSelect||row.Name==HotasPad.WheelCancel)return Environment.NewLine+"pair with Right/Left trigger to fire";
  if(row.Name==HotasPad.WheelStepCw||row.Name==HotasPad.WheelStepCcw)return Environment.NewLine+"encoder: one button per direction";
  int i=Array.IndexOf(TapButtons,row.Name);
  if(i<0)return "";
  var tap=Array.Find(ButtonActions,a=>a.Id==tapActions[i]);
  int h=Array.IndexOf(HoldButtons,row.Name);
  var hold=h<0?null:Array.Find(ButtonActions,a=>a.Id==holdActions[h]);
  return Environment.NewLine+"In flight: "+(tap==null?tapActions[i]:tap.Caption)+(hold!=null&&hold.Id!="none"?", hold: "+hold.Caption:"");
 }

 static string CanvasKey(FlightAxisBinding binding,HotasModel.Group group) {
  string kind=HotasModel.KindOf(group,binding);
  if(kind==HotasModel.Hat)return "Hat:"+binding.InputIndex+":"+binding.Direction;
  return (kind==HotasModel.Button?"Button":"Axis")+":"+binding.InputIndex;
 }

 void ToggleInvert() {
  if(hotasLoading)return;
  var row=SelectedHotasRow();
  var binding=row==null?null:HotasBinding(row);
  if(binding==null)return;
  binding.Invert=hotasInvert.Checked;
  AfterHotasChange();
 }

 void ClearSelected() {
  var row=SelectedHotasRow();
  if(row==null)return;
  var set=HotasSet(row.Group,false);
  if(set!=null&&set.Remove(row.Name))AfterHotasChange();
 }

 void AfterHotasChange() {
  SaveHotasWorkingProfile();
  RebuildHotasList();
  RefreshHotasCanvasLabels();
  RefreshHotasStatus();
 }

 // ----- devices and the live preview -----

 void BuildHotasDeviceBar() {
  hotasDeviceBar.SuspendLayout();
  hotasDeviceBar.Controls.Clear();
  int count=hotasProfile==null?0:hotasProfile.Devices.Count;
  int barWidth=hotasDeviceBar.ClientSize.Width>50?hotasDeviceBar.ClientSize.Width:560;
  chipWidth=Math.Max(130,Math.Min(250,(barWidth-8*count)/Math.Max(count,1)));
  if(hotasProfile!=null)for(int i=0;i<hotasProfile.Devices.Count;i++){
   int index=i;
   var d=hotasProfile.Devices[i];
   var button=ActionButton(DeviceChipText(i),delegate{hotasView=index;BuildHotasDeviceBar();ShowHotasDevice();});
   button.AutoSize=false;button.Width=chipWidth;button.Height=32;button.Dock=DockStyle.None;button.Tag=i;
   button.Font=new Font("Consolas",9,FontStyle.Bold);
   TabLook(button,i==hotasView);
   button.MouseUp+=delegate(object sender,MouseEventArgs e){
    if(e.Button!=MouseButtons.Right||hotasSession==null)return;
    var menu=new ContextMenuStrip();
    foreach(var live in hotasSession.Devices){
     var chosen=live;
     menu.Items.Add("Use "+chosen.Inventory.Name+" here",null,delegate{try{RemapHotasDevice(index,chosen);}catch(Exception ex){hotasNotice=ex.Message;RefreshHotasStatus();}});
    }
    if(menu.Items.Count==0)menu.Items.Add("No controller connected").Enabled=false;
    menu.Show(button,e.Location);
   };
   hotasDeviceBar.Controls.Add(button);
  }
  hotasDeviceBar.ResumeLayout();
 }

 int chipWidth=240;
 string DeviceChipText(int i) {
  var d=hotasProfile.Devices[i];
  bool online=i<hotasMatches.Count&&hotasMatches[i]!=null&&hotasMatches[i].Connected;
  int room=Math.Max(10,(chipWidth-34)/7);
  string twin=HotasModel.TwinSuffix(hotasProfile,i);
  room=Math.Max(8,room-twin.Length);
  return (online?"● ":"○ ")+(d.Name.Length>room?d.Name.Substring(0,room-1)+"…":d.Name)+twin;
 }

 void ShowHotasDevice() {
  var device=hotasProfile!=null&&hotasView<hotasProfile.Devices.Count?hotasProfile.Devices[hotasView]:null;
  hotasCanvas.Device=device;
  if(device!=null){
   if(hotasProfile.Layouts==null)hotasProfile.Layouts=new List<DeviceLayout>();
   var layout=hotasProfile.Layouts.Find(l=>l.DeviceIndex==hotasView);
   if(layout==null){layout=new DeviceLayout{DeviceIndex=hotasView,Widgets=new List<LayoutWidget>()};hotasProfile.Layouts.Add(layout);}
   hotasCanvas.Arrangement=layout;
  }else hotasCanvas.Arrangement=null;
  hotasCanvas.Online=device!=null&&hotasView<hotasMatches.Count&&hotasMatches[hotasView]!=null&&hotasMatches[hotasView].Connected;
  hotasCanvas.Rebuild();
  RefreshHotasCanvasLabels();
 }

 int ReadHotasInput(string kind,int index) {
  if(hotasView>=hotasMatches.Count||hotasMatches[hotasView]==null)return 0;
  var live=hotasMatches[hotasView];
  var input=live.Inventory.Inputs.Find(i=>i.Kind==kind&&i.Index==index);
  if(input==null||!live.Connected)return 0;
  int raw=live.ReadRaw(input);
  return raw==0&&live.RecentPulse(input)?1:raw;
 }

 // Which action sits on which control, for the picture.
 void RefreshHotasCanvasLabels() {
  var labels=new Dictionary<string,string>();
  if(hotasProfile!=null)foreach(var pair in HotasModel.Groups(hotasProfile)){
   if(pair.Value==null)continue;
   foreach(var entry in pair.Value){
    var b=entry.Value;
    if(b.DeviceIndex!=hotasView)continue;
    string key=CanvasKey(b,pair.Key);
    string name=pair.Key==HotasModel.Group.Virtual?HotasRowTitle(new HotasRow{Group=pair.Key,Name=entry.Key}):entry.Key;
    string existing;
    labels[key]=labels.TryGetValue(key,out existing)?existing+", "+name:name;
   }
  }
  hotasCanvas.Labels=labels;
  hotasCanvas.Invalidate();
 }

 void ChooseHotasBackground() {
  if(hotasCanvas.Arrangement==null)throw new InvalidOperationException("Scan your controllers first.");
  using(var dialog=new OpenFileDialog{Filter="Pictures (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",Title="Choose a picture of this controller"}){
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   hotasCanvas.Arrangement.BackgroundImage=dialog.FileName;
   hotasCanvas.Rebuild();
   SaveHotasWorkingProfile();
  }
 }

 void ResetHotasLayout() {
  if(hotasCanvas.Arrangement==null)return;
  hotasCanvas.Arrangement.BackgroundImage=null;
  hotasCanvas.ResetLayout();
  SaveHotasWorkingProfile();
 }

 // ----- listening for a control -----

 void ToggleListening() {
  if(hotasListening!=null||hotasProbing){StopListening();RefreshHotasStatus();return;}
  var row=SelectedHotasRow();
  if(row==null)throw new InvalidOperationException("Choose an action on the list first.");
  if(hotasSession==null||hotasMatches.All(m=>m==null||!m.Connected))
   throw new InvalidOperationException("No controller is connected. Plug it in and press Scan controllers.");
  StartListening(row);
 }

 InputCapture.Mode CaptureMode(HotasRow row) {
  if(row.Group==HotasModel.Group.NativeAxis)return InputCapture.Mode.Axis;
  if(row.Group==HotasModel.Group.Virtual&&HotasPad.IsAnalog(row.Name))return InputCapture.Mode.Any;
  return InputCapture.Mode.Digital;
 }

 void StartListening(HotasRow row) {
  hotasListening=row;
  hotasProbing=false;
  hotasCapture=new InputCapture(CaptureMode(row),row.Group!=HotasModel.Group.NativeAxis);
  hotasStableKey=null;hotasStable=0;hotasListenTicks=0;
  hotasNotice=null;
  hotasAssign.Text="Cancel";
  RefreshHotasStatus();
 }

 void StartProbe() {
  if(hotasSession==null||hotasMatches.All(m=>m==null||!m.Connected))
   throw new InvalidOperationException("No controller is connected. Plug it in and press Scan controllers.");
  StopListening();
  hotasProbing=true;
  hotasCapture=new InputCapture(InputCapture.Mode.Any,true);
  hotasStableKey=null;hotasStable=0;hotasListenTicks=0;
  hotasAssign.Text="Cancel";
  RefreshHotasStatus();
 }

 void StopListening() {
  hotasListening=null;
  hotasProbing=false;
  hotasCapture=null;
  if(hotasAssign!=null)hotasAssign.Text="Assign: press a control";
 }

 void HotasTick() {
  if(hotasSession!=null)hotasSession.Poll();
  // Lights on the device chips show which physical device is which.
  if(hotasDeviceBar!=null)foreach(Control chip in hotasDeviceBar.Controls){
   int i=(int)chip.Tag;
   string text=DeviceChipText(i);
   if(chip.Text!=text)chip.Text=text;
  }
  var device=hotasView<hotasMatches.Count?hotasMatches[hotasView]:null;
  bool online=device!=null&&device.Connected;
  if(hotasCanvas.Online!=online)hotasCanvas.Online=online;
  hotasCanvas.Invalidate();
  if(hotasCapture==null)return;
  hotasListenTicks++;
  hotasCapture.NextTick();
  for(int d=0;d<hotasMatches.Count;d++){
   var live=hotasMatches[d];
   if(live==null||!live.Connected)continue;
   foreach(var input in live.Inventory.Inputs){
    int raw=live.ReadRaw(input);
    // An encoder detent can be over before this tick: count a recent press as a press.
    if(raw==0&&live.RecentPulse(input))raw=1;
    hotasCapture.Feed(d,input.Kind,input.Index,raw);
   }
  }
  hotasCapture.Settle();
  var best=hotasCapture.Best();
  string key=best==null?null:best.Device+":"+best.Kind+best.Index+":"+best.Direction+(best.Ambiguous?"?":"");
  if(best==null||best.Ambiguous){hotasStableKey=null;hotasStable=0;}
  else if(key==hotasStableKey)hotasStable++;
  else{hotasStableKey=key;hotasStable=1;}
  if(best!=null)hotasCanvas.HighlightKey=best.Kind=="Hat"?"Hat:"+best.Index+":"+best.Direction:best.Kind+":"+best.Index;
  if(best!=null&&best.Device!=hotasView){hotasView=best.Device;BuildHotasDeviceBar();ShowHotasDevice();hotasCanvas.HighlightKey=best.Kind=="Hat"?"Hat:"+best.Index+":"+best.Direction:best.Kind+":"+best.Index;}
  int need=best!=null&&best.Kind=="Axis"?18:8; // a button settles quickly; an axis is given time to reach where the player is taking it
  if(hotasProbing){
   if(best!=null&&!best.Ambiguous&&hotasStable>=need){
    string name=hotasProfile.Devices[best.Device].Name;
    StopListening();
    hotasNotice=name+" reports "+(best.Kind=="Hat"?"Hat "+(best.Index+1)+" "+HotasModel.DirectionName(best.Direction):best.Kind+" "+(best.Index+1))+". It is listed and ready to assign.";
    RefreshHotasStatus();
    return;
   }
   if(hotasListenTicks>=150){
    StopListening();
    hotasNotice="Nothing changed. If the control does nothing here, the game cannot see it either. Try another mode on the device, or bind it to a button in the manufacturer's software, then scan again.";
    RefreshHotasStatus();
    return;
   }
  }else if(hotasListening!=null){
   if(best!=null&&!best.Ambiguous&&hotasStable>=need){AcceptCandidate(hotasListening,best);return;}
   if(hotasListenTicks>=ListenLimit){StopListening();hotasNotice="No control was detected. Select the action and try again.";}
  }
  RefreshHotasStatus();
 }

 // Turns what was detected into a binding, or explains why the game cannot use it.
 void AcceptCandidate(HotasRow row,CaptureCandidate c) {
  var binding=new FlightAxisBinding{DeviceIndex=c.Device,InputIndex=c.Index};
  var old=HotasBinding(row);
  string problem=null;
  switch(row.Group){
   case HotasModel.Group.NativeAxis:
    break;
   case HotasModel.Group.NativeAction:
    if(c.Kind=="Button")binding.Kind=HotasModel.Button;
    else if(c.Kind=="Axis"){binding.Kind=HotasModel.AxisButton;binding.Direction=c.Direction<0?-1:1;}
    else{
     binding.Kind=HotasModel.Hat;binding.Direction=c.Direction;
     if(c.Index!=0||!(c.Direction==1||c.Direction==2||c.Direction==4||c.Direction==8))
      problem="FotG binds only the first hat's four directions to game actions. Use another control, or give this hat a Flight Control job.";
    }
    break;
   default:
    if(c.Kind=="Button"){binding.Kind=HotasModel.Button;binding.Direction=0;}
    else if(c.Kind=="Hat"){binding.Kind=HotasModel.Hat;binding.Direction=c.Direction;}
    else{binding.Kind=HotasModel.Axis;binding.Direction=HotasPad.IsAnalog(row.Name)?0:(c.Direction<0?-1:1);}
    break;
  }
  if(problem!=null){
   hotasNotice=problem;
   // Keep listening for a different control.
   StartListening(row);
   hotasNotice=problem;
   return;
  }
  if(old!=null&&old.Invert&&(binding.Kind==HotasModel.Axis||binding.Kind==null))binding.Invert=true;
  HotasSet(row.Group,true)[row.Name]=binding;
  StopListening();
  hotasNotice="Assigned "+HotasRowTitle(row)+" to "+HotasModel.Describe(hotasProfile,binding,row.Group)+".";
  hotasView=c.Device;
  BuildHotasDeviceBar();
  ShowHotasDevice();
  AfterHotasChange();
  ReselectRow(row);
 }

 void ReselectRow(HotasRow row) {
  foreach(ListViewItem item in hotasList.Items)
   if(item.Tag is HotasRow&&HotasRowKey((HotasRow)item.Tag)==HotasRowKey(row)){item.Selected=true;item.EnsureVisible();break;}
 }

 // Clicking a control in the picture assigns it to the selected action, with no need to press the hardware.
 void HotasCanvasClicked(string kind,int index,int direction) {
  var row=SelectedHotasRow();
  if(row==null){hotasNotice="Choose an action on the list, then click a control here.";RefreshHotasStatus();return;}
  if(hotasProfile==null||hotasView>=hotasProfile.Devices.Count)return;
  var click=new CaptureCandidate{Device=hotasView,Kind=kind,Index=index,Direction=kind=="Hat"?direction:1};
  if(row.Group==HotasModel.Group.NativeAxis&&kind!="Axis"){hotasNotice=HotasRowTitle(row)+" needs an axis (a bar), not a button or hat.";RefreshHotasStatus();return;}
  if(row.Group==HotasModel.Group.Virtual&&HotasPad.IsAnalog(row.Name)&&kind=="Axis")click.Direction=0;
  AcceptCandidate(row,click);
 }

 // ----- guidance, problems and apply -----

 void RefreshHotasStatus() {
  if(hotasGuide==null)return;
  string guide;
  int online=hotasMatches.Count(m=>m!=null&&m.Connected);
  int assigned=hotasProfile==null?0:HotasModel.Groups(hotasProfile).Sum(p=>p.Value==null?0:p.Value.Count);
  if(hotasProbing)guide="Press or move the control now. If the game can see it, it will light up here.";
  else if(hotasListening!=null){
   guide="Now press the button, move the axis or push the hat for  \""+HotasRowTitle(hotasListening)+"\".  Cancel to stop.";
  }
  else if(hotasSession==null)guide=hotasNotice??"Press Scan controllers to find your sticks, throttles and pedals.";
  else if(hotasProfile==null||hotasProfile.Devices.Count==0)guide="No controller found. Plug in your stick, throttle or pedals, then press Scan controllers.";
  else if(assigned==0)guide=online+" controller"+(online==1?"":"s")+" found. Move a control to see it light up, pick an action on the left, then press that control.";
  else guide=assigned+" assignment"+(assigned==1?"":"s")+" saved. Close the game, then Apply to pilot when you are happy.";
  if(hotasNotice!=null&&hotasSession!=null&&!hotasProbing&&hotasListening==null)guide=hotasNotice;
  else if(hotasNotice!=null&&hotasListening!=null)guide+="\n"+hotasNotice;
  hotasGuide.Text=guide;
  var problems=new List<string>();
  if(hotasProfile!=null){
   problems.AddRange(HotasModel.Conflicts(hotasProfile).Select(c=>"Conflict: "+c));
   problems.AddRange(HotasModel.Overlaps(hotasProfile).Select(c=>"Note: "+c));
   foreach(var pair in HotasModel.Groups(hotasProfile))
    if(pair.Value!=null)foreach(var entry in pair.Value)
     if(entry.Value.DeviceIndex<hotasMatches.Count&&hotasMatches[entry.Value.DeviceIndex]==null){
      problems.Add("Not connected: "+hotasProfile.Devices[entry.Value.DeviceIndex].Name);
      break;
     }
  }
  if(hotasSlotNote!=null)problems.Insert(0,hotasSlotNote);
  hotasIssues.Text=String.Join("\n",problems.Distinct().Take(3));
 }

 void ApplyHotasProfile() {
  if(hotasProfile==null)throw new InvalidOperationException("Scan your controllers and assign at least one control first.");
  StopListening();
  HardwareInspector.Validate(hotasProfile);
  var conflicts=HotasModel.Conflicts(hotasProfile);
  if(conflicts.Count>0)throw new InvalidOperationException("Give each physical input one action before applying:\r\n"+String.Join("\r\n",conflicts));
  // The game needs a unique GUID for its own bindings; Flight Control reads devices itself.
  var guids=hotasProfile.Devices.Select((d,i)=>{
   var live=i<hotasMatches.Count?hotasMatches[i]:null;
   return live!=null&&live.Connected&&hotasSession.Devices.Count(o=>o.Connected&&String.Equals(o.Inventory.Guid,live.Inventory.Guid,StringComparison.OrdinalIgnoreCase))==1?live.Inventory.Guid:null;
  }).ToArray();
  foreach(var pair in HotasModel.Groups(hotasProfile)){
   if(pair.Value==null)continue;
   bool native=pair.Key!=HotasModel.Group.Virtual;
   foreach(var entry in pair.Value){
    int d=entry.Value.DeviceIndex;
    var live=d<hotasMatches.Count?hotasMatches[d]:null;
    if(live==null||!live.Connected)
     throw new InvalidOperationException("Connect "+hotasProfile.Devices[d].Name+" for "+entry.Key+", then press Scan controllers.");
    if(native&&guids[d]==null)
     throw new InvalidOperationException(hotasProfile.Devices[d].Name+" looks identical to another connected device, and FotG needs a unique GUID for its own bindings. Give "+entry.Key+" a Flight Control job instead, or unplug the twin.");
   }
  }
  SaveHotasWorkingProfile();
  ApplyHotas(hotasProfile,guids);
  hotasSlotNote=HotasSlotNote();
  RefreshHotasStatus();
 }
}

// Stand-in controllers for previews and screenshots; never used when playing.
sealed class HotasDemoDevice : FlightDevices.HardwareSession.Device {
 public Func<HardwareInput,int> Source;
 internal override bool Connected{get{return true;}}
 internal override int ReadRaw(HardwareInput input){return Source(input);}
}

partial class ControllerSetup {
 internal readonly Dictionary<string,int> demoValues=new Dictionary<string,int>();
 int DemoValue(int device,HardwareInput input){int v;return demoValues.TryGetValue(device+":"+input.Kind+input.Index,out v)?v:0;}

 internal void HotasDemo(bool keepPrefs=false) {
  Func<string,string,int,int,HardwareDevice> make=delegate(string name,string guid,int axes,int buttons){
   var d=new HardwareDevice{Name=name,Guid=guid,Inputs=new List<HardwareInput>()};
   for(int i=0;i<axes;i++)d.Inputs.Add(new HardwareInput{Kind="Axis",Index=i});
   for(int i=0;i<buttons;i++)d.Inputs.Add(new HardwareInput{Kind="Button",Index=i});
   return d;
  };
  var stick=make("Logitech Freedom 2.4",new string('A',32),4,10);
  stick.Inputs.Add(new HardwareInput{Kind="Hat",Index=0});
  var pedals=make("PXN Pedals",new string('B',32),3,1);
  demoValues["0:Axis0"]=12000;demoValues["0:Axis1"]=-24000;demoValues["0:Axis3"]=7232;demoValues["0:Hat0"]=1;
  demoValues["0:Button0"]=1;demoValues["0:Button5"]=1;demoValues["1:Axis0"]=-9000;demoValues["1:Axis1"]=30000;
  hotasSession=new FlightDevices.HardwareSession(new FlightDevices.HardwareSession.Device[]{
   new HotasDemoDevice{Inventory=stick,Source=i=>DemoValue(0,i)},
   new HotasDemoDevice{Inventory=pedals,Source=i=>DemoValue(1,i)}});
  hotasProfile=new HardwareProfile{SchemaVersion=HardwareInspector.CurrentSchema,Backend="SDL3",Devices=new List<HardwareDevice>{stick,pedals},
   FlightAxes=new Dictionary<string,FlightAxisBinding>{
    {"Pitch",new FlightAxisBinding{DeviceIndex=0,InputIndex=1,Invert=true}},{"Roll",new FlightAxisBinding{DeviceIndex=0,InputIndex=0}},
    {"Yaw",new FlightAxisBinding{DeviceIndex=1,InputIndex=0}},{"Absolute throttle",new FlightAxisBinding{DeviceIndex=0,InputIndex=3}}},
   FlightButtons=new Dictionary<string,FlightAxisBinding>{
    {"Fire primary",new FlightAxisBinding{DeviceIndex=0,InputIndex=0,Kind=HotasModel.Button}},
    {"Fire secondary",new FlightAxisBinding{DeviceIndex=0,InputIndex=1,Kind=HotasModel.Button}},
    {"Target next ship",new FlightAxisBinding{DeviceIndex=0,InputIndex=2,Kind=HotasModel.Button}},
    {"Afterburner",new FlightAxisBinding{DeviceIndex=0,InputIndex=0,Kind=HotasModel.Hat,Direction=HotasModel.HatUp}}},
   Virtual=new Dictionary<string,FlightAxisBinding>{
    {"A",new FlightAxisBinding{DeviceIndex=0,InputIndex=4,Kind=HotasModel.Button}},
    {"Dpad right",new FlightAxisBinding{DeviceIndex=0,InputIndex=0,Kind=HotasModel.Hat,Direction=HotasModel.HatRight}},
    {"Right trigger",new FlightAxisBinding{DeviceIndex=1,InputIndex=1,Kind=HotasModel.Axis}},
    {"Wheel select",new FlightAxisBinding{DeviceIndex=0,InputIndex=6,Kind=HotasModel.Button}},
    {"Wheel cancel",new FlightAxisBinding{DeviceIndex=0,InputIndex=7,Kind=HotasModel.Button}},
    {"Wheel step clockwise",new FlightAxisBinding{DeviceIndex=0,InputIndex=8,Kind=HotasModel.Button}},
    {"Wheel step counter-clockwise",new FlightAxisBinding{DeviceIndex=0,InputIndex=9,Kind=HotasModel.Button}}}};
  if(!keepPrefs)prefs=null;
  hotasLoading=false;
  hotasNotice=null;
  UseHotasSession();
 }

 // Read-only diagnostic: list what the game's engine can see. Usage: --hotas-scan <game folder> <output file>
 static int HotasScanCli(string[] args) {
  int at=Array.IndexOf(args,"--hotas-scan");
  if(at<0||at+2>=args.Length)return 1;
  var text=new System.Text.StringBuilder();
  try{
   using(var session=new FlightDevices.HardwareSession(args[at+1])){
    session.Poll();
    text.AppendLine("Devices: "+session.Devices.Count);
    foreach(var d in session.Devices){
     var i=d.Inventory;
     text.AppendLine(i.Name+" | guid "+i.Guid+" | serial "+(i.Serial??"-")+" | axes "+i.Inputs.Count(x=>x.Kind=="Axis")+" buttons "+i.Inputs.Count(x=>x.Kind=="Button")+" hats "+i.Inputs.Count(x=>x.Kind=="Hat"));
    }
   }
  }catch(Exception e){text.AppendLine("ERROR: "+e.Message);}
  File.WriteAllText(args[at+2],text.ToString());
  return 0;
 }

 // Drives the real assign flow against stand-in controllers and checks the profile it builds.
 static int HotasUiTestCli(string[] args) {
  int at=Array.IndexOf(args,"--hotas-uitest");
  if(at<0||at+1>=args.Length)return 1;
  string result;
  try{result=HotasUiTest();}catch(Exception e){result="FAIL: exception "+e;}
  File.WriteAllText(args[at+1],result);
  return result.StartsWith("PASS")?0:2;
 }

 static string HotasUiTest() {
  using(var form=new ControllerSetup()){
   form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2000,-2000);form.ShowInTaskbar=false;
   form.Show();
   form.HotasDemo();
   form.ShowPage(false,false,false,true);
   form.hotasTimer.Stop();   // the test supplies the ticks
   Func<string,bool> pick=delegate(string name){
    foreach(ListViewItem item in form.hotasList.Items){
     var row=item.Tag as HotasRow;
     if(row!=null&&row.Name==name){item.Selected=true;return true;}
    }
    return false;
   };
   Action<int> ticks=delegate(int n){for(int i=0;i<n;i++)form.HotasTick();};
   var profile=form.hotasProfile;

   // 1. A button on the stick becomes a native action.
   if(!pick("Cycle primary weapon"))return "FAIL: row missing";
   form.ToggleListening();
   ticks(12);
   form.demoValues["0:Button7"]=1;
   ticks(14);
   FlightAxisBinding b;
   if(form.hotasListening!=null||!profile.FlightButtons.TryGetValue("Cycle primary weapon",out b)||b.DeviceIndex!=0||b.InputIndex!=7||b.Kind!=HotasModel.Button)return "FAIL: button not assigned ("+form.hotasGuide.Text+")";
   form.demoValues["0:Button7"]=0;

   // 2. An axis on the pedals becomes the relative throttle.
   if(!pick("Relative throttle"))return "FAIL: axis row missing";
   form.ToggleListening();
   ticks(12);
   form.demoValues["1:Axis2"]=26000;
   ticks(30);
   if(!profile.FlightAxes.TryGetValue("Relative throttle",out b)||b.DeviceIndex!=1||b.InputIndex!=2)return "FAIL: axis not assigned ("+form.hotasGuide.Text+")";
   form.demoValues["1:Axis2"]=0;

   // 3. A diagonal hat press is refused for a game action, a plain direction is accepted.
   if(!pick("Cycle primary backward"))return "FAIL: hat row missing";
   form.demoValues["0:Hat0"]=0;
   form.ToggleListening();
   ticks(12);
   form.demoValues["0:Hat0"]=HotasModel.HatUp|HotasModel.HatRight;
   ticks(14);
   if(profile.FlightButtons.ContainsKey("Cycle primary backward")||form.hotasListening==null||!form.hotasGuide.Text.Contains("first hat"))return "FAIL: diagonal hat not refused ("+form.hotasGuide.Text+")";
   form.demoValues["0:Hat0"]=0;
   ticks(12);
   form.demoValues["0:Hat0"]=HotasModel.HatDown;
   ticks(14);
   if(!profile.FlightButtons.TryGetValue("Cycle primary backward",out b)||b.Kind!=HotasModel.Hat||b.Direction!=HotasModel.HatDown)return "FAIL: hat not assigned ("+form.hotasGuide.Text+")";
   form.demoValues["0:Hat0"]=0;

   // 4. A Flight Control trigger takes a button.
   if(!pick("Left trigger"))return "FAIL: virtual row missing";
   form.ToggleListening();
   ticks(12);
   form.demoValues["0:Button8"]=1;
   ticks(14);
   if(!profile.Virtual.TryGetValue("Left trigger",out b)||b.Kind!=HotasModel.Button||b.InputIndex!=8)return "FAIL: virtual not assigned ("+form.hotasGuide.Text+")";
   form.demoValues["0:Button8"]=0;

   // 5. Clicking the picture assigns without touching the hardware, and a shared input is reported.
   if(!pick("Fire secondary"))return "FAIL: fire secondary row missing";
   form.HotasCanvasClicked("Button",0,0);
   if(HotasModel.Conflicts(profile).Count<1||!form.hotasIssues.Text.Contains("Conflict"))return "FAIL: conflict not reported ("+form.hotasIssues.Text+")";

   // 6. Clear removes an assignment, and the profile still validates once the conflict is gone.
   if(!pick("Fire secondary"))return "FAIL: row missing again";
   form.ClearSelected();
   if(profile.FlightButtons.ContainsKey("Fire secondary"))return "FAIL: clear did nothing";
   HardwareInspector.Validate(profile);
   // 7. Moving a control in Arrange mode is remembered in the profile.
   var layout=form.hotasCanvas.Arrangement;
   form.hotasCanvas.ResetLayout();
   if(layout.Widgets.Count!=0)return "FAIL: reset left saved positions";
   form.HotasTick();
   // 7b. The Controls switch: Simple and Advanced, with the advanced pages following it.
   form.modeAdvanced.PerformClick();
   if(!form.advancedMode.Checked||!form.buttonsPage.Enabled)return "FAIL: Advanced did not switch the advanced controls on";
   form.modeSimple.PerformClick();
   if(form.advancedMode.Checked||form.buttonsPage.Enabled)return "FAIL: Simple did not switch the advanced controls off";
   // 8. A shared profile whose pedals are not the ones plugged in: use the connected pedals in their place.
   var pedalsLive=form.hotasSession.Devices[1];
   var otherPedals=new HardwareDevice{Name="Someone else's pedals",Guid=new string('C',32),Inputs=new List<HardwareInput>()};
   foreach(var input in pedalsLive.Inventory.Inputs)otherPedals.Inputs.Add(new HardwareInput{Kind=input.Kind,Index=input.Index});
   profile.Devices[1]=otherPedals;
   form.UseHotasSession();
   if(profile.Devices.Count!=3||form.hotasMatches[1]!=null)return "FAIL: the unfamiliar device was not treated as missing";
   int before=profile.FlightAxes.Values.Count(x=>x.DeviceIndex==1)+profile.Virtual.Values.Count(x=>x.DeviceIndex==1);
   form.RemapHotasDevice(1,pedalsLive);
   if(profile.Devices.Count!=2||form.hotasMatches[1]==null||profile.Devices[1].Guid!=pedalsLive.Inventory.Guid)return "FAIL: remapping did not take over the connected pedals";
   int after=profile.FlightAxes.Values.Count(x=>x.DeviceIndex==1)+profile.Virtual.Values.Count(x=>x.DeviceIndex==1);
   if(before==0||before!=after)return "FAIL: remapping disturbed the bindings";
   HardwareInspector.Validate(profile);
   form.Close();
  }
  return "PASS";
 }

 static int RenderHotasCli(string[] args) {
  int at=Array.IndexOf(args,"--render-hotas");
  if(at<0||at+1>=args.Length)return 1;
  try{return RenderHotas(args,at);}
  catch(Exception e){File.WriteAllText(args[at+1]+".err",e.ToString());return 2;}
 }

 static int RenderHotas(string[] args,int at) {
  using(var form=new ControllerSetup()){
   form.StartPosition=FormStartPosition.Manual;
   form.Location=new Point(-2000,-2000);
   form.ShowInTaskbar=false;
   form.Show();
   // After the picture name: live "<game folder>" (a real read-only scan), listen, select:<action>, device:<n>, arrange.
   var tokens=args.Skip(at+2).ToList();
   bool live=tokens.Count>1&&tokens[0]=="live";
   if(live){form.root.Text=tokens[1];form.prefs=null;tokens.RemoveRange(0,2);}   // nothing is saved
   else form.HotasDemo();
   form.ShowPage(false,false,false,true);
   form.hotasShowSetup=false;form.ShowHotasSetup();
   Func<string,bool> pick=delegate(string name){
    foreach(ListViewItem item in form.hotasList.Items){
     var row=item.Tag as HotasRow;
     if(row!=null&&row.Name==name){item.Selected=true;item.EnsureVisible();return true;}
    }
    return false;
   };
   for(int settle=0;settle<5;settle++){Application.DoEvents();System.Threading.Thread.Sleep(60);}
   foreach(string token in tokens){
    if(token=="listen"&&pick("Pitch"))form.StartListening((HotasRow)form.hotasList.SelectedItems[0].Tag);
    else if(token.StartsWith("select:")){if(pick(token.Substring(7)))form.hotasList.TopItem=form.hotasList.SelectedItems[0];}
    else if(token.StartsWith("device:")){form.hotasView=Int32.Parse(token.Substring(7));form.BuildHotasDeviceBar();form.ShowHotasDevice();}
    else if(token=="arrange"){form.hotasCanvas.Arrange=true;form.hotasArrange.Text="Done arranging";form.TabLook(form.hotasArrange,true);}
   }
   for(int i=0;i<5;i++){Application.DoEvents();System.Threading.Thread.Sleep(60);}
   using(var bitmap=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){
    form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));
    bitmap.Save(args[at+1],System.Drawing.Imaging.ImageFormat.Png);
   }
   form.Close();
  }
  return 0;
 }
}
