using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Rules for a HOTAS profile: what each kind of binding may point at, which bindings clash, and how to describe them.
static class HotasModel {
 internal const string Button="Button",Axis="Axis",Hat="Hat",AxisButton="AxisButton";
 internal const int HatUp=1,HatRight=2,HatDown=4,HatLeft=8;

 // The three places a binding can live, and what each one accepts.
 internal enum Group{NativeAxis,NativeAction,Virtual}

 internal static string KindOf(Group group,FlightAxisBinding binding) {
  if(!String.IsNullOrEmpty(binding.Kind))return binding.Kind;
  return group==Group.NativeAxis?Axis:Button;
 }

 internal static bool Allowed(Group group,string kind) {
  switch(group){
   case Group.NativeAxis:return kind==Axis;
   case Group.NativeAction:return kind==Button||kind==Hat||kind==AxisButton;
   default:return kind==Button||kind==Hat||kind==Axis;
  }
 }

 // The SDL input list entry a binding refers to.
 internal static string InventoryKind(string bindingKind) {
  return bindingKind==Hat?"Hat":bindingKind==Button?"Button":"Axis";
 }

 static bool IsSingleDirection(int direction) {
  return direction==HatUp||direction==HatRight||direction==HatDown||direction==HatLeft;
 }

 internal static IEnumerable<KeyValuePair<Group,Dictionary<string,FlightAxisBinding>>> Groups(HardwareProfile profile) {
  yield return new KeyValuePair<Group,Dictionary<string,FlightAxisBinding>>(Group.NativeAxis,profile.FlightAxes);
  yield return new KeyValuePair<Group,Dictionary<string,FlightAxisBinding>>(Group.NativeAction,profile.FlightButtons);
  yield return new KeyValuePair<Group,Dictionary<string,FlightAxisBinding>>(Group.Virtual,profile.Virtual);
 }

 static bool KnownAction(Group group,string action) {
  switch(group){
   case Group.NativeAxis:return ControllerSetup.HotasAxisNames.Contains(action);
   case Group.NativeAction:return ControllerSetup.HotasButtonActions.ContainsKey(action);
   default:return HotasPad.IsVirtualControl(action);
  }
 }

 internal static void ValidateBindings(HardwareProfile value) {
  foreach(var pair in Groups(value)){
   if(pair.Value==null)continue;
   foreach(var entry in pair.Value){
    var b=entry.Value;
    if(b==null||!KnownAction(pair.Key,entry.Key)||b.DeviceIndex<0||b.DeviceIndex>=value.Devices.Count)
     throw new InvalidDataException("The profile contains an invalid assignment for "+entry.Key+".");
    string kind=KindOf(pair.Key,b);
    if(!Allowed(pair.Key,kind)||
     !value.Devices[b.DeviceIndex].Inputs.Any(i=>i.Kind==InventoryKind(kind)&&i.Index==b.InputIndex))
     throw new InvalidDataException("The profile contains an invalid "+kind.ToLowerInvariant()+" assignment for "+entry.Key+".");
    if(kind==Hat&&pair.Key==Group.NativeAction&&(b.InputIndex!=0||!IsSingleDirection(b.Direction)))
     throw new InvalidDataException("FotG binds only the first hat's four directions natively ("+entry.Key+").");
    if(kind==Hat&&pair.Key==Group.Virtual&&(b.Direction<1||b.Direction>15))
     throw new InvalidDataException("Choose a hat direction for "+entry.Key+".");
    if((kind==AxisButton||(kind==Axis&&pair.Key==Group.Virtual&&!HotasPad.IsAnalog(entry.Key)))&&b.Direction!=1&&b.Direction!=-1)
     throw new InvalidDataException("Choose which side of the axis triggers "+entry.Key+".");
   }
  }
 }

