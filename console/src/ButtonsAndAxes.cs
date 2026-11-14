using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// Button actions (quick press and long press) and the Custom flight-axis preset.
// The ids here must match BUTTON_ACTIONS in zzz_fotg_wheel-sct.lua.
partial class ControllerSetup {
 sealed class ButtonAction {
  public readonly string Id,Caption;public readonly bool Held;
  public ButtonAction(string id,string caption,bool held=false){Id=id;Caption=caption;Held=held;}
  public override string ToString(){return Caption;}
 }
 static readonly ButtonAction[] ButtonActions={
  new ButtonAction("none","Nothing"),
  new ButtonAction("target_reticle","Target ship in reticle"),
  new ButtonAction("target_attacker","Target closest attacker"),
  new ButtonAction("target_next","Next target in selected class"),
  new ButtonAction("target_nearest_enemy","Nearest enemy"),
  new ButtonAction("target_nearest_friendly","Nearest friendly"),
  new ButtonAction("target_targets_target","Target's target"),
  new ButtonAction("target_newest","Newest ship"),
  new ButtonAction("target_next_bomb","Next bomb"),
  new ButtonAction("target_clear","Clear target"),
  new ButtonAction("target_subsystem_toggle","Subsystem / hull"),
  new ButtonAction("target_subsystem_reticle","Subsystem in sight"),
  new ButtonAction("toggle_targeting_computer","Toggle targeting computer (Z)"),
  new ButtonAction("target_next_subsystem","Next subsystem"),
  new ButtonAction("toggle_mouse_mode","Toggle mouse movement (N)"),
  new ButtonAction("link_guns","Link guns / primary firing sequence (E)"),
  new ButtonAction("link_torpedoes","Link torpedo banks / missile salvo count (Q)"),
  new ButtonAction("target_live_turret","Next live turret"),
  new ButtonAction("match_speed","Match target speed"),
  new ButtonAction("cycle_secondary","Cycle secondary bank"),
  new ButtonAction("cycle_primary","Cycle primary weapon"),
  new ButtonAction("cycle_ship_mode","Ship mode (gun control, burst, S-foils, targeting)"),
  new ButtonAction("third_gun","Third gun, B-wing / T-Blade (while held)",true),
  new ButtonAction("afterburner","Afterburner (while held)",true),
  new ButtonAction("countermeasure","Launch countermeasure"),
  new ButtonAction("shields_max","Maximum shields"),
  new ButtonAction("weapons_max","Maximum weapons"),
  new ButtonAction("engines_max","Maximum engines"),
  new ButtonAction("balance_energy","Balance energy"),
  new ButtonAction("shield_equalize","Equalize shields"),
  new ButtonAction("transfer_shields","Transfer energy to shields"),
  new ButtonAction("transfer_weapons","Transfer energy to lasers"),
  new ButtonAction("increase_weapons","Weapon energy +"),
  new ButtonAction("decrease_weapons","Weapon energy -"),
  new ButtonAction("increase_shields","Shield energy +"),
  new ButtonAction("decrease_shields","Shield energy -"),
  new ButtonAction("increase_engines","Engine energy +"),
  new ButtonAction("decrease_engines","Engine energy -"),
  new ButtonAction("max_throttle","Full throttle"),
  new ButtonAction("zero_throttle","Zero throttle"),
  new ButtonAction("view_chase","Chase view"),
  new ButtonAction("view_external","External view"),
  new ButtonAction("toggle_hud","Toggle HUD"),
  new ButtonAction("radar_range","Cycle radar range"),
  new ButtonAction("nav_cycle","Cycle nav point"),
  new ButtonAction("auto_pilot","Toggle autopilot"),
  new ButtonAction("boarding_task","Cycle boarding party task")
 };
 static readonly string[] TapButtons={"A","B","X","Y","LB","RB","L3","R3"};
 // The 1.0 quick-press layout.
 static readonly string[] DefaultTapActions={"target_reticle","target_attacker","target_next","third_gun","cycle_secondary","cycle_primary","match_speed","target_next_subsystem"};
 readonly string[] tapActions=(string[])DefaultTapActions.Clone();
 bool holdEnabled;
 static ButtonAction FindAction(string id){return Array.Find(ButtonActions,a=>a.Id==id);}
 static bool IsTapAction(string id){return FindAction(id)!=null;}
 static bool IsHoldAction(string id){var a=FindAction(id);return a!=null&&!a.Held;}

