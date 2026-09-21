using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TweekPro.Core;

namespace TweekPro.Store {
 /// <summary>How Tweek Pro treats a package: removable, removable with a warning, or protected (never offered for removal).</summary>
 public enum AppxStatus { Removable, Caution, Protected }

 /// <summary>One installed Appx/MSIX package as reported by Get-AppxPackage.</summary>
 [DataContract] public class WindowsApp {
  [DataMember(Name="Name")] public string Name="";
  [DataMember(Name="PackageFullName")] public string FullName="";
  [DataMember(Name="PackageFamilyName")] public string FamilyName="";
  [DataMember(Name="Version")] public string Version="";
  [DataMember(Name="Publisher")] public string Publisher="";
  [DataMember(Name="InstallLocation")] public string InstallLocation="";
  [DataMember(Name="IsFramework")] public bool IsFramework;
  [DataMember(Name="NonRemovable")] public bool NonRemovable;
  [DataMember(Name="SignatureKind")] public int SignatureKind;
  [DataMember(Name="Architecture")] public string Architecture="";

  public string DisplayName { get { return WindowsApps.FriendlyName(Name); } }
  public string PublisherName { get { return WindowsApps.PublisherLabel(Publisher); } }
  /// <summary>System = signed by Windows itself; Store = from Microsoft Store; Other = sideloaded/developer/enterprise.</summary>
  public string Origin { get { return SignatureKind==4?"System":SignatureKind==3?"Store":SignatureKind==0?"Unsigned":"Other"; } }
  public AppxStatus Status { get { return WindowsApps.Classify(this); } }
  public string StatusReason { get { return WindowsApps.Reason(this); } }
 }

 /// <summary>
 /// Windows built-in and Store applications (Appx/MSIX). Listing, removal and re-registration go through PowerShell's Appx
 /// cmdlets; the safety classification here is code, not data, so no rule file can unlock a protected package.
 /// </summary>
 public static class WindowsApps {
  /// <summary>Packages that keep Windows usable even though Get-AppxPackage may report them as removable.</summary>
  public static readonly string[] CoreProtected={
   "Microsoft.Windows.ShellExperienceHost","Microsoft.Windows.StartMenuExperienceHost","Microsoft.Windows.Search","Microsoft.Windows.CloudExperienceHost",
   "Microsoft.Windows.SecHealthUI","Microsoft.SecHealthUI","Microsoft.Windows.AssignedAccessLockApp","Microsoft.LockApp","Microsoft.AAD.BrokerPlugin","Microsoft.AccountsControl",
   "Microsoft.CredDialogHost","Microsoft.Win32WebViewHost","Microsoft.Windows.Apprep.ChxApp","Microsoft.Windows.CapturePicker","Microsoft.Windows.ContentDeliveryManager",
   "Microsoft.Windows.OOBENetworkCaptivePortal","Microsoft.Windows.OOBENetworkConnectionFlow","Microsoft.Windows.ParentalControls","Microsoft.Windows.PeopleExperienceHost",
   "Microsoft.Windows.PinningConfirmationDialog","Microsoft.Windows.XGpuEjectDialog","Microsoft.Windows.CallingShellApp","Microsoft.Windows.NarratorQuickStart","Microsoft.Windows.PrintQueueActionCenter",
   "Microsoft.Windows.FileExplorer","Microsoft.Windows.AugLoop.CBS","Microsoft.ECApp","Microsoft.AsyncTextService","Microsoft.BioEnrollment","NcsiUwpApp","Windows.CBSPreview","Windows.PrintDialog",
   "Windows.immersivecontrolpanel","windows.immersivecontrolpanel","Microsoft.MicrosoftEdge","Microsoft.MicrosoftEdge.Stable","Microsoft.MicrosoftEdgeDevToolsClient","Microsoft.Windows.WindowsLensUI",
   "MicrosoftWindows.Client.CBS","MicrosoftWindows.Client.Core","MicrosoftWindows.Client.FileExp","MicrosoftWindows.Client.LKG","MicrosoftWindows.Client.AIX","MicrosoftWindows.Client.Photon","MicrosoftWindows.UndockedDevKit",
   "Microsoft.UI.Xaml.CBS","Microsoft.Windows.DevHomeGitHubExtension","Microsoft.WindowsAppRuntime.CBS","Microsoft.Windows.OOBENetworkCaptivePortal","Microsoft.XboxGameCallableUI","Microsoft.Windows.Ai.Copilot.Provider"
  };
  /// <summary>Inbox packages under the protected prefixes that are known to be safe to remove and easy to get back from the Store.</summary>
  public static readonly string[] SafeInbox={"Microsoft.Windows.Photos","Microsoft.Windows.DevHome","MicrosoftWindows.Client.WebExperience","Microsoft.Windows.Cortana","Microsoft.549981C3F5F10"};
  /// <summary>Removable, but losing them hurts: the Store itself, the package installer (winget) and the Store purchase helper.</summary>
  public static readonly string[] CautionList={"Microsoft.WindowsStore","Microsoft.DesktopAppInstaller","Microsoft.StorePurchaseApp","Microsoft.Services.Store.Engagement","Microsoft.WindowsTerminal","Microsoft.Windows.Photos","Microsoft.WindowsNotepad","Microsoft.Paint","Microsoft.ScreenSketch","Microsoft.WindowsCalculator","Microsoft.WindowsCamera","Microsoft.Windows.Cortana","Microsoft.HEIFImageExtension","Microsoft.WebpImageExtension","Microsoft.VP9VideoExtensions","Microsoft.HEVCVideoExtension","Microsoft.RawImageExtension","Microsoft.AV1VideoExtension","Microsoft.WebMediaExtensions","Microsoft.MPEG2VideoExtension"};

