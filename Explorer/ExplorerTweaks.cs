using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using TweekPro.Core;

namespace TweekPro.Explorer {
 public enum TweakGroup { Files, Navigation, Taskbar }

 /// <summary>One File Explorer switch stored as a DWORD under HKCU: both states are known and the Windows default is documented.</summary>
 public class ExplorerTweak {
  public string Id, Name, Description, KeyPath, ValueName; public int OnValue, OffValue; public bool DefaultOn, Caution; public TweakGroup Group;
  public override string ToString(){return Id+" ("+KeyPath+"\\"+ValueName+")";}
 }
 /// <summary>Current state of a tweak as read from the registry; Known is false when the value is missing or holds an unexpected number.</summary>
 public class TweakState { public ExplorerTweak Tweak; public bool Enabled, Known, Missing; public object Raw; }
 public class TweakChange { public ExplorerTweak Tweak; public bool Enable; }

 /// <summary>Hard allow-list: Tweek Pro only ever writes the catalog values under the Explorer keys of the current user (plus the self-test fixture key).</summary>
 public static class ExplorerSafety {
  public const string Advanced=@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
  public const string CabinetState=@"Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState";
  public const string ExplorerKey=@"Software\Microsoft\Windows\CurrentVersion\Explorer";
  public const string TestKey=@"Software\TweekProTest\Explorer";
  public static readonly string[] AllowedKeys={Advanced,CabinetState,ExplorerKey,TestKey};

  /// <summary>True for exactly one of the allowed HKCU keys (no parents, children, other hives or path tricks).</summary>
  public static bool AllowedKey(string keyPath){
   if(String.IsNullOrWhiteSpace(keyPath)||keyPath.Contains("..")||keyPath.StartsWith("\\")||keyPath.IndexOf("HKEY",StringComparison.OrdinalIgnoreCase)>=0)return false;
   return AllowedKeys.Any(k=>String.Equals(k,keyPath.Trim().TrimEnd('\\'),StringComparison.OrdinalIgnoreCase));
  }
  /// <summary>A tweak may be applied only when it is a catalog entry (or the self-test fixture) with a sane value name.</summary>
  public static bool Allowed(ExplorerTweak t){
   if(t==null||String.IsNullOrWhiteSpace(t.ValueName)||t.ValueName.Length>64||t.ValueName.IndexOfAny(new[]{'\\','/'})>=0||t.OnValue==t.OffValue||!AllowedKey(t.KeyPath))return false;
   if(String.Equals(t.KeyPath,TestKey,StringComparison.OrdinalIgnoreCase))return true;
   return ExplorerTweaks.Catalog.Any(c=>c.Id==t.Id&&String.Equals(c.KeyPath,t.KeyPath,StringComparison.OrdinalIgnoreCase)&&c.ValueName==t.ValueName&&c.OnValue==t.OnValue&&c.OffValue==t.OffValue);
  }
 }

