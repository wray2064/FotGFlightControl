using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[StructLayout(LayoutKind.Sequential)] struct Pad { public ushort buttons; public byte lt,rt; public short lx,ly,rx,ry; }
[StructLayout(LayoutKind.Sequential)] struct State { public uint packet; public Pad pad; }
static class ConsoleAsset { public static string File(string name){string local=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name);if(System.IO.File.Exists(local))return local;return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"GamepadMods","FotG_Gamepad_Wheel","ControllerSetup",name);} }
partial class ControllerSetup : Form {
 [DllImport("xinput1_4.dll")] static extern uint XInputGetState(uint slot,out State state);
 TextBox root=new TextBox(), live=new TextBox(); ComboBox pilots=new ComboBox(), slot=new ComboBox();
 NumericUpDown sensitivity=new NumericUpDown(), deadzone=new NumericUpDown(); Timer timer=new Timer();
 CheckBox useMouse=new CheckBox(); bool calibrating; double drift; string prefs;
 JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=16777216};
 public ControllerSetup() { BuildCockpit(); Shown+=delegate{StartConsoleUpdateCheck();}; }
 public void SetEmbedded(){
  if(embeddedCockpit)return;
  embeddedCockpit=true;MinimumSize=Size.Empty;
  cockpitShell.RowStyles[0]=new RowStyle(SizeType.Absolute,0);
  FitCockpit();
 }
 string FindGame(){string p=AppDomain.CurrentDomain.BaseDirectory;while(p!=null){if(Directory.Exists(Path.Combine(p,"kn_portable")))return p;var d=Directory.GetParent(p);p=d==null?null:d.FullName;}return "";}
 void AddLabel(string text,int x,int y){Label l=new Label();l.Text=text;l.SetBounds(x,y,750,48);Controls.Add(l);l.SendToBack();}
 Button Button(string text,int x,int y,Action act){Button b=new Button();b.Text=text;b.SetBounds(x,y,200,35);b.Click+=delegate{try{act();}catch(Exception e){MessageBox.Show(e.Message,Text);}};Controls.Add(b);return b;}
 string PilotPath(){if(pilots.SelectedItem==null)throw new Exception("Select a native pilot first.");return Path.Combine(prefs,"data","players",pilots.SelectedItem+".json");}
 static string SettingsPath(string prefsRoot){string active=Path.Combine(prefsRoot,"data","GamepadMods_settings.ini");return File.Exists(active)?active:Path.Combine(prefsRoot,"data","FotG_R_settings.ini");}
 string SettingsPath(){return SettingsPath(prefs);}
 Dictionary<string,object> Read(string path){return json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));}
 Dictionary<string,object> Section(Dictionary<string,object> d,string key){return (Dictionary<string,object>)d[key];}
 void LoadPilots(){prefs=Path.Combine(root.Text,"kn_portable","HardLightProductions","FreeSpaceOpen");pilots.Items.Clear();string dir=Path.Combine(prefs,"data","players");if(!Directory.Exists(dir))return;foreach(string f in Directory.GetFiles(dir,"*.json"))pilots.Items.Add(Path.GetFileNameWithoutExtension(f));if(pilots.Items.Count>0)pilots.SelectedIndex=0;LoadControllerSlot();}
 void LoadControllerSlot(){try{string ini=SettingsPath();if(!File.Exists(ini))return;Match match=Regex.Match(File.ReadAllText(ini),@"(?im)^XInputSlot\s*=\s*([0-3])\s*$");int saved;if(match.Success&&Int32.TryParse(match.Groups[1].Value,out saved))slot.SelectedIndex=saved;}catch(IOException){}}
 void LoadPilot(){try{var s=Section(Section(Read(PilotPath()),"sections"),"settings");sensitivity.Value=Math.Max(0,Math.Min(9,Convert.ToInt32(s["joy_sensitivity"])));deadzone.Value=Math.Max(0,Math.Min(45,Convert.ToInt32(s["joy_dead_zone_size"])));useMouse.Checked=Convert.ToInt32(s["use_mouse_to_fly"])!=0;}catch(Exception e){MessageBox.Show(e.Message,Text);}}
 void Poll(){State s;bool connected=XInputGetState((uint)slot.SelectedIndex,out s)==0;status.Text=connected?"CONTROLLER ONLINE / SLOT "+slot.SelectedIndex:"CONTROLLER OFFLINE";padView.Connected=connected;padView.UpdatePad(connected?s.pad:new Pad());if(!connected){calibrating=false;live.Text="Connect a controller or select another slot.";return;}var p=s.pad;double max=new int[]{p.lx,p.ly,p.rx,p.ry}.Max(v=>Math.Abs((double)v))/32768.0;if(calibrating)drift=Math.Max(drift,max);live.Text=String.Format("{0}\r\nCENTER DRIFT  {1:F1}%",calibrating?"SAMPLING / KEEP STICKS CENTERED":"LIVE INPUT MONITOR",drift*100);} void EnsureClosed(){if(Process.GetProcesses().Any(p=>p.ProcessName.StartsWith("fs2_open",StringComparison.OrdinalIgnoreCase)))throw new Exception("Close FreeSpace Open before changing or restoring settings.");}
 public static string SetIni(string text,string section,string key,string value){var lines=new List<string>(text.Replace("\r\n","\n").Split('\n'));int start=lines.FindIndex(l=>l.Trim().Equals("["+section+"]",StringComparison.OrdinalIgnoreCase));if(start<0){lines.Add("["+section+"]");lines.Add(key+"="+value);}else{int end=start+1;while(end<lines.Count&&!lines[end].TrimStart().StartsWith("["))end++;int found=-1;for(int i=start+1;i<end;i++)if(lines[i].Split('=')[0].Trim().Equals(key,StringComparison.OrdinalIgnoreCase)){found=i;break;}if(found>=0)lines[found]=key+"="+value;else lines.Insert(end,key+"="+value);}return String.Join("\r\n",lines);}
 void Apply(){EnsureClosed();State selectedState;if(slot.SelectedIndex<0||XInputGetState((uint)slot.SelectedIndex,out selectedState)!=0)throw new Exception("Select a connected XInput controller before applying settings.");
  string path=PilotPath();var d=Read(path);if(Convert.ToInt32(d["version"])!=4)throw new Exception("This preview supports FSO pilot version 4 only.");
  string ini=SettingsPath();if(!File.Exists(ini))throw new Exception("The active game settings INI was not found; no changes made.");
  List<FlightDevice> devices=FlightDevices.List(root.Text);if(devices.Count==0)throw new Exception("FSO does not see a connected joystick. Check the controller connection and retry.");
  FlightDevice device=FlightDevices.Choose(FindForm(),devices,slot.SelectedIndex);if(device==null)return;
  var s=Section(Section(d,"sections"),"settings");s["joy_sensitivity"]=(int)sensitivity.Value;s["joy_dead_zone_size"]=(int)deadzone.Value;s["use_mouse_to_fly"]=useMouse.Checked?1:0;
  string updated=SetIni(File.ReadAllText(ini),"Input","JoystickSensitivity",sensitivity.Value.ToString());updated=SetIni(updated,"Input","JoystickDeadZone",deadzone.Value.ToString());updated=SetIni(updated,"Input","UseMouse",useMouse.Checked?"true":"false");
  string selection=json.Serialize(new Dictionary<string,object>{{"guid",device.Guid},{"id",device.Id}});
  updated=SetIni(updated,"Input","Joystick",selection);updated=SetIni(updated,"FlightControl","XInputSlot",slot.SelectedIndex.ToString());
  string b=Path.Combine(prefs,"ControllerSetupBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(b);File.Copy(path,Path.Combine(b,Path.GetFileName(path)));File.Copy(ini,Path.Combine(b,Path.GetFileName(ini)));
  try{Atomic(path,json.Serialize(d));Atomic(ini,updated);}catch{File.Copy(Path.Combine(b,Path.GetFileName(path)),path,true);File.Copy(Path.Combine(b,Path.GetFileName(ini)),ini,true);throw;}
  MessageBox.Show("Flight controller saved: XInput "+slot.SelectedIndex+" / "+device.Name+" (SDL "+device.Id+").\nRestart the game to use it. Backup: "+b,Text);
 }
 static void Atomic(string path,string text){string temp=path+".controller-tmp";File.WriteAllText(temp,text,new System.Text.UTF8Encoding(false));try{File.Replace(temp,path,null);}finally{if(File.Exists(temp))File.Delete(temp);}}
 void Restore(){EnsureClosed();string path=PilotPath();string parent=Path.Combine(prefs,"ControllerSetupBackups");if(!Directory.Exists(parent))throw new Exception("No backups found.");string activeName=Path.GetFileName(SettingsPath());string b=Directory.GetDirectories(parent).OrderByDescending(v=>v).FirstOrDefault(v=>File.Exists(Path.Combine(v,Path.GetFileName(path)))&&(File.Exists(Path.Combine(v,activeName))||File.Exists(Path.Combine(v,"FotG_R_settings.ini"))));if(b==null)throw new Exception("No backup for this pilot.");string savedName=File.Exists(Path.Combine(b,activeName))?activeName:"FotG_R_settings.ini";string ini=Path.Combine(prefs,"data",savedName);string undo=Path.Combine(parent,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-before-restore");Directory.CreateDirectory(undo);File.Copy(path,Path.Combine(undo,Path.GetFileName(path)));File.Copy(ini,Path.Combine(undo,savedName));Atomic(path,File.ReadAllText(Path.Combine(b,Path.GetFileName(path))));Atomic(ini,File.ReadAllText(Path.Combine(b,savedName)));LoadPilot();MessageBox.Show("Restored "+b,Text);}
 [STAThread] static int Main(string[] args){if(args.Contains("--reserve-wheel"))return ReserveWheelCli(args);if(args.Contains("--apply-custom-axes"))return ApplyCustomAxesCli(args);if(args.Contains("--self-test")){string t="[Input]\r\nJoystickSensitivity=9\r\n[Audio]\r\nJoystickSensitivity=7";string r=SetIni(t,"Input","JoystickSensitivity","5");if(!r.Contains("JoystickSensitivity=5")||!r.Contains("[Audio]\r\nJoystickSensitivity=7"))return 1;if(!SetIni("[Audio]\nVolume=1","Input","UseMouse","false").Contains("[Input]\r\nUseMouse=false"))return 2;if(!WheelSelfTest())return 3;if(!Updater.SelfTest())return 4;File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),"PASS: INI section isolation, setting replacement, missing section insertion, wheel button isolation, macro label escaping. No game settings changed.");return 0;}Application.EnableVisualStyles();Application.Run(new ControllerSetup());return 0;}
}
