using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Web.Script.Serialization;

internal static class HangarProgram {
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  Application.Run(new HangarBay());
  return 0;
 }
 // Removes the add-on, the launchers and the Setup copy, in this process. Hangar Bay.exe is left:
 // a running program cannot delete its own file, so the player deletes it after Hangar Bay closes.
 // Returns the files that could not be removed.
 internal static List<string> RemoveInstalled(string game){
  string root=Path.GetFullPath(game).TrimEnd('\\');
  if(!Directory.Exists(Path.Combine(root,"kn_portable")))throw new Exception("This is not a Fate of the Galaxy folder; nothing was removed.");
  string addon=Path.GetFullPath(Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel"));
  string parent=Path.GetFullPath(Path.Combine(root,"GamepadMods"));
  if(!addon.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(addon)!="FotG_Gamepad_Wheel")throw new Exception("The add-on folder did not resolve inside GamepadMods; nothing was removed.");
  var left=new List<string>();
  if(Directory.Exists(addon)){
   foreach(string file in Directory.GetFiles(addon,"*",SearchOption.AllDirectories)){try{File.SetAttributes(file,FileAttributes.Normal);File.Delete(file);}catch(IOException){left.Add(file);}catch(UnauthorizedAccessException){left.Add(file);}}
   if(left.Count==0){try{Directory.Delete(addon,true);}catch(IOException){left.Add(addon);}}
  }
  foreach(string name in new[]{"Flight Control Setup.exe","Play with Gamepad Wheel.exe","Play with Gamepad Wheel VR.exe","Configure Gamepad Wheel.exe","Play with Gamepad Wheel.cmd","Configure Gamepad Wheel.cmd"}){
   string file=Path.Combine(root,name);
   if(File.Exists(file)){try{File.Delete(file);}catch(IOException){left.Add(file);}catch(UnauthorizedAccessException){left.Add(file);}}
  }
  return left;
 }
}

internal sealed class HangarBay : Form {
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
 readonly Color ink=Color.FromArgb(240,246,248), muted=Color.FromArgb(170,196,206), cyan=Color.FromArgb(246,201,69), ground=Color.FromArgb(3,9,15);
 readonly Panel host=new Panel { Dock=DockStyle.Fill };
 readonly Label status=new Label { Dock=DockStyle.Bottom,Height=36,TextAlign=ContentAlignment.MiddleLeft };
 readonly ComboBox packs=new ComboBox { Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=30,FlatStyle=FlatStyle.Flat };
 readonly ListView backups=new ListView { Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,OwnerDraw=true,HideSelection=false,BorderStyle=BorderStyle.None };
 readonly List<VectorButton> navButtons=new List<VectorButton>();
 string root;
 ControllerSetup console;
 sealed class Pack { public string Name, Path; public override string ToString(){return Name;} }

