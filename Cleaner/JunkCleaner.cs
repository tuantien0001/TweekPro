using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace TweekPro.Cleaner {
 /// <summary>One file proposed for cleaning.</summary>
 public class JunkItem { public string Path; public long Bytes; public DateTime LastWrite; public string Root; }

 /// <summary>Preview outcome for one rule: the files found, their total size and whether the rule is currently locked.</summary>
 public class JunkRuleResult {
  public JunkRule Rule; public List<JunkItem> Items=new List<JunkItem>(); public List<string> Roots=new List<string>();
  public long Bytes; public bool Locked, Partial; public string LockReason="", Note="";
  public int Count { get { return Items.Count; } }
 }

 /// <summary>Progress snapshot emitted while previewing or cleaning.</summary>
 public class JunkProgress { public string Stage, Current; public int Files; public long Bytes; }

 /// <summary>Result of a clean run across the selected rules.</summary>
 public class JunkReport {
  public int Cleaned, SkippedInUse, Failed; public long Bytes; public bool Direct; public List<Backup> Backups=new List<Backup>(); public List<string> Errors=new List<string>();
 }

 /// <summary>Index entry stored next to a Junk backup so each file can be put back exactly where it came from.</summary>
 public class JunkMoved { public string Original, Stored; public long Bytes; }

 /// <summary>Rule-driven junk preview and clean. Files go to the Vault by default; direct deletion is a separate explicit mode.</summary>
 public static class JunkCleaner {
  public const int MaxFilesPerRule=100000;
  public const int MaxSecondsPerRule=30;

  /// <summary>Names of blocked processes that are currently running, or an empty list.</summary>
  public static List<string> RunningBlockers(JunkRule rule){
   var running=new List<string>();
   foreach(string name in rule.BlockedProcesses??new List<string>()){
    if(String.IsNullOrWhiteSpace(name))continue;
    try{var processes=Process.GetProcessesByName(name);if(processes.Length>0)running.Add(name);foreach(var p in processes)p.Dispose();}catch(Exception){}
   }
   return running;
  }

  /// <summary>Enumerates matching files for every rule with size and age filters, without changing anything on disk.</summary>
  public static List<JunkRuleResult> Preview(IEnumerable<JunkRule> rules,bool elevated,int minAgeOverrideHours,CancellationToken cancel,Action<JunkProgress> progress=null){
   var results=new List<JunkRuleResult>();DateTime now=DateTime.Now;
   var allowed=JunkSafety.AllowedRoots();var browsers=JunkSafety.BrowserProfileRoots();var forbidden=JunkSafety.ForbiddenRoots();
   foreach(var rule in rules){
    cancel.ThrowIfCancellationRequested();
    var result=new JunkRuleResult{Rule=rule};results.Add(result);
    if(progress!=null)progress(new JunkProgress{Stage="Đang xem trước: "+rule.Name});
    var blockers=RunningBlockers(rule);
    if(blockers.Count>0){result.Locked=true;result.LockReason="Trình duyệt/ứng dụng đang chạy: "+String.Join(", ",blockers)+". Đóng hẳn rồi xem trước lại.";}
    if(rule.RequiresAdmin&&!elevated){result.Locked=true;result.LockReason=(result.LockReason==""?"":result.LockReason+" ")+"Cần quyền quản trị.";}
    // The settings value is a floor for rules that already filter by age; rules with 0 (browser caches) stay at 0.
    int minAge=rule.MinAgeHours>0&&minAgeOverrideHours>=0?Math.Max(rule.MinAgeHours,minAgeOverrideHours):rule.MinAgeHours;
    var patterns=rule.Patterns.Select(JunkRules.GlobToRegex).ToList();
    var watch=Stopwatch.StartNew();
    foreach(string template in rule.Paths){
     foreach(string root in JunkRules.ExpandPath(template)){
      cancel.ThrowIfCancellationRequested();
      try{JunkSafety.ValidateRoot(root,allowed,browsers,forbidden);}catch(IOException e){result.Note=Append(result.Note,e.Message);continue;}
      if(!Directory.Exists(root))continue;
      result.Roots.Add(root);
      Walk(root,rule.Recurse,patterns,minAge,now,result,watch,cancel,progress);
      if(result.Partial)break;
     }
     if(result.Partial)break;
    }
    result.Bytes=result.Items.Sum(i=>i.Bytes);
    if(result.Roots.Count==0&&result.Note=="")result.Note="Không có thư mục tương ứng trên máy này.";
   }
   return results;
  }

  static string Append(string note,string text){return String.IsNullOrEmpty(note)?text:note+" "+text;}

  /// <summary>Breadth-first walk with file and time limits; skips reparse points and folders that cannot be read.</summary>
  static void Walk(string root,bool recurse,List<Regex> patterns,int minAge,DateTime now,JunkRuleResult result,Stopwatch watch,CancellationToken cancel,Action<JunkProgress> progress){
   var queue=new Queue<string>();queue.Enqueue(root);int visited=0;
   while(queue.Count>0){
    cancel.ThrowIfCancellationRequested();
    if(result.Items.Count>=MaxFilesPerRule||watch.Elapsed.TotalSeconds>MaxSecondsPerRule){result.Partial=true;result.Note=Append(result.Note,"Đã chạm giới hạn "+MaxFilesPerRule.ToString("N0")+" tệp / "+MaxSecondsPerRule+" giây; dọn rồi xem trước lại để tiếp.");return;}
    string dir=queue.Dequeue();
    try{
     foreach(string file in Directory.EnumerateFiles(dir)){
      if(result.Items.Count>=MaxFilesPerRule)break;
      var info=new FileInfo(file);
      if(!JunkRules.MatchesName(patterns,info.Name))continue;
      if(!JunkSafety.AcceptFile(info,root,minAge,now))continue;
      long bytes;try{bytes=info.Length;}catch(IOException){continue;}
      result.Items.Add(new JunkItem{Path=info.FullName,Bytes=bytes,LastWrite=info.LastWriteTime,Root=root});
      visited++;
      if(progress!=null&&visited%500==0)progress(new JunkProgress{Stage="Đang xem trước: "+result.Rule.Name,Current=dir,Files=result.Items.Count,Bytes=result.Items.Sum(i=>i.Bytes)});
     }
     if(recurse)foreach(string sub in Directory.EnumerateDirectories(dir)){
      try{if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)!=0)continue;}catch(IOException){continue;}catch(UnauthorizedAccessException){continue;}
      queue.Enqueue(sub);
     }
    }catch(UnauthorizedAccessException){result.Note=Append(result.Note,"Không đủ quyền đọc: "+dir);}
    catch(IOException){result.Note=Append(result.Note,"Bỏ qua thư mục không đọc được: "+dir);}
   }
  }

  /// <summary>True when the file can be opened exclusively, meaning no other process holds it.</summary>
  public static bool CanTake(string path){
   try{using(new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}return true;}
   catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
  }

  /// <summary>Cleans the given previewed rules. Vault mode moves files into one Junk backup per rule; direct mode deletes them permanently.</summary>
  public static JunkReport Clean(IEnumerable<JunkRuleResult> selected,bool direct,CancellationToken cancel,Action<JunkProgress> progress=null){
   var report=new JunkReport{Direct=direct};
   var allowed=JunkSafety.AllowedRoots();var browsers=JunkSafety.BrowserProfileRoots();var forbidden=JunkSafety.ForbiddenRoots();
   foreach(var result in selected){
    cancel.ThrowIfCancellationRequested();
    if(result.Locked)throw new IOException(result.Rule.Name+": "+result.LockReason);
    if(result.Items.Count==0)continue;
    var blockers=RunningBlockers(result.Rule);
    if(blockers.Count>0)throw new IOException(result.Rule.Name+": "+String.Join(", ",blockers)+" vừa được khởi động; bỏ qua nhóm này.");
    foreach(string root in result.Roots)JunkSafety.ValidateRoot(root,allowed,browsers,forbidden);
    if(direct)CleanDirect(result,report,cancel,progress);else CleanToVault(result,report,cancel,progress);
    if(result.Rule.Recurse)foreach(string root in result.Roots)RemoveEmptyDirectories(root);
   }
   return report;
  }

  static void CleanDirect(JunkRuleResult result,JunkReport report,CancellationToken cancel,Action<JunkProgress> progress){
   int index=0;
   foreach(var item in result.Items){
    cancel.ThrowIfCancellationRequested();index++;
    if(progress!=null&&index%100==0)progress(new JunkProgress{Stage="Đang xóa thẳng: "+result.Rule.Name,Current=item.Path,Files=report.Cleaned,Bytes=report.Bytes});
    try{
     if(!result.Roots.Any(r=>Engine.Under(item.Path,r)))throw new IOException("Tệp nằm ngoài gốc quy tắc.");
     if(!File.Exists(item.Path))continue;
     if((File.GetAttributes(item.Path)&FileAttributes.ReparsePoint)!=0)continue;
     if(!CanTake(item.Path)){report.SkippedInUse++;continue;}
     File.SetAttributes(item.Path,FileAttributes.Normal);
     File.Delete(item.Path);report.Cleaned++;report.Bytes+=item.Bytes;
    }catch(Exception e){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(item.Path+": "+e.Message);}
   }
  }

  static void CleanToVault(JunkRuleResult result,JunkReport report,CancellationToken cancel,Action<JunkProgress> progress){
   Engine.NoLinks(Engine.Vault,false);
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Kind="Junk",Purpose="Junk",AppName=result.Rule.Name,Original=String.Join(" | ",result.Roots),Payload="content"};
   string folder=Path.Combine(Engine.Vault,backup.Id);string content=Path.Combine(folder,"content");
   Directory.CreateDirectory(content);Engine.SaveBackup(backup);
   var moved=new List<JunkMoved>();int index=0;string indexPath=Path.Combine(folder,"files.xml");
   try{
    foreach(var item in result.Items){
     cancel.ThrowIfCancellationRequested();index++;
     if(progress!=null&&index%100==0)progress(new JunkProgress{Stage="Đang chuyển vào kho: "+result.Rule.Name,Current=item.Path,Files=report.Cleaned,Bytes=report.Bytes});
     try{
      if(!result.Roots.Any(r=>Engine.Under(item.Path,r)))throw new IOException("Tệp nằm ngoài gốc quy tắc.");
      if(!File.Exists(item.Path))continue;
      if((File.GetAttributes(item.Path)&FileAttributes.ReparsePoint)!=0)continue;
      if(!CanTake(item.Path)){report.SkippedInUse++;continue;}
      string stored=StoredName(index,item.Path);
      File.Move(item.Path,Path.Combine(content,stored));
      moved.Add(new JunkMoved{Original=item.Path,Stored=stored,Bytes=item.Bytes});
      report.Cleaned++;report.Bytes+=item.Bytes;
      if(moved.Count%200==0)Engine.Save(indexPath,moved);
     }catch(OperationCanceledException){throw;}
     catch(Exception e){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(item.Path+": "+e.Message);}
    }
    Engine.Save(indexPath,moved);
    backup.State="BackedUp";Engine.SaveBackup(backup);report.Backups.Add(backup);
   }catch(Exception e){
    try{Engine.Save(indexPath,moved);}catch(Exception){}
    backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;
   }
   if(moved.Count==0){
    // Nothing was moved: drop the empty backup so the vault list stays meaningful.
    try{Directory.Delete(folder,true);report.Backups.Remove(backup);}catch(Exception){}
   }
  }

  /// <summary>Flat, collision-free file name inside the backup content folder.</summary>
  public static string StoredName(int index,string originalPath){
   int cut=Math.Max(originalPath.LastIndexOf('\\'),originalPath.LastIndexOf('/'));
   string name=originalPath.Substring(cut+1);
   if(name.Length>80)name=name.Substring(0,80);
   var sb=new System.Text.StringBuilder(name.Length);
   foreach(char c in name)sb.Append(c<32||"<>:\"/\\|?*".IndexOf(c)>=0?'_':c);
   return index.ToString("D6")+"_"+sb;
  }

  /// <summary>Removes empty subfolders beneath a cleaned root (never the root itself), skipping links and unreadable folders.</summary>
  public static int RemoveEmptyDirectories(string root){
   int removed=0;
   try{
    foreach(string sub in Directory.GetDirectories(root)){
     try{
      if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)!=0)continue;
      removed+=RemoveEmptyDirectories(sub);
      if(!Directory.EnumerateFileSystemEntries(sub).Any()){Directory.Delete(sub);removed++;}
     }catch(IOException){}catch(UnauthorizedAccessException){}
    }
   }catch(IOException){}catch(UnauthorizedAccessException){}
   return removed;
  }

  /// <summary>Puts every file of a Junk backup back to its original path, skipping destinations that already exist.</summary>
  public static void Restore(Backup b){
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   string folder=Path.Combine(Engine.VaultOf(b),b.Id);string content=Path.Combine(folder,"content");string indexPath=Path.Combine(folder,"files.xml");
   Engine.NoLinks(folder,true);
   if(!File.Exists(indexPath))throw new IOException("Bản sao lưu rác thiếu danh mục tệp (files.xml).");
   var moved=Engine.Load<List<JunkMoved>>(indexPath);
   var allowed=JunkSafety.AllowedRoots();var browsers=JunkSafety.BrowserProfileRoots();var forbidden=JunkSafety.ForbiddenRoots();
   int restored=0,skipped=0,failed=0;
   foreach(var entry in moved){
    try{
     string source=Path.Combine(content,entry.Stored);
     if(!File.Exists(source)){skipped++;continue;}
     string target=Engine.Canon(entry.Original);
     JunkSafety.ValidateRoot(Path.GetDirectoryName(target),allowed,browsers,forbidden);
     if(File.Exists(target)||Directory.Exists(target)){skipped++;continue;}
     Directory.CreateDirectory(Path.GetDirectoryName(target));
     File.Move(source,target);restored++;
    }catch(Exception){failed++;}
   }
   if(failed>0){b.State="NeedsReview";b.Error="Khôi phục "+restored+" tệp; "+failed+" tệp lỗi; "+skipped+" tệp bỏ qua vì đích đã tồn tại.";Engine.SaveBackup(b);throw new IOException(b.Error);}
   b.State="Restored";b.Error=skipped>0?"Bỏ qua "+skipped+" tệp vì đích đã tồn tại.":"";Engine.SaveBackup(b);
  }
 }
}