 // What makes two bindings the same physical signal. Native hats and axis sides are distinct from buttons.
 static string Signal(Group group,FlightAxisBinding b) {
  string kind=KindOf(group,b);
  string side=kind==Hat||kind==AxisButton?b.Direction.ToString():"";
  return b.DeviceIndex+":"+(kind==AxisButton?Axis:kind)+b.InputIndex+":"+side;
 }

 // Native FotG gives each physical input one job, so a shared input is an error.
 internal static List<string> Conflicts(HardwareProfile value) {
  var found=new List<string>();
  // Flight Control actions may share an input (a button can select in a wheel and fire a trigger at once), so only the game's own bindings clash.
  foreach(var group in new[]{Group.NativeAxis,Group.NativeAction}){
   var set=group==Group.NativeAxis?value.FlightAxes:value.FlightButtons;
   if(set==null)continue;
   foreach(var shared in set.GroupBy(e=>Signal(group,e.Value)).Where(g=>g.Count()>1)){
    var first=shared.First().Value;
    found.Add(String.Join(" and ",shared.Select(e=>e.Key).OrderBy(k=>k))+" share "+Describe(value,first,group));
   }
  }
  return found;
 }

 // One input that drives both a native action and a Flight Control feature is allowed but worth a warning.
 internal static List<string> Overlaps(HardwareProfile value) {
  var found=new List<string>();
  var native=new Dictionary<string,string>();
  foreach(var group in new[]{Group.NativeAxis,Group.NativeAction}){
   var set=group==Group.NativeAxis?value.FlightAxes:value.FlightButtons;
   if(set!=null)foreach(var entry in set)native[Signal(group,entry.Value)]=entry.Key;
  }
  if(value.Virtual!=null)foreach(var entry in value.Virtual){
   string other;
   if(native.TryGetValue(Signal(Group.Virtual,entry.Value),out other))
    found.Add(Describe(value,entry.Value,Group.Virtual)+" drives both "+other+" in the game and "+entry.Key+" in Flight Control. The game does not pause its own bindings while a wheel is open. For one button that acts in flight and in wheels, use Right trigger (fire) or a Flight Control button with a quick-press action instead of the game action");
  }
  return found;
 }

 internal static string DirectionName(int direction) {
  var names=new List<string>();
  if((direction&HatUp)!=0)names.Add("up");
  if((direction&HatRight)!=0)names.Add("right");
  if((direction&HatDown)!=0)names.Add("down");
  if((direction&HatLeft)!=0)names.Add("left");
  return String.Join("-",names);
 }

 // Two devices with the same name (a pair of identical sticks) are told apart as #1 and #2.
 internal static string TwinSuffix(HardwareProfile profile,int index) {
  if(profile==null||index<0||index>=profile.Devices.Count)return "";
  string name=profile.Devices[index].Name;
  int total=profile.Devices.Count(d=>d.Name==name);
  if(total<2)return "";
  int rank=0;
  for(int i=0;i<index;i++)if(profile.Devices[i].Name==name)rank++;
  return " #"+(rank+1);
 }

 internal static string ShortName(string name){return name.Length<=14?name:name.Substring(0,13)+"…";}

 internal static string Describe(HardwareProfile profile,FlightAxisBinding b,Group group) {
  string device=b.DeviceIndex>=0&&b.DeviceIndex<profile.Devices.Count?profile.Devices[b.DeviceIndex].Name:"?";
  string kind=KindOf(group,b);
  // Numbers are shown from 1, as in Windows' game controller test; the profile keeps the engine's zero-based index.
  string text=kind=="Hat"?"Hat "+(b.InputIndex+1)+" "+DirectionName(b.Direction):
   kind=="AxisButton"?"Axis "+(b.InputIndex+1)+(b.Direction<0?" low side":" high side"):
   kind=="Axis"?"Axis "+(b.InputIndex+1)+(b.Direction>0?" high side":b.Direction<0?" low side":"")+(b.Invert?" (inverted)":""):
   "Button "+(b.InputIndex+1);
  return ShortName(device)+TwinSuffix(profile,b.DeviceIndex)+" / "+text;
 }
}
