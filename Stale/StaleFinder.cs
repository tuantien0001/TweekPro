using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using TweekPro.Cleaner;
using TweekPro.Core;

namespace TweekPro.Stale {
 /// <summary>Why a file counts as stale: an installer, an archive, a disk image, an abandoned partial download, or simply large.</summary>
 public enum StaleKind { Installer, Archive, DiskImage, Partial, Large }

 /// <summary>One candidate file found under Downloads/Desktop with its age and classification.</summary>
 public class StaleFile {
  public string Path; public long Bytes; public DateTime Created, LastWrite; public StaleKind Kind; public int AgeDays;
  public string Name { get { return System.IO.Path.GetFileName(Path??""); } }
  public string Folder { get { return System.IO.Path.GetDirectoryName(Path??"")??""; } }
 }

 /// <summary>Outcome of a read-only stale-file scan.</summary>
 public class StaleScanResult {
  public List<string> Roots=new List<string>(); public List<StaleFile> Files=new List<StaleFile>();
  public int Scanned; public long TotalBytes; public bool Partial; public List<string> Notes=new List<string>();
 }

 /// <summary>Progress snapshot emitted while scanning or quarantining.</summary>
 public class StaleProgress { public string Stage, Current; public int Files; }

 /// <summary>Result of moving stale files into the vault or deleting them.</summary>
 public class StaleReport {
  public int Quarantined, SkippedInUse, Failed; public long Bytes; public bool Direct;
  public List<Backup> Backups=new List<Backup>(); public List<string> Errors=new List<string>();
 }

 /// <summary>
 /// Code-level boundary for the stale-file cleaner: it may only ever scan the current user's Downloads and Desktop and may only
 /// remove regular files found there. Shortcuts, hidden/system files and anything under a protected or cloud-synced folder are skipped.
 /// </summary>
 public static class StaleSafety {
  public static readonly string[] InstallerExtensions={".exe",".msi",".msix",".msixbundle",".appx",".appxbundle",".apk",".msu",".msp"};
  public static readonly string[] ArchiveExtensions={".zip",".7z",".rar",".tar",".gz",".tgz",".bz2",".xz",".cab",".zipx"};
  public static readonly string[] DiskImageExtensions={".iso",".img",".vhd",".vhdx",".dmg",".wim",".esd",".vmdk"};
  public static readonly string[] PartialExtensions={".crdownload",".part",".partial",".download",".opdownload",".tmp",".!ut",".bc!"};
  static readonly string[] NeverTouch={".lnk",".url",".ini",".library-ms"};

