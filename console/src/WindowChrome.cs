using System;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
partial class ControllerSetup {
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
 void DragHeader(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
 void ToggleMaximize(){WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
 void OpenHelp(){string path=ConsoleAsset.File("Help.html");if(!File.Exists(path))throw new FileNotFoundException("Help.html is missing. Extract the complete download folder.");Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
 void OpenAbout(){string path=ConsoleAsset.File("About.html");if(!File.Exists(path))throw new FileNotFoundException("About.html is missing. Extract the complete download folder.");Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
 Button WindowButton(string text,string accessible,Action action){var b=ActionButton(text,action);b.AccessibleName=accessible;b.Font=new Font("Consolas",13,FontStyle.Bold);b.FlatAppearance.BorderSize=0;b.Margin=Padding.Empty;b.FlatAppearance.MouseOverBackColor=accessible=="Close"?Color.FromArgb(125,30,18):Color.FromArgb(57,46,5);return b;}
 protected override void OnHelpRequested(HelpEventArgs e){try{OpenHelp();e.Handled=true;}catch(Exception ex){MessageBox.Show(ex.Message,Text);}base.OnHelpRequested(e);}
 protected override void WndProc(ref Message m){
 // Preserve edge resizing and normal Windows dragging with custom chrome.
 if(m.Msg==0x84&&WindowState==FormWindowState.Normal){long v=m.LParam.ToInt64();Point p=PointToClient(new Point((short)(v&0xffff),(short)((v>>16)&0xffff)));int edge=Math.Max(6,(int)(7*DeviceScale()));bool l=p.X<edge,r=p.X>=ClientSize.Width-edge,t=p.Y<edge,b=p.Y>=ClientSize.Height-edge;int hit=t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:0;if(hit!=0){m.Result=new IntPtr(hit);return;}}
 base.WndProc(ref m);
 }
 float DeviceScale(){using(var g=CreateGraphics())return g.DpiX/96f;}
 protected override CreateParams CreateParams {get{var cp=base.CreateParams;cp.Style|=0x20000|0x10000;return cp;}}
}
