using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TweekPro {
 /// <summary>Drag the crosshair onto a window. The match itself is HunterMatch and is unit-tested; this form only resolves the process on Windows.</summary>
 public class HunterForm:Form {
  readonly IList<AppEntry> apps;
  readonly Action<AppEntry> onMatch;
  bool dragging;
  Label hint;
  public HunterForm(IList<AppEntry> apps,Action<AppEntry> onMatch){
   this.apps=apps;this.onMatch=onMatch;
   Text=Core.L.T("Hunter");FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
   ClientSize=new Size(420,160);BackColor=Theme.Surface;Font=Theme.Body;
   hint=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(16),ForeColor=Theme.Text,
    Text=Core.L.T("Giữ chuột trên ô chữ thập, kéo thả lên cửa sổ ứng dụng rồi thả. Tweek Pro tìm ứng dụng đã cài khớp tệp đó.")};
   var mark=new Panel{Width=64,Height=64,Dock=DockStyle.Left,BackColor=Theme.Primary,Cursor=Cursors.Cross};
   mark.MouseDown+=(s,e)=>{dragging=true;mark.Capture=true;};
   mark.MouseUp+=(s,e)=>{if(!dragging)return;dragging=false;mark.Capture=false;Resolve(Control.MousePosition);};
   Controls.Add(hint);Controls.Add(mark);
  }
  void Resolve(Point screen){
   uint pid;var hwnd=WindowFromPoint(new Pt{X=screen.X,Y=screen.Y});
   if(hwnd==IntPtr.Zero){hint.Text=Core.L.T("Không thấy cửa sổ tại vị trí thả.");return;}
   GetWindowThreadProcessId(hwnd,out pid);
   string exe="";
   try{using(var p=Process.GetProcessById((int)pid))exe=p.MainModule.FileName;}catch(Exception){hint.Text=Core.L.T("Không đọc được tệp của cửa sổ (tiến trình được bảo vệ).");return;}
   var match=HunterMatch.Find(exe,apps);
   if(match==null){hint.Text=Core.L.F("Không khớp ứng dụng đã cài:\r\n{0}",exe);return;}
   hint.Text=match.Name;
   if(onMatch!=null)onMatch(match);
   DialogResult=DialogResult.OK;Close();
  }
  [StructLayout(LayoutKind.Sequential)] struct Pt { public int X,Y; }
  [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Pt p);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 }
}
