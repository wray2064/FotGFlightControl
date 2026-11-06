using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

// Two copies of this small launcher live beside kn_portable. The filename
// chooses Play or Configure, and the game root is derived from its own folder.
static class Launcher {
 [STAThread] static int Main() {
  try {
   string root=Path.GetDirectoryName(Application.ExecutablePath);
   string addon=Path.Combine(root,"GamepadMods","FotG_Gamepad_Wheel");
   bool configure=Path.GetFileNameWithoutExtension(Application.ExecutablePath)
    .StartsWith("Configure",StringComparison.OrdinalIgnoreCase);
   string target=configure
    ?Path.Combine(addon,"ControllerSetup","ControllerSetup.exe")
    :Path.Combine(addon,"WheelBridge.exe");
   if(!File.Exists(target))throw new FileNotFoundException("Gamepad Wheel is missing. Run Setup.exe to install or repair it.");
   var start=new ProcessStartInfo(target){WorkingDirectory=root,UseShellExecute=false};
   if(!configure)start.Arguments="--game \""+root.TrimEnd('\\')+"\"";
   Process.Start(start);
   return 0;
  } catch(Exception error) {
   MessageBox.Show(error.Message,"Flight Control",MessageBoxButtons.OK,MessageBoxIcon.Error);
   return 1;
  }
 }
}
