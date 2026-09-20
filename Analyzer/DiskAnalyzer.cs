using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace TweekPro.Analyzer {
 /// <summary>Recursive size and file count of one immediate child folder of the analysed root.</summary>
 public class FolderUsage { public string Path; public long Bytes; public int Files; }

 /// <summary>Total bytes and file count for one file-name extension across the whole scan.</summary>
 public class ExtensionUsage { public string Extension; public long Bytes; public int Files; }

 /// <summary>One of the largest individual files found during the scan.</summary>
 public class LargeFile { public string Path; public long Bytes; public DateTime LastWrite; }

 /// <summary>Read-only disk usage report for a chosen folder.</summary>
 public class DiskReport {
  public string Root; public long TotalBytes; public int TotalFiles, TotalFolders;
  public List<FolderUsage> Folders=new List<FolderUsage>();
  public List<ExtensionUsage> Extensions=new List<ExtensionUsage>();
  public List<LargeFile> LargestFiles=new List<LargeFile>();
  public bool Partial; public List<string> Notes=new List<string>();
 }

 /// <summary>Progress snapshot emitted while analysing.</summary>
 public class AnalyzeProgress { public string Stage, Current; public int Files; public long Bytes; }

 /// <summary>Read-only disk space analyser: totals, per-child-folder sizes, usage by extension and the largest files.</summary>
 public static class DiskAnalyzer {
  public const int MaxFiles=1000000;
  public const int MaxSeconds=120;
  public const int MaxFolders=1000;
  public const int MaxExtensions=80;

  struct Sum { public long Bytes; public int Files; }

  /// <summary>Walks the folder read-only and returns totals, the heaviest child folders, usage by extension and the largest files.</summary>
  public static DiskReport Analyze(string root,int topFiles,CancellationToken cancel,Action<AnalyzeProgress> progress=null){
   if(String.IsNullOrWhiteSpace(root))throw new IOException("Chưa chọn thư mục để phân tích.");
   var report=new DiskReport{Root=Engine.Canon(root)};
   if(report.Root.Length<=3)throw new IOException("Hãy chọn một thư mục cụ thể, không phải gốc ổ đĩa trống.");
   if(!Directory.Exists(report.Root))throw new IOException("Thư mục không tồn tại: "+root);
   Engine.NoLinks(report.Root,false);
   if(topFiles<=0)topFiles=100;

   var byExt=new Dictionary<string,ExtensionUsage>(StringComparer.OrdinalIgnoreCase);
   var largest=new List<LargeFile>();
   long largestThreshold=0;
   var watch=Stopwatch.StartNew();
   Action<FileInfo,long> onFile=(info,len)=>{
    report.TotalBytes+=len;report.TotalFiles++;
    string ext=info.Extension;ext=String.IsNullOrEmpty(ext)?"(không đuôi)":ext.ToLowerInvariant();
    ExtensionUsage eu;if(!byExt.TryGetValue(ext,out eu)){eu=new ExtensionUsage{Extension=ext};byExt[ext]=eu;}
    eu.Bytes+=len;eu.Files++;
    if(len>0&&(largest.Count<topFiles||len>largestThreshold)){
     largest.Add(new LargeFile{Path=info.FullName,Bytes=len,LastWrite=SafeWrite(info)});
     if(largest.Count>=topFiles*2){
      largest.Sort((a,b)=>b.Bytes.CompareTo(a.Bytes));
      largest.RemoveRange(topFiles,largest.Count-topFiles);
      largestThreshold=largest[largest.Count-1].Bytes;
     }
    }
    if(progress!=null&&report.TotalFiles%2000==0)progress(new AnalyzeProgress{Stage="Đang phân tích",Current=info.DirectoryName,Files=report.TotalFiles,Bytes=report.TotalBytes});
   };

   Sum rootFiles=Scan(report.Root,false,report,onFile,cancel,watch);
   if(rootFiles.Files>0)report.Folders.Add(new FolderUsage{Path=report.Root,Bytes=rootFiles.Bytes,Files=rootFiles.Files});
   try{
    foreach(string child in Directory.EnumerateDirectories(report.Root)){
     cancel.ThrowIfCancellationRequested();
     if(report.Partial)break;
     try{if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)continue;}catch(Exception){continue;}
     report.TotalFolders++;
     Sum sum=Scan(child,true,report,onFile,cancel,watch);
     report.Folders.Add(new FolderUsage{Path=child,Bytes=sum.Bytes,Files=sum.Files});
    }
   }catch(UnauthorizedAccessException){report.Notes.Add("Không đủ quyền đọc thư mục gốc.");}
   catch(IOException){report.Notes.Add("Không đọc được toàn bộ thư mục gốc.");}

   report.Folders=report.Folders.OrderByDescending(f=>f.Bytes).Take(MaxFolders).ToList();
   report.Extensions=byExt.Values.OrderByDescending(e=>e.Bytes).Take(MaxExtensions).ToList();
   largest.Sort((a,b)=>b.Bytes.CompareTo(a.Bytes));
   report.LargestFiles=largest.Take(topFiles).ToList();
   return report;
  }

  static DateTime SafeWrite(FileInfo info){try{return info.LastWriteTime;}catch(Exception){return DateTime.MinValue;}}

  static Sum Scan(string start,bool recursive,DiskReport report,Action<FileInfo,long> onFile,CancellationToken cancel,Stopwatch watch){
   long bytes=0;int files=0;
   var stack=new Stack<string>();stack.Push(start);
   while(stack.Count>0){
    cancel.ThrowIfCancellationRequested();
    if(report.TotalFiles>=MaxFiles||watch.Elapsed.TotalSeconds>MaxSeconds){report.Partial=true;break;}
    string current=stack.Pop();
    try{
     foreach(string file in Directory.EnumerateFiles(current)){
      if(report.TotalFiles>=MaxFiles){report.Partial=true;break;}
      FileInfo info;try{info=new FileInfo(file);}catch(Exception){continue;}
      try{if((info.Attributes&FileAttributes.ReparsePoint)!=0)continue;}catch(Exception){continue;}
      long len;try{len=info.Length;}catch(IOException){continue;}
      bytes+=len;files++;onFile(info,len);
     }
     if(recursive){
      foreach(string sub in Directory.EnumerateDirectories(current)){
       try{if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)!=0)continue;}catch(Exception){continue;}
       report.TotalFolders++;stack.Push(sub);
      }
     }
    }catch(UnauthorizedAccessException){report.Notes.Add("Không đủ quyền đọc: "+current);}
    catch(IOException){report.Notes.Add("Bỏ qua thư mục không đọc được: "+current);}
   }
   return new Sum{Bytes=bytes,Files=files};
  }
 }
}
