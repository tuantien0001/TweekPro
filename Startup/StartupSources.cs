using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using TweekPro.Core;
using TweekPro.Network;
using TweekPro.Services;

namespace TweekPro.Startup {
 /// <summary>StartupTask.State values Windows stores for a packaged app (WinRT StartupTaskState).</summary>
 public static class StartupTaskRules {
  public const int Disabled=0,DisabledByUser=1,Enabled=2,DisabledByPolicy=3,EnabledByPolicy=4;
  public static bool Runs(int state){return state==Enabled||state==EnabledByPolicy;}
  public static bool Locked(int state){return state==DisabledByPolicy||state==EnabledByPolicy;}
  public static string Label(int state){
   switch(state){
    case Enabled:case EnabledByPolicy:return "Đang bật";
    case DisabledByUser:return "Người dùng đã tắt";
    case DisabledByPolicy:return "Chính sách đã tắt";
    case Disabled:return "Đang tắt";
    default:return "Không rõ";
   }
  }
  /// <summary>Readable app name from a package family (Claude_8wekyb3d8bbwe) and its task id.</summary>
  public static string AppLabel(string packageFamily,string taskId){
   string name=packageFamily??"";int us=name.LastIndexOf('_');
   if(us>0&&name.Length-us-1>=10)name=name.Substring(0,us);
   if(name.StartsWith("Microsoft.",StringComparison.OrdinalIgnoreCase))name=name.Substring("Microsoft.".Length);
   string task=taskId??"";
   if(task.IndexOf("defender",StringComparison.OrdinalIgnoreCase)>=0)return "Microsoft Defender";
   if(String.Equals(name,"GamingApp",StringComparison.OrdinalIgnoreCase))return "Xbox";
   if(String.Equals(name,"WindowsTerminal",StringComparison.OrdinalIgnoreCase))return "Windows Terminal";
   if(String.Equals(name,"MicrosoftOfficeHub",StringComparison.OrdinalIgnoreCase))return "Microsoft 365";
   if(String.Equals(name,"StartExperiencesApp",StringComparison.OrdinalIgnoreCase))return "Microsoft Start";
   if(String.Equals(name,"MSTeams",StringComparison.OrdinalIgnoreCase))return "Microsoft Teams";
   if(String.Equals(name,"Copilot",StringComparison.OrdinalIgnoreCase))return "Copilot";
   return name.Replace('.',' ').Trim();
  }
 }

 /// <summary>
 /// Extra logon sources Revo's Autorun Manager shows beyond Run/RunOnce/Startup shortcuts:
 /// packaged-app startup tasks (HKCU AppModel SystemAppData) and services whose start mode is Automatic.
 /// Kernel drivers and core Windows services stay visible for services but cannot be switched off.
 /// </summary>
 public static class StartupSources {
  public const string BackupKind="StartupTask";
  public const string AppModelRoot=@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";
  public const string GroupRun="Run / Startup",GroupApps="Ứng dụng Windows",GroupServices="Dịch vụ tự chạy";

  /// <summary>True for a Win32 service that starts at boot (WMI StartMode Auto), excluding kernel drivers.</summary>
  public static bool IsAutoService(string startMode,string serviceType){
   if(!String.Equals(startMode,"Auto",StringComparison.OrdinalIgnoreCase)&&!String.Equals(startMode,"Automatic",StringComparison.OrdinalIgnoreCase))return false;
   return (serviceType??"").IndexOf("Driver",StringComparison.OrdinalIgnoreCase)<0;
  }

