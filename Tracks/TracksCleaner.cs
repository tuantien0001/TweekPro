using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using TweekPro.Cleaner;

namespace TweekPro.Tracks {
 /// <summary>Where a track lives: files under a fixed folder, or values/subkeys under a fixed HKCU key.</summary>
 public enum TrackSource { Files, Registry }

 /// <summary>One privacy trace the cleaner knows how to list and remove; paths may use environment variables and one "*" folder/key segment.</summary>
 public class TrackRule {
  public string Id, Name, Group, Description; public TrackSource Source;
  public List<string> Paths=new List<string>(); public List<string> Patterns=new List<string>(); public List<string> BlockedProcesses=new List<string>();
  public bool DefaultChecked=true; public bool Sensitive;
 }

 /// <summary>One file or registry value proposed for removal.</summary>
 public class TrackItem { public string Path, Detail; public long Bytes; }

 /// <summary>Preview outcome for one rule.</summary>
 public class TrackResult {
  public TrackRule Rule; public List<TrackItem> Items=new List<TrackItem>(); public List<string> Roots=new List<string>();
  public long Bytes; public bool Locked; public string LockReason="", Note="";
  public int Count { get { return Items.Count; } }
 }

 /// <summary>Result of a clean run across the selected rules.</summary>
 public class TrackReport { public int Cleaned, SkippedInUse, Failed; public long Bytes; public List<Backup> Backups=new List<Backup>(); public List<string> Errors=new List<string>(); }

 /// <summary>
 /// Code-level boundary: file rules may only touch the exact folders named by the catalog (Recent, Jump Lists, browser profile
 /// history files, Activity cache); registry rules may only touch the listed HKCU MRU keys. Anything else is refused.
 /// </summary>
 public static class TracksSafety {
  public const string Explorer=@"Software\Microsoft\Windows\CurrentVersion\Explorer";
  public static readonly string[] AllowedKeys={
   Explorer+@"\RunMRU",Explorer+@"\TypedPaths",Explorer+@"\RecentDocs",Explorer+@"\ComDlg32\OpenSavePidlMRU",Explorer+@"\ComDlg32\LastVisitedPidlMRU",Explorer+@"\WordWheelQuery",Explorer+@"\UserAssist",
   @"Software\Microsoft\Windows\CurrentVersion\Applets\Paint\Recent File List",@"Software\Microsoft\Windows\CurrentVersion\Applets\Wordpad\Recent File List",@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit",
   @"Software\Microsoft\Windows\CurrentVersion\Search\RecentApps",@"Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppSwitched",
   @"Software\TweekProTest\Tracks"};
  static readonly string[] NeverDelete={"desktop.ini","thumbs.db"};

  /// <summary>A registry key is allowed when it equals or lies under one of the catalog keys (HKCU only).</summary>
  public static void ValidateKey(string hive,string key){
   if(hive!="HKCU")throw new IOException("Dấu vết chỉ được dọn trong HKCU.");
   string k=(key??"").Trim('\\');
   if(k==""||k.Contains("..")||!AllowedKeys.Any(a=>String.Equals(k,a,StringComparison.OrdinalIgnoreCase)||k.StartsWith(a+"\\",StringComparison.OrdinalIgnoreCase)))throw new IOException("Khóa Registry nằm ngoài danh sách dấu vết cho phép: "+key);
  }

  /// <summary>A file may be removed only when it sits directly under one of the rule's expanded roots, matches its pattern, and is not a link or a folder-config file.</summary>
  public static void ValidateFile(string path,IList<string> roots,IList<string> patterns){
   string p=Engine.Canon(path);
   if(!roots.Any(r=>Engine.Under(p,r)))throw new IOException("Tệp nằm ngoài thư mục của quy tắc.");
   string leaf=Path.GetFileName(p);
   if(NeverDelete.Contains(leaf,StringComparer.OrdinalIgnoreCase))throw new IOException("Không xóa tệp cấu hình thư mục.");
   if(!patterns.Any(g=>JunkRules.GlobToRegex(g).IsMatch(leaf)))throw new IOException("Tệp không khớp mẫu của quy tắc.");
   Engine.NoLinks(p,false);
  }

