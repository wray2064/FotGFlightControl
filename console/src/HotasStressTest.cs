using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Test-only: how the HOTAS tab copes with profiles that have dozens, hundreds or thousands of inputs.
// Usage: ControllerSetup.exe --hotas-stresstest <output file>
partial class ControllerSetup {
 static int HotasStressTestCli(string[] args) {
  int at=Array.IndexOf(args,"--hotas-stresstest");
  if(at<0||at+1>=args.Length)return 1;
  var report=new StringBuilder();
  StressShots=args[at+1];
  try{HotasStressTest(report);}
  catch(Exception e){report.AppendLine("EXCEPTION: "+e);}
  File.WriteAllText(args[at+1],report.ToString());
  return 0;
 }

 static string StressShots;
 sealed class StressDevice { public string Name; public int Axes,Buttons,Hats; }

 static HardwareDevice StressInventory(StressDevice d,int index) {
  var device=new HardwareDevice{Name=d.Name,Guid=index.ToString("X2").PadLeft(2,'0').PadRight(32,'A'),Inputs=new List<HardwareInput>()};
  for(int i=0;i<d.Axes;i++)device.Inputs.Add(new HardwareInput{Kind="Axis",Index=i});
  for(int i=0;i<d.Buttons;i++)device.Inputs.Add(new HardwareInput{Kind="Button",Index=i});
  for(int i=0;i<d.Hats;i++)device.Inputs.Add(new HardwareInput{Kind="Hat",Index=i});
  return device;
 }

