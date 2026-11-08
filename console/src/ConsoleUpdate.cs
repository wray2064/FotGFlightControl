using System;
using System.Windows.Forms;

// The console checks update.json once when it opens and offers a newer release. Yes downloads
// and verifies it and shows its Setup.exe in File Explorer for the player to run (the console
// closes, which Install / Repair requires); No asks again next time; Cancel skips that version.
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
   "Yes: download it, check its SHA-256 and show its Setup.exe in File Explorer. This window then closes; run that Setup.exe and choose Install / Repair. Your settings are kept.\r\n"+
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
    Updater.ShowInExplorer(setup);
    MessageBox.Show(this,"Flight Control "+info.Version+" was downloaded and matches its published SHA-256.\r\n\r\nFile Explorer shows its Setup.exe. This window closes now; run that Setup.exe and choose Install / Repair.","Update Flight Control");
    Close();
   });
  });
 }
}