 /// <summary>Reads and toggles File Explorer display settings; every write stores the previous value in the vault (Kind=Explorer).</summary>
 public static class ExplorerTweaks {
  public const string BackupKind="Explorer";
  public static readonly ExplorerTweak[] Catalog={
   new ExplorerTweak{Id="show-ext",Group=TweakGroup.Files,Name="Hiện phần mở rộng tệp (.exe, .pdf…)",Description="Luôn hiện đuôi tệp để nhận ra tệp giả dạng như \"hoadon.pdf.exe\". Nên bật.",KeyPath=ExplorerSafety.Advanced,ValueName="HideFileExt",OnValue=0,OffValue=1,DefaultOn=false},
   new ExplorerTweak{Id="show-hidden",Group=TweakGroup.Files,Name="Hiện tệp và thư mục ẩn",Description="Hiện các mục có thuộc tính Ẩn (AppData, cấu hình ứng dụng…) với biểu tượng mờ.",KeyPath=ExplorerSafety.Advanced,ValueName="Hidden",OnValue=1,OffValue=2,DefaultOn=false},
   new ExplorerTweak{Id="show-superhidden",Group=TweakGroup.Files,Name="Hiện tệp hệ thống được bảo vệ",Description="Hiện cả tệp Windows đánh dấu Hệ thống (desktop.ini, pagefile.sys, thư mục Recovery…). Chỉ bật khi cần vì dễ xóa nhầm.",KeyPath=ExplorerSafety.Advanced,ValueName="ShowSuperHidden",OnValue=1,OffValue=0,DefaultOn=false,Caution=true},
   new ExplorerTweak{Id="checkboxes",Group=TweakGroup.Files,Name="Hiện hộp chọn để tích nhiều mục",Description="Mỗi tệp có một ô tích, chọn nhiều mục không cần giữ Ctrl (tiện với màn hình cảm ứng).",KeyPath=ExplorerSafety.Advanced,ValueName="AutoCheckSelect",OnValue=1,OffValue=0,DefaultOn=false},
   new ExplorerTweak{Id="status-bar",Group=TweakGroup.Files,Name="Hiện thanh trạng thái",Description="Dòng dưới cùng của Explorer: số mục, dung lượng phần đang chọn.",KeyPath=ExplorerSafety.Advanced,ValueName="ShowStatusBar",OnValue=1,OffValue=0,DefaultOn=true},
   new ExplorerTweak{Id="compact",Group=TweakGroup.Files,Name="Chế độ gọn (Windows 11)",Description="Các hàng sát nhau hơn, hiện được nhiều tệp hơn trên một màn hình.",KeyPath=ExplorerSafety.Advanced,ValueName="UseCompactMode",OnValue=1,OffValue=0,DefaultOn=false},
   new ExplorerTweak{Id="infotip",Group=TweakGroup.Files,Name="Hiện chú giải khi trỏ vào tệp",Description="Hộp thông tin nhỏ (loại, dung lượng, ngày sửa) khi rê chuột lên tệp hoặc thư mục.",KeyPath=ExplorerSafety.Advanced,ValueName="ShowInfoTip",OnValue=1,OffValue=0,DefaultOn=true},
   new ExplorerTweak{Id="ntfs-color",Group=TweakGroup.Files,Name="Tô màu tệp nén / mã hóa NTFS",Description="Tên tệp nén NTFS hiện màu xanh dương, tệp mã hóa EFS màu xanh lá.",KeyPath=ExplorerSafety.Advanced,ValueName="ShowEncryptCompressedColor",OnValue=1,OffValue=0,DefaultOn=true},
   new ExplorerTweak{Id="full-path",Group=TweakGroup.Navigation,Name="Hiện đường dẫn đầy đủ trên thanh tiêu đề",Description="Tiêu đề cửa sổ Explorer hiện C:\\Users\\…\\Thư mục thay vì chỉ tên thư mục.",KeyPath=ExplorerSafety.CabinetState,ValueName="FullPath",OnValue=1,OffValue=0,DefaultOn=false},
   new ExplorerTweak{Id="launch-thispc",Group=TweakGroup.Navigation,Name="Mở Explorer vào This PC",Description="Explorer mở thẳng vào danh sách ổ đĩa thay vì Home / Quick access.",KeyPath=ExplorerSafety.Advanced,ValueName="LaunchTo",OnValue=1,OffValue=2,DefaultOn=false},
   new ExplorerTweak{Id="recent",Group=TweakGroup.Navigation,Name="Hiện tệp gần đây trong Home / Quick access",Description="Tắt nếu không muốn người dùng máy nhìn thấy các tệp vừa mở.",KeyPath=ExplorerSafety.ExplorerKey,ValueName="ShowRecent",OnValue=1,OffValue=0,DefaultOn=true},
   new ExplorerTweak{Id="frequent",Group=TweakGroup.Navigation,Name="Hiện thư mục thường dùng trong Home / Quick access",Description="Danh sách thư mục hay mở do Windows tự đề xuất.",KeyPath=ExplorerSafety.ExplorerKey,ValueName="ShowFrequent",OnValue=1,OffValue=0,DefaultOn=true},
   new ExplorerTweak{Id="sync-ads",Group=TweakGroup.Navigation,Name="Hiện thông báo của nhà cung cấp đồng bộ",Description="Các gợi ý / quảng cáo OneDrive chen trong Explorer. Tắt để Explorer sạch hơn.",KeyPath=ExplorerSafety.Advanced,ValueName="ShowSyncProviderNotifications",OnValue=1,OffValue=0,DefaultOn=true},
   new ExplorerTweak{Id="clock-seconds",Group=TweakGroup.Taskbar,Name="Hiện giây trên đồng hồ thanh tác vụ",Description="Đồng hồ góc phải hiện cả giây (tốn thêm một chút pin trên laptop).",KeyPath=ExplorerSafety.Advanced,ValueName="ShowSecondsInSystemClock",OnValue=1,OffValue=0,DefaultOn=false},
   new ExplorerTweak{Id="taskbar-left",Group=TweakGroup.Taskbar,Name="Căn thanh tác vụ sang trái (Windows 11)",Description="Nút Start và các icon dồn về góc trái như Windows 10.",KeyPath=ExplorerSafety.Advanced,ValueName="TaskbarAl",OnValue=0,OffValue=1,DefaultOn=false},
   new ExplorerTweak{Id="task-view",Group=TweakGroup.Taskbar,Name="Hiện nút Task View",Description="Nút xem mọi cửa sổ và màn hình ảo trên thanh tác vụ.",KeyPath=ExplorerSafety.Advanced,ValueName="ShowTaskViewButton",OnValue=1,OffValue=0,DefaultOn=true},
   new ExplorerTweak{Id="widgets",Group=TweakGroup.Taskbar,Name="Hiện nút Widgets (Windows 11)",Description="Bảng thời tiết / tin tức ở góc trái thanh tác vụ.",KeyPath=ExplorerSafety.Advanced,ValueName="TaskbarDa",OnValue=1,OffValue=0,DefaultOn=true}
  };

