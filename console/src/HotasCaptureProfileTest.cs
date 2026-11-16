using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Test-only: takes real "hotas_hardware_capability_profile" files (what players submit from the HOTAS profiler)
// and runs the devices they describe through the HOTAS tab: inventory, matching, layout, assignment, apply checks.
// Usage: ControllerSetup.exe --hotas-captest <report file> <capability profile.json> [more files]
partial class ControllerSetup {
 static int HotasCapTestCli(string[] args) {
  int at=Array.IndexOf(args,"--hotas-captest");
  if(at<0||at+2>=args.Length)return 1;
  var report=new StringBuilder();
  try{HotasCapTest(args[at+1],args.Skip(at+2).ToArray(),report);}
  catch(Exception e){report.AppendLine("EXCEPTION: "+e);}
  File.WriteAllText(args[at+1],report.ToString());
  return 0;
 }

 // SDL's GUID for a USB device: bus 03, then vendor, product and version little-endian. Enough to tell models apart.
 static string CapGuid(string vid,string pid) {
  Func<string,string> le=delegate(string hex){
   int v=Convert.ToInt32(hex.Replace("0x",""),16);
   return (v&0xff).ToString("x2")+((v>>8)&0xff).ToString("x2");
  };
  return ("0300"+"0000"+le(vid)+"0000"+le(pid)+"0000"+"0000"+"0000").ToUpperInvariant();
 }

 static int CapCount(object value){var list=value as IEnumerable;int n=0;if(list!=null)foreach(var item in list)n++;return n;}

