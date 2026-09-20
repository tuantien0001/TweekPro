using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TweekPro.Cleaner {
 /// <summary>Result of an empty-folder scan: the top-most empty directories that can be removed.</summary>
 public class EmptyFolderResult {
  public string Root; public List<string> Folders=new List<string>();
  public int Scanned; public bool Partial; public List<string> Notes=new List<string>();
 }

 /// <summary>Outcome of removing empty folders.</summary>
 public class EmptyFolderReport { public int Removed, Skipped, Failed; public List<string> Errors=new List<string>(); }

 /// <summary>
 /// Finds and removes truly empty directories (no files anywhere in their subtree) under a chosen folder.
 /// Reports the top-most empty directory of each empty branch so one removal clears the whole branch.
 /// Read-only until Remove is called; skips links and protected system/Tweek Pro folders.
 /// </summary>
 public static class EmptyFolders {
  public const int MaxFolders=500000;
  public const int MaxSeconds=90;

  /// <summary>System and Tweek Pro folders that are never scanned into or removed.</summary>
  public static List<string> ForbiddenRoots(){
   var list=new List<string>();
   Action<string> add=p=>{if(!String.IsNullOrWhiteSpace(p)){try{list.Add(Engine.Canon(p));}catch(ArgumentException){}}};
   foreach(var folder in new[]{Environment.SpecialFolder.Windows,Environment.SpecialFolder.System,Environment.SpecialFolder.SystemX86,Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86})add(Environment.GetFolderPath(folder));
   add(Core.Paths.Root);add(Core.Paths.Legacy);add(Engine.Vault);
   return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  static bool IsForbidden(string folder,IList<string> forbidden){
   string p;try{p=Engine.Canon(folder);}catch(Exception){return true;}
   return forbidden.Any(f=>String.Equals(p,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,f));
  }

  /// <summary>Validates a scan root: rooted, existing, not a drive root, not a link, not a protected area.</summary>
  public static void ValidateScanRoot(string folder,IList<string> forbidden){
   if(String.IsNullOrWhiteSpace(folder))throw new IOException("Chưa chọn thư mục để quét.");
   string p=Engine.Canon(folder);
   if(p.Length<=3)throw new IOException("Không quét gốc ổ đĩa; hãy chọn một thư mục cụ thể.");
   if(!Directory.Exists(p))throw new IOException("Thư mục không tồn tại: "+folder);
   if(forbidden.Any(f=>String.Equals(p,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,f)))throw new IOException("Không quét thư mục hệ thống hoặc dữ liệu Tweek Pro: "+folder);
   Engine.NoLinks(p,false);
  }

  /// <summary>Finds the top-most empty directories beneath the root (never the root itself).</summary>
  public static EmptyFolderResult Find(string root,CancellationToken cancel,Action<string> progress=null){
   var forbidden=ForbiddenRoots();
   ValidateScanRoot(root,forbidden);
   var result=new EmptyFolderResult{Root=Engine.Canon(root)};
   var watch=System.Diagnostics.Stopwatch.StartNew();
   HasFiles(result.Root,result,forbidden,watch,cancel,progress,true);
   result.Folders.Sort(StringComparer.OrdinalIgnoreCase);
   return result;
  }

  /// <summary>Post-order walk: returns true when the subtree contains at least one file (or is skipped/unreadable, treated as non-empty for safety). Adds top-most empty directories to the result.</summary>
  static bool HasFiles(string dir,EmptyFolderResult result,IList<string> forbidden,System.Diagnostics.Stopwatch watch,CancellationToken cancel,Action<string> progress,bool isRoot){
   cancel.ThrowIfCancellationRequested();
   if(result.Scanned>=MaxFolders||watch.Elapsed.TotalSeconds>MaxSeconds){result.Partial=true;return true;}
   result.Scanned++;
   if(progress!=null&&result.Scanned%1000==0)progress(dir);
   if(!isRoot&&IsForbidden(dir,forbidden))return true;
   try{if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)return true;}catch(Exception){return true;}
   bool anyFile;
   try{anyFile=Directory.EnumerateFiles(dir).Any();}
   catch(UnauthorizedAccessException){result.Notes.Add("Không đủ quyền đọc: "+dir);return true;}
   catch(IOException){result.Notes.Add("Bỏ qua thư mục không đọc được: "+dir);return true;}
   bool subtreeHasFiles=anyFile;
   var emptyChildren=new List<string>();
   List<string> subs;
   try{subs=Directory.EnumerateDirectories(dir).ToList();}
   catch(UnauthorizedAccessException){result.Notes.Add("Không đủ quyền đọc: "+dir);return true;}
   catch(IOException){return true;}
   foreach(string sub in subs){
    bool childHasFiles=HasFiles(sub,result,forbidden,watch,cancel,progress,false);
    if(childHasFiles)subtreeHasFiles=true;
    else emptyChildren.Add(sub);
   }
   // This directory is empty (no files anywhere below). Let the parent claim it as a top-most empty branch.
   if(!subtreeHasFiles&&!isRoot)return false;
   // This directory has files (or is the root): each empty child is a top-most empty branch to report.
   foreach(string emptyChild in emptyChildren)result.Folders.Add(Engine.Canon(emptyChild));
   return subtreeHasFiles||isRoot;
  }

  /// <summary>Deletes the given empty folders after re-checking each is still empty, under the root, and not a link.</summary>
  public static EmptyFolderReport Remove(string root,IEnumerable<string> folders,CancellationToken cancel){
   var report=new EmptyFolderReport();var forbidden=ForbiddenRoots();string canonRoot=Engine.Canon(root);
   foreach(string folder in folders){
    cancel.ThrowIfCancellationRequested();
    try{
     string p=Engine.Canon(folder);
     if(!Engine.Under(p,canonRoot)){report.Skipped++;continue;}
     if(IsForbidden(p,forbidden)){report.Skipped++;continue;}
     if(!Directory.Exists(p)){report.Skipped++;continue;}
     Engine.NoLinks(p,true);
     if(Directory.EnumerateFiles(p,"*",SearchOption.AllDirectories).Any()){report.Skipped++;continue;}
     Directory.Delete(p,true);report.Removed++;
    }catch(Exception e){report.Failed++;if(report.Errors.Count<50)report.Errors.Add(folder+": "+e.Message);}
   }
   return report;
  }
 }
}
