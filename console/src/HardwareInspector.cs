using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

sealed class HardwareProfile {
 // Marks a file as a Flight Control HOTAS profile so a stray JSON is refused with a clear message. Older exports lack it.
 public string Format {get;set;}
 // How the four flight axes are handled: Game (the engine's own, with one global dead zone and sensitivity) or Precision
 // (Flight Control shapes each axis and steers through the game script). Null means Game.
 public string AxisMode {get;set;}
 public int SchemaVersion {get;set;}
 public string Backend {get;set;}
 public List<HardwareDevice> Devices {get;set;}
 public Dictionary<string,FlightAxisBinding> FlightAxes {get;set;}
 public Dictionary<string,FlightAxisBinding> FlightButtons {get;set;}
 // Flight Control's own controls (wheels, gestures, menus), driven through a virtual gamepad by the bridge.
 public Dictionary<string,FlightAxisBinding> Virtual {get;set;}
 public List<DeviceLayout> Layouts {get;set;}
}

// Version 1 profiles stored the SDL index as AxisIndex for axes and buttons alike.
// AxisIndex stays readable so they still load; it is never written.
// Kind names the input: Button, Axis, Hat, or AxisButton (an axis used as a button). Null means the
// group's default (Axis for FlightAxes, Button for FlightButtons). Direction is a hat direction
// (1 up, 2 right, 4 down, 8 left) or the +1/-1 side of an axis. Invert flips an axis.
sealed class FlightAxisBinding {
 public int DeviceIndex {get;set;}
 public int InputIndex {get;set;}
 public string Kind {get;set;}
 public int Direction {get;set;}
 public bool Invert {get;set;}
 // Dead zone, saturation, curve and calibration for this axis. Null means the defaults. Used by precision axes and shaped Flight Control axes.
 public HotasAxisSettings Shape {get;set;}
 [ScriptIgnore] public int AxisIndex {get{return InputIndex;}set{InputIndex=value;}}
}

// Preview arrangement for one device. Positions and sizes are fractions of the canvas (0-1).
// BackgroundImage is a local, user-provided path and is not meant to be shared.
sealed class DeviceLayout {
 public int DeviceIndex {get;set;}
 public string BackgroundImage {get;set;}
 public List<LayoutWidget> Widgets {get;set;}
}

sealed class LayoutWidget {
 public string Kind {get;set;}
 public int Index {get;set;}
 public double X {get;set;}
 public double Y {get;set;}
 public double Width {get;set;}
 public double Height {get;set;}
 public string Label {get;set;}
}

sealed class HardwareDevice {
 public string Name {get;set;}
 public string Guid {get;set;}
 public string Serial {get;set;}
 public List<HardwareInput> Inputs {get;set;}
}

sealed class HardwareInput {
 public string Kind {get;set;}
 public int Index {get;set;}
 public string Label {get;set;}
 public string Verification {get;set;}
}

static partial class FlightDevices {
 const string Sdl3 = "SDL3.dll";
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr SDL_OpenJoystick(uint id);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern void SDL_CloseJoystick(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr SDL_GetJoystickSerial(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GetNumJoystickAxes(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GetNumJoystickButtons(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GetNumJoystickHats(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern short SDL_GetJoystickAxis(IntPtr joystick,int index);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)]
 static extern bool SDL_GetJoystickButton(IntPtr joystick,int index);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern byte SDL_GetJoystickHat(IntPtr joystick,int index);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)]
 static extern bool SDL_JoystickConnected(IntPtr joystick);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern void SDL_PumpEvents();
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] static extern bool SDL_PollEvent(IntPtr e);
 [DllImport(Sdl3,CallingConvention=CallingConvention.Cdecl)] static extern uint SDL_GetJoystickID(IntPtr joystick);

