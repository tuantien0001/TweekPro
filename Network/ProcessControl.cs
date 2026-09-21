using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using TweekPro.Core;

namespace TweekPro.Network {
 /// <summary>A Windows service hosted by a process, as reported by WMI Win32_Service.</summary>
 public class HostedService { public string Name="", DisplayName="", State="", StartMode="", PathName=""; public int Pid; }

 /// <summary>
 /// Terminates processes and stops/disables services from the Network tab. Core Windows processes and services are refused
 /// in code; disabling a service is recorded in the vault (Kind=Service) so its start mode can be restored.
 /// </summary>
 public static class ProcessControl {
  public static readonly bool IsWindows=Environment.OSVersion.Platform==PlatformID.Win32NT;

  /// <summary>Processes whose termination logs the user off, blue-screens or disables security; never offered.</summary>
  public static readonly string[] CoreProcesses={"system","system idle process","registry","memory compression","secure system","smss.exe","csrss.exe","wininit.exe","winlogon.exe","services.exe","lsass.exe","svchost.exe","dwm.exe","fontdrvhost.exe","msmpeng.exe","logonui.exe","sihost.exe","ctfmon.exe","conhost.exe"};

  /// <summary>Services whose stop breaks networking, logon, security or the shell; never offered.</summary>
  public static readonly string[] CoreServices={"RpcSs","RpcEptMapper","DcomLaunch","LSM","PlugPlay","Power","BrokerInfrastructure","SystemEventsBroker","Winmgmt","EventLog","ProfSvc","UserManager","CoreMessagingRegistrar","Dhcp","Dnscache","nsi","NlaSvc","Wcmsvc","WinDefend","SecurityHealthService","CryptSvc","TrustedInstaller","gpsvc","SamSs","KeyIso","Netman","netprofm","LanmanWorkstation","Schedule","StateRepository","TimeBrokerSvc","Appinfo","Themes","AudioSrv","AudioEndpointBuilder","EventSystem","FontCache","BFE","mpssvc","VaultSvc","TokenBroker","LicenseManager","sppsvc","SENS","ShellHWDetection","DispBrokerDesktopSvc","WlanSvc","WWANSvc","Wlansvc","DeviceInstall","SgrmBroker","WdNisSvc","Sense","webthreatdefsvc","webthreatdefusersvc"};

  [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(int access,bool inherit,int pid);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool TerminateProcess(IntPtr process,uint exitCode);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
  const int PROCESS_TERMINATE=0x0001;

  /// <summary>Returns null when the process may be terminated, otherwise the localized reason it is refused.</summary>
  public static string TerminateBlockReason(int pid,string name){
   if(pid==0||pid==4)return L.T("Tiến trình cốt lõi của Windows — không thể kết thúc.");
   if(pid==Process.GetCurrentProcess().Id)return L.T("Đây là Tweek Pro.");
   string n=(name??"").Trim().ToLowerInvariant();
   if(n=="")return null;
   if(CoreProcesses.Contains(n))return n=="svchost.exe"?L.T("svchost.exe chứa nhiều dịch vụ Windows — hãy dừng từng dịch vụ bên trong thay vì kết thúc tiến trình."):L.T("Tiến trình cốt lõi của Windows — không thể kết thúc.");
   return null;
  }

  /// <summary>True when the service is on the hard-coded core list (compared case-insensitively by service name).</summary>
  public static bool IsCoreService(string name){return !String.IsNullOrEmpty(name)&&CoreServices.Any(c=>String.Equals(c,name,StringComparison.OrdinalIgnoreCase));}

  /// <summary>Optional replacement for the WMI lookup (layout previews and tests); null uses WMI.</summary>
  public static Func<int,List<HostedService>> ServiceProvider;

  /// <summary>Services hosted by a process (WMI Win32_Service by ProcessId); empty off Windows or when WMI is unavailable.</summary>
  public static List<HostedService> ServicesOf(int pid){
   var list=new List<HostedService>();
   if(ServiceProvider!=null)return ServiceProvider(pid)??list;
   if(!IsWindows||pid<=4)return list;
   try{
    using(var searcher=new System.Management.ManagementObjectSearcher("SELECT Name,DisplayName,State,StartMode,PathName,ProcessId FROM Win32_Service WHERE ProcessId="+pid))
    using(var results=searcher.Get()){
     foreach(System.Management.ManagementBaseObject o in results){
      list.Add(new HostedService{Pid=pid,Name=Convert.ToString(o["Name"])??"",DisplayName=Convert.ToString(o["DisplayName"])??"",State=Convert.ToString(o["State"])??"",StartMode=Convert.ToString(o["StartMode"])??"",PathName=Convert.ToString(o["PathName"])??""});
     }
    }
   }catch(Exception e){Log.Warn("Không đọc được dịch vụ của PID "+pid+": "+e.Message);}
   return list.OrderBy(s=>s.DisplayName,StringComparer.CurrentCultureIgnoreCase).ToList();
  }

  /// <summary>Terminates a process after the core-process check; falls back to TerminateProcess when the managed Kill is refused.</summary>
  public static void Terminate(int pid,string name){
   string reason=TerminateBlockReason(pid,name);
   if(reason!=null)throw new IOException(reason);
   if(!IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   Process p;
   try{p=Process.GetProcessById(pid);}catch(ArgumentException){throw new IOException(L.T("Tiến trình đã kết thúc."));}
   using(p){
    try{p.Kill();}
    catch(Win32Exception){
     IntPtr handle=OpenProcess(PROCESS_TERMINATE,false,pid);
     if(handle==IntPtr.Zero)throw new IOException(L.T("Không đủ quyền để kết thúc tiến trình này (tiến trình được bảo vệ)."));
     try{if(!TerminateProcess(handle,1))throw new IOException(L.T("Windows từ chối kết thúc tiến trình này."));}finally{CloseHandle(handle);}
    }
    catch(InvalidOperationException){return;}
    if(!p.WaitForExit(8000))throw new IOException(L.T("Tiến trình chưa kết thúc sau 8 giây."));
   }
   Log.Info("Đã kết thúc tiến trình "+name+" (PID "+pid+").");
  }

  /// <summary>Maps a WMI StartMode to the sc.exe start= argument.</summary>
  public static string StartModeArgument(string wmiStartMode){
   switch((wmiStartMode??"").Trim().ToLowerInvariant()){
    case "auto":case "automatic":return "auto";
    case "disabled":return "disabled";
    case "boot":return "boot";
    case "system":return "system";
    default:return "demand";
   }
  }

  /// <summary>Validates a service name for use on the sc.exe command line (letters, digits, and a few punctuation marks only).</summary>
  public static bool ValidServiceName(string name){
   if(String.IsNullOrEmpty(name)||name.Length>256)return false;
   foreach(char c in name)if(!(Char.IsLetterOrDigit(c)||c=='_'||c=='-'||c=='.'||c==' '||c=='$'||c=='{'||c=='}'||c=='@'))return false;
   return true;
  }

  static void Sc(params string[] args){
   var psi=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"sc.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   psi.Arguments=String.Join(" ",args.Select(a=>a.Contains(" ")?"\""+a+"\"":a));
   using(var p=Process.Start(psi)){string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();if(!p.WaitForExit(15000)||p.ExitCode!=0)throw new IOException("sc.exe: "+output.Trim());}
  }

