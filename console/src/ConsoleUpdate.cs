using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

// The console checks update.json once when it opens and offers a newer release. Yes downloads,
// verifies and opens the new Setup (closing the console, which Install / Repair requires);
// No asks again next time; Cancel skips that version until a newer one appears.
partial class ControllerSetup {
 void StartConsoleUpdateCheck(){
  if(!Updater.CheckEnabled)return;
  System.Threading.ThreadPool.QueueUserWorkItem(delegate{
   UpdateInfo info=null;
   try{info=Updater.Check(6000);}catch{return;}
   if(info==null||info.Version==Updater.SkippedVersion)return;
   try{BeginInvoke((Action)delegate{OfferUpdate(info);});}catch(InvalidOperationException){}
  });
 }
 void OfferUpdate(UpdateInfo info){
  var answer=MessageBox.Show(this,
   "Flight Control "+info.Version+" is available (you have "+BuildInfo.Version+").\r\n\r\n"+
   "Yes: download it, check its SHA-256 and open its Setup (this window closes). Then choose Install / Repair; your settings are kept.\r\n"+
   "No: remind me next time.\r\nCancel: skip "+info.Version+".\r\n\r\n"+
   "Windows may warn that the new Setup is from an unknown publisher; it is not code-signed yet. Release notes: "+info.Notes,
   "Update Flight Control",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Information);
  if(answer==DialogResult.Cancel){Updater.SkippedVersion=info.Version;return;}
  if(answer!=DialogResult.Yes)return;
  string title=Text;Enabled=false;
  System.Threading.ThreadPool.QueueUserWorkItem(delegate{
   string setup=null,error=null;
   try{setup=Updater.DownloadAndExtract(info,delegate(string step){BeginInvoke((Action)delegate{Text=step;});});}
   catch(Exception e){error=e.Message;}
   BeginInvoke((Action)delegate{
    Enabled=true;Text=title;
    if(error!=null){MessageBox.Show(this,"The update was not installed.\r\n\r\n"+error,"Update Flight Control");return;}
    Process.Start(new ProcessStartInfo(setup){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(setup)});
    Close();
   });
  });
 }
}
