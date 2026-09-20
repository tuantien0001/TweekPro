using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using TweekPro.Cleaner;

namespace TweekPro.Dupes {
 /// <summary>One file considered while searching for duplicates.</summary>
 public class DuplicateFile { public string Path; public long Bytes; public DateTime Created; public DateTime LastWrite; }

 /// <summary>A set of byte-identical files (same size and content hash) together with the copy chosen to keep.</summary>
 public class DuplicateGroup {
  public string Hash; public long Bytes; public List<DuplicateFile> Files=new List<DuplicateFile>(); public DuplicateFile Keeper;
  public int Count { get { return Files.Count; } }
  public long Wasted { get { return Bytes*Math.Max(0,Files.Count-1); } }
  public IEnumerable<DuplicateFile> Redundant { get { return Keeper==null?Files:Files.Where(f=>!ReferenceEquals(f,Keeper)); } }
 }

 /// <summary>Outcome of a read-only duplicate scan.</summary>
 public class DuplicateScanResult {
  public string Root; public List<DuplicateGroup> Groups=new List<DuplicateGroup>();
  public int FilesScanned, Hashed; public long WastedBytes; public bool Partial; public List<string> Notes=new List<string>();
 }

 /// <summary>Progress snapshot emitted while scanning or quarantining.</summary>
 public class DuplicateProgress { public string Stage, Current; public int Files; }

 /// <summary>Result of moving redundant copies into the vault or deleting them.</summary>
 public class DuplicateReport {
  public int Quarantined, SkippedInUse, Failed; public long Bytes; public bool Direct;
  public List<Backup> Backups=new List<Backup>(); public List<string> Errors=new List<string>();
 }

 /// <summary>Code-level boundary for the duplicate finder: which folders it may scan and which files it may ever move.</summary>
 public static class DupeSafety {
  /// <summary>System and Tweek Pro folders the finder never scans or removes from, even when nested inside a chosen root.</summary>
  public static List<string> ForbiddenRoots(){
   var list=new List<string>();
   Action<string> add=p=>{if(!String.IsNullOrWhiteSpace(p)){try{list.Add(Engine.Canon(p));}catch(ArgumentException){}}};
   foreach(var folder in new[]{Environment.SpecialFolder.Windows,Environment.SpecialFolder.System,Environment.SpecialFolder.SystemX86,Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86})add(Environment.GetFolderPath(folder));
   add(Core.Paths.Root);add(Core.Paths.Legacy);add(Engine.Vault);
   return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>Validates a scan root: it must be a real, rooted directory that is not a drive root, a link, or a protected area.</summary>
  public static void ValidateScanRoot(string folder){ValidateScanRoot(folder,ForbiddenRoots());}

  /// <summary>Testable overload of ValidateScanRoot with an explicit deny list.</summary>
  public static void ValidateScanRoot(string folder,IList<string> forbidden){
   if(String.IsNullOrWhiteSpace(folder))throw new IOException("Chưa chọn thư mục để quét.");
   string p=Engine.Canon(folder);
   if(p.Length<=3)throw new IOException("Không quét toàn bộ gốc ổ đĩa; hãy chọn một thư mục cụ thể.");
   if(!Directory.Exists(p))throw new IOException("Thư mục không tồn tại: "+folder);
   foreach(string f in forbidden)if(String.Equals(p,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,f))throw new IOException("Không quét thư mục hệ thống hoặc dữ liệu Tweek Pro: "+folder);
   Engine.NoLinks(p,false);
  }

  /// <summary>True when a folder must be skipped during the walk because it is protected.</summary>
  public static bool IsForbidden(string folder,IList<string> forbidden){
   string p;try{p=Engine.Canon(folder);}catch(Exception){return true;}
   return forbidden.Any(f=>String.Equals(p,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,f));
  }

  /// <summary>Validates that a file may be moved out: under the scan root, not a link, not inside a protected area.</summary>
  public static void ValidateRemovable(string path,string root,IList<string> forbidden){
   string p=Engine.Canon(path);
   if(!Engine.Under(p,Engine.Canon(root)))throw new IOException("Tệp nằm ngoài thư mục đã quét.");
   foreach(string f in forbidden)if(Engine.Under(p,f))throw new IOException("Tệp nằm trong thư mục được bảo vệ.");
   Engine.NoLinks(p,false);
  }
 }

