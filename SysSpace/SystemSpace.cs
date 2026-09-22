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
  public long Bytes=-1; public bool Partial, Available; public List<string> Details=new List<string>(); public string Note="";
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
  const string VolumeCaches=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches";
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
    new SpaceItem{Id="hiberfil",Group="Nguồn điện",Name="hiberfil.sys — tệp ngủ đông",Description="Ảnh RAM cho Hibernate và Fast Startup, thường 40–75% dung lượng RAM.",Location=Path.Combine(root,"hiberfil.sys"),ToolLabel="powercfg /hibernate off",ToolCommand="powercfg.exe /hibernate off",Warning="Tắt luôn Hibernate và Fast Startup; bật lại bằng powercfg /hibernate on.",Reversible=true},
    new SpaceItem{Id="pagefile",Group="Nguồn điện",Name="pagefile.sys + swapfile.sys",Description="Bộ nhớ ảo do Windows quản lý. Chỉ hiển thị; đổi kích thước trong System Properties → Performance.",Location=Path.Combine(root,"pagefile.sys"),ToolLabel="Mở System Properties → Performance",ToolCommand="SystemPropertiesPerformance.exe",Info=true},
    new SpaceItem{Id="restore-points",Group="Sao lưu",Name="Điểm khôi phục hệ thống (Shadow Copies)",Description="Dung lượng System Restore / Volume Shadow Copy đang dùng trên các ổ. Chỉ hiển thị; xóa điểm cũ trong System Protection.",Location="Win32_ShadowStorage",ToolLabel="Mở System Protection",ToolCommand="SystemPropertiesProtection.exe",Info=true},
    new SpaceItem{Id="recycle-bin",Group="Sao lưu",Name="Thùng rác (mọi ổ đĩa)",Description="Tệp đã xóa còn nằm trong $Recycle.Bin của tất cả ổ cố định.",Location=Path.Combine(root,"$Recycle.Bin"),ToolLabel="Clear-RecycleBin",ToolCommand="powershell Clear-RecycleBin -Force",Warning="Tệp trong thùng rác bị xóa hẳn, không qua Kho khôi phục."}
   };
  }

  /// <summary>Fills Bytes/Available for one item using folder walks, file sizes or a culture-neutral PowerShell query; never throws.</summary>
  public static void Measure(SpaceItem item,TimeSpan budget,List<DriverPackage> drivers=null){
   item.Details.Clear();item.Note="";item.Partial=false;
   try{
    switch(item.Id){
     case "hiberfil":case "pagefile":{
      long total=0;bool any=false;
      foreach(string f in item.Id=="pagefile"?new[]{item.Location,Path.Combine(Path.GetDirectoryName(item.Location),"swapfile.sys")}:new[]{item.Location}){
       try{if(File.Exists(f)){var info=new FileInfo(f);total+=info.Length;any=true;item.Details.Add(f+"  "+Presentation.BytesLabel(info.Length));}}catch(Exception){}
      }
      item.Available=any;item.Bytes=any?total:-1;if(!any&&item.Id=="hiberfil")item.Note="Hibernate đang tắt.";
      return;
     }
     case "recycle-bin":{
      long total=0;bool any=false;
      foreach(var drive in SafeDrives()){string bin=Path.Combine(drive.RootDirectory.FullName,"$Recycle.Bin");if(!Directory.Exists(bin))continue;bool p;long b=FolderSize.Measure(bin,budget,out p);if(b<0)continue;any=true;total+=b;item.Partial|=p;item.Details.Add(bin+"  "+Presentation.BytesLabel(b));}
      item.Available=any;item.Bytes=any?total:-1;return;
     }
     case "restore-points":{
      if(!Windows||Core.PowerShell.Executable==null){item.Available=false;item.Note="Chỉ đọc được trên Windows.";return;}
      var r=Core.PowerShell.Run("Get-CimInstance Win32_ShadowStorage -ErrorAction SilentlyContinue | ForEach-Object { \"$($_.Volume.DeviceID)|$($_.UsedSpace)|$($_.MaxSpace)\" }",TimeSpan.FromSeconds(40));
      long total=0;foreach(string line in r.Output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){var parts=line.Split('|');long used;if(parts.Length>=2&&long.TryParse(parts[1],out used)){total+=used;item.Details.Add(parts[0]+"  "+Presentation.BytesLabel(used));}}
      item.Available=item.Details.Count>0;item.Bytes=item.Available?total:0;return;
     }
     case "driver-store":{
      if(drivers==null){if(!Windows||Core.PowerShell.Executable==null){item.Available=false;item.Note="Chỉ đọc được trên Windows.";return;}drivers=ListDrivers();}
      var older=Duplicates(drivers);long total=0;bool measuredAll=true;
      foreach(var d in older){string folder=d.Folder;if(folder!=""&&Directory.Exists(folder)){bool p;d.Bytes=FolderSize.Measure(folder,TimeSpan.FromSeconds(3),out p);if(d.Bytes>0)total+=d.Bytes;else measuredAll=false;}else measuredAll=false;item.Details.Add(d.Published+"  "+d.Inf+"  "+d.Provider+"  v"+d.Version+" ("+d.Date+")"+(d.Bytes>0?"  "+Presentation.BytesLabel(d.Bytes):""));}
      item.Available=older.Count>0;item.Bytes=total;item.Partial=!measuredAll&&older.Count>0;item.Note=older.Count==0?"Không có gói trùng lặp; "+drivers.Count+" gói đã kiểm tra.":older.Count+" bản cũ trong "+drivers.Count+" gói.";
      return;
     }
     default:{
      bool exists;try{exists=Directory.Exists(item.Location);}catch(Exception){exists=false;}
      item.Available=exists;if(!exists){item.Bytes=-1;item.Note=item.Id=="windows-old"?"Không có Windows.old.":"Thư mục không tồn tại.";return;}
      bool p;item.Bytes=FolderSize.Measure(item.Location,budget,out p);item.Partial=p;
      if(item.Bytes<0){item.Available=false;item.Note="Không đủ quyền đọc.";}
      return;
     }
    }
   }catch(Exception e){item.Available=false;item.Bytes=-1;item.Note=e.Message;}
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
   if(item.Id=="windows-old")SetSageFlags(2);
   try{
    foreach(var cmd in Commands(item,older)){
     cancel.ThrowIfCancellationRequested();
     if(progress!=null)progress(Path.GetFileName(cmd.Key)+" "+cmd.Value);
     results.Add(Execute(cmd.Key,cmd.Value,item.Id=="winsxs"?TimeSpan.FromMinutes(90):TimeSpan.FromMinutes(30)));
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

  static ToolResult Execute(string exe,string args,TimeSpan timeout){
   if(Runner!=null)return Runner(exe,args,timeout);
   var watch=Stopwatch.StartNew();
   if(exe=="powershell"){var r=Core.PowerShell.Run(args,timeout);return new ToolResult{Exe=exe,Args=args,Output=r.Output,Error=r.Error,ExitCode=r.ExitCode,Elapsed=watch.Elapsed};}
   var enc=Encoding.Default;try{enc=Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);}catch(Exception){}
   var info=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=enc,StandardErrorEncoding=enc};
   var result=new ToolResult{Exe=exe,Args=args};
   using(var p=Process.Start(info)){
    var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
    if(!p.WaitForExit((int)timeout.TotalMilliseconds)){try{p.Kill();}catch(Exception){}throw new IOException(Path.GetFileName(exe)+" không phản hồi trong thời gian cho phép.");}
    result.Output=output.Result.Trim();result.Error=error.Result.Trim();result.ExitCode=p.ExitCode;
   }
   result.Elapsed=watch.Elapsed;return result;
  }

  /// <summary>Short, log-friendly tail of a tool's output (DISM progress bars collapse to their last line).</summary>
  public static string Tail(ToolResult r,int lines){
   var all=(r.Output+"\n"+r.Error).Replace("\r","\n").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Select(l=>l.Trim()).Where(l=>l!="").ToList();
   return String.Join("\r\n",all.Skip(Math.Max(0,all.Count-lines)));
  }
 }
}