 // --- Custom flight axes ---------------------------------------------------------------
 static readonly string[] AxisFunctions={"yaw","pitch","roll","throttle"};
 static readonly string[] AxisFunctionCaptions={"Yaw (turn)","Pitch (nose up/down)","Roll","Throttle (relative)"};
 static readonly string[] AxisSources={"left_x","left_y","right_x","right_y","none"};
 static readonly string[] AxisSourceCaptions={"Left stick, left/right","Left stick, up/down","Right stick, left/right","Right stick, up/down","Not used"};
 // FSO's SDL3 gamepad axes: left X/Y, right X/Y (the right stick reads as Z and RX).
 static readonly Dictionary<string,string> Sdl3Axes=new Dictionary<string,string>{{"left_x","X_AXIS"},{"left_y","Y_AXIS"},{"right_x","Z_AXIS"},{"right_y","RX_AXIS"}};
 // Setup records the engine's stick numbering in engine-axes.txt as "sdl2 X_AXIS,Y_AXIS,RX_AXIS,RY_AXIS"
 // (left X, left Y, right X, right Y). An SDL2 engine reads raw joystick axes, so its right stick differs.
 static Dictionary<string,string> StickAxes(string modFolder){
  try{
   string record=Path.Combine(modFolder,"engine-axes.txt");
   if(File.Exists(record)){
    var m=Regex.Match(File.ReadAllText(record).Trim(),@"^(sdl2|sdl3) (?<map>[A-Z_]+,[A-Z_]+,[A-Z_]+,[A-Z_]+)$");
    if(m.Success){var a=m.Groups["map"].Value.Split(',');return new Dictionary<string,string>{{"left_x",a[0]},{"left_y",a[1]},{"right_x",a[2]},{"right_y",a[3]}};}
   }
  }catch(IOException){}
  return Sdl3Axes;
 }
 static readonly string[] FsoAxisActions={"JOY_HEADING_AXIS","JOY_PITCH_AXIS","JOY_BANK_AXIS","JOY_REL_THROTTLE_AXIS"};
 // Classic direct: right stick yaw/pitch, left stick roll/throttle.
 static readonly string[] DefaultCustomAxes={"right_x","right_y","left_x","left_y"};
 readonly string[] customAxes=(string[])DefaultCustomAxes.Clone();
 readonly bool[] customInvert=new bool[4];
 const string CustomPresetName="FotG Custom";