  public static bool IsWindows { get { return StubbornFiles.IsWindows; } }

  /// <summary>Vietnamese label of a tweak group (translated by Core.L).</summary>
  public static string GroupLabel(TweakGroup g){return L.T(g==TweakGroup.Files?"Hiển thị tệp":g==TweakGroup.Navigation?"Điều hướng":"Thanh tác vụ");}

  /// <summary>Maps a raw registry value to on/off; a missing value means the Windows default and any other number is reported as unknown.</summary>
  public static bool Interpret(ExplorerTweak t,object raw,out bool known){
   known=false;if(t==null)return false;
   if(raw==null)return t.DefaultOn;
   int v;if(raw is int)v=(int)raw;else if(raw is long)v=(int)(long)raw;else if(!Int32.TryParse(Convert.ToString(raw),out v))return t.DefaultOn;
   if(v==t.OnValue){known=true;return true;}
   if(v==t.OffValue){known=true;return false;}
   return t.DefaultOn;
  }

  /// <summary>Reads every catalog tweak from HKCU; off Windows every entry reports its default as unknown.</summary>
  public static List<TweakState> Read(){
   var list=new List<TweakState>();
   foreach(var t in Catalog){
    object raw=null;bool known=false;
    if(IsWindows){try{raw=ReadValue(t.KeyPath,t.ValueName);}catch(Exception e){Log.Warn("Explorer: không đọc được "+t.ValueName+": "+e.Message);}}
    bool enabled=Interpret(t,raw,out known);
    list.Add(new TweakState{Tweak=t,Enabled=enabled,Known=known,Missing=raw==null,Raw=raw});
   }
   return list;
  }

  static object ReadValue(string keyPath,string name){using(var key=Registry.CurrentUser.OpenSubKey(keyPath,false))return key==null?null:key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);}