 static void HotasStressTest(StringBuilder report) {
  var cases=new List<KeyValuePair<string,StressDevice[]>>{
   new KeyValuePair<string,StressDevice[]>("Freedom 2.4 (1 device)",new[]{new StressDevice{Name="Stick",Axes=4,Buttons=10,Hats=1}}),
   new KeyValuePair<string,StressDevice[]>("Stick and throttle, ~50 inputs each",new[]{new StressDevice{Name="Stick",Axes=3,Buttons=19,Hats=1},new StressDevice{Name="Throttle",Axes=6,Buttons=32,Hats=2}}),
   new KeyValuePair<string,StressDevice[]>("Button box, 128 buttons",new[]{new StressDevice{Name="Button box",Axes=0,Buttons=128,Hats=0}}),
   new KeyValuePair<string,StressDevice[]>("Big rig: 6 devices, 450 inputs",new[]{
    new StressDevice{Name="Stick",Axes=8,Buttons=32,Hats=4},new StressDevice{Name="Throttle",Axes=8,Buttons=64,Hats=4},
    new StressDevice{Name="Pedals",Axes=3,Buttons=2,Hats=0},new StressDevice{Name="Box 1",Axes=0,Buttons=128,Hats=0},
    new StressDevice{Name="Box 2",Axes=0,Buttons=128,Hats=0},new StressDevice{Name="Panel",Axes=4,Buttons=64,Hats=0}}),
   new KeyValuePair<string,StressDevice[]>("Limit: 16 devices, 1000 buttons each",Enumerable.Range(0,16).Select(i=>new StressDevice{Name="Device "+i,Axes=8,Buttons=1000,Hats=15}).ToArray())
  };
  var serializer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
  foreach(var test in cases){
   report.AppendLine("== "+test.Key);
   var list=test.Value;
   var inventories=list.Select((d,i)=>StressInventory(d,i)).ToList();
   int inputs=inventories.Sum(d=>d.Inputs.Count);
   var profile=new HardwareProfile{SchemaVersion=HardwareInspector.CurrentSchema,Backend="SDL3",Devices=inventories,
    FlightAxes=new Dictionary<string,FlightAxisBinding>(),FlightButtons=new Dictionary<string,FlightAxisBinding>(),Virtual=new Dictionary<string,FlightAxisBinding>(),
    Layouts=new List<DeviceLayout>()};
   // Assign as many actions as there are inputs to give them, spread over the devices.
   int cursor=0;
   Func<int,FlightAxisBinding> nextButton=delegate(int device){
    var d=inventories[device];var b=d.Inputs.Where(x=>x.Kind=="Button").ElementAtOrDefault(cursor++%Math.Max(1,list[device].Buttons));
    return b==null?null:new FlightAxisBinding{DeviceIndex=device,InputIndex=b.Index,Kind=HotasModel.Button};
   };
   int dev=0;
   foreach(string action in HotasButtonActions.Keys){
    var binding=nextButton(dev%Math.Min(list.Length,4));
    if(binding==null)break;
    // keep inputs unique across native actions
    if(profile.FlightButtons.Values.Any(x=>x.DeviceIndex==binding.DeviceIndex&&x.InputIndex==binding.InputIndex))continue;
    profile.FlightButtons[action]=binding;dev++;
   }
   int axisDev=0;
   foreach(string axis in new[]{"Pitch","Yaw","Roll","Absolute throttle"}){
    var d=inventories[axisDev%Math.Min(list.Length,4)];var a=d.Inputs.FirstOrDefault(x=>x.Kind=="Axis"&&!profile.FlightAxes.Values.Any(y=>y.DeviceIndex==axisDev%Math.Min(list.Length,4)&&y.InputIndex==x.Index));
    if(a!=null)profile.FlightAxes[axis]=new FlightAxisBinding{DeviceIndex=axisDev%Math.Min(list.Length,4),InputIndex=a.Index};
    axisDev++;
   }
   int vb=0;
   foreach(string name in HotasPad.WheelOnlyControls.Concat(HotasPad.ButtonBits.Keys)){
    var d=inventories[vb%list.Length];var b=d.Inputs.Where(x=>x.Kind=="Button").ElementAtOrDefault((vb*7+3)%Math.Max(1,list[vb%list.Length].Buttons));
    if(b!=null)profile.Virtual[name]=new FlightAxisBinding{DeviceIndex=vb%list.Length,InputIndex=b.Index,Kind=HotasModel.Button};
    vb++;
   }
   int assigned=profile.FlightAxes.Count+profile.FlightButtons.Count+profile.Virtual.Count;

   var sw=Stopwatch.StartNew();
   string text=serializer.Serialize(profile);
   long tSerialize=sw.ElapsedMilliseconds;sw.Restart();
   report.AppendLine("inputs="+inputs+" devices="+list.Length+" assigned="+assigned+" file="+(text.Length/1024)+" KB serialize="+tSerialize+" ms");
   // What loading someone's file does.
   string file=Path.Combine(Path.GetTempPath(),"stress-"+Guid.NewGuid().ToString("N")+".fchotas");
   File.WriteAllText(file,text);
   try{
    using(var form=new ControllerSetup()){
     form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2000,-2000);form.ShowInTaskbar=false;form.Show();
     form.hotasTimer.Stop();
     var devices=inventories.Select(inv=>(FlightDevices.HardwareSession.Device)new HotasDemoDevice{Inventory=inv,Source=i=>i.Kind=="Axis"?(i.Index*4000%60000)-30000:(i.Kind=="Button"?(i.Index%7==0?1:0):0)}).ToList();
     form.hotasSession=new FlightDevices.HardwareSession(devices);
     form.prefs=null;
     sw.Restart();
     HardwareProfile loaded;
     try{loaded=form.ReadHotasProfile(file);}
     catch(Exception e){report.AppendLine("load refused: "+e.Message);continue;}
     report.AppendLine("load (parse, validate, strip) = "+sw.ElapsedMilliseconds+" ms");
     sw.Restart();
     form.hotasProfile=loaded;
     form.UseHotasSession();
     report.AppendLine("match devices, build chips and list = "+sw.ElapsedMilliseconds+" ms (matched "+form.hotasMatches.Count(m=>m!=null)+"/"+form.hotasMatches.Count+")");
     long worstPaint=0,worstRebuild=0;int worstDevice=0;
     for(int d=0;d<loaded.Devices.Count&&d<16;d++){
      form.hotasView=d;
      sw.Restart();form.ShowHotasDevice();long rebuild=sw.ElapsedMilliseconds;
      sw.Restart();
      using(var bmp=new Bitmap(form.hotasCanvas.Width,form.hotasCanvas.Height))form.hotasCanvas.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));
      long paint=sw.ElapsedMilliseconds;
      if(paint>worstPaint){worstPaint=paint;worstDevice=d;}
      worstRebuild=Math.Max(worstRebuild,rebuild);
     }
     if(StressShots!=null&&(test.Key.StartsWith("Button box")||test.Key.StartsWith("Limit"))){
      form.hotasView=test.Key.StartsWith("Limit")?11:0;form.ShowHotasDevice();
      using(var bmp=new Bitmap(form.hotasCanvas.Width,form.hotasCanvas.Height)){form.hotasCanvas.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(StressShots+"-"+(test.Key.StartsWith("Limit")?"limit":"box")+".png",System.Drawing.Imaging.ImageFormat.Png);}
     }
     report.AppendLine("preview: worst layout="+worstRebuild+" ms, worst paint="+worstPaint+" ms (device "+worstDevice+", "+loaded.Devices[worstDevice].Inputs.Count+" inputs)");
     // One live tick while listening for a control, reading every input on every device.
     form.hotasView=0;
     form.StartListening(new HotasRow{Group=HotasModel.Group.NativeAction,Name="Fire primary",Label="x"});
     sw.Restart();for(int t=0;t<20;t++)form.HotasTick();
     report.AppendLine("listening tick (all inputs, all devices) = "+(sw.ElapsedMilliseconds/20.0).ToString("F1")+" ms each");
     form.StopListening();
     sw.Restart();for(int t=0;t<20;t++)form.HotasTick();
     report.AppendLine("idle tick = "+(sw.ElapsedMilliseconds/20.0).ToString("F1")+" ms each");
     sw.Restart();form.RefreshHotasStatus();form.RebuildHotasList();
     report.AppendLine("status + list rebuild = "+sw.ElapsedMilliseconds+" ms");
     sw.Restart();var conflicts=HotasModel.Conflicts(loaded);var overlaps=HotasModel.Overlaps(loaded);
     report.AppendLine("conflicts/overlaps = "+sw.ElapsedMilliseconds+" ms ("+conflicts.Count+"/"+overlaps.Count+")");
     // What Apply builds.
     sw.Restart();
     var nativeDevices=loaded.FlightAxes.Values.Concat(loaded.FlightButtons.Values).Select(b=>b.DeviceIndex).Distinct().OrderBy(i=>i).ToArray();
     var slots=new Dictionary<int,int>();for(int i=0;i<nativeDevices.Length;i++)slots[nativeDevices[i]]=i;
     string table="#ControlConfigOverride\r\n$Name: default\r\n#End\r\n";
     string result=nativeDevices.Length>4?"needs "+nativeDevices.Length+" native slots (limit 4: Apply refuses with a clear message)":HotasTable(table,loaded,slots).Length+" chars";
     string map=HotasMapJson(loaded);
     report.AppendLine("apply build = "+sw.ElapsedMilliseconds+" ms; control table "+result+"; map "+(map==null?0:map.Length/1024)+" KB");
     form.Close();
    }
   }finally{try{File.Delete(file);}catch(Exception){}}
  }

  // Files just over the limits must be refused, not crash.
  report.AppendLine("== Over the limits");
  Func<string,HardwareProfile,string> tryLoad=delegate(string label,HardwareProfile p){
   string file=Path.Combine(Path.GetTempPath(),"stress-"+Guid.NewGuid().ToString("N")+".fchotas");
   File.WriteAllText(file,serializer.Serialize(p));
   try{
    using(var form=new ControllerSetup()){
     form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2000,-2000);form.ShowInTaskbar=false;form.Show();form.hotasTimer.Stop();
     try{form.ReadHotasProfile(file);return label+": accepted";}catch(Exception e){form.Close();return label+": refused ("+e.Message+")";}
    }
   }finally{try{File.Delete(file);}catch(Exception){}}
  };
  var seventeen=Enumerable.Range(0,17).Select(i=>StressInventory(new StressDevice{Name="D"+i,Axes=1,Buttons=1},i)).ToList();
  report.AppendLine(tryLoad("17 devices",new HardwareProfile{SchemaVersion=2,Backend="SDL3",Devices=seventeen}));
  report.AppendLine(tryLoad("1 device with 1025 inputs",new HardwareProfile{SchemaVersion=2,Backend="SDL3",Devices=new List<HardwareDevice>{StressInventory(new StressDevice{Name="Huge",Buttons=1025},0)}}));
  var big=new HardwareProfile{SchemaVersion=2,Backend="SDL3",Devices=Enumerable.Range(0,16).Select(i=>StressInventory(new StressDevice{Name="Device "+i,Axes=8,Buttons=1000,Hats=15},i)).ToList()};
  foreach(var d in big.Devices)foreach(var inp in d.Inputs)inp.Label="a long label for this input number "+inp.Index+" "+new string('x',20);
  string bigText=serializer.Serialize(big);
  report.AppendLine("16 devices fully labelled = "+(bigText.Length/1024)+" KB -> "+(bigText.Length>2*1024*1024?"over the 2 MB file limit, refused":"within the 2 MB limit"));
 }
}