 static void ValidateAxes(string[] axes,bool[] invert){
  if(axes==null||axes.Length!=4||invert==null||invert.Length!=4)throw new Exception("Custom axes are incomplete.");
  if(axes.Any(a=>Array.IndexOf(AxisSources,a)<0))throw new Exception("Custom axes use an unknown stick axis.");
  var used=axes.Where(a=>a!="none").ToArray();
  if(used.Distinct().Count()!=used.Length)throw new Exception("Each stick axis can drive only one flight axis.");
 }
 // One $Bind block per flight axis. "Invert" is relative to the natural direction: for
 // throttle on an up/down axis, pushing up already needs FSO's INVERTED flag.
 static string CustomAxisBinds(string[] axes,bool[] invert,Dictionary<string,string> stick=null){
  stick=stick??Sdl3Axes;
  ValidateAxes(axes,invert);
  var b=new StringBuilder();
  for(int i=0;i<4;i++){
   b.Append("    $Bind: ").Append(FsoAxisActions[i]).Append("\r\n        $Primary:\r\n");
   if(axes[i]=="none")b.Append("            $Controller: CID_NONE\r\n");
   else{
    bool flag=invert[i]^(AxisFunctions[i]=="throttle"&&axes[i].EndsWith("_y"));
    // FSO treats these four as relative analog items and warns at startup unless RELATIVE is listed.
    b.Append("            $Controller: CID_JOY0\r\n            $Flags: AXIS").Append(", RELATIVE").Append(flag?", INVERTED":"").Append("\r\n            $Input: ").Append(stick[axes[i]]).Append("\r\n");
   }
   b.Append("        $Secondary:\r\n            $Controller: CID_NONE\r\n");
  }
  b.Append("    $Bind: JOY_ABS_THROTTLE_AXIS\r\n        $Primary:\r\n            $Controller: CID_NONE\r\n        $Secondary:\r\n            $Controller: CID_NONE\r\n");
  return b.ToString();
 }
 static readonly string[] AllAxisActions={"JOY_HEADING_AXIS","JOY_PITCH_AXIS","JOY_BANK_AXIS","JOY_REL_THROTTLE_AXIS","JOY_ABS_THROTTLE_AXIS"};
 static string PresetSection(string table,string name){
  var m=Regex.Match(table,@"(?ms)^#ControlConfigOverride[^\r\n]*\r?\n(?:(?!^#End)(?!^#ControlConfigOverride).)*?^\$Name:[ \t]*"+Regex.Escape(name)+@"[ \t]*(?:;[^\r\n]*)?\r?\n.*?^#End[^\r\n]*\r?\n?");
  return m.Success?m.Value:null;
 }
 // Copies the generated Classic Native preset (its buttons and options), swaps in the
 // custom axis bindings, and replaces any earlier FotG Custom preset in the table.
 static string WithCustomPreset(string table,string[] axes,bool[] invert,Dictionary<string,string> stick=null,string presetName=CustomPresetName){
  string baseline=PresetSection(table,"FotG Classic Native")??PresetSection(table,"default");
  if(baseline==null)throw new Exception("The wheel's control table has no baseline preset. Run Install / Repair in Setup first.");
  string custom=Regex.Replace(baseline,@"(?m)^\$Name:[^\r\n]*","$$Name: "+presetName);
  foreach(string axis in AllAxisActions)
   custom=Regex.Replace(custom,@"(?ms)^[ \t]*\$Bind:\s*"+axis+@"\b.*?(?=^\s*\$Bind:|^#End)","");
  custom=Regex.Replace(custom,@"(?m)^#End\b",CustomAxisBinds(axes,invert,stick).Replace("$","$$")+"#End");
  string existing=PresetSection(table,presetName);
  if(existing!=null)table=table.Replace(existing,"");
  return DefaultSectionLast(table.TrimEnd()+"\r\n\r\n"+custom.TrimEnd()+"\r\n");
 }
 // Same rule as Presets.DefaultSectionLast in Setup: the engine renames an earlier preset when a later one has
 // identical bindings, so the "default" section (a copy of Classic Native in FotG 2) must stay last.
 static string DefaultSectionLast(string table){
  string d=PresetSection(table,"default");
  if(d==null||table.TrimEnd().EndsWith(d.TrimEnd()))return table;
  string rest=Regex.Replace(table.Replace(d,""),@"(\r?\n){3,}","\r\n\r\n").Trim();
  return rest+"\r\n\r\n"+d.TrimEnd()+"\r\n";
 }
 static string[] customAxesOrDefault(string[] axes){return axes!=null&&axes.Length==4?axes:DefaultCustomAxes;}
 static bool[] invertOrDefault(bool[] invert){return invert!=null&&invert.Length==4?invert:new bool[4];}
 static readonly string[] AxesModes={"classic","classic_inverted","official","custom"};
 static string AxesMode(int index){return index>=0&&index<AxesModes.Length?AxesModes[index]:"classic";}
 static int AxesIndex(string mode){int i=Array.IndexOf(AxesModes,mode);return i<0?0:i;}
 static string AxesHint(int index){
  switch(index){
   case 1:return "Classic, with pitch reversed.";
   case 2:return "Uses the game's own bindings: rebind axes in the game's Controls menu.";
   case 3:return "Your own stick axes. Edit axes, then Save Wheel.";
   default:return "Right stick steers; left stick rolls and changes throttle.";
  }
 }
 static string ControlTablePath(string modFolder){return Path.Combine(modFolder,"data","tables","Controlconfigdefaults.tbl");}
 static void WriteCustomPreset(string modFolder,string[] axes,bool[] invert,string presetName=CustomPresetName){
  string path=ControlTablePath(modFolder);
  if(!File.Exists(path))throw new Exception("The wheel's control table was not found. Run Install / Repair in Setup first.");
  string updated=WithCustomPreset(File.ReadAllText(path),axes,invert,StickAxes(modFolder),presetName);
  string folder=Path.Combine(modFolder,"ConfigBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-controls");
  Directory.CreateDirectory(folder);File.Copy(path,Path.Combine(folder,"Controlconfigdefaults.tbl"));
  Atomic(path,updated);
 }
 // Lua lines for the saved axis map, read back by the console and by --apply-custom-axes.
 static string RenderCustomAxes(string[] axes,bool[] invert){
  var b=new StringBuilder("  custom_axes = { ");
  for(int i=0;i<4;i++)b.Append(AxisFunctions[i]+"=\""+axes[i]+"\", ");
  for(int i=0;i<4;i++){b.Append("invert_"+AxisFunctions[i]+"="+(invert[i]?"true":"false"));if(i<3)b.Append(", ");}
  return b.Append(" },").ToString();
 }
 static bool ParseCustomAxes(string source,string[] axes,bool[] invert){
  var block=Regex.Match(source,@"\bcustom_axes\s*=\s*\{(?<body>[^}]*)\}");
  if(!block.Success)return false;
  var parsedAxes=new string[4];var parsedInvert=new bool[4];
  for(int i=0;i<4;i++){
   var a=Regex.Match(block.Groups["body"].Value,@"\b"+AxisFunctions[i]+@"\s*=\s*""(?<v>[a-z_]+)""");
   parsedAxes[i]=a.Success?a.Groups["v"].Value:DefaultCustomAxes[i];
   parsedInvert[i]=Regex.IsMatch(block.Groups["body"].Value,@"\binvert_"+AxisFunctions[i]+@"\s*=\s*true\b");
  }
  try{ValidateAxes(parsedAxes,parsedInvert);}catch{return false;}
  Array.Copy(parsedAxes,axes,4);Array.Copy(parsedInvert,invert,4);return true;
 }
 static string RenderTapBindings(string[] taps){
  var b=new StringBuilder("  tap_bindings = { ");
  for(int i=0;i<TapButtons.Length;i++){
   string id=taps!=null&&i<taps.Length&&IsTapAction(taps[i])?taps[i]:DefaultTapActions[i];
   b.Append(TapButtons[i]+"=\""+id+"\"");if(i<TapButtons.Length-1)b.Append(", ");
  }
  return b.Append(" },").ToString();
 }
 static void ParseTapBindings(string source,string[] taps){
  var block=Regex.Match(source,@"\btap_bindings\s*=\s*\{(?<body>[^}]*)\}");
  for(int i=0;i<TapButtons.Length;i++){
   var m=block.Success?Regex.Match(block.Groups["body"].Value,@"\b"+TapButtons[i]+@"\s*=\s*""(?<id>[a-z_0-9]+)"""):Match.Empty;
   taps[i]=m.Success&&IsTapAction(m.Groups["id"].Value)?m.Groups["id"].Value:DefaultTapActions[i];
  }
 }

