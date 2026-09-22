using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace TweekPro.RegClean {
 /// <summary>A registry location the cleaner may inspect: hive + view + key; findings are direct children (keys or values) of it.</summary>
 public class RegParent { public string Hive, View, Key; public override string ToString(){return Hive+" ["+View+"]\\"+Key;} }

 /// <summary>One orphaned entry: a subkey or a value under an allowed parent whose file target no longer exists.</summary>
 public class RegFinding { public string Category, Hive, View, Key, ValueName, Target, Detail; public bool IsValue { get { return ValueName!=null; } } public string Path { get { return Hive+" ["+View+"]\\"+Key+(ValueName==null?"":" :: "+ValueName); } } }

 /// <summary>Saved copy of one removed entry so the whole category run can be written back.</summary>
 public class RegCleanEntry { public string Hive, View, Key, ValueName; public RegNode Node; public RegValue Value; }

 /// <summary>Preview per category.</summary>
 public class RegCategoryResult { public RegCategory Category; public List<RegFinding> Findings=new List<RegFinding>(); public string Note=""; public int Count { get { return Findings.Count; } } }

 /// <summary>Clean outcome.</summary>
 public class RegCleanReport { public int Cleaned, Failed; public List<Backup> Backups=new List<Backup>(); public List<string> Errors=new List<string>(); }

 /// <summary>A cleaner category: where to look and how to decide an entry is orphaned.</summary>
 public class RegCategory {
  public string Id, Name, Description; public bool DefaultChecked=true, Values; public List<RegParent> Parents=new List<RegParent>();
  /// <summary>For key categories: returns the file the subkey depends on ("" when it cannot be judged, so the key is kept).</summary>
  public Func<RegistryKey,string,string> KeyTarget;
  /// <summary>For value categories: returns the file a value depends on ("" when not judgeable).</summary>
  public Func<RegistryKey,string,string> ValueTarget;
 }

 /// <summary>
 /// Code-level boundary: only direct children (or values) of the fixed parent keys may be removed, never the parents, never anything
 /// under HKLM\SYSTEM, Classes\CLSID, Policies, or with an empty/short name. Restore refuses the same set.
 /// </summary>
 public static class RegCleanSafety {
  public static readonly string[] NeverParents={"SYSTEM","SOFTWARE\\Policies","SOFTWARE\\Microsoft\\Windows NT","SOFTWARE\\Classes\\CLSID","SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run","SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce"};
  /// <summary>Uninstall leaves Windows itself writes; never proposed even if their command looks broken.</summary>
  public static readonly string[] KeepUninstallLeaves={"AddressBook","Connection Manager","DirectDrawEx","Fontcore","IE40","IE4Data","IE5BAKEX","IEData","MobileOptionPack","MPlayer2","SchedulingAgent","WIC"};

  public static void Validate(RegFinding f,IEnumerable<RegParent> allowed){
   if(f==null||String.IsNullOrWhiteSpace(f.Key)||f.Key.Contains(".."))throw new IOException("Đường dẫn khóa không hợp lệ.");
   if(f.Hive!="HKLM"&&f.Hive!="HKCU")throw new IOException("Chỉ dọn trong HKLM/HKCU.");
   var parent=allowed.FirstOrDefault(p=>p.Hive==f.Hive&&p.View==f.View&&(f.IsValue?String.Equals(p.Key,f.Key,StringComparison.OrdinalIgnoreCase):f.Key.StartsWith(p.Key+"\\",StringComparison.OrdinalIgnoreCase)&&f.Key.Substring(p.Key.Length+1).IndexOf('\\')<0));
   if(parent==null)throw new IOException("Mục nằm ngoài danh sách vị trí cho phép: "+f.Path);
   foreach(string never in NeverParents)if(f.Key.StartsWith(never+"\\",StringComparison.OrdinalIgnoreCase)||String.Equals(f.Key,never,StringComparison.OrdinalIgnoreCase))throw new IOException("Vùng Registry bị chặn cứng: "+f.Key);
   if(f.IsValue){if(f.ValueName.Trim()=="")throw new IOException("Không xóa giá trị mặc định của khóa.");}
   else{string leaf=f.Key.Substring(f.Key.LastIndexOf('\\')+1);if(leaf.Length<2)throw new IOException("Tên khóa quá ngắn.");if(f.Key.IndexOf("Uninstall\\",StringComparison.OrdinalIgnoreCase)>=0&&KeepUninstallLeaves.Contains(leaf,StringComparer.OrdinalIgnoreCase))throw new IOException("Mục Uninstall của Windows; giữ nguyên.");}
  }

  /// <summary>A path can be judged only when it is an absolute local path on a ready fixed drive; anything else is treated as "unknown" and kept.</summary>
  public static bool Verifiable(string path){
   if(String.IsNullOrWhiteSpace(path)||path.Length<4||path[1]!=':'||path[2]!='\\'||path.StartsWith("\\\\"))return false;
   if(path.IndexOfAny(Path.GetInvalidPathChars())>=0||path.IndexOf('*')>=0||path.IndexOf('?')>=0)return false;
   try{var drive=new DriveInfo(path.Substring(0,3));return drive.IsReady&&(drive.DriveType==DriveType.Fixed||drive.DriveType==DriveType.Unknown);}catch(Exception){return false;}
  }
 }

 /// <summary>Conservative registry cleaner: only entries whose executable/DLL is verifiably gone, always through one vault backup per category.</summary>
 public static class RegistryCleaner {
  public const string BackupKind="RegClean";
  public const int MaxPerCategory=3000;
  const string Uninstall=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
  const string AppPaths=@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
  const string MuiCache=@"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
  const string SharedDlls=@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs";
  const string Approved=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved";
  const string Applications=@"SOFTWARE\Classes\Applications";
  public const string FixtureRoot=@"SOFTWARE\TweekProTest\RegClean";

  static IEnumerable<string> Views { get { return Environment.Is64BitOperatingSystem?new[]{"64","32"}:new[]{"32"}; } }
  static List<RegParent> Both(string key){var l=new List<RegParent>();foreach(string h in new[]{"HKLM","HKCU"})foreach(string v in Views)l.Add(new RegParent{Hive=h,View=v,Key=key});return l;}
  static List<RegParent> User(string key){return Views.Select(v=>new RegParent{Hive="HKCU",View=v,Key=key}).ToList();}

  /// <summary>Path a command string depends on, expanded; "" when it cannot be judged (msiexec, rundll32, interpreters, relative names).</summary>
  public static string CommandTarget(string command){
   string exe=Advanced.CommandExe(command);if(exe=="")return "";
   exe=Environment.ExpandEnvironmentVariables(exe).Trim();
   string leaf=Path.GetFileName(exe).ToLowerInvariant();
   if(new[]{"msiexec.exe","rundll32.exe","cmd.exe","powershell.exe","pwsh.exe","wscript.exe","cscript.exe","mshta.exe","explorer.exe","control.exe"}.Contains(leaf))return "";
   return RegCleanSafety.Verifiable(exe)?exe:"";
  }

  /// <summary>MUICache value names are "&lt;path&gt;.FriendlyAppName" / ".ApplicationCompany"; returns the path part or "".</summary>
  public static string MuiCacheTarget(string valueName){
   if(String.IsNullOrEmpty(valueName))return "";
   int dot=valueName.LastIndexOf('.');if(dot<=0)return "";
   string suffix=valueName.Substring(dot+1);if(suffix!="FriendlyAppName"&&suffix!="ApplicationCompany")return "";
   string path=valueName.Substring(0,dot);if(path.StartsWith("@"))return "";
   return RegCleanSafety.Verifiable(path)?Environment.ExpandEnvironmentVariables(path):"";
  }

  /// <summary>Where a CLSID's in-process server lives, resolved from HKCR (HKCU then HKLM); "" when the CLSID exists but is not file-based.</summary>
  public static string ClsidServer(string clsid,out bool clsidExists){
   clsidExists=false;if(String.IsNullOrWhiteSpace(clsid)||!clsid.StartsWith("{"))return "";
   foreach(string hive in new[]{"HKCU","HKLM"})foreach(string view in Views){
    try{using(var root=Engine.Base(hive,view))using(var key=root.OpenSubKey(@"SOFTWARE\Classes\CLSID\"+clsid)){
     if(key==null)continue;clsidExists=true;
     using(var inproc=key.OpenSubKey("InprocServer32")){if(inproc==null)return "";string dll=Engine.ExpandTrim(Engine.Read(inproc,""));return RegCleanSafety.Verifiable(dll)?dll:"";}
    }}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}
   }
   return "";
  }

  /// <summary>The category catalog; fixtureRoot (tests) adds an HKCU parent per category under SOFTWARE\TweekProTest.</summary>
  public static List<RegCategory> Categories(string fixtureRoot=null){
   Func<RegistryKey,string,string> uninstall=(key,leaf)=>{
    if(Engine.Read(key,"SystemComponent")=="1"||Engine.Read(key,"WindowsInstaller")=="1"||Engine.Read(key,"ReleaseType")!=""||Engine.Read(key,"ParentKeyName")!="")return "";
    string cmd=Engine.Read(key,"UninstallString");if(cmd=="")return "";
    string target=CommandTarget(cmd);if(target==""||File.Exists(target))return "";
    string loc=Engine.ExpandTrim(Engine.Read(key,"InstallLocation"));if(loc!=""&&RegCleanSafety.Verifiable(loc)&&Directory.Exists(loc))return "";
    string icon;int i;if(Presentation.ParseIcon(Engine.Read(key,"DisplayIcon"),out icon,out i)&&RegCleanSafety.Verifiable(icon)&&File.Exists(icon))return "";
    return target;
   };
   Func<RegistryKey,string,string> appPath=(key,leaf)=>{string exe=Engine.ExpandTrim(Engine.Read(key,"").Trim('"'));return RegCleanSafety.Verifiable(exe)&&!File.Exists(exe)?exe:"";};
   Func<RegistryKey,string,string> application=(key,leaf)=>{using(var cmd=key.OpenSubKey(@"shell\open\command")){if(cmd==null)return "";string exe=CommandTarget(Engine.Read(cmd,""));return exe!=""&&!File.Exists(exe)?exe:"";}};
   Func<RegistryKey,string,string> mui=(key,name)=>{string p=MuiCacheTarget(name);return p!=""&&!File.Exists(p)?p:"";};
   Func<RegistryKey,string,string> shared=(key,name)=>{string p=Engine.ExpandTrim(name);return RegCleanSafety.Verifiable(p)&&!File.Exists(p)&&!Directory.Exists(p)?p:"";};
   Func<RegistryKey,string,string> approved=(key,name)=>{bool exists;string dll=ClsidServer(name,out exists);if(!exists)return "(CLSID không còn đăng ký) "+name;return dll!=""&&!File.Exists(dll)?dll:"";};
   var list=new List<RegCategory>{
    new RegCategory{Id="uninstall",Name="Mục Uninstall mồ côi",Description="Khóa trong danh sách Gỡ cài đặt mà trình gỡ, thư mục cài và biểu tượng đều không còn trên đĩa (bỏ qua MSI, thành phần hệ thống, bản cập nhật).",Parents=Both(Uninstall),KeyTarget=uninstall},
    new RegCategory{Id="app-paths",Name="App Paths trỏ tệp không tồn tại",Description="Đăng ký tên lệnh ngắn (Win+R) trỏ tới .exe đã bị xóa.",Parents=Both(AppPaths),KeyTarget=appPath},
    new RegCategory{Id="applications",Name="Đăng ký «Mở bằng» của ứng dụng đã mất",Description="Classes\\Applications\\<exe> có lệnh shell\\open trỏ tới tệp không còn; menu «Mở bằng» hết hiện mục hỏng.",Parents=Both(Applications),KeyTarget=application},
    new RegCategory{Id="mui-cache",Name="MUICache của chương trình đã xóa",Description="Tên hiển thị/nhà phát hành lưu đệm cho .exe không còn tồn tại.",Parents=User(MuiCache),Values=true,ValueTarget=mui},
    new RegCategory{Id="shared-dlls",Name="SharedDLLs trỏ tệp không tồn tại",Description="Bộ đếm tham chiếu DLL dùng chung cho tệp đã bị xóa.",Parents=Both(SharedDlls),Values=true,ValueTarget=shared},
    new RegCategory{Id="shell-approved",Name="Shell extension đã duyệt nhưng DLL mất",Description="CLSID trong Shell Extensions\\Approved không còn đăng ký hoặc InprocServer32 trỏ DLL không tồn tại.",Parents=Both(Approved),Values=true,ValueTarget=approved,DefaultChecked=false}
   };
   if(!String.IsNullOrEmpty(fixtureRoot))foreach(var c in list)c.Parents.Add(new RegParent{Hive="HKCU",View="64",Key=fixtureRoot+"\\"+c.Id});
   return list;
  }

  /// <summary>Read-only scan of every category.</summary>
  public static List<RegCategoryResult> Scan(IEnumerable<RegCategory> categories,CancellationToken cancel){
   var results=new List<RegCategoryResult>();
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return categories.Select(c=>new RegCategoryResult{Category=c,Note="Chỉ đọc được trên Windows."}).ToList();
   foreach(var c in categories){
    var result=new RegCategoryResult{Category=c};var watch=Stopwatch.StartNew();
    foreach(var parent in c.Parents){
     cancel.ThrowIfCancellationRequested();
     try{using(var root=Engine.Base(parent.Hive,parent.View))using(var key=root.OpenSubKey(parent.Key)){
      if(key==null)continue;
      if(c.Values){
       foreach(string name in key.GetValueNames()){
        if(result.Count>=MaxPerCategory||watch.Elapsed.TotalSeconds>20){result.Note="Đã chạm giới hạn; kết quả chưa đủ.";break;}
        if(name.Trim()=="")continue;string target;try{target=c.ValueTarget(key,name);}catch(Exception){continue;}
        if(target=="")continue;
        var f=new RegFinding{Category=c.Id,Hive=parent.Hive,View=parent.View,Key=parent.Key,ValueName=name,Target=target,Detail=Detail(key,name)};
        try{RegCleanSafety.Validate(f,c.Parents);result.Findings.Add(f);}catch(IOException){}
       }
      }else{
       foreach(string sub in key.GetSubKeyNames()){
        if(result.Count>=MaxPerCategory||watch.Elapsed.TotalSeconds>20){result.Note="Đã chạm giới hạn; kết quả chưa đủ.";break;}
        string target;string detail="";try{using(var child=key.OpenSubKey(sub)){if(child==null)continue;target=c.KeyTarget(child,sub);detail=Engine.Read(child,"DisplayName");if(detail=="")detail=Engine.Read(child,"");}}catch(Exception){continue;}
        if(target=="")continue;
        var f=new RegFinding{Category=c.Id,Hive=parent.Hive,View=parent.View,Key=parent.Key+"\\"+sub,Target=target,Detail=detail};
        try{RegCleanSafety.Validate(f,c.Parents);result.Findings.Add(f);}catch(IOException){}
       }
      }
     }}catch(System.Security.SecurityException){result.Note="Không đủ quyền đọc "+parent;}catch(UnauthorizedAccessException){result.Note="Không đủ quyền đọc "+parent;}catch(IOException e){result.Note=e.Message;}
    }
    // HKCU is shared between views on most systems; drop exact duplicates.
    result.Findings=result.Findings.GroupBy(f=>f.Hive+"|"+f.Key+"|"+f.ValueName,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
    results.Add(result);
   }
   return results;
  }

  static string Detail(RegistryKey key,string name){try{var kind=key.GetValueKind(name);object v=key.GetValue(name);return kind==RegistryValueKind.String||kind==RegistryValueKind.ExpandString?Convert.ToString(v):kind.ToString();}catch(Exception){return "";}}

  /// <summary>Removes the findings of each category after re-checking the target is still gone; one vault backup per category holds every removed key/value.</summary>
  public static RegCleanReport Clean(IEnumerable<RegCategoryResult> selected,CancellationToken cancel){
   var report=new RegCleanReport();
   foreach(var result in selected){
    cancel.ThrowIfCancellationRequested();if(result.Count==0)continue;
    Engine.NoLinks(Engine.Vault,false);
    var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Kind=BackupKind,Purpose=result.Category.Id,AppName=result.Category.Name,Original=String.Join(" | ",result.Category.Parents.Select(p=>p.Key).Distinct().Take(2)),Payload="entries.xml"};
    string folder=Path.Combine(Engine.Vault,backup.Id);Directory.CreateDirectory(folder);Engine.SaveBackup(backup);
    var entries=new List<RegCleanEntry>();string payload=Path.Combine(folder,"entries.xml");
    try{
     foreach(var f in result.Findings){
      cancel.ThrowIfCancellationRequested();
      try{
       RegCleanSafety.Validate(f,result.Category.Parents);
       if(f.Target!=""&&!f.Target.StartsWith("(")&&(File.Exists(f.Target)||Directory.Exists(f.Target)))throw new IOException("Tệp đích đã xuất hiện lại; giữ nguyên.");
       using(var root=Engine.Base(f.Hive,f.View)){
        if(f.IsValue){
         using(var key=root.OpenSubKey(f.Key,true)){if(key==null)continue;if(!key.GetValueNames().Contains(f.ValueName))continue;var value=Advanced.ReadValue(key,f.ValueName);entries.Add(new RegCleanEntry{Hive=f.Hive,View=f.View,Key=f.Key,ValueName=f.ValueName,Value=value});Engine.Save(payload,entries);key.DeleteValue(f.ValueName,true);}
        }else{
         RegNode node;using(var key=root.OpenSubKey(f.Key)){if(key==null)continue;node=Engine.ReadTree(key);}
         entries.Add(new RegCleanEntry{Hive=f.Hive,View=f.View,Key=f.Key,Node=node});Engine.Save(payload,entries);
         root.DeleteSubKeyTree(f.Key,true);
        }
       }
       report.Cleaned++;
      }catch(OperationCanceledException){throw;}
      catch(Exception e){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(f.Path+": "+e.Message);}
     }
     Engine.Save(payload,entries);Engine.Load<List<RegCleanEntry>>(payload);
     backup.State="BackedUp";Engine.SaveBackup(backup);report.Backups.Add(backup);
    }catch(Exception e){try{Engine.Save(payload,entries);}catch(Exception){}backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;}
    if(entries.Count==0){try{Directory.Delete(folder,true);report.Backups.Remove(backup);}catch(Exception){}}
   }
   return report;
  }

  /// <summary>Writes back every saved key/value that is still absent; existing ones are left alone and counted as skipped.</summary>
  public static void Restore(Backup b){
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   if(b.Kind!=BackupKind)throw new IOException("Không phải bản sao lưu Registry cleaner.");
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new IOException("Chỉ khôi phục Registry trên Windows.");
   string payload=Path.Combine(Engine.VaultOf(b),b.Id,"entries.xml");Engine.NoLinks(payload,false);
   var entries=Engine.Load<List<RegCleanEntry>>(payload);var allowed=Categories(FixtureRoot).SelectMany(c=>c.Parents).ToList();
   int restored=0,skipped=0,failed=0;
   foreach(var e in entries){
    try{
     var f=new RegFinding{Hive=e.Hive,View=e.View,Key=e.Key,ValueName=e.ValueName};RegCleanSafety.Validate(f,allowed);
     using(var root=Engine.Base(e.Hive,e.View)){
      if(e.ValueName!=null){using(var key=root.CreateSubKey(e.Key)){if(key.GetValueNames().Contains(e.ValueName)){skipped++;continue;}Engine.WriteTree(key,new RegNode{Values={e.Value}});restored++;}}
      else{using(var existing=root.OpenSubKey(e.Key))if(existing!=null){skipped++;continue;}using(var key=root.CreateSubKey(e.Key))Engine.WriteTree(key,e.Node);restored++;}
     }
    }catch(Exception){failed++;}
   }
   if(failed>0){b.State="NeedsReview";b.Error="Khôi phục "+restored+" mục; "+failed+" mục lỗi; "+skipped+" mục bỏ qua vì đã tồn tại.";Engine.SaveBackup(b);throw new IOException(b.Error);}
   b.State="Restored";b.Error=skipped>0?"Bỏ qua "+skipped+" mục vì đã tồn tại.":"";Engine.SaveBackup(b);
  }

  public static string BackupKindLabel(){return Core.L.T("Registry cleaner");}
 }
}