 public HangarBay() {
  root=FindGame(); Text="Flight Control | Hangar Bay";
  MinimumSize=new Size(640,440); Size=new Size(1160,790); StartPosition=FormStartPosition.CenterScreen;
  BackColor=ground; ForeColor=ink; Font=new Font("Consolas",10);
  FormBorderStyle=FormBorderStyle.None;
  MaximizedBounds=Screen.FromControl(this).WorkingArea;
  Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  var navigation=new Panel {Dock=DockStyle.Left,Width=216,BackColor=Color.FromArgb(6,16,24)};
  navigation.Paint+=delegate(object s,PaintEventArgs e){using(var pen=new Pen(Color.FromArgb(70,104,120),1))e.Graphics.DrawLine(pen,navigation.Width-1,0,navigation.Width-1,navigation.Height);};
  Controls.Add(host); Controls.Add(navigation); Controls.Add(status); Controls.Add(BuildTitleBar());
  status.Padding=new Padding(16,0,0,0);status.ForeColor=muted;status.BackColor=Color.FromArgb(6,16,24);status.Font=new Font("Consolas",10);
  packs.BackColor=Color.FromArgb(10,26,36);packs.ForeColor=ink;packs.Font=new Font("Consolas",11,FontStyle.Bold);
  packs.DrawItem+=delegate(object s,DrawItemEventArgs e){if(e.Index<0)return;bool sel=(e.State&DrawItemState.Selected)!=0;using(var b=new SolidBrush(sel?Color.FromArgb(62,48,10):Color.FromArgb(10,26,36)))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,Convert.ToString(packs.Items[e.Index]),packs.Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-10,e.Bounds.Height),ink,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);};
  StyleBackups();
  var heading=new Label {Dock=DockStyle.Top,Height=118,Text="FLIGHT\nCONTROL\nHANGAR BAY",ForeColor=ink,Font=new Font("Consolas",15,FontStyle.Bold),Padding=new Padding(16,18,0,0)};
  navigation.Controls.Add(heading);
  var menu=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(8,8,8,0)};
  navigation.Controls.Add(menu);navigation.Controls.SetChildIndex(menu,0);
  Nav(menu,"01  DEPLOY",Home);Nav(menu,"02  CONSOLE",ShowConsole);Nav(menu,"03  SERVICE",Maintenance);Nav(menu,"04  GUIDE",Help);
  SetSection(0);
  Home();
 }
 Control BuildTitleBar(){
  var bar=new Panel{Dock=DockStyle.Top,Height=42,BackColor=Color.FromArgb(6,16,24)};
  var title=new Label{Dock=DockStyle.Fill,Text=Text,Padding=new Padding(16,0,0,0),TextAlign=ContentAlignment.MiddleLeft};
  title.MouseDown+=delegate(object sender,MouseEventArgs e){
   if(e.Button!=MouseButtons.Left)return;
   if(e.Clicks==2){ToggleMaximize();return;}
   ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);
  };
  bar.Controls.Add(title);
  var minimize=TitleButton("−","Minimize",delegate{WindowState=FormWindowState.Minimized;});
  var maximize=TitleButton("□","Maximize or restore",ToggleMaximize);
  var close=TitleButton("×","Close",Close);
  bar.Controls.Add(minimize);bar.Controls.Add(maximize);bar.Controls.Add(close);
  Resize+=delegate{maximize.Text=WindowState==FormWindowState.Maximized?"❐":"□";};
  return bar;
 }
 Button TitleButton(string text,string name,Action action){
  var button=new Button{Dock=DockStyle.Right,Width=48,Text=text,AccessibleName=name,FlatStyle=FlatStyle.Flat,ForeColor=ink,Font=new Font("Segoe UI",14)};
  button.FlatAppearance.BorderSize=0;
  button.FlatAppearance.MouseOverBackColor=name=="Close"?Color.FromArgb(125,30,18):Color.FromArgb(22,45,58);
  button.Click+=delegate{action();};
  return button;
 }
 void ToggleMaximize(){
  MaximizedBounds=Screen.FromControl(this).WorkingArea;
  WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;
 }
 protected override CreateParams CreateParams{
  get{var p=base.CreateParams;p.Style|=0x20000|0x10000;return p;}
 }
 protected override void WndProc(ref Message m){
  base.WndProc(ref m);
  if(m.Msg!=0x84||WindowState!=FormWindowState.Normal||m.Result.ToInt64()!=1)return;
  long point=m.LParam.ToInt64();
  Point p=PointToClient(new Point((short)point,(short)(point>>16)));
  const int edge=7;
  bool left=p.X<edge,right=p.X>=ClientSize.Width-edge,top=p.Y<edge,bottom=p.Y>=ClientSize.Height-edge;
  int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1;
  m.Result=new IntPtr(hit);
 }
 void SetSection(int index){for(int i=0;i<navButtons.Count;i++){navButtons[i].Selected=i==index;navButtons[i].Invalidate();}}
 void StyleBackups(){
  backups.BackColor=Color.FromArgb(8,20,29);backups.ForeColor=ink;backups.Font=new Font("Consolas",10);backups.HeaderStyle=ColumnHeaderStyle.Nonclickable;
  var rowHeight=new ImageList{ImageSize=new Size(1,38)};backups.SmallImageList=rowHeight;
  backups.DrawColumnHeader+=delegate(object s,DrawListViewColumnHeaderEventArgs e){using(var b=new SolidBrush(Color.FromArgb(14,34,46)))e.Graphics.FillRectangle(b,e.Bounds);using(var pen=new Pen(Color.FromArgb(70,104,120)))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);TextRenderer.DrawText(e.Graphics,e.Header.Text.ToUpperInvariant(),backups.Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);};
  backups.DrawItem+=delegate(object s,DrawListViewItemEventArgs e){};
  backups.DrawSubItem+=delegate(object s,DrawListViewSubItemEventArgs e){bool sel=e.Item.Selected;Color back=sel?Color.FromArgb(62,48,10):(e.ItemIndex%2==0?Color.FromArgb(8,20,29):Color.FromArgb(11,26,37));using(var b=new SolidBrush(back))e.Graphics.FillRectangle(b,e.Bounds);if(sel){using(var pen=new Pen(Color.FromArgb(246,201,69),3))e.Graphics.DrawLine(pen,e.Bounds.Left+1,e.Bounds.Top,e.Bounds.Left+1,e.Bounds.Bottom);}
   TextRenderer.DrawText(e.Graphics,e.SubItem.Text,backups.Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),e.ColumnIndex==2&&!sel?muted:ink,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);};
  backups.Resize+=delegate{FitBackupColumns();};
 }
 void FitBackupColumns(){if(backups.Columns.Count<3)return;int w=Math.Max(300,backups.ClientSize.Width-4);int a=Math.Max(190,(int)(w*0.24)),b=Math.Max(190,(int)(w*0.25));backups.Columns[0].Width=a;backups.Columns[1].Width=b;backups.Columns[2].Width=Math.Max(80,w-a-b);}
 static string FindGame(){string p=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');while(p!=null){if(Directory.Exists(Path.Combine(p,"kn_portable")))return p;var d=Directory.GetParent(p);p=d==null?null:d.FullName;}return "";}
 void Nav(FlowLayoutPanel bar,string name,Action action){int index=navButtons.Count;var b=Button(name,delegate{SetSection(index);action();});b.Kind=ButtonKind.Nav;b.Width=198;b.Height=56;b.Margin=new Padding(0,0,0,6);navButtons.Add(b);bar.Controls.Add(b);}
 VectorButton Button(string name,Action action){var b=new VectorButton{Text=name,Height=48,BackColor=Color.FromArgb(10,29,40),ForeColor=ink,Font=new Font("Consolas",11,FontStyle.Bold),Cursor=Cursors.Hand,AccessibleName=name};b.Click+=delegate{try{action();}catch(Exception e){MessageBox.Show(e.Message,"Hangar Bay",MessageBoxButtons.OK,MessageBoxIcon.Error);}};return b;}
 Label Label(string text,int height,int size=11){return new Label{Text=text,Dock=DockStyle.Top,Height=height,ForeColor=ink,Font=new Font("Consolas",size,FontStyle.Bold),Padding=new Padding(0,8,0,0)};}
 void Clear(){host.Controls.Clear();status.Text="  HANGAR BAY  //  "+(String.IsNullOrEmpty(root)?"GAME NOT FOUND":root);}
 Panel Page(string title){Clear();var p=new Panel{Dock=DockStyle.Fill,Padding=new Padding(26),BackColor=ground,AutoScroll=true};host.Controls.Add(p);var t=Label(title,54,17);t.ForeColor=ink;p.Controls.Add(t);return p;}
 Label Note(string text,int height,Color color){return new Label{Text=text,Dock=DockStyle.Fill,Height=height,ForeColor=color,Font=new Font("Consolas",10),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=false};}
 void Home(){var p=Page("01   /   DEPLOYMENT BAY");
  p.AutoScrollMinSize=new Size(0,640);
  var art=new TacticalPanel {Dock=DockStyle.Top,Height=170,BackColor=ground};p.Controls.Add(art);p.Controls.SetChildIndex(art,0);
  var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(0,18,0,0)};
  content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  content.RowStyles.Add(new RowStyle(SizeType.Absolute,30));content.RowStyles.Add(new RowStyle(SizeType.Absolute,44));content.RowStyles.Add(new RowStyle(SizeType.Absolute,18));content.RowStyles.Add(new RowStyle(SizeType.Absolute,80));content.RowStyles.Add(new RowStyle(SizeType.Absolute,12));content.RowStyles.Add(new RowStyle(SizeType.Absolute,80));content.RowStyles.Add(new RowStyle(SizeType.Absolute,74));
  var pick=Label("MISSION PROFILE   /   MOD LOADOUT",30,10);pick.Dock=DockStyle.Fill;pick.ForeColor=muted;pick.Padding=Padding.Empty;pick.TextAlign=ContentAlignment.BottomLeft;content.Controls.Add(pick,0,0);
  packs.Dock=DockStyle.Fill;packs.AccessibleName="Mission pack";content.Controls.Add(packs,0,1);
  var normal=Button("LAUNCH  ·  STANDARD FLIGHT",delegate{Launch(false);});normal.Kind=ButtonKind.Primary;normal.Subtitle="Gamepad flight with command wheels";normal.Dock=DockStyle.Fill;normal.Font=new Font("Consolas",13,FontStyle.Bold);normal.Margin=Padding.Empty;content.Controls.Add(normal,0,3);
  var vr=Button("LAUNCH  ·  OPENXR VR",delegate{Launch(true);});vr.Subtitle="Headset flight, same load order and wheels";vr.Dock=DockStyle.Fill;vr.Font=new Font("Consolas",13,FontStyle.Bold);vr.Margin=Padding.Empty;content.Controls.Add(vr,0,5);
  var order=Note("LOAD ORDER   FLIGHT CONTROL  >  SELECTED PACK  >  FOTG\nCommand wheels stay active in both launch modes.",74,muted);order.Padding=new Padding(0,10,0,0);order.TextAlign=ContentAlignment.TopLeft;content.Controls.Add(order,0,6);
  p.Controls.Add(content);p.Controls.SetChildIndex(content,0);RefreshPacks();
 }
 void RefreshPacks(){bool myMod=GameInstall.HasMyMod(root);packs.Items.Clear();packs.Items.Add(new Pack{Name=myMod?"Fate of the Galaxy 2 / MyMod":"Fate of the Galaxy / base game",Path=null});
  string library=Path.Combine(root,"kn_portable","Library");if(Directory.Exists(library))foreach(string manifest in Directory.GetFiles(library,"mod.json",SearchOption.AllDirectories)){
   try{string folder=Path.GetDirectoryName(manifest);if(folder.IndexOf(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0)continue;
    var obj=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(manifest));string id=Convert.ToString(obj.ContainsKey("id")?obj["id"]:"");
    if(id=="MyMod"||!obj.ContainsKey("parent")||Convert.ToString(obj["parent"])!=(myMod?"FotG_2":"FotG_R"))continue;
    packs.Items.Add(new Pack{Name=Convert.ToString(obj.ContainsKey("title")?obj["title"]:id)+" / "+Convert.ToString(obj.ContainsKey("version")?obj["version"]:""),Path=folder});
   }catch{} }
  packs.SelectedIndex=0;
 }
 void Launch(bool vr){if(String.IsNullOrEmpty(root))throw new Exception("Game installation not found. Open Hangar Bay from the game folder.");
  string bridge=GameInstall.HasMyMod(root)?Path.Combine(root,"Flight Control Console","WheelBridge.exe"):Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel","WheelBridge.exe");if(!File.Exists(bridge))throw new Exception("Controller bridge missing. Open Maintenance to repair the installation.");
  var pack=packs.SelectedItem as Pack;var info=new ProcessStartInfo(bridge){WorkingDirectory=root,UseShellExecute=false};
  info.Arguments="--game \""+root+"\""+(vr?" --vr":"")+(pack!=null&&pack.Path!=null?" --extra-mod \""+pack.Path+"\"":"");
  Process.Start(info);status.Text="  STARTING "+(pack==null?"BASE GAME":pack.Name)+(vr?" / VR":"");
 }
 void ShowConsole(){Clear();if(console==null||console.IsDisposed){console=new ControllerSetup();console.SetEmbedded();console.TopLevel=false;console.FormBorderStyle=FormBorderStyle.None;console.Dock=DockStyle.Fill;}host.Controls.Add(console);console.Show();console.BringToFront();}
 void Maintenance(){var p=Page("03   /   SERVICE DECK");
  p.AutoScrollMinSize=new Size(0,640);
  var buttons=new TableLayoutPanel{Dock=DockStyle.Top,Height=210,ColumnCount=2,RowCount=4,Margin=Padding.Empty};
  p.Controls.Add(buttons);p.Controls.SetChildIndex(buttons,0);
  var actions=new[]{"REFRESH BACKUPS","OPEN BACKUP","REPAIR SETUP","CHOOSE CONTROLLER","REMOVE TOOL"};
  var notes=new[]{"Rescan snapshot folders","Show selected row in Explorer","Reinstall launchers, keep pilots","Pick the active gamepad","Keeps pilots and backups"};
  var serviceButtons=new List<VectorButton>();
  for(int i=0;i<actions.Length;i++){int choice=i;var b=Button(actions[i],delegate{if(choice==0)RefreshBackups();else if(choice==1)OpenBackup();else if(choice==2)Repair();else if(choice==3)SelectController();else Remove();});b.Dock=DockStyle.Fill;b.Subtitle=notes[i];b.Margin=new Padding(0,0,8,8);if(i==4)b.Kind=ButtonKind.Danger;serviceButtons.Add(b);}
  Action layoutButtons=delegate{
   bool narrow=p.ClientSize.Width<700;buttons.SuspendLayout();buttons.Controls.Clear();buttons.ColumnStyles.Clear();buttons.RowStyles.Clear();
   buttons.ColumnCount=narrow?1:2;buttons.RowCount=narrow?5:3;
   for(int c=0;c<buttons.ColumnCount;c++)buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/buttons.ColumnCount));
   for(int r=0;r<buttons.RowCount;r++)buttons.RowStyles.Add(new RowStyle(SizeType.Absolute,66));
   for(int i=0;i<4;i++)buttons.Controls.Add(serviceButtons[i],narrow?0:i%2,narrow?i:i/2);
   buttons.Controls.Add(serviceButtons[4],0,narrow?4:2);if(!narrow)buttons.SetColumnSpan(serviceButtons[4],2);
   buttons.Height=buttons.RowCount*66+4;buttons.ResumeLayout(true);
  };
  layoutButtons();p.Resize+=delegate{bool narrow=p.ClientSize.Width<700;if((buttons.ColumnCount==1)!=narrow)layoutButtons();};
  var listLabel=Label("BACKUPS   /   SELECT A ROW, THEN OPEN BACKUP",40,10);listLabel.ForeColor=muted;listLabel.Padding=new Padding(0,16,0,0);p.Controls.Add(listLabel);p.Controls.SetChildIndex(listLabel,0);
  backups.Columns.Clear();backups.Columns.Add("Date / snapshot",220);backups.Columns.Add("Category",190);backups.Columns.Add("Folder",570);FitBackupColumns();backups.Dock=DockStyle.Fill;p.Controls.Add(backups);p.Controls.SetChildIndex(backups,0);RefreshBackups();
 }
 void RefreshBackups(){backups.Items.Clear();string[] sources={Path.Combine(root,"GamepadMods","_WheelInstallBackups"),Path.Combine(root,"kn_portable","HardLightProductions","FreeSpaceOpen","ControllerSetupBackups")};foreach(string folder in sources)if(Directory.Exists(folder))foreach(string path in Directory.GetDirectories(folder).OrderByDescending(x=>x)){var row=new ListViewItem(Path.GetFileName(path));row.SubItems.Add(folder.Contains("_Wheel")?"Installer snapshot":"Pilot and settings");row.SubItems.Add(path);row.Tag=path;backups.Items.Add(row);}}
 void OpenBackup(){if(backups.SelectedItems.Count==0)throw new Exception("Select a backup first.");Process.Start("explorer.exe","\""+(string)backups.SelectedItems[0].Tag+"\"");}
 void SelectController(){SetSection(1);ShowConsole();console.FocusControllerSlot();status.Text="  SELECT THE ACTIVE XINPUT SLOT, THEN APPLY WITH BACKUP. RESTART THE GAME TO SWITCH CONTROLLERS.";}
 void Repair(){
  if(Process.GetProcesses().Any(x=>x.ProcessName.StartsWith("fs2_open",StringComparison.OrdinalIgnoreCase)))throw new Exception("Close the game before repairing Flight Control.");
  if(Process.GetProcesses().Any(x=>x.ProcessName=="WheelBridge"||x.ProcessName.StartsWith("ControllerSetup",StringComparison.OrdinalIgnoreCase)))throw new Exception("Close the controller bridge and any open Configure window before repairing.");
  string addon=Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel"), setup=Path.Combine(root,"Flight Control Setup.exe");
  if(!Directory.Exists(addon)||!File.Exists(setup))throw new Exception("The installed repair files are missing. Run Setup.exe from the full Flight Control download.");
  // The installed files are the repair source, and Install replaces that folder, so copy them aside
  // first. This is data only: the repair runs here, with the same installer code as Setup, and no
  // program is started from the copy. Hangar Bay.exe itself is not copied; it is already in place.
  ReleaseAddonFiles();
  string stage=Path.Combine(Path.GetTempPath(),"FotG-Hangar-Repair-"+Guid.NewGuid().ToString("N"));
  var log=new StringBuilder();
  try{
   CopyTree(addon,Path.Combine(stage,"FotG_Gamepad_Wheel"));File.Copy(setup,Path.Combine(stage,"Setup.exe"));
   foreach(string name in new[]{"Play with Gamepad Wheel.exe","Play with Gamepad Wheel VR.exe","Configure Gamepad Wheel.exe"}){
    string file=Path.Combine(root,name);if(File.Exists(file))File.Copy(file,Path.Combine(stage,name));
   }
   Cursor=Cursors.WaitCursor;status.Text="  REPAIRING...";Application.DoEvents();
   // An install made with the version-check override records the game version; repair keeps it.
   string overrideRecord=Path.Combine(addon,"version-override.txt");
   string overridden=File.Exists(overrideRecord)?File.ReadAllText(overrideRecord).Trim():null;
   var options=new InstallOptions{GameRoot=root,PackageRoot=stage};
   if(!String.IsNullOrEmpty(overridden)){options.GameVersion=overridden;options.OverrideVersionCheck=true;}
   Installer.Run(options,line=>log.AppendLine(line),line=>log.AppendLine("WARNING: "+line));
  }catch(InstallException e){throw new Exception("Repair failed: "+e.Message+(log.Length>0?"\n\n"+log:""));}
  finally{Cursor=Cursors.Default;try{Directory.Delete(stage,true);}catch{}Maintenance();}
  MessageBox.Show("Repair complete. Previous files and pilots were backed up.\n\n"+log,"Hangar Bay");
 }
 // Close the pages that read files from the add-on folder, so it can be replaced or deleted.
 void ReleaseAddonFiles(){Clear();if(console!=null){console.Dispose();console=null;}GC.Collect();GC.WaitForPendingFinalizers();}
 static void CopyTree(string source,string destination){Directory.CreateDirectory(destination);foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));foreach(string folder in Directory.GetDirectories(source))CopyTree(folder,Path.Combine(destination,Path.GetFileName(folder)));}
 void Remove(){
  if(Process.GetProcesses().Any(x=>x.ProcessName.StartsWith("fs2_open",StringComparison.OrdinalIgnoreCase)||x.ProcessName=="WheelBridge"))throw new Exception("Close the game and controller bridge before removing Flight Control.");
  if(MessageBox.Show("Remove Flight Control files and launchers? Pilot files and backups will be kept. Restore any native pilot settings you want from the Console before removal.","Remove Flight Control",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
  ReleaseAddonFiles();
  var left=HangarProgram.RemoveInstalled(root);
  if(left.Count>0){
   MessageBox.Show("Some files could not be removed because they are in use:\n\n"+String.Join("\n",left.Take(8))+(left.Count>8?"\n...":"")+"\n\nClose any program using them and choose Remove tool again.","Remove Flight Control",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   Maintenance();return;
  }
  string self=Application.ExecutablePath;
  MessageBox.Show("Flight Control is removed. Your pilots and backups were kept.\n\nHangar Bay can't delete its own file while it is open. File Explorer will show Hangar Bay.exe: delete it after this window closes to finish.","Remove Flight Control",MessageBoxButtons.OK,MessageBoxIcon.Information);
  Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+self+"\""){UseShellExecute=true});
  Close();
 }
 void Help(){var p=Page("04   /   FIELD GUIDE");string file=GameInstall.HasMyMod(root)?Path.Combine(root,"Flight Control Console","Help.html"):Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel","Help.html");if(!File.Exists(file)){var missing=Note("The guide (Help.html) was not found in the installed Flight Control folder.\nChoose SERVICE > REPAIR SETUP to restore it.",80,muted);missing.Dock=DockStyle.Top;p.Controls.Add(missing);p.Controls.SetChildIndex(missing,0);return;}var web=new WebBrowser{Dock=DockStyle.Fill,ScriptErrorsSuppressed=true};p.Controls.Add(web);p.Controls.SetChildIndex(web,0);web.Navigate(file);}
}

internal sealed class TacticalPanel:Panel {
 public TacticalPanel(){DoubleBuffered=true;ResizeRedraw=true;SetStyle(ControlStyles.Selectable,false);AccessibleName="Decorative tactical flight grid";AccessibleRole=AccessibleRole.Graphic;}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;int w=Width,h=Height,cx=w/2,cy=h/2;
  using(var line=new Pen(Color.FromArgb(232,240,244),2.5f))using(var thin=new Pen(Color.FromArgb(150,176,188),1.2f))using(var dim=new Pen(Color.FromArgb(40,70,84),1))using(var accent=new Pen(Color.FromArgb(246,201,69),2.5f)){
   g.DrawRectangle(line,10,8,w-20,h-16);
   for(int i=1;i<8;i++){int x=10+i*(w-20)/8;g.DrawLine(dim,x,8,x,h-8);}
   for(int i=1;i<4;i++){int y=8+i*(h-16)/4;g.DrawLine(dim,10,y,w-10,y);}
   g.DrawLine(thin,10,cy,w-10,cy);g.DrawLine(thin,cx,8,cx,h-8);
   for(int i=1;i<8;i++){int x=10+i*(w-20)/8;g.DrawLine(line,x,8,x,18);g.DrawLine(line,x,h-18,x,h-8);}
   // range rings and bracket reticle (original artwork)
   g.DrawEllipse(thin,cx-70,cy-70,140,140);
   g.DrawEllipse(accent,cx-46,cy-46,92,92);
   g.DrawArc(line,cx-66,cy-66,132,132,205,56);g.DrawArc(line,cx-66,cy-66,132,132,25,56);
   g.DrawLine(line,cx-12,cy,cx+12,cy);g.DrawLine(line,cx,cy-12,cx,cy+12);
   // generic wing-pair glyph made of straight strokes
   g.DrawLine(line,cx-96,cy+26,cx-30,cy+4);g.DrawLine(line,cx+96,cy+26,cx+30,cy+4);
   g.DrawLine(line,cx-96,cy+14,cx-96,cy+38);g.DrawLine(line,cx+96,cy+14,cx+96,cy+38);
   g.DrawLine(accent,cx-132,cy-52,cx-96,cy-52);g.DrawLine(accent,cx+96,cy-52,cx+132,cy-52);
   using(var font=new Font("Consolas",10,FontStyle.Bold))using(var brush=new SolidBrush(Color.FromArgb(232,240,244)))using(var gold=new SolidBrush(Color.FromArgb(246,201,69))){if(w>=560)g.DrawString("TACTICAL FLIGHT GRID",font,brush,24,16);g.DrawString("VECTOR 01",font,brush,w-110,h-30);g.DrawString("LOCK READY",font,gold,24,h-30);}
  }
 }
}

