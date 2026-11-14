using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

partial class ControllerSetup {
 const string WheelFolderName = "FotG_Gamepad_Wheel";
 const string MacroConfigName = "zz_fotg_wheel_config-sct.lua";
 Panel contentHost;
 TableLayoutPanel flightPage;
 Control macroPage;
 TableLayoutPanel macroDeckLayout,macroFoot,macroOptions;
 Control[] macroFootChildren=new Control[5];
 Control macroButtonsEditor;
 bool compactMacroFooter;
 bool macroFooterInitialized;
 Button flightTabButton, deckTabButton;
 Label macroStatus;
 Label macroHeading;
 TableLayoutPanel macroTypeRow,macroFlightRow,macroLookRow;
 Control macroTypeLabel,macroTypeHint,macroFlightLabel,macroFlightHint,macroLookLabel;
 bool compactMacroSettings;
 string wheelModFolder = "";
 readonly TextBox[] macroLabels = new TextBox[4];
 readonly ComboBox[] macroActions = new ComboBox[4];
 readonly ComboBox[] macroArguments = new ComboBox[4];
 ComboBox wheelFontSize;
 CheckBox wheelFontBold;
 ComboBox flightAxes;
 TrackBar lookHoldSlider;
 Label lookHoldValue;
 CheckBox disableLook;
 CheckBox cameraHold;
 Button editAxes;
 int holdLayerMs=450;
 bool r3MouseHoldEnabled=true;
 bool rbModeHoldEnabled=true;
 bool l3GunHoldEnabled=true;
 bool lbTorpedoHoldEnabled=true;
 static readonly string[] HoldButtons={"A","B","X","LB","RB","L3","R3"};
 readonly string[] holdActions={"none","none","none","none","none","none","none"};

 sealed class MacroAction {
  public readonly string Id, Caption;
  public MacroAction(string id,string caption){Id=id;Caption=caption;}
  public override string ToString(){return Caption;}
 }
 sealed class MacroSpec {
  public string Label,Action,Argument;
  public MacroSpec(string label,string action,string argument){Label=label;Action=action;Argument=argument;}
 }
 static readonly MacroAction[] AvailableActions = {
  new MacroAction("shields_max","Maximum shield energy"),
  new MacroAction("target_nearest_enemy","Target nearest enemy"),
  new MacroAction("target_enemy_subsystem","Target enemy subsystem"),
  new MacroAction("match_speed","Match target speed"),
  new MacroAction("balance_energy","Balance energy"),
  new MacroAction("boarding_task","Cycle boarding party task")
 };
 static readonly string[] SubsystemAliases = {"engines","weapons","shields","navigation","turrets","sensors"};
 static readonly MacroSpec[] DefaultMacros = {
  new MacroSpec("SHIELDS MAX","shields_max",""),
  new MacroSpec("NEAREST ENEMY","target_nearest_enemy",""),
  new MacroSpec("ENEMY ENGINES","target_enemy_subsystem","engines"),
  new MacroSpec("MATCH SPEED","match_speed","")
 };