  /// <summary>Folder roots a file rule may expand to; they must be inside the current user's profile and never a profile/AppData root itself.</summary>
  public static void ValidateRoot(string folder,string profile){
   string p=Engine.Canon(folder);
   if(String.IsNullOrWhiteSpace(profile)||!Engine.Under(p,Engine.Canon(profile)))throw new IOException("Thư mục dấu vết phải nằm trong hồ sơ người dùng hiện tại: "+folder);
   string rel=p.Substring(Engine.Canon(profile).Length).Trim('\\');
   if(rel.Split('\\').Length<3)throw new IOException("Thư mục quá rộng để coi là dấu vết: "+folder);
  }
 }

 /// <summary>Catalog, preview, clean (vault only) and restore for Windows and browser usage traces.</summary>
 public static class TracksCleaner {
  public const string BackupKind="Tracks";
  public const int MaxItemsPerRule=5000;
  static readonly string[] Chromium={@"%LOCALAPPDATA%\Google\Chrome\User Data\*",@"%LOCALAPPDATA%\Microsoft\Edge\User Data\*",@"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data\*",@"%LOCALAPPDATA%\Vivaldi\User Data\*",@"%LOCALAPPDATA%\Chromium\User Data\*"};
  static readonly string[] ChromiumProcesses={"chrome","msedge","brave","vivaldi","chromium"};

