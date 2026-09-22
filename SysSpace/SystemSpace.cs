using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using TweekPro.Sizing;

namespace TweekPro.SysSpace {
 /// <summary>One system-owned space consumer: how big it is, which Windows tool reclaims it and whether that is reversible.</summary>
 public class SpaceItem {
  public string Id, Name, Description, Location, ToolLabel, ToolCommand, Warning, Group; public bool Info, Reversible;
  /// <summary>True when Tweek Pro itself moves the files into the vault (restorable) instead of calling a Windows tool.</summary>
  public bool Vaulted;
  public long Bytes=-1; public bool Partial, Available; public List<string> Details=new List<string>(); public string Note="";
  /// <summary>Bytes the tool is expected to free (-1 unknown); ReclaimNote explains the source of the estimate.</summary>
  public long Reclaim=-1; public string ReclaimNote="";
  /// <summary>Concrete files an action would move (installer orphans).</summary>
  public List<string> Paths=new List<string>();
 }

 /// <summary>Captured output of an external tool.</summary>
 public class ToolResult { public string Exe, Args, Output="", Error=""; public int ExitCode; public TimeSpan Elapsed; public bool Ok { get { return ExitCode==0; } } }

 /// <summary>One package in the driver store as reported by Get-WindowsDriver -Online.</summary>
 public class DriverPackage {
  public string Published, OriginalFile, Provider, Class, Version, Date; public bool BootCritical, Inbox; public long Bytes=-1;
  public string Inf { get { return Path.GetFileName(OriginalFile??"").ToLowerInvariant(); } }
  public string Folder { get { try{return Path.GetDirectoryName(OriginalFile??"")??"";}catch(Exception){return "";} } }
 }

