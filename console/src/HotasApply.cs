using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class ControllerSetup {
 // Native FotG actions a HOTAS can drive. The first ten are verified in-game on FotG 2.0.2; the rest
 // come from FSO's control list and should be checked in a practice mission before being relied on.
 internal static readonly string[] HotasCategories={"Weapons","Targeting","Throttle and thrust","Energy and shields","Views","Squad and mission"};
 static readonly string[][] HotasCatalog={
  new[]{"Weapons","Fire primary","FIRE_PRIMARY"},new[]{"Weapons","Fire secondary","FIRE_SECONDARY"},
  new[]{"Weapons","Cycle primary weapon","CYCLE_NEXT_PRIMARY"},new[]{"Weapons","Cycle primary backward","CYCLE_PREV_PRIMARY"},
  new[]{"Weapons","Cycle secondary weapon","CYCLE_SECONDARY"},new[]{"Weapons","Cycle primary firing rate","CYCLE_PRIMARY_WEAPON_SEQUENCE"},
  new[]{"Weapons","Cycle secondary firing rate","CYCLE_NUM_MISSLES"},new[]{"Weapons","Launch countermeasure","LAUNCH_COUNTERMEASURE"},
  new[]{"Targeting","Target next ship","TARGET_NEXT"},new[]{"Targeting","Target previous ship","TARGET_PREV"},
  new[]{"Targeting","Target closest hostile","TARGET_NEXT_CLOSEST_HOSTILE"},new[]{"Targeting","Target previous hostile","TARGET_PREV_CLOSEST_HOSTILE"},
  new[]{"Targeting","Target closest friendly","TARGET_NEXT_CLOSEST_FRIENDLY"},new[]{"Targeting","Target ship in reticle","TARGET_SHIP_IN_RETICLE"},
  new[]{"Targeting","Target closest attacker","TARGET_CLOSEST_SHIP_ATTACKING_SELF"},new[]{"Targeting","Target next subsystem","TARGET_NEXT_SUBOBJECT"},
  new[]{"Targeting","Target previous subsystem","TARGET_PREV_SUBOBJECT"},new[]{"Targeting","Target subsystem in reticle","TARGET_SUBOBJECT_IN_RETICLE"},
  new[]{"Targeting","Target next escort ship","TARGET_NEXT_ESCORT_SHIP"},new[]{"Targeting","Turn off targeting","STOP_TARGETING_SHIP"},
  new[]{"Targeting","Match target speed","MATCH_TARGET_SPEED"},new[]{"Targeting","Toggle auto speed matching","TOGGLE_AUTO_MATCH_TARGET_SPEED"},
  new[]{"Throttle and thrust","Afterburner","AFTERBURNER"},new[]{"Throttle and thrust","Zero throttle","ZERO_THROTTLE"},
  new[]{"Throttle and thrust","Full throttle","MAX_THROTTLE"},new[]{"Throttle and thrust","One-third throttle","ONE_THIRD_THROTTLE"},
  new[]{"Throttle and thrust","Two-thirds throttle","TWO_THIRDS_THROTTLE"},new[]{"Throttle and thrust","Throttle +5%","PLUS_5_PERCENT_THROTTLE"},
  new[]{"Throttle and thrust","Throttle -5%","MINUS_5_PERCENT_THROTTLE"},new[]{"Throttle and thrust","Forward thrust","FORWARD_THRUST"},
  new[]{"Throttle and thrust","Reverse thrust","REVERSE_THRUST"},new[]{"Throttle and thrust","Slide up","UP_SLIDE_THRUST"},
  new[]{"Throttle and thrust","Slide down","DOWN_SLIDE_THRUST"},new[]{"Throttle and thrust","Slide left","LEFT_SLIDE_THRUST"},
  new[]{"Throttle and thrust","Slide right","RIGHT_SLIDE_THRUST"},new[]{"Throttle and thrust","Toggle gliding","TOGGLE_GLIDING"},
  new[]{"Energy and shields","Equalize energy","ETS_EQUALIZE"},new[]{"Energy and shields","Weapon energy up","INCREASE_WEAPON"},
  new[]{"Energy and shields","Weapon energy down","DECREASE_WEAPON"},new[]{"Energy and shields","Shield energy up","INCREASE_SHIELD"},
  new[]{"Energy and shields","Shield energy down","DECREASE_SHIELD"},new[]{"Energy and shields","Engine energy up","INCREASE_ENGINE"},
  new[]{"Energy and shields","Engine energy down","DECREASE_ENGINE"},new[]{"Energy and shields","Equalize shields","SHIELD_EQUALIZE"},
  new[]{"Energy and shields","Augment shield forward","SHIELD_XFER_TOP"},new[]{"Energy and shields","Augment shield rear","SHIELD_XFER_BOTTOM"},
  new[]{"Energy and shields","Augment shield left","SHIELD_XFER_LEFT"},new[]{"Energy and shields","Augment shield right","SHIELD_XFER_RIGHT"},
  new[]{"Views","Chase view","VIEW_CHASE"},new[]{"Views","External view","VIEW_EXTERNAL"},
  new[]{"Views","Target view","VIEW_OTHER_SHIP"},new[]{"Views","Center view","VIEW_CENTER"},
  new[]{"Views","Free-look (hold)","VIEW_SLEW"},new[]{"Views","Look up","PADLOCK_UP"},new[]{"Views","Look rear","PADLOCK_DOWN"},
  new[]{"Views","Look left","PADLOCK_LEFT"},new[]{"Views","Look right","PADLOCK_RIGHT"},new[]{"Views","Target padlock view","VIEW_TRACK_TARGET"},
  new[]{"Squad and mission","Communications menu","SQUADMSG_MENU"},new[]{"Squad and mission","Squad: attack my target","ATTACK_MESSAGE"},
  new[]{"Squad and mission","Squad: engage enemy","ENGAGE_MESSAGE"},new[]{"Squad and mission","Squad: form on my wing","FORM_MESSAGE"},
  new[]{"Squad and mission","Squad: protect my target","PROTECT_MESSAGE"},new[]{"Squad and mission","Squad: cover me","COVER_MESSAGE"},
  new[]{"Squad and mission","Squad: return to base","WARP_MESSAGE"},new[]{"Squad and mission","Squad: rearm me","REARM_MESSAGE"},
  new[]{"Squad and mission","Show objectives","SHOW_GOALS"},new[]{"Squad and mission","Cycle radar range","RADAR_RANGE_CYCLE"},
  new[]{"Squad and mission","Show nav map","SHOW_NAVMAP"},new[]{"Squad and mission","Toggle autopilot","AUTO_PILOT_TOGGLE"},
  new[]{"Squad and mission","Cycle nav points","NAV_CYCLE"},new[]{"Squad and mission","Enter subspace","END_MISSION"},
  new[]{"Squad and mission","Toggle HUD","TOGGLE_HUD"}
 };

 internal static readonly Dictionary<string,string> HotasButtonActions=
  HotasCatalog.ToDictionary(row=>row[1],row=>row[2]);

 internal static string HotasCategory(string action) {
  var row=HotasCatalog.FirstOrDefault(r=>r[1]==action);
  return row==null?null:row[0];
 }

 static readonly Dictionary<string,string> HotasAxisActions=new Dictionary<string,string>{
  {"Pitch","JOY_PITCH_AXIS"},{"Yaw","JOY_HEADING_AXIS"},
  {"Roll","JOY_BANK_AXIS"},{"Absolute throttle","JOY_ABS_THROTTLE_AXIS"},
  {"Relative throttle","JOY_REL_THROTTLE_AXIS"}
 };
 internal static readonly string[] HotasAxisNames={"Pitch","Yaw","Roll","Absolute throttle","Relative throttle"};

 const string HotasStart=";; Flight Control HOTAS BEGIN";
 const string HotasEnd=";; Flight Control HOTAS END";

 // FSO reads a hat as a button after the 128 real ones; its code order is Down, Up, Left, Right
 // (hatBtnToEnum in FSO's joy-sdl.cpp). Only the first hat's four directions exist natively.
 // Not yet confirmed against FotG 2.0.2: if a hat direction fires the wrong action, change this table.
 static int NativeHatCode(int direction) {
  switch(direction){
   case HotasModel.HatDown:return 0;
   case HotasModel.HatUp:return 1;
   case HotasModel.HatLeft:return 2;
   default:return 3;
  }
 }

 internal static string HotasTable(string table,HardwareProfile profile,Dictionary<int,int> slots) {
  var text=new StringBuilder();
  text.AppendLine(HotasStart);
  var axes=profile.FlightAxes??new Dictionary<string,FlightAxisBinding>();
  // Precision axes are shaped and applied by Flight Control, so the game's own axis bindings are cleared to stop it also
  // acting on the same axes (it would otherwise use its default joystick axes).
  if(profile.AxisMode=="Precision"&&axes.Count>0){
   foreach(string id in HotasAxisActions.Values)AppendHotasUnbound(text,id);
   axes=new Dictionary<string,FlightAxisBinding>();
  }
  foreach(var entry in axes){
   bool absolute=entry.Key=="Absolute throttle";
   var binding=entry.Value;
   var flags=new List<string>{"AXIS"};
   if(!absolute)flags.Add("RELATIVE");
   if(binding.Invert)flags.Add("INVERTED");
   AppendHotasBinding(text,HotasAxisActions[entry.Key],"Primary",slots[binding.DeviceIndex],String.Join(", ",flags),binding.InputIndex);
   if(absolute&&!axes.ContainsKey("Relative throttle"))text.AppendLine("    $Bind: JOY_REL_THROTTLE_AXIS")
    .AppendLine("        $Primary:").AppendLine("            $Controller: CID_NONE")
    .AppendLine("        $Secondary:").AppendLine("            $Controller: CID_NONE");
  }
  if(profile.FlightButtons!=null)foreach(var entry in profile.FlightButtons){
   var binding=entry.Value;
   string action=HotasButtonActions[entry.Key];
   string flags="NONE";
   int input=binding.InputIndex;
   string kind=HotasModel.KindOf(HotasModel.Group.NativeAction,binding);
   if(kind==HotasModel.Hat){flags="HAT";input=128+NativeHatCode(binding.Direction);}
   else if(kind==HotasModel.AxisButton)flags=binding.Direction<0?"AXIS_BTN, INVERTED":"AXIS_BTN";
   AppendHotasBinding(text,action,DefaultPrimaryIsEmpty(table,action)?"Primary":"Secondary",
    slots[binding.DeviceIndex],flags,input);
  }
  text.AppendLine(HotasEnd);
  string pattern=@"(?ms)^"+Regex.Escape(HotasStart)+@"\r?\n.*?^"+Regex.Escape(HotasEnd)+@"\r?\n?";
  string baseline=Regex.Replace(table,pattern,"");
  if(baseline.Contains(HotasStart)||baseline.Contains(HotasEnd)||
   Regex.IsMatch(baseline,@"(?im)^\s*\$Name:\s*Flight Control HOTAS\s*(?:;.*)?$"))
   throw new InvalidDataException("The control table already contains an unrecognized HOTAS preset.");
  if(!Regex.IsMatch(baseline,@"(?im)^#ControlConfigOverride\b"))
   throw new InvalidDataException("The FotG control table has no default override.");
  var defaults=Regex.Match(baseline,@"(?ims)^#ControlConfigOverride[^\r\n]*\r?\n(?:[ \t]*(?:;[^\r\n]*)?\r?\n)*[ \t]*\$Name:[ \t]*default\b.*?^(?=#End\b)");
  if(!defaults.Success)throw new InvalidDataException("Could not locate the FotG default override.");
  return baseline.Insert(defaults.Index+defaults.Length,text.ToString());
 }

 // Friendly names of actions in the installed HOTAS block that the profile no longer assigns.
 internal static List<string> HotasDroppedActions(string table,HardwareProfile profile) {
  var block=Regex.Match(table,@"(?ms)^"+Regex.Escape(HotasStart)+@"\r?\n.*?^"+Regex.Escape(HotasEnd));
  var dropped=new List<string>();
  if(!block.Success)return dropped;
  var kept=new HashSet<string>((profile.FlightAxes??new Dictionary<string,FlightAxisBinding>()).Keys
   .Select(k=>HotasAxisActions[k]).Concat((profile.FlightButtons??new Dictionary<string,FlightAxisBinding>()).Keys
   .Select(k=>HotasButtonActions[k])));
  if(profile.AxisMode=="Precision"&&profile.FlightAxes!=null&&profile.FlightAxes.Count>0)foreach(string id in HotasAxisActions.Values)kept.Add(id);
  bool absoluteOnly=kept.Contains("JOY_ABS_THROTTLE_AXIS")&&!kept.Contains("JOY_REL_THROTTLE_AXIS");
  foreach(Match bind in Regex.Matches(block.Value,@"(?im)^\s*\$Bind:\s*(\w+)")){
   string id=bind.Groups[1].Value;
   if((id=="JOY_REL_THROTTLE_AXIS"&&absoluteOnly)||kept.Contains(id))continue;
   string name=HotasAxisActions.Concat(HotasButtonActions).Where(p=>p.Value==id).Select(p=>p.Key).FirstOrDefault()??id;
   if(!dropped.Contains(name))dropped.Add(name);
  }
  return dropped;
 }

 // The bridge's copy of the Flight Control (virtual gamepad) bindings; null when there are none.
 internal static string HotasMapJson(HardwareProfile profile) {
  bool precision=profile.AxisMode=="Precision"&&profile.FlightAxes!=null&&profile.FlightAxes.Count>0;
  bool features=profile.Virtual!=null&&profile.Virtual.Count>0;
  if(!features&&!precision)return null;
  var used=new List<int>();
  if(features)used.AddRange(profile.Virtual.Values.Select(b=>b.DeviceIndex));
  if(precision)used.AddRange(profile.FlightAxes.Values.Select(b=>b.DeviceIndex));
  var devices=used.Distinct().OrderBy(i=>i).ToList();
  var map=new HotasMapFile{Version=1,Devices=new List<HotasMapDevice>(),Controls=new Dictionary<string,HotasMapBinding>()};
  foreach(int index in devices)
   map.Devices.Add(new HotasMapDevice{Guid=profile.Devices[index].Guid.ToUpperInvariant(),Serial=profile.Devices[index].Serial});
  if(features)foreach(var entry in profile.Virtual){
   var b=entry.Value;
   string kind=HotasModel.KindOf(HotasModel.Group.Virtual,b);
   map.Controls[entry.Key]=new HotasMapBinding{DeviceIndex=devices.IndexOf(b.DeviceIndex),Kind=kind,Index=b.InputIndex,
    Direction=b.Direction,Invert=b.Invert,Shape=b.Shape==null?null:b.Shape.Copy()};
  }
  if(precision){
   map.Flight=new Dictionary<string,HotasFlightAxis>();
   foreach(var entry in profile.FlightAxes){
    var b=entry.Value;
    // A throttle lever starts with a smaller dead zone than a stick.
    var shape=b.Shape!=null?b.Shape.Copy():new HotasAxisSettings{Deadzone=entry.Key.EndsWith("throttle")?0.02:0.05};
    shape.Invert=b.Invert;
    map.Flight[entry.Key]=new HotasFlightAxis{DeviceIndex=devices.IndexOf(b.DeviceIndex),Index=b.InputIndex,Shape=shape};
   }
  }
  if(!HotasMapFile.Check(map))throw new InvalidDataException("The Flight Control bindings are not valid.");
  return new JavaScriptSerializer().Serialize(map);
 }

 static bool HotasSelfTest() {
  var stick=new HardwareDevice{Name="Stick",Guid=new string('A',32),Inputs=new List<HardwareInput>()};
  for(int i=0;i<8;i++)stick.Inputs.Add(new HardwareInput{Kind="Button",Index=i});
  for(int i=0;i<4;i++)stick.Inputs.Add(new HardwareInput{Kind="Axis",Index=i});
  stick.Inputs.Add(new HardwareInput{Kind="Hat",Index=0});
  var pedals=new HardwareDevice{Name="Pedals",Guid=new string('B',32),Inputs=new List<HardwareInput>{
   new HardwareInput{Kind="Axis",Index=0},new HardwareInput{Kind="Axis",Index=1},new HardwareInput{Kind="Button",Index=0}}};
  var button=new FlightAxisBinding{DeviceIndex=0,InputIndex=3};
  var profile=new HardwareProfile{SchemaVersion=HardwareInspector.CurrentSchema,Backend="SDL3",
   Devices=new List<HardwareDevice>{stick,pedals},
   FlightButtons=new Dictionary<string,FlightAxisBinding>{{"Fire primary",button},{"Fire secondary",new FlightAxisBinding{DeviceIndex=0,InputIndex=3}}},
   FlightAxes=new Dictionary<string,FlightAxisBinding>()};
  if(HotasModel.Conflicts(profile).Count!=1)return false;
  profile.FlightButtons["Fire secondary"].InputIndex=4;
  if(HotasModel.Conflicts(profile).Count!=0)return false;
  HardwareInspector.Validate(profile);
  string table="#ControlConfigOverride\r\n$Name: default\r\n#End\r\n";
  var slots=new Dictionary<int,int>{{0,0},{1,1}};
  string applied=HotasTable(table,profile,slots);
  if(!applied.Contains("$Bind: FIRE_PRIMARY")||!applied.Contains("CID_JOY0")||HotasTable(applied,profile,slots)!=applied)return false;
  profile.FlightButtons.Remove("Fire secondary");
  var dropped=HotasDroppedActions(applied,profile);
  if(dropped.Count!=1||dropped[0]!="Fire secondary")return false;

  // Hats, axis sides, inversion, a second device and the Flight Control group.
  profile.FlightButtons["Target next ship"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=0,Kind=HotasModel.Hat,Direction=HotasModel.HatUp};
  profile.FlightButtons["Afterburner"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=2,Kind=HotasModel.AxisButton,Direction=-1};
  profile.FlightAxes["Yaw"]=new FlightAxisBinding{DeviceIndex=1,InputIndex=0,Invert=true};
  profile.Virtual=new Dictionary<string,FlightAxisBinding>{
   {"A",new FlightAxisBinding{DeviceIndex=1,InputIndex=0,Kind=HotasModel.Button}},
   {"Dpad left",new FlightAxisBinding{DeviceIndex=0,InputIndex=0,Kind=HotasModel.Hat,Direction=HotasModel.HatLeft}},
   {"Right stick X",new FlightAxisBinding{DeviceIndex=1,InputIndex=1,Kind=HotasModel.Axis,Invert=true}}};
  HardwareInspector.Validate(profile);
  string rich=HotasTable(table,profile,slots);
  if(!rich.Contains("$Input: 129")||!rich.Contains("$Flags: HAT")||!rich.Contains("$Flags: AXIS_BTN, INVERTED")||
   !rich.Contains("$Flags: AXIS, RELATIVE, INVERTED")||!rich.Contains("CID_JOY1"))return false;
  var map=new JavaScriptSerializer().Deserialize<HotasMapFile>(HotasMapJson(profile));
  if(!HotasMapFile.Check(map)||map.Devices.Count!=2||map.Controls["Dpad left"].Direction!=HotasModel.HatLeft)return false;
  if(HotasModel.Overlaps(profile).Count!=0)return false;
  // Switching back to the gamepad and sharing: helpers that edit files text-wise or strip personal details.
  if(RemoveHotasBlock(rich)!=table)return false;
  string iniText="[Input]\r\nJoystick={\"guid\":\"AA\",\"id\":-1}\r\nJoystick1=x\r\n[Other]\r\nJoystick1=keep";
  if(GetIni(iniText,"Input","Joystick1")!="x"||GetIni(iniText,"Input","Joystick2")!=null)return false;
  string cleaned=RemoveIni(iniText,"Input","Joystick1");
  if(GetIni(cleaned,"Input","Joystick1")!=null||GetIni(cleaned,"Other","Joystick1")!="keep"||GetIni(cleaned,"Input","Joystick")==null)return false;
  stick.Serial="SN-1";
  profile.Layouts=new List<DeviceLayout>{new DeviceLayout{DeviceIndex=0,BackgroundImage=@"\\server\share\a.png",Widgets=new List<LayoutWidget>()}};
  var shared=ShareableCopy(profile);
  if(shared.Devices[0].Serial!=null||shared.Layouts[0].BackgroundImage!=null||stick.Serial!="SN-1"||profile.Layouts[0].BackgroundImage==null)return false;
  if(shared.Virtual["Dpad left"].Direction!=HotasModel.HatLeft)return false;
  stick.Serial=null;profile.Layouts=null;
  profile.Virtual["B"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=3,Kind=HotasModel.Button};
  if(HotasModel.Overlaps(profile).Count!=1)return false;
  profile.Virtual.Remove("B");
  // Precision axes: the game's own axis bindings are cleared, and Flight Control gets the axes with their settings.
  profile.FlightAxes["Pitch"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=1,Shape=new HotasAxisSettings{Deadzone=0.12,Curve=0.3}};
  profile.AxisMode="Precision";
  HardwareInspector.Validate(profile);
  string precisionTable=HotasTable(table,profile,slots);
  if(Regex.IsMatch(precisionTable,@"^\s*\$Flags: AXIS\s*(,|$)",RegexOptions.Multiline))return false;
  if(Regex.Matches(precisionTable,@"\$Bind: JOY_\w+_AXIS").Count!=5)return false;
  if(!Regex.IsMatch(precisionTable,@"\$Bind: JOY_PITCH_AXIS\s+\$Primary:\s+\$Controller: CID_NONE"))return false;
  if(!precisionTable.Contains("$Bind: AFTERBURNER"))return false;                     // game buttons are untouched
  if(HotasDroppedActions(precisionTable,profile).Count!=0)return false;
  var precisionMap=new JavaScriptSerializer().Deserialize<HotasMapFile>(HotasMapJson(profile));
  if(!HotasMapFile.Check(precisionMap)||precisionMap.Flight==null||precisionMap.Flight.Count!=2)return false;
  if(precisionMap.Flight["Pitch"].Shape.Deadzone!=0.12||precisionMap.Flight["Pitch"].Shape.Curve!=0.3||!precisionMap.Flight["Yaw"].Shape.Invert)return false;
  if(precisionMap.Flight["Yaw"].DeviceIndex<0||precisionMap.Flight["Yaw"].DeviceIndex>=precisionMap.Devices.Count)return false;
  profile.AxisMode="Game";
  if(HotasMapJson(profile).Contains("\"Flight\":{"))return false;                     // Game mode sends no flight axes
  profile.FlightAxes["Pitch"].Shape=new HotasAxisSettings{Deadzone=0.95};
  try{HardwareInspector.Validate(profile);return false;}catch(InvalidDataException){}
  profile.FlightAxes["Pitch"].Shape=null;
  profile.AxisMode="Sideways";
  try{HardwareInspector.Validate(profile);return false;}catch(InvalidDataException){}
  profile.AxisMode=null;
  profile.FlightAxes.Remove("Pitch");
  // Bad bindings are refused: native hats beyond the first, no direction, an axis for a button.
  profile.FlightButtons["Afterburner"].Direction=0;
  try{HardwareInspector.Validate(profile);return false;}catch(InvalidDataException){}
  profile.FlightButtons["Afterburner"].Direction=1;
  profile.FlightButtons["Target next ship"].Direction=HotasModel.HatUp|HotasModel.HatRight;
  try{HardwareInspector.Validate(profile);return false;}catch(InvalidDataException){}
  profile.FlightButtons["Target next ship"].Direction=HotasModel.HatUp;
  profile.Virtual["Left stick X"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=5,Kind=HotasModel.Axis};
  try{HardwareInspector.Validate(profile);return false;}catch(InvalidDataException){}
  profile.Virtual.Remove("Left stick X");
  if(HotasMapJson(new HardwareProfile{Devices=profile.Devices})!=null)return false;
  if(!HotasPad.SelfTest())return false;
  if(!InputCapture.SelfTest())return false;
  if(!AxisCalibrator.SelfTest())return false;
  var read=new JavaScriptSerializer().Deserialize<HardwareProfile>("{\"SchemaVersion\":1,\"FlightButtons\":{\"Fire primary\":{\"DeviceIndex\":0,\"AxisIndex\":7}}}");
  return read.FlightButtons["Fire primary"].InputIndex==7&&!new JavaScriptSerializer().Serialize(read).Contains("AxisIndex");
 }

 static bool DefaultPrimaryIsEmpty(string table,string action) {
  var defaults=Regex.Match(table,@"(?ims)^#ControlConfigOverride[^\r\n]*\r?\n(?:(?!^#End\b).)*");
  return Regex.IsMatch(defaults.Value,@"(?im)^\s*\$Bind:\s*"+Regex.Escape(action)+
   @"[ \t]*(?:;[^\r\n]*)?\r?\n\s*\$Primary:\s*\$Controller:\s*CID_NONE\b");
 }

 static void AppendHotasUnbound(StringBuilder text,string action) {
  text.AppendLine("    $Bind: "+action).AppendLine("        $Primary:").AppendLine("            $Controller: CID_NONE")
   .AppendLine("        $Secondary:").AppendLine("            $Controller: CID_NONE");
 }

 static void AppendHotasBinding(StringBuilder text,string action,string position,int slot,string flags,int input) {
  text.AppendLine("    $Bind: "+action).AppendLine("        $"+position+":")
   .AppendLine("            $Controller: CID_JOY"+slot)
   .AppendLine("            $Flags: "+flags)
   .AppendLine("            $Input: "+input.ToString(CultureInfo.InvariantCulture));
 }

 // A copy that is safe to hand to someone else or to load from someone else: no device serials (they identify a unit)
 // and no picture paths (a path from a shared file could point at a network location).
 internal static HardwareProfile ShareableCopy(HardwareProfile profile) {
  var serializer=new JavaScriptSerializer();
  var copy=serializer.Deserialize<HardwareProfile>(serializer.Serialize(profile));
  foreach(var device in copy.Devices)device.Serial=null;
  if(copy.Layouts!=null)foreach(var layout in copy.Layouts)layout.BackgroundImage=null;
  return copy;
 }

 // Dialogs go through here so the file logic can be tested without a person clicking.
 internal static bool? AutoConfirm;
 static DialogResult Say(IWin32Window owner,string text,string title,MessageBoxButtons buttons=MessageBoxButtons.OK) {
  if(AutoConfirm.HasValue)return AutoConfirm.Value?DialogResult.OK:DialogResult.Cancel;
  return MessageBox.Show(owner,text,title,buttons);
 }

 // What the game looked like before HOTAS was applied, so switching back to the gamepad is one step.
 sealed class HotasState {
  public bool Active {get;set;}
  public string PreviousPreset {get;set;}
  public Dictionary<string,string> PreviousJoystick {get;set;}   // null = unknown
  public List<string> AppliedSlots {get;set;}                     // device GUID per native joystick slot
 }
 static readonly string[] JoystickKeys={"Joystick","Joystick1","Joystick2","Joystick3"};

 string HotasStatePath(){return Path.Combine(prefs,"data","players","fotg-hotas-state.hotas");}

 HotasState LoadHotasState() {
  try{
   if(String.IsNullOrEmpty(prefs)||!File.Exists(HotasStatePath()))return null;
   return json.Deserialize<HotasState>(File.ReadAllText(HotasStatePath()));
  }catch(Exception){return null;}
 }

 internal static string GetIni(string text,string section,string key) {
  var lines=text.Replace("\r\n","\n").Split('\n');
  int start=Array.FindIndex(lines,l=>l.Trim().Equals("["+section+"]",StringComparison.OrdinalIgnoreCase));
  if(start<0)return null;
  for(int i=start+1;i<lines.Length&&!lines[i].TrimStart().StartsWith("[");i++){
   int eq=lines[i].IndexOf('=');
   if(eq>0&&lines[i].Substring(0,eq).Trim().Equals(key,StringComparison.OrdinalIgnoreCase))return lines[i].Substring(eq+1);
  }
  return null;
 }

 internal static string RemoveIni(string text,string section,string key) {
  var lines=new List<string>(text.Replace("\r\n","\n").Split('\n'));
  int start=lines.FindIndex(l=>l.Trim().Equals("["+section+"]",StringComparison.OrdinalIgnoreCase));
  if(start<0)return text;
  for(int i=start+1;i<lines.Count&&!lines[i].TrimStart().StartsWith("[");i++){
   int eq=lines[i].IndexOf('=');
   if(eq>0&&lines[i].Substring(0,eq).Trim().Equals(key,StringComparison.OrdinalIgnoreCase)){lines.RemoveAt(i);break;}
  }
  return String.Join("\r\n",lines);
 }

 internal static string RemoveHotasBlock(string table) {
  return Regex.Replace(table,@"(?ms)^"+Regex.Escape(HotasStart)+@"\r?\n.*?^"+Regex.Escape(HotasEnd)+@"\r?\n?","");
 }

 // The game's joystick slots still point at the devices HOTAS was applied with? (Applying gamepad settings changes them.)
 string HotasSlotNote() {
  try{
   var state=LoadHotasState();
   if(state==null||!state.Active||state.AppliedSlots==null||state.AppliedSlots.Count==0)return null;
   string iniPath=SettingsPath();
   if(!File.Exists(iniPath))return null;
   string text=File.ReadAllText(iniPath);
   for(int i=0;i<state.AppliedSlots.Count&&i<JoystickKeys.Length;i++){
    string raw=GetIni(text,"Input",JoystickKeys[i]);
    string guid=null;
    if(raw!=null){var d=json.Deserialize<Dictionary<string,object>>(raw);object g;if(d!=null&&d.TryGetValue("guid",out g))guid=g as string;}
    if(!String.Equals(guid,state.AppliedSlots[i],StringComparison.OrdinalIgnoreCase))
     return "The game's joystick slots no longer match your HOTAS (gamepad settings were applied since). Apply to pilot to re-sync.";
   }
  }catch(Exception){}
  return null;
 }

 // Switches the game back to the gamepad: removes the HOTAS bindings and the Flight Control map, puts the pilot's
 // preset and the game's joystick selection back as they were before HOTAS. The HOTAS profile is kept for later.
 void ReturnToGamepad() {
  EnsureClosed();
  string path=PilotPath(),ini=SettingsPath(),table=ControlTablePath(GameInstall.MyModCore(root.Text));
  string mapPath=Path.Combine(prefs,"data","players",HotasMapFile.FileName),statePath=HotasStatePath();
  if(!File.Exists(ini)||!File.Exists(table))throw new InvalidOperationException("Install Flight Control for FotG 2 first.");
  var state=LoadHotasState();
  string tableText=File.ReadAllText(table);
  if(!tableText.Contains(HotasStart)&&!File.Exists(mapPath)&&(state==null||!state.Active))
   throw new InvalidOperationException("HOTAS is not applied, so there is nothing to switch back.");
  var pilot=Read(path);
  if(Convert.ToInt32(pilot["version"])!=4)throw new InvalidOperationException("This needs an FSO version 4 pilot.");
  string newTable=RemoveHotasBlock(tableText);
  string preset=state!=null&&!String.IsNullOrEmpty(state.PreviousPreset)?state.PreviousPreset:
   (PresetSection(newTable,"FotG Classic Native")!=null?"FotG Classic Native":"default");
  // Never leave the pilot on a preset that no longer exists.
  if(preset!="default"&&PresetSection(newTable,preset)==null)preset="default";
  Section(Section(pilot,"sections"),"controls")["preset"]=preset;
  string settings=File.ReadAllText(ini);
  bool joystickKnown=state!=null&&state.PreviousJoystick!=null;
  if(joystickKnown)foreach(string key in JoystickKeys){
   string before;
   state.PreviousJoystick.TryGetValue(key,out before);
   settings=before==null?RemoveIni(settings,"Input",key):SetIni(settings,"Input",key,before);
  }
  if(Say(this,"Switch "+Path.GetFileNameWithoutExtension(path)+" back to the gamepad?\r\n\r\nThis removes the HOTAS game bindings and the Flight Control HOTAS map, and selects the pilot preset \""+preset+"\"."+
   (joystickKnown?" The game's joystick selection goes back to how it was.":"\r\n\r\nThe previous joystick selection was not recorded: afterwards open Flight response, check the gamepad slot and press APPLY WITH BACKUP.")+
   "\r\n\r\nYour HOTAS profile is kept; Apply to pilot brings HOTAS back.","Gamepad mode",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
  EnsureClosed();
  string backup=Path.Combine(prefs,"ControllerSetupBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-hotas-off");
  Directory.CreateDirectory(backup);
  var changes=new List<KeyValuePair<string,string>>{
   new KeyValuePair<string,string>(table,newTable),new KeyValuePair<string,string>(ini,settings),
   new KeyValuePair<string,string>(path,json.Serialize(pilot)),
   new KeyValuePair<string,string>(mapPath,null),new KeyValuePair<string,string>(statePath,null)};
  foreach(var change in changes)if(File.Exists(change.Key))File.Copy(change.Key,Path.Combine(backup,Path.GetFileName(change.Key)));
  var written=new List<string>();
  try{
   foreach(var change in changes){
    if(change.Value==null){if(File.Exists(change.Key))File.Delete(change.Key);}else Atomic(change.Key,change.Value);
    written.Add(change.Key);
   }
  }catch{
   foreach(string file in written){
    string saved=Path.Combine(backup,Path.GetFileName(file));
    if(File.Exists(saved))Atomic(file,File.ReadAllText(saved));
   }
   throw;
  }
  Say(this,"Back to gamepad mode (preset \""+preset+"\"). Launch the game through Flight Control and check the gamepad.\r\nBackup: "+backup,"Gamepad mode");
 }

 // Puts back everything the latest HOTAS apply changed: pilot, settings, control table and the Flight Control map.
 void RestoreHotas() {
  EnsureClosed();
  string parent=Path.Combine(prefs,"ControllerSetupBackups");
  string backup=Directory.Exists(parent)?Directory.GetDirectories(parent,"*-hotas").OrderByDescending(v=>v).FirstOrDefault():null;
  if(backup==null)throw new InvalidOperationException("There is no HOTAS backup yet. One is made each time you apply.");
  string path=PilotPath(),ini=SettingsPath(),table=ControlTablePath(GameInstall.MyModCore(root.Text));
  string mapPath=Path.Combine(prefs,"data","players",HotasMapFile.FileName);
  foreach(string file in new[]{path,ini,table})
   if(!File.Exists(Path.Combine(backup,Path.GetFileName(file))))
    throw new InvalidOperationException("The newest HOTAS backup was made for a different pilot or installation:"+Environment.NewLine+backup);
  if(Say(this,"Restore the pilot, settings and control table from the last HOTAS apply?"+Environment.NewLine+backup+
   Environment.NewLine+Environment.NewLine+"The current files are saved first.",
   "Restore HOTAS backup",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
  string undo=Path.Combine(parent,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-before-hotas-restore");
  Directory.CreateDirectory(undo);
  string statePath=HotasStatePath();
  foreach(string file in new[]{path,ini,table,mapPath,statePath})if(File.Exists(file))File.Copy(file,Path.Combine(undo,Path.GetFileName(file)));
  foreach(string file in new[]{path,ini,table})Atomic(file,File.ReadAllText(Path.Combine(backup,Path.GetFileName(file))));
  string savedMap=Path.Combine(backup,HotasMapFile.FileName);
  if(File.Exists(savedMap))Atomic(mapPath,File.ReadAllText(savedMap));else if(File.Exists(mapPath))File.Delete(mapPath);
  string savedState=Path.Combine(backup,Path.GetFileName(statePath));
  if(File.Exists(savedState))Atomic(statePath,File.ReadAllText(savedState));else if(File.Exists(statePath))File.Delete(statePath);
  LoadPilot();
  Say(this,"Restored from "+backup,"Restore HOTAS backup");
 }

 // Read-only: builds everything Apply would write for a saved profile and reports any exception.
 // Usage: --hotas-dryrun <game folder> <profile json> <output file>
 static int HotasDryRunCli(string[] args) {
  int at=Array.IndexOf(args,"--hotas-dryrun");
  if(at<0||at+3>=args.Length)return 1;
  var text=new StringBuilder();
  try{
   var serializer=new JavaScriptSerializer();
   var profile=serializer.Deserialize<HardwareProfile>(File.ReadAllText(args[at+2]));
   HardwareInspector.Validate(profile);
   text.AppendLine("validated; conflicts="+HotasModel.Conflicts(profile).Count);
   string table=ControlTablePath(GameInstall.MyModCore(args[at+1]));
   var axes=profile.FlightAxes??new Dictionary<string,FlightAxisBinding>();
   var buttons=profile.FlightButtons??new Dictionary<string,FlightAxisBinding>();
   var devices=axes.Values.Concat(buttons.Values).Select(b=>b.DeviceIndex).Distinct().OrderBy(i=>i).ToArray();
   var slots=new Dictionary<int,int>();
   for(int i=0;i<devices.Length;i++)slots.Add(devices[i],i);
   string result=HotasTable(File.ReadAllText(table),profile,slots);
   text.AppendLine("table ok, "+result.Length+" chars");
   text.AppendLine("dropped: "+String.Join(", ",HotasDroppedActions(File.ReadAllText(table),profile)));
   text.AppendLine("map: "+HotasMapJson(profile));
  }catch(Exception e){text.AppendLine(e.ToString());}
  File.WriteAllText(args[at+3],text.ToString());
  return 0;
 }

 void ApplyHotas(HardwareProfile profile,string[] guids) {
  EnsureClosed();
  string path=PilotPath();
  var pilot=Read(path);
  if(Convert.ToInt32(pilot["version"])!=4)
   throw new InvalidOperationException("HOTAS apply requires an FSO version 4 pilot.");
  string ini=SettingsPath();
  string table=ControlTablePath(GameInstall.MyModCore(root.Text));
  if(!File.Exists(ini)||!File.Exists(table))
   throw new InvalidOperationException("Install Flight Control for FotG 2 before applying HOTAS bindings.");
  var axes=profile.FlightAxes??new Dictionary<string,FlightAxisBinding>();
  var buttons=profile.FlightButtons??new Dictionary<string,FlightAxisBinding>();
  var features=profile.Virtual??new Dictionary<string,FlightAxisBinding>();
  if(axes.Count+buttons.Count+features.Count==0)throw new InvalidOperationException("Assign at least one control before applying.");
  bool precision=profile.AxisMode=="Precision"&&axes.Count>0;
  if((!precision&&axes.Values.Any(binding=>binding.InputIndex>=8))||
   buttons.Values.Any(binding=>HotasModel.KindOf(HotasModel.Group.NativeAction,binding)==HotasModel.Button&&binding.InputIndex>=128)||
   buttons.Values.Any(binding=>HotasModel.KindOf(HotasModel.Group.NativeAction,binding)==HotasModel.AxisButton&&binding.InputIndex>=8))
   throw new InvalidOperationException("FotG supports native axes 0–7 and buttons 0–127.");
  var conflicts=HotasModel.Conflicts(profile);
  if(conflicts.Count>0)
   throw new InvalidOperationException("Give each physical input one action before applying:\r\n"+String.Join("\r\n",conflicts));
  // Only the game's own bindings use its four joystick slots; Flight Control features read their devices directly.
  var devices=(precision?buttons.Values:axes.Values.Concat(buttons.Values)).Select(binding=>binding.DeviceIndex).Distinct().OrderBy(i=>i).ToArray();
  if(devices.Length>4)throw new InvalidOperationException("FotG provides four native joystick slots.");
  var slots=new Dictionary<int,int>();
  string settings=File.ReadAllText(ini);
  string originalSettings=settings;
  string originalTable=File.ReadAllText(table);
  bool nativeInUse=devices.Length>0||precision;   // precision still writes the (empty) axis bindings into the default preset
  // Remember how the game was set up before HOTAS, once, so "Gamepad mode" can put it back.
  var priorState=LoadHotasState();
  HotasState state;
  if(priorState!=null&&priorState.Active&&originalTable.Contains(HotasStart))state=priorState;
  else{
   state=new HotasState{Active=true};
   // If HOTAS bindings were already installed without a record, what came before is unknown; Gamepad mode then falls back.
   if(!originalTable.Contains(HotasStart)){
    object preset;
    var controlsSection=Section(Section(pilot,"sections"),"controls");
    state.PreviousPreset=controlsSection.TryGetValue("preset",out preset)?Convert.ToString(preset):null;
    state.PreviousJoystick=new Dictionary<string,string>();
    foreach(string key in JoystickKeys)state.PreviousJoystick[key]=GetIni(originalSettings,"Input",key);
   }
  }
  state.Active=true;
  state.AppliedSlots=new List<string>();
  for(int i=0;i<devices.Length;i++){
   slots.Add(devices[i],i);
   string device=json.Serialize(new{guid=guids[devices[i]].ToUpperInvariant(),id=-1});
   settings=SetIni(settings,"Input",JoystickKeys[i],device);
   state.AppliedSlots.Add(guids[devices[i]].ToUpperInvariant());
  }
  string newTable=HotasTable(originalTable,profile,slots);
  // Flight Control actions alone leave the gamepad's own axes, preset and joystick selection untouched.
  if(nativeInUse)Section(Section(pilot,"sections"),"controls")["preset"]="default";
  string pilotData=json.Serialize(pilot);
  string statePath=HotasStatePath();
  string stateData=json.Serialize(state);
  string mapPath=Path.Combine(prefs,"data","players",HotasMapFile.FileName);
  string mapData=HotasMapJson(profile);
  var dropped=HotasDroppedActions(File.ReadAllText(table),profile);
  string warning=dropped.Count==0?"":"\r\n\r\nThis replaces the installed HOTAS bindings. These are installed but not in this profile, and will be removed:\r\n"+
   String.Join(", ",dropped);
  string summary=(precision?buttons.Count:axes.Count+buttons.Count)+" game binding(s) on "+devices.Length+" joystick device(s), "+features.Count+" Flight Control binding(s)"+
   (precision?", and "+axes.Count+" flight axes processed by Flight Control (precision)":"");
  if(Say(this,"Apply the HOTAS preset to "+Path.GetFileNameWithoutExtension(path)+": "+summary+"?"+warning,"Apply HOTAS",
   MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
  EnsureClosed();
  string backup=Path.Combine(prefs,"ControllerSetupBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-hotas");
  Directory.CreateDirectory(backup);
  // A null value removes the file, so stale Flight Control bindings never outlive their profile.
  var changes=new List<KeyValuePair<string,string>>{
   new KeyValuePair<string,string>(table,newTable),new KeyValuePair<string,string>(ini,settings),
   new KeyValuePair<string,string>(path,pilotData)};
  if(mapData!=null||File.Exists(mapPath))changes.Add(new KeyValuePair<string,string>(mapPath,mapData));
  changes.Add(new KeyValuePair<string,string>(statePath,stateData));
  foreach(var change in changes)if(File.Exists(change.Key))File.Copy(change.Key,Path.Combine(backup,Path.GetFileName(change.Key)));
  var written=new List<string>();
  try {
   foreach(var change in changes){
    if(change.Value==null)File.Delete(change.Key);else Atomic(change.Key,change.Value);
    written.Add(change.Key);
   }
  }catch {
   foreach(string file in written){
    string saved=Path.Combine(backup,Path.GetFileName(file));
    if(File.Exists(saved))Atomic(file,File.ReadAllText(saved));else if(File.Exists(file))File.Delete(file);
   }
   throw;
  }
  Say(this,"Applied to "+Path.GetFileNameWithoutExtension(path)+
   ". Launch FotG through Flight Control MyMod and verify the controls.\r\nBackup: "+backup,"HOTAS preset");
 }
}
