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
 readonly Color ink=Color.FromArgb(240,247,249), muted=Color.FromArgb(157,184,192), cyan=Color.FromArgb(103,222,239), ground=Color.FromArgb(5,13,20);
 readonly Panel host=new Panel { Dock=DockStyle.Fill };
 readonly Label status=new Label { Dock=DockStyle.Bottom,Height=32,TextAlign=ContentAlignment.MiddleLeft };
 readonly ComboBox packs=new ComboBox { Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,Height=34 };
 readonly ListView backups=new ListView { Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true };
 string root;
 ControllerSetup console;
 sealed class Pack { public string Name, Path; public override string ToString(){return Name;} }

 public HangarBay() {
  root=FindGame(); Text="Flight Control | Hangar Bay";
  MinimumSize=new Size(800,600); Size=new Size(1140,790); StartPosition=FormStartPosition.CenterScreen;
  BackColor=ground; ForeColor=ink; Font=new Font("Consolas",10);
  string icon=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"GamepadMods","FotG_Gamepad_Wheel","ControllerSetup","Assets","FlightControl.ico");
  if(File.Exists(icon)) Icon=new Icon(icon);
  var navigation=new Panel {Dock=DockStyle.Left,Width=196,BackColor=Color.FromArgb(8,20,29)};
  Controls.Add(host); Controls.Add(navigation); Controls.Add(status);
  status.Padding=new Padding(16,0,0,0);status.ForeColor=cyan;status.BackColor=Color.FromArgb(9,25,34);
  var heading=new Label {Dock=DockStyle.Top,Height=110,Text="FLIGHT\nCONTROL\n────────────",ForeColor=ink,Font=new Font("Consolas",16,FontStyle.Bold),Padding=new Padding(15,17,0,0)};
  navigation.Controls.Add(heading);
  var menu=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(10,130,10,0)};
  navigation.Controls.Add(menu);navigation.Controls.SetChildIndex(heading,0);
  Nav(menu,"01  DEPLOY",Home);Nav(menu,"02  FLIGHT CONSOLE",ShowConsole);Nav(menu,"03  MAINTENANCE",Maintenance);Nav(menu,"04  FIELD GUIDE",Help);
  Home();
 }
 static string FindGame(){string p=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');while(p!=null){if(Directory.Exists(Path.Combine(p,"kn_portable")))return p;var d=Directory.GetParent(p);p=d==null?null:d.FullName;}return "";}
 void Nav(FlowLayoutPanel bar,string name,Action action){var b=Button(name,action);b.Width=166;b.Height=48;bar.Controls.Add(b);}
 Button Button(string name,Action action){var b=new Button{Text=name,Height=44,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(10,29,40),ForeColor=ink,Font=new Font("Consolas",10,FontStyle.Bold),Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=cyan;b.Click+=delegate{try{action();}catch(Exception e){MessageBox.Show(e.Message,"Hangar Bay",MessageBoxButtons.OK,MessageBoxIcon.Error);}};return b;}
 Label Label(string text,int height,int size=11){return new Label{Text=text,Dock=DockStyle.Top,Height=height,ForeColor=ink,Font=new Font("Consolas",size,FontStyle.Bold),Padding=new Padding(0,8,0,0)};}
 void Clear(){host.Controls.Clear();status.Text="  HANGAR BAY  /  "+(String.IsNullOrEmpty(root)?"GAME NOT FOUND":root);}
 Panel Page(string title){Clear();var p=new Panel{Dock=DockStyle.Fill,Padding=new Padding(30),AutoScroll=true};host.Controls.Add(p);p.Controls.Add(Label(title,62,19));return p;}
 void Home(){var p=Page("DEPLOYMENT / FLIGHT CONTROL");
  var art=new TacticalPanel {Dock=DockStyle.Top,Height=180,BackColor=ground};p.Controls.Add(art);p.Controls.SetChildIndex(art,0);
  var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(0,20,0,0)};
  content.RowStyles.Add(new RowStyle(SizeType.Absolute,42));content.RowStyles.Add(new RowStyle(SizeType.Absolute,42));content.RowStyles.Add(new RowStyle(SizeType.Absolute,64));content.RowStyles.Add(new RowStyle(SizeType.Absolute,58));content.RowStyles.Add(new RowStyle(SizeType.Absolute,58));content.RowStyles.Add(new RowStyle(SizeType.Absolute,80));
  content.Controls.Add(Label("MISSION PACK / MOD",38),0,0);packs.BackColor=Color.FromArgb(10,29,40);packs.ForeColor=ink;content.Controls.Add(packs,0,1);
  var normal=Button("LAUNCH  /  FLIGHT CONTROL",delegate{Launch(false);});normal.Dock=DockStyle.Fill;content.Controls.Add(normal,0,3);
  var vr=Button("LAUNCH  /  FLIGHT CONTROL VR",delegate{Launch(true);});vr.Dock=DockStyle.Fill;content.Controls.Add(vr,0,4);
  content.Controls.Add(Label("Select a pack to layer it over Fate of the Galaxy. Flight Control remains the first mod; the game data stays last.",72,9),0,5);
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
 void ShowConsole(){Clear();if(console==null||console.IsDisposed){console=new ControllerSetup();console.TopLevel=false;console.FormBorderStyle=FormBorderStyle.None;console.Dock=DockStyle.Fill;}host.Controls.Add(console);console.Show();console.BringToFront();}
 void Maintenance(){var p=Page("MAINTENANCE / RECOVERY");
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Top,Height=60};p.Controls.Add(buttons);p.Controls.SetChildIndex(buttons,0);
  foreach(var item in new[]{"Refresh backups","Open selected backup","Repair installation","Select controller","Remove Flight Control"}){string name=item;var b=Button(name,delegate{if(name=="Refresh backups")RefreshBackups();else if(name=="Open selected backup")OpenBackup();else if(name=="Repair installation")Repair();else if(name=="Select controller")SelectController();else Remove();});b.Width=165;buttons.Controls.Add(b);}
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
 void Help(){var p=Page("FIELD GUIDE / FLIGHT CONTROL");string file=Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel","Help.html");if(!File.Exists(file))return;var web=new WebBrowser{Dock=DockStyle.Fill,ScriptErrorsSuppressed=true};p.Controls.Add(web);p.Controls.SetChildIndex(web,0);web.Navigate(file);}
}

internal sealed class TacticalPanel:Panel {
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var pen=new Pen(Color.FromArgb(226,239,242),2)){int w=Width,h=Height;g.DrawRectangle(pen,18,15,w-36,h-30);g.DrawLine(pen,18,h/2,w-18,h/2);g.DrawLine(pen,w/2,15,w/2,h-15);g.DrawEllipse(pen,w/2-35,h/2-35,70,70);g.DrawLine(pen,w/2-75,h/2,w/2+75,h/2);g.DrawLine(pen,w/2,h/2-75,w/2,h/2+75);for(int i=0;i<7;i++){int x=50+i*(w-100)/6;g.DrawLine(pen,x,15,x,25);g.DrawLine(pen,x,h-25,x,h-15);}}}
}
