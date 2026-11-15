using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

sealed class HardwareProfile {
 public int SchemaVersion {get;set;}
 public string Backend {get;set;}
 public List<HardwareDevice> Devices {get;set;}
 public Dictionary<string,FlightAxisBinding> FlightAxes {get;set;}
 public Dictionary<string,FlightAxisBinding> FlightButtons {get;set;}
}

sealed class FlightAxisBinding {
 public int DeviceIndex {get;set;}
 public int AxisIndex {get;set;}
}

sealed class HardwareDevice {
 public string Name {get;set;}
 public string Guid {get;set;}
 public string Serial {get;set;}
 public List<HardwareInput> Inputs {get;set;}
}

sealed class HardwareInput {
 public string Kind {get;set;}
 public int Index {get;set;}
 public string Label {get;set;}
 public string Verification {get;set;}
}

static partial class FlightDevices {
 const string Sdl3 = "SDL3.dll";
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr SDL_OpenJoystick(uint id);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern void SDL_CloseJoystick(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr SDL_GetJoystickSerial(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GetNumJoystickAxes(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GetNumJoystickButtons(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GetNumJoystickHats(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern short SDL_GetJoystickAxis(IntPtr joystick,int index);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)]
 static extern bool SDL_GetJoystickButton(IntPtr joystick,int index);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern byte SDL_GetJoystickHat(IntPtr joystick,int index);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)]
 static extern bool SDL_JoystickConnected(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern void SDL_PumpEvents();

 internal sealed class HardwareSession : IDisposable {
  internal sealed class Device {
   internal IntPtr Handle;
   internal HardwareDevice Inventory;
   internal bool Connected {get{return SDL_JoystickConnected(Handle);}}
   internal int ReadRaw(HardwareInput input) {
    switch(input.Kind) {
     case "Axis": return SDL_GetJoystickAxis(Handle,input.Index);
     case "Button": return SDL_GetJoystickButton(Handle,input.Index)?1:0;
     default: return SDL_GetJoystickHat(Handle,input.Index)&15;
    }
   }

   internal string Read(HardwareInput input) {
    int raw=ReadRaw(input);
    switch(input.Kind) {
     case "Axis": return raw.ToString();
     case "Button": return raw!=0?"Pressed":"Released";
     default: return Hats[raw];
    }
   }
   static readonly string[] Hats={"Centered","Up","Right","Up-right","Down","—","Down-right","—","Left","Up-left","—","—","Down-left","—","—","—"};
  }

  internal readonly List<Device> Devices=new List<Device>();
  bool initialized;

  internal HardwareSession(string gameRoot) {
   if(!GameInstall.HasMyMod(gameRoot))
    throw new InvalidOperationException("Select your FotG 2 installation in the Console first.");
   EngineInfo engine=GameInstall.FindEngine(gameRoot,null);
   if(engine==null || !engine.Sdl3)
    throw new InvalidOperationException("The FotG 2 SDL3 engine was not found.");
   if(!SetDllDirectory(Path.GetDirectoryName(engine.Exe)))
    throw new InvalidOperationException("Could not select the FotG 2 engine folder.");
   try {
    if(!S3Init(0x200))throw new InvalidOperationException("SDL3 could not initialize joystick input.");
    initialized=true;
    int count;
    IntPtr ids=S3List(out count);
    if(ids==IntPtr.Zero)throw new InvalidOperationException("SDL3 could not enumerate joysticks.");
    try {
     for(int i=0;i<count;i++)Open((uint)Marshal.ReadInt32(ids,i*4));
    }finally{S3Free(ids);}
   }catch {
    Dispose();
    throw;
   }finally{SetDllDirectory(null);}
  }

  void Open(uint id) {
   string name=Marshal.PtrToStringAnsi(S3Name(id))??"Unknown joystick";
   IntPtr handle=SDL_OpenJoystick(id);
   if(handle==IntPtr.Zero)throw new InvalidOperationException("Could not open "+name+". Reconnect it and scan again.");
   var device=new Device{Handle=handle};
   Devices.Add(device);
   var guid=new StringBuilder(33);
   S3GuidText(S3Guid(id),guid,guid.Capacity);
   device.Inventory=new HardwareDevice{
    Name=name,Guid=guid.ToString(),Serial=Marshal.PtrToStringAnsi(SDL_GetJoystickSerial(handle)),
    Inputs=new List<HardwareInput>()
   };
   AddInputs(device.Inventory,"Axis",SDL_GetNumJoystickAxes(handle));
   AddInputs(device.Inventory,"Button",SDL_GetNumJoystickButtons(handle));
   AddInputs(device.Inventory,"Hat",SDL_GetNumJoystickHats(handle));
  }

  static void AddInputs(HardwareDevice device,string kind,int count) {
   if(count<0)throw new InvalidOperationException("Could not read "+kind.ToLowerInvariant()+" capabilities for "+device.Name+".");
   for(int i=0;i<count;i++)device.Inputs.Add(new HardwareInput{Kind=kind,Index=i});
  }

  internal void Poll(){SDL_PumpEvents();}

  public void Dispose() {
   foreach(var device in Devices)SDL_CloseJoystick(device.Handle);
   Devices.Clear();
   if(initialized){S3Quit();initialized=false;}
  }
 }
}

sealed class HardwareInspector : Form {
 static readonly string[] FlightActions={"Pitch","Yaw","Roll","Absolute throttle"};
 readonly ComboBox flightActions=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=185};
 readonly Label mapping=new Label{AutoSize=true};
 readonly string gameRoot;
 readonly DataGridView grid=new DataGridView{
  Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,
  AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill
 };
 readonly ListBox devices=new ListBox{Dock=DockStyle.Left,Width=275};
 readonly Label status=new Label{Dock=DockStyle.Bottom,Height=50};
 readonly Timer timer=new Timer{Interval=60};
 readonly JavaScriptSerializer json=new JavaScriptSerializer();
 FlightDevices.HardwareSession session;
 HardwareProfile profile;
 readonly List<FlightDevices.HardwareSession.Device> matches=new List<FlightDevices.HardwareSession.Device>();

 internal HardwareInspector(string root,Action<HardwareProfile,string[]> apply) {
  gameRoot=root;
  Text="Flight Control | HOTAS mode";
  Width=900;
  Height=560;
  StartPosition=FormStartPosition.CenterParent;
  var actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=42};
  AddAction(actions,"Rescan connections",Scan);
  AddAction(actions,"New inventory",NewInventory);
  AddAction(actions,"Open profile",OpenProfile);
  AddAction(actions,"Save profile",SaveProfile);
  AddAction(actions,"Apply to pilot",delegate{ApplyProfile(apply);});
  AddAction(actions,"Verify control",VerifyControl);
  var bindings=new FlowLayoutPanel{Dock=DockStyle.Top,Height=64};
  flightActions.Items.AddRange(FlightActions.Concat(ControllerSetup.HotasButtonActions.Keys).ToArray());
  flightActions.SelectedIndex=0;
  flightActions.SelectedIndexChanged+=delegate{ShowMapping();};
  bindings.Controls.Add(flightActions);
  AddAction(bindings,"Capture input",MapAction);
  AddAction(bindings,"Clear assignment",delegate{
   string action=(string)flightActions.SelectedItem;
   var assignments=Assignments(action);
   if(assignments!=null)assignments.Remove(action);
   ShowMapping();
  });
  bindings.Controls.Add(mapping);
  ShowMapping();
  Controls.Add(grid);
  Controls.Add(devices);
  Controls.Add(status);
  Controls.Add(bindings);
  Controls.Add(actions);
  grid.Columns.Add("input","SDL input");
  grid.Columns.Add("value","Live value");
  grid.Columns.Add("label","Label");
  grid.Columns.Add("verification","Verification");
  grid.Columns[3].ReadOnly=true;
  grid.Columns[0].ReadOnly=true;
  grid.Columns[1].ReadOnly=true;
  foreach(DataGridViewColumn column in grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
  grid.CellValueChanged+=delegate(object sender,DataGridViewCellEventArgs e){
   if(e.ColumnIndex==2&&e.RowIndex>=0&&grid.Rows[e.RowIndex].Tag is HardwareInput)
    {
    var row=grid.Rows[e.RowIndex];
    var input=(HardwareInput)row.Tag;
    input.Label=Convert.ToString(row.Cells[2].Value);
    input.Verification=null;
    row.Cells[3].Value=null;
   }
  };
  devices.SelectedIndexChanged+=delegate{ShowInputs();};
  timer.Tick+=delegate{UpdateReadings();};
  Shown+=delegate{Run(Scan);};
 }

 void AddAction(FlowLayoutPanel panel,string title,Action action) {
  var button=new Button{Text=title,AutoSize=true,Height=32};
  button.Click+=delegate{Run(action);};
  panel.Controls.Add(button);
 }

 void Run(Action action) {
  try{action();}
  catch(Exception error){MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 }

 void Scan() {
  grid.EndEdit();
  timer.Stop();
  matches.Clear();
  if(session!=null)session.Dispose();
  session=null;
  try{
   session=new FlightDevices.HardwareSession(gameRoot);
   if(profile==null||profile.Devices.Count==0)CaptureInventory();
  }finally{
   ShowProfile();
   if(session!=null)timer.Start();
  }
 }

 void NewInventory() {
  Scan();
  CaptureInventory();
  ShowProfile();
 }

 void CaptureInventory() {
  profile=new HardwareProfile{SchemaVersion=1,Backend="SDL3",
   Devices=session.Devices.Select(device=>device.Inventory).ToList()};
 }

 void OpenProfile() {
  using(var dialog=new OpenFileDialog{Filter="Hardware profile (*.json)|*.json"}) {
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   var opened=json.Deserialize<HardwareProfile>(File.ReadAllText(dialog.FileName));
   Validate(opened);
   grid.EndEdit();
   profile=opened;
   ShowProfile();
  }
 }

 void SaveProfile() {
  grid.EndEdit();
  Validate(profile);
  using(var dialog=new SaveFileDialog{Filter="Hardware profile (*.json)|*.json",FileName="hardware-profile.json"}) {
   if(dialog.ShowDialog(this)==DialogResult.OK)File.WriteAllText(dialog.FileName,json.Serialize(profile));
  }
 }

 internal static void Validate(HardwareProfile value) {
  if(value==null||value.SchemaVersion!=1||value.Backend!="SDL3"||value.Devices==null||value.Devices.Count==0)
   throw new InvalidDataException("Select a nonempty Flight Control SDL3 hardware profile.");
  foreach(var device in value.Devices) {
   if(device==null||String.IsNullOrWhiteSpace(device.Name)||device.Guid==null||
    device.Guid.Length!=32||!device.Guid.All(Uri.IsHexDigit)||device.Inputs==null)
    throw new InvalidDataException("The hardware profile contains an invalid device.");
   var keys=new HashSet<string>();
   foreach(var input in device.Inputs)
    if(input==null||input.Index<0||(input.Kind!="Axis"&&input.Kind!="Button"&&input.Kind!="Hat")||
     !keys.Add(input.Kind+input.Index))
     throw new InvalidDataException("The hardware profile contains an invalid or duplicate input.");
  }
  ValidateAssignments(value,value.FlightAxes,FlightActions,"Axis");
  ValidateAssignments(value,value.FlightButtons,ControllerSetup.HotasButtonActions.Keys,"Button");
 }

 static void ValidateAssignments(HardwareProfile value,Dictionary<string,FlightAxisBinding> assignments,
  IEnumerable<string> actions,string kind) {
  if(assignments==null)return;
  foreach(var entry in assignments){
   var binding=entry.Value;
   if(!actions.Contains(entry.Key)||binding==null||binding.DeviceIndex<0||
    binding.DeviceIndex>=value.Devices.Count||
    !value.Devices[binding.DeviceIndex].Inputs.Any(i=>i.Kind==kind&&i.Index==binding.AxisIndex))
    throw new InvalidDataException("The profile contains an invalid "+kind.ToLowerInvariant()+" assignment.");
  }
 }

 Dictionary<string,FlightAxisBinding> Assignments(string action) {
  if(profile==null)return null;
  return FlightActions.Contains(action)?profile.FlightAxes:profile.FlightButtons;
 }

 void ApplyProfile(Action<HardwareProfile,string[]> apply) {
  grid.EndEdit();
  Validate(profile);
  Scan();
  session.Poll();
  var resolved=matches.Select(device=>device!=null&&device.Connected&&
   session.Devices.Count(other=>other.Connected&&String.Equals(other.Inventory.Guid,device.Inventory.Guid,StringComparison.OrdinalIgnoreCase))==1
   ?device.Inventory.Guid:null).ToArray();
  foreach(var group in new[]{profile.FlightAxes,profile.FlightButtons}){
   if(group==null)continue;
   string kind=group==profile.FlightAxes?"Axis":"Button";
   foreach(var entry in group){
    var binding=entry.Value;
    if(resolved[binding.DeviceIndex]==null)
     throw new InvalidOperationException("Connect the assigned device for "+entry.Key+". FotG requires a unique GUID for each assigned device.");
    if(!matches[binding.DeviceIndex].Inventory.Inputs.Any(i=>i.Kind==kind&&i.Index==binding.AxisIndex))
     throw new InvalidOperationException("The assigned input for "+entry.Key+" is unavailable. Capture it again.");
   }
  }
  apply(profile,resolved);
 }

 void ShowMapping() {
  FlightAxisBinding binding;
  string action=(string)flightActions.SelectedItem;
  var assignments=Assignments(action);
  if(assignments==null||!assignments.TryGetValue(action,out binding)){
   mapping.Text="Unassigned. Select a device to capture.";
   return;
  }
  var device=profile.Devices[binding.DeviceIndex];
  var live=binding.DeviceIndex<matches.Count?matches[binding.DeviceIndex]:null;
  string kind=FlightActions.Contains(action)?"Axis":"Button";
  var axis=live==null?null:live.Inventory.Inputs.Find(i=>i.Kind==kind&&i.Index==binding.AxisIndex);
  mapping.Text=device.Name+" / "+kind+" "+binding.AxisIndex+
   (axis!=null&&live.Connected?" / "+live.Read(axis):" / Offline");
 }

 void MapAction() {
  grid.EndEdit();
  int index=devices.SelectedIndex;
  if(index<0||matches[index]==null||!matches[index].Connected)
   throw new InvalidOperationException("Select a connected device before capturing an axis.");
  CaptureControl(index,null,(string)flightActions.SelectedItem);
 }

 static bool SameDevice(HardwareDevice saved,HardwareDevice live) {
  return String.Equals(saved.Guid,live.Guid,StringComparison.OrdinalIgnoreCase)&&
   (String.IsNullOrEmpty(saved.Serial)||saved.Serial==live.Serial);
 }

 void ShowProfile() {
  devices.Items.Clear();
  matches.Clear();
  if(profile!=null)foreach(var saved in profile.Devices) {
   var candidates=session==null?new List<FlightDevices.HardwareSession.Device>():
    session.Devices.Where(live=>SameDevice(saved,live.Inventory)).ToList();
   var match=candidates.Count==1&&profile.Devices.Count(other=>SameDevice(other,candidates[0].Inventory))==1
    ?candidates[0]:null;
   // Fresh enumeration can inspect each open handle, even for identical devices.
   if(session!=null)match=session.Devices.Find(live=>ReferenceEquals(saved,live.Inventory))??match;
   matches.Add(match);
   devices.Items.Add(saved.Name+(match!=null?"":candidates.Count==0?" (missing)":" (ambiguous)"));
  }
  ShowMapping();
  if(devices.Items.Count>0)devices.SelectedIndex=0;
  else ShowInputs();
 }

 void ShowInputs() {
  grid.Rows.Clear();
  int index=devices.SelectedIndex;
  if(index<0){status.Text="Connect your controllers and scan devices, or open a saved profile.";return;}
  var saved=profile.Devices[index];
  foreach(var input in saved.Inputs) {
   int row=grid.Rows.Add(input.Kind+" "+input.Index,"",input.Label,input.Verification);
   grid.Rows[row].Tag=input;
  }
  UpdateReadings();
 }

 internal static bool SignalChanged(HardwareInput input,int baseline,int reading) {
  return input.Kind=="Axis"?Math.Abs(reading-baseline)>=2048:reading!=baseline;
 }

 internal static int StrongestAxis(int[] peaks) {
  int best=-1;
  for(int i=0;i<peaks.Length;i++)
   if(peaks[i]>=2048&&(best<0||peaks[i]>peaks[best]))best=i;
  return best;
 }

 internal static void ConfirmInput(HardwareInput expected,HardwareInput chosen) {
  if(chosen!=expected) {
   chosen.Label=expected.Label;
   expected.Label=null;
   expected.Verification=null;
  }
  chosen.Verification="Verified";
 }

 void VerifyControl() {
  grid.EndEdit();
  int index=devices.SelectedIndex;
  if(index<0||grid.CurrentRow==null||matches[index]==null||!matches[index].Connected)
   throw new InvalidOperationException("Select an input on a connected device.");
  var expected=(HardwareInput)grid.CurrentRow.Tag;
  if(String.IsNullOrWhiteSpace(expected.Label))
   throw new InvalidOperationException("Label the selected row with the physical control you want to verify.");
  CaptureControl(index,expected,null);
 }

 void CaptureControl(int index,HardwareInput expected,string action) {
  string control=action??expected.Label;
  var device=matches[index];
  using(var dialog=new Form{Text=(action==null?"Verify ":"Assign ")+control,Width=480,Height=340,StartPosition=FormStartPosition.CenterParent})
  using(var sampling=new Timer{Interval=30}) {
   var instructions=new Label{Dock=DockStyle.Top,Height=48,Text="Release all controls, then start capture."};
   var candidates=new ListBox{Dock=DockStyle.Fill};
   var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=45};
   string kind=FlightActions.Contains(action)?"Axis":"Button";
   var inputs=device.Inventory.Inputs.Where(input=>action==null||input.Kind==kind).ToList();
   var observed=new List<HardwareInput>();
   int[] baseline=null;
   var peaks=new int[inputs.Count];
   int listedAxis=-1;
   bool validCapture=false;
   AddAction(actions,"Start / retry",delegate{
    session.Poll();
    if(!device.Connected)throw new InvalidOperationException("Reconnect the device and rescan.");
    baseline=inputs.Select(device.ReadRaw).ToArray();
    Array.Clear(peaks,0,peaks.Length);
    listedAxis=-1;
    observed.Clear();
    candidates.Items.Clear();
    validCapture=true;
    instructions.Text="Move only the control for "+control+", then select the responding input.";
    sampling.Start();
   });
   AddAction(actions,"Confirm input",delegate{
    if(!validCapture||!device.Connected||candidates.SelectedIndex<0)return;
    var live=observed[candidates.SelectedIndex];
    var saved=profile.Devices[index].Inputs;
    var chosen=saved.Find(i=>i.Kind==live.Kind&&i.Index==live.Index);
    if(action==null&&chosen!=null&&chosen!=expected&&!String.IsNullOrWhiteSpace(chosen.Label)&&
     MessageBox.Show(dialog,"Replace the label '"+chosen.Label+"'?","Confirm input",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
    if(chosen==null){
     chosen=new HardwareInput{Kind=live.Kind,Index=live.Index};
     saved.Add(chosen);
    }
    if(action==null)ConfirmInput(expected,chosen);
    else {
     if(kind=="Axis"&&profile.FlightAxes==null)profile.FlightAxes=new Dictionary<string,FlightAxisBinding>();
     if(kind=="Button"&&profile.FlightButtons==null)profile.FlightButtons=new Dictionary<string,FlightAxisBinding>();
     Assignments(action)[action]=new FlightAxisBinding{DeviceIndex=index,AxisIndex=chosen.Index};
    }
    dialog.Close();
   });
   if(action==null)AddAction(actions,"Record no signal",delegate{
    if(!validCapture||!device.Connected||observed.Count!=0)return;
    expected.Verification="No signal";
    dialog.Close();
   });
   sampling.Tick+=delegate{
    session.Poll();
    if(!device.Connected){
     validCapture=false;
     sampling.Stop();
     instructions.Text="Device disconnected. Close this window and rescan.";
     return;
    }
    for(int i=0;i<inputs.Count;i++){
     var input=inputs[i];
     int reading=device.ReadRaw(input);
     if(input.Kind=="Axis"){
      peaks[i]=Math.Max(peaks[i],Math.Abs(reading-baseline[i]));
      continue;
     }
     if(!observed.Contains(input)&&SignalChanged(input,baseline[i],reading)){
      observed.Add(input);
      candidates.Items.Add(input.Kind+" "+input.Index);
     }
    }
    int best=StrongestAxis(peaks);
    if(best>=0&&best!=listedAxis){
     var axis=inputs[best];
     string label=axis.Kind+" "+axis.Index;
     if(listedAxis<0){
      observed.Insert(0,axis);
      candidates.Items.Insert(0,label);
     }else{
      observed[0]=axis;
      candidates.Items[0]=label;
     }
     listedAxis=best;
     if(candidates.Items.Count==1)candidates.SelectedIndex=0;
    }
   };
   dialog.Controls.Add(candidates);
   dialog.Controls.Add(actions);
   dialog.Controls.Add(instructions);
   timer.Stop();
   try{dialog.ShowDialog(this);}
   finally{timer.Start();}
  }
  ShowInputs();
  ShowMapping();
 }

 void UpdateReadings() {
  int index=devices.SelectedIndex;
  if(index<0||index>=matches.Count)return;
  if(session!=null)session.Poll();
  ShowMapping();
  var device=matches[index];
  bool connected=device!=null&&device.Connected;
  status.Text=connected?"Move a control to see its raw value. Edit the Label column to identify it.":
   "No unique live match. Reconnect the device and scan again.";
  foreach(DataGridViewRow row in grid.Rows) {
   var input=(HardwareInput)row.Tag;
   bool available=connected&&device.Inventory.Inputs.Any(item=>item.Kind==input.Kind&&item.Index==input.Index);
   string value=available?device.Read(input):"—";
   if(!Equals(row.Cells[1].Value,value))row.Cells[1].Value=value;
  }
 }

 protected override void Dispose(bool disposing) {
  if(disposing){timer.Dispose();if(session!=null)session.Dispose();}
  base.Dispose(disposing);
 }
}