  /// <summary>Serializes the previous registry value for the vault manifest ("absent", "dword:1", "string:x"…).</summary>
  public static string SerializePrior(object raw){
   if(raw==null)return "absent";
   if(raw is int)return "dword:"+(int)raw;
   if(raw is long)return "qword:"+(long)raw;
   if(raw is string)return "string:"+(string)raw;
   if(raw is byte[])return "binary:"+BitConverter.ToString((byte[])raw).Replace("-","");
   return "other:"+Convert.ToString(raw);
  }
  /// <summary>Parses a manifest payload back into a registry value; false for anything Tweek Pro did not write.</summary>
  public static bool TryParsePrior(string payload,out object value,out RegistryValueKind kind){
   value=null;kind=RegistryValueKind.Unknown;if(payload==null)return false;
   if(payload=="absent")return true;
   int colon=payload.IndexOf(':');if(colon<=0)return false;
   string type=payload.Substring(0,colon),body=payload.Substring(colon+1);
   int i;long l;
   if(type=="dword"&&Int32.TryParse(body,out i)){value=i;kind=RegistryValueKind.DWord;return true;}
   if(type=="qword"&&Int64.TryParse(body,out l)){value=l;kind=RegistryValueKind.QWord;return true;}
   if(type=="string"){value=body;kind=RegistryValueKind.String;return true;}
   if(type=="binary"&&body.Length%2==0){try{var bytes=new byte[body.Length/2];for(int k=0;k<bytes.Length;k++)bytes[k]=Convert.ToByte(body.Substring(k*2,2),16);value=bytes;kind=RegistryValueKind.Binary;return true;}catch(FormatException){return false;}}
   return false;
  }

  /// <summary>Writes one tweak after saving the prior value to the vault; returns the backup (State=BackedUp).</summary>
  public static Backup Apply(ExplorerTweak t,bool enable){
   if(!ExplorerSafety.Allowed(t))throw new IOException(L.T("Tùy chỉnh này không nằm trong danh mục cho phép của Tweek Pro."));
   if(!IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   object prior=ReadValue(t.KeyPath,t.ValueName);
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Original=t.KeyPath,ValueName=t.ValueName,Kind=BackupKind,AppName=t.Name,Purpose=enable?"Enable":"Disable",Payload=SerializePrior(prior),Hive="HKCU"};
   Engine.NoLinks(Engine.Vault,false);Directory.CreateDirectory(Path.Combine(Engine.Vault,backup.Id));Engine.SaveBackup(backup);
   try{
    using(var key=Registry.CurrentUser.CreateSubKey(t.KeyPath,RegistryKeyPermissionCheck.ReadWriteSubTree))key.SetValue(t.ValueName,enable?t.OnValue:t.OffValue,RegistryValueKind.DWord);
    backup.State="BackedUp";backup.Error=L.F("Giá trị cũ: {0}; khôi phục = ghi lại giá trị cũ (hoặc xóa nếu trước đó chưa có).",PriorLabel(prior));Engine.SaveBackup(backup);
    Log.Info("Explorer: "+t.Id+" -> "+(enable?"on":"off")+" ("+t.ValueName+"="+(enable?t.OnValue:t.OffValue)+", prior "+backup.Payload+")");
    return backup;
   }catch(Exception e){backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;}
  }

  static string PriorLabel(object prior){return prior==null?L.T("chưa có"):Convert.ToString(prior);}