 internal sealed class HardwareSession : IDisposable {
  internal class Device {
   internal IntPtr Handle;
   internal HardwareDevice Inventory;
   internal uint InstanceId;
   internal HardwareSession Owner;
   // A button pressed within the last moments, even if it has been released again since (an encoder detent).
   internal virtual bool RecentPulse(HardwareInput input){return Owner!=null&&input.Kind=="Button"&&Owner.PulseRecent(InstanceId,input.Index);}
   internal virtual bool Connected {get{return SDL_JoystickConnected(Handle);}}
   internal virtual int ReadRaw(HardwareInput input) {
    switch(input.Kind) {
     case "Axis": return SDL_GetJoystickAxis(Handle,input.Index);
     case "Button": return SDL_GetJoystickButton(Handle,input.Index)?1:0;
     default: return SDL_GetJoystickHat(Handle,input.Index)&15;
    }
   }

   internal string Read(HardwareInput input) {
    int raw=ReadRaw(input);
    switch(input.Kind) {
     case "Axis": return raw.ToString();
     case "Button": return raw!=0?"Pressed":"Released";
     default: return Hats[raw];
    }
   }
   static readonly string[] Hats={"Centered","Up","Right","Up-right","Down","—","Down-right","—","Left","Up-left","—","—","Down-left","—","—","—"};
  }

  internal readonly List<Device> Devices=new List<Device>();
  bool initialized;
  readonly Dictionary<long,long> pulseAt=new Dictionary<long,long>();
  readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
  IntPtr eventBuffer=IntPtr.Zero;
  bool PulseRecent(uint instance,int button){long at;return pulseAt.TryGetValue(((long)instance<<16)|(uint)button,out at)&&clock.ElapsedMilliseconds-at<150;}

  internal HardwareSession(string gameRoot) {
   if(!GameInstall.HasMyMod(gameRoot))
    throw new InvalidOperationException("Select your FotG 2 installation in the Console first.");
   EngineInfo engine=GameInstall.FindEngine(gameRoot,null);
   if(engine==null || !engine.Sdl3)
    throw new InvalidOperationException("The FotG 2 SDL3 engine was not found.");
   if(!SetDllDirectory(Path.GetDirectoryName(engine.Exe)))
    throw new InvalidOperationException("Could not select the FotG 2 engine folder.");
   try {
    if(!S3Init(0x200))throw new InvalidOperationException("SDL3 could not initialize joystick input.");
    initialized=true;
    int count;
    IntPtr ids=S3List(out count);
    if(ids==IntPtr.Zero)throw new InvalidOperationException("SDL3 could not enumerate joysticks.");
    try {
     for(int i=0;i<count;i++)Open((uint)Marshal.ReadInt32(ids,i*4));
    }finally{S3Free(ids);}
   }catch {
    Dispose();
    throw;
   }finally{SetDllDirectory(null);}
  }

  void Open(uint id) {
   string name=Marshal.PtrToStringAnsi(S3Name(id))??"Unknown joystick";
   IntPtr handle=SDL_OpenJoystick(id);
   if(handle==IntPtr.Zero)throw new InvalidOperationException("Could not open "+name+". Reconnect it and scan again.");
   var device=new Device{Handle=handle,InstanceId=SDL_GetJoystickID(handle),Owner=this};
   Devices.Add(device);
   var guid=new StringBuilder(33);
   S3GuidText(S3Guid(id),guid,guid.Capacity);
   device.Inventory=new HardwareDevice{
    Name=name,Guid=guid.ToString(),Serial=Marshal.PtrToStringAnsi(SDL_GetJoystickSerial(handle)),
    Inputs=new List<HardwareInput>()
   };
   AddInputs(device.Inventory,"Axis",SDL_GetNumJoystickAxes(handle));
   AddInputs(device.Inventory,"Button",SDL_GetNumJoystickButtons(handle));
   AddInputs(device.Inventory,"Hat",SDL_GetNumJoystickHats(handle));
  }

  static void AddInputs(HardwareDevice device,string kind,int count) {
   if(count<0)throw new InvalidOperationException("Could not read "+kind.ToLowerInvariant()+" capabilities for "+device.Name+".");
   for(int i=0;i<count;i++)device.Inputs.Add(new HardwareInput{Kind=kind,Index=i});
  }