  /// <summary>Stops a service (and optionally disables it, recording the previous start mode in the vault for restore).</summary>
  public static Backup StopService(HostedService s,bool disable){
   if(IsCoreService(s.Name))throw new IOException(L.F("{0} là dịch vụ cốt lõi của Windows — không dừng.",s.DisplayName));
   if(!ValidServiceName(s.Name))throw new IOException(L.T("Tên dịch vụ không hợp lệ."));
   if(!IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   Backup backup=null;
   if(disable){
    backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Original=s.Name,Kind="Service",AppName=s.DisplayName,ValueName=s.StartMode,Purpose="Disabled",Payload=s.PathName};
    Engine.NoLinks(Engine.Vault,false);Directory.CreateDirectory(Path.Combine(Engine.Vault,backup.Id));Engine.SaveBackup(backup);
   }
   try{
    using(var controller=new ServiceController(s.Name)){
     if(controller.Status!=ServiceControllerStatus.Stopped){
      if(!controller.CanStop)throw new IOException(L.T("Dịch vụ không cho phép dừng (CanStop=false)."));
      controller.Stop();controller.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(30));
     }
    }
    if(disable){Sc("config",s.Name,"start=","disabled");backup.State="BackedUp";backup.Error=L.F("Đã dừng và vô hiệu hóa; khôi phục = đặt lại kiểu khởi động {0} và chạy lại dịch vụ.",s.StartMode);Engine.SaveBackup(backup);}
    Log.Info((disable?"Đã dừng và vô hiệu hóa dịch vụ ":"Đã dừng dịch vụ ")+s.Name+" ("+s.DisplayName+").");
    return backup;
   }catch(Exception e){
    if(backup!=null){backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);}
    throw new IOException(L.F("Không dừng được dịch vụ {0}: {1}",s.DisplayName,e.Message));
   }
  }

  /// <summary>Restores a disabled service: puts the previous start mode back and starts it when that mode is not disabled.</summary>
  public static void Restore(Backup b){
   if(b.Kind!="Service")throw new IOException(L.T("Không phải bản sao lưu dịch vụ."));
   if(!ValidServiceName(b.Original))throw new IOException(L.T("Tên dịch vụ không hợp lệ."));
   if(!IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   string mode=StartModeArgument(b.ValueName);
   Sc("config",b.Original,"start=",mode);
   if(mode!="disabled"){
    try{using(var controller=new ServiceController(b.Original)){if(controller.Status==ServiceControllerStatus.Stopped){controller.Start();controller.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(30));}}}
    catch(Exception e){b.State="Restored";Engine.SaveBackup(b);throw new IOException(L.F("Đã đặt lại kiểu khởi động nhưng chưa chạy được dịch vụ: {0}",e.Message));}
   }
   b.State="Restored";Engine.SaveBackup(b);Log.Info("Đã khôi phục dịch vụ "+b.Original+" (kiểu khởi động "+mode+").");
  }
 }
}
