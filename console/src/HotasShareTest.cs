using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Test-only: one player saves a profile, another loads it, and hostile files are refused.
// Usage: ControllerSetup.exe --hotas-sharetest <output file>
partial class ControllerSetup {
 static int HotasShareTestCli(string[] args) {
  int at=Array.IndexOf(args,"--hotas-sharetest");
  if(at<0||at+1>=args.Length)return 1;
  string temp=Path.Combine(Path.GetTempPath(),"FotGHotasShare-"+Guid.NewGuid().ToString("N"));
  string result;
  try{result=HotasShareTest(temp);}
  catch(Exception e){result="FAIL: exception "+e;}
  finally{AutoConfirm=null;try{Directory.Delete(temp,true);}catch(Exception){}}
  File.WriteAllText(args[at+1],result);
  return result.StartsWith("PASS")?0:2;
 }

 static ControllerSetup HotasShareForm(string prefsFolder) {
  Directory.CreateDirectory(Path.Combine(prefsFolder,"data","players"));
  var form=new ControllerSetup();
  form.StartPosition=FormStartPosition.Manual;
  form.Location=new Point(-2000,-2000);
  form.ShowInTaskbar=false;
  form.Show();
  form.prefs=prefsFolder;
  form.HotasDemo(true);
  form.hotasTimer.Stop();
  return form;
 }

