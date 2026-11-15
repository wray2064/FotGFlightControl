using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Test-only: the whole switch-over cycle against a throwaway copy of a game folder.
// Usage: ControllerSetup.exe --hotas-flowtest <real game folder (read only)> <output file>
partial class ControllerSetup {
 static int HotasFlowTestCli(string[] args) {
  int at=Array.IndexOf(args,"--hotas-flowtest");
  if(at<0||at+2>=args.Length)return 1;
  string result;
  string temp=Path.Combine(Path.GetTempPath(),"FotGHotasFlow-"+Guid.NewGuid().ToString("N"));
  try{result=HotasFlowTest(args[at+1],temp);}
  catch(Exception e){result="FAIL: exception "+e;}
  finally{AutoConfirm=null;try{Directory.Delete(temp,true);}catch(Exception){}}
  File.WriteAllText(args[at+2],result);
  return result.StartsWith("PASS")?0:2;
 }

 static string HotasFlowTest(string realGame,string temp) {
  string realTable=ControlTablePath(GameInstall.MyModCore(realGame));
  string clean=RemoveHotasBlock(File.ReadAllText(realTable));
  // The test pilot starts on the gamepad's own preset, which a real table may not have yet.
  if(PresetSection(clean,"FotG Classic Native")==null)clean=clean.TrimEnd()+"\r\n\r\n#ControlConfigOverride\r\n$Name: FotG Classic Native\r\n#End\r\n";
  if(PresetSection(clean,"FotG Classic Native")==null)return "FAIL: could not set up the gamepad preset for the test";
  string core=GameInstall.MyModCore(temp);
  string table=ControlTablePath(core);
  string prefs=Path.Combine(temp,"kn_portable","HardLightProductions","FreeSpaceOpen");
  string players=Path.Combine(prefs,"data","players"),ini=Path.Combine(prefs,"data","FotG_2_settings.ini"),pilotPath=Path.Combine(players,"Tester.json");
  Directory.CreateDirectory(Path.GetDirectoryName(table));
  Directory.CreateDirectory(players);
  File.WriteAllText(Path.Combine(core,"..","mod.json"),"{}");
  File.WriteAllText(table,clean);
  string gamepadJoystick="{\"guid\":\"0300FA67EF170000EB61000000017801\",\"id\":0}";
  File.WriteAllText(ini,"[Input]\r\nJoystick="+gamepadJoystick+"\r\nJoystickSensitivity=5\r\n[FlightControl]\r\nXInputSlot=0\r\n");
  var serializer=new JavaScriptSerializer();
  string realPilot=Path.Combine(realGame,"kn_portable","HardLightProductions","FreeSpaceOpen","data","players","Issur.json");
  var pilot=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(realPilot));
  ((Dictionary<string,object>)((Dictionary<string,object>)pilot["sections"])["controls"])["preset"]="FotG Classic Native";
  File.WriteAllText(pilotPath,serializer.Serialize(pilot));
  Func<string> presetNow=delegate{
   var p=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(pilotPath));
   return Convert.ToString(((Dictionary<string,object>)((Dictionary<string,object>)p["sections"])["controls"])["preset"]);
  };
  ControllerSetup.AutoConfirm=true;
  using(var form=new ControllerSetup()){
   form.StartPosition=FormStartPosition.Manual;
   form.Location=new Point(-2000,-2000);
   form.ShowInTaskbar=false;
   form.Show();
   form.root.Text=temp;
   form.LoadPilots();
   if(form.pilots.SelectedItem==null||form.prefs==null)return "FAIL: the test pilot was not found";
   form.HotasDemo(true);
   var guids=new[]{new string('A',32),new string('B',32)};
   string mapPath=Path.Combine(players,HotasMapFile.FileName),statePath=form.HotasStatePath();
   Func<string,string> iniNow=delegate(string key){return GetIni(File.ReadAllText(ini),"Input",key);};

   // 1. Apply HOTAS: bindings, map and state appear; the pilot moves to the default preset.
   form.ApplyHotas(form.hotasProfile,guids);
   if(!File.ReadAllText(table).Contains(HotasStart)||!File.Exists(mapPath)||!File.Exists(statePath))return "FAIL: apply did not write the table, map and state";
   if(presetNow()!="default"||iniNow("Joystick")==null||!iniNow("Joystick").Contains("AAAA")||iniNow("Joystick1")==null)return "FAIL: apply left the pilot or joysticks wrong";
   var state=form.LoadHotasState();
   if(state==null||state.PreviousPreset!="FotG Classic Native"||state.PreviousJoystick["Joystick"]!=gamepadJoystick||state.PreviousJoystick["Joystick1"]!=null||state.AppliedSlots.Count!=2)return "FAIL: the previous gamepad setup was not recorded";
   if(form.HotasSlotNote()!=null)return "FAIL: a fresh apply was reported as out of sync";

   // 2. Applying again must not overwrite what was recorded about the gamepad.
   form.ApplyHotas(form.hotasProfile,guids);
   state=form.LoadHotasState();
   if(state.PreviousPreset!="FotG Classic Native"||state.PreviousJoystick["Joystick"]!=gamepadJoystick)return "FAIL: re-applying overwrote the recorded gamepad setup";

   // 3. Gamepad settings applied in between are noticed, and applying again re-syncs.
   File.WriteAllText(ini,SetIni(File.ReadAllText(ini),"Input","Joystick",gamepadJoystick));
   if(form.HotasSlotNote()==null)return "FAIL: a changed joystick slot was not noticed";
   form.ApplyHotas(form.hotasProfile,guids);
   if(form.HotasSlotNote()!=null)return "FAIL: re-applying did not re-sync the slots";

   // 4. Back to the gamepad restores everything and keeps the HOTAS profile for later.
   form.ReturnToGamepad();
   if(File.ReadAllText(table)!=clean)return "FAIL: the table is not back to the gamepad version";
   if(presetNow()!="FotG Classic Native"||iniNow("Joystick")!=gamepadJoystick||iniNow("Joystick1")!=null)return "FAIL: the pilot or joystick selection was not restored";
   if(File.Exists(mapPath)||File.Exists(statePath))return "FAIL: the map or state file was left behind";
   if(form.hotasProfile.FlightButtons.Count==0)return "FAIL: the HOTAS profile was lost";
   bool refused=false;
   try{form.ReturnToGamepad();}catch(InvalidOperationException){refused=true;}
   if(!refused)return "FAIL: switching back twice should be refused";

   // 5. And HOTAS comes straight back.
   form.ApplyHotas(form.hotasProfile,guids);
   if(!File.ReadAllText(table).Contains(HotasStart)||form.LoadHotasState().PreviousPreset!="FotG Classic Native")return "FAIL: HOTAS did not come back cleanly";

   // 6. Flight Control actions alone must leave the gamepad's axes, preset and joystick untouched.
   form.ReturnToGamepad();
   var featuresOnly=new HardwareProfile{SchemaVersion=HardwareInspector.CurrentSchema,Backend="SDL3",Devices=form.hotasProfile.Devices,
    Virtual=new Dictionary<string,FlightAxisBinding>{{"A",new FlightAxisBinding{DeviceIndex=0,InputIndex=4,Kind=HotasModel.Button}}}};
   form.ApplyHotas(featuresOnly,guids);
   if(presetNow()!="FotG Classic Native"||iniNow("Joystick")!=gamepadJoystick)return "FAIL: Flight Control actions changed the gamepad setup";
   if(!File.Exists(mapPath))return "FAIL: the Flight Control map was not written";
   form.ReturnToGamepad();

   // 7. Restore puts back the state from before the last apply.
   form.ApplyHotas(form.hotasProfile,guids);
   form.RestoreHotas();
   if(File.ReadAllText(table)!=clean||presetNow()!="FotG Classic Native"||File.Exists(mapPath)||File.Exists(statePath))return "FAIL: restore did not undo the last apply";
   form.Close();
  }
  return "PASS";
 }
}