 /// <summary>
 /// System-space cleanup that never deletes files itself: every action is the matching Windows tool (cleanmgr, DISM, pnputil,
 /// powercfg, Delivery Optimization / Recycle Bin cmdlets). Measurement is read-only and bounded.
 /// </summary>
 public static class SystemSpace {
  /// <summary>Test hook: replaces process execution. Receives exe and arguments, returns the captured result.</summary>
  public static Func<string,string,TimeSpan,ToolResult> Runner;
  public const int SageSet=77;
  public const string InstallerBackupKind="Installer";
  /// <summary>Cached .msi/.msp files newer than this are never treated as orphans (an installation may still be registering them).</summary>
  public static readonly TimeSpan InstallerMinAge=TimeSpan.FromDays(1);
  const string VolumeCaches=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches";
  const string InstallerUserData=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData";
  static bool Windows { get { return Environment.OSVersion.Platform==PlatformID.Win32NT; } }
  static string SystemDrive { get { string w=Environment.GetFolderPath(Environment.SpecialFolder.Windows);return String.IsNullOrEmpty(w)?@"C:\":Path.GetPathRoot(w); } }
  static string WinDir { get { string w=Environment.GetFolderPath(Environment.SpecialFolder.Windows);return String.IsNullOrEmpty(w)?@"C:\Windows":w; } }

  public static List<SpaceItem> Catalog(){
   string root=SystemDrive;
   return new List<SpaceItem>{
    new SpaceItem{Id="windows-old",Group="Windows",Name="Windows.old — bản Windows trước",Description="Bản cài trước còn giữ để quay lui sau khi nâng cấp lớn; Windows tự xóa sau 10 ngày nhưng thường sót lại.",Location=Path.Combine(root,"Windows.old"),ToolLabel="Dọn bằng Disk Cleanup (Previous Installations)",ToolCommand="cleanmgr.exe /sagerun:"+SageSet,Warning="Sau khi dọn sẽ KHÔNG thể quay lui về bản Windows trước."},
    new SpaceItem{Id="winsxs",Group="Windows",Name="WinSxS — kho thành phần",Description="Kho thành phần Windows chứa mọi phiên bản tệp hệ thống đã cập nhật. Chỉ dọn bằng DISM; không bao giờ xóa tay.",Location=Path.Combine(WinDir,"WinSxS"),ToolLabel="DISM /StartComponentCleanup",ToolCommand="Dism.exe /Online /Cleanup-Image /StartComponentCleanup",Warning="Chạy vài phút; không dùng /ResetBase nên vẫn gỡ được bản cập nhật gần nhất."},
    new SpaceItem{Id="delivery-optimization",Group="Windows",Name="Bộ đệm Delivery Optimization",Description="Tệp cập nhật Windows/Store tải về để chia sẻ ngang hàng với máy khác trong mạng.",Location=Path.Combine(WinDir,@"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"),ToolLabel="Delete-DeliveryOptimizationCache",ToolCommand="powershell Delete-DeliveryOptimizationCache -Force",Warning="Windows sẽ tải lại khi cần; không ảnh hưởng dữ liệu."},
    new SpaceItem{Id="driver-store",Group="Windows",Name="Driver cũ trùng lặp trong DriverStore",Description="Gói driver bên thứ ba còn nhiều phiên bản cho cùng một INF; chỉ đề xuất bản cũ hơn, bỏ qua driver hộp và driver khởi động.",Location=Path.Combine(WinDir,@"System32\DriverStore\FileRepository"),ToolLabel="pnputil /delete-driver (không cưỡng bức)",ToolCommand="pnputil.exe /delete-driver oemNN.inf",Warning="pnputil từ chối gói đang được thiết bị dùng — đó là lớp an toàn, Tweek Pro không dùng /force."},
    new SpaceItem{Id="installer-orphans",Group="Windows",Name="Windows\\Installer — gói MSI/MSP mồ côi",Description="Bản cache .msi/.msp mà không sản phẩm hay bản vá nào còn tham chiếu (LocalPackage trong Installer\\UserData). Thường vài GB sau nhiều năm cập nhật Office, driver, runtime.",Location=Path.Combine(WinDir,"Installer"),ToolLabel="Chuyển vào Kho khôi phục (Tweek Pro)",ToolCommand="Tweek Pro → Kho khôi phục (Kind=Installer)",Warning="Chỉ những tệp không còn được tham chiếu và cũ hơn 1 ngày. Nếu sau đó một ứng dụng báo thiếu gói khi sửa/gỡ, khôi phục tệp từ Kho.",Vaulted=true},
    new SpaceItem{Id="hiberfil",Group="Nguồn điện",Name="hiberfil.sys — tệp ngủ đông",Description="Ảnh RAM cho Hibernate và Fast Startup, thường 40–75% dung lượng RAM.",Location=Path.Combine(root,"hiberfil.sys"),ToolLabel="powercfg /hibernate off",ToolCommand="powercfg.exe /hibernate off",Warning="Tắt luôn Hibernate và Fast Startup; bật lại bằng powercfg /hibernate on.",Reversible=true},
    new SpaceItem{Id="pagefile",Group="Nguồn điện",Name="pagefile.sys + swapfile.sys",Description="Bộ nhớ ảo do Windows quản lý. Chỉ hiển thị; đổi kích thước trong System Properties → Performance.",Location=Path.Combine(root,"pagefile.sys"),ToolLabel="Mở System Properties → Performance",ToolCommand="SystemPropertiesPerformance.exe",Info=true},
    new SpaceItem{Id="restore-points",Group="Sao lưu",Name="Điểm khôi phục hệ thống (Shadow Copies)",Description="Dung lượng System Restore / Volume Shadow Copy đang dùng trên các ổ. Chỉ hiển thị; xóa điểm cũ trong System Protection.",Location="Win32_ShadowStorage",ToolLabel="Mở System Protection",ToolCommand="SystemPropertiesProtection.exe",Info=true},
    new SpaceItem{Id="recycle-bin",Group="Sao lưu",Name="Thùng rác (mọi ổ đĩa)",Description="Tệp đã xóa còn nằm trong $Recycle.Bin của tất cả ổ cố định.",Location=Path.Combine(root,"$Recycle.Bin"),ToolLabel="Clear-RecycleBin",ToolCommand="powershell Clear-RecycleBin -Force",Warning="Tệp trong thùng rác bị xóa hẳn, không qua Kho khôi phục."}
   };
  }

  /// <summary>Fills Bytes/Available/Reclaim for one item using folder walks, file sizes, DISM analysis or a culture-neutral PowerShell query; never throws.</summary>
  public static void Measure(SpaceItem item,TimeSpan budget,List<DriverPackage> drivers=null,Action<string> progress=null){
   item.Details.Clear();item.Paths.Clear();item.Note="";item.ReclaimNote="";item.Partial=false;item.Reclaim=-1;
   try{
    switch(item.Id){
     case "hiberfil":case "pagefile":{
      long total=0;bool any=false;
      foreach(string f in item.Id=="pagefile"?new[]{item.Location,Path.Combine(Path.GetDirectoryName(item.Location),"swapfile.sys")}:new[]{item.Location}){
       try{if(File.Exists(f)){var info=new FileInfo(f);total+=info.Length;any=true;item.Details.Add(f+"  "+Presentation.BytesLabel(info.Length));}}catch(Exception){}
      }
      item.Available=any;item.Bytes=any?total:-1;if(!any&&item.Id=="hiberfil")item.Note="Hibernate đang tắt.";
      if(item.Id=="hiberfil"&&any){item.Reclaim=total;item.ReclaimNote="Toàn bộ tệp biến mất khi tắt Hibernate.";}
      return;
     }
     case "recycle-bin":{
      long total=0;bool any=false;
      foreach(var drive in SafeDrives()){string bin=Path.Combine(drive.RootDirectory.FullName,"$Recycle.Bin");if(!Directory.Exists(bin))continue;bool p;long b=FolderSize.Measure(bin,budget,out p);if(b<0)continue;any=true;total+=b;item.Partial|=p;item.Details.Add(bin+"  "+Presentation.BytesLabel(b));}
      item.Available=any;item.Bytes=any?total:-1;if(any){item.Reclaim=total;item.ReclaimNote="Toàn bộ thùng rác.";}return;
     }
     case "restore-points":{
      if(!Windows||Core.PowerShell.Executable==null){item.Available=false;item.Note="Chỉ đọc được trên Windows.";return;}
      var r=Core.PowerShell.Run("Get-CimInstance Win32_ShadowStorage -ErrorAction SilentlyContinue | ForEach-Object { \"$($_.Volume.DeviceID)|$($_.UsedSpace)|$($_.MaxSpace)\" }",TimeSpan.FromSeconds(40));
      long total=0;foreach(string line in r.Output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){var parts=line.Split('|');long used;if(parts.Length>=2&&long.TryParse(parts[1],out used)){total+=used;item.Details.Add(parts[0]+"  "+Presentation.BytesLabel(used));}}
      item.Available=item.Details.Count>0;item.Bytes=item.Available?total:0;item.ReclaimNote="Tùy số điểm khôi phục bạn xóa trong System Protection.";return;
     }
     case "driver-store":{
      if(drivers==null){if(!Windows||Core.PowerShell.Executable==null){item.Available=false;item.Note="Chỉ đọc được trên Windows.";return;}if(progress!=null)progress("Get-WindowsDriver -Online…");drivers=ListDrivers();}
      var older=Duplicates(drivers);long total=0;bool measuredAll=true;
      foreach(var d in older){string folder=d.Folder;if(folder!=""&&Directory.Exists(folder)){bool p;d.Bytes=FolderSize.Measure(folder,TimeSpan.FromSeconds(3),out p);if(d.Bytes>0)total+=d.Bytes;else measuredAll=false;}else measuredAll=false;item.Details.Add(d.Published+"  "+d.Inf+"  "+d.Provider+"  v"+d.Version+" ("+d.Date+")"+(d.Bytes>0?"  "+Presentation.BytesLabel(d.Bytes):""));}
      item.Available=older.Count>0;item.Bytes=total;item.Partial=!measuredAll&&older.Count>0;item.Note=older.Count==0?"Không có gói trùng lặp; "+drivers.Count+" gói đã kiểm tra.":older.Count+" bản cũ trong "+drivers.Count+" gói.";
      if(older.Count>0){item.Reclaim=total;item.ReclaimNote="Tổng các gói cũ; pnputil có thể từ chối gói đang dùng.";}
      return;
     }
     case "installer-orphans":{
      if(!Windows){item.Available=false;item.Note="Chỉ đọc được trên Windows.";return;}
      if(progress!=null)progress("Đọc Installer\\UserData…");
      int products;var referenced=ReferencedPackages(out products);
      if(products==0){item.Available=false;item.Bytes=-1;item.Note="Không đọc được danh sách sản phẩm MSI — bỏ qua để tránh xóa nhầm.";return;}
      var orphans=Orphans(item.Location,referenced,DateTime.Now-InstallerMinAge);
      long total=0;foreach(var f in orphans){total+=f.Length;item.Paths.Add(f.FullName);item.Details.Add(f.FullName+"  "+Presentation.BytesLabel(f.Length)+"  ("+f.LastWriteTime.ToString("dd/MM/yyyy")+")");}
      item.Available=orphans.Count>0;item.Bytes=total;item.Note=orphans.Count==0?products+" sản phẩm/bản vá tham chiếu "+referenced.Count+" gói; không có gói mồ côi.":orphans.Count+" gói mồ côi; "+products+" sản phẩm/bản vá còn tham chiếu "+referenced.Count+" gói.";
      if(orphans.Count>0){item.Reclaim=total;item.ReclaimNote="Toàn bộ, khôi phục được từ Kho.";}
      return;
     }
     default:{
      bool exists;try{exists=Directory.Exists(item.Location);}catch(Exception){exists=false;}
      item.Available=exists;if(!exists){item.Bytes=-1;item.Note=item.Id=="windows-old"?"Không có Windows.old.":"Thư mục không tồn tại.";return;}
      bool p;item.Bytes=FolderSize.Measure(item.Location,budget,out p);item.Partial=p;
      if(item.Bytes<0){item.Available=false;item.Note="Không đủ quyền đọc.";return;}
      if(item.Id=="winsxs")AnalyzeComponentStore(item,progress);
      else{item.Reclaim=item.Bytes;item.ReclaimNote=item.Id=="windows-old"?"Gần như toàn bộ thư mục.":"Toàn bộ bộ đệm; Windows tải lại khi cần.";}
      return;
     }
    }
   }catch(Exception e){item.Available=false;item.Bytes=-1;item.Note=e.Message;}
  }

  /// <summary>Runs DISM /AnalyzeComponentStore (read-only, elevated) and turns its "Backups and Disabled Features" + "Cache and Temporary Data" lines into the reclaim estimate.</summary>
  static void AnalyzeComponentStore(SpaceItem item,Action<string> progress){
   if(!Windows||Runner==null&&!Core.Elevation.IsElevated){item.ReclaimNote="Cần quyền quản trị để DISM ước tính.";return;}
   if(progress!=null)progress("Dism /AnalyzeComponentStore…");
   string sys=Environment.GetFolderPath(Environment.SpecialFolder.System);if(String.IsNullOrEmpty(sys))sys=@"C:\Windows\System32";
   ToolResult r;try{r=Execute(Path.Combine(sys,"Dism.exe"),"/Online /Cleanup-Image /AnalyzeComponentStore",TimeSpan.FromMinutes(5),null);}catch(Exception e){item.ReclaimNote="DISM: "+e.Message;return;}
   var parsed=ParseAnalyze(r.Output+"\n"+r.Error);
   if(parsed.Count<5){item.ReclaimNote=r.Ok?"DISM không trả về số liệu đọc được.":"DISM lỗi mã "+r.ExitCode+".";return;}
   item.Reclaim=parsed[3]+parsed[4];item.ReclaimNote="DISM: sao lưu và tính năng đã tắt "+Presentation.BytesLabel(parsed[3])+" + bộ đệm "+Presentation.BytesLabel(parsed[4])+" (Explorer thấy "+Presentation.BytesLabel(parsed[0])+", thực tế "+Presentation.BytesLabel(parsed[1])+").";
   item.Details.Insert(0,"DISM: "+String.Join(" / ",parsed.Take(5).Select(Presentation.BytesLabel)));
  }

  /// <summary>Extracts every "label : 1.23 GB" size in order from DISM output regardless of display language; the fixed order is Explorer size, actual size, shared, backups+disabled, cache+temp.</summary>
  public static List<long> ParseAnalyze(string text){
   var list=new List<long>();
   foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text??"",@":\s*([\d]+(?:[.,]\d+)?)\s*(bytes?|KB|MB|GB|TB)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase)){
    double n;if(!double.TryParse(m.Groups[1].Value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out n))continue;
    string unit=m.Groups[2].Value.ToUpperInvariant();double mult=unit.StartsWith("B")?1:unit=="KB"?1024d:unit=="MB"?1024d*1024:unit=="GB"?1024d*1024*1024:1024d*1024*1024*1024;
    list.Add((long)(n*mult));
   }
   return list;
  }

  /// <summary>Every LocalPackage path referenced by installed products and patches (all users), plus how many product/patch keys were read.</summary>
  public static HashSet<string> ReferencedPackages(out int products){
   var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);products=0;
   if(!Windows)return set;
   using(var root=Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,Microsoft.Win32.RegistryView.Registry64))using(var userData=root.OpenSubKey(InstallerUserData)){
    if(userData==null)return set;
    foreach(string sid in userData.GetSubKeyNames()){
     foreach(string kind in new[]{"Products","Patches"}){
      try{
       using(var list=userData.OpenSubKey(sid+"\\"+kind)){
        if(list==null)continue;
        foreach(string code in list.GetSubKeyNames()){
         try{using(var k=list.OpenSubKey(kind=="Products"?code+"\\InstallProperties":code)){if(k==null)continue;products++;string local=Convert.ToString(k.GetValue("LocalPackage",""));if(!String.IsNullOrEmpty(local))set.Add(Environment.ExpandEnvironmentVariables(local).Trim());}}catch(Exception){}
        }
       }
      }catch(Exception){}
     }
    }
   }
   return set;
  }

