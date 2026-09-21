using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using TweekPro.Core;
using TweekPro.Network;

namespace TweekPro.Services {
 /// <summary>How safe it is to stop a service, from "never" to "third-party, stop freely".</summary>
 public enum ServiceSafety { Core, Windows, Optional, ThirdParty }

 [DataContract] public class KnownService {
  [DataMember(Name="name")] public string Name="";
  [DataMember(Name="friendly")] public string Friendly="";
  [DataMember(Name="category")] public string Category="";
  [DataMember(Name="safety")] public string Safety="";
  [DataMember(Name="vi")] public string Vi="";
  [DataMember(Name="en")] public string En="";
 }
 [DataContract] public class KnownPattern {
  [DataMember(Name="contains")] public string Contains="";
  [DataMember(Name="category")] public string Category="";
  [DataMember(Name="safety")] public string Safety="";
  [DataMember(Name="vi")] public string Vi="";
  [DataMember(Name="en")] public string En="";
 }
 [DataContract] public class ServiceKnowledge {
  [DataMember(Name="services")] public List<KnownService> Services=new List<KnownService>();
  [DataMember(Name="patterns")] public List<KnownPattern> Patterns=new List<KnownPattern>();
 }

 /// <summary>One Windows service with its live state plus the plain-language explanation and safety verdict.</summary>
 public class ServiceEntry {
  public string Name="", DisplayName="", Description="", State="", StartMode="", PathName="", Account="", ServiceType="";
  public int Pid; public long Memory=-1;
  public string Friendly="", Explanation="", Category="", Publisher="", Executable="";
  public ServiceSafety Safety=ServiceSafety.Windows;
  public bool Running { get { return String.Equals(State,"Running",StringComparison.OrdinalIgnoreCase); } }
  public bool PerUser { get { return ServiceCatalog.PerUserSuffix.IsMatch(Name); } }
  public string BaseName { get { return ServiceCatalog.BaseNameOf(Name); } }
  public HostedService ToHosted(){return new HostedService{Pid=Pid,Name=Name,DisplayName=DisplayName,State=State,StartMode=StartMode,PathName=PathName};}
 }

 /// <summary>
 /// Lists Windows services (WMI Win32_Service) and explains each one in plain language using the embedded knowledge base,
 /// name patterns and the executable location; classifies how safe it is to stop.
 /// </summary>
 public static class ServiceCatalog {
  public const string ResourceName="TweekPro.Services.service-knowledge.json";
  /// <summary>Per-user services carry a random 5-hex suffix (e.g. cbdhsvc_4a2b1).</summary>
  public static readonly Regex PerUserSuffix=new Regex(@"_[0-9a-f]{4,8}$",RegexOptions.IgnoreCase|RegexOptions.Compiled);
  static ServiceKnowledge knowledge;

  /// <summary>Knowledge base compiled into the executable (loaded once).</summary>
  public static ServiceKnowledge Knowledge { get { if(knowledge==null)knowledge=LoadEmbedded();return knowledge; } }