  /// <summary>Built-in rules; Sensitive rules (cookies) start unchecked because they sign the user out of websites.</summary>
  public static List<TrackRule> Catalog(){
   return new List<TrackRule>{
    new TrackRule{Id="recent-items",Name="Tệp mở gần đây (Recent)",Group="Windows",Description="Shortcut tới tệp và thư mục vừa mở; Explorer và hộp Mở/Lưu dùng để gợi ý.",Source=TrackSource.Files,Paths={@"%APPDATA%\Microsoft\Windows\Recent"},Patterns={"*.lnk"}},
    new TrackRule{Id="jump-lists",Name="Jump List trên thanh tác vụ",Group="Windows",Description="Danh sách «gần đây / thường dùng» khi chuột phải biểu tượng trên taskbar và Start.",Source=TrackSource.Files,Paths={@"%APPDATA%\Microsoft\Windows\Recent\AutomaticDestinations",@"%APPDATA%\Microsoft\Windows\Recent\CustomDestinations"},Patterns={"*.automaticDestinations-ms","*.customDestinations-ms"}},
    new TrackRule{Id="activity-history",Name="Lịch sử hoạt động (Timeline)",Group="Windows",Description="Cơ sở dữ liệu ActivitiesCache.db của Timeline / Đồng bộ hoạt động; tệp đang được dịch vụ giữ sẽ bị bỏ qua.",Source=TrackSource.Files,Paths={@"%LOCALAPPDATA%\ConnectedDevicesPlatform\*"},Patterns={"ActivitiesCache.db","ActivitiesCache.db-shm","ActivitiesCache.db-wal"}},
    new TrackRule{Id="run-mru",Name="Lịch sử hộp Run (Win+R)",Group="Explorer",Description="Các lệnh đã gõ vào hộp Run.",Source=TrackSource.Registry,Paths={TracksSafety.Explorer+@"\RunMRU"}},
    new TrackRule{Id="typed-paths",Name="Đường dẫn đã gõ vào thanh địa chỉ",Group="Explorer",Description="Đường dẫn gõ vào thanh địa chỉ Explorer.",Source=TrackSource.Registry,Paths={TracksSafety.Explorer+@"\TypedPaths"}},
    new TrackRule{Id="recent-docs",Name="RecentDocs (tài liệu theo đuôi tệp)",Group="Explorer",Description="Tên tệp vừa mở, xếp theo phần mở rộng; nguồn của menu «Recent» trong Start.",Source=TrackSource.Registry,Paths={TracksSafety.Explorer+@"\RecentDocs"}},
    new TrackRule{Id="open-save-mru",Name="Hộp Mở / Lưu (ComDlg32)",Group="Explorer",Description="Tệp và thư mục dùng gần đây trong hộp thoại Mở/Lưu của mọi ứng dụng.",Source=TrackSource.Registry,Paths={TracksSafety.Explorer+@"\ComDlg32\OpenSavePidlMRU",TracksSafety.Explorer+@"\ComDlg32\LastVisitedPidlMRU"}},
    new TrackRule{Id="search-history",Name="Từ khóa tìm trong Explorer",Group="Explorer",Description="Chuỗi đã gõ vào ô tìm kiếm của Explorer (WordWheelQuery).",Source=TrackSource.Registry,Paths={TracksSafety.Explorer+@"\WordWheelQuery"}},
    new TrackRule{Id="user-assist",Name="Thống kê chạy chương trình (UserAssist)",Group="Explorer",Description="Số lần và lần cuối chạy từng chương trình từ Start; Start dùng để xếp «Most used».",Source=TrackSource.Registry,Paths={TracksSafety.Explorer+@"\UserAssist\*\Count"},DefaultChecked=false},
    new TrackRule{Id="applet-mru",Name="Tệp gần đây của Paint, WordPad, Regedit",Group="Ứng dụng",Description="Danh sách tệp gần đây của Paint/WordPad và khóa mở lần cuối của Regedit.",Source=TrackSource.Registry,Paths={@"Software\Microsoft\Windows\CurrentVersion\Applets\Paint\Recent File List",@"Software\Microsoft\Windows\CurrentVersion\Applets\Wordpad\Recent File List",@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"}},
    new TrackRule{Id="chromium-history",Name="Chrome / Edge / Brave — lịch sử duyệt web",Group="Trình duyệt",Description="History, Visited Links, Top Sites, Shortcuts của từng hồ sơ. Không đụng mật khẩu, dấu trang, tiện ích. Khóa khi trình duyệt còn chạy.",Source=TrackSource.Files,Paths=Chromium.ToList(),Patterns={"History","History-journal","Visited Links","Top Sites","Top Sites-journal","Shortcuts","Shortcuts-journal","Media History","Media History-journal"},BlockedProcesses=ChromiumProcesses.ToList()},
    new TrackRule{Id="chromium-cookies",Name="Chrome / Edge / Brave — cookie (đăng xuất mọi web)",Group="Trình duyệt",Description="Xóa cookie đăng nhập của mọi trang web; bạn sẽ phải đăng nhập lại. Mặc định không chọn.",Source=TrackSource.Files,Paths=Chromium.Select(p=>p+@"\Network").ToList(),Patterns={"Cookies","Cookies-journal"},BlockedProcesses=ChromiumProcesses.ToList(),DefaultChecked=false,Sensitive=true},
    new TrackRule{Id="firefox-forms",Name="Firefox — lịch sử biểu mẫu và phiên",Group="Trình duyệt",Description="formhistory.sqlite (chuỗi đã gõ vào ô nhập) và sessionstore-backups. Không đụng places.sqlite (lịch sử + dấu trang), mật khẩu, cookie.",Source=TrackSource.Files,Paths={@"%APPDATA%\Mozilla\Firefox\Profiles\*",@"%APPDATA%\Mozilla\Firefox\Profiles\*\sessionstore-backups"},Patterns={"formhistory.sqlite","recovery.jsonlz4","recovery.baklz4","previous.jsonlz4","upgrade.jsonlz4-*"},BlockedProcesses={"firefox"}}
   };
  }

  /// <summary>Expands environment variables and a single "*" folder segment into existing folders.</summary>
  public static List<string> ExpandFolders(string pattern){
   string expanded=Environment.ExpandEnvironmentVariables(pattern??"");if(expanded.IndexOf('%')>=0||expanded=="")return new List<string>();
   int star=expanded.IndexOf('*');
   if(star<0)return Directory.Exists(expanded)?new List<string>{Engine.Canon(expanded)}:new List<string>();
   int cut=expanded.LastIndexOf('\\',star);string parent=expanded.Substring(0,cut);string rest=expanded.Substring(star+1).TrimStart('\\');
   if(rest.IndexOf('*')>=0)throw new IOException("Chỉ hỗ trợ một dấu * trong đường dẫn: "+pattern);
   var list=new List<string>();if(!Directory.Exists(parent))return list;
   try{foreach(string dir in Directory.EnumerateDirectories(parent)){string leaf=Path.GetFileName(dir);if(leaf.StartsWith("System Profile",StringComparison.OrdinalIgnoreCase)||leaf.Equals("Guest Profile",StringComparison.OrdinalIgnoreCase))continue;string full=rest==""?dir:Path.Combine(dir,rest);try{if(Directory.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)==0)list.Add(Engine.Canon(full));}catch(IOException){}}}catch(UnauthorizedAccessException){}
   return list;
  }

  /// <summary>Expands a registry path whose single "*" segment stands for every subkey.</summary>
  public static List<string> ExpandKeys(RegistryKey hive,string pattern){
   int star=pattern.IndexOf('*');
   if(star<0)return new List<string>{pattern};
   string parent=pattern.Substring(0,star).TrimEnd('\\');string rest=pattern.Substring(star+1).TrimStart('\\');
   var list=new List<string>();
   using(var key=hive.OpenSubKey(parent)){if(key==null)return list;foreach(string sub in key.GetSubKeyNames())list.Add(rest==""?parent+"\\"+sub:parent+"\\"+sub+"\\"+rest);}
   return list;
  }

  public static List<string> RunningBlockers(TrackRule rule){
   var running=new List<string>();
   foreach(string name in rule.BlockedProcesses){try{var ps=Process.GetProcessesByName(name);if(ps.Length>0)running.Add(name);foreach(var p in ps)p.Dispose();}catch(Exception){}}
   return running;
  }

  static bool Windows { get { return Environment.OSVersion.Platform==PlatformID.Win32NT; } }

  /// <summary>Lists what each rule would remove without changing anything.</summary>
  public static List<TrackResult> Preview(IEnumerable<TrackRule> rules,CancellationToken cancel){return Preview(rules,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),cancel);}

