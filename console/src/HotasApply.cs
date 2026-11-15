using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

partial class ControllerSetup {
 internal static readonly Dictionary<string,string> HotasButtonActions=new Dictionary<string,string>{
  {"Fire primary","FIRE_PRIMARY"},
  {"Fire secondary","FIRE_SECONDARY"},
  {"Cycle primary weapon","CYCLE_NEXT_PRIMARY"},
  {"Launch countermeasure","LAUNCH_COUNTERMEASURE"},
  {"Afterburner","AFTERBURNER"},
  {"Match target speed","MATCH_TARGET_SPEED"},
  {"Target next ship","TARGET_NEXT"},
  {"Target closest hostile","TARGET_NEXT_CLOSEST_HOSTILE"},
  {"Target ship in reticle","TARGET_SHIP_IN_RETICLE"},
  {"Target next subsystem","TARGET_NEXT_SUBOBJECT"}
 };

 static readonly Dictionary<string,string> HotasAxisActions=new Dictionary<string,string>{
  {"Pitch","JOY_PITCH_AXIS"},{"Yaw","JOY_HEADING_AXIS"},
  {"Roll","JOY_BANK_AXIS"},{"Absolute throttle","JOY_ABS_THROTTLE_AXIS"}
 };

 const string HotasStart=";; Flight Control HOTAS BEGIN";
 const string HotasEnd=";; Flight Control HOTAS END";

 internal static string HotasTable(string table,HardwareProfile profile,Dictionary<int,int> slots) {
  var text=new StringBuilder();
  text.AppendLine(HotasStart);
  if(profile.FlightAxes!=null)foreach(var entry in profile.FlightAxes){
   bool absolute=entry.Key=="Absolute throttle";
   var binding=entry.Value;
   AppendHotasBinding(text,HotasAxisActions[entry.Key],"Primary",slots[binding.DeviceIndex],
    absolute?"AXIS":"AXIS, RELATIVE",binding.AxisIndex);
   if(absolute)text.AppendLine("    $Bind: JOY_REL_THROTTLE_AXIS")
    .AppendLine("        $Primary:").AppendLine("            $Controller: CID_NONE")
    .AppendLine("        $Secondary:").AppendLine("            $Controller: CID_NONE");
  }
  if(profile.FlightButtons!=null)foreach(var entry in profile.FlightButtons){
   var binding=entry.Value;
   string action=HotasButtonActions[entry.Key];
   AppendHotasBinding(text,action,DefaultPrimaryIsEmpty(table,action)?"Primary":"Secondary",
    slots[binding.DeviceIndex],"NONE",binding.AxisIndex);
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
  if(axes.Count+buttons.Count==0)throw new InvalidOperationException("Capture at least one flight action before applying.");
  if(axes.Values.Any(binding=>binding.AxisIndex>=8)||buttons.Values.Any(binding=>binding.AxisIndex>=128))
   throw new InvalidOperationException("FotG supports native axes 0–7 and buttons 0–127.");
  if(buttons.Values.GroupBy(binding=>new{binding.DeviceIndex,binding.AxisIndex}).Any(group=>group.Count()>1))
   throw new InvalidOperationException("Assign each physical button to just one action before applying.");
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
  if(MessageBox.Show(this,"Apply the HOTAS preset to "+Path.GetFileNameWithoutExtension(path)+
   " and select "+devices.Length+" joystick device(s) in FotG 2?","Apply HOTAS",
   MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
  EnsureClosed();
  string backup=Path.Combine(prefs,"ControllerSetupBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-hotas");
  Directory.CreateDirectory(backup);
  var changes=new Dictionary<string,string>{{table,newTable},{ini,settings},{path,pilotData}};
  foreach(string file in changes.Keys)File.Copy(file,Path.Combine(backup,Path.GetFileName(file)));
  var written=new List<string>();
  try {
   foreach(var change in changes){
    Atomic(change.Key,change.Value);
    written.Add(change.Key);
   }
  }catch {
   foreach(string file in written)Atomic(file,File.ReadAllText(Path.Combine(backup,Path.GetFileName(file))));
   throw;
  }
  MessageBox.Show(this,"Applied to "+Path.GetFileNameWithoutExtension(path)+
   ". Launch FotG through Flight Control MyMod and verify the controls.\r\nBackup: "+backup,"HOTAS preset");
 }
}