 void ShowPage(bool deck,bool profiles=false,bool buttons=false){
  if(flightPage==null||macroPage==null)return;
  flightPage.Visible=!deck&&!profiles&&!buttons;macroPage.Visible=deck;profilePage.Visible=profiles;buttonsPage.Visible=buttons;
  (buttons?buttonsPage:profiles?profilePage:deck?macroPage:flightPage).BringToFront();
  if(nativeFooter!=null)nativeFooter.Visible=!deck&&!profiles&&!buttons;
  if(embeddedCockpit)FitCockpit();
  TabLook(flightTabButton,!deck&&!profiles&&!buttons);TabLook(deckTabButton,deck);TabLook(profileTab,profiles);TabLook(buttonsTab,buttons);
 }
 void UpdateMode(){
  bool advanced=advancedMode.Checked;
  advancedMode.Text=advanced?"Advanced (Experimental)":"Default";
  if(macroFlightRow==null)return;
  macroFlightRow.Enabled=advanced;
  macroLookRow.Enabled=advanced;
  if(buttonsPage!=null)buttonsPage.Enabled=advanced;
  macroFootChildren[3].Enabled=advanced;
  macroCards.Enabled=advanced;
  cameraHold.Enabled=advanced;
 }
 void TabLook(Button b,bool on){if(b==null)return;b.BackColor=on?Color.FromArgb(110,88,8):Color.FromArgb(16,14,3);b.ForeColor=on?Color.White:Color.FromArgb(214,190,70);b.FlatAppearance.BorderSize=on?3:1;b.FlatAppearance.BorderColor=on?Color.FromArgb(255,236,120):Color.FromArgb(120,104,26);b.AccessibleDescription=on?"Selected tab":"Tab";}
 Control BuildMacroDeck(){
  var deck=Grid(1,7);macroDeckLayout=deck;deck.BackColor=Surface;TargetingFrame.Apply(deck);deck.Padding=new Padding(12);Rows(deck,34,40,-1,46,46,46,118);
  macroHeading=LabelText("04 / COMMAND WHEEL  ·  MACROS ON CARDINALS / MISSION ON DIAGONALS",12,Cyan);
  deck.Controls.Add(macroHeading,0,0);
  var compass=Grid(4,1);
  for(int i=0;i<4;i++)compass.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
  string[] directions={"↑  SHIP SYSTEMS","←  TARGET MODES","↓  SQUAD COMMANDS","→  MACRO DECK"};
  for(int i=0;i<4;i++){
   var badge=LabelText(directions[i],9,i==3?Cyan:Ink);
   badge.TextAlign=ContentAlignment.MiddleCenter;
   badge.BackColor=Color.FromArgb(31,26,4);
   badge.Margin=new Padding(3,0,3,5);
   compass.Controls.Add(badge,i,0);
  }
  deck.Controls.Add(compass,0,1);
  var cards=Grid(4,1);macroCards=cards;
  for(int i=0;i<4;i++)cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
  for(int i=0;i<4;i++){macroCardControls[i]=BuildMacroCard(i);cards.Controls.Add(macroCardControls[i],i,0);}
  deck.Controls.Add(cards,0,2);
  var typeRow=Grid(4,1);macroTypeRow=typeRow;typeRow.Padding=new Padding(4,7,4,4);
  typeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
  typeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));
  typeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,18));
  typeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));
  var typeLabel=LabelText("WHEEL TEXT",10,Cyan);macroTypeLabel=typeLabel;typeLabel.TextAlign=ContentAlignment.MiddleLeft;typeRow.Controls.Add(typeLabel,0,0);
  wheelFontSize=new ComboBox{Font=new Font("Segoe UI",12),Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Night,ForeColor=Ink};
  wheelFontSize.Items.AddRange(new object[]{"Medium (20 px)","Large (30 px)"});wheelFontSize.SelectedIndex=0;typeRow.Controls.Add(wheelFontSize,1,0);
  wheelFontBold=new CheckBox{Dock=DockStyle.Fill,Text="Bold",Checked=true,ForeColor=Ink,BackColor=Surface,TextAlign=ContentAlignment.MiddleLeft};typeRow.Controls.Add(wheelFontBold,2,0);
  var typeHint=LabelText("Dark labels and high contrast are always on. Restart game after saving.",9,Ink);macroTypeHint=typeHint;typeHint.TextAlign=ContentAlignment.MiddleLeft;typeRow.Controls.Add(typeHint,3,0);
  deck.Controls.Add(typeRow,0,3);
  var flightRow=Grid(4,1);macroFlightRow=flightRow;flightRow.Padding=new Padding(4,3,4,3);
  flightRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
  flightRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));
  flightRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,14));
  flightRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));
  var flightLabel=LabelText("FLIGHT AXES",10,Cyan);macroFlightLabel=flightLabel;flightLabel.TextAlign=ContentAlignment.MiddleLeft;flightRow.Controls.Add(flightLabel,0,0);
  flightAxes=new ComboBox{Font=new Font("Segoe UI",12),Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Night,ForeColor=Ink};
  flightAxes.Items.AddRange(new object[]{"Classic direct (tested)","Classic inverted pitch","Game's saved setup","Custom axes"});flightAxes.SelectedIndex=0;flightRow.Controls.Add(flightAxes,1,0);
  editAxes=ActionButton("Edit axes...",delegate{EditCustomAxes();});editAxes.Visible=false;flightRow.Controls.Add(editAxes,2,0);
  var flightHint=LabelText(AxesHint(0),9,Ink);macroFlightHint=flightHint;flightHint.TextAlign=ContentAlignment.MiddleLeft;flightRow.Controls.Add(flightHint,3,0);
  bool axesLoading=true;
  flightAxes.SelectedIndexChanged+=delegate{
   editAxes.Visible=flightAxes.SelectedIndex==3;flightHint.Text=AxesHint(flightAxes.SelectedIndex);
   if(flightAxes.SelectedIndex==3&&!axesLoading&&flightAxes.Focused)EditCustomAxes();
  };
  axesLoading=false;
  deck.Controls.Add(flightRow,0,4);
  var lookRow=Grid(4,1);macroLookRow=lookRow;lookRow.Padding=new Padding(4,0,4,0);
  lookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
  lookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
  lookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,12));
  lookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
  var lookLabel=LabelText("D-PAD HOLD TO LOOK",10,Cyan);macroLookLabel=lookLabel;lookLabel.TextAlign=ContentAlignment.MiddleLeft;lookRow.Controls.Add(lookLabel,0,0);
  lookHoldSlider=new TrackBar{Dock=DockStyle.Fill,AutoSize=false,Minimum=6,Maximum=24,TickFrequency=3,SmallChange=1,LargeChange=2,Value=10,BackColor=Surface};
  lookRow.Controls.Add(lookHoldSlider,1,0);
  lookHoldValue=LabelText("250 ms",10,Ink);lookHoldValue.TextAlign=ContentAlignment.MiddleCenter;lookRow.Controls.Add(lookHoldValue,2,0);
  disableLook=new CheckBox{Dock=DockStyle.Fill,Text="Disable look around",Checked=false,ForeColor=Ink,BackColor=Surface,TextAlign=ContentAlignment.MiddleLeft};
  lookRow.Controls.Add(disableLook,3,0);
  lookHoldSlider.ValueChanged+=delegate{UpdateLookTiming();};
  disableLook.CheckedChanged+=delegate{UpdateLookTiming();};
  deck.Controls.Add(lookRow,0,5);
  var foot=Grid(5,1);macroFoot=foot;foot.Padding=new Padding(0,2,0,0);
  foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43));
  foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,12));
  foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,18));
  foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,13));
  foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,14));
  var options=Grid(4,1);macroOptions=options;
  options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
  options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
  options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
  options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,12));
  macroStatus=LabelText("Find or select the installed wheel mod.",9,Ink);macroStatus.TextAlign=ContentAlignment.MiddleLeft;options.Controls.Add(macroStatus,0,0);
  cameraHold=new CheckBox{Text="Hold Menu cycles camera",Checked=true,Dock=DockStyle.Fill,ForeColor=Ink,BackColor=Surface};options.Controls.Add(cameraHold,1,0);
  macroButtonsEditor=new Panel{Visible=false};
  macroFootChildren[0]=options;macroFootChildren[1]=ActionButton("Browse mod",delegate{BrowseWheelMod();});macroFootChildren[2]=new Panel{Visible=false};macroFootChildren[3]=ActionButton("Edit layouts",delegate{EditLayouts();});
  var save=ActionButton("SAVE WHEEL",delegate{SaveMacroConfig();});save.BackColor=Color.FromArgb(110,88,8);save.FlatAppearance.BorderColor=Color.FromArgb(255,236,120);save.Font=new Font("Consolas",11,FontStyle.Bold);macroFootChildren[4]=save;
  for(int i=0;i<5;i++)foot.Controls.Add(macroFootChildren[i],i,0);
  deck.Controls.Add(foot,0,6);
  for(int i=0;i<4;i++)SetMacro(i,DefaultMacros[i]);
  return deck;
 }
 void ReflowMacroSettings(bool compact,float scale){
  if(macroTypeRow==null||compactMacroSettings==compact)return;
  compactMacroSettings=compact;
  macroTypeRow.SuspendLayout();macroTypeRow.Controls.Clear();macroTypeRow.ColumnStyles.Clear();macroTypeRow.RowStyles.Clear();
  macroFlightRow.SuspendLayout();macroFlightRow.Controls.Clear();macroFlightRow.ColumnStyles.Clear();macroFlightRow.RowStyles.Clear();
  macroLookRow.SuspendLayout();macroLookRow.Controls.Clear();macroLookRow.ColumnStyles.Clear();macroLookRow.RowStyles.Clear();
  if(compact){
   macroTypeRow.ColumnCount=3;macroTypeRow.RowCount=2;
   foreach(float width in new float[]{28,48,24})macroTypeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,width));
   macroTypeRow.RowStyles.Add(new RowStyle(SizeType.Percent,50));macroTypeRow.RowStyles.Add(new RowStyle(SizeType.Percent,50));
   macroTypeRow.Controls.Add(macroTypeLabel,0,0);macroTypeRow.Controls.Add(wheelFontSize,1,0);macroTypeRow.Controls.Add(wheelFontBold,2,0);macroTypeRow.Controls.Add(macroTypeHint,0,1);macroTypeRow.SetColumnSpan(macroTypeHint,3);
   macroFlightRow.ColumnCount=3;macroFlightRow.RowCount=2;
   foreach(float width in new float[]{28,50,22})macroFlightRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,width));
   macroFlightRow.RowStyles.Add(new RowStyle(SizeType.Percent,50));macroFlightRow.RowStyles.Add(new RowStyle(SizeType.Percent,50));
   macroFlightRow.Controls.Add(macroFlightLabel,0,0);macroFlightRow.Controls.Add(flightAxes,1,0);macroFlightRow.Controls.Add(editAxes,2,0);macroFlightRow.Controls.Add(macroFlightHint,0,1);macroFlightRow.SetColumnSpan(macroFlightHint,3);
   macroLookRow.ColumnCount=3;macroLookRow.RowCount=2;
   foreach(float width in new float[]{28,53,19})macroLookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,width));
   macroLookRow.RowStyles.Add(new RowStyle(SizeType.Percent,50));macroLookRow.RowStyles.Add(new RowStyle(SizeType.Percent,50));
   macroLookRow.Controls.Add(macroLookLabel,0,0);macroLookRow.Controls.Add(lookHoldSlider,1,0);macroLookRow.Controls.Add(lookHoldValue,2,0);macroLookRow.Controls.Add(disableLook,0,1);macroLookRow.SetColumnSpan(disableLook,3);
  }else{
   macroTypeRow.ColumnCount=4;macroTypeRow.RowCount=1;foreach(float width in new float[]{20,24,18,38})macroTypeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,width));
   macroTypeRow.Controls.Add(macroTypeLabel,0,0);macroTypeRow.Controls.Add(wheelFontSize,1,0);macroTypeRow.Controls.Add(wheelFontBold,2,0);macroTypeRow.Controls.Add(macroTypeHint,3,0);
   macroFlightRow.ColumnCount=4;macroFlightRow.RowCount=1;foreach(float width in new float[]{20,24,14,42})macroFlightRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,width));
   macroFlightRow.Controls.Add(macroFlightLabel,0,0);macroFlightRow.Controls.Add(flightAxes,1,0);macroFlightRow.Controls.Add(editAxes,2,0);macroFlightRow.Controls.Add(macroFlightHint,3,0);
   macroLookRow.ColumnCount=4;macroLookRow.RowCount=1;foreach(float width in new float[]{20,40,12,28})macroLookRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,width));
   macroLookRow.Controls.Add(macroLookLabel,0,0);macroLookRow.Controls.Add(lookHoldSlider,1,0);macroLookRow.Controls.Add(lookHoldValue,2,0);macroLookRow.Controls.Add(disableLook,3,0);
  }
  int[] original={46,46,46};int[] narrow={90,90,100};
  for(int i=0;i<3;i++)macroDeckLayout.RowStyles[3+i]=new RowStyle(SizeType.Absolute,(int)Math.Ceiling((compact?narrow[i]:original[i])*scale));
  macroTypeRow.ResumeLayout(true);macroFlightRow.ResumeLayout(true);macroLookRow.ResumeLayout(true);
 }
 void ReflowMacroFooter(bool compact,float scale){
  if(macroFoot==null||(macroFooterInitialized&&compactMacroFooter==compact))return;
  macroFooterInitialized=true;compactMacroFooter=compact;macroFoot.SuspendLayout();macroFoot.Controls.Clear();macroFoot.ColumnStyles.Clear();macroFoot.RowStyles.Clear();
  macroOptions.Controls.Remove(macroStatus);macroOptions.Controls.Remove(cameraHold);macroOptions.Controls.Remove(macroButtonsEditor);
  if(compact){
   macroFoot.ColumnCount=2;macroFoot.RowCount=4;
   for(int i=0;i<2;i++)macroFoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   for(int i=0;i<4;i++)macroFoot.RowStyles.Add(new RowStyle(SizeType.Percent,25));
   macroFoot.Controls.Add(macroStatus,0,0);macroFoot.SetColumnSpan(macroStatus,2);
   cameraHold.Text="MENU HOLD: CAMERA";macroFoot.Controls.Add(cameraHold,0,1);macroFoot.Controls.Add(macroButtonsEditor,1,1);macroFoot.SetColumnSpan(macroButtonsEditor,1);
   for(int i=1;i<5;i++)macroFoot.Controls.Add(macroFootChildren[i],(i-1)%2,2+(i-1)/2);
  }else{
   macroFoot.ColumnCount=4;macroFoot.RowCount=2;
   for(int i=0;i<4;i++)macroFoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
   macroFoot.RowStyles.Add(new RowStyle(SizeType.Percent,42));macroFoot.RowStyles.Add(new RowStyle(SizeType.Percent,58));
   macroFoot.Controls.Add(macroStatus,0,0);macroFoot.SetColumnSpan(macroStatus,2);
   cameraHold.Text="Hold Menu cycles camera";macroFoot.Controls.Add(cameraHold,2,0);macroFoot.Controls.Add(macroButtonsEditor,3,0);macroFoot.SetColumnSpan(macroButtonsEditor,1);
   for(int i=1;i<5;i++)macroFoot.Controls.Add(macroFootChildren[i],i-1,1);
  }
  macroDeckLayout.RowStyles[6]=new RowStyle(SizeType.Absolute,(int)Math.Ceiling((compact?204:118)*scale));macroFoot.ResumeLayout(true);
 }
 void UpdateLookTiming(){
  if(lookHoldSlider==null||lookHoldValue==null||disableLook==null)return;
  lookHoldSlider.Enabled=!disableLook.Checked;
  lookHoldValue.Text=disableLook.Checked?"OFF":(lookHoldSlider.Value*25)+" ms";
 }
 Control BuildMacroCard(int i){
  var card=Grid(2,5);card.BackColor=Color.FromArgb(15,14,5);TargetingFrame.Apply(card);card.Margin=new Padding(4,0,4,0);card.Padding=new Padding(4);
  card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,112));
  card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  Rows(card,28,42,42,42,-1);
  card.Controls.Add(LabelText("RIGHT / SLOT "+(i+1),11,Cyan),0,0);card.SetColumnSpan(card.GetControlFromPosition(0,0),2);
  card.Controls.Add(LabelText("HUD label",9,Ink),0,1);
  macroLabels[i]=new TextBox{Dock=DockStyle.Fill,BackColor=Night,ForeColor=Ink,MaxLength=32,Font=new Font("Segoe UI",12)};
  card.Controls.Add(macroLabels[i],1,1);
  card.Controls.Add(LabelText("Action",9,Ink),0,2);
  var action=new ComboBox{Font=new Font("Segoe UI",12),Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Night,ForeColor=Ink};
  foreach(var choice in AvailableActions)action.Items.Add(choice);
  macroActions[i]=action;card.Controls.Add(action,1,2);
  card.Controls.Add(LabelText("Subsystem",9,Ink),0,3);
  var argument=new ComboBox{Font=new Font("Segoe UI",12),Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Night,ForeColor=Ink};
  argument.Items.AddRange(SubsystemAliases);
  macroArguments[i]=argument;card.Controls.Add(argument,1,3);
  int slotIndex=i;action.SelectedIndexChanged+=delegate{UpdateArgumentState(slotIndex);};
  return card;
 }
 void UpdateArgumentState(int i){
  var selected=macroActions[i].SelectedItem as MacroAction;
  macroArguments[i].Enabled=selected!=null&&selected.Id=="target_enemy_subsystem";
  if(macroArguments[i].Enabled&&macroArguments[i].SelectedIndex<0)macroArguments[i].SelectedIndex=0;
  if(!macroArguments[i].Enabled)macroArguments[i].SelectedIndex=-1;
 }
 void SetMacro(int i,MacroSpec spec){
  macroLabels[i].Text=spec.Label;
  for(int n=0;n<AvailableActions.Length;n++)if(AvailableActions[n].Id==spec.Action){macroActions[i].SelectedIndex=n;break;}
  int arg=Array.IndexOf(SubsystemAliases,spec.Argument);
  macroArguments[i].SelectedIndex=arg<0?0:arg;
  UpdateArgumentState(i);
 }
 void RefreshWheelModFolder(){
  string gameRoot=root.Text.Trim();
  string candidate=GameInstall.HasMyMod(gameRoot)?GameInstall.MyModCore(gameRoot):Path.Combine(gameRoot,"GamepadMods",WheelFolderName);
  wheelModFolder=Directory.Exists(candidate)?candidate:"";
  if(flightAxes!=null){
   bool myMod=GameInstall.HasMyMod(gameRoot);
   flightAxes.Items[0]=myMod?"Default":"Classic direct (tested)";
   flightAxes.Items[3]=myMod?"Advanced (Experimental)":"Custom axes";
  }
  if(macroStatus!=null){
   macroStatus.Text=wheelModFolder.Length>0?"WHEEL MOD  "+wheelModFolder:"Wheel mod not found. Install it or choose its folder.";
   if(wheelModFolder.Length>0)LoadMacroConfig();
  }
 }
 void BrowseWheelMod(){
  using(var d=new FolderBrowserDialog()){
   d.Description="Select the FotG_Gamepad_Wheel mod folder";
   if(Directory.Exists(wheelModFolder))d.SelectedPath=wheelModFolder;
   if(d.ShowDialog(this)!=DialogResult.OK)return;
   wheelModFolder=d.SelectedPath;
   macroStatus.Text="WHEEL MOD  "+wheelModFolder;
   LoadMacroConfig();
  }
 }
 string MacroConfigPath(){
  if(String.IsNullOrWhiteSpace(wheelModFolder))throw new Exception("Choose the installed FotG_Gamepad_Wheel mod folder first.");
  if(!String.Equals(Path.GetFileName(Path.GetFullPath(wheelModFolder).TrimEnd(Path.DirectorySeparatorChar)),WheelFolderName,StringComparison.OrdinalIgnoreCase))
   throw new Exception("Select the FotG_Gamepad_Wheel folder, not the game or Lua subfolder.");
  string lua=Path.Combine(wheelModFolder,"data","tables","lua");
  if(!Directory.Exists(lua))throw new Exception("The wheel mod's data/tables/lua folder was not found.");
  if(!File.Exists(Path.Combine(lua,"zzz_fotg_wheel-sct.lua")))throw new Exception("The wheel runtime was not found. Install the wheel mod before saving macros.");
  return Path.Combine(lua,MacroConfigName);
 }
 void LoadMacroConfig(){
  string path;
  try{path=MacroConfigPath();}catch(Exception e){macroStatus.Text=e.Message;return;}
  if(!File.Exists(path)){macroStatus.Text="WHEEL MOD FOUND  /  Default macros ready to save.";return;}
  string source=File.ReadAllText(path);
  var matches=Regex.Matches(source,@"\{\s*label\s*=\s*""(?<label>(?:\\.|[^""\\])*)""\s*,\s*action\s*=\s*""(?<action>[a-z_]+)""\s*,\s*argument\s*=\s*""(?<argument>[a-z_]*)""\s*\}");
  if(matches.Count!=4){macroStatus.Text="Existing macro config has a different format; review it before saving.";return;}
  for(int i=0;i<4;i++){
   string action=matches[i].Groups["action"].Value,arg=matches[i].Groups["argument"].Value;
   if(Array.Find(AvailableActions,a=>a.Id==action)==null||(arg!=""&&Array.IndexOf(SubsystemAliases,arg)<0)){macroStatus.Text="Existing macro config uses unsupported actions; review it before saving.";return;}
  }
  for(int i=0;i<4;i++)SetMacro(i,new MacroSpec(UnescapeLua(matches[i].Groups["label"].Value),matches[i].Groups["action"].Value,matches[i].Groups["argument"].Value));
  var sizeMatch=Regex.Match(source,@"\bfont_size\s*=\s*""(medium|large)""");
  wheelFontSize.SelectedIndex=sizeMatch.Success&&sizeMatch.Groups[1].Value=="large"?1:0;
  var boldMatch=Regex.Match(source,@"\bfont_bold\s*=\s*(true|false)\b");
  wheelFontBold.Checked=!boldMatch.Success||boldMatch.Groups[1].Value=="true";
  ParseCustomAxes(source,customAxes,customInvert);
  advancedMode.Checked=Regex.IsMatch(source,@"(?m)^-- console_mode: advanced\s*$");
  var axesMatch=Regex.Match(source,@"\bflight_axes\s*=\s*""(classic|classic_inverted|official|custom)""");
  flightAxes.SelectedIndex=axesMatch.Success?AxesIndex(axesMatch.Groups[1].Value):0;
  if(GameInstall.HasMyMod(root.Text.Trim())&&flightAxes.SelectedIndex==2)flightAxes.SelectedIndex=0;
  ParseTapBindings(source,tapActions);
  var holdMatch=Regex.Match(source,@"\blook_hold_ms\s*=\s*(\d+)");
  int holdMs;
  lookHoldSlider.Value=holdMatch.Success&&Int32.TryParse(holdMatch.Groups[1].Value,out holdMs)?Math.Max(6,Math.Min(24,(int)Math.Round(holdMs/25.0))):10;
  disableLook.Checked=Regex.IsMatch(source,@"\blook_enabled\s*=\s*false\b");
  cameraHold.Checked=!Regex.IsMatch(source,@"\bcamera_hold_enabled\s*=\s*false\b");
  holdEnabled=Regex.IsMatch(source,@"\bhold_enabled\s*=\s*true\b");
  r3MouseHoldEnabled=!Regex.IsMatch(source,@"\br3_mouse_hold_enabled\s*=\s*false\b");
  rbModeHoldEnabled=!Regex.IsMatch(source,@"\brb_mode_hold_enabled\s*=\s*false\b");
  l3GunHoldEnabled=!Regex.IsMatch(source,@"\bl3_gun_hold_enabled\s*=\s*false\b");
  lbTorpedoHoldEnabled=!Regex.IsMatch(source,@"\blb_torpedo_hold_enabled\s*=\s*false\b");
  var holdDuration=Regex.Match(source,@"\bhold_ms\s*=\s*(\d+)");
  int parsedHold;
  holdLayerMs=holdDuration.Success&&Int32.TryParse(holdDuration.Groups[1].Value,out parsedHold)?Math.Max(250,Math.Min(1000,parsedHold)):450;
  var bindings=Regex.Match(source,@"\bhold_bindings\s*=\s*\{(?<body>[^}]*)\}");
  for(int i=0;i<HoldButtons.Length;i++){
   var binding=Regex.Match(bindings.Groups["body"].Value,@"\b"+HoldButtons[i]+@"\s*=\s*""(?<id>[a-z_]+)""");
   string id=binding.Success?binding.Groups["id"].Value:"none";
   holdActions[i]=IsHoldAction(id)?id:"none";
  }
  LoadLayouts(source); if(refreshButtonEditor!=null)refreshButtonEditor();
  UpdateLookTiming();
  macroStatus.Text="WHEEL MOD READY  /  Saved macros loaded.";
 }
 static string EscapeLua(string input){return input.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n");}
 static string UnescapeLua(string input){return Regex.Replace(input,@"\\([\\""rn])",delegate(Match m){string v=m.Groups[1].Value;return v=="r"?"\r":v=="n"?"\n":v;});}
 static string RenderMacroConfig(MacroSpec[] specs,string fontSize="medium",bool bold=true,string axes="classic",int lookHoldMs=250,bool lookEnabled=true,bool cameraEnabled=true,bool holdEnabled=false,int holdMs=450,string[] holdBindings=null,int[][] layouts=null,string[] tapBindings=null,string[] axesMap=null,bool[] invert=null,bool r3MouseHold=true,bool rbModeHold=true,bool l3GunHold=true,bool lbTorpedoHold=true){
  var b=new StringBuilder();
  b.AppendLine("-- Generated by Flight Control Console. Edit through the Macro Deck tab.");
  b.AppendLine("FotGWheelConfig = {");
  b.AppendLine("  flight_axes = \""+AxesMode(AxesIndex(axes))+"\",");
  b.AppendLine(RenderCustomAxes(customAxesOrDefault(axesMap),invertOrDefault(invert)));
  b.AppendLine(RenderTapBindings(tapBindings));
  b.AppendLine("  look_hold_ms = "+Math.Max(150,Math.Min(600,lookHoldMs))+ ",");
  b.AppendLine("  look_enabled = "+(lookEnabled?"true":"false")+",");
  b.AppendLine("  camera_hold_enabled = "+(cameraEnabled?"true":"false")+",");
  b.AppendLine("  hold_enabled = "+(holdEnabled?"true":"false")+",");
  b.AppendLine("  r3_mouse_hold_enabled = "+(r3MouseHold?"true":"false")+",");
  b.AppendLine("  rb_mode_hold_enabled = "+(rbModeHold?"true":"false")+",");
  b.AppendLine("  l3_gun_hold_enabled = "+(l3GunHold?"true":"false")+",");
  b.AppendLine("  lb_torpedo_hold_enabled = "+(lbTorpedoHold?"true":"false")+",");
  b.AppendLine("  hold_ms = "+Math.Max(250,Math.Min(1000,holdMs)) + ",");
  b.Append("  hold_bindings = { ");
  for(int i=0;i<HoldButtons.Length;i++){
   string id=holdBindings!=null&&i<holdBindings.Length&&IsHoldAction(holdBindings[i])?holdBindings[i]:"none";
   b.Append(HoldButtons[i]+"=\""+id+"\"");if(i<HoldButtons.Length-1)b.Append(", ");
  }
  b.AppendLine(" },");
  b.AppendLine("  font_size = \""+(fontSize=="large"?"large":"medium")+"\",");
  b.AppendLine("  font_bold = "+(bold?"true":"false")+",");
  b.AppendLine("  layouts = {");
  for(int i=0;i<LayoutNames.Length;i++)b.AppendLine(LayoutLine(LayoutNames[i],layouts!=null?layouts[i]:Sequence(LayoutCaptions[i].Length)));
  b.AppendLine("  },");
  b.AppendLine("  macros = {");
  foreach(var m in specs)b.AppendLine("    { label = \""+EscapeLua(m.Label)+"\", action = \""+m.Action+"\", argument = \""+m.Argument+"\" },");
  b.AppendLine("  }");
  b.AppendLine("}");
  return b.ToString();
 }
 MacroSpec[] ReadMacroCards(){
  var result=new MacroSpec[4];
  for(int i=0;i<4;i++){
   string label=macroLabels[i].Text.Trim();
   if(label.Length==0||label.Length>32)throw new Exception("Slot "+(i+1)+": enter a label between 1 and 32 characters.");
   foreach(char c in label)if(Char.IsControl(c))throw new Exception("Slot "+(i+1)+": the label cannot contain a line break or control character.");
   var action=macroActions[i].SelectedItem as MacroAction;
   if(action==null)throw new Exception("Slot "+(i+1)+": choose a supported action.");
   string arg=action.Id=="target_enemy_subsystem"?(macroArguments[i].SelectedItem as string):"";
   if(action.Id=="target_enemy_subsystem"&&Array.IndexOf(SubsystemAliases,arg)<0)throw new Exception("Slot "+(i+1)+": choose a subsystem.");
   result[i]=new MacroSpec(label,action.Id,arg);
  }
  return result;
 }
 void SaveMacroConfig(){
  EnsureClosed();
  string path=MacroConfigPath();
  string mode=AxesMode(flightAxes.SelectedIndex);
  bool myMod=GameInstall.HasMyMod(root.Text.Trim());
  string text="-- console_mode: "+(advancedMode.Checked?"advanced":"default")+"\r\n"+
   RenderMacroConfig(ReadMacroCards(),wheelFontSize.SelectedIndex==1?"large":"medium",wheelFontBold.Checked,mode,lookHoldSlider.Value*25,!disableLook.Checked,cameraHold.Checked,holdEnabled,holdLayerMs,holdActions,layoutOrders,tapActions,customAxes,customInvert,r3MouseHoldEnabled,rbModeHoldEnabled,l3GunHoldEnabled,lbTorpedoHoldEnabled);
  // The Custom preset goes into the wheel's control table first, so a failure leaves the config unchanged.
  if(mode=="custom")WriteCustomPreset(wheelModFolder,customAxes,customInvert);
  if(myMod&&mode=="classic")WriteCustomPreset(wheelModFolder,DefaultCustomAxes,new bool[4],"FotG Classic Native");
  if(myMod&&mode=="classic_inverted")WriteCustomPreset(wheelModFolder,DefaultCustomAxes,new[]{false,true,false,false},"FotG Classic Inverted");
  if(File.Exists(path)){
   string folder=Path.Combine(wheelModFolder,"ConfigBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
   Directory.CreateDirectory(folder);
   File.Copy(path,Path.Combine(folder,MacroConfigName));
  }
  string temp=path+".controller-tmp";
  File.WriteAllText(temp,text,new UTF8Encoding(false));
  try{if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
  finally{if(File.Exists(temp))File.Delete(temp);}
  macroStatus.Text="WHEEL SAVED  /  Restart the game to load settings.";
  MessageBox.Show("Wheel layout, macros and options saved. Restart the game to load them.",Text);
 }
 void ReserveWheelButtons(){
  EnsureClosed();
  string path=PilotPath(),ini=SettingsPath();
  if(!File.Exists(ini))throw new Exception("The active game settings INI was not found; no pilot bindings changed.");
  var pilot=Read(path);
  int changed=ReserveBindingData(pilot);
  if(changed==0){MessageBox.Show("No native wheel-button joystick bindings were found. No changes made.",Text);return;}
  string backup=Path.Combine(prefs,"ControllerSetupBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-before-wheel-reserve");
  Directory.CreateDirectory(backup);
  File.Copy(path,Path.Combine(backup,Path.GetFileName(path)));
  File.Copy(ini,Path.Combine(backup,Path.GetFileName(ini)));
  Atomic(path,json.Serialize(pilot));
  MessageBox.Show("Reserved "+changed+" native joystick bindings used by the wheel. Pilot backup: "+backup+"\nThe wheel companion uses RT for primary fire. Restart the game before testing.",Text);
 }
 static bool WheelOwnsJoystickButton(int input){
  // View (6) keeps its native targeting action; these other buttons belong to the companion.
  return (input>=0&&input<=5)||(input>=7&&input<=9)||(input>=128&&input<=131);
 }
 static int ReserveBindingData(Dictionary<string,object> pilot){
  object version;
  if(!pilot.TryGetValue("version",out version)||Convert.ToInt32(version)!=4)throw new Exception("This tool supports native pilot version 4 only.");
  var sections=pilot["sections"] as Dictionary<string,object>;
  var controls=sections==null?null:sections["controls"] as Dictionary<string,object>;
  object raw;
  if(controls==null||!controls.TryGetValue("controls",out raw)||raw==null||raw is string||!(raw is IEnumerable))
   throw new Exception("Pilot controls use an unrecognized format; no bindings changed.");
  int changed=0;
  foreach(object item in (IEnumerable)raw){
   var binding=item as Dictionary<string,object>;
   if(binding==null)throw new Exception("Pilot controls use an unrecognized entry format; no bindings changed.");
   object value;
   if(!binding.TryGetValue("joystick",out value))continue;
   int input;
   try{input=Convert.ToInt32(value);}catch{throw new Exception("Pilot joystick binding format is unrecognized; no bindings changed.");}
   if(WheelOwnsJoystickButton(input)){binding["joystick"]=-1;changed++;}
  }
  return changed;
 }
 static int ReserveWheelCli(string[] args){
  try{
   int at=Array.IndexOf(args,"--game");
   if(at<0||at+1>=args.Length)throw new Exception("Usage: ControllerSetup.exe --reserve-wheel --game <game-root>");
   string game=Path.GetFullPath(args[at+1]);
   string prefsRoot=Path.Combine(game,"kn_portable","HardLightProductions","FreeSpaceOpen");
   string players=Path.Combine(prefsRoot,"data","players");
   string ini=SettingsPath(prefsRoot);
   if(!Directory.Exists(players)||!File.Exists(ini))throw new Exception("Native portable pilot folder or active game settings INI was not found.");
   if(System.Diagnostics.Process.GetProcesses().Length>0&&
      Array.Exists(System.Diagnostics.Process.GetProcesses(),p=>p.ProcessName.StartsWith("fs2_open",StringComparison.OrdinalIgnoreCase)))
    throw new Exception("Close FreeSpace Open before reserving controls.");
   var serializer=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=16777216};
   var changes=new Dictionary<string,string>();
   foreach(string file in Directory.GetFiles(players,"*.json")){
    var pilot=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(file));
    object version;
    if(!pilot.TryGetValue("version",out version)||Convert.ToInt32(version)!=4)continue;
    if(ReserveBindingData(pilot)>0)changes[file]=serializer.Serialize(pilot);
   }
   if(changes.Count==0)return 0;
   string backup=Path.Combine(prefsRoot,"ControllerSetupBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-before-wheel-reserve-cli");
   Directory.CreateDirectory(backup);
   foreach(string file in changes.Keys)File.Copy(file,Path.Combine(backup,Path.GetFileName(file)));
   File.Copy(ini,Path.Combine(backup,Path.GetFileName(ini)));
   try{
    foreach(var entry in changes)Atomic(entry.Key,entry.Value);
   }catch{
    foreach(string file in changes.Keys)File.Copy(Path.Combine(backup,Path.GetFileName(file)),file,true);
    throw;
   }
   File.WriteAllText(Path.Combine(backup,"wheel-reserve-result.txt"),"Reserved companion-owned joystick bindings in "+changes.Count+" native v4 pilot(s).\r\n");
   return 0;
  }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
 }
 static bool WheelSelfTest(){
  var binding=new Dictionary<string,object>{{"key",200},{"joystick",128},{"mouse",-1}};
  var bindingA=new Dictionary<string,object>{{"key",-1},{"joystick",0},{"mouse",-1}};
  var bindingB=new Dictionary<string,object>{{"key",57},{"joystick",1},{"mouse",-1}};
  var bindingX=new Dictionary<string,object>{{"key",34},{"joystick",2},{"mouse",-1}};
  var bindingL3=new Dictionary<string,object>{{"key",50},{"joystick",8},{"mouse",-1}};
  var bindingR3=new Dictionary<string,object>{{"key",51},{"joystick",9},{"mouse",-1}};
  var bindingY=new Dictionary<string,object>{{"key",52},{"joystick",3},{"mouse",-1}};
  var bindingLB=new Dictionary<string,object>{{"key",53},{"joystick",4},{"mouse",-1}};
  var bindingRB=new Dictionary<string,object>{{"key",54},{"joystick",5},{"mouse",-1}};
  var bindingStart=new Dictionary<string,object>{{"key",55},{"joystick",7},{"mouse",-1}};
  var bindingView=new Dictionary<string,object>{{"key",56},{"joystick",6},{"mouse",-1}};
  var other=new Dictionary<string,object>{{"key",12},{"joystick",10},{"mouse",-1}};
  var controls=new Dictionary<string,object>{{"controls",new object[]{binding,bindingA,bindingB,bindingX,bindingL3,bindingR3,bindingY,bindingLB,bindingRB,bindingStart,bindingView,other}}};
  var sections=new Dictionary<string,object>{{"controls",controls}};
  var pilot=new Dictionary<string,object>{{"version",4},{"sections",sections}};
  int changed=ReserveBindingData(pilot);
  return changed==10&&Convert.ToInt32(binding["joystick"])==-1&&Convert.ToInt32(bindingA["joystick"])==-1&&Convert.ToInt32(bindingB["joystick"])==-1&&Convert.ToInt32(bindingX["joystick"])==-1&&Convert.ToInt32(bindingL3["joystick"])==-1&&Convert.ToInt32(bindingR3["joystick"])==-1&&Convert.ToInt32(bindingY["joystick"])==-1&&Convert.ToInt32(bindingLB["joystick"])==-1&&Convert.ToInt32(bindingRB["joystick"])==-1&&Convert.ToInt32(bindingStart["joystick"])==-1&&Convert.ToInt32(bindingView["joystick"])==6&&Convert.ToInt32(other["joystick"])==10&&
   RenderMacroConfig(new MacroSpec[]{new MacroSpec("A \"quote\"","shields_max","")}).Contains("A \\\"quote\\\"")&&
   RenderMacroConfig(DefaultMacros,"large",false).Contains("font_size = \"large\"")&&
   RenderMacroConfig(DefaultMacros,"large",false).Contains("font_bold = false")&&
   RenderMacroConfig(DefaultMacros,"medium",true,"official").Contains("flight_axes = \"official\"")&&
   RenderMacroConfig(DefaultMacros,"medium",true,"classic_inverted").Contains("flight_axes = \"classic_inverted\"")&&
   RenderMacroConfig(DefaultMacros,"medium",true,"classic",375,false).Contains("look_hold_ms = 375")&&
   RenderMacroConfig(DefaultMacros,"medium",true,"classic",375,false).Contains("look_enabled = false")&&
   RenderMacroConfig(DefaultMacros,"medium",true,"classic",250,true,false,true,600,new[]{"shields_max","none","none","none","none","none","none"}).Contains("camera_hold_enabled = false")&&
   RenderMacroConfig(DefaultMacros,"medium",true,"classic",250,true,false,true,600,new[]{"shields_max","none","none","none","none","none","none"}).Contains("A=\"shields_max\"")&&
   LayoutSelfTest()&&ButtonsAndAxesSelfTest()&&
   RenderMacroConfig(DefaultMacros,"medium",true,"custom").Contains("flight_axes = \"custom\"")&&
   RenderMacroConfig(DefaultMacros,tapBindings:new[]{"countermeasure","none","x","third_gun","afterburner","cycle_ship_mode","match_speed","target_next_subsystem"}).Contains("tap_bindings = { A=\"countermeasure\", B=\"none\", X=\"target_next\"");
 }
}