  public static List<TrackResult> Preview(IEnumerable<TrackRule> rules,string profile,CancellationToken cancel){
   var results=new List<TrackResult>();
   foreach(var rule in rules){
    cancel.ThrowIfCancellationRequested();
    var result=new TrackResult{Rule=rule};
    var blockers=RunningBlockers(rule);if(blockers.Count>0){result.Locked=true;result.LockReason="Đang chạy: "+String.Join(", ",blockers)+" — đóng trình duyệt rồi xem trước lại.";}
    try{
     if(rule.Source==TrackSource.Files)PreviewFiles(rule,result,profile,cancel);
     else if(Windows)PreviewRegistry(rule,result,cancel);
     else result.Note="Chỉ đọc được trên Windows.";
    }catch(OperationCanceledException){throw;}
    catch(Exception e){result.Note=e.Message;}
    results.Add(result);
   }
   return results;
  }

  static void PreviewFiles(TrackRule rule,TrackResult result,string profile,CancellationToken cancel){
   foreach(string pattern in rule.Paths)foreach(string root in ExpandFolders(pattern)){
    try{TracksSafety.ValidateRoot(root,profile);}catch(IOException e){result.Note=e.Message;continue;}
    result.Roots.Add(root);
    try{
     foreach(string file in Directory.EnumerateFiles(root)){
      cancel.ThrowIfCancellationRequested();if(result.Items.Count>=MaxItemsPerRule){result.Note="Đã chạm giới hạn "+MaxItemsPerRule+" mục.";break;}
      string leaf=Path.GetFileName(file);if(!rule.Patterns.Any(g=>JunkRules.GlobToRegex(g).IsMatch(leaf)))continue;
      FileInfo info;try{info=new FileInfo(file);if((info.Attributes&FileAttributes.ReparsePoint)!=0)continue;}catch(Exception){continue;}
      long bytes;try{bytes=info.Length;}catch(IOException){continue;}
      result.Items.Add(new TrackItem{Path=info.FullName,Bytes=bytes,Detail=info.LastWriteTime.ToString("dd/MM/yyyy HH:mm")});result.Bytes+=bytes;
     }
    }catch(UnauthorizedAccessException){result.Note="Không đủ quyền đọc: "+root;}catch(IOException){result.Note="Không đọc được: "+root;}
   }
  }

  static void PreviewRegistry(TrackRule rule,TrackResult result,CancellationToken cancel){
   using(var hive=Engine.Base("HKCU","64"))foreach(string pattern in rule.Paths)foreach(string path in ExpandKeys(hive,pattern)){
    TracksSafety.ValidateKey("HKCU",path);
    using(var key=hive.OpenSubKey(path)){
     if(key==null)continue;result.Roots.Add("HKCU\\"+path);
     CountValues(key,path,result,cancel,0);
    }
   }
  }

