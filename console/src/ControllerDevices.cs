using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// FSO uses SDL for flight axes, while the wheel bridge uses XInput. Identify
// both sides before saving a controller selection; an SDL GUID alone can match
// several identical pads, so the device index is saved with it.
sealed class FlightDevice {
 public int Id, PlayerIndex;
 public string Guid, Name;
 public override string ToString(){return (Name??"Unknown joystick")+"  /  SDL "+Id+(PlayerIndex>=0?"  /  XInput "+PlayerIndex:"");}
}

static class FlightDevices {
 [StructLayout(LayoutKind.Sequential)] struct SdlGuid {public ulong A,B;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool SetDllDirectory(string path);
 [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_Init")] static extern bool S3Init(uint flags);
 [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetJoysticks")] static extern IntPtr S3List(out int count);
 [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetJoystickGUIDForID")] static extern SdlGuid S3Guid(uint id);
 [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GUIDToString")] static extern void S3GuidText(SdlGuid guid,StringBuilder text,int length);
 [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetJoystickNameForID")] static extern IntPtr S3Name(uint id);
 [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetJoystickPlayerIndexForID")] static extern int S3Player(uint id);
 [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_free")] static extern void S3Free(IntPtr pointer);
 [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_Quit")] static extern void S3Quit();
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_Init")] static extern int S2Init(uint flags);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_NumJoysticks")] static extern int S2Count();
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_JoystickGetDeviceGUID")] static extern SdlGuid S2Guid(int index);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_JoystickGetGUIDString")] static extern void S2GuidText(SdlGuid guid,StringBuilder text,int length);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_JoystickNameForIndex")] static extern IntPtr S2Name(int index);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_JoystickGetDevicePlayerIndex")] static extern int S2Player(int index);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_Quit")] static extern void S2Quit();
 static string GameVersion(string root){
  string overridePath=Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel","version-override.txt");
  if(File.Exists(overridePath)){string value=File.ReadAllText(overridePath).Trim();
   if(Regex.IsMatch(value,@"^\d+(\.\d+){1,3}$")&&File.Exists(Path.Combine(root,"kn_portable","Library","FotG_R","FotG_R-"+value,"mod.json")))return value;}
  foreach(string value in new[]{"1.1.3","1.1.2","1.1.1","1.1.0"})
   if(File.Exists(Path.Combine(root,"kn_portable","Library","FotG_R","FotG_R-"+value,"mod.json")))return value;
  return null;
 }
 public static List<FlightDevice> List(string gameRoot){
  EngineInfo engine=GameInstall.FindEngine(gameRoot,GameVersion(gameRoot));
  if(engine==null)throw new Exception("The FSO engine was not found. Launch the game once from Knossos, then retry.");
  string dllDir=Path.GetDirectoryName(engine.Exe);
  SetDllDirectory(dllDir);
  try{return engine.Sdl3?List3():List2();}
  finally{SetDllDirectory(null);}
 }
 static List<FlightDevice> List3(){
  if(!S3Init(0x200))throw new Exception("SDL3 could not initialize the joystick subsystem.");
  try{var devices=new List<FlightDevice>();int count;IntPtr ids=S3List(out count);
   try{if(ids==IntPtr.Zero&&count>0)throw new Exception("SDL3 could not enumerate joysticks.");
    for(int i=0;i<count;i++){uint id=(uint)Marshal.ReadInt32(ids,4*i);var guid=new StringBuilder(64);S3GuidText(S3Guid(id),guid,guid.Capacity);
     devices.Add(new FlightDevice{Id=i,Guid=guid.ToString(),Name=Marshal.PtrToStringAnsi(S3Name(id)),PlayerIndex=S3Player(id)});}
   }finally{if(ids!=IntPtr.Zero)S3Free(ids);}return devices;
  }finally{S3Quit();}
 }
 static List<FlightDevice> List2(){
  if(S2Init(0x200)!=0)throw new Exception("SDL2 could not initialize the joystick subsystem.");
  try{var devices=new List<FlightDevice>();for(int i=0;i<S2Count();i++){var guid=new StringBuilder(64);S2GuidText(S2Guid(i),guid,guid.Capacity);
   devices.Add(new FlightDevice{Id=i,Guid=guid.ToString(),Name=Marshal.PtrToStringAnsi(S2Name(i)),PlayerIndex=S2Player(i)});}return devices;
  }finally{S2Quit();}
 }
 public static FlightDevice Choose(IWin32Window owner,List<FlightDevice> devices,int slot){
  var matches=devices.FindAll(d=>d.PlayerIndex==slot);
  if(matches.Count==1)return matches[0];
  using(var dialog=new Form{Text="Select FSO flight controller",Width=520,Height=225,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
   var prompt=new Label{Text="FSO could not uniquely match XInput slot "+slot+". Select the same physical controller for flight:",Dock=DockStyle.Top,Height=62,Padding=new Padding(15,12,15,0)};
   var choice=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(15),Height=36};
   choice.Items.AddRange(devices.ToArray());if(devices.Count>0)choice.SelectedIndex=0;
   var accept=new Button{Text="USE THIS CONTROLLER",Dock=DockStyle.Bottom,Height=48,DialogResult=DialogResult.OK};
   dialog.Controls.Add(accept);dialog.Controls.Add(choice);dialog.Controls.Add(prompt);dialog.AcceptButton=accept;
   return dialog.ShowDialog(owner)==DialogResult.OK?choice.SelectedItem as FlightDevice:null;
  }
 }
}