  /// <summary>Splits a SystemAppData task key into package family and task id; rejects anything that is not exactly those two segments.</summary>
  public static bool SplitTask(string path,out string package,out string task){
   package=task="";if(String.IsNullOrEmpty(path))return false;
   string prefix=AppModelRoot+"\\";if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))return false;
   string rest=path.Substring(prefix.Length);int slash=rest.IndexOf('\\');
   if(slash<=0||slash>=rest.Length-1||rest.IndexOf('\\',slash+1)>=0)return false;
   package=rest.Substring(0,slash);task=rest.Substring(slash+1);
   return SafeSegment(package)&&SafeSegment(task);
  }

  static bool SafeSegment(string s){
   if(String.IsNullOrEmpty(s)||s.Length>180)return false;
   foreach(char c in s)if(!(Char.IsLetterOrDigit(c)||c=='.'||c=='_'||c=='-'||c==' '))return false;
   return true;
  }

  /// <summary>Appends packaged startup tasks and automatic services. Both probes are skipped off Windows and never throw.</summary>
  public static void Append(List<AutorunEntry> items){
   if(!ProcessControl.IsWindows)return;
   try{AddWindowsApps(items);}catch(Exception){}
   try{AddAutoServices(items);}catch(Exception){}
  }

  static void AddWindowsApps(List<AutorunEntry> items){
   using(var root=Engine.Base("HKCU","64"))using(var parent=root.OpenSubKey(AppModelRoot)){
    if(parent==null)return;
    foreach(string package in parent.GetSubKeyNames())using(var pkg=parent.OpenSubKey(package)){
     if(pkg==null)continue;
     foreach(string task in pkg.GetSubKeyNames())using(var key=pkg.OpenSubKey(task)){
      if(key==null||Array.IndexOf(key.GetValueNames(),"State")<0)continue;
      try{if(key.GetValueKind("State")!=RegistryValueKind.DWord)continue;}catch(Exception){continue;}
      int state=0;try{state=Convert.ToInt32(key.GetValue("State"));}catch(Exception){continue;}
      string path=AppModelRoot+"\\"+package+"\\"+task;string label=StartupTaskRules.AppLabel(package,task);bool runs=StartupTaskRules.Runs(state);
      items.Add(new AutorunEntry{Name=label,Command=task,Source=GroupApps,State=StartupTaskRules.Label(state),Insight=new StartupInsight{Approval=new StartupApproval{Known=true,Enabled=runs},Impact=runs?StartupImpact.Unknown:StartupImpact.Disabled},Item=new Candidate{Kind=BackupKind,Path=path,Hive="HKCU",View="64",ValueName="State",AppName=label,Reason=state.ToString()}});
     }
    }
   }
  }

  static void AddAutoServices(List<AutorunEntry> items){
   foreach(var e in ServiceCatalog.List()){
    if(!IsAutoService(e.StartMode,e.ServiceType))continue;
    bool core=ProcessControl.IsCoreService(e.Name);
    string name=String.IsNullOrWhiteSpace(e.DisplayName)?e.Name:e.DisplayName;
    items.Add(new AutorunEntry{Name=name,Command=e.PathName,Source=GroupServices,State=core?"Dịch vụ cốt lõi":"Tự chạy cùng máy",Publisher=e.Publisher,Insight=new StartupInsight{Target=e.Executable??"",Signer=e.Publisher??"",Signed=!String.IsNullOrEmpty(e.Publisher),Approval=new StartupApproval{Known=true,Enabled=true},Impact=StartupImpact.Unknown},Item=new Candidate{Kind="Service",Path=e.Executable??"",ValueName=e.Name,AppName=name,Reason=e.StartMode}});
   }
  }

  /// <summary>Turns a packaged startup task off (State = DisabledByUser) and stores the previous dword in the vault.</summary>
  public static Backup DisableTask(Candidate c){return WriteTask(c,StartupTaskRules.DisabledByUser,true);}
  /// <summary>Turns a packaged startup task on (State = Enabled) and stores the previous dword in the vault.</summary>
  public static Backup EnableTask(Candidate c){return WriteTask(c,StartupTaskRules.Enabled,false);}

  static Backup WriteTask(Candidate c,int next,bool mustBeRunning){
   string package,task;if(c==null||!SplitTask(c.Path,out package,out task))throw new IOException(L.T("Không phải mục khởi động của ứng dụng Windows."));
   if(!ProcessControl.IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   using(var root=Engine.Base("HKCU","64"))using(var key=root.OpenSubKey(c.Path,true)){
    if(key==null)throw new IOException(L.T("Mục khởi động không còn tồn tại."));
    if(Array.IndexOf(key.GetValueNames(),"State")<0||key.GetValueKind("State")!=RegistryValueKind.DWord)throw new IOException(L.T("Mục khởi động không có trạng thái hợp lệ."));
    int state=Convert.ToInt32(key.GetValue("State"));
    if(StartupTaskRules.Locked(state))throw new IOException(L.T("Chính sách Windows khóa mục khởi động này."));
    if(mustBeRunning&&!StartupTaskRules.Runs(state))throw new IOException(L.T("Mục khởi động này đang tắt."));
    if(!mustBeRunning&&StartupTaskRules.Runs(state))throw new IOException(L.T("Mục khởi động này đang bật."));
    int once=0;try{if(Array.IndexOf(key.GetValueNames(),"UserEnabledStartupOnce")>=0)once=Convert.ToInt32(key.GetValue("UserEnabledStartupOnce"));}catch(Exception){}
    var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Original=c.Path,Kind=BackupKind,Hive="HKCU",View="64",AppName=c.AppName,ValueName="State",Purpose="Autorun",Payload="state.txt"};
    Engine.NoLinks(Engine.Vault,false);Directory.CreateDirectory(Path.Combine(Engine.Vault,backup.Id));Engine.SaveBackup(backup);
    try{
     File.WriteAllText(Path.Combine(Engine.Vault,backup.Id,backup.Payload),state+"\n"+once);
     key.SetValue("State",next,RegistryValueKind.DWord);key.SetValue("UserEnabledStartupOnce",1,RegistryValueKind.DWord);
     backup.State="BackedUp";Engine.SaveBackup(backup);return backup;
    }catch(Exception e){backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;}
   }
  }

  /// <summary>Writes the saved State and UserEnabledStartupOnce back. Refuses a key outside SystemAppData.</summary>
  public static void Restore(Backup b){
   if(b==null||b.Kind!=BackupKind)throw new IOException(L.T("Không phải bản sao lưu khởi động ứng dụng Windows."));
   string package,task;if(!SplitTask(b.Original,out package,out task))throw new IOException(L.T("Đường dẫn khôi phục không hợp lệ."));
   if(!ProcessControl.IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   string payload=Path.Combine(Engine.VaultOf(b),b.Id,b.Payload??"state.txt");Engine.NoLinks(payload,false);
   var lines=File.ReadAllLines(payload);if(lines.Length<1)throw new IOException(L.T("Bản sao lưu trống."));
   int state,once=0;if(!Int32.TryParse(lines[0],out state)||state<0||state>4)throw new IOException(L.T("Trạng thái đã lưu không hợp lệ."));
   if(lines.Length>1)Int32.TryParse(lines[1],out once);
   using(var root=Engine.Base("HKCU","64"))using(var key=root.OpenSubKey(b.Original,true)){
    if(key==null)throw new IOException(L.T("Mục khởi động không còn tồn tại."));
    key.SetValue("State",state,RegistryValueKind.DWord);key.SetValue("UserEnabledStartupOnce",once,RegistryValueKind.DWord);
   }
   b.State="Restored";b.Error="";Engine.SaveBackup(b);
  }
 }
}
