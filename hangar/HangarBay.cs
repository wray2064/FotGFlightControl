using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Web.Script.Serialization;

internal static class HangarProgram {
 [STAThread] static int Main(string[] args) {
  if(args.Length==2&&args[0]=="--remove")return RemoveInstalled(args[1]);
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  Application.Run(new HangarBay());
  return 0;
 }
 static int RemoveInstalled(string game){
  try{System.Threading.Thread.Sleep(1200);string root=Path.GetFullPath(game).TrimEnd('\\');
   if(!Directory.Exists(Path.Combine(root,"kn_portable")))return 2;
   string addon=Path.GetFullPath(Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel"));
   string parent=Path.GetFullPath(Path.Combine(root,"GamepadMods"));
   if(!addon.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(addon)!="FotG_Gamepad_Wheel")return 3;
   if(Directory.Exists(addon))Directory.Delete(addon,true);
   foreach(string name in new[]{"Hangar Bay.exe","Flight Control Setup.exe","Play with Gamepad Wheel.exe","Play with Gamepad Wheel VR.exe","Configure Gamepad Wheel.exe"}){
    string file=Path.Combine(root,name);if(File.Exists(file))File.Delete(file);
   }
   return 0;
  }catch{return 1;}
 }
}

internal sealed class HangarBay : Form {
 readonly Color ink=Color.FromArgb(245,246,239), muted=Color.FromArgb(157,184,192), cyan=Color.FromArgb(103,222,239), ground=Color.FromArgb(3,9,15);
 readonly Panel host=new Panel { Dock=DockStyle.Fill };
 readonly Label status=new Label { Dock=DockStyle.Bottom,Height=32,TextAlign=ContentAlignment.MiddleLeft };
 readonly ComboBox packs=new ComboBox { Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,Height=34 };
 readonly ListView backups=new ListView { Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true };
 string root;
 ControllerSetup console;
 sealed class Pack { public string Name, Path; public override string ToString(){return Name;} }

