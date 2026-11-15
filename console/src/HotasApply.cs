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
  if(profile.Virtual==null||profile.Virtual.Count==0)return null;
  var devices=profile.Virtual.Values.Select(b=>b.DeviceIndex).Distinct().OrderBy(i=>i).ToList();
  var map=new HotasMapFile{Version=1,Devices=new List<HotasMapDevice>(),Controls=new Dictionary<string,HotasMapBinding>()};
  foreach(int index in devices)
   map.Devices.Add(new HotasMapDevice{Guid=profile.Devices[index].Guid.ToUpperInvariant(),Serial=profile.Devices[index].Serial});
  foreach(var entry in profile.Virtual){
   var b=entry.Value;
   string kind=HotasModel.KindOf(HotasModel.Group.Virtual,b);
   map.Controls[entry.Key]=new HotasMapBinding{DeviceIndex=devices.IndexOf(b.DeviceIndex),Kind=kind,Index=b.InputIndex,
    Direction=b.Direction,Invert=b.Invert};
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
  profile.Virtual["B"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=3,Kind=HotasModel.Button};
  if(HotasModel.Overlaps(profile).Count!=1)return false;
  profile.Virtual.Remove("B");
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
  var read=new JavaScriptSerializer().Deserialize<HardwareProfile>("{\"SchemaVersion\":1,\"FlightButtons\":{\"Fire primary\":{\"DeviceIndex\":0,\"AxisIndex\":7}}}");
  return read.FlightButtons["Fire primary"].InputIndex==7&&!new JavaScriptSerializer().Serialize(read).Contains("AxisIndex");
 }

 static bool DefaultPrimaryIsEmpty(string table,string action) {
  var defaults=Regex.Match(table,@"(?ims)^#ControlConfigOverride[^\r\n]*\r?\n(?:(?!^#End\b).)*");
  return Regex.IsMatch(defaults.Value,@"(?im)^\s*\$Bind:\s*"+Regex.Escape(action)+
   @"[ \t]*(?:;[^\r\n]*)?\r?\n\s*\$Primary:\s*\$Controller:\s*CID_NONE\b");
 }

 static void AppendHotasBinding(StringBuilder text,string action,string position,int slot,string flags,int input) {
  text.AppendLine("    $Bind: "+action).AppendLine("        $"+position+":")
   .AppendLine("            $Controller: CID_JOY"+slot)
   .AppendLine("            $Flags: "+flags)
   .AppendLine("            $Input: "+input.ToString(CultureInfo.InvariantCulture));
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
  if(axes.Values.Any(binding=>binding.InputIndex>=8)||
   buttons.Values.Any(binding=>HotasModel.KindOf(HotasModel.Group.NativeAction,binding)==HotasModel.Button&&binding.InputIndex>=128)||
   buttons.Values.Any(binding=>HotasModel.KindOf(HotasModel.Group.NativeAction,binding)==HotasModel.AxisButton&&binding.InputIndex>=8))
   throw new InvalidOperationException("FotG supports native axes 0–7 and buttons 0–127.");
  var conflicts=HotasModel.Conflicts(profile);
  if(conflicts.Count>0)
   throw new InvalidOperationException("Give each physical input one action before applying:\r\n"+String.Join("\r\n",conflicts));
  // Only the game's own bindings use its four joystick slots; Flight Control features read their devices directly.
  var devices=axes.Values.Concat(buttons.Values).Select(binding=>binding.DeviceIndex).Distinct().OrderBy(i=>i).ToArray();
  if(devices.Length>4)throw new InvalidOperationException("FotG provides four native joystick slots.");
  var slots=new Dictionary<int,int>();
  string settings=File.ReadAllText(ini);
  for(int i=0;i<devices.Length;i++){
   slots.Add(devices[i],i);
   string device=json.Serialize(new{guid=guids[devices[i]].ToUpperInvariant(),id=-1});
   settings=SetIni(settings,"Input",i==0?"Joystick":"Joystick"+i,device);
  }
  string newTable=HotasTable(File.ReadAllText(table),profile,slots);
  Section(Section(pilot,"sections"),"controls")["preset"]="default";
  string pilotData=json.Serialize(pilot);
  string mapPath=Path.Combine(prefs,"data","players",HotasMapFile.FileName);
  string mapData=HotasMapJson(profile);
  var dropped=HotasDroppedActions(File.ReadAllText(table),profile);
  string warning=dropped.Count==0?"":"\r\n\r\nThis replaces the installed HOTAS bindings. These are installed but not in this profile, and will be removed:\r\n"+
   String.Join(", ",dropped);
  string summary=axes.Count+buttons.Count+" game binding(s) on "+devices.Length+" joystick device(s), "+features.Count+" Flight Control binding(s)";
  if(MessageBox.Show(this,"Apply the HOTAS preset to "+Path.GetFileNameWithoutExtension(path)+": "+summary+"?"+warning,"Apply HOTAS",
   MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
  EnsureClosed();
  string backup=Path.Combine(prefs,"ControllerSetupBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-hotas");
  Directory.CreateDirectory(backup);
  // A null value removes the file, so stale Flight Control bindings never outlive their profile.
  var changes=new List<KeyValuePair<string,string>>{
   new KeyValuePair<string,string>(table,newTable),new KeyValuePair<string,string>(ini,settings),
   new KeyValuePair<string,string>(path,pilotData)};
  if(mapData!=null||File.Exists(mapPath))changes.Add(new KeyValuePair<string,string>(mapPath,mapData));
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
  MessageBox.Show(this,"Applied to "+Path.GetFileNameWithoutExtension(path)+
   ". Launch FotG through Flight Control MyMod and verify the controls.\r\nBackup: "+backup,"HOTAS preset");
 }
}