  /// <summary>Top-level .msi/.msp files in the installer cache that no product or patch references and that are older than the cutoff; subfolders (icons, transforms) and links are never touched.</summary>
  public static List<FileInfo> Orphans(string installerDir,HashSet<string> referenced,DateTime olderThan){
   var list=new List<FileInfo>();if(!Directory.Exists(installerDir))return list;
   foreach(string file in Directory.EnumerateFiles(installerDir)){
    string ext=Path.GetExtension(file);if(!ext.Equals(".msi",StringComparison.OrdinalIgnoreCase)&&!ext.Equals(".msp",StringComparison.OrdinalIgnoreCase))continue;
    FileInfo info;try{info=new FileInfo(file);if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.LastWriteTime>olderThan||info.CreationTime>olderThan)continue;}catch(Exception){continue;}
    if(referenced.Contains(info.FullName))continue;
    list.Add(info);
   }
   return list.OrderByDescending(f=>f.Length).ToList();
  }

  /// <summary>Moves the orphaned packages into one vault backup (Kind=Installer); re-checks the reference set so nothing registered since the measurement is taken.</summary>
  public static Backup MoveOrphans(SpaceItem item,CancellationToken cancel,Action<string> progress=null){
   if(!Windows)throw new IOException("Chỉ chạy được trên Windows.");
   if(item.Id!="installer-orphans"||item.Paths.Count==0)throw new IOException("Không có gói mồ côi nào để chuyển.");
   string installer=Engine.Canon(item.Location);int products;var referenced=ReferencedPackages(out products);
   if(products==0)throw new IOException("Không đọc được danh sách sản phẩm MSI — dừng để tránh xóa nhầm.");
   Engine.NoLinks(Engine.Vault,false);
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Kind=InstallerBackupKind,Purpose=item.Id,AppName=item.Name,Original=installer,Payload="content"};
   string folder=Path.Combine(Engine.Vault,backup.Id);string content=Path.Combine(folder,"content");string indexPath=Path.Combine(folder,"files.xml");
   Directory.CreateDirectory(content);Engine.SaveBackup(backup);
   var moved=new List<Cleaner.JunkMoved>();int index=0;var errors=new List<string>();
   try{
    foreach(string path in item.Paths){
     cancel.ThrowIfCancellationRequested();index++;
     try{
      string p=Engine.Canon(path);
      if(!String.Equals(Path.GetDirectoryName(p),installer,StringComparison.OrdinalIgnoreCase))throw new IOException("Nằm ngoài Windows\\Installer.");
      string ext=Path.GetExtension(p);if(!ext.Equals(".msi",StringComparison.OrdinalIgnoreCase)&&!ext.Equals(".msp",StringComparison.OrdinalIgnoreCase))throw new IOException("Không phải .msi/.msp.");
      if(referenced.Contains(p))throw new IOException("Vừa được một sản phẩm tham chiếu lại.");
      if(!File.Exists(p))continue;Engine.NoLinks(p,false);
      var info=new FileInfo(p);if(info.LastWriteTime>DateTime.Now-InstallerMinAge)throw new IOException("Tệp mới được ghi.");
      if(progress!=null)progress(Path.GetFileName(p)+"  "+Presentation.BytesLabel(info.Length));
      string stored=Cleaner.JunkCleaner.StoredName(index,p);
      File.Move(p,Path.Combine(content,stored));moved.Add(new Cleaner.JunkMoved{Original=p,Stored=stored,Bytes=info.Length});
     }catch(OperationCanceledException){throw;}
     catch(Exception e){errors.Add(Path.GetFileName(path)+": "+e.Message);}
    }
    Engine.Save(indexPath,moved);backup.State=errors.Count==0?"BackedUp":"NeedsReview";backup.Error=errors.Count==0?"":String.Join(" | ",errors.Take(10));Engine.SaveBackup(backup);
   }catch(Exception e){try{Engine.Save(indexPath,moved);}catch(Exception){}backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;}
   if(moved.Count==0){try{Directory.Delete(folder,true);}catch(Exception){}throw new IOException("Không chuyển được gói nào."+(errors.Count>0?" "+errors[0]:""));}
   backup.Bytes=moved.Sum(m=>m.Bytes);return backup;
  }

  /// <summary>Puts cached packages back into Windows\Installer; files whose destination exists again are skipped.</summary>
  public static void RestoreInstaller(Backup b){
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   if(b.Kind!=InstallerBackupKind)throw new IOException("Không phải bản sao lưu gói Installer.");
   string folder=Path.Combine(Engine.VaultOf(b),b.Id);Engine.NoLinks(folder,true);
   string content=Path.Combine(folder,"content");string indexPath=Path.Combine(folder,"files.xml");
   if(!File.Exists(indexPath))throw new IOException("Bản sao lưu thiếu danh mục tệp (files.xml).");
   var moved=Engine.Load<List<Cleaner.JunkMoved>>(indexPath);string installer=Engine.Canon(b.Original);
   int restored=0,skipped=0,failed=0;
   foreach(var entry in moved){
    try{
     string source=Path.Combine(content,entry.Stored);if(!File.Exists(source)){skipped++;continue;}
     string target=Engine.Canon(entry.Original);
     if(!String.Equals(Path.GetDirectoryName(target),installer,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(installer).Equals("Installer",StringComparison.OrdinalIgnoreCase))throw new IOException("Đích không phải Windows\\Installer.");
     if(File.Exists(target)){skipped++;continue;}
     Directory.CreateDirectory(installer);File.Move(source,target);restored++;
    }catch(Exception){failed++;}
   }
   if(failed>0){b.State="NeedsReview";b.Error="Khôi phục "+restored+" tệp; "+failed+" lỗi; "+skipped+" bỏ qua.";Engine.SaveBackup(b);throw new IOException(b.Error);}
   b.State="Restored";b.Error=skipped>0?"Bỏ qua "+skipped+" tệp vì đích đã tồn tại.":"";Engine.SaveBackup(b);
  }

  static IEnumerable<DriveInfo> SafeDrives(){var list=new List<DriveInfo>();try{foreach(var d in DriveInfo.GetDrives()){try{if(d.IsReady&&d.DriveType==DriveType.Fixed)list.Add(d);}catch(Exception){}}}catch(Exception){}return list;}

  /// <summary>Queries the live driver store through a culture-neutral PowerShell projection.</summary>
  public static List<DriverPackage> ListDrivers(){
   var r=Core.PowerShell.Run("Get-WindowsDriver -Online -ErrorAction Stop | ForEach-Object { \"$($_.Driver)|$($_.OriginalFileName)|$($_.ProviderName)|$($_.ClassName)|$($_.Version)|$($_.Date.ToString('yyyy-MM-dd'))|$($_.BootCritical)|$($_.Inbox)\" }",TimeSpan.FromMinutes(2));
   if(r.ExitCode!=0&&r.Output=="")throw new IOException("Get-WindowsDriver lỗi: "+(r.Error.Length>200?r.Error.Substring(0,200):r.Error));
   return ParseDrivers(r.Output);
  }

  /// <summary>Parses the pipe-delimited projection; malformed lines are skipped.</summary>
  public static List<DriverPackage> ParseDrivers(string text){
   var list=new List<DriverPackage>();
   foreach(string raw in (text??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
    var p=raw.Split('|');if(p.Length<8||!p[0].EndsWith(".inf",StringComparison.OrdinalIgnoreCase))continue;
    list.Add(new DriverPackage{Published=p[0].Trim(),OriginalFile=p[1].Trim(),Provider=p[2].Trim(),Class=p[3].Trim(),Version=p[4].Trim(),Date=p[5].Trim(),BootCritical=p[6].Trim().Equals("True",StringComparison.OrdinalIgnoreCase),Inbox=p[7].Trim().Equals("True",StringComparison.OrdinalIgnoreCase)});
   }
   return list;
  }

  /// <summary>Older packages among groups that share INF name, provider and class; inbox and boot-critical packages are never proposed.</summary>
  public static List<DriverPackage> Duplicates(IEnumerable<DriverPackage> drivers){
   var older=new List<DriverPackage>();
   foreach(var g in drivers.Where(d=>!d.Inbox&&d.Inf!="").GroupBy(d=>d.Inf+"|"+(d.Provider??"").ToLowerInvariant()+"|"+(d.Class??"").ToLowerInvariant())){
    var ordered=g.OrderByDescending(d=>ParseVersion(d.Version)).ThenByDescending(d=>d.Date,StringComparer.Ordinal).ToList();
    if(ordered.Count<2)continue;
    older.AddRange(ordered.Skip(1).Where(d=>!d.BootCritical));
   }
   return older.OrderBy(d=>d.Inf).ThenBy(d=>d.Version).ToList();
  }

  public static Version ParseVersion(string text){
   Version v;var parts=(text??"0").Trim().Split('.');
   while(parts.Length<2)parts=parts.Concat(new[]{"0"}).ToArray();
   return Version.TryParse(String.Join(".",parts.Take(4).Select(s=>{int n;return int.TryParse(s,out n)?n.ToString(CultureInfo.InvariantCulture):"0";})),out v)?v:new Version(0,0);
  }

  /// <summary>The exact tool invocations an action would run, without running them; drivers only matter for the driver-store item.</summary>
  public static List<KeyValuePair<string,string>> Commands(SpaceItem item,IEnumerable<DriverPackage> older=null){
   string sys=Environment.GetFolderPath(Environment.SpecialFolder.System);if(String.IsNullOrEmpty(sys))sys=@"C:\Windows\System32";
   var list=new List<KeyValuePair<string,string>>();
   switch(item.Id){
    case "windows-old":list.Add(new KeyValuePair<string,string>(Path.Combine(sys,"cleanmgr.exe"),"/sagerun:"+SageSet));break;
    case "winsxs":list.Add(new KeyValuePair<string,string>(Path.Combine(sys,"Dism.exe"),"/Online /Cleanup-Image /StartComponentCleanup"));break;
    case "delivery-optimization":list.Add(new KeyValuePair<string,string>("powershell","Delete-DeliveryOptimizationCache -Force"));break;
    case "driver-store":foreach(var d in older??new List<DriverPackage>()){if(!System.Text.RegularExpressions.Regex.IsMatch(d.Published??"",@"^oem\d+\.inf$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))continue;list.Add(new KeyValuePair<string,string>(Path.Combine(sys,"pnputil.exe"),"/delete-driver "+d.Published));}break;
    case "hiberfil":list.Add(new KeyValuePair<string,string>(Path.Combine(sys,"powercfg.exe"),"/hibernate off"));break;
    case "recycle-bin":list.Add(new KeyValuePair<string,string>("powershell","Clear-RecycleBin -Force -ErrorAction SilentlyContinue"));break;
    case "pagefile":list.Add(new KeyValuePair<string,string>(Path.Combine(sys,"SystemPropertiesPerformance.exe"),""));break;
    case "restore-points":list.Add(new KeyValuePair<string,string>(Path.Combine(sys,"SystemPropertiesProtection.exe"),""));break;
   }
   return list;
  }

  /// <summary>Runs the item's Windows tool(s) and returns every captured result. Requires elevation; the caller has already confirmed.</summary>
  public static List<ToolResult> Run(SpaceItem item,IEnumerable<DriverPackage> older,CancellationToken cancel,Action<string> progress=null){
   if(!Windows)throw new IOException("Chỉ chạy được trên Windows.");
   if(!Core.Elevation.IsElevated)throw new IOException("Cần quyền quản trị để chạy công cụ hệ thống.");
   var results=new List<ToolResult>();
   if(item.Info){var cmd=Commands(item)[0];Process.Start(new ProcessStartInfo(cmd.Key,cmd.Value){UseShellExecute=true});return results;}
   if(item.Vaulted)throw new IOException("Mục này đi qua Kho khôi phục; dùng MoveOrphans.");
   if(item.Id=="windows-old")SetSageFlags(2);
   try{
    foreach(var cmd in Commands(item,older)){
     cancel.ThrowIfCancellationRequested();
     string label=Path.GetFileName(cmd.Key)+" "+cmd.Value;if(progress!=null)progress(label);
     results.Add(Execute(cmd.Key,cmd.Value,item.Id=="winsxs"?TimeSpan.FromMinutes(90):TimeSpan.FromMinutes(30),progress==null?(Action<string>)null:line=>progress(label+"  |  "+line)));
    }
   }finally{if(item.Id=="windows-old")try{SetSageFlags(0);}catch(Exception){}}
   return results;
  }

  /// <summary>Marks only "Previous Installations" (and its upgrade logs) for the Tweek Pro sageset so cleanmgr touches nothing else.</summary>
  static void SetSageFlags(int value){
   using(var root=Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,Microsoft.Win32.RegistryView.Registry64))using(var caches=root.OpenSubKey(VolumeCaches)){
    if(caches==null)throw new IOException("Không tìm thấy VolumeCaches của Disk Cleanup.");
    foreach(string name in caches.GetSubKeyNames()){
     bool target=name.Equals("Previous Installations",StringComparison.OrdinalIgnoreCase)||name.Equals("Windows Upgrade Log Files",StringComparison.OrdinalIgnoreCase);
     using(var k=root.OpenSubKey(VolumeCaches+"\\"+name,true)){if(k==null)continue;if(target)k.SetValue("StateFlags"+SageSet.ToString("D4"),value,Microsoft.Win32.RegistryValueKind.DWord);else if(k.GetValueNames().Contains("StateFlags"+SageSet.ToString("D4")))k.SetValue("StateFlags"+SageSet.ToString("D4"),0,Microsoft.Win32.RegistryValueKind.DWord);}
    }
   }
  }

  /// <summary>Runs a tool capturing stdout/stderr; every completed line (or "\r" progress segment, as DISM prints) is also handed to the live callback.</summary>
  static ToolResult Execute(string exe,string args,TimeSpan timeout,Action<string> line){
   if(Runner!=null)return Runner(exe,args,timeout);
   var watch=Stopwatch.StartNew();
   if(exe=="powershell"){var r=Core.PowerShell.Run(args,timeout);return new ToolResult{Exe=exe,Args=args,Output=r.Output,Error=r.Error,ExitCode=r.ExitCode,Elapsed=watch.Elapsed};}
   var enc=Encoding.Default;try{enc=Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);}catch(Exception){}
   var info=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=enc,StandardErrorEncoding=enc};
   var result=new ToolResult{Exe=exe,Args=args};
   using(var p=Process.Start(info)){
    var error=p.StandardError.ReadToEndAsync();
    var output=System.Threading.Tasks.Task.Run(()=>ReadLines(p.StandardOutput,line));
    if(!p.WaitForExit((int)timeout.TotalMilliseconds)){try{p.Kill();}catch(Exception){}throw new IOException(Path.GetFileName(exe)+" không phản hồi trong thời gian cho phép.");}
    result.Output=output.Result.Trim();result.Error=error.Result.Trim();result.ExitCode=p.ExitCode;
   }
   result.Elapsed=watch.Elapsed;return result;
  }

  /// <summary>Reads a stream to the end, splitting on CR or LF so carriage-return progress bars surface as separate lines.</summary>
  public static string ReadLines(TextReader reader,Action<string> line){
   var all=new StringBuilder();var current=new StringBuilder();var buffer=new char[512];int n;
   while((n=reader.Read(buffer,0,buffer.Length))>0){
    for(int i=0;i<n;i++){
     char c=buffer[i];
     if(c=='\r'||c=='\n'){if(current.Length>0){string text=current.ToString().Trim();if(text!=""){all.AppendLine(text);if(line!=null)try{line(text);}catch(Exception){}}current.Length=0;}}
     else current.Append(c);
    }
   }
   if(current.Length>0){string text=current.ToString().Trim();if(text!=""){all.AppendLine(text);if(line!=null)try{line(text);}catch(Exception){}}}
   return all.ToString();
  }

  /// <summary>Short, log-friendly tail of a tool's output (DISM progress bars collapse to their last line).</summary>
  public static string Tail(ToolResult r,int lines){
   var all=(r.Output+"\n"+r.Error).Replace("\r","\n").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Select(l=>l.Trim()).Where(l=>l!="").ToList();
   return String.Join("\r\n",all.Skip(Math.Max(0,all.Count-lines)));
  }
 }
}