  public static ServiceKnowledge LoadEmbedded(){
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)){
    if(stream==null)throw new IOException("Thiếu tài nguyên "+ResourceName);
    return (ServiceKnowledge)new DataContractJsonSerializer(typeof(ServiceKnowledge)).ReadObject(stream);
   }
  }

  /// <summary>Strips the per-user suffix so cbdhsvc_4a2b1 matches the cbdhsvc entry.</summary>
  public static string BaseNameOf(string name){return PerUserSuffix.Replace(name??"","");}

  /// <summary>Path of the executable inside a service command line (handles quotes and svchost -k groups).</summary>
  public static string ExecutableOf(string pathName){
   if(String.IsNullOrWhiteSpace(pathName))return "";
   string p=Environment.ExpandEnvironmentVariables(pathName.Trim());
   if(p.StartsWith(@"\??\",StringComparison.Ordinal))p=p.Substring(4);
   if(p.StartsWith("\"")){int end=p.IndexOf('"',1);return end>0?p.Substring(1,end-1):p.Trim('"');}
   int exe=p.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);
   return exe>0?p.Substring(0,exe+4):p.Split(' ')[0];
  }

  /// <summary>Microsoft's own signing names: such binaries count as Windows components even outside the Windows folder (Defender platform, Edge WebView…).</summary>
  public static bool IsMicrosoftSigner(string publisher){
   if(String.IsNullOrEmpty(publisher))return false;
   return publisher.StartsWith("Microsoft Windows",StringComparison.OrdinalIgnoreCase)||String.Equals(publisher,"Microsoft Corporation",StringComparison.OrdinalIgnoreCase);
  }

  static ServiceSafety ParseSafety(string s){
   switch((s??"").ToLowerInvariant()){case "core":return ServiceSafety.Core;case "optional":return ServiceSafety.Optional;case "thirdparty":return ServiceSafety.ThirdParty;default:return ServiceSafety.Windows;}
  }

  /// <summary>Best-effort application name for pattern explanations: the display name without generic words.</summary>
  public static string AppNameOf(string displayName,string name){
   string s=String.IsNullOrWhiteSpace(displayName)?name:displayName;
   s=Regex.Replace(s??"",@"\(.*?\)","");
   s=Regex.Replace(s,@"(?i)\b(update[rs]?|service|elevation|helper|dịch vụ)\b","").Trim();
   s=Regex.Replace(s,@"\s{2,}"," ").Trim(' ','-','_');
   return s==""?(name??""):s;
  }

  /// <summary>Fills Friendly, Explanation, Category and Safety from the knowledge base, name patterns and executable location.</summary>
  public static void Explain(ServiceEntry e){
   string baseName=e.BaseName;
   var known=Knowledge.Services.FirstOrDefault(k=>String.Equals(k.Name,baseName,StringComparison.OrdinalIgnoreCase))??Knowledge.Services.FirstOrDefault(k=>String.Equals(k.Name,e.Name,StringComparison.OrdinalIgnoreCase));
   e.Executable=ExecutableOf(e.PathName);
   if(e.Publisher=="Không ký"||e.Publisher=="Đã ký"||e.Publisher==null)e.Publisher="";
   string windir=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
   bool inWindows=windir!=""&&e.Executable.StartsWith(windir,StringComparison.OrdinalIgnoreCase);
   if(!inWindows&&IsMicrosoftSigner(e.Publisher))inWindows=true;
   if(inWindows&&e.Publisher=="")e.Publisher="Microsoft Windows";
   if(known!=null){
    e.Friendly=known.Friendly;e.Category=known.Category;e.Safety=ParseSafety(known.Safety);
    e.Explanation=L.English&&!String.IsNullOrEmpty(known.En)?known.En:known.Vi;
    if(e.PerUser)e.Explanation+=" "+L.T("(dịch vụ theo tài khoản; Windows tự tạo lại khi đăng nhập)");
   }else{
    e.Friendly=String.IsNullOrWhiteSpace(e.DisplayName)?e.Name:e.DisplayName;
    string hay=(e.Name+" "+e.DisplayName).ToLowerInvariant();
    var pattern=inWindows?null:Knowledge.Patterns.FirstOrDefault(p=>hay.Contains(p.Contains.ToLowerInvariant()));
    if(pattern!=null){
     e.Category=pattern.Category;e.Safety=ParseSafety(pattern.Safety);
     e.Explanation=(L.English?pattern.En:pattern.Vi).Replace("{app}",AppNameOf(e.DisplayName,e.Name));
    }else if(inWindows){
     e.Category="windows";e.Safety=ServiceSafety.Windows;
     e.Explanation=String.IsNullOrWhiteSpace(e.Description)?L.T("Thành phần của Windows (chạy từ thư mục Windows). Không có mô tả thêm."):e.Description;
    }else{
     e.Category="thirdparty";e.Safety=ServiceSafety.ThirdParty;
     string app=String.IsNullOrEmpty(e.Publisher)?AppNameOf(e.DisplayName,e.Name):e.Publisher;
     e.Explanation=L.F("Dịch vụ nền của {0}. Ứng dụng cài kèm để chạy ngầm dù bạn chưa mở nó; dừng thường an toàn, ứng dụng sẽ tự bật lại khi cần.",app)+(String.IsNullOrWhiteSpace(e.Description)?"":"\r\n"+e.Description);
    }
   }
   if(ProcessControl.IsCoreService(e.Name))e.Safety=ServiceSafety.Core;
  }

  /// <summary>Short verdict for the safety level, in the active language.</summary>
  public static string SafetyLabel(ServiceSafety s){
   switch(s){
    case ServiceSafety.Core:return L.T("Cốt lõi — không dừng");
    case ServiceSafety.Windows:return L.T("Windows — dừng khi hiểu rõ");
    case ServiceSafety.Optional:return L.T("Tùy chọn — tắt được nếu không dùng");
    default:return L.T("Bên thứ ba — dừng an toàn");
   }
  }

  /// <summary>Longer advice for the details pane.</summary>
  public static string SafetyAdvice(ServiceSafety s){
   switch(s){
    case ServiceSafety.Core:return L.T("Windows cần dịch vụ này để chạy ổn định; Tweek Pro không cho dừng hay vô hiệu hóa.");
    case ServiceSafety.Windows:return L.T("Thuộc Windows nhưng không thiết yếu. Dừng có thể làm mất một tính năng; vô hiệu hóa được lưu vào Kho để bật lại.");
    case ServiceSafety.Optional:return L.T("Tính năng tùy chọn của Windows. Nếu bạn không dùng, tắt an toàn để tiết kiệm RAM và thời gian khởi động.");
    default:return L.T("Do ứng dụng bên thứ ba cài. Dừng an toàn; nếu vô hiệu hóa, ứng dụng đó có thể mất cập nhật tự động.");
   }
  }

  /// <summary>Category label, in the active language.</summary>
  public static string CategoryLabel(string category){
   switch((category??"").ToLowerInvariant()){
    case "core":return L.T("Hệ thống");case "network":return L.T("Mạng");case "security":return L.T("Bảo mật");case "update":case "updater":return L.T("Cập nhật");
    case "desktop":return L.T("Màn hình & âm thanh");case "optional":return L.T("Tùy chọn");case "privacy":return L.T("Quyền riêng tư");case "gaming":return L.T("Game");
    case "driver":return L.T("Driver");case "server":return L.T("Máy chủ");case "remote":return L.T("Điều khiển từ xa");case "peripheral":return L.T("Thiết bị ngoại vi");
    case "licensing":return L.T("Bản quyền");case "helper":return L.T("Phụ trợ");case "thirdparty":return L.T("Bên thứ ba");default:return L.T("Windows");
   }
  }

  /// <summary>Reads all services through WMI and explains them; empty off Windows.</summary>
  public static List<ServiceEntry> List(){
   var list=new List<ServiceEntry>();
   if(!ProcessControl.IsWindows)return list;
   using(var searcher=new System.Management.ManagementObjectSearcher("SELECT Name,DisplayName,Description,State,StartMode,ProcessId,PathName,StartName,ServiceType FROM Win32_Service"))
   using(var results=searcher.Get()){
    foreach(System.Management.ManagementBaseObject o in results){
     var e=new ServiceEntry{Name=Convert.ToString(o["Name"])??"",DisplayName=Convert.ToString(o["DisplayName"])??"",Description=Convert.ToString(o["Description"])??"",State=Convert.ToString(o["State"])??"",StartMode=Convert.ToString(o["StartMode"])??"",PathName=Convert.ToString(o["PathName"])??"",Account=Convert.ToString(o["StartName"])??"",ServiceType=Convert.ToString(o["ServiceType"])??""};
     try{e.Pid=Convert.ToInt32(o["ProcessId"]);}catch(Exception){e.Pid=0;}
     list.Add(e);
    }
   }
   string windir=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
   foreach(var e in list){
    e.Executable=ExecutableOf(e.PathName);
    bool inWindows=windir!=""&&e.Executable.StartsWith(windir,StringComparison.OrdinalIgnoreCase);
    if(e.Executable!=""&&!inWindows)try{e.Publisher=ProcessResolver.PublisherOf(e.Executable);}catch(Exception){e.Publisher="";}
    if(e.Pid>4){try{using(var p=Process.GetProcessById(e.Pid))e.Memory=p.WorkingSet64;}catch(Exception){e.Memory=-1;}}
    Explain(e);
   }
   return list.OrderBy(e=>e.Running?0:1).ThenBy(e=>e.Safety==ServiceSafety.ThirdParty?0:1).ThenBy(e=>e.Friendly,StringComparer.CurrentCultureIgnoreCase).ToList();
  }

  /// <summary>Starts a stopped service (core or not — starting is always safe).</summary>
  public static void Start(string name){
   if(!ProcessControl.ValidServiceName(name))throw new IOException(L.T("Tên dịch vụ không hợp lệ."));
   if(!ProcessControl.IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   using(var c=new System.ServiceProcess.ServiceController(name)){
    if(c.Status==System.ServiceProcess.ServiceControllerStatus.Running)return;
    c.Start();c.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running,TimeSpan.FromSeconds(30));
   }
   Log.Info("Đã khởi động dịch vụ "+name+".");
  }

  /// <summary>Stops then starts a service; refused for core services like every other stop.</summary>
  public static void Restart(ServiceEntry e){ProcessControl.StopService(e.ToHosted(),false);Start(e.Name);}
 }
}