  static void CountValues(RegistryKey key,string path,TrackResult result,CancellationToken cancel,int depth){
   cancel.ThrowIfCancellationRequested();
   foreach(string name in key.GetValueNames()){
    if(result.Items.Count>=MaxItemsPerRule)return;
    if(name=="MRUList"||name=="MRUListEx")continue;
    string detail="";try{var kind=key.GetValueKind(name);object v=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);detail=kind==RegistryValueKind.String||kind==RegistryValueKind.ExpandString?Convert.ToString(v):kind==RegistryValueKind.MultiString?String.Join(" | ",(string[])v):kind.ToString();}catch(Exception){}
    result.Items.Add(new TrackItem{Path="HKCU\\"+path+" :: "+(name==""?"(Default)":name),Detail=detail.Length>160?detail.Substring(0,160)+"…":detail});
   }
   if(depth>=4)return;
   foreach(string sub in key.GetSubKeyNames()){if(result.Items.Count>=MaxItemsPerRule)return;try{using(var child=key.OpenSubKey(sub))if(child!=null)CountValues(child,path+"\\"+sub,result,cancel,depth+1);}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}}
  }

  /// <summary>Removes the previewed items of each selected rule, one vault backup per rule (files moved, registry tree saved then emptied).</summary>
  public static TrackReport Clean(IEnumerable<TrackResult> selected,CancellationToken cancel){return Clean(selected,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),cancel);}

  public static TrackReport Clean(IEnumerable<TrackResult> selected,string profile,CancellationToken cancel){
   var report=new TrackReport();
   foreach(var result in selected){
    cancel.ThrowIfCancellationRequested();
    if(result.Locked)throw new IOException(result.Rule.Name+": "+result.LockReason);
    if(result.Items.Count==0)continue;
    var blockers=RunningBlockers(result.Rule);if(blockers.Count>0)throw new IOException(result.Rule.Name+": "+String.Join(", ",blockers)+" vừa được khởi động; bỏ qua nhóm này.");
    if(result.Rule.Source==TrackSource.Files)CleanFiles(result,profile,report,cancel);else CleanRegistry(result,report);
   }
   return report;
  }

  static void CleanFiles(TrackResult result,string profile,TrackReport report,CancellationToken cancel){
   foreach(string root in result.Roots)TracksSafety.ValidateRoot(root,profile);
   Engine.NoLinks(Engine.Vault,false);
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Kind=BackupKind,Purpose=result.Rule.Id,AppName=result.Rule.Name,Original=String.Join(" | ",result.Roots),Payload="content"};
   string folder=Path.Combine(Engine.Vault,backup.Id);string content=Path.Combine(folder,"content");string indexPath=Path.Combine(folder,"files.xml");
   Directory.CreateDirectory(content);Engine.SaveBackup(backup);
   var moved=new List<JunkMoved>();int index=0;
   try{
    foreach(var item in result.Items){
     cancel.ThrowIfCancellationRequested();index++;
     try{
      TracksSafety.ValidateFile(item.Path,result.Roots,result.Rule.Patterns);
      if(!File.Exists(item.Path))continue;
      if(!JunkCleaner.CanTake(item.Path)){report.SkippedInUse++;continue;}
      string stored=JunkCleaner.StoredName(index,item.Path);
      File.Move(item.Path,Path.Combine(content,stored));
      moved.Add(new JunkMoved{Original=item.Path,Stored=stored,Bytes=item.Bytes});report.Cleaned++;report.Bytes+=item.Bytes;
     }catch(OperationCanceledException){throw;}
     catch(Exception e){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(item.Path+": "+e.Message);}
    }
    Engine.Save(indexPath,moved);backup.State="BackedUp";Engine.SaveBackup(backup);report.Backups.Add(backup);
   }catch(Exception e){try{Engine.Save(indexPath,moved);}catch(Exception){}backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;}
   if(moved.Count==0){try{Directory.Delete(folder,true);report.Backups.Remove(backup);}catch(Exception){}}
  }

  static void CleanRegistry(TrackResult result,TrackReport report){
   if(!Windows)throw new IOException("Chỉ dọn Registry trên Windows.");
   using(var hive=Engine.Base("HKCU","64"))foreach(string root in result.Roots){
    string path=root.StartsWith("HKCU\\")?root.Substring(5):root;TracksSafety.ValidateKey("HKCU",path);
    RegNode node;using(var key=hive.OpenSubKey(path)){if(key==null)continue;node=Engine.ReadTree(key);}
    int values=CountTree(node);if(values==0)continue;
    Engine.NoLinks(Engine.Vault,false);
    var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Kind=BackupKind,Purpose=result.Rule.Id,AppName=result.Rule.Name,Original=path,Hive="HKCU",View="64",Payload="registry.xml"};
    string folder=Path.Combine(Engine.Vault,backup.Id);Directory.CreateDirectory(folder);Engine.SaveBackup(backup);
    try{
     string payload=Path.Combine(folder,"registry.xml");Engine.Save(payload,node);Engine.Load<RegNode>(payload);
     using(var key=hive.OpenSubKey(path,true)){
      if(key==null)throw new IOException("Không mở được khóa để ghi.");
      foreach(string sub in key.GetSubKeyNames())key.DeleteSubKeyTree(sub,false);
      foreach(string name in key.GetValueNames())key.DeleteValue(name,false);
     }
     backup.State="BackedUp";Engine.SaveBackup(backup);report.Backups.Add(backup);report.Cleaned+=values;
    }catch(Exception e){backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);report.Failed++;if(report.Errors.Count<50)report.Errors.Add(root+": "+e.Message);}
   }
  }

  static int CountTree(RegNode node){return node.Values.Count+node.Children.Sum(c=>CountTree(c.Node));}

  /// <summary>Puts files back (skipping destinations that exist) or merges saved registry values back without overwriting newer ones.</summary>
  public static void Restore(Backup b){
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   if(b.Kind!=BackupKind)throw new IOException("Không phải bản sao lưu dấu vết.");
   string folder=Path.Combine(Engine.VaultOf(b),b.Id);Engine.NoLinks(folder,true);
   if(b.Payload=="registry.xml"){
    if(!Windows)throw new IOException("Chỉ khôi phục Registry trên Windows.");
    TracksSafety.ValidateKey(b.Hive,b.Original);
    var node=Engine.Load<RegNode>(Path.Combine(folder,"registry.xml"));
    using(var hive=Engine.Base("HKCU","64"))using(var key=hive.CreateSubKey(b.Original))Merge(key,node);
    b.State="Restored";b.Error="";Engine.SaveBackup(b);return;
   }
   string content=Path.Combine(folder,"content");string indexPath=Path.Combine(folder,"files.xml");
   if(!File.Exists(indexPath))throw new IOException("Bản sao lưu dấu vết thiếu danh mục tệp (files.xml).");
   var moved=Engine.Load<List<JunkMoved>>(indexPath);string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   int restored=0,skipped=0,failed=0;
   foreach(var entry in moved){
    try{
     string source=Path.Combine(content,entry.Stored);if(!File.Exists(source)){skipped++;continue;}
     string target=Engine.Canon(entry.Original);TracksSafety.ValidateRoot(Path.GetDirectoryName(target),profile);
     if(File.Exists(target)||Directory.Exists(target)){skipped++;continue;}
     Directory.CreateDirectory(Path.GetDirectoryName(target));File.Move(source,target);restored++;
    }catch(Exception){failed++;}
   }
   if(failed>0){b.State="NeedsReview";b.Error="Khôi phục "+restored+" tệp; "+failed+" tệp lỗi; "+skipped+" tệp bỏ qua vì đích đã tồn tại.";Engine.SaveBackup(b);throw new IOException(b.Error);}
   b.State="Restored";b.Error=skipped>0?"Bỏ qua "+skipped+" tệp vì đích đã tồn tại.":"";Engine.SaveBackup(b);
  }

  /// <summary>Writes saved values that are absent from the live key; existing values and their newer data stay untouched.</summary>
  public static void Merge(RegistryKey key,RegNode node){
   var present=new HashSet<string>(key.GetValueNames(),StringComparer.OrdinalIgnoreCase);
   var only=new RegNode();foreach(var v in node.Values)if(!present.Contains(v.Name))only.Values.Add(v);
   Engine.WriteTree(key,only);
   foreach(var child in node.Children)using(var sub=key.CreateSubKey(child.Name))Merge(sub,child.Node);
  }

  public static string GroupLabel(string group){return Core.L.T(group);}
  public static string BackupKindLabel(){return Core.L.T("Dấu vết");}
 }
}
