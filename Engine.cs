using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace TweekPro {
 public class AppEntry {
  public string Name, Publisher, Version, Location, Command, Key, Hive, View, InstallDate, DisplayIcon;
  public bool Msi; public long Size; public List<string> KnownExecutables=new List<string>();
  public string Id { get { return Hive+"|"+View+"|"+Key; } }
 }
 public class Candidate {
  public string Kind, Path, Hive, View, Reason, AppId, AppName, ValueName, ExpectedHash; public bool ReviewOnly;
  public override string ToString(){return Kind+": "+Path;}
 }
 public class RegValue { public string Name; public RegistryValueKind Kind; public string Text; public string[] Texts; public byte[] Bytes; }
 public class RegNode { public List<RegValue> Values=new List<RegValue>(); public List<RegChild> Children=new List<RegChild>(); }
 public class RegChild { public string Name; public RegNode Node; }
 public class Backup {
  public string Id, Created, State, Original, Payload, Kind, Hive, View, AppName, Error, ValueName, Purpose;
  /// <summary>Vault folder this manifest was loaded from; null means the current vault. Not serialized.</summary>
  [XmlIgnore] public string VaultPath;
  /// <summary>Size of the payload on disk as measured when listed; -1 when unknown. Not serialized.</summary>
  [XmlIgnore] public long Bytes=-1;
  /// <summary>Parses the ISO creation stamp; returns DateTime.MinValue when missing or malformed.</summary>
  public DateTime CreatedAt { get { DateTime d;return DateTime.TryParseExact(Created??"","s",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out d)?d:DateTime.MinValue; } }
 }
 public static class Engine {
  public static string Vault=Core.Paths.Backups;
  const string Uninstall=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
  /// <summary>Returns the vault folder that physically holds a backup (legacy AppCare vault or the current one).</summary>
  public static string VaultOf(Backup b){return String.IsNullOrEmpty(b.VaultPath)?Vault:b.VaultPath;}
  /// <summary>Every vault folder to list: the current one plus the legacy AppCare vault when it still exists separately.</summary>
  public static string[] VaultFolders(){
   var list=new List<string>{Vault};
   string legacy=Core.Paths.LegacyBackups;
   if(!String.Equals(Canon(legacy),Canon(Vault),StringComparison.OrdinalIgnoreCase)&&Directory.Exists(legacy))list.Add(legacy);
   return list.ToArray();
  }
  public static RegistryKey Base(string hive,string view){return RegistryKey.OpenBaseKey(hive=="HKLM"?RegistryHive.LocalMachine:RegistryHive.CurrentUser,view=="64"?RegistryView.Registry64:RegistryView.Registry32);}
  public static string Read(RegistryKey k,string n){return Convert.ToString(k.GetValue(n,""));}
  public static List<AppEntry> Inventory(){
   var list=new List<AppEntry>();
   foreach(string h in new[]{"HKLM","HKCU"}) foreach(string v in (Environment.Is64BitOperatingSystem?new[]{"64","32"}:new[]{"32"})) {
    using(var b=Base(h,v)) using(var r=b.OpenSubKey(Uninstall)) {
     if(r==null)continue;
     foreach(string name in r.GetSubKeyNames()) { try { using(var k=r.OpenSubKey(name)) {
      if(k==null||Read(k,"DisplayName")==""||Read(k,"SystemComponent")=="1"||Read(k,"ParentKeyName")!="")continue;
      long size;long.TryParse(Read(k,"EstimatedSize"),out size);
      list.Add(new AppEntry{Name=Read(k,"DisplayName"),Publisher=Read(k,"Publisher"),Version=Read(k,"DisplayVersion"),Location=Read(k,"InstallLocation").Trim().Trim('"'),Command=Read(k,"UninstallString"),Key=Uninstall+"\\"+name,Hive=h,View=v,Msi=Read(k,"WindowsInstaller")=="1",Size=size,InstallDate=Read(k,"InstallDate"),DisplayIcon=Read(k,"DisplayIcon")});
     }}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){} }
    }
   }
   return list.GroupBy(a=>a.Hive+"|"+a.Key+"|"+a.Name+"|"+a.Command).Select(g=>g.First()).OrderBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
  }
  public static bool Installed(string id){
   var p=id.Split('|');if(p.Length!=3)throw new InvalidOperationException("Mã ứng dụng không hợp lệ.");
   using(var b=Base(p[0],p[1]))using(var k=b.OpenSubKey(p[2]))return k!=null;
  }
  public static string Canon(string p){return Path.GetFullPath(Environment.ExpandEnvironmentVariables(p)).TrimEnd(Path.DirectorySeparatorChar);}
  public static bool Under(string path,string root){string r=root.TrimEnd('\\','/');return path.StartsWith(r+"\\",StringComparison.OrdinalIgnoreCase)||path.StartsWith(r+"/",StringComparison.OrdinalIgnoreCase);}
  public static void NoLinks(string path,bool tree){
   string current=Canon(path);
   while(!String.IsNullOrEmpty(current)){
    if((Directory.Exists(current)||File.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Không xử lý đường dẫn liên kết: "+current);
    current=Path.GetDirectoryName(current);
   }
   if(tree&&Directory.Exists(path))foreach(string entry in Directory.EnumerateFileSystemEntries(path)){
    var attributes=File.GetAttributes(entry);
    if((attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Thư mục chứa liên kết: "+entry);
    if((attributes&FileAttributes.Directory)!=0)NoLinks(entry,true);
   }
  }
  /// <summary>Returns the current user's AppData\LocalLow folder, which sits beside Local rather than under it.</summary>
  public static string LocalLow(){
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   if(String.IsNullOrWhiteSpace(local))return "";
   string parent=Path.GetDirectoryName(local);
   return String.IsNullOrWhiteSpace(parent)?"":Canon(Path.Combine(parent,"LocalLow"));
  }
  /// <summary>Returns %LOCALAPPDATA%\Programs, the usual per-user install root for Electron and similar installers.</summary>
  public static string LocalPrograms(){
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   return String.IsNullOrWhiteSpace(local)?"":Canon(Path.Combine(local,"Programs"));
  }
  static string[] UniquePaths(IEnumerable<string> paths){return paths.Where(s=>!String.IsNullOrWhiteSpace(s)).Select(Canon).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
  /// <summary>Folders whose descendants may be quarantined after an uninstall, including per-user Programs and LocalLow.</summary>
  public static string[] Roots(){return UniquePaths(new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),LocalPrograms(),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),LocalLow(),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)});}
  /// <summary>User-local roots first so leftover scans reach AppData installs before Program Files can exhaust the visit budget.</summary>
  public static string[] ScanRoots(){return UniquePaths(new[]{LocalPrograms(),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),LocalLow(),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)});}
  /// <summary>AppData locations checked by exact name / publisher\app probes, including per-user Programs.</summary>
  public static string[] DataRoots(){return UniquePaths(new[]{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),LocalPrograms(),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),LocalLow(),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)});}
  /// <summary>Temp folders that may be probed by fingerprint only, never walked in full.</summary>
  public static string[] TempRoots(){
   var list=new List<string>();
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   if(!String.IsNullOrWhiteSpace(local))list.Add(Path.Combine(local,"Temp"));
   try{list.Add(Path.GetTempPath());}catch(IOException){}
   return UniquePaths(list);
  }
  public static void ValidateFolder(string path,bool inspectTree=true){
   string p=Canon(path); var roots=Roots();
   if(!roots.Any(r=>Under(p,r))||roots.Any(r=>String.Equals(p,r,StringComparison.OrdinalIgnoreCase)))throw new IOException("Đường dẫn nằm ngoài vùng được phép dọn.");
   string local=Canon(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
   foreach(string own in new[]{Core.Paths.ProductFolder,Core.Paths.LegacyFolder})if(Under(p,Path.Combine(local,own))||String.Equals(p,Path.Combine(local,own),StringComparison.OrdinalIgnoreCase))throw new IOException("Đây là thư mục được bảo vệ.");
   foreach(var r in roots)foreach(var name in new[]{"Microsoft","Windows","Common Files","Packages"}){
    string blocked=Path.Combine(r,name);if(String.Equals(p,blocked,StringComparison.OrdinalIgnoreCase)||Under(p,blocked))throw new IOException("Không dọn vùng hệ thống hoặc thành phần dùng chung.");
   }
   NoLinks(p,inspectTree);
  }
  public static bool Overlap(string a,string b){return String.Equals(a,b,StringComparison.OrdinalIgnoreCase)||Under(a,b)||Under(b,a);}
  public static bool SafeName(string name){return !String.IsNullOrWhiteSpace(name)&&name.Length>=3&&name.IndexOfAny(Path.GetInvalidFileNameChars())<0&&name!="."&&name!="..";}
  public static List<Candidate> Scan(AppEntry app){
   var found=new List<Candidate>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   if(SafeName(app.Name))names.Add(app.Name);
   // An exact leaf from the recorded install path supplies aliases such as GPU-Z.
   if(!String.IsNullOrWhiteSpace(app.Location))try{
    string location=Canon(app.Location);ValidateFolder(location,false);
    string leaf=Path.GetFileName(location);
    if(SafeName(leaf)&&!String.Equals(leaf,app.Publisher,StringComparison.OrdinalIgnoreCase))names.Add(leaf);
   }catch(IOException){}catch(ArgumentException){}catch(UnauthorizedAccessException){}

   Action<string,string> addFolder=(path,reason)=>{
    try{ValidateFolder(path,false);if(Directory.Exists(path))found.Add(new Candidate{Kind="Folder",Path=Canon(path),Reason=reason});}catch(IOException){}catch(ArgumentException){}catch(UnauthorizedAccessException){}
   };
   if(!String.IsNullOrWhiteSpace(app.Location))addFolder(app.Location,"Thư mục InstallLocation do ứng dụng khai báo; cần duyệt nội dung.");
   foreach(string root in DataRoots())foreach(string n in names){
    addFolder(Path.Combine(root,n),"Trùng tên ứng dụng hoặc tên thư mục cài đã ghi nhận trong AppData/ProgramData; có thể chứa dữ liệu cá nhân.");
    if(SafeName(app.Publisher))addFolder(Path.Combine(root,app.Publisher,n),"Đường dẫn nhà phát hành\\ứng dụng trong AppData/ProgramData; cần duyệt nội dung.");
   }
   foreach(string temp in TempRoots())foreach(string n in names){
    addFolder(Path.Combine(temp,n),"Thư mục tạm trùng tên ứng dụng hoặc thư mục cài; chỉ đưa vào khi khớp dấu vân tay, không quét toàn bộ Temp.");
    if(SafeName(app.Publisher))addFolder(Path.Combine(temp,app.Publisher,n),"Thư mục tạm theo nhà phát hành\\ứng dụng; chỉ đưa vào khi khớp dấu vân tay.");
   }
   if(app.KnownExecutables!=null)foreach(string exe in app.KnownExecutables){
    try{string dir=Path.GetDirectoryName(exe);if(!String.IsNullOrWhiteSpace(dir))addFolder(dir,"Thư mục chứa executable đã ghi nhận trước khi gỡ; cần duyệt nội dung.");}catch(ArgumentException){}
   }
   foreach(string h in new[]{"HKCU","HKLM"})foreach(string v in (Environment.Is64BitOperatingSystem?new[]{"64","32"}:new[]{"32"}))foreach(string n in names){
    foreach(string key in new[]{"SOFTWARE\\"+n,SafeName(app.Publisher)?"SOFTWARE\\"+app.Publisher+"\\"+n:""}){
     if(key=="")continue;try{ValidateRegistry(key);using(var b=Base(h,v))using(var k=b.OpenSubKey(key))if(k!=null)found.Add(new Candidate{Kind="Registry",Path=key,Hive=h,View=v,Reason="Khóa trùng tên ứng dụng; cần kiểm tra quyền sở hữu trước khi dọn."});}catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
    }
   }
   foreach(var c in found){c.AppId=app.Id;c.AppName=app.Name;}
   return found.GroupBy(c=>c.Kind+"|"+c.Path+"|"+c.Hive+"|"+c.View,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
  }
  public static void ValidateRegistry(string key){
   var p=key.Split('\\');if(p.Length<2||p.Length>3||!p[0].Equals("SOFTWARE",StringComparison.OrdinalIgnoreCase)||p.Any(s=>!SafeName(s))||new[]{"Microsoft","Windows","Classes","Policies","WOW6432Node"}.Contains(p[1],StringComparer.OrdinalIgnoreCase))throw new IOException("Khóa Registry nằm ngoài vùng được phép dọn.");
  }
  public static RegNode ReadTree(RegistryKey key){
   var node=new RegNode();foreach(string n in key.GetValueNames()){
    var kind=key.GetValueKind(n);var value=key.GetValue(n,null,RegistryValueOptions.DoNotExpandEnvironmentNames);var r=new RegValue{Name=n,Kind=kind};
    if(kind==RegistryValueKind.Binary||kind==RegistryValueKind.None)r.Bytes=(byte[])value;
    else if(kind==RegistryValueKind.MultiString)r.Texts=(string[])value;
    else if(kind==RegistryValueKind.DWord)r.Text=((int)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
    else if(kind==RegistryValueKind.QWord)r.Text=((long)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
    else if(kind==RegistryValueKind.String||kind==RegistryValueKind.ExpandString)r.Text=(string)value;
    else throw new IOException("Kiểu Registry chưa được hỗ trợ; giữ nguyên khóa.");
    node.Values.Add(r);
   }
   foreach(string n in key.GetSubKeyNames())using(var child=key.OpenSubKey(n)){if(child==null)throw new IOException("Không đọc được khóa con.");node.Children.Add(new RegChild{Name=n,Node=ReadTree(child)});}return node;
  }
  public static void WriteTree(RegistryKey key,RegNode node){
   foreach(var r in node.Values){object value=r.Text;
    if(r.Kind==RegistryValueKind.Binary||r.Kind==RegistryValueKind.None)value=r.Bytes;
    else if(r.Kind==RegistryValueKind.MultiString)value=r.Texts;
    else if(r.Kind==RegistryValueKind.DWord)value=Int32.Parse(r.Text,System.Globalization.CultureInfo.InvariantCulture);
    else if(r.Kind==RegistryValueKind.QWord)value=Int64.Parse(r.Text,System.Globalization.CultureInfo.InvariantCulture);
    key.SetValue(r.Name,value,r.Kind);
   }
   foreach(var c in node.Children)using(var child=key.CreateSubKey(c.Name))WriteTree(child,c.Node);
  }
  public static void Save<T>(string path,T value){
   string tmp=path+".tmp";using(var f=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){new XmlSerializer(typeof(T)).Serialize(f,value);f.Flush(true);}
   if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
  }
  public static T Load<T>(string path){using(var f=File.OpenRead(path))return (T)new XmlSerializer(typeof(T)).Deserialize(f);}
  public static void SaveBackup(Backup b){Save(Path.Combine(VaultOf(b),b.Id,"manifest.xml"),b);}
  public static Backup Quarantine(Candidate c){
   if(c.ReviewOnly)throw new IOException("Mục này chỉ để kiểm tra.");
   if(c.Kind=="File"||c.Kind=="RegistryValue")return Advanced.Store(c,false);
   if(Installed(c.AppId))throw new IOException("Ứng dụng vẫn được đăng ký cài đặt. Hãy gỡ chính thức và chờ hoàn tất trước khi dọn.");
   if(c.Kind=="Folder"){
    ValidateFolder(c.Path);if(!Directory.Exists(c.Path))throw new IOException("Thư mục không còn tồn tại.");
    foreach(var a in Inventory())if(!String.IsNullOrWhiteSpace(a.Location)){try{if(Overlap(Canon(c.Path),Canon(a.Location)))throw new IOException("Thư mục giao với ứng dụng còn cài: "+a.Name);}catch(ArgumentException){}}
   }else { ValidateRegistry(c.Path); if(Inventory().Any(a=>String.Equals(a.Name,c.AppName,StringComparison.OrdinalIgnoreCase)))throw new IOException("Một ứng dụng cùng tên vẫn còn cài đặt; giữ lại Registry."); }
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Original=c.Kind=="Folder"?Canon(c.Path):c.Path,Kind=c.Kind,Hive=c.Hive,View=c.View,AppName=c.AppName};
   NoLinks(Vault,false);Directory.CreateDirectory(Path.Combine(Vault,backup.Id));backup.Payload=c.Kind=="Folder"?"content":"registry.xml";SaveBackup(backup);
   try{
    string payload=Path.Combine(Vault,backup.Id,backup.Payload);
    if(c.Kind=="Folder"){
     if(!String.Equals(Path.GetPathRoot(backup.Original),Path.GetPathRoot(payload),StringComparison.OrdinalIgnoreCase))throw new IOException("Bản này chỉ chuyển vào kho trên cùng ổ đĩa.");
     var outcome=Core.StubbornFiles.Escalate(backup.Original,()=>{ValidateFolder(backup.Original);Directory.Move(backup.Original,payload);});
     if(outcome.Steps!="")Core.Log.Info("Tệp cứng đầu tại "+backup.Original+": "+outcome.Steps+".");
    }else{
     using(var b=Base(c.Hive,c.View)){
      RegNode node;using(var key=b.OpenSubKey(c.Path)){if(key==null)throw new IOException("Khóa không còn tồn tại.");node=ReadTree(key);}
      Save(payload,node);Load<RegNode>(payload);if(Installed(c.AppId))throw new IOException("Ứng dụng đã xuất hiện lại; hủy dọn.");b.DeleteSubKeyTree(c.Path);
     }
    }
    backup.State="BackedUp";SaveBackup(backup);return backup;
   }catch(Exception e){backup.Error=e.Message;backup.State="NeedsReview";SaveBackup(backup);throw;}
  }
  /// <summary>
  /// Last resort for a leftover that stays locked after ownership takeover: schedules it for deletion at the next boot.
  /// Same safety checks as Quarantine; records a PendingReboot entry (no payload) so the vault shows what will disappear.
  /// </summary>
  public static Backup ScheduleOnReboot(Candidate c){
   if(c.ReviewOnly)throw new IOException("Mục này chỉ để kiểm tra.");
   if(c.Kind!="Folder"&&c.Kind!="File")throw new IOException("Chỉ hẹn xóa được thư mục hoặc tệp.");
   if(Installed(c.AppId))throw new IOException("Ứng dụng vẫn được đăng ký cài đặt. Hãy gỡ chính thức và chờ hoàn tất trước khi dọn.");
   string path=Canon(c.Path);
   if(c.Kind=="Folder"){
    if(!Directory.Exists(path))throw new IOException("Thư mục không còn tồn tại.");
    Core.StubbornFiles.Escalate(path,()=>ValidateFolder(path));
    foreach(var a in Inventory())if(!String.IsNullOrWhiteSpace(a.Location)){try{if(Overlap(path,Canon(a.Location)))throw new IOException("Thư mục giao với ứng dụng còn cài: "+a.Name);}catch(ArgumentException){}}
   }else{Advanced.ValidateFile(path);if(!File.Exists(path))throw new IOException("Tệp không còn tồn tại.");}
   Core.StubbornFiles.ClearAttributes(path);Core.StubbornFiles.TakeOwnership(path);
   int refused;int scheduled=Core.StubbornFiles.ScheduleDeleteOnReboot(path,out refused);
   if(scheduled==0)throw new IOException("Windows không nhận lịch xóa khi khởi động lại cho "+path+".");
   foreach(var stale in Backups().Where(b=>b.State=="NeedsReview"&&String.IsNullOrEmpty(b.VaultPath)&&String.Equals(b.Original,path,StringComparison.OrdinalIgnoreCase)&&!Directory.Exists(Path.Combine(Vault,b.Id,b.Payload??"content")))){try{Directory.Delete(Path.Combine(Vault,stale.Id),true);}catch(Exception){}}
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="PendingReboot",Original=path,Kind=c.Kind,AppName=c.AppName,Purpose="RebootDelete",Payload="",Error="Hẹn xóa "+scheduled+" mục khi khởi động lại"+(refused>0?" ("+refused+" mục Windows từ chối, xem nhật ký)":"")+"; không có bản sao lưu."};
   NoLinks(Vault,false);Directory.CreateDirectory(Path.Combine(Vault,backup.Id));SaveBackup(backup);
   Core.Log.Info("Hẹn xóa khi khởi động lại ("+scheduled+" mục): "+path);
   return backup;
  }
  /// <summary>Lists manifests from the current vault and, when present, the legacy AppCare vault so old backups stay restorable.</summary>
  public static List<Backup> Backups(){
   var list=new List<Backup>();
   foreach(string vault in VaultFolders()){
    if(!Directory.Exists(vault))continue;
    try{NoLinks(vault,false);}catch(IOException){continue;}
    foreach(string d in Directory.GetDirectories(vault)){try{NoLinks(d,false);var b=Load<Backup>(Path.Combine(d,"manifest.xml"));if(b.Id==Path.GetFileName(d)){b.VaultPath=String.Equals(Canon(vault),Canon(Vault),StringComparison.OrdinalIgnoreCase)?null:vault;list.Add(b);}}catch(Exception){}}
   }
   return list.GroupBy(b=>b.Id).Select(g=>g.First()).OrderByDescending(b=>b.Created).ToList();
  }
  /// <summary>Selects backups older than the given number of days; unrestored ones are included only when explicitly requested.</summary>
  public static List<Backup> SelectForPurge(IEnumerable<Backup> backups,int olderThanDays,DateTime now,bool includeUnrestored){
   DateTime cutoff=now.AddDays(-Math.Max(0,olderThanDays));
   return backups.Where(b=>{
    var created=b.CreatedAt;if(created==DateTime.MinValue)return false;
    if(created>cutoff)return false;
    return b.State=="Restored"||includeUnrestored;
   }).ToList();
  }
  /// <summary>Measures the bytes held by a backup folder; returns 0 when the folder is missing.</summary>
  public static long BackupSize(Backup b){
   string dir=Path.Combine(VaultOf(b),b.Id);if(!Directory.Exists(dir))return 0;long total=0;
   try{foreach(string f in Directory.EnumerateFiles(dir,"*",SearchOption.AllDirectories)){try{total+=new FileInfo(f).Length;}catch(IOException){}catch(UnauthorizedAccessException){}}}catch(IOException){}catch(UnauthorizedAccessException){}
   return total;
  }
  /// <summary>Permanently deletes one backup folder (manifest and payload). Irreversible; the caller must have confirmed. Returns bytes released.</summary>
  public static long Purge(Backup b){
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   string vault=Canon(VaultOf(b));string dir=Canon(Path.Combine(vault,b.Id));
   if(!Under(dir,vault))throw new IOException("Thư mục sao lưu nằm ngoài kho.");
   if(!Directory.Exists(dir))throw new IOException("Bản sao lưu không còn trên đĩa.");
   NoLinks(dir,true);
   if(!File.Exists(Path.Combine(dir,"manifest.xml")))throw new IOException("Thư mục không có manifest; không xóa.");
   long bytes=BackupSize(b);
   Directory.Delete(dir,true);
   return bytes;
  }
  public static void Restore(Backup b){
   if(b.Kind=="File"||b.Kind=="RegistryValue"){Advanced.Restore(b);return;}
   if(b.Kind=="Junk"){Cleaner.JunkCleaner.Restore(b);return;}
   if(b.Kind=="Duplicate"){Dupes.DuplicateFinder.Restore(b);return;}
   if(b.Kind=="Store"){Store.WindowsApps.Restore(b);return;}
   if(b.State=="PendingReboot")throw new IOException("Mục này được hẹn xóa khi khởi động lại và không có bản sao lưu để khôi phục.");
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   string payload=Path.Combine(VaultOf(b),b.Id,b.Kind=="Folder"?"content":"registry.xml");NoLinks(payload,true);
   if(b.Kind=="Folder"){
    ValidateFolder(b.Original);if(Directory.Exists(b.Original)||File.Exists(b.Original))throw new IOException("Đích đã tồn tại; không ghi đè dữ liệu.");
    if(!Directory.Exists(payload))throw new IOException("Không có dữ liệu trong kho.");
    Directory.CreateDirectory(Path.GetDirectoryName(b.Original));NoLinks(b.Original,false);Directory.Move(payload,b.Original);
   }else if(b.Kind=="Registry"){
    ValidateRegistry(b.Original);var node=Load<RegNode>(payload);using(var root=Base(b.Hive,b.View)){
     using(var existing=root.OpenSubKey(b.Original))if(existing!=null)throw new IOException("Khóa đã tồn tại; không ghi đè dữ liệu.");
     using(var key=root.CreateSubKey(b.Original))WriteTree(key,node);
    }
   }else throw new IOException("Loại sao lưu không hợp lệ.");
   b.State="Restored";b.Error="";SaveBackup(b);
  }
  public static ProcessStartInfo UninstallInfo(AppEntry a){
   Guid product;string leaf=a.Key.Substring(a.Key.LastIndexOf('\\')+1);
   if(a.Msi&&Guid.TryParse(leaf,out product))return new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"msiexec.exe"),"/x "+product.ToString("B")){UseShellExecute=true};
   string cmd=Environment.ExpandEnvironmentVariables(a.Command??"").Trim();string exe,args;
   if(cmd.StartsWith("\"")){int end=cmd.IndexOf('"',1);if(end<2)throw new IOException("Lệnh gỡ thiếu dấu ngoặc kép.");exe=cmd.Substring(1,end-1);args=cmd.Substring(end+1).Trim();}
   else {var m=Regex.Match(cmd,@"^(.*?\.exe)(?:\s+(.*))?$",RegexOptions.IgnoreCase);if(!m.Success)throw new IOException("Lệnh gỡ không được hỗ trợ. Dùng Windows Settings để gỡ ứng dụng này.");exe=m.Groups[1].Value;args=m.Groups[2].Value;}
   if(!Path.IsPathRooted(exe)||!File.Exists(exe)||!Path.GetExtension(exe).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Không tìm thấy trình gỡ .exe với đường dẫn tuyệt đối.");
   var deny=new[]{"cmd.exe","powershell.exe","pwsh.exe","wscript.exe","cscript.exe","mshta.exe"};if(deny.Contains(Path.GetFileName(exe),StringComparer.OrdinalIgnoreCase))throw new IOException("Lệnh gỡ qua trình thông dịch chưa được hỗ trợ; hãy dùng Windows Settings.");
   return new ProcessStartInfo(exe,args){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(exe)};
  }
  public static string Csv(string value){if(!String.IsNullOrEmpty(value)&&"=+-@\t\r\n".IndexOf(value[0])>=0)value="'"+value;return "\""+(value??"").Replace("\"","\"\"")+"\"";}
 }
}
