using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Accessibility;

namespace TweekPro {
 /// <summary>Executable under the sight, plus the installed application when one matches.</summary>
 public class HunterHit {
  public string Exe="", Name="", Publisher="";
  public AppEntry App;
 }

 /// <summary>Hides the main window, shows a tray icon and a draggable sight. Drop it on a desktop icon or a window.</summary>
 public static class HunterSession {
  public static void Start(Form owner,IList<AppEntry> apps,Action<AppEntry> uninstall,Action finished){
   bool ended=false;
   var tray=new NotifyIcon{Icon=Branding.AppIcon(32),Visible=true,Text=Core.L.T("Kéo kính ngắm lên biểu tượng. Chuột phải để hủy.")};
   var sight=new HunterSight(apps);
   Action<bool> end=notify=>{
    if(ended)return;ended=true;
    tray.Visible=false;tray.Dispose();
    if(!sight.IsDisposed)sight.Close();
    if(notify&&finished!=null)finished();
   };
   var menu=SightMenu(sight,()=>{if(owner!=null){owner.Show();owner.WindowState=FormWindowState.Normal;owner.Activate();}},()=>end(true));
   sight.ContextMenuStrip=menu;tray.ContextMenuStrip=menu;
   tray.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left&&owner!=null){owner.Show();owner.WindowState=FormWindowState.Normal;owner.Activate();}};
   sight.FormClosed+=(s,e)=>end(true);
   sight.Dropped+=hit=>ShowHit(sight,hit,uninstall,()=>end(false),()=>end(true));
   sight.Show();
   var area=(owner!=null?Screen.FromHandle(owner.Handle):Screen.FromPoint(Cursor.Position)).WorkingArea;
   int x=area.Left+Math.Max(0,(area.Width-sight.Width)/2);
   int y=area.Top+Math.Max(0,(area.Height-sight.Height)/2);
   sight.Location=new Point(x,y);sight.TopMost=true;sight.BringToFront();
  }
  static void ShowHit(Form sight,HunterHit hit,Action<AppEntry> uninstall,Action closeSight,Action cancel){
   if(hit==null||(hit.App==null&&String.IsNullOrWhiteSpace(hit.Exe))){MessageBox.Show(sight,Core.L.T("Không thấy ứng dụng tại vị trí thả."),"Tweek Pro");return;}
   string title=hit.App!=null?hit.App.Name:hit.Name;
   string publisher=hit.App!=null?(hit.App.Publisher??""):hit.Publisher;
   var menu=new ContextMenuStrip();
   menu.Items.Add(new ToolStripLabel(title){Enabled=false});
   if(!String.IsNullOrWhiteSpace(publisher)&&!String.Equals(publisher,title,StringComparison.OrdinalIgnoreCase))menu.Items.Add(new ToolStripLabel(publisher){Enabled=false});
   if(!String.IsNullOrWhiteSpace(hit.Exe))menu.Items.Add(new ToolStripLabel(hit.Exe){Enabled=false});
   menu.Items.Add(new ToolStripSeparator());
   if(hit.App!=null)menu.Items.Add(Core.L.F("Gỡ {0}",hit.App.Name),null,(s,e)=>{closeSight();if(uninstall!=null)uninstall(hit.App);});
   if(!String.IsNullOrWhiteSpace(hit.Exe)&&File.Exists(hit.Exe)){
    menu.Items.Add(Core.L.T("Kết thúc tiến trình"),null,(s,e)=>{HunterSight.StopProcesses(hit.Exe);});
    menu.Items.Add(Core.L.T("Mở thư mục chứa"),null,(s,e)=>{Process.Start("explorer.exe","/select,\""+hit.Exe+"\"");});
   }
   menu.Items.Add(Core.L.T("Hủy"),null,(s,e)=>{if(cancel!=null)cancel();});
   menu.Show(Cursor.Position);
  }
  static ContextMenuStrip SightMenu(HunterSight sight,Action openMain,Action exit){
   var menu=new ContextMenuStrip();
   menu.Items.Add(Core.L.T("Mở cửa sổ chính"),null,(s,e)=>openMain());
   var auto=new ToolStripMenuItem(Core.L.T("Tự chạy cùng Windows")){Checked=HunterAutoStart.Enabled};
   auto.Click+=(s,e)=>{auto.Checked=!auto.Checked;HunterAutoStart.Set(auto.Checked);};
   menu.Items.Add(auto);
   var top=new ToolStripMenuItem(Core.L.T("Luôn nổi trên cùng")){Checked=true};
   top.Click+=(s,e)=>{top.Checked=!top.Checked;sight.TopMost=top.Checked;};
   menu.Items.Add(top);
   var size=new ToolStripMenuItem(Core.L.T("Kích thước"));
   foreach(var item in new[]{new[]{"Nhỏ","36"},new[]{"Vừa","48"},new[]{"Lớn","64"}}){string label=item[0];int px=int.Parse(item[1]);size.DropDownItems.Add(Core.L.T(label),null,(s,e)=>sight.ApplySize(px));}
   menu.Items.Add(size);
   var fade=new ToolStripMenuItem(Core.L.T("Độ trong suốt"));
   foreach(var item in new[]{new[]{"Không","100"},new[]{"20%","80"},new[]{"40%","60"},new[]{"60%","40"}}){string label=item[0];int op=int.Parse(item[1]);fade.DropDownItems.Add(Core.L.T(label),null,(s,e)=>sight.SetFade(op));}
   menu.Items.Add(fade);
   menu.Items.Add(Core.L.T("Trợ giúp"),null,(s,e)=>MessageBox.Show(sight,Core.L.T("Kéo kính ngắm lên biểu tượng hoặc cửa sổ.\r\nThả chuột để gỡ hoặc mở thư mục.\r\nChuột phải trên kính ngắm để mở menu này.\r\nThoát để trở lại Tweek Pro."),Core.L.T("Kính ngắm"),MessageBoxButtons.OK,MessageBoxIcon.Information));
   menu.Items.Add(new ToolStripSeparator());
   menu.Items.Add(Core.L.T("Thoát"),null,(s,e)=>exit());
   return menu;
  }
 }

 /// <summary>Borderless sight the user drags onto a desktop icon or a window.</summary>
 public class HunterSight:Form {
  readonly IList<AppEntry> apps;
  bool dragging;Point grab;
  public event Action<HunterHit> Dropped;
  readonly HunterTip tip=new HunterTip();
  void ShowTip(HunterHit hit){if(hit==null||(String.IsNullOrWhiteSpace(hit.Exe)&&String.IsNullOrWhiteSpace(hit.Name))){tip.Hide();return;}tip.ShowAt(Cursor.Position,hit);}
  void HideTip(){tip.Hide();}
  protected override void OnFormClosed(FormClosedEventArgs e){tip.Dispose();base.OnFormClosed(e);}
  public HunterSight(IList<AppEntry> apps){
   this.apps=apps;
   FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
   AutoScaleMode=AutoScaleMode.None;BackColor=Color.Magenta;TransparencyKey=Color.Magenta;
   SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
   Cursor=Cursors.SizeAll;ApplySize(48);
   MouseDown+=(s,e)=>{if(e.Button!=MouseButtons.Left)return;grab=e.Location;dragging=true;Capture=true;};
   MouseMove+=(s,e)=>{if(!dragging)return;var p=Cursor.Position;Location=new Point(p.X-grab.X,p.Y-grab.Y);ShowTip(Resolve(new Point(Left+Width/2,Top+Height/2)));};
   MouseUp+=(s,e)=>{if(!dragging||e.Button!=MouseButtons.Left)return;dragging=false;Capture=false;var hit=Resolve(Cursor.Position);HideTip();if(Dropped!=null)Dropped(hit);};
  }
  int fade=255;
  public void SetFade(int percent){fade=Math.Max(70,Math.Min(255,percent*255/100));Invalidate();}
  public void ApplySize(int px){
   var center=new Point(Left+Math.Max(Width,1)/2,Top+Math.Max(Height,1)/2);
   MinimumSize=MaximumSize=new Size(px,px);ClientSize=new Size(px,px);
   Location=new Point(center.X-px/2,center.Y-px/2);
   Invalidate();
  }
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.Clear(Color.Magenta);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
   int side=Math.Min(ClientSize.Width,ClientSize.Height);float mid=side/2f;
   var blue=Color.FromArgb(Math.Min(255,fade),255,23,68);
   using(var rim=new Pen(blue,2.4f))g.DrawEllipse(rim,3,3,side-7,side-7);
   using(var hair=new Pen(blue,1.5f)){
    g.DrawLine(hair,mid,2,mid,mid-5);g.DrawLine(hair,mid,mid+5,mid,side-3);
    g.DrawLine(hair,2,mid,mid-5,mid);g.DrawLine(hair,mid+5,mid,side-3,mid);
   }
   using(var dot=new SolidBrush(blue))g.FillEllipse(dot,mid-1.5f,mid-1.5f,3,3);
  }
  HunterHit Resolve(Point screen){
   string exe=HunterDesktop.TargetAt(screen);string icon=HunterDesktop.LastName;
   if(String.IsNullOrWhiteSpace(exe))exe=WindowExe(screen);
   if(String.IsNullOrWhiteSpace(exe))return new HunterHit{Name=icon??""};
   if(exe.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)){string target=Advanced.ShortcutTarget(exe);if(!String.IsNullOrWhiteSpace(target))exe=target;}
   var app=HunterMatch.Find(exe,apps);
   return new HunterHit{Exe=exe,App=app,Name=app==null?Path.GetFileNameWithoutExtension(exe):app.Name,Publisher=app==null?"":(app.Publisher??"")};
  }
  static string WindowExe(Point screen){
   var hwnd=WindowFromPoint(new Pt{X=screen.X,Y=screen.Y});
   if(hwnd==IntPtr.Zero)return "";
   hwnd=GetAncestor(hwnd,2);
   uint pid;GetWindowThreadProcessId(hwnd,out pid);
   try{using(var p=Process.GetProcessById((int)pid)){string file=p.MainModule.FileName;return Path.GetFileName(file).Equals("explorer.exe",StringComparison.OrdinalIgnoreCase)?"":file;}}
   catch(Exception){return "";}
  }
  /// <summary>Ends processes started from this executable. Refuses anything under the Windows directory.</summary>
  public static void StopProcesses(string exe){
   string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
   string full;try{full=Path.GetFullPath(exe);}catch(Exception){return;}
   if(!String.IsNullOrEmpty(windows)&&full.StartsWith(windows,StringComparison.OrdinalIgnoreCase))throw new IOException(Core.L.T("Không kết thúc tiến trình của Windows."));
   if(MessageBox.Show(Core.L.F("Kết thúc mọi tiến trình của tệp này?\r\n{0}",full),Core.L.T("Xác nhận thao tác"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
   foreach(var p in Process.GetProcesses())try{if(p.Id!=Process.GetCurrentProcess().Id&&String.Equals(p.MainModule.FileName,full,StringComparison.OrdinalIgnoreCase))p.Kill();}catch(Exception){}
  }
  [StructLayout(LayoutKind.Sequential)] struct Pt { public int X,Y; }
  [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Pt p);
  [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 }
 static class HunterAutoStart {
  const string Value="TweekPro";
  public static bool Enabled { get { using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",false))return key!=null&&key.GetValue(Value)!=null; } }
  public static void Set(bool on){
   using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)){
    if(key==null)return;
    if(on)key.SetValue(Value,"\""+Application.ExecutablePath+"\"");
    else try{key.DeleteValue(Value,false);}catch(ArgumentException){}
   }
  }
 }

 /// <summary>Floating label that follows the sight without taking focus.</summary>
 public class HunterTip:Form {
  readonly Label text=new Label{Dock=DockStyle.Fill,AutoSize=false,Padding=new Padding(10,8,10,8),ForeColor=Color.FromArgb(30,41,59)};
  public HunterTip(){
   FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
   BackColor=Color.White;ClientSize=new Size(420,88);Controls.Add(text);text.Font=Theme.Small;
  }
  protected override bool ShowWithoutActivation { get { return true; } }
  protected override CreateParams CreateParams { get { var p=base.CreateParams;p.ExStyle|=0x08000000|0x00000080;return p; } }
  public void ShowAt(Point cursor,HunterHit hit){
   string title=String.IsNullOrWhiteSpace(hit.Name)?Path.GetFileName(hit.Exe):hit.Name;
   bool same=String.Equals(title,hit.Publisher,StringComparison.OrdinalIgnoreCase);
   text.Text=title+"\r\n"+(hit.Exe??"")+(same||String.IsNullOrWhiteSpace(hit.Publisher)?"":"\r\n"+hit.Publisher);
   var size=TextRenderer.MeasureText(text.Text,text.Font,new Size(520,200),TextFormatFlags.WordBreak);
   ClientSize=new Size(Math.Max(280,size.Width+24),Math.Max(72,size.Height+16));
   var area=Screen.FromPoint(cursor).WorkingArea;
   int x=cursor.X+28,y=cursor.Y+24;if(x+Width>area.Right)x=cursor.X-Width-12;if(y+Height>area.Bottom)y=cursor.Y-Height-12;
   Location=new Point(x,y);
   if(!Visible)Show();
  }
 }

 /// <summary>Reads the desktop icon under a screen point and returns its shortcut or executable. Windows only.</summary>
 public static class HunterDesktop {
  static string cachedName="",cachedPath="";
  public static string TargetAt(Point screen){
   IntPtr list=FindList();if(list==IntPtr.Zero)return "";
   string name=AccName(list,screen);if(String.IsNullOrWhiteSpace(name)||name=="FolderView"||name=="Desktop"){cachedName="";cachedPath="";return "";}
   if(name==cachedName)return cachedPath;
   foreach(string folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Desktop),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}){
    if(String.IsNullOrWhiteSpace(folder))continue;
    foreach(string ext in new[]{".lnk",".exe"}){string path=Path.Combine(folder,name+ext);if(File.Exists(path)){cachedName=name;cachedPath=path;return path;}}
   }
   cachedName=name;cachedPath="";return "";
  }
  public static string LastName { get { return cachedName; } }
  static string AccName(IntPtr list,Point screen){
   Guid iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");IAccessible acc;
   if(AccessibleObjectFromWindow(list,0xFFFFFFFC,ref iid,out acc)!=0||acc==null)return "";
   object child=acc.accHitTest(screen.X,screen.Y);if(child==null||(child is int&&(int)child==0))return "";
   var nested=child as IAccessible;string name=nested!=null?nested.get_accName(0):acc.get_accName(child);
   return (name??"").Trim();
  }
  [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(IntPtr hwnd,uint id,ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out IAccessible acc);
  static IntPtr FindList(){
   IntPtr list=ListUnder(FindWindow("Progman",null));if(list!=IntPtr.Zero)return list;
   IntPtr worker=IntPtr.Zero;
   while((worker=FindWindowEx(IntPtr.Zero,worker,"WorkerW",null))!=IntPtr.Zero){list=ListUnder(worker);if(list!=IntPtr.Zero)return list;}
   return IntPtr.Zero;
  }
  static IntPtr ListUnder(IntPtr root){
   if(root==IntPtr.Zero)return IntPtr.Zero;
   IntPtr view=FindWindowEx(root,IntPtr.Zero,"SHELLDLL_DefView",null);
   return view==IntPtr.Zero?IntPtr.Zero:FindWindowEx(view,IntPtr.Zero,"SysListView32","FolderView");
  }
  static string ItemText(IntPtr list,Point screen){
   uint pid;GetWindowThreadProcessId(list,out pid);
   IntPtr process=OpenProcess(0x0438,false,pid);if(process==IntPtr.Zero)return "";
   IntPtr remote=VirtualAllocEx(process,IntPtr.Zero,4096,0x1000,0x04);
   try{
    if(remote==IntPtr.Zero)return "";
    var pt=screen;ScreenToClient(list,ref pt);
    var hit=new Hit{X=pt.X,Y=pt.Y};
    Write(process,remote,hit);
    int index=(int)SendMessage(list,0x1012,IntPtr.Zero,remote).ToInt64();
    if(index<0)return "";
    IntPtr text=new IntPtr(remote.ToInt64()+512);
    Write(process,remote,new Item{iItem=index,pszText=text,cchTextMax=260});
    SendMessage(list,0x1073,new IntPtr(index),remote);
    byte[] chars=new byte[520];IntPtr read;ReadProcessMemory(process,text,chars,chars.Length,out read);
    int n=0;while(n+1<chars.Length&&(chars[n]!=0||chars[n+1]!=0))n+=2;
    return System.Text.Encoding.Unicode.GetString(chars,0,n);
   }finally{if(remote!=IntPtr.Zero)VirtualFreeEx(process,remote,0,0x8000);CloseHandle(process);}
  }
  static void Write(IntPtr process,IntPtr remote,object value){
   int size=Marshal.SizeOf(value);IntPtr local=Marshal.AllocHGlobal(size);
   try{Marshal.StructureToPtr(value,local,false);byte[] raw=new byte[size];Marshal.Copy(local,raw,0,size);IntPtr wrote;WriteProcessMemory(process,remote,raw,size,out wrote);}
   finally{Marshal.FreeHGlobal(local);}
  }
  [StructLayout(LayoutKind.Sequential)] struct Hit { public int X,Y; public uint Flags; public int Item,Sub; }
  [StructLayout(LayoutKind.Sequential)] struct Item {
   public uint mask; public int iItem,iSubItem; public uint state,stateMask; public IntPtr pszText; public int cchTextMax;
  }
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string name);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string name);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
  [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hwnd,ref Point pt);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
  [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr process,IntPtr addr,int size,uint type,uint protect);
  [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr process,IntPtr addr,int size,uint type);
  [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr process,IntPtr addr,byte[] buf,int size,out IntPtr wrote);
  [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr process,IntPtr addr,byte[] buf,int size,out IntPtr read);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 }
}