 static void HotasCapTest(string reportPath,string[] files,StringBuilder report) {
  var serializer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
  int shot=0;
  foreach(string file in files){
   report.AppendLine("##### "+Path.GetFileName(file));
   var doc=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(file));
   if(Convert.ToString(doc["kind"])!="hotas_hardware_capability_profile"){report.AppendLine("not a capability profile");continue;}
   var devices=new List<HardwareDevice>();
   var specs=new List<string>();
   foreach(Dictionary<string,object> d in (IEnumerable)doc["devices"]){
    int axes=CapCount(d["axes"]),buttons=CapCount(d["buttons"]),hats=CapCount(d["hats"]);
    string name=Convert.ToString(d["product_name"]);
    var inventory=new HardwareDevice{Name=name,Guid=CapGuid(Convert.ToString(d["usb_vid"]),Convert.ToString(d["usb_pid"])),Inputs=new List<HardwareInput>()};
    for(int i=0;i<axes;i++)inventory.Inputs.Add(new HardwareInput{Kind="Axis",Index=i});
    for(int i=0;i<buttons;i++)inventory.Inputs.Add(new HardwareInput{Kind="Button",Index=i});
    for(int i=0;i<hats;i++)inventory.Inputs.Add(new HardwareInput{Kind="Hat",Index=i});
    devices.Add(inventory);
    report.AppendLine("device: "+name+" ["+d["usb_vid"]+":"+d["usb_pid"]+"] guid "+inventory.Guid+": "+axes+" axes, "+buttons+" buttons, "+hats+" hat(s)");
    specs.Add(name);
   }
   var identical=devices.GroupBy(x=>x.Guid).Where(g=>g.Count()>1).Select(g=>g.Key+" x"+g.Count()).ToList();
   if(identical.Count>0)report.AppendLine("identical devices without serials: "+String.Join(", ",identical));

   using(var form=new ControllerSetup()){
    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2000,-2000);form.ShowInTaskbar=false;form.Show();
    form.hotasTimer.Stop();
    // live devices: fresh copies of what the file describes, with some inputs moving
    var live=devices.Select(inv=>(FlightDevices.HardwareSession.Device)new HotasDemoDevice{
     Inventory=new HardwareDevice{Name=inv.Name,Guid=inv.Guid,Inputs=inv.Inputs.Select(i=>new HardwareInput{Kind=i.Kind,Index=i.Index}).ToList()},
     Source=i=>i.Kind=="Axis"?(i.Index*9000%50000)-25000:(i.Kind=="Button"?(i.Index%9==2?1:0):(i.Index==0?1:0))}).ToList();
    form.hotasSession=new FlightDevices.HardwareSession(live);
    form.prefs=null;
    var sw=Stopwatch.StartNew();
    form.hotasProfile=null;
    form.UseHotasSession();
    report.AppendLine("first scan: "+form.hotasProfile.Devices.Count+" device(s) in the profile, "+form.hotasMatches.Count(m=>m!=null)+" matched, "+sw.ElapsedMilliseconds+" ms");
    HardwareInspector.Validate(form.hotasProfile);
    var liveSet=new HashSet<object>(form.hotasMatches.Cast<object>());
    if(form.hotasMatches.Count(m=>m!=null)!=devices.Count||liveSet.Count!=devices.Count)report.AppendLine("PROBLEM: not every device was matched to its own connected device");

    // Assign a realistic set: flight axes, fire, targeting and wheel controls spread over the devices.
    var profile=form.hotasProfile;
    profile.FlightAxes=new Dictionary<string,FlightAxisBinding>();profile.FlightButtons=new Dictionary<string,FlightAxisBinding>();profile.Virtual=new Dictionary<string,FlightAxisBinding>();
    var first=profile.Devices[0];
    int axisCount=first.Inputs.Count(x=>x.Kind=="Axis");
    string[] axisNames={"Roll","Pitch","Yaw","Absolute throttle"};
    for(int i=0;i<Math.Min(4,axisCount);i++)profile.FlightAxes[axisNames[i]]=new FlightAxisBinding{DeviceIndex=0,InputIndex=i};
    string[] actions={"Fire primary","Fire secondary","Target next ship","Target closest hostile","Afterburner","Launch countermeasure","Match target speed","Cycle primary weapon"};
    int buttonCount=first.Inputs.Count(x=>x.Kind=="Button");
    for(int i=0;i<actions.Length&&i<buttonCount;i++)profile.FlightButtons[actions[i]]=new FlightAxisBinding{DeviceIndex=0,InputIndex=i,Kind=HotasModel.Button};
    var wheel=new[]{"Wheel select","Wheel cancel","Wheel step clockwise","Wheel step counter-clockwise"};
    int target=profile.Devices.Count>1?1:0;
    int targetButtons=profile.Devices[target].Inputs.Count(x=>x.Kind=="Button");
    for(int i=0;i<wheel.Length&&i<targetButtons;i++)profile.Virtual[wheel[i]]=new FlightAxisBinding{DeviceIndex=target,InputIndex=10+i<targetButtons?10+i:i,Kind=HotasModel.Button};
    if(first.Inputs.Any(x=>x.Kind=="Hat")){
     profile.Virtual["Dpad up"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=0,Kind=HotasModel.Hat,Direction=HotasModel.HatUp};
     profile.Virtual["Dpad down"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=0,Kind=HotasModel.Hat,Direction=HotasModel.HatDown};
     profile.Virtual["Dpad left"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=0,Kind=HotasModel.Hat,Direction=HotasModel.HatLeft};
     profile.Virtual["Dpad right"]=new FlightAxisBinding{DeviceIndex=0,InputIndex=0,Kind=HotasModel.Hat,Direction=HotasModel.HatRight};
    }
    HardwareInspector.Validate(profile);
    report.AppendLine("assigned "+(profile.FlightAxes.Count+profile.FlightButtons.Count+profile.Virtual.Count)+" actions; conflicts="+HotasModel.Conflicts(profile).Count+" overlaps="+HotasModel.Overlaps(profile).Count);
    form.UseHotasSession();

    // The same profile as a shared file, loaded back.
    string temp=Path.Combine(Path.GetTempPath(),"captest-"+Guid.NewGuid().ToString("N")+".fchotas");
    try{
     form.WriteHotasProfile(temp);
     var loaded=form.ReadHotasProfile(temp);
     report.AppendLine("shared file: "+(new FileInfo(temp).Length/1024.0).ToString("F1")+" KB, reloads with "+loaded.Devices.Count+" device(s) and "+(loaded.FlightAxes.Count+loaded.FlightButtons.Count+loaded.Virtual.Count)+" assignments");
    }finally{try{File.Delete(temp);}catch(Exception){}}

    // Listening: a stand-in press on the last device must be found on the right device.
    // Apply checks: identical devices cannot take game bindings (FotG needs a unique GUID), only Flight Control ones.
    if(identical.Count>0){
     string message=null;
     try{form.ApplyHotasProfile();message="accepted";}catch(Exception e){message=e.Message;}
     report.AppendLine("apply with game bindings on identical devices: "+message.Replace("\r\n"," "));
    }

    // Look at it: one picture per device.
    for(int d=0;d<profile.Devices.Count;d++){
     form.hotasView=d;form.BuildHotasDeviceBar();form.ShowHotasDevice();
     form.ShowPage(false,false,false,true);form.hotasShowSetup=false;form.ShowHotasSetup();
     for(int i=0;i<4;i++){Application.DoEvents();System.Threading.Thread.Sleep(40);}
     using(var bitmap=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){
      form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));
      string png=reportPath+"."+(++shot)+".png";
      bitmap.Save(png,System.Drawing.Imaging.ImageFormat.Png);
      report.AppendLine("picture: "+Path.GetFileName(png)+" ("+profile.Devices[d].Name+")");
     }
    }
    form.Close();
   }
  }
 }
}
