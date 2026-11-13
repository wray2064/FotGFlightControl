using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class ControllerSetup {
 static readonly string[] LayoutNames={"systems","targeting","macros","commands"};
 static readonly string[][] LayoutCaptions={
  new[]{"All energy: shields","All energy: weapons","All energy: engines","Balance energy","Equalize shields","Match target speed","Transfer to shields","Transfer to lasers"},
  new[]{"All ships","Target subsystem / hull","Enemies","Next live turret","Clear target","Subsystem in sight","Friendlies"},
  new[]{"Macro 1","Macro 2","Macro 3","Macro 4"},
  new[]{"Squad choice 1","Squad choice 2","Squad choice 3","Squad choice 4","Squad choice 5","Squad choice 6"}
 };
 readonly int[][] layoutOrders={Sequence(8),Sequence(7),Sequence(4),Sequence(6)};
 static int[] Sequence(int count){var values=new int[count];for(int i=0;i<count;i++)values[i]=i+1;return values;}
 static bool ValidOrder(int[] values,int count){return values!=null&&values.Length==count&&values.OrderBy(x=>x).SequenceEqual(Sequence(count));}
 static int[][] CopyLayouts(int[][] values){return values.Select(x=>(int[])x.Clone()).ToArray();}
 static string LayoutLine(string name,int[] order){return "    "+name+" = { "+String.Join(", ",order.Select(x=>x.ToString()).ToArray())+" },";}
 void LoadLayouts(string source){
  for(int i=0;i<LayoutNames.Length;i++){
   var m=Regex.Match(source,@"\b"+LayoutNames[i]+@"\s*=\s*\{(?<items>[\d,\s]+)\}");
   if(!m.Success){layoutOrders[i]=Sequence(LayoutCaptions[i].Length);continue;}
   int[] order;
   try{order=m.Groups["items"].Value.Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(x=>Int32.Parse(x.Trim())).ToArray();}
   catch{order=null;}
   layoutOrders[i]=ValidOrder(order,LayoutCaptions[i].Length)?order:Sequence(LayoutCaptions[i].Length);
  }
 }
 void EditLayouts(){
  var draft=CopyLayouts(layoutOrders);
  using(var dialog=new Form{Text="Advanced wheel layouts",StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.Sizable,MinimumSize=new Size(500,450),ClientSize=new Size(690,530),BackColor=Night,ForeColor=Ink,Font=new Font("Consolas",10)}){
   var main=Grid(1,3);main.Padding=new Padding(14);Rows(main,42,-1,52);dialog.Controls.Add(main);
   main.Controls.Add(LabelText("Swap top-level choices. Targeting's eighth slot cycles subsystems; squad changes affect page one only.",10,Ink),0,0);
   var tabs=new TabControl{Dock=DockStyle.Fill};main.Controls.Add(tabs,0,1);
   for(int panel=0;panel<LayoutNames.Length;panel++){
    int index=panel;
    var page=new TabPage(LayoutNames[panel].ToUpperInvariant()){BackColor=Night,ForeColor=Ink};tabs.TabPages.Add(page);
    var body=Grid(2,1);body.Padding=new Padding(10);body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,72));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));page.Controls.Add(body);
    var list=new ListBox{Dock=DockStyle.Fill,BackColor=Night,ForeColor=Ink,Font=new Font("Consolas",11),IntegralHeight=false};body.Controls.Add(list,0,0);
    Action refresh=delegate{int chosen=list.SelectedIndex;list.Items.Clear();for(int slot=0;slot<draft[index].Length;slot++){int sector=index==2?slot*2+1:slot;int source=draft[index][slot]-1;string caption=index==2?macroLabels[source].Text:LayoutCaptions[index][source];list.Items.Add((sector+1).ToString("00")+"  "+caption);}list.SelectedIndex=Math.Min(chosen,list.Items.Count-1);};refresh();
    var buttons=Grid(1,4);Rows(buttons,48,48,48,-1);body.Controls.Add(buttons,1,0);
    buttons.Controls.Add(ActionButton("Move up",delegate{int pos=list.SelectedIndex;if(pos<=0)return;int item=draft[index][pos-1];draft[index][pos-1]=draft[index][pos];draft[index][pos]=item;refresh();list.SelectedIndex=pos-1;}),0,0);
    buttons.Controls.Add(ActionButton("Move down",delegate{int pos=list.SelectedIndex;if(pos<0||pos>=draft[index].Length-1)return;int item=draft[index][pos+1];draft[index][pos+1]=draft[index][pos];draft[index][pos]=item;refresh();list.SelectedIndex=pos+1;}),0,1);
    buttons.Controls.Add(ActionButton("Reset panel",delegate{draft[index]=Sequence(LayoutCaptions[index].Length);refresh();}),0,2);
   }
   var foot=Grid(4,1);for(int i=0;i<4;i++)foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));main.Controls.Add(foot,0,2);
   foot.Controls.Add(ActionButton("Import profile",delegate{if(ImportProfile()){draft=CopyLayouts(layoutOrders);dialog.DialogResult=DialogResult.Cancel;dialog.Close();}}),0,0);
   foot.Controls.Add(ActionButton("Export profile",delegate{ExportProfile(draft);}),1,0);
   foot.Controls.Add(ActionButton("Cancel",delegate{dialog.DialogResult=DialogResult.Cancel;dialog.Close();}),2,0);
   foot.Controls.Add(ActionButton("Use layout",delegate{for(int i=0;i<draft.Length;i++)layoutOrders[i]=(int[])draft[i].Clone();macroStatus.Text="LAYOUT READY / Save wheel to apply.";dialog.DialogResult=DialogResult.OK;dialog.Close();}),3,0);
   dialog.ShowDialog(this);
  }
 }
 public sealed class ProfileMacro {public string label{get;set;}public string action{get;set;}public string argument{get;set;}}
 public sealed class WheelProfile {
  public int version{get;set;}public string game{get;set;}public int[][] layouts{get;set;}
  public ProfileMacro[] macros{get;set;}public string fontSize{get;set;}public bool fontBold{get;set;}
  public string flightAxes{get;set;}public int lookHoldMs{get;set;}public bool lookEnabled{get;set;}
  public bool cameraEnabled{get;set;}public bool holdEnabled{get;set;}public int holdMs{get;set;}
  public bool r3MouseHoldEnabled{get;set;}
  public bool rbModeHoldEnabled{get;set;}
  public bool l3GunHoldEnabled{get;set;}
  public bool lbTorpedoHoldEnabled{get;set;}
  public string[] holdBindings{get;set;}
  // Version 2 added quick-press actions and Custom axes; versions 3–5 added default hold choices. Older profiles load with those holds enabled.
  public string[] tapBindings{get;set;}public string[] customAxes{get;set;}public bool[] customInvert{get;set;}
 }
 WheelProfile CurrentProfile(int[][] layouts){return new WheelProfile{
  version=5,game="FotG 1.1.0",layouts=CopyLayouts(layouts),
  macros=ReadMacroCards().Select(m=>new ProfileMacro{label=m.Label,action=m.Action,argument=m.Argument}).ToArray(),
  fontSize=wheelFontSize.SelectedIndex==1?"large":"medium",fontBold=wheelFontBold.Checked,
  flightAxes=AxesMode(flightAxes.SelectedIndex),
  lookHoldMs=lookHoldSlider.Value*25,lookEnabled=!disableLook.Checked,cameraEnabled=cameraHold.Checked,
  holdEnabled=holdEnabled,holdMs=holdLayerMs,r3MouseHoldEnabled=r3MouseHoldEnabled,rbModeHoldEnabled=rbModeHoldEnabled,l3GunHoldEnabled=l3GunHoldEnabled,lbTorpedoHoldEnabled=lbTorpedoHoldEnabled,holdBindings=(string[])holdActions.Clone(),
  tapBindings=(string[])tapActions.Clone(),customAxes=(string[])customAxes.Clone(),customInvert=(bool[])customInvert.Clone()};}
 static void ValidateProfile(WheelProfile p){
  if(p==null||(p.version!=1&&p.version!=2&&p.version!=3&&p.version!=4&&p.version!=5)||p.game!="FotG 1.1.0")throw new Exception("This profile is not a Flight Control wheel profile.");
  if(p.version==1){p.tapBindings=(string[])DefaultTapActions.Clone();p.customAxes=(string[])DefaultCustomAxes.Clone();p.customInvert=new bool[4];}
  if(p.version<3)p.r3MouseHoldEnabled=true;
  if(p.version<4)p.rbModeHoldEnabled=true;
  if(p.version<5){p.l3GunHoldEnabled=true;p.lbTorpedoHoldEnabled=true;}
  if(p.tapBindings==null||p.tapBindings.Length!=TapButtons.Length||p.tapBindings.Any(x=>!IsTapAction(x)))throw new Exception("Invalid button actions.");
  ValidateAxes(p.customAxes,p.customInvert);
  if(p.layouts==null||p.layouts.Length!=4)throw new Exception("Profile layouts are incomplete.");
  for(int i=0;i<4;i++)if(!ValidOrder(p.layouts[i],LayoutCaptions[i].Length))throw new Exception("Profile has an invalid "+LayoutNames[i]+" layout.");
  if(p.macros==null||p.macros.Length!=4)throw new Exception("Profile must contain four macros.");
  foreach(var m in p.macros){if(m==null||String.IsNullOrWhiteSpace(m.label)||m.label.Length>32||m.label.Any(Char.IsControl)||Array.Find(AvailableActions,a=>a.Id==m.action)==null||
    (m.action=="target_enemy_subsystem"?Array.IndexOf(SubsystemAliases,m.argument)<0:!String.IsNullOrEmpty(m.argument)))throw new Exception("Profile contains an invalid macro.");}
  if(p.fontSize!="medium"&&p.fontSize!="large")throw new Exception("Invalid wheel font size.");
  if(Array.IndexOf(AxesModes,p.flightAxes)<0)throw new Exception("Invalid flight axes mode.");
  if(p.lookHoldMs<150||p.lookHoldMs>600||p.lookHoldMs%25!=0||p.holdMs<250||p.holdMs>1000)throw new Exception("Invalid hold timing.");
  if(p.holdBindings==null||p.holdBindings.Length!=HoldButtons.Length||p.holdBindings.Any(x=>!IsHoldAction(x)))throw new Exception("Invalid hold bindings.");
 }
 static bool LayoutSelfTest(){
  int[][] orders={new[]{2,1,3,4,5,6,7,8},Sequence(7),new[]{4,2,3,1},Sequence(6)};
  string lua=RenderMacroConfig(DefaultMacros,layouts:orders);
  if(!lua.Contains("systems = { 2, 1, 3, 4, 5, 6, 7, 8 }")||!lua.Contains("macros = { 4, 2, 3, 1 }"))return false;
  var profile=new WheelProfile{version=1,game="FotG 1.1.0",layouts=orders,macros=DefaultMacros.Select(x=>new ProfileMacro{label=x.Label,action=x.Action,argument=x.Argument}).ToArray(),fontSize="medium",fontBold=true,flightAxes="classic",lookHoldMs=250,lookEnabled=true,cameraEnabled=true,holdEnabled=false,holdMs=450,holdBindings=new[]{"none","none","none","none","none","none","none"}};
  var roundtrip=new JavaScriptSerializer().Deserialize<WheelProfile>(new JavaScriptSerializer().Serialize(profile));
  ValidateProfile(roundtrip);
  // A version 1 profile still loads and gets the default buttons and axes.
  if(!roundtrip.tapBindings.SequenceEqual(DefaultTapActions)||!roundtrip.customAxes.SequenceEqual(DefaultCustomAxes))return false;
  var v2=new JavaScriptSerializer().Deserialize<WheelProfile>(new JavaScriptSerializer().Serialize(profile));v2.version=2;
  v2.tapBindings=new[]{"countermeasure","none","target_next","afterburner","cycle_secondary","cycle_ship_mode","match_speed","target_next_subsystem"};
  v2.customAxes=new[]{"left_x","left_y","right_x","right_y"};v2.customInvert=new[]{false,false,true,false};v2.flightAxes="custom";
  ValidateProfile(v2);
  v2.customAxes=new[]{"left_x","left_x","none","none"};
  try{ValidateProfile(v2);return false;}catch(Exception){}
  roundtrip.layouts[0][1]=2;
  try{ValidateProfile(roundtrip);return false;}catch(Exception){return true;}
 }
 void ExportProfile(int[][] layouts){using(var save=new SaveFileDialog{Filter="Wheel profile (*.json)|*.json",FileName="FotG-Wheel-Profile.json"}){
  if(save.ShowDialog(this)!=DialogResult.OK)return;
  var profile=CurrentProfile(layouts);ValidateProfile(profile);
  File.WriteAllText(save.FileName,new JavaScriptSerializer().Serialize(profile),new UTF8Encoding(false));
 }}
 bool ImportProfile(){using(var open=new OpenFileDialog{Filter="Wheel profile (*.json)|*.json"}){
  if(open.ShowDialog(this)!=DialogResult.OK)return false;
  var info=new FileInfo(open.FileName);if(info.Length>65536)throw new Exception("Profile exceeds 64 KB.");
  WheelProfile profile;
  try{profile=new JavaScriptSerializer().Deserialize<WheelProfile>(File.ReadAllText(open.FileName));}
  catch(Exception e){throw new Exception("Invalid profile JSON: "+e.Message);}
  ValidateProfile(profile);
  for(int i=0;i<4;i++){layoutOrders[i]=(int[])profile.layouts[i].Clone();SetMacro(i,new MacroSpec(profile.macros[i].label,profile.macros[i].action,profile.macros[i].argument));}
  wheelFontSize.SelectedIndex=profile.fontSize=="large"?1:0;wheelFontBold.Checked=profile.fontBold;
  flightAxes.SelectedIndex=AxesIndex(profile.flightAxes);
  Array.Copy(profile.tapBindings,tapActions,TapButtons.Length);Array.Copy(profile.customAxes,customAxes,4);Array.Copy(profile.customInvert,customInvert,4);
  lookHoldSlider.Value=profile.lookHoldMs/25;disableLook.Checked=!profile.lookEnabled;cameraHold.Checked=profile.cameraEnabled;
  holdEnabled=profile.holdEnabled;holdLayerMs=profile.holdMs;r3MouseHoldEnabled=profile.r3MouseHoldEnabled;rbModeHoldEnabled=profile.rbModeHoldEnabled;l3GunHoldEnabled=profile.l3GunHoldEnabled;lbTorpedoHoldEnabled=profile.lbTorpedoHoldEnabled;Array.Copy(profile.holdBindings,holdActions,HoldButtons.Length);
  macroStatus.Text="PROFILE LOADED / Review and save wheel to apply.";
  return true;
 }}
}