internal enum ButtonKind { Normal, Primary, Nav, Danger }

internal sealed class VectorButton:Button {
 bool hot,down;Font subFont;
 public ButtonKind Kind;public bool Selected;public string Subtitle;
 public VectorButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;DoubleBuffered=true;Margin=new Padding(3);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);TabStop=true;}
 protected override void Dispose(bool disposing){if(disposing&&subFont!=null)subFont.Dispose();base.Dispose(disposing);}
 protected override void OnMouseEnter(EventArgs e){hot=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hot=false;down=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
 protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
 protected override bool ShowFocusCues{get{return true;}}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
  Color amber=Color.FromArgb(246,201,69),white=Color.FromArgb(240,246,248),ice=Color.FromArgb(120,200,216),red=Color.FromArgb(226,88,66);
  Color back=Color.FromArgb(10,26,36),edge=ice,text=ForeColor,sub=Color.FromArgb(170,196,206);int edgeW=1;
  if(Kind==ButtonKind.Primary){back=Color.FromArgb(64,48,8);edge=amber;edgeW=2;text=white;}
  else if(Kind==ButtonKind.Danger){back=Color.FromArgb(28,14,14);edge=Color.FromArgb(170,70,56);text=Color.FromArgb(255,160,140);sub=Color.FromArgb(214,150,138);}
  else if(Kind==ButtonKind.Nav){back=Color.FromArgb(6,16,24);edge=Color.FromArgb(62,92,106);}
  if(Selected){back=Kind==ButtonKind.Nav?Color.FromArgb(40,34,12):Color.FromArgb(46,40,16);edge=amber;edgeW=2;text=white;}
  if(hot&&Enabled){back=Kind==ButtonKind.Danger?Color.FromArgb(70,24,20):Kind==ButtonKind.Primary?Color.FromArgb(94,72,12):Color.FromArgb(24,56,70);edge=Kind==ButtonKind.Danger?red:white;edgeW=2;text=white;}
  if(down&&Enabled){back=Color.FromArgb(Math.Min(255,back.R+22),Math.Min(255,back.G+22),Math.Min(255,back.B+22));}
  if(!Enabled){back=Color.FromArgb(9,16,22);edge=Color.FromArgb(48,66,76);text=Color.FromArgb(104,124,132);sub=text;edgeW=1;}
  using(var fill=new SolidBrush(back))g.FillRectangle(fill,ClientRectangle);
  using(var pen=new Pen(edge,edgeW)){
   int o=edgeW/2;g.DrawRectangle(pen,o,o,Width-1-edgeW,Height-1-edgeW);
   if(Kind!=ButtonKind.Nav){using(var tick=new Pen(Enabled?amber:edge,3)){g.DrawLine(tick,1,1,22,1);g.DrawLine(tick,1,1,1,20);g.DrawLine(tick,Width-23,Height-2,Width-2,Height-2);g.DrawLine(tick,Width-2,Height-22,Width-2,Height-2);}}
  }
  if(Kind==ButtonKind.Nav&&Selected)using(var bar=new SolidBrush(amber))g.FillRectangle(bar,0,0,6,Height);
  if(Kind!=ButtonKind.Nav){using(var arrow=new Pen(Enabled?text:edge,2)){int x=Width-24,y=Height/2;g.DrawLines(arrow,new[]{new Point(x-5,y-6),new Point(x+1,y),new Point(x-5,y+6)});}}
  int left=Kind==ButtonKind.Nav?18:16,right=Kind==ButtonKind.Nav?8:40;
  if(String.IsNullOrEmpty(Subtitle)){TextRenderer.DrawText(g,Text,Font,new Rectangle(left,0,Width-left-right,Height),text,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);}
  else{
   if(subFont==null)subFont=new Font("Consolas",9.5f);
   int th=TextRenderer.MeasureText(g,Text,Font).Height,sh=TextRenderer.MeasureText(g,Subtitle,subFont).Height,top=(Height-(th+sh+2))/2;
   TextRenderer.DrawText(g,Text,Font,new Rectangle(left,top,Width-left-right,th),text,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
   TextRenderer.DrawText(g,Subtitle,subFont,new Rectangle(left,top+th+2,Width-left-right,sh),sub,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
  }
  if(Focused){using(var f=new Pen(white,2){DashStyle=System.Drawing.Drawing2D.DashStyle.Dash})g.DrawRectangle(f,5,5,Width-11,Height-11);}
 }
}