  public static AppxStatus Classify(WindowsApp a){
   if(a==null||String.IsNullOrWhiteSpace(a.Name))return AppxStatus.Protected;
   if(a.IsFramework||a.NonRemovable)return AppxStatus.Protected;
   if(CoreProtected.Contains(a.Name,StringComparer.OrdinalIgnoreCase))return AppxStatus.Protected;
   bool inbox=a.Name.StartsWith("Microsoft.Windows.",StringComparison.OrdinalIgnoreCase)||a.Name.StartsWith("MicrosoftWindows.",StringComparison.OrdinalIgnoreCase)||a.Name.StartsWith("Windows.",StringComparison.OrdinalIgnoreCase);
   if(inbox&&!SafeInbox.Contains(a.Name,StringComparer.OrdinalIgnoreCase))return AppxStatus.Protected;
   if(a.Name.StartsWith("Microsoft.UI.Xaml",StringComparison.OrdinalIgnoreCase)||a.Name.StartsWith("Microsoft.VCLibs",StringComparison.OrdinalIgnoreCase)||a.Name.StartsWith("Microsoft.NET.Native",StringComparison.OrdinalIgnoreCase)||a.Name.StartsWith("Microsoft.WindowsAppRuntime",StringComparison.OrdinalIgnoreCase)||a.Name.StartsWith("Microsoft.DirectX",StringComparison.OrdinalIgnoreCase))return AppxStatus.Protected;
   if(CautionList.Contains(a.Name,StringComparer.OrdinalIgnoreCase))return AppxStatus.Caution;
   return AppxStatus.Removable;
  }

  public static string Reason(WindowsApp a){
   var status=Classify(a);
   if(status==AppxStatus.Removable)return L.T("Gỡ được; cài lại từ Microsoft Store khi cần.");
   if(status==AppxStatus.Caution)return L.T("Gỡ được nhưng nên cân nhắc: thành phần hữu ích của Windows hoặc cần để cài app khác.");
   if(a.IsFramework)return L.T("Thư viện nền (framework) mà app khác phụ thuộc; Windows tự dọn khi không còn ai dùng.");
   if(a.NonRemovable)return L.T("Windows đánh dấu không thể gỡ.");
   return L.T("Thành phần cốt lõi của Windows (Start, Search, Settings, đăng nhập…); gỡ sẽ làm hỏng hệ thống.");
  }

  /// <summary>Human-friendly name for a package identity; known inbox packages get their product name, others are derived from the identity.</summary>
  public static string FriendlyName(string name){
   if(String.IsNullOrWhiteSpace(name))return "";
   string known;if(Known.TryGetValue(name,out known))return known;
   string tail=name;
   foreach(string prefix in new[]{"Microsoft.Windows.","MicrosoftWindows.Client.","MicrosoftWindows.","Microsoft.","Windows."})if(tail.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){tail=tail.Substring(prefix.Length);break;}
   var segments=tail.Split('.').Where(seg=>seg.Length>2&&Regex.IsMatch(seg,"[A-Za-z]")&&!Regex.IsMatch(seg,"^[0-9A-F]+$",RegexOptions.IgnoreCase)).ToList();
   if(segments.Count>0)tail=segments.Last();
   tail=Regex.Replace(tail,"(?<=[a-z0-9])(?=[A-Z])"," ");
   return tail.Trim();
  }