 public HangarBay() {
  root=FindGame(); Text="Flight Control | Hangar Bay";
  MinimumSize=new Size(640,440); Size=new Size(1160,790); StartPosition=FormStartPosition.CenterScreen;
  BackColor=ground; ForeColor=ink; Font=new Font("Consolas",10);
  string icon=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"GamepadMods","FotG_Gamepad_Wheel","ControllerSetup","Assets","FlightControl.ico");
  if(File.Exists(icon)) Icon=new Icon(icon);
  var navigation=new Panel {Dock=DockStyle.Left,Width=198,BackColor=Color.FromArgb(8,20,30)};
  Controls.Add(host); Controls.Add(navigation); Controls.Add(status);
  status.Padding=new Padding(16,0,0,0);status.ForeColor=cyan;status.BackColor=Color.FromArgb(9,25,34);
  var heading=new Label {Dock=DockStyle.Top,Height=112,Text="MTP   /   FLIGHT\nCONTROL\n━━━━━━━━━━━━",ForeColor=ink,Font=new Font("Consolas",14,FontStyle.Bold),Padding=new Padding(15,20,0,0)};
  navigation.Controls.Add(heading);
  var menu=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(9,128,9,0)};
  navigation.Controls.Add(menu);navigation.Controls.SetChildIndex(heading,0);
  Nav(menu,"01  DEPLOY",Home);Nav(menu,"02  CONSOLE",ShowConsole);Nav(menu,"03  SERVICE",Maintenance);Nav(menu,"04  GUIDE",Help);
  Home();
 }
 static string FindGame(){string p=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');while(p!=null){if(Directory.Exists(Path.Combine(p,"kn_portable")))return p;var d=Directory.GetParent(p);p=d==null?null:d.FullName;}return "";}
 void Nav(FlowLayoutPanel bar,string name,Action action){var b=Button(name,action);b.Width=170;b.Height=51;bar.Controls.Add(b);}
 Button Button(string name,Action action){var b=new VectorButton{Text=name,Height=44,BackColor=Color.FromArgb(10,29,40),ForeColor=ink,Font=new Font("Consolas",10,FontStyle.Bold),Cursor=Cursors.Hand};b.Click+=delegate{try{action();}catch(Exception e){MessageBox.Show(e.Message,"Hangar Bay",MessageBoxButtons.OK,MessageBoxIcon.Error);}};return b;}
 Label Label(string text,int height,int size=11){return new Label{Text=text,Dock=DockStyle.Top,Height=height,ForeColor=ink,Font=new Font("Consolas",size,FontStyle.Bold),Padding=new Padding(0,8,0,0)};}
 void Clear(){host.Controls.Clear();status.Text="  HANGAR BAY  //  "+(String.IsNullOrEmpty(root)?"GAME NOT FOUND":root);}
 Panel Page(string title){Clear();var p=new Panel{Dock=DockStyle.Fill,Padding=new Padding(26),BackColor=ground,AutoScroll=true};host.Controls.Add(p);p.Controls.Add(Label(title,54,17));return p;}
 void Home(){var p=Page("01   /   DEPLOYMENT BAY");
  p.AutoScrollMinSize=new Size(0,550);
  var art=new TacticalPanel {Dock=DockStyle.Top,Height=160,BackColor=ground};p.Controls.Add(art);p.Controls.SetChildIndex(art,0);
  var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(0,16,0,0)};
  content.RowStyles.Add(new RowStyle(SizeType.Absolute,32));content.RowStyles.Add(new RowStyle(SizeType.Absolute,38));content.RowStyles.Add(new RowStyle(SizeType.Absolute,12));content.RowStyles.Add(new RowStyle(SizeType.Absolute,60));content.RowStyles.Add(new RowStyle(SizeType.Absolute,60));content.RowStyles.Add(new RowStyle(SizeType.Absolute,66));
  content.Controls.Add(Label("SELECT MISSION PROFILE   /   MOD LOADOUT",34,10),0,0);packs.BackColor=Color.FromArgb(12,31,40);packs.ForeColor=ink;packs.Font=new Font("Consolas",11,FontStyle.Bold);content.Controls.Add(packs,0,1);
  var normal=Button("▶    STANDARD FLIGHT  /  GAMEPAD",delegate{Launch(false);});normal.Dock=DockStyle.Fill;normal.Font=new Font("Consolas",11,FontStyle.Bold);content.Controls.Add(normal,0,3);
  var vr=Button("◇    OPENXR FLIGHT  /  HEADSET",delegate{Launch(true);});vr.Dock=DockStyle.Fill;vr.Font=new Font("Consolas",11,FontStyle.Bold);content.Controls.Add(vr,0,4);
  content.Controls.Add(Label("LOAD ORDER  »  FLIGHT CONTROL  /  SELECTED PACK  /  FOTG\nCommand wheels stay active in both launch modes.",60,9),0,5);
  p.Controls.Add(content);p.Controls.SetChildIndex(content,0);RefreshPacks();
 }
 void RefreshPacks(){packs.Items.Clear();packs.Items.Add(new Pack{Name="Fate of the Galaxy / base game",Path=null});
  string library=Path.Combine(root,"kn_portable","Library");if(Directory.Exists(library))foreach(string manifest in Directory.GetFiles(library,"mod.json",SearchOption.AllDirectories)){
   try{string folder=Path.GetDirectoryName(manifest);if(folder.IndexOf(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0)continue;
    var obj=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(manifest));string id=Convert.ToString(obj.ContainsKey("id")?obj["id"]:"");
    if(id=="FotG_R"||!obj.ContainsKey("parent")||Convert.ToString(obj["parent"])!="FotG_R")continue;
    packs.Items.Add(new Pack{Name=Convert.ToString(obj.ContainsKey("title")?obj["title"]:id)+" / "+Convert.ToString(obj.ContainsKey("version")?obj["version"]:""),Path=folder});
   }catch{} }
  packs.SelectedIndex=0;
 }
 void Launch(bool vr){if(String.IsNullOrEmpty(root))throw new Exception("Game installation not found. Open Hangar Bay from the game folder.");
  string bridge=Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel","WheelBridge.exe");if(!File.Exists(bridge))throw new Exception("Controller bridge missing. Open Maintenance to repair the installation.");
  var pack=packs.SelectedItem as Pack;var info=new ProcessStartInfo(bridge){WorkingDirectory=root,UseShellExecute=false};
  info.Arguments="--game \""+root+"\""+(vr?" --vr":"")+(pack!=null&&pack.Path!=null?" --extra-mod \""+pack.Path+"\"":"");
  Process.Start(info);status.Text="  STARTING "+(pack==null?"BASE GAME":pack.Name)+(vr?" / VR":"");
 }
 void ShowConsole(){Clear();if(console==null||console.IsDisposed){console=new ControllerSetup();console.SetEmbedded();console.TopLevel=false;console.FormBorderStyle=FormBorderStyle.None;console.Dock=DockStyle.Fill;}host.Controls.Add(console);console.Show();console.BringToFront();}
 void Maintenance(){var p=Page("03   /   SERVICE DECK");
  var buttons=new TableLayoutPanel{Dock=DockStyle.Top,Height=156,ColumnCount=2,RowCount=3};
  for(int i=0;i<2;i++)buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
  for(int i=0;i<3;i++)buttons.RowStyles.Add(new RowStyle(SizeType.Percent,100f/3));
  p.Controls.Add(buttons);p.Controls.SetChildIndex(buttons,0);
  var actions=new[]{"REFRESH BACKUPS","OPEN BACKUP","REPAIR SETUP","CHOOSE CONTROLLER","REMOVE TOOL"};
  for(int i=0;i<actions.Length;i++){int choice=i;var b=Button(actions[i],delegate{if(choice==0)RefreshBackups();else if(choice==1)OpenBackup();else if(choice==2)Repair();else if(choice==3)SelectController();else Remove();});b.Dock=DockStyle.Fill;buttons.Controls.Add(b,i%2,i/2);}
  backups.Columns.Clear();backups.Columns.Add("Date / snapshot",220);backups.Columns.Add("Category",190);backups.Columns.Add("Folder",570);backups.BackColor=Color.FromArgb(10,25,34);backups.ForeColor=ink;p.Controls.Add(backups);p.Controls.SetChildIndex(backups,0);RefreshBackups();
 }
 void RefreshBackups(){backups.Items.Clear();string[] sources={Path.Combine(root,"GamepadMods","_WheelInstallBackups"),Path.Combine(root,"kn_portable","HardLightProductions","FreeSpaceOpen","ControllerSetupBackups")};foreach(string folder in sources)if(Directory.Exists(folder))foreach(string path in Directory.GetDirectories(folder).OrderByDescending(x=>x)){var row=new ListViewItem(Path.GetFileName(path));row.SubItems.Add(folder.Contains("_Wheel")?"Installer snapshot":"Pilot and settings");row.SubItems.Add(path);row.Tag=path;backups.Items.Add(row);}}
 void OpenBackup(){if(backups.SelectedItems.Count==0)throw new Exception("Select a backup first.");Process.Start("explorer.exe","\""+(string)backups.SelectedItems[0].Tag+"\"");}
 void SelectController(){MessageBox.Show("Connect only the controller you want to use. Hangar Bay will back up the settings and ask FSO's SDL library to identify it.","Select controller");Repair();}
 void Repair(){
  if(Process.GetProcesses().Any(x=>x.ProcessName.StartsWith("fs2_open",StringComparison.OrdinalIgnoreCase)))throw new Exception("Close the game before repairing Flight Control.");
  string addon=Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel"), setup=Path.Combine(root,"Flight Control Setup.exe");
  if(!Directory.Exists(addon)||!File.Exists(setup))throw new Exception("The installed repair files are missing. Run Setup.exe from the full Flight Control download.");
  string stage=Path.Combine(Path.GetTempPath(),"FotG-Hangar-Repair-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  CopyTree(addon,Path.Combine(stage,"FotG_Gamepad_Wheel"));File.Copy(setup,Path.Combine(stage,"Setup.exe"));
  foreach(string name in new[]{"Play with Gamepad Wheel.exe","Play with Gamepad Wheel VR.exe","Configure Gamepad Wheel.exe","Hangar Bay.exe"}){
   string file=Path.Combine(root,name);if(File.Exists(file))File.Copy(file,Path.Combine(stage,name));
  }
  var info=new ProcessStartInfo(Path.Combine(stage,"Setup.exe")){WorkingDirectory=stage,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true};
  info.Arguments="--install --game \""+root+"\"";using(var process=Process.Start(info)){string output=process.StandardOutput.ReadToEnd();process.WaitForExit();
   if(process.ExitCode!=0)throw new Exception("Repair failed: "+output);MessageBox.Show("Repair complete. Previous files and pilots were backed up.\n\n"+output,"Hangar Bay");}
  RefreshBackups();
 }
 static void CopyTree(string source,string destination){Directory.CreateDirectory(destination);foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));foreach(string folder in Directory.GetDirectories(source))CopyTree(folder,Path.Combine(destination,Path.GetFileName(folder)));}
 void Remove(){
  if(Process.GetProcesses().Any(x=>x.ProcessName.StartsWith("fs2_open",StringComparison.OrdinalIgnoreCase)||x.ProcessName=="WheelBridge"))throw new Exception("Close the game and controller bridge before removing Flight Control.");
  if(MessageBox.Show("Remove Flight Control files and launchers? Pilot files and backups will be kept. Restore any native pilot settings you want from Flight Console before removal.","Remove Flight Control",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
  string helper=Path.Combine(Path.GetTempPath(),"FotG-Hangar-Remove-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Application.ExecutablePath,helper);
  Process.Start(new ProcessStartInfo(helper){Arguments="--remove \""+root+"\"",WorkingDirectory=Path.GetTempPath(),UseShellExecute=false,CreateNoWindow=true});Close();
 }
 void Help(){var p=Page("04   /   FIELD GUIDE");string file=Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel","Help.html");if(!File.Exists(file))return;var web=new WebBrowser{Dock=DockStyle.Fill,ScriptErrorsSuppressed=true};p.Controls.Add(web);p.Controls.SetChildIndex(web,0);web.Navigate(file);}
}

internal sealed class TacticalPanel:Panel {
 public TacticalPanel(){DoubleBuffered=true;ResizeRedraw=true;}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;int w=Width,h=Height,cx=w/2,cy=h/2;
  using(var line=new Pen(Color.FromArgb(224,236,237),2))using(var dim=new Pen(Color.FromArgb(51,89,104),1))using(var accent=new Pen(Color.FromArgb(92,215,231),2)){
   g.DrawRectangle(line,10,8,w-20,h-16);g.DrawLine(dim,10,cy,w-10,cy);g.DrawLine(dim,cx,8,cx,h-8);
   for(int i=1;i<8;i++){int x=10+i*(w-20)/8;g.DrawLine(dim,x,8,x,h-8);}
   for(int i=1;i<4;i++){int y=8+i*(h-16)/4;g.DrawLine(dim,10,y,w-10,y);}
   for(int i=1;i<8;i++){int x=10+i*(w-20)/8;g.DrawLine(line,x,8,x,17);g.DrawLine(line,x,h-17,x,h-8);}
   g.DrawEllipse(accent,cx-46,cy-46,92,92);g.DrawArc(line,cx-66,cy-66,132,132,205,56);g.DrawArc(line,cx-66,cy-66,132,132,25,56);
   g.DrawLine(line,cx-8,cy,cx+8,cy);g.DrawLine(line,cx,cy-8,cx,cy+8);
   var ship=new[]{new Point(cx,cy-34),new Point(cx+10,cy-4),new Point(cx+78,cy+18),new Point(cx+78,cy+31),new Point(cx+13,cy+13),new Point(cx+7,cy+35),new Point(cx-7,cy+35),new Point(cx-13,cy+13),new Point(cx-78,cy+31),new Point(cx-78,cy+18),new Point(cx-10,cy-4)};
   g.DrawPolygon(line,ship);g.DrawLine(accent,cx-78,cy+18,cx-78,cy+31);g.DrawLine(accent,cx+78,cy+18,cx+78,cy+31);
   g.DrawLine(accent,cx-110,cy-51,cx-83,cy-51);g.DrawLine(accent,cx+83,cy-51,cx+110,cy-51);
   using(var font=new Font("Consolas",8,FontStyle.Bold))using(var brush=new SolidBrush(Color.FromArgb(215,233,236))){g.DrawString("TACTICAL FLIGHT GRID",font,brush,22,16);g.DrawString("VECTOR  /  01",font,brush,w-125,h-29);g.DrawString("LOCK  READY",font,brush,22,h-29);}
  }
 }
}

internal sealed class VectorButton:Button {
 bool hot;
 public VectorButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;DoubleBuffered=true;Margin=new Padding(3);}
 protected override void OnMouseEnter(EventArgs e){hot=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hot=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
  using(var fill=new SolidBrush(hot?Color.FromArgb(22,53,66):BackColor))g.FillRectangle(fill,ClientRectangle);
  using(var pen=new Pen(hot?Color.FromArgb(248,245,222):Color.FromArgb(113,207,222),hot?2:1)){
   g.DrawRectangle(pen,1,1,Width-3,Height-3);g.DrawLine(pen,1,1,22,1);g.DrawLine(pen,1,1,1,20);g.DrawLine(pen,Width-23,Height-2,Width-2,Height-2);g.DrawLine(pen,Width-2,Height-22,Width-2,Height-2);
   int x=Width-23,y=Height/2;g.DrawLines(pen,new[]{new Point(x-5,y-5),new Point(x,y),new Point(x-5,y+5)});
  }
  TextRenderer.DrawText(g,Text,Font,new Rectangle(13,0,Width-39,Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
 }
}