 // --- Dialogs ------------------------------------------------------------------------------
 Action refreshButtonEditor;
 Control BuildButtonEditor(){
   var layout=Grid(3,15);layout.Padding=new Padding(16);layout.Dock=DockStyle.Top;layout.Height=510;
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,70));
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
   Rows(layout,50,30,34,34,34,34,34,34,34,34,38,35,35,35,52);
   var intro=LabelText("IN FLIGHT, WITH NO WHEEL OPEN. A confirms and B cancels while a wheel is open, whatever is set here. The D-pad, Menu, sticks and triggers keep their roles.",9,Cyan);
   layout.Controls.Add(intro,0,0);layout.SetColumnSpan(intro,3);
   layout.Controls.Add(LabelText("BUTTON",9,Ink),0,1);layout.Controls.Add(LabelText("QUICK PRESS",9,Ink),1,1);layout.Controls.Add(LabelText("LONG PRESS",9,Ink),2,1);
   var taps=new ComboBox[TapButtons.Length];var holds=new ComboBox[TapButtons.Length];
   var useHolds=new CheckBox{Text="Use long presses",Checked=holdEnabled,Dock=DockStyle.Fill,ForeColor=Ink,BackColor=Night};
   Action refresh=delegate{
    for(int i=0;i<TapButtons.Length;i++){
     if(holds[i]==null)continue;
     var tap=taps[i].SelectedItem as ButtonAction;
     bool canHold=useHolds.Checked&&tap!=null&&!tap.Held;
     holds[i].Enabled=canHold;
    }
   };
   for(int i=0;i<TapButtons.Length;i++){
    layout.Controls.Add(LabelText(TapButtons[i],11,Ink),0,i+2);
    var tap=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Surface,ForeColor=Ink,MaxDropDownItems=20};
    foreach(var a in ButtonActions)tap.Items.Add(a);
    int t=Array.FindIndex(ButtonActions,a=>a.Id==tapActions[i]);tap.SelectedIndex=t<0?Array.FindIndex(ButtonActions,a=>a.Id==DefaultTapActions[i]):t;
    taps[i]=tap;layout.Controls.Add(tap,1,i+2);
    int hi=Array.IndexOf(HoldButtons,TapButtons[i]);
    if(hi<0){layout.Controls.Add(LabelText("No long press on Y",9,Ink),2,i+2);continue;}
    var hold=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Surface,ForeColor=Ink,MaxDropDownItems=20};
    foreach(var a in ButtonActions.Where(a=>!a.Held))hold.Items.Add(a);
    var items=hold.Items.Cast<ButtonAction>().ToList();int h=items.FindIndex(a=>a.Id==holdActions[hi]);hold.SelectedIndex=h<0?0:h;
    holds[i]=hold;layout.Controls.Add(hold,2,i+2);
    tap.SelectedIndexChanged+=delegate{refresh();};
   }
   var timing=Grid(3,1);timing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));timing.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));timing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,66));
   timing.Controls.Add(useHolds,0,0);
   var duration=new NumericUpDown{Dock=DockStyle.Fill,Minimum=250,Maximum=1000,Increment=50,Value=holdLayerMs,BackColor=Surface,ForeColor=Ink};
   timing.Controls.Add(duration,1,0);timing.Controls.Add(LabelText("ms to count as a long press. A button with a long press runs its quick action on release.",9,Ink),2,0);
   layout.Controls.Add(timing,0,10);layout.SetColumnSpan(timing,3);
   useHolds.CheckedChanged+=delegate{refresh();};
   var r3Default=new CheckBox{Text="Hold R3 for mouse movement (N); tap R3 for next subsystem. Custom R3 long press overrides this.",Checked=r3MouseHoldEnabled,Dock=DockStyle.Fill,ForeColor=Ink,BackColor=Night};
   layout.Controls.Add(r3Default,0,11);layout.SetColumnSpan(r3Default,3);
   var rbDefault=new CheckBox{Text="Tap RB to cycle primary bank; hold RB to change ship mode. Custom RB long press overrides this.",Checked=rbModeHoldEnabled,Dock=DockStyle.Fill,ForeColor=Ink,BackColor=Night};
   layout.Controls.Add(rbDefault,0,12);layout.SetColumnSpan(rbDefault,3);
   var l3Default=new CheckBox{Text="Hold L3: link guns (E)",Checked=l3GunHoldEnabled,Dock=DockStyle.Fill,ForeColor=Ink,BackColor=Night};
   var lbDefault=new CheckBox{Text="Hold LB: link torpedoes (Q)",Checked=lbTorpedoHoldEnabled,Dock=DockStyle.Fill,ForeColor=Ink,BackColor=Night};
   layout.Controls.Add(l3Default,1,13);layout.Controls.Add(lbDefault,2,13);
   Action refreshDuration=delegate{duration.Enabled=useHolds.Checked||r3Default.Checked||rbDefault.Checked||l3Default.Checked||lbDefault.Checked;};
   r3Default.CheckedChanged+=delegate{refreshDuration();};rbDefault.CheckedChanged+=delegate{refreshDuration();};l3Default.CheckedChanged+=delegate{refreshDuration();};lbDefault.CheckedChanged+=delegate{refreshDuration();};useHolds.CheckedChanged+=delegate{refreshDuration();};refreshDuration();
   var controls=Grid(3,1);for(int c=0;c<3;c++)controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.3f));
   controls.Controls.Add(ActionButton("Reset to defaults",delegate{
    for(int i=0;i<TapButtons.Length;i++){taps[i].SelectedIndex=Array.FindIndex(ButtonActions,a=>a.Id==DefaultTapActions[i]);if(holds[i]!=null)holds[i].SelectedIndex=0;}
    useHolds.Checked=false;r3Default.Checked=true;rbDefault.Checked=true;l3Default.Checked=true;lbDefault.Checked=true;duration.Value=450;}),0,0);
   controls.Controls.Add(ActionButton("Reserve wheel buttons",delegate{ReserveWheelButtons();}),1,0);
   controls.Controls.Add(ActionButton("Save button actions",delegate{SaveMacroConfig();}),2,0);
   layout.Controls.Add(controls,0,14);layout.SetColumnSpan(controls,3);
   bool loading=false;
   Action capture=delegate{
    if(loading)return;
    for(int i=0;i<TapButtons.Length;i++){
     var tap=(ButtonAction)taps[i].SelectedItem;tapActions[i]=tap.Id;
     int hi=Array.IndexOf(HoldButtons,TapButtons[i]);
     if(hi>=0)holdActions[hi]=tap.Held?"none":((ButtonAction)holds[i].SelectedItem).Id;
    }
    holdEnabled=useHolds.Checked;r3MouseHoldEnabled=r3Default.Checked;rbModeHoldEnabled=rbDefault.Checked;
    l3GunHoldEnabled=l3Default.Checked;lbTorpedoHoldEnabled=lbDefault.Checked;holdLayerMs=(int)duration.Value;
   };
   foreach(var combo in taps.Concat(holds).Where(c=>c!=null))combo.SelectedIndexChanged+=delegate{capture();};
   foreach(var check in new[]{useHolds,r3Default,rbDefault,l3Default,lbDefault})check.CheckedChanged+=delegate{capture();};
   duration.ValueChanged+=delegate{capture();};
   refreshButtonEditor=delegate{
    loading=true;
    try{
     for(int i=0;i<TapButtons.Length;i++){
      taps[i].SelectedIndex=Array.FindIndex(ButtonActions,a=>a.Id==tapActions[i]);
      if(holds[i]!=null){
       int hi=Array.IndexOf(HoldButtons,TapButtons[i]);
       holds[i].SelectedIndex=Math.Max(0,holds[i].Items.Cast<ButtonAction>().ToList().FindIndex(a=>a.Id==holdActions[hi]));
      }
     }
     useHolds.Checked=holdEnabled;r3Default.Checked=r3MouseHoldEnabled;rbDefault.Checked=rbModeHoldEnabled;
     l3Default.Checked=l3GunHoldEnabled;lbDefault.Checked=lbTorpedoHoldEnabled;duration.Value=holdLayerMs;
     refresh();refreshDuration();
    }finally{loading=false;}
   };
   refreshButtonEditor();
   var page=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Surface};
   page.Controls.Add(layout);
   return page;
 }
 void EditCustomAxes(){
  using(var dialog=new Form{Text="Custom flight axes",FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false,ClientSize=new Size(640,360),AutoScaleMode=AutoScaleMode.Dpi,BackColor=Night,ForeColor=Ink,Font=new Font("Segoe UI",10)}){
   var layout=Grid(3,8);layout.Padding=new Padding(16);
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));
   Rows(layout,68,28,36,36,36,36,44,52);
   var intro=LabelText("Experimental. Axis behavior depends on the game and controller. Configure your gamepad in Flight Control. Either stick selects wheel choices.",9,Cyan);
   layout.Controls.Add(intro,0,0);layout.SetColumnSpan(intro,3);
   layout.Controls.Add(LabelText("FLIGHT AXIS",9,Ink),0,1);layout.Controls.Add(LabelText("STICK AXIS",9,Ink),1,1);layout.Controls.Add(LabelText("INVERT",9,Ink),2,1);
   var sources=new ComboBox[4];var inverts=new CheckBox[4];
   for(int i=0;i<4;i++){
    layout.Controls.Add(LabelText(AxisFunctionCaptions[i],10,Ink),0,i+2);
    var pick=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Surface,ForeColor=Ink};
    pick.Items.AddRange(AxisSourceCaptions);pick.SelectedIndex=Math.Max(0,Array.IndexOf(AxisSources,customAxes[i]));
    sources[i]=pick;layout.Controls.Add(pick,1,i+2);
    var inv=new CheckBox{Checked=customInvert[i],Dock=DockStyle.Fill,BackColor=Night};inverts[i]=inv;layout.Controls.Add(inv,2,i+2);
   }
   layout.Controls.Add(LabelText("Unchecked: stick right turns and rolls right, and stick up raises throttle. Pitch follows Classic direct. Each stick axis can drive one flight axis.",9,Ink),0,6);layout.SetColumnSpan(layout.GetControlFromPosition(0,6),3);
   var controls=Grid(3,1);for(int c=0;c<3;c++)controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.3f));
   controls.Controls.Add(ActionButton("Classic layout",delegate{for(int i=0;i<4;i++){sources[i].SelectedIndex=Array.IndexOf(AxisSources,DefaultCustomAxes[i]);inverts[i].Checked=false;}}),0,0);
   controls.Controls.Add(ActionButton("Cancel",delegate{dialog.DialogResult=DialogResult.Cancel;}),1,0);
   var ok=ActionButton("Use these axes",delegate{
    var axes=sources.Select(s=>AxisSources[s.SelectedIndex]).ToArray();var inv=inverts.Select(c=>c.Checked).ToArray();
    try{ValidateAxes(axes,inv);}catch(Exception e){MessageBox.Show(dialog,e.Message,dialog.Text);return;}
    dialog.DialogResult=DialogResult.OK;});
   ok.BackColor=Color.FromArgb(82,66,5);controls.Controls.Add(ok,2,0);
   layout.Controls.Add(controls,0,7);layout.SetColumnSpan(controls,3);
   dialog.Controls.Add(layout);
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   for(int i=0;i<4;i++){customAxes[i]=AxisSources[sources[i].SelectedIndex];customInvert[i]=inverts[i].Checked;}
   macroStatus.Text="CUSTOM AXES READY / Save wheel to apply.";
  }
 }

 // --- Repair support: rebuild the Custom preset from the saved config -----------------------
 // Setup regenerates the control table on Install / Repair, then runs this so a saved
 // Custom layout survives. Exit code 0 also when the config does not use Custom axes.
 static int ApplyCustomAxesCli(string[] args){
  try{
   int at=Array.IndexOf(args,"--game");
   if(at<0||at+1>=args.Length)throw new Exception("Usage: ControllerSetup.exe --apply-custom-axes --game <game-root>");
   string mod=Path.Combine(Path.GetFullPath(args[at+1]),"GamepadMods",WheelFolderName);
   string config=Path.Combine(mod,"data","tables","lua",MacroConfigName);
   if(!File.Exists(config))return 0;
   string source=File.ReadAllText(config);
   if(!Regex.IsMatch(source,@"\bflight_axes\s*=\s*""custom"""))return 0;
   var axes=(string[])DefaultCustomAxes.Clone();var invert=new bool[4];
   if(!ParseCustomAxes(source,axes,invert))throw new Exception("The saved custom axes are invalid; open Configure and save them again.");
   WriteCustomPreset(mod,axes,invert);
   return 0;
  }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
 }

 static bool ButtonsAndAxesSelfTest(){
  // A two-preset table shaped like the Classic presets Setup writes.
  string axisBlock="    $Bind: JOY_HEADING_AXIS\r\n        $Primary:\r\n            $Controller: CID_JOY0\r\n            $Flags: AXIS\r\n            $Input: Z_AXIS\r\n        $Secondary:\r\n            $Controller: CID_NONE\r\n";
  string table="#ControlConfigOverride\r\n$Name: default\r\n    $Bind: FIRE_PRIMARY\r\n        $Primary:\r\n            $Controller: CID_NONE\r\n#End\r\n\r\n"+
   "#ControlConfigOverride\r\n$Name: FotG Classic Native\r\n    $Bind: FIRE_PRIMARY\r\n        $Primary:\r\n            $Controller: CID_NONE\r\n"+axisBlock+"#End\r\n\r\n"+
   "#ControlConfigOverride\r\n$Name: FotG Classic Inverted\r\n"+axisBlock+"#End\r\n";
  var axes=new[]{"left_x","right_y","right_x","left_y"};var inv=new[]{false,true,false,false};
  string once=WithCustomPreset(table,axes,inv);
  string twice=WithCustomPreset(once,axes,inv);
  string custom=PresetSection(twice,CustomPresetName);
  if(custom==null||once!=twice)return false;
  if(Regex.Matches(twice,@"\$Name: FotG Custom").Count!=1)return false;
  foreach(string a in AllAxisActions)if(Regex.Matches(custom,@"\$Bind:\s*"+a+@"\b").Count!=1)return false;
  if(!custom.Contains("$Bind: FIRE_PRIMARY"))return false;
  if(!Regex.IsMatch(custom,@"JOY_HEADING_AXIS\s+\$Primary:\s+\$Controller: CID_JOY0\s+\$Flags: AXIS, RELATIVE\s+\$Input: X_AXIS"))return false;
  if(!Regex.IsMatch(custom,@"JOY_PITCH_AXIS\s+\$Primary:\s+\$Controller: CID_JOY0\s+\$Flags: AXIS, RELATIVE, INVERTED\s+\$Input: RX_AXIS"))return false;
  if(!Regex.IsMatch(custom,@"JOY_REL_THROTTLE_AXIS\s+\$Primary:\s+\$Controller: CID_JOY0\s+\$Flags: AXIS, RELATIVE, INVERTED\s+\$Input: Y_AXIS"))return false;
  if(PresetSection(twice,"FotG Classic Native")!=PresetSection(table,"FotG Classic Native"))return false;
  // Saving never leaves the "default" section ahead of a preset with the same bindings.
  if(twice.IndexOf("$Name: default")<twice.IndexOf("$Name: FotG Classic Native")||twice.IndexOf("$Name: default")<twice.IndexOf("$Name: FotG Custom")||Regex.Matches(twice,@"\$Name: default").Count!=1)return false;
  try{ValidateAxes(new[]{"left_x","left_x","none","none"},new bool[4]);return false;}catch(Exception){}
  // Classic through the custom editor gives the same axis bindings as Classic direct.
  // A recorded stick map other than the SDL3 numbering (here a pad whose right stick is raw axes 3 and 4) is honoured.
  var sdl2=new Dictionary<string,string>{{"left_x","X_AXIS"},{"left_y","Y_AXIS"},{"right_x","RX_AXIS"},{"right_y","RY_AXIS"}};
  string onSdl2=CustomAxisBinds(DefaultCustomAxes,new bool[4],sdl2);
  if(!Regex.IsMatch(onSdl2,@"JOY_HEADING_AXIS\s+\$Primary:\s+\$Controller: CID_JOY0\s+\$Flags: AXIS, RELATIVE\s+\$Input: RX_AXIS")||!Regex.IsMatch(onSdl2,@"JOY_PITCH_AXIS\s+\$Primary:\s+\$Controller: CID_JOY0\s+\$Flags: AXIS, RELATIVE\s+\$Input: RY_AXIS"))return false;
  string classic=CustomAxisBinds(DefaultCustomAxes,new bool[4]);
  if(!classic.Contains("JOY_REL_THROTTLE_AXIS\r\n        $Primary:\r\n            $Controller: CID_JOY0\r\n            $Flags: AXIS, RELATIVE, INVERTED\r\n            $Input: Y_AXIS"))return false;
  var taps=new string[8];ParseTapBindings("  tap_bindings = { A=\"countermeasure\", Y=\"bogus\" },",taps);
  if(taps[0]!="countermeasure"||taps[3]!="third_gun"||taps[1]!=DefaultTapActions[1])return false;
  var a2=new string[4];var i2=new bool[4];
  if(!ParseCustomAxes(RenderCustomAxes(axes,inv),a2,i2)||!a2.SequenceEqual(axes)||!i2.SequenceEqual(inv))return false;
  return true;
 }
}