  static readonly Dictionary<string,string> Known=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   {"Microsoft.WindowsCalculator","Calculator"},{"Microsoft.WindowsCamera","Camera"},{"Microsoft.Windows.Photos","Photos"},{"Microsoft.WindowsNotepad","Notepad"},{"Microsoft.Paint","Paint"},{"Microsoft.MSPaint","Paint 3D"},
   {"Microsoft.ScreenSketch","Snipping Tool"},{"Microsoft.WindowsStore","Microsoft Store"},{"Microsoft.DesktopAppInstaller","App Installer (winget)"},{"Microsoft.StorePurchaseApp","Store Purchase App"},
   {"Microsoft.WindowsTerminal","Windows Terminal"},{"Microsoft.WindowsAlarms","Clock"},{"Microsoft.WindowsMaps","Maps"},{"Microsoft.WindowsSoundRecorder","Sound Recorder"},{"Microsoft.WindowsFeedbackHub","Feedback Hub"},
   {"Microsoft.GetHelp","Get Help"},{"Microsoft.Getstarted","Tips"},{"Microsoft.MicrosoftOfficeHub","Microsoft 365 (Office) Hub"},{"Microsoft.MicrosoftSolitaireCollection","Solitaire Collection"},{"Microsoft.MicrosoftStickyNotes","Sticky Notes"},
   {"Microsoft.People","People"},{"Microsoft.Todos","Microsoft To Do"},{"Microsoft.PowerAutomateDesktop","Power Automate"},{"Microsoft.BingNews","News"},{"Microsoft.BingWeather","Weather"},{"Microsoft.BingSearch","Bing Search"},
   {"Microsoft.ZuneMusic","Media Player"},{"Microsoft.ZuneVideo","Movies & TV"},{"Microsoft.YourPhone","Phone Link"},{"Microsoft.OutlookForWindows","Outlook (new)"},{"microsoft.windowscommunicationsapps","Mail and Calendar"},
   {"Microsoft.Xbox.TCUI","Xbox TCUI"},{"Microsoft.XboxApp","Xbox Console Companion"},{"Microsoft.XboxGameOverlay","Xbox Game Bar Plugin"},{"Microsoft.XboxGamingOverlay","Xbox Game Bar"},{"Microsoft.XboxIdentityProvider","Xbox Identity Provider"},{"Microsoft.XboxSpeechToTextOverlay","Xbox Speech To Text"},{"Microsoft.GamingApp","Xbox"},
   {"Microsoft.549981C3F5F10","Cortana"},{"Microsoft.Windows.Cortana","Cortana"},{"MicrosoftWindows.Client.WebExperience","Widgets"},{"Microsoft.Windows.DevHome","Dev Home"},{"MicrosoftTeams","Microsoft Teams (personal)"},{"MSTeams","Microsoft Teams"},{"Microsoft.MicrosoftEdge.Stable","Microsoft Edge"},
   {"Clipchamp.Clipchamp","Clipchamp"},{"Microsoft.Windows.ShellExperienceHost","Shell Experience Host"},{"Microsoft.Windows.StartMenuExperienceHost","Start Menu"},{"Microsoft.Windows.Search","Windows Search"},{"windows.immersivecontrolpanel","Settings"},{"Microsoft.SecHealthUI","Windows Security"},{"Microsoft.Windows.SecHealthUI","Windows Security"},
   {"Microsoft.QuickAssist","Quick Assist"},{"Microsoft.Copilot","Copilot"},{"Microsoft.Windows.Ai.Copilot.Provider","Copilot Provider"},{"Microsoft.Whiteboard","Whiteboard"},{"Microsoft.OneDriveSync","OneDrive Sync"},{"Microsoft.LinkedIn","LinkedIn"}
  };

  /// <summary>Extracts the organization from a certificate subject such as "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond".</summary>
  public static string PublisherLabel(string subject){
   if(String.IsNullOrWhiteSpace(subject))return "";
   var m=Regex.Match(subject,@"(?:^|,\s*)(?:O|CN)=([^,]+)");
   return m.Success?m.Groups[1].Value.Trim():subject;
  }