  /// <summary>The only folders the cleaner may scan: the user's Downloads and Desktop when they exist.</summary>
  public static List<string> AllowedRoots(){
   var list=new List<string>();
   Action<string> add=p=>{if(!String.IsNullOrWhiteSpace(p)){try{if(Directory.Exists(p))list.Add(Engine.Canon(p));}catch(ArgumentException){}}};
   try{string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);if(!String.IsNullOrWhiteSpace(profile))add(Path.Combine(profile,"Downloads"));}catch(Exception){}
   try{add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));}catch(Exception){}
   return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>System folders, Tweek Pro data, the vault and cloud-sync roots: never scanned or removed from, even when nested under Downloads.</summary>
  public static List<string> ForbiddenRoots(){
   var list=new List<string>();
   Action<string> add=p=>{if(!String.IsNullOrWhiteSpace(p)){try{list.Add(Engine.Canon(p));}catch(ArgumentException){}}};
   foreach(var folder in new[]{Environment.SpecialFolder.Windows,Environment.SpecialFolder.System,Environment.SpecialFolder.SystemX86,Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.MyDocuments,Environment.SpecialFolder.MyPictures,Environment.SpecialFolder.MyMusic,Environment.SpecialFolder.MyVideos})add(Environment.GetFolderPath(folder));
   add(Core.Paths.Root);add(Core.Paths.Legacy);add(Engine.Vault);
   add(Environment.GetEnvironmentVariable("OneDrive"));add(Environment.GetEnvironmentVariable("OneDriveCommercial"));
   string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   if(!String.IsNullOrWhiteSpace(profile)){add(Path.Combine(profile,"OneDrive"));add(Path.Combine(profile,"Dropbox"));add(Path.Combine(profile,"Google Drive"));}
   // A Desktop redirected into OneDrive would otherwise forbid itself; the allow list already limits the scan to Downloads/Desktop.
   var allowed=AllowedRoots();
   return list.Distinct(StringComparer.OrdinalIgnoreCase).Where(f=>!allowed.Any(a=>String.Equals(a,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(a,f))).ToList();
  }

  /// <summary>Validates a scan root against explicit lists: it must be, or lie under, an allowed root and outside every forbidden one.</summary>
  public static void ValidateRoot(string folder,IList<string> allowed,IList<string> forbidden){
   if(String.IsNullOrWhiteSpace(folder))throw new IOException("Chưa chọn thư mục để quét.");
   string p=Engine.Canon(folder);
   if(p.Length<=3)throw new IOException("Không quét gốc ổ đĩa.");
   if(!allowed.Any(a=>String.Equals(p,a,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,a)))throw new IOException("Chỉ quét Downloads hoặc Desktop của tài khoản hiện tại: "+folder);
   foreach(string f in forbidden)if(String.Equals(p,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,f))throw new IOException("Thư mục nằm trong vùng được bảo vệ: "+folder);
   if(Directory.Exists(p))Engine.NoLinks(p,false);
  }
  public static void ValidateRoot(string folder){ValidateRoot(folder,AllowedRoots(),ForbiddenRoots());}

  public static bool IsForbidden(string folder,IList<string> forbidden){
   string p;try{p=Engine.Canon(folder);}catch(Exception){return true;}
   return forbidden.Any(f=>String.Equals(p,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,f));
  }

  /// <summary>A file may be removed only when it sits under an allowed root, outside forbidden roots, is not a link and is not a shortcut/system file.</summary>
  public static void ValidateRemovable(string path,IList<string> allowed,IList<string> forbidden){
   string p=Engine.Canon(path);
   if(!allowed.Any(a=>Engine.Under(p,a)))throw new IOException("Tệp nằm ngoài Downloads/Desktop.");
   foreach(string f in forbidden)if(Engine.Under(p,f))throw new IOException("Tệp nằm trong thư mục được bảo vệ.");
   if(NeverTouch.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase))throw new IOException("Không xóa shortcut hay tệp cấu hình thư mục.");
   Engine.NoLinks(p,false);
  }

  /// <summary>Classifies by extension; null means the file is neither an installer/archive/image/partial nor eligible as "large".</summary>
  public static StaleKind? Classify(string path,long bytes,long minLargeBytes){
   string ext=Path.GetExtension(path??"")??"";
   if(NeverTouch.Contains(ext,StringComparer.OrdinalIgnoreCase))return null;
   if(PartialExtensions.Contains(ext,StringComparer.OrdinalIgnoreCase))return StaleKind.Partial;
   if(InstallerExtensions.Contains(ext,StringComparer.OrdinalIgnoreCase))return StaleKind.Installer;
   if(ArchiveExtensions.Contains(ext,StringComparer.OrdinalIgnoreCase))return StaleKind.Archive;
   if(DiskImageExtensions.Contains(ext,StringComparer.OrdinalIgnoreCase))return StaleKind.DiskImage;
   if(minLargeBytes>0&&bytes>=minLargeBytes)return StaleKind.Large;
   return null;
  }

  /// <summary>Whole days since the newer of creation and last write; future stamps count as zero.</summary>
  public static int AgeDays(DateTime created,DateTime lastWrite,DateTime now){
   DateTime newest=created>lastWrite?created:lastWrite;
   double days=(now-newest).TotalDays;
   return days<0?0:(int)Math.Floor(days);
  }
 }

 /// <summary>Read-only scan of Downloads/Desktop for old installers, archives, disk images, abandoned partial downloads and large files, with quarantine to the vault.</summary>
 public static class StaleFinder {
  public const int MaxFiles=200000, MaxSeconds=60;
  public const string BackupKind="Stale";

  /// <summary>Walks the given roots (each must be Downloads or Desktop) and returns files older than minAgeDays that match a stale kind.</summary>
  public static StaleScanResult Scan(IEnumerable<string> roots,int minAgeDays,long minLargeBytes,CancellationToken cancel,Action<StaleProgress> progress=null){
   return Scan(roots,minAgeDays,minLargeBytes,DateTime.Now,StaleSafety.AllowedRoots(),StaleSafety.ForbiddenRoots(),cancel,progress);
  }

  /// <summary>Testable overload with explicit clock and boundary lists.</summary>
  public static StaleScanResult Scan(IEnumerable<string> roots,int minAgeDays,long minLargeBytes,DateTime now,IList<string> allowed,IList<string> forbidden,CancellationToken cancel,Action<StaleProgress> progress=null){
   var result=new StaleScanResult();
   var watch=Stopwatch.StartNew();
   foreach(string root in roots.Where(r=>!String.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase)){
    StaleSafety.ValidateRoot(root,allowed,forbidden);
    string canon=Engine.Canon(root);if(!Directory.Exists(canon))continue;
    result.Roots.Add(canon);
    var queue=new Queue<string>();queue.Enqueue(canon);
    while(queue.Count>0){
     cancel.ThrowIfCancellationRequested();
     if(result.Scanned>=MaxFiles||watch.Elapsed.TotalSeconds>MaxSeconds){result.Partial=true;result.Notes.Add("Đã chạm giới hạn "+MaxFiles.ToString("N0")+" tệp / "+MaxSeconds+" giây; kết quả chưa đầy đủ.");queue.Clear();break;}
     string dir=queue.Dequeue();
     if(StaleSafety.IsForbidden(dir,forbidden))continue;
     try{
      foreach(string file in Directory.EnumerateFiles(dir)){
       if(result.Scanned>=MaxFiles)break;
       result.Scanned++;
       FileInfo info;try{info=new FileInfo(file);}catch(Exception){continue;}
       FileAttributes attributes;try{attributes=info.Attributes;}catch(Exception){continue;}
       if((attributes&FileAttributes.ReparsePoint)!=0)continue;
       if((attributes&FileAttributes.System)!=0||(attributes&FileAttributes.Hidden)!=0)continue;
       long bytes;try{bytes=info.Length;}catch(IOException){continue;}
       var kind=StaleSafety.Classify(info.FullName,bytes,minLargeBytes);
       if(kind==null)continue;
       DateTime created=Safe(info,true),lastWrite=Safe(info,false);
       int age=StaleSafety.AgeDays(created,lastWrite,now);
       if(age<minAgeDays)continue;
       result.Files.Add(new StaleFile{Path=info.FullName,Bytes=bytes,Created=created,LastWrite=lastWrite,Kind=kind.Value,AgeDays=age});
       result.TotalBytes+=bytes;
       if(progress!=null&&result.Scanned%500==0)progress(new StaleProgress{Stage="Đang quét",Current=dir,Files=result.Scanned});
      }
      foreach(string sub in Directory.EnumerateDirectories(dir)){
       try{var a=File.GetAttributes(sub);if((a&FileAttributes.ReparsePoint)!=0||(a&FileAttributes.System)!=0)continue;}catch(Exception){continue;}
       if(StaleSafety.IsForbidden(sub,forbidden))continue;
       queue.Enqueue(sub);
      }
     }catch(UnauthorizedAccessException){result.Notes.Add("Không đủ quyền đọc: "+dir);}
     catch(IOException){result.Notes.Add("Bỏ qua thư mục không đọc được: "+dir);}
    }
   }
   result.Files=result.Files.OrderByDescending(f=>f.Bytes).ThenByDescending(f=>f.AgeDays).ToList();
   return result;
  }

  static DateTime Safe(FileInfo info,bool created){try{return created?info.CreationTime:info.LastWriteTime;}catch(Exception){return DateTime.MinValue;}}

  /// <summary>True when the file on disk still has the size and last-write time recorded at scan time.</summary>
  public static bool Unchanged(StaleFile file){
   try{var info=new FileInfo(file.Path);if(!info.Exists)return false;if(info.Length!=file.Bytes)return false;return info.LastWriteTime==file.LastWrite;}
   catch(Exception){return false;}
  }

  /// <summary>Moves the selected files into one vault backup (Kind=Stale) or deletes them permanently in direct mode.</summary>
  public static StaleReport Quarantine(IEnumerable<StaleFile> files,bool direct,CancellationToken cancel,Action<StaleProgress> progress=null){
   return Quarantine(files,direct,StaleSafety.AllowedRoots(),StaleSafety.ForbiddenRoots(),cancel,progress);
  }

  public static StaleReport Quarantine(IEnumerable<StaleFile> files,bool direct,IList<string> allowed,IList<string> forbidden,CancellationToken cancel,Action<StaleProgress> progress=null){
   var report=new StaleReport{Direct=direct};
   var targets=files.Where(f=>f!=null&&!String.IsNullOrWhiteSpace(f.Path)).ToList();
   if(targets.Count==0)return report;
   if(direct)DeleteDirect(targets,allowed,forbidden,report,cancel,progress);
   else MoveToVault(targets,allowed,forbidden,report,cancel,progress);
   return report;
  }

  static bool Removable(StaleFile item,IList<string> allowed,IList<string> forbidden,StaleReport report){
   StaleSafety.ValidateRemovable(item.Path,allowed,forbidden);
   if(!File.Exists(item.Path))return false;
   if((File.GetAttributes(item.Path)&FileAttributes.ReparsePoint)!=0)return false;
   if(!Unchanged(item)){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(item.Path+": tệp đã thay đổi sau khi quét; không xóa.");return false;}
   if(!JunkCleaner.CanTake(item.Path)){report.SkippedInUse++;return false;}
   return true;
  }

  static void MoveToVault(List<StaleFile> targets,IList<string> allowed,IList<string> forbidden,StaleReport report,CancellationToken cancel,Action<StaleProgress> progress){
   Engine.NoLinks(Engine.Vault,false);
   string original=targets.Select(t=>t.Folder).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1?targets[0].Folder:String.Join("; ",targets.Select(t=>t.Folder).Distinct(StringComparer.OrdinalIgnoreCase).Take(3));
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Kind=BackupKind,Purpose=BackupKind,AppName="Tệp tải về cũ",Original=original,Payload="content"};
   string folder=Path.Combine(Engine.Vault,backup.Id);string content=Path.Combine(folder,"content");
   Directory.CreateDirectory(content);Engine.SaveBackup(backup);
   var moved=new List<JunkMoved>();int index=0;string indexPath=Path.Combine(folder,"files.xml");
   try{
    foreach(var item in targets){
     cancel.ThrowIfCancellationRequested();index++;
     if(progress!=null&&index%20==0)progress(new StaleProgress{Stage="Đang chuyển vào kho",Current=item.Path,Files=report.Quarantined});
     try{
      if(!Removable(item,allowed,forbidden,report))continue;
      string stored=JunkCleaner.StoredName(index,item.Path);
      File.Move(item.Path,Path.Combine(content,stored));
      moved.Add(new JunkMoved{Original=item.Path,Stored=stored,Bytes=item.Bytes});
      report.Quarantined++;report.Bytes+=item.Bytes;
      if(moved.Count%100==0)Engine.Save(indexPath,moved);
     }catch(OperationCanceledException){throw;}
     catch(Exception e){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(item.Path+": "+e.Message);}
    }
    Engine.Save(indexPath,moved);
    backup.State="BackedUp";Engine.SaveBackup(backup);report.Backups.Add(backup);
   }catch(Exception e){
    try{Engine.Save(indexPath,moved);}catch(Exception){}
    backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;
   }
   if(moved.Count==0){try{Directory.Delete(folder,true);report.Backups.Remove(backup);}catch(Exception){}}
  }

  static void DeleteDirect(List<StaleFile> targets,IList<string> allowed,IList<string> forbidden,StaleReport report,CancellationToken cancel,Action<StaleProgress> progress){
   int index=0;
   foreach(var item in targets){
    cancel.ThrowIfCancellationRequested();index++;
    if(progress!=null&&index%20==0)progress(new StaleProgress{Stage="Đang xóa thẳng",Current=item.Path,Files=report.Quarantined});
    try{
     if(!Removable(item,allowed,forbidden,report))continue;
     File.SetAttributes(item.Path,FileAttributes.Normal);
     File.Delete(item.Path);report.Quarantined++;report.Bytes+=item.Bytes;
    }catch(Exception e){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(item.Path+": "+e.Message);}
   }
  }

  /// <summary>Puts every quarantined file back to its original path, skipping destinations that already exist.</summary>
  public static void Restore(Backup b){
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   string folder=Path.Combine(Engine.VaultOf(b),b.Id);string content=Path.Combine(folder,"content");string indexPath=Path.Combine(folder,"files.xml");
   Engine.NoLinks(folder,true);
   if(!File.Exists(indexPath))throw new IOException("Bản sao lưu tệp cũ thiếu danh mục tệp (files.xml).");
   var moved=Engine.Load<List<JunkMoved>>(indexPath);
   var forbidden=StaleSafety.ForbiddenRoots();
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

  /// <summary>Vault "Kind" column label for Stale backups.</summary>
  public static string BackupKindLabel(){return L.T("Tệp cũ");}

  public static string KindLabel(StaleKind k){
   switch(k){
    case StaleKind.Installer:return L.T("Bộ cài");
    case StaleKind.Archive:return L.T("Tệp nén");
    case StaleKind.DiskImage:return L.T("Ảnh đĩa");
    case StaleKind.Partial:return L.T("Tải dở");
    default:return L.T("Tệp lớn");
   }
  }
 }
}