 /// <summary>Read-only duplicate scan (group by size, then by SHA-256) with quarantine to the vault or direct deletion.</summary>
 public static class DuplicateFinder {
  public const int MaxFiles=500000;
  public const int MaxSeconds=90;

  /// <summary>Chooses which copy to keep: the oldest by default, or the newest, with deterministic path tie-breaks.</summary>
  public static DuplicateFile SelectKeeper(IEnumerable<DuplicateFile> files,bool keepNewest){
   var list=files.ToList();
   IOrderedEnumerable<DuplicateFile> ordered=keepNewest
    ?list.OrderByDescending(f=>f.Created).ThenByDescending(f=>f.LastWrite)
    :list.OrderBy(f=>f.Created).ThenBy(f=>f.LastWrite);
   return ordered.ThenBy(f=>(f.Path??"").Length).ThenBy(f=>f.Path,StringComparer.OrdinalIgnoreCase).First();
  }

  /// <summary>Walks the chosen folder read-only, grouping byte-identical files by size and then by content hash.</summary>
  public static DuplicateScanResult Scan(string root,long minBytes,CancellationToken cancel,Action<DuplicateProgress> progress=null){
   var result=new DuplicateScanResult{Root=Engine.Canon(root)};
   var forbidden=DupeSafety.ForbiddenRoots();
   DupeSafety.ValidateScanRoot(root,forbidden);
   var bySize=new Dictionary<long,List<DuplicateFile>>();
   var watch=Stopwatch.StartNew();
   var queue=new Queue<string>();queue.Enqueue(result.Root);
   while(queue.Count>0){
    cancel.ThrowIfCancellationRequested();
    if(result.FilesScanned>=MaxFiles||watch.Elapsed.TotalSeconds>MaxSeconds){result.Partial=true;result.Notes.Add("Đã chạm giới hạn "+MaxFiles.ToString("N0")+" tệp / "+MaxSeconds+" giây; kết quả chưa đầy đủ.");break;}
    string dir=queue.Dequeue();
    if(DupeSafety.IsForbidden(dir,forbidden))continue;
    try{
     foreach(string file in Directory.EnumerateFiles(dir)){
      if(result.FilesScanned>=MaxFiles)break;
      FileInfo info;try{info=new FileInfo(file);}catch(Exception){continue;}
      try{if((info.Attributes&FileAttributes.ReparsePoint)!=0)continue;}catch(Exception){continue;}
      long bytes;try{bytes=info.Length;}catch(IOException){continue;}
      if(bytes<minBytes)continue;
      var df=new DuplicateFile{Path=info.FullName,Bytes=bytes,Created=Safe(info,true),LastWrite=Safe(info,false)};
      List<DuplicateFile> bucket;if(!bySize.TryGetValue(bytes,out bucket)){bucket=new List<DuplicateFile>();bySize[bytes]=bucket;}
      bucket.Add(df);result.FilesScanned++;
      if(progress!=null&&result.FilesScanned%500==0)progress(new DuplicateProgress{Stage="Đang quét",Current=dir,Files=result.FilesScanned});
     }
     foreach(string sub in Directory.EnumerateDirectories(dir)){
      try{if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)!=0)continue;}catch(Exception){continue;}
      if(DupeSafety.IsForbidden(sub,forbidden))continue;
      queue.Enqueue(sub);
     }
    }catch(UnauthorizedAccessException){result.Notes.Add("Không đủ quyền đọc: "+dir);}
    catch(IOException){result.Notes.Add("Bỏ qua thư mục không đọc được: "+dir);}
   }
   foreach(var pair in bySize){
    cancel.ThrowIfCancellationRequested();
    if(pair.Value.Count<2)continue;
    var byHash=new Dictionary<string,DuplicateGroup>();
    foreach(var df in pair.Value){
     cancel.ThrowIfCancellationRequested();
     string hash;try{hash=HashFile(df.Path);}catch(Exception){result.Notes.Add("Không đọc được nội dung: "+df.Path);continue;}
     result.Hashed++;
     if(progress!=null&&result.Hashed%200==0)progress(new DuplicateProgress{Stage="Đang so khớp nội dung",Current=df.Path,Files=result.Hashed});
     DuplicateGroup group;if(!byHash.TryGetValue(hash,out group)){group=new DuplicateGroup{Hash=hash,Bytes=df.Bytes};byHash[hash]=group;}
     group.Files.Add(df);
    }
    foreach(var group in byHash.Values)if(group.Files.Count>=2){group.Keeper=SelectKeeper(group.Files,false);result.Groups.Add(group);}
   }
   result.Groups=result.Groups.OrderByDescending(g=>g.Wasted).ThenByDescending(g=>g.Bytes).ToList();
   result.WastedBytes=result.Groups.Sum(g=>g.Wasted);
   return result;
  }