  /// <summary>PowerShell that lists packages as a JSON array with only the fields Tweek Pro needs. allUsers requires elevation.</summary>
  public static string ListScript(bool allUsers){
   return "$ErrorActionPreference='Stop'; $p=Get-AppxPackage"+(allUsers?" -AllUsers":"")+" | Select-Object Name,PackageFullName,PackageFamilyName,@{n='Version';e={$_.Version.ToString()}},Publisher,InstallLocation,IsFramework,NonRemovable,@{n='SignatureKind';e={[int]$_.SignatureKind}},@{n='Architecture';e={$_.Architecture.ToString()}}; ConvertTo-Json -InputObject @($p) -Compress -Depth 2";
  }

  /// <summary>Parses the JSON produced by ListScript; accepts a bare object for the single-package case.</summary>
  public static List<WindowsApp> Parse(string json){
   if(String.IsNullOrWhiteSpace(json))return new List<WindowsApp>();
   string text=json.Trim();if(text.StartsWith("{"))text="["+text+"]";
   var serializer=new DataContractJsonSerializer(typeof(List<WindowsApp>));
   using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(text))){
    var list=(List<WindowsApp>)serializer.ReadObject(stream);
    foreach(var a in list){a.Name=a.Name??"";a.FullName=a.FullName??"";a.FamilyName=a.FamilyName??"";a.Version=a.Version??"";a.Publisher=a.Publisher??"";a.InstallLocation=a.InstallLocation??"";a.Architecture=a.Architecture??"";}
    return list.Where(a=>a.FullName!="").GroupBy(a=>a.FullName,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(a=>a.Status).ThenBy(a=>a.DisplayName,StringComparer.CurrentCultureIgnoreCase).ToList();
   }
  }

  /// <summary>Lists installed packages through PowerShell; Windows only.</summary>
  public static List<WindowsApp> List(bool allUsers){
   var result=PowerShell.Run(ListScript(allUsers),TimeSpan.FromSeconds(90));
   if(result.ExitCode!=0)throw new IOException(L.T("Không đọc được danh sách ứng dụng Windows: ")+result.Error);
   return Parse(result.Output);
  }

  static string Quote(string s){return "'"+(s??"").Replace("'","''")+"'";}

  /// <summary>Removal script: refuses protected packages, removes for the current user (or all users when elevated) and optionally the provisioned copy for new accounts.</summary>
  public static string RemoveScript(WindowsApp a,bool allUsers,bool deprovision){
   if(Classify(a)==AppxStatus.Protected)throw new IOException(L.T("Thành phần được bảo vệ, không gỡ: ")+a.DisplayName+" — "+Reason(a));
   if(!Regex.IsMatch(a.FullName??"",@"^[A-Za-z0-9._\-]+$"))throw new IOException(L.T("Tên gói không hợp lệ: ")+a.FullName);
   var sb=new StringBuilder("$ErrorActionPreference='Stop'; ");
   sb.Append("Remove-AppxPackage -Package "+Quote(a.FullName)+(allUsers?" -AllUsers":"")+"; ");
   if(deprovision)sb.Append("Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq "+Quote(a.Name)+" } | ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName | Out-Null }; ");
   sb.Append("'OK'");
   return sb.ToString();
  }

  /// <summary>Re-registration script used by Restore when the package folder still exists (inbox apps stay under WindowsApps after removal).</summary>
  public static string RegisterScript(string installLocation){
   string manifest=Path.Combine(installLocation??"","AppxManifest.xml");
   return "$ErrorActionPreference='Stop'; Add-AppxPackage -DisableDevelopmentMode -Register "+Quote(manifest)+"; 'OK'";
  }

  /// <summary>Microsoft Store deep link for reinstalling a package family.</summary>
  public static string StoreLink(string familyName){return "ms-windows-store://pdp/?PFN="+Uri.EscapeDataString(familyName??"");}

  /// <summary>Removes a package and records a Store entry in the vault so the action is visible and reversible where Windows allows it.</summary>
  public static Backup Remove(WindowsApp a,bool allUsers,bool deprovision){
   string script=RemoveScript(a,allUsers,deprovision);
   if(PowerShell.Executable==null)throw new IOException(L.T("Không tìm thấy Windows PowerShell; tính năng này chỉ chạy trên Windows."));
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Original=a.InstallLocation,Kind="Store",AppName=a.DisplayName,Purpose=a.FullName,ValueName=a.FamilyName,Payload=""};
   Engine.NoLinks(Engine.Vault,false);Directory.CreateDirectory(Path.Combine(Engine.Vault,backup.Id));Engine.SaveBackup(backup);
   try{
    var result=PowerShell.Run(script,TimeSpan.FromMinutes(3));
    if(result.ExitCode!=0||!result.Output.Contains("OK"))throw new IOException(String.IsNullOrWhiteSpace(result.Error)?result.Output:result.Error);
    backup.State="BackedUp";backup.Error=L.T("Đã gỡ gói; khôi phục = đăng ký lại từ thư mục gói nếu còn, hoặc cài lại từ Microsoft Store.");Engine.SaveBackup(backup);
    Log.Info("Đã gỡ ứng dụng Windows "+a.FullName+(allUsers?" (mọi tài khoản)":"")+(deprovision?" + bỏ provisioning":"")+".");
    return backup;
   }catch(Exception e){backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;}
  }

  /// <summary>Restores a removed package: re-registers from the still-present package folder, otherwise opens its Store page and explains.</summary>
  public static void Restore(Backup b){
   if(b.Kind!="Store")throw new IOException("Không phải bản sao lưu ứng dụng Windows.");
   if(!String.IsNullOrWhiteSpace(b.Original)&&File.Exists(Path.Combine(b.Original,"AppxManifest.xml"))){
    var result=PowerShell.Run(RegisterScript(b.Original),TimeSpan.FromMinutes(3));
    if(result.ExitCode!=0||!result.Output.Contains("OK"))throw new IOException(L.T("Đăng ký lại gói thất bại: ")+(String.IsNullOrWhiteSpace(result.Error)?result.Output:result.Error));
    b.State="Restored";Engine.SaveBackup(b);Log.Info("Đã đăng ký lại ứng dụng Windows "+b.Purpose+".");return;
   }
   try{Process.Start(new ProcessStartInfo(StoreLink(b.ValueName)){UseShellExecute=true});}catch(Exception){}
   throw new IOException(L.F("Thư mục gói không còn; đã mở trang Microsoft Store của {0} để cài lại.",b.AppName));
  }

  /// <summary>Finds the best logo bitmap for a package: reads Properties/Logo from AppxManifest.xml and resolves scale/targetsize variants.</summary>
  public static string ResolveLogo(string installLocation){
   try{
    if(String.IsNullOrWhiteSpace(installLocation)||!Directory.Exists(installLocation))return null;
    string manifest=Path.Combine(installLocation,"AppxManifest.xml");if(!File.Exists(manifest))return null;
    var doc=XDocument.Load(manifest);
    var logo=doc.Descendants().FirstOrDefault(e=>e.Name.LocalName=="Logo"&&e.Parent!=null&&e.Parent.Name.LocalName=="Properties");
    string rel=logo==null?null:logo.Value.Trim();
    if(String.IsNullOrEmpty(rel))return null;
    return ResolveAsset(installLocation,rel);
   }catch(Exception){return null;}
  }

  /// <summary>Resolves "Assets\StoreLogo.png" to an existing file, preferring the exact name, then the smallest scale at or above 100, then any variant.</summary>
  public static string ResolveAsset(string root,string relative){
   string full=Path.Combine(root,relative.Replace('\\',Path.DirectorySeparatorChar).Replace('/',Path.DirectorySeparatorChar));
   if(File.Exists(full))return full;
   string dir=Path.GetDirectoryName(full),stem=Path.GetFileNameWithoutExtension(full),ext=Path.GetExtension(full);
   if(String.IsNullOrEmpty(dir)||!Directory.Exists(dir))return null;
   var candidates=Directory.GetFiles(dir,stem+".*"+ext).Where(f=>Path.GetFileName(f).StartsWith(stem+".",StringComparison.OrdinalIgnoreCase)).ToList();
   if(candidates.Count==0)return null;
   Func<string,int> rank=f=>{
    var m=Regex.Match(Path.GetFileName(f),@"\.(scale|targetsize)-(\d+)",RegexOptions.IgnoreCase);
    if(!m.Success)return 5000;
    int n=Int32.Parse(m.Groups[2].Value);
    bool scale=m.Groups[1].Value.Equals("scale",StringComparison.OrdinalIgnoreCase);
    if(scale)return n>=100?n-100:1000+(100-n);
    return n>=48?2000+n:3000+(48-n);
   };
   return candidates.OrderBy(rank).ThenBy(f=>f.Length).First();
  }
 }
}