  /// <summary>Restores the previous value recorded by Apply (or deletes the value when it did not exist before).</summary>
  public static void Restore(Backup b){
   if(b==null||b.Kind!=BackupKind)throw new IOException(L.T("Không phải bản sao lưu tùy chỉnh Explorer."));
   if(!ExplorerSafety.AllowedKey(b.Original)||String.IsNullOrWhiteSpace(b.ValueName)||b.ValueName.IndexOfAny(new[]{'\\','/'})>=0)throw new IOException(L.T("Bản sao lưu trỏ ra ngoài các khóa Explorer cho phép; từ chối khôi phục."));
   object value;RegistryValueKind kind;
   if(!TryParsePrior(b.Payload,out value,out kind))throw new IOException(L.T("Không đọc được giá trị cũ trong bản sao lưu."));
   if(!IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   using(var key=Registry.CurrentUser.CreateSubKey(b.Original,RegistryKeyPermissionCheck.ReadWriteSubTree)){
    if(value==null)key.DeleteValue(b.ValueName,false);else key.SetValue(b.ValueName,value,kind);
   }
   b.State="Restored";b.Error="";Engine.SaveBackup(b);
   Log.Info("Explorer: khôi phục "+b.ValueName+" = "+b.Payload);
   RefreshExplorer();
  }

  [DllImport("shell32.dll")]static extern void SHChangeNotify(int eventId,uint flags,IntPtr item1,IntPtr item2);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessageTimeout(IntPtr hWnd,uint msg,UIntPtr wParam,string lParam,uint flags,uint timeout,out UIntPtr result);
  const int ShcneAssocChanged=0x08000000;const uint ShcnfIdList=0x0,ShcnfFlush=0x1000,WmSettingChange=0x1A,SmtoAbortIfHung=0x2;

  /// <summary>Asks running Explorer windows to re-read the folder settings (the same notifications Folder Options sends); harmless off Windows.</summary>
  public static void RefreshExplorer(){
   if(!IsWindows)return;
   try{SHChangeNotify(ShcneAssocChanged,ShcnfIdList|ShcnfFlush,IntPtr.Zero,IntPtr.Zero);}catch(Exception e){Log.Warn("Explorer refresh: "+e.Message);}
   try{UIntPtr r;SendMessageTimeout(new IntPtr(0xffff),WmSettingChange,UIntPtr.Zero,"ShellState",SmtoAbortIfHung,2000,out r);}catch(Exception e){Log.Warn("Explorer settings broadcast: "+e.Message);}
  }

  /// <summary>Ends every explorer.exe of the current session and starts a new, non-elevated shell (runas /trustlevel) so taskbar settings take effect.</summary>
  public static int RestartExplorer(){
   if(!IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   int killed=0;int session=Process.GetCurrentProcess().SessionId;
   foreach(var p in Process.GetProcessesByName("explorer")){using(p){try{if(p.SessionId!=session)continue;p.Kill();p.WaitForExit(5000);killed++;}catch(Exception e){Log.Warn("Explorer restart: "+e.Message);}}}
   string explorer=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
   try{Process.Start(new ProcessStartInfo("runas.exe","/trustlevel:0x20000 \""+explorer+"\""){UseShellExecute=false,CreateNoWindow=true});}
   catch(Exception e){Log.Warn("runas /trustlevel failed, starting explorer directly: "+e.Message);Process.Start(explorer);}
   Log.Info("Explorer restarted ("+killed+" process(es) ended).");
   return killed;
  }
 }

 /// <summary>Shell helpers around a file: the Windows Properties dialog and reveal-in-Explorer.</summary>
 public static class ExplorerShell {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct ShellExecuteInfo{
   public int cbSize;public uint fMask;public IntPtr hwnd;[MarshalAs(UnmanagedType.LPWStr)]public string lpVerb;[MarshalAs(UnmanagedType.LPWStr)]public string lpFile;[MarshalAs(UnmanagedType.LPWStr)]public string lpParameters;[MarshalAs(UnmanagedType.LPWStr)]public string lpDirectory;public int nShow;public IntPtr hInstApp;public IntPtr lpIDList;[MarshalAs(UnmanagedType.LPWStr)]public string lpClass;public IntPtr hkeyClass;public uint dwHotKey;public IntPtr hIcon;public IntPtr hProcess;
  }
  [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool ShellExecuteEx(ref ShellExecuteInfo info);
  const uint SeeMaskInvokeIdList=0x0000000C;

  /// <summary>Opens the native Properties dialog (General / Security / Details tabs) for a file or folder.</summary>
  public static void ShowProperties(IntPtr owner,string path){
   if(!StubbornFiles.IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   if(!File.Exists(path)&&!Directory.Exists(path))throw new FileNotFoundException(L.T("Đường dẫn không còn tồn tại: ")+path);
   var info=new ShellExecuteInfo{cbSize=Marshal.SizeOf(typeof(ShellExecuteInfo)),fMask=SeeMaskInvokeIdList,hwnd=owner,lpVerb="properties",lpFile=path,nShow=5};
   if(!ShellExecuteEx(ref info))throw new IOException(L.F("Không mở được hộp thoại Thuộc tính (mã {0}).",Marshal.GetLastWin32Error()));
  }
  /// <summary>Reveals the file in a new Explorer window.</summary>
  public static void Reveal(string path){
   if(!File.Exists(path)&&!Directory.Exists(path))throw new FileNotFoundException(L.T("Đường dẫn không còn tồn tại: ")+path);
   Process.Start("explorer.exe","/select,\""+path+"\"");
  }
 }
}
