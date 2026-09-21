using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;
using TweekPro.Network;

namespace TweekPro.Remnants {
 /// <summary>Second pass of the leftover deep scan: name variants plus the registry caches, firewall rules, shell registrations, crash reports and user folders that uninstallers routinely leave behind.</summary>
 public static class TraceHunter {
  public const string MuiCache=@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
  public const string FirewallRules=FirewallBlock.RulesKey;
  public const string SharedDlls=@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs";
  public const string Persisted=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Persisted";
  public const string StartupApprovedRun=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
  public const string StartupApprovedRun32=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
  public const string StartupApprovedFolder=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
  public const string FeatureUsage=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage";
  /// <summary>Value locations (besides Run/RunOnce/AppCompat) whose single values may be removed through the vault: caches, counters and per-program rules, never configuration shared with other software.</summary>
  public static readonly string[] ValueKeys={MuiCache,FirewallRules,SharedDlls,Persisted,StartupApprovedRun,StartupApprovedRun32,StartupApprovedFolder,FeatureUsage+@"\AppSwitched",FeatureUsage+@"\AppLaunch",FeatureUsage+@"\AppBadgeUpdated",FeatureUsage+@"\ShowJumpView"};
  /// <summary>Words too generic to identify an application on their own (folder or key names).</summary>
  public static readonly HashSet<string> GenericNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"app","apps","application","applications","bin","cache","client","common","config","data","default","desktop","driver","drivers","edition","free","game","games","helper","home","install","installer","launcher","lib","library","local","manager","microsoft","windows","google","program","programs","pro","plus","professional","runtime","service","services","setup","shared","software","studio","suite","system","temp","tmp","tool","tools","update","updater","user","users","utility","utilities","version","viewer","x64","x86","win64","win32","beta","preview","portable","package","packages","platform","player","server","engine","core","framework","sdk","net"};
  static readonly HashSet<string> GenericExe=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"setup","install","installer","uninstall","uninstaller","unins000","unins001","update","updater","launcher","helper","crashhandler","crashreporter","crashpad_handler","service","host","broker","runtime","node","python","java","javaw","electron","chrome","msedge","wscript","cscript","cmd","powershell","rundll32","regsvr32","msiexec"};
  static readonly Regex VersionToken=new Regex(@"(?:^|\s)(?:v(?:er(?:sion)?)?\.?\s*)?\d+(?:[.\-_]\d+)+(?=\s|$)|\s+(?:v\.?\s*)?\d+(?=\s|$)|\s+(?:19|20)\d{2}(?=\s|$)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
  static readonly Regex Parenthetical=new Regex(@"\s*[\(\[][^\)\]]*[\)\]]",RegexOptions.Compiled);
  static readonly Regex ArchToken=new Regex(@"\s+(?:x64|x86|amd64|arm64|64[- ]?bit|32[- ]?bit|win64|win32)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
  static readonly Regex Spaces=new Regex(@"\s+",RegexOptions.Compiled);

  /// <summary>True when a name is specific enough to delete a folder on its own; short single words only become review items.</summary>
  public static bool Strong(string name){return !String.IsNullOrEmpty(name)&&(name.Length>=8||name.Trim().Contains(' '))&&!GenericNames.Contains(name)&&!name.All(Char.IsDigit);}

  /// <summary>Alternative names the application may have used for folders and keys: without version, architecture, parenthesis, publisher prefix, spaces, plus executable base names.</summary>
  public static HashSet<string> NameVariants(AppEntry app){
   var variants=new HashSet<string>(StringComparer.OrdinalIgnoreCase);if(app==null)return variants;
   string publisher=(app.Publisher??"").Trim();string publisherWord=publisher.Split(new[]{' ',',','.'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
   Action<string> add=n=>{if(n==null)return;n=Spaces.Replace(n.Trim()," ").Trim(' ','-','_','.');if(Engine.SafeName(n)&&n.Length>=4&&!GenericNames.Contains(n)&&!n.All(c=>Char.IsDigit(c)||c=='.')&&!String.Equals(n,app.Name,StringComparison.OrdinalIgnoreCase)&&!String.Equals(n,publisher,StringComparison.OrdinalIgnoreCase)&&!String.Equals(n,publisherWord,StringComparison.OrdinalIgnoreCase))variants.Add(n);};
   string name=(app.Name??"").Trim();
   if(name!=""){
    string stripped=ArchToken.Replace(Parenthetical.Replace(name,""),"");stripped=VersionToken.Replace(stripped,"").Trim();
    add(stripped);
    foreach(string candidate in new[]{name,stripped}){
     if(publisherWord.Length>=3&&candidate.StartsWith(publisherWord+" ",StringComparison.OrdinalIgnoreCase))add(candidate.Substring(publisherWord.Length+1));
     if(publisher.Length>=3&&candidate.StartsWith(publisher+" ",StringComparison.OrdinalIgnoreCase))add(candidate.Substring(publisher.Length+1));
    }
    foreach(string candidate in variants.ToList().Concat(new[]{stripped}))if(candidate.Contains(' ')&&candidate.Replace(" ","").Length>=6)add(candidate.Replace(" ",""));
   }
   foreach(string exe in ExeBaseNames(app))add(exe);
   return variants;
  }

  /// <summary>Executable base names (without .exe) recorded before uninstall, excluding generic installer/helper names.</summary>
  public static List<string> ExeBaseNames(AppEntry app){
   var list=new List<string>();if(app==null||app.KnownExecutables==null)return list;
   foreach(string exe in app.KnownExecutables){
    string leaf;try{leaf=Path.GetFileNameWithoutExtension(exe);}catch(ArgumentException){continue;}
    if(String.IsNullOrWhiteSpace(leaf)||leaf.Length<3||GenericExe.Contains(leaf)||GenericNames.Contains(leaf))continue;
    if(!list.Contains(leaf,StringComparer.OrdinalIgnoreCase))list.Add(leaf);
   }
   return list;
  }

  /// <summary>Program path encoded in a MuiCache value name ("C:\x\app.exe.FriendlyAppName" → "C:\x\app.exe").</summary>
  public static string MuiCacheProgram(string valueName){
   if(String.IsNullOrEmpty(valueName))return "";
   foreach(string suffix in new[]{".FriendlyAppName",".ApplicationCompany"})if(valueName.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))return valueName.Substring(0,valueName.Length-suffix.Length);
   return valueName;
  }

  /// <summary>Program path in a FirewallRules value ("...|App=C:\x\app.exe|...").</summary>
  public static string FirewallProgram(string value){return FirewallBlock.Parse("",value).Program;}

  /// <summary>Splits a PATH value into its non-empty entries with environment variables expanded.</summary>
  public static List<string> PathEntries(string value){
   var list=new List<string>();if(String.IsNullOrWhiteSpace(value))return list;
   foreach(string raw in value.Split(';')){string e=raw.Trim().Trim('"');if(e=="")continue;try{list.Add(Engine.Canon(e));}catch(ArgumentException){}catch(NotSupportedException){}catch(PathTooLongException){}}
   return list;
  }

  /// <summary>True when a WER report folder ("AppCrash_photoshop.exe_1a2b…", "AppHang_app_…") belongs to one of the executables.</summary>
  public static bool WerMatches(string folderName,IEnumerable<string> exeBaseNames){
   if(String.IsNullOrEmpty(folderName))return false;int first=folderName.IndexOf('_');if(first<0||first+1>=folderName.Length)return false;
   string rest=folderName.Substring(first+1);
   foreach(string exe in exeBaseNames){if(rest.StartsWith(exe+".exe_",StringComparison.OrdinalIgnoreCase)||rest.StartsWith(exe+"_",StringComparison.OrdinalIgnoreCase))return true;}
   return false;
  }

  /// <summary>True when a Prefetch file ("PHOTOSHOP.EXE-1A2B3C4D.pf") or CrashDumps file ("photoshop.exe.1234.dmp") belongs to one of the executables.</summary>
  public static bool TraceFileMatches(string fileName,IEnumerable<string> exeBaseNames){
   if(String.IsNullOrEmpty(fileName))return false;
   foreach(string exe in exeBaseNames){if(fileName.StartsWith(exe+".exe-",StringComparison.OrdinalIgnoreCase)||fileName.StartsWith(exe+".exe.",StringComparison.OrdinalIgnoreCase))return true;}
   return false;
  }

  /// <summary>True when a path lies inside the recorded install folder or a folder of a recorded executable.</summary>
  public static bool UnderApp(AppEntry identity,string path){
   if(String.IsNullOrWhiteSpace(path)||!Advanced.LocalPath(path))return false;string p;try{p=Engine.Canon(path);}catch(Exception){return false;}
   try{if(!String.IsNullOrWhiteSpace(identity.Location)){string root=Engine.Canon(identity.Location);if(String.Equals(p,root,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,root))return true;}}catch(Exception){}
   if(identity.KnownExecutables!=null)foreach(string exe in identity.KnownExecutables)try{string dir=Path.GetDirectoryName(exe);if(!String.IsNullOrEmpty(dir)&&(String.Equals(p,dir,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,dir)))return true;}catch(ArgumentException){}
   return false;
  }

  static void Review(ScanResult result,string path,string reason){result.Items.Add(new Candidate{Kind="Review",Path=path,ReviewOnly=true,Reason=reason});}
  static void Value(ScanResult result,string hive,string view,string keyPath,RegistryKey key,string name,string reason){
   try{var value=Advanced.ReadValue(key,name);result.Items.Add(new Candidate{Kind="RegistryValue",Path=keyPath,Hive=hive,View=view,ValueName=name,ExpectedHash=Advanced.Fingerprint(value),Reason=reason});}catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
  }

  /// <summary>Runs every extra stage with a shared 25-second budget; each stage is bounded and never throws past its own try block except for cancellation.</summary>
  public static void Extend(AppEntry app,AppEntry identity,HashSet<string> names,ScanResult result,CancellationToken cancel,Action<string,string> report){
   if(!Core.StubbornFiles.IsWindows)return;
   var watch=Stopwatch.StartNew();Func<bool> over=()=>watch.Elapsed.TotalSeconds>25;
   var exeNames=ExeBaseNames(app);var variants=NameVariants(app);
   var stages=new List<Tuple<string,Action>>{
    Tuple.Create("Đang kiểm tra MUICache, AppCompat và bộ đếm Explorer…",(Action)(()=>ScanUserCaches(identity,result,cancel))),
    Tuple.Create("Đang kiểm tra quy tắc Windows Firewall và SharedDLLs…",(Action)(()=>ScanMachineValues(identity,result,cancel))),
    Tuple.Create("Đang kiểm tra StartupApproved…",(Action)(()=>ScanStartupApproved(result,cancel))),
    Tuple.Create("Đang kiểm tra đăng ký kiểu tệp, COM và ứng dụng mặc định…",(Action)(()=>ScanClasses(identity,exeNames,names,result,cancel,watch))),
    Tuple.Create("Đang kiểm tra biến PATH và nguồn Event Log…",(Action)(()=>{ScanPath(identity,result);ScanEventLog(identity,result,cancel);})),
    Tuple.Create("Đang kiểm tra Windows Installer và Tracing…",(Action)(()=>ScanInstallerAndTracing(app,names,exeNames,result,cancel))),
    Tuple.Create("Đang kiểm tra báo cáo lỗi, Prefetch, Recent và thư mục người dùng…",(Action)(()=>ScanUserFiles(identity,names,variants,exeNames,result,cancel))),
    Tuple.Create("Đang kiểm tra tên gần giống trong Registry…",(Action)(()=>ScanVariantKeys(app,variants,result,cancel)))
   };
   foreach(var stage in stages){
    cancel.ThrowIfCancellationRequested();if(over()){result.Notes.Add("Đã chạm giới hạn 25 giây của quét dấu vết mở rộng; một số vùng chưa kiểm tra.");break;}
    if(report!=null)report(stage.Item1,null);
    try{stage.Item2();}catch(OperationCanceledException){throw;}catch(Exception e){result.Notes.Add("Bỏ qua một vùng quét dấu vết: "+e.Message);}
   }
  }

  static void ScanUserCaches(AppEntry identity,ScanResult result,CancellationToken cancel){
   string view=Advanced.Views[0];
   try{using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(MuiCache))if(key!=null)foreach(string n in key.GetValueNames().Take(20000)){
    cancel.ThrowIfCancellationRequested();if(Advanced.MatchesPath(identity,MuiCacheProgram(n)))Value(result,"HKCU",view,MuiCache,key,n,"Bộ đệm tên hiển thị (MUICache) của executable đã gỡ; xóa an toàn, Windows tự tạo lại khi cần.");
   }}catch(UnauthorizedAccessException){result.Notes.Add("Không đọc được MUICache.");}catch(System.Security.SecurityException){}
   foreach(string v in Advanced.Views)try{using(var root=Engine.Base("HKCU",v))using(var key=root.OpenSubKey(Persisted))if(key!=null)foreach(string n in key.GetValueNames()){
    cancel.ThrowIfCancellationRequested();if(Advanced.MatchesPath(identity,n))Value(result,"HKCU",v,Persisted,key,n,"Trợ lý tương thích đã ghi nhớ executable này; giá trị chỉ là lịch sử.");
   }}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
   foreach(string sub in new[]{"AppSwitched","AppLaunch","AppBadgeUpdated","ShowJumpView"}){
    string path=FeatureUsage+"\\"+sub;
    try{using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(path))if(key!=null)foreach(string n in key.GetValueNames().Take(5000)){
     cancel.ThrowIfCancellationRequested();if(Advanced.MatchesPath(identity,n))Value(result,"HKCU",view,path,key,n,"Bộ đếm thanh tác vụ (FeatureUsage) cho executable đã gỡ; chỉ là thống kê.");
    }}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
   }
  }

  static void ScanMachineValues(AppEntry identity,ScanResult result,CancellationToken cancel){
   string view=Advanced.Views[0];
   try{using(var root=Engine.Base("HKLM",view))using(var key=root.OpenSubKey(FirewallRules))if(key!=null)foreach(string n in key.GetValueNames().Take(30000)){
    cancel.ThrowIfCancellationRequested();string program=FirewallProgram(Engine.Read(key,n));
    if(program!=""&&(Advanced.MatchesPath(identity,program)||UnderApp(identity,program)))Value(result,"HKLM",view,FirewallRules,key,n,"Quy tắc Windows Firewall trỏ tới executable đã gỡ; xóa để dọn danh sách quy tắc (không ảnh hưởng phần mềm khác).");
   }}catch(UnauthorizedAccessException){result.Notes.Add("Không đọc được quy tắc Firewall.");}catch(System.Security.SecurityException){}
   foreach(string v in Advanced.Views)try{using(var root=Engine.Base("HKLM",v))using(var key=root.OpenSubKey(SharedDlls))if(key!=null)foreach(string n in key.GetValueNames().Take(30000)){
    cancel.ThrowIfCancellationRequested();if(UnderApp(identity,n))Value(result,"HKLM",v,SharedDlls,key,n,"Bộ đếm SharedDLLs cho tệp trong thư mục ứng dụng đã gỡ; tệp không còn nên bộ đếm vô nghĩa.");
   }}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
  }

  /// <summary>StartupApproved entries whose names mirror Run values or Startup shortcuts already found for this app.</summary>
  static void ScanStartupApproved(ScanResult result,CancellationToken cancel){
   var runNames=new HashSet<string>(result.Items.Where(c=>c.Kind=="RegistryValue"&&(String.Equals(c.Path,Advanced.Run,StringComparison.OrdinalIgnoreCase)||String.Equals(c.Path,Advanced.RunOnce,StringComparison.OrdinalIgnoreCase))).Select(c=>c.ValueName),StringComparer.OrdinalIgnoreCase);
   var linkNames=new HashSet<string>(result.Items.Where(c=>c.Kind=="File"&&Advanced.StartupRoots().Any(r=>Engine.Under(c.Path,r))).Select(c=>Path.GetFileName(c.Path)),StringComparer.OrdinalIgnoreCase);
   if(runNames.Count==0&&linkNames.Count==0)return;
   foreach(string h in new[]{"HKCU","HKLM"})foreach(string v in Advanced.Views)foreach(string path in new[]{StartupApprovedRun,StartupApprovedRun32,StartupApprovedFolder}){
    cancel.ThrowIfCancellationRequested();var wanted=path==StartupApprovedFolder?linkNames:runNames;if(wanted.Count==0)continue;
    try{using(var root=Engine.Base(h,v))using(var key=root.OpenSubKey(path))if(key!=null)foreach(string n in key.GetValueNames())if(wanted.Contains(n))Value(result,h,v,path,key,n,"Trạng thái bật/tắt khởi động (StartupApproved) của mục Run/Startup thuộc ứng dụng; xóa cùng mục gốc.");}
    catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
   }
  }

  /// <summary>Shell registrations: Applications\exe, ProgIDs and CLSIDs whose commands point into the app, RegisteredApplications — review only, since class keys are shared territory.</summary>
  static void ScanClasses(AppEntry identity,List<string> exeNames,HashSet<string> names,ScanResult result,CancellationToken cancel,Stopwatch watch){
   double deadline=Math.Min(watch.Elapsed.TotalSeconds+10,25);int visited=0;
   foreach(string h in new[]{"HKCU","HKLM"})foreach(string v in (h=="HKCU"?new[]{Advanced.Views[0]}:Advanced.Views)){
    try{using(var root=Engine.Base(h,v))using(var classes=root.OpenSubKey(@"Software\Classes")){
     if(classes==null)continue;
     foreach(string exe in exeNames){cancel.ThrowIfCancellationRequested();using(var k=classes.OpenSubKey(@"Applications\"+exe+".exe"))if(k!=null)Review(result,h+@"\Software\Classes\Applications\"+exe+".exe","Đăng ký «Mở bằng» của executable đã gỡ; xóa khóa này trong Registry Editor nếu không còn dùng.");}
     using(var clsid=classes.OpenSubKey("CLSID"))if(clsid!=null)foreach(string sub in clsid.GetSubKeyNames()){
      cancel.ThrowIfCancellationRequested();if(++visited>12000||watch.Elapsed.TotalSeconds>deadline){result.Notes.Add("Đã chạm giới hạn duyệt Classes ("+h+"); kiểm tra thêm bằng Registry Editor.");break;}
      foreach(string server in new[]{"InprocServer32","LocalServer32"})try{using(var k=clsid.OpenSubKey(sub+"\\"+server)){if(k==null)continue;string cmd=Advanced.CommandExe(Engine.Read(k,""));if(cmd==""){cmd=Engine.Read(k,"").Trim('"');}if(Advanced.MatchesPath(identity,cmd)||UnderApp(identity,cmd)){Review(result,h+@"\Software\Classes\CLSID\"+sub,"Thành phần COM ("+server+") trỏ tới tệp trong thư mục ứng dụng đã gỡ; shell extension/COM hỏng — xem bằng Registry Editor.");break;}}}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
     }
     foreach(string sub in classes.GetSubKeyNames()){
      cancel.ThrowIfCancellationRequested();if(++visited>12000||watch.Elapsed.TotalSeconds>deadline)break;
      if(sub.StartsWith(".")||sub.StartsWith("{")||sub=="CLSID"||sub=="Applications"||sub=="Interface"||sub=="TypeLib"||sub=="AppID"||sub=="Local Settings"||sub=="WOW6432Node"||sub=="Installer"||sub=="Record"||sub=="Extensions"||sub=="MIME"||sub=="Wow6432Node")continue;
      try{using(var k=classes.OpenSubKey(sub+@"\shell\open\command")){if(k==null)continue;string cmd=Advanced.CommandExe(Engine.Read(k,""));if(Advanced.MatchesPath(identity,cmd)||UnderApp(identity,cmd))Review(result,h+@"\Software\Classes\"+sub,"Kiểu tệp (ProgID) mở bằng executable đã gỡ; các đuôi tệp trỏ tới ProgID này sẽ báo «không tìm thấy ứng dụng».");}}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
     }
    }}catch(UnauthorizedAccessException){result.Notes.Add("Không đọc được Software\\Classes ("+h+").");}catch(System.Security.SecurityException){}
    try{using(var root=Engine.Base(h,v))using(var reg=root.OpenSubKey(@"SOFTWARE\RegisteredApplications"))if(reg!=null)foreach(string n in reg.GetValueNames()){
     cancel.ThrowIfCancellationRequested();string target=Engine.Read(reg,n);string leaf=target.Split('\\').LastOrDefault(s=>s!="Capabilities")??"";
     if(names.Contains(n)||names.Contains(leaf)||exeNames.Any(e=>String.Equals(e,n,StringComparison.OrdinalIgnoreCase)))Review(result,h+@"\SOFTWARE\RegisteredApplications\"+n+"  →  "+target,"Đăng ký ứng dụng mặc định trỏ tới nhánh Clients/Capabilities của ứng dụng đã gỡ; xem trong Registry Editor.");
    }}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
   }
  }

  static void ScanPath(AppEntry identity,ScanResult result){
   foreach(var location in new[]{Tuple.Create("HKCU",@"Environment"),Tuple.Create("HKLM",@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment")}){
    try{using(var root=Engine.Base(location.Item1,Advanced.Views[0]))using(var key=root.OpenSubKey(location.Item2)){
     if(key==null)continue;object raw=key.GetValue("Path",null,RegistryValueOptions.DoNotExpandEnvironmentNames);
     foreach(string entry in PathEntries(Convert.ToString(raw)))if(UnderApp(identity,entry))Review(result,"PATH ("+(location.Item1=="HKCU"?"người dùng":"hệ thống")+"): "+entry,"Biến PATH còn trỏ tới thư mục ứng dụng đã gỡ; gỡ mục này trong Cài đặt → Biến môi trường.");
    }}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
   }
  }

  static void ScanEventLog(AppEntry identity,ScanResult result,CancellationToken cancel){
   try{using(var root=Engine.Base("HKLM",Advanced.Views[0]))using(var logs=root.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\EventLog")){
    if(logs==null)return;int count=0;
    foreach(string log in logs.GetSubKeyNames())using(var l=logs.OpenSubKey(log)){if(l==null)continue;foreach(string source in l.GetSubKeyNames()){
     cancel.ThrowIfCancellationRequested();if(++count>6000)return;
     try{using(var s=l.OpenSubKey(source)){if(s==null)continue;string file=Convert.ToString(s.GetValue("EventMessageFile",""));foreach(string part in file.Split(';'))if(UnderApp(identity,part.Trim())){Review(result,@"HKLM\SYSTEM\CurrentControlSet\Services\EventLog\"+log+"\\"+source,"Nguồn Event Log trỏ tới DLL thông điệp trong thư mục ứng dụng đã gỡ; Event Viewer sẽ báo thiếu mô tả.");break;}}}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
    }}
   }}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
  }

  /// <summary>Orphaned MSI product registrations (InstallProperties DisplayName / Installer\Products ProductName) and Microsoft\Tracing keys named after executables.</summary>
  static void ScanInstallerAndTracing(AppEntry app,HashSet<string> names,List<string> exeNames,ScanResult result,CancellationToken cancel){
   try{using(var root=Engine.Base("HKLM",Advanced.Views[0]))using(var users=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData")){
    if(users!=null)foreach(string sid in users.GetSubKeyNames())using(var products=users.OpenSubKey(sid+@"\Products")){if(products==null)continue;int count=0;foreach(string code in products.GetSubKeyNames()){
     cancel.ThrowIfCancellationRequested();if(++count>4000)break;
     try{using(var p=products.OpenSubKey(code+@"\InstallProperties")){if(p==null)continue;string display=Engine.Read(p,"DisplayName");if(display!=""&&(String.Equals(display,app.Name,StringComparison.OrdinalIgnoreCase)||names.Contains(display)))Review(result,@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\"+sid+@"\Products\"+code,"Đăng ký Windows Installer còn mang tên ứng dụng; nếu ứng dụng đã gỡ hẳn, dùng Microsoft «Program Install and Uninstall troubleshooter» để dọn.");}}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
    }}
   }}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
   try{using(var root=Engine.Base("HKLM",Advanced.Views[0]))using(var products=root.OpenSubKey(@"SOFTWARE\Classes\Installer\Products")){
    if(products!=null){int count=0;foreach(string code in products.GetSubKeyNames()){cancel.ThrowIfCancellationRequested();if(++count>4000)break;try{using(var p=products.OpenSubKey(code)){if(p==null)continue;string product=Engine.Read(p,"ProductName");if(product!=""&&(String.Equals(product,app.Name,StringComparison.OrdinalIgnoreCase)||names.Contains(product)))Review(result,@"HKLM\SOFTWARE\Classes\Installer\Products\"+code,"Sản phẩm Windows Installer còn mang tên ứng dụng (ProductName); xem bằng Registry Editor hoặc troubleshooter của Microsoft.");}}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}}}
   }}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
   foreach(string v in Advanced.Views)try{using(var root=Engine.Base("HKLM",v))using(var tracing=root.OpenSubKey(@"SOFTWARE\Microsoft\Tracing")){
    if(tracing==null)continue;foreach(string sub in tracing.GetSubKeyNames()){cancel.ThrowIfCancellationRequested();foreach(string exe in exeNames)if(sub.StartsWith(exe+"_",StringComparison.OrdinalIgnoreCase)){Review(result,@"HKLM\SOFTWARE\"+(v=="32"?@"WOW6432Node\":"")+@"Microsoft\Tracing\"+sub,"Khóa Tracing (RAS) do executable đã gỡ tạo; vô hại, có thể xóa bằng Registry Editor.");break;}}
   }}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
  }

  /// <summary>Crash dumps, WER reports, Prefetch, Recent shortcuts, Start Menu folders, Documents / Saved Games / dot-folders named after the app — all review only (system or personal areas).</summary>
  static void ScanUserFiles(AppEntry identity,HashSet<string> names,HashSet<string> variants,List<string> exeNames,ScanResult result,CancellationToken cancel){
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),programData=Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
   var allNames=new HashSet<string>(names.Concat(variants),StringComparer.OrdinalIgnoreCase);
   Action<string,Func<string,bool>,string,bool> files=(folder,match,reason,directories)=>{
    if(String.IsNullOrEmpty(folder)||!Directory.Exists(folder))return;int count=0;
    try{foreach(string entry in directories?Directory.EnumerateDirectories(folder):Directory.EnumerateFiles(folder)){cancel.ThrowIfCancellationRequested();if(++count>8000)break;if(match(Path.GetFileName(entry)))Review(result,entry,reason);}}
    catch(UnauthorizedAccessException){}catch(IOException){}
   };
   if(exeNames.Count>0){
    files(Path.Combine(local,"CrashDumps"),n=>TraceFileMatches(n,exeNames),"Tệp dump lỗi của executable đã gỡ; xóa thủ công để lấy lại dung lượng.",false);
    foreach(string wer in new[]{Path.Combine(local,@"Microsoft\Windows\WER\ReportArchive"),Path.Combine(local,@"Microsoft\Windows\WER\ReportQueue"),Path.Combine(programData,@"Microsoft\Windows\WER\ReportArchive"),Path.Combine(programData,@"Microsoft\Windows\WER\ReportQueue")})
     files(wer,n=>WerMatches(n,exeNames),"Báo cáo lỗi Windows (WER) của executable đã gỡ; dọn bằng Cleanup hệ thống hoặc xóa thủ công.",true);
    files(Path.Combine(windows,"Prefetch"),n=>TraceFileMatches(n,exeNames),"Tệp Prefetch của executable đã gỡ; Windows tự dọn dần, có thể xóa thủ công.",false);
   }
   string recent=Path.Combine(roaming,@"Microsoft\Windows\Recent");
   if(Directory.Exists(recent))try{int count=0;foreach(string link in Directory.EnumerateFiles(recent,"*.lnk")){cancel.ThrowIfCancellationRequested();if(++count>3000)break;string target=Advanced.ShortcutTarget(link);if(target!=""&&(Advanced.MatchesPath(identity,target)||UnderApp(identity,target)))Review(result,link,"Mục «Gần đây» trỏ tới tệp trong thư mục ứng dụng đã gỡ; xóa shortcut này là an toàn.");}}catch(UnauthorizedAccessException){}catch(IOException){}
   foreach(string programs in new[]{Path.Combine(roaming,@"Microsoft\Windows\Start Menu\Programs"),Path.Combine(programData,@"Microsoft\Windows\Start Menu\Programs")})foreach(string n in allNames){
    string dir=Path.Combine(programs,n);if(!Directory.Exists(dir))continue;
    bool onlyLinks;try{onlyLinks=Directory.EnumerateFileSystemEntries(dir).All(e=>e.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)||e.EndsWith(".url",StringComparison.OrdinalIgnoreCase)||e.EndsWith("desktop.ini",StringComparison.OrdinalIgnoreCase));}catch(Exception){onlyLinks=false;}
    Review(result,dir,onlyLinks?"Thư mục Start Menu mang tên ứng dụng (chỉ còn shortcut); các shortcut trỏ tới ứng dụng đã được liệt kê riêng, xóa thư mục sau khi dọn.":"Thư mục Start Menu mang tên ứng dụng; kiểm tra nội dung trước khi xóa.");
   }
   if(!String.IsNullOrEmpty(profile))foreach(string n in allNames){
    foreach(string parent in new[]{Path.Combine(profile,"Documents"),Path.Combine(profile,"Saved Games"),Path.Combine(profile,"Pictures"),Path.Combine(profile,"Videos"),Path.Combine(profile,"Music"),Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments)}){
     if(String.IsNullOrEmpty(parent))continue;string dir=Path.Combine(parent,n);if(Directory.Exists(dir))Review(result,dir,"Thư mục dữ liệu người dùng mang tên ứng dụng (tài liệu, save game, dự án); không tự dọn — sao lưu rồi xóa thủ công nếu chắc chắn.");
    }
    string dot=Path.Combine(profile,"."+n.ToLowerInvariant().Replace(" ",""));if(Directory.Exists(dot))Review(result,dot,"Thư mục cấu hình ẩn «.tên» trong hồ sơ người dùng do ứng dụng tạo; kiểm tra rồi xóa thủ công.");
   }
  }

  /// <summary>SOFTWARE\variant and SOFTWARE\publisher\variant keys for near-miss names; strong variants are cleanable, weak ones review only.</summary>
  static void ScanVariantKeys(AppEntry app,HashSet<string> variants,ScanResult result,CancellationToken cancel){
   foreach(string n in variants)foreach(string h in new[]{"HKCU","HKLM"})foreach(string v in Advanced.Views){
    cancel.ThrowIfCancellationRequested();
    foreach(string key in new[]{"SOFTWARE\\"+n,Engine.SafeName(app.Publisher)?"SOFTWARE\\"+app.Publisher+"\\"+n:""}){
     if(key=="")continue;
     try{Engine.ValidateRegistry(key);using(var b=Engine.Base(h,v))using(var k=b.OpenSubKey(key))if(k!=null){
      bool strong=Strong(n);
      result.Items.Add(new Candidate{Kind=strong?"Registry":"Review",Path=strong?key:h+"\\"+key,Hive=h,View=v,ReviewOnly=!strong,Reason=strong?"Khóa trùng tên rút gọn của ứng dụng («"+n+"», bỏ phiên bản/nhà phát hành); kiểm tra quyền sở hữu trước khi dọn.":"Khóa trùng tên ngắn «"+n+"» có thể thuộc phần mềm khác; chỉ xem."});
     }}catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
    }
   }
  }
 }
}