 static string HotasShareTest(string temp) {
  var serializer=new JavaScriptSerializer();
  string shared=Path.Combine(temp,"shared","my-hotas.json");
  Directory.CreateDirectory(Path.GetDirectoryName(shared));
  ControllerSetup.AutoConfirm=true;

  // ----- Player A saves -----
  using(var a=HotasShareForm(Path.Combine(temp,"playerA"))){
   var profile=a.hotasProfile;
   profile.Devices[0].Serial="SN-A-0042";
   profile.Layouts=new List<DeviceLayout>{new DeviceLayout{DeviceIndex=0,BackgroundImage=@"C:\Users\wray2\Pictures\my-stick.png",
    Widgets=new List<LayoutWidget>{new LayoutWidget{Kind="Button",Index=0,X=0.2,Y=0.3,Width=0.1,Height=0.1}}}};
   a.WriteHotasProfile(shared);
   string text=File.ReadAllText(shared);
   if(text.Contains("SN-A-0042")||text.Contains("wray2")||text.Contains("Pictures")||text.Contains(@"C:\\"))return "FAIL: the shared file carries personal details";
   if(!text.Contains("Fire primary")||!text.Contains("Logitech Freedom 2.4")||!text.Contains("\"SchemaVersion\":2"))return "FAIL: the shared file is missing the setup itself";
   if(text.Length>20000)return "FAIL: the shared file is unexpectedly large ("+text.Length+")";
   if(profile.Devices[0].Serial!="SN-A-0042"||profile.Layouts[0].BackgroundImage==null)return "FAIL: saving changed the player's own profile";
   a.Close();
  }

  // ----- Player B has other serials and a profile of their own, and loads A's file -----
  string prefsB=Path.Combine(temp,"playerB");
  using(var b=HotasShareForm(prefsB)){
   foreach(var d in b.hotasSession.Devices)d.Inventory.Serial="SN-B-"+d.Inventory.Name.Length;
   b.hotasProfile.FlightButtons.Remove("Fire secondary");   // B's own setup differs
   b.SaveHotasWorkingProfile();
   string ownBefore=File.ReadAllText(b.HotasProfilePath());
   var ownProfile=b.hotasProfile;

   // Hostile or broken files are refused and change nothing.
   string refusedWhy=null;
   Func<string,string,bool> refused=delegate(string name,string content){
    string file=Path.Combine(temp,"shared",name);
    File.WriteAllText(file,content);
    try{b.LoadSharedHotasProfile(file);}catch(Exception){return ReferenceEquals(b.hotasProfile,ownProfile)&&File.ReadAllText(b.HotasProfilePath())==ownBefore;}
    refusedWhy=name;
    return false;
   };
   string good=File.ReadAllText(shared);
   var bad=new Dictionary<string,string>{
    {"not-json.json","this is not json {{{"},
    {"wrong-backend.json",good.Replace("\"SDL3\"","\"DirectInput\"")},
    {"bad-action.json",good.Replace("\"Fire primary\"","\"Format C:\"")},
    {"bad-index.json",good.Replace("\"InputIndex\":0","\"InputIndex\":99")},
    {"control-char.json",good.Replace("Logitech Freedom 2.4","Evil\\u0007Name")},
    {"no-devices.json","{\"SchemaVersion\":2,\"Backend\":\"SDL3\",\"Devices\":[]}"},
    {"too-many.json",serializer.Serialize(new HardwareProfile{SchemaVersion=2,Backend="SDL3",Devices=Enumerable.Range(0,20).Select(i=>new HardwareDevice{Name="D"+i,Guid=new string('A',32),Inputs=new List<HardwareInput>()}).ToList()})}
   };
   foreach(var pair in bad)if(!refused(pair.Key,pair.Value))return "FAIL: a bad profile was accepted or changed things ("+pair.Key+")";
   string big=Path.Combine(temp,"shared","huge.json");
   File.WriteAllText(big,new string(' ',3*1024*1024));
   bool hugeRefused=false;
   try{b.LoadSharedHotasProfile(big);}catch(InvalidDataException){hugeRefused=true;}
   if(!hugeRefused||!ReferenceEquals(b.hotasProfile,ownProfile))return "FAIL: an oversized file was not refused";

   // A picture path from a shared file must never survive loading.
   string withPath=good.Replace("\"Layouts\":[{\"DeviceIndex\":0,\"BackgroundImage\":null","\"Layouts\":[{\"DeviceIndex\":0,\"BackgroundImage\":\"\\\\\\\\evil\\\\share\\\\a.png\"");
   if(withPath==good||!withPath.Contains("evil"))return "FAIL: test setup could not plant a picture path";
   File.WriteAllText(Path.Combine(temp,"shared","path.json"),withPath);
   var read=b.ReadHotasProfile(Path.Combine(temp,"shared","path.json"));
   if(read.Layouts!=null&&read.Layouts.Any(l=>l.BackgroundImage!=null))return "FAIL: a picture path survived loading";

   // The good file loads: bindings arrive, devices match by GUID, the old profile is kept.
   b.LoadSharedHotasProfile(shared);
   var loaded=b.hotasProfile;
   if(ReferenceEquals(loaded,ownProfile))return "FAIL: the shared profile was not loaded";
   if(!loaded.FlightButtons.ContainsKey("Fire secondary")||!loaded.FlightAxes.ContainsKey("Pitch")||!loaded.Virtual.ContainsKey("Right trigger"))return "FAIL: bindings did not arrive";
   if(b.hotasMatches.Count!=2||b.hotasMatches.Any(m=>m==null))return "FAIL: the loaded profile did not match B's controllers by model";
   if(loaded.Devices.Any(d=>d.Serial!=null))return "FAIL: a serial came along";
   string backups=Path.GetDirectoryName(b.HotasProfilePath());
   if(Directory.GetFiles(backups,"fotg-hotas-profile.hotas.bak-*").Length<1)return "FAIL: B's own profile was not kept as a backup";
   var backedUp=serializer.Deserialize<HardwareProfile>(File.ReadAllText(Directory.GetFiles(backups,"fotg-hotas-profile.hotas.bak-*")[0]));
   if(backedUp.FlightButtons.ContainsKey("Fire secondary"))return "FAIL: the backup is not B's own profile";
   HardwareInspector.Validate(loaded);
   b.Close();
  }

  // ----- B restarts the Console: the loaded profile is still there -----
  using(var again=HotasShareForm(prefsB)){
   again.hotasProfile=null;
   again.LoadHotasWorkingProfile();
   if(again.hotasProfile==null||!again.hotasProfile.FlightButtons.ContainsKey("Fire secondary"))return "FAIL: the loaded profile did not survive a restart";
   again.Close();
  }
  return "PASS";
 }
}