  // A session over standIn devices, for previews and tests: no SDL involved.
  internal HardwareSession(IEnumerable<Device> standIn){Devices.AddRange(standIn);}

  internal void Poll(){
   if(!initialized)return;
   SDL_PumpEvents();
   if(eventBuffer==IntPtr.Zero)eventBuffer=Marshal.AllocHGlobal(128);
   // Remember when each button last went down, so a pulse shorter than a poll still shows up.
   while(SDL_PollEvent(eventBuffer)){
    if((uint)Marshal.ReadInt32(eventBuffer,0)!=0x603)continue;
    pulseAt[((long)(uint)Marshal.ReadInt32(eventBuffer,16)<<16)|(uint)Marshal.ReadByte(eventBuffer,20)]=clock.ElapsedMilliseconds;
   }
  }

  public void Dispose() {
   foreach(var device in Devices)if(device.Handle!=IntPtr.Zero)SDL_CloseJoystick(device.Handle);
   Devices.Clear();
   if(initialized){S3Quit();initialized=false;}
   if(eventBuffer!=IntPtr.Zero){Marshal.FreeHGlobal(eventBuffer);eventBuffer=IntPtr.Zero;}
  }
 }
}

// Rules for saving and loading a hardware profile.
static class HardwareInspector {
 internal const int CurrentSchema=2;

 static void ValidateLayouts(HardwareProfile value) {
  if(value.Layouts==null)return;
  var seen=new HashSet<int>();
  foreach(var layout in value.Layouts){
   if(layout==null||layout.DeviceIndex<0||layout.DeviceIndex>=value.Devices.Count||!seen.Add(layout.DeviceIndex)||layout.Widgets==null||layout.Widgets.Count>1100)
    throw new InvalidDataException("The profile contains an invalid preview layout.");
   var inputs=value.Devices[layout.DeviceIndex].Inputs;
   foreach(var widget in layout.Widgets)
    if(widget==null||!inputs.Any(i=>i.Kind==widget.Kind&&i.Index==widget.Index)||
     !(widget.X>=0&&widget.X<=1&&widget.Y>=0&&widget.Y<=1)||!(widget.Width>0&&widget.Width<=1&&widget.Height>0&&widget.Height<=1))
     throw new InvalidDataException("The profile contains an invalid preview widget.");
  }
 }

 internal static void Validate(HardwareProfile value) {
  if(value==null||value.SchemaVersion<1||value.SchemaVersion>CurrentSchema||value.Backend!="SDL3"||value.Devices==null||value.Devices.Count==0)
   throw new InvalidDataException("Select a nonempty Flight Control SDL3 hardware profile.");
  // A profile may come from anyone: keep every part of it within sane bounds.
  if(value.Devices.Count>16)throw new InvalidDataException("The hardware profile lists too many devices.");
  Func<string,int,bool> plain=(text,max)=>text==null||(text.Length<=max&&!text.Any(Char.IsControl));
  foreach(var device in value.Devices) {
   if(device==null||String.IsNullOrWhiteSpace(device.Name)||!plain(device.Name,128)||!plain(device.Serial,128)||device.Guid==null||
    device.Guid.Length!=32||!device.Guid.All(Uri.IsHexDigit)||device.Inputs==null||device.Inputs.Count>1024)
    throw new InvalidDataException("The hardware profile contains an invalid device.");
   var keys=new HashSet<string>();
   foreach(var input in device.Inputs)
    if(input==null||input.Index<0||input.Index>4095||(input.Kind!="Axis"&&input.Kind!="Button"&&input.Kind!="Hat")||
     !plain(input.Label,64)||!plain(input.Verification,32)||!keys.Add(input.Kind+input.Index))
     throw new InvalidDataException("The hardware profile contains an invalid or duplicate input.");
  }
  if(value.AxisMode!=null&&value.AxisMode!="Game"&&value.AxisMode!="Precision")throw new InvalidDataException("The hardware profile has an unknown axis mode.");
  HotasModel.ValidateBindings(value);
  ValidateLayouts(value);
 }
}