  static DateTime Safe(FileInfo info,bool created){try{return created?info.CreationTime:info.LastWriteTime;}catch(Exception){return DateTime.MinValue;}}

  /// <summary>Computes the SHA-256 of a file as a lowercase hex string, reading it as a shared stream.</summary>
  public static string HashFile(string path){
   using(var sha=SHA256.Create())
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536))
    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
  }

  /// <summary>Moves the redundant copies of the given groups into one vault backup, or deletes them permanently in direct mode.</summary>
  public static DuplicateReport Quarantine(string root,IEnumerable<DuplicateGroup> groups,bool direct,CancellationToken cancel,Action<DuplicateProgress> progress=null){
   var report=new DuplicateReport{Direct=direct};
   var forbidden=DupeSafety.ForbiddenRoots();
   string canonRoot=Engine.Canon(root);
   var targets=new List<RemovalTarget>();
   foreach(var group in groups){
    // Never trust the scan snapshot blindly: the keeper must still exist unchanged, otherwise the whole group is skipped.
    if(group.Files.Count==0)continue;
    if(group.Keeper==null)group.Keeper=SelectKeeper(group.Files,false);
    if(group.Keeper==null||!Unchanged(group.Keeper,group.Bytes)){report.Failed++;if(report.Errors.Count<50)report.Errors.Add((group.Keeper==null?"(nhóm "+group.Hash+")":group.Keeper.Path)+": bản giữ lại đã thay đổi hoặc không còn sau khi quét; bỏ qua cả nhóm.");continue;}
    foreach(var file in group.Redundant)targets.Add(new RemovalTarget{File=file,Group=group});
   }
   if(targets.Count==0)return report;
   if(direct)DeleteDirect(targets,canonRoot,forbidden,report,cancel,progress);
   else MoveToVault(targets,canonRoot,forbidden,report,cancel,progress);
   return report;
  }

  class RemovalTarget { public DuplicateFile File; public DuplicateGroup Group; }

  /// <summary>True when the file on disk still has the size and last-write time recorded at scan time.</summary>
  public static bool Unchanged(DuplicateFile file,long expectedBytes){
   try{var info=new FileInfo(file.Path);if(!info.Exists)return false;if(info.Length!=expectedBytes)return false;return info.LastWriteTime==file.LastWrite;}
   catch(Exception){return false;}
  }

  /// <summary>Rejects a redundant copy that changed since the scan; direct deletion additionally re-hashes the content.</summary>
  static bool StillDuplicate(RemovalTarget t,bool rehash,DuplicateReport report){
   if(!Unchanged(t.File,t.Group.Bytes)){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(t.File.Path+": tệp đã thay đổi sau khi quét; không xóa.");return false;}
   if(rehash&&!String.Equals(HashFile(t.File.Path),t.Group.Hash,StringComparison.OrdinalIgnoreCase)){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(t.File.Path+": nội dung không còn trùng với bản giữ lại; không xóa.");return false;}
   return true;
  }

  static void MoveToVault(List<RemovalTarget> targets,string root,IList<string> forbidden,DuplicateReport report,CancellationToken cancel,Action<DuplicateProgress> progress){
   Engine.NoLinks(Engine.Vault,false);
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Kind="Duplicate",Purpose="Duplicate",AppName="Tệp trùng lặp",Original=root,Payload="content"};
   string folder=Path.Combine(Engine.Vault,backup.Id);string content=Path.Combine(folder,"content");
   Directory.CreateDirectory(content);Engine.SaveBackup(backup);
   var moved=new List<JunkMoved>();int index=0;string indexPath=Path.Combine(folder,"files.xml");
   try{
    foreach(var target in targets){
     var item=target.File;
     cancel.ThrowIfCancellationRequested();index++;
     if(progress!=null&&index%50==0)progress(new DuplicateProgress{Stage="Đang chuyển vào kho",Current=item.Path,Files=report.Quarantined});
     try{
      DupeSafety.ValidateRemovable(item.Path,root,forbidden);
      if(!File.Exists(item.Path))continue;
      if((File.GetAttributes(item.Path)&FileAttributes.ReparsePoint)!=0)continue;
      if(!StillDuplicate(target,false,report))continue;
      if(!JunkCleaner.CanTake(item.Path)){report.SkippedInUse++;continue;}
      string stored=JunkCleaner.StoredName(index,item.Path);
      File.Move(item.Path,Path.Combine(content,stored));
      moved.Add(new JunkMoved{Original=item.Path,Stored=stored,Bytes=item.Bytes});
      report.Quarantined++;report.Bytes+=item.Bytes;
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

  static void DeleteDirect(List<RemovalTarget> targets,string root,IList<string> forbidden,DuplicateReport report,CancellationToken cancel,Action<DuplicateProgress> progress){
   int index=0;
   foreach(var target in targets){
    var item=target.File;
    cancel.ThrowIfCancellationRequested();index++;
    if(progress!=null&&index%50==0)progress(new DuplicateProgress{Stage="Đang xóa thẳng",Current=item.Path,Files=report.Quarantined});
    try{
     DupeSafety.ValidateRemovable(item.Path,root,forbidden);
     if(!File.Exists(item.Path))continue;
     if((File.GetAttributes(item.Path)&FileAttributes.ReparsePoint)!=0)continue;
     if(!StillDuplicate(target,true,report))continue;
     if(!JunkCleaner.CanTake(item.Path)){report.SkippedInUse++;continue;}
     File.SetAttributes(item.Path,FileAttributes.Normal);
     File.Delete(item.Path);report.Quarantined++;report.Bytes+=item.Bytes;
    }catch(Exception e){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(item.Path+": "+e.Message);}
   }
  }

  /// <summary>Puts every quarantined duplicate back to its original path, skipping destinations that already exist.</summary>
  public static void Restore(Backup b){
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   string folder=Path.Combine(Engine.VaultOf(b),b.Id);string content=Path.Combine(folder,"content");string indexPath=Path.Combine(folder,"files.xml");
   Engine.NoLinks(folder,true);
   if(!File.Exists(indexPath))throw new IOException("Bản sao lưu trùng lặp thiếu danh mục tệp (files.xml).");
   var moved=Engine.Load<List<JunkMoved>>(indexPath);
   var forbidden=DupeSafety.ForbiddenRoots();
   int restored=0,skipped=0,failed=0;
   foreach(var entry in moved){
    try{
     string source=Path.Combine(content,entry.Stored);
     if(!File.Exists(source)){skipped++;continue;}
     string target=Engine.Canon(entry.Original);
     foreach(string f in forbidden)if(Engine.Under(target,f))throw new IOException("Đích nằm trong thư mục được bảo vệ.");
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
