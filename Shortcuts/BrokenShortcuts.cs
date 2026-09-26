using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using TweekPro.Core;

namespace TweekPro.Shortcuts {
 public sealed class BrokenShortcut { public string Path,Target,Hash; }
 public sealed class ShortcutScan {
  public readonly List<BrokenShortcut> Items=new List<BrokenShortcut>();
  public int Examined,Skipped;public bool Limited;
 }
 public static class BrokenShortcuts {
  const FileAttributes UnsafeAttributes=(FileAttributes)(0x400|0x1000|0x40000|0x400000);

  /// <summary>Only ready fixed disks and fully qualified DOS paths can prove a target missing; never probe UNC, devices or URLs.</summary>
  internal static bool LocalTarget(string path){
   return !String.IsNullOrWhiteSpace(path)&&path.Length>3&&Char.IsLetter(path[0])&&path[1]==':'&&path[2]=='\\'&&path.IndexOf('%')<0&&path.IndexOf(':',2)<0&&!path.Contains("..")&&!path.Contains("/");
  }

  /// <summary>Walks each component so missing files differ from access failures, offline placeholders, junctions and unmounted drives.</summary>
  internal static bool DefinitelyMissing(string path){
   return TargetMissing(path,File.GetAttributes,root=>{var drive=new DriveInfo(root);return drive.DriveType==DriveType.Fixed&&drive.IsReady;});
  }

  /// <summary>Injectable filesystem probe: access errors and offline/reparse ancestors are uncertainty, never absence.</summary>
  internal static bool TargetMissing(string path,Func<string,FileAttributes> attributes,Func<string,bool> readyFixedDrive){
   if(!LocalTarget(path))return false;
   try{
    string current=path.Substring(0,3);if(!readyFixedDrive(current))return false;
    if((attributes(current)&UnsafeAttributes)!=0)return false;
    foreach(string part in path.Substring(3).Split('\\')){
     if(String.IsNullOrEmpty(part)||part==".")return false;current=current.TrimEnd('\\')+"\\"+part;
     try{if((attributes(current)&UnsafeAttributes)!=0)return false;}
     catch(FileNotFoundException){return true;}
     catch(DirectoryNotFoundException){return true;}
    }
   }catch(Exception){return false;}
   return false;
  }

  /// <summary>Rejects malformed and MSI-advertised links; their missing target can be intentional (install on demand).</summary>
  internal static bool OrdinaryLink(byte[] header){
   return header.Length>=76&&BitConverter.ToUInt32(header,0)==76&&new Guid(header.Skip(4).Take(16).ToArray())==new Guid("00021401-0000-0000-c000-000000000046")&&(BitConverter.ToUInt32(header,20)&0x1000)==0;
  }

  /// <summary>Inspects a link without launching or resolving it; unknown/unsupported targets never become cleanup candidates.</summary>
  internal static BrokenShortcut Inspect(string file){
   Engine.NoLinks(file,false);if((File.GetAttributes(file)&UnsafeAttributes)!=0)return null;
   if(!String.Equals(Path.GetExtension(file),".lnk",StringComparison.OrdinalIgnoreCase))return null;
   using(var stream=File.OpenRead(file)){var header=new byte[76];if(stream.Read(header,0,header.Length)!=header.Length||!OrdinaryLink(header))return null;}
   string hash=Advanced.FileHash(file),target=Advanced.ShortcutTarget(file);
   if(!DefinitelyMissing(target)||hash!=Advanced.FileHash(file))return null;
   return new BrokenShortcut{Path=Engine.Canon(file),Target=target,Hash=hash};
  }

  /// <summary>Bounded read-only scan of the account's and all-users' Start Menu/Desktop/Startup folders.</summary>
  public static ShortcutScan Scan(CancellationToken token){return ScanRoots(Advanced.ShortcutRoots(),token);}
  internal static ShortcutScan ScanRoots(IEnumerable<string> roots,CancellationToken token){
   var report=new ShortcutScan();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var clock=Stopwatch.StartNew();int entries=0;
   var pending=new Stack<Tuple<string,int>>(roots.Where(r=>!String.IsNullOrWhiteSpace(r)).Select(r=>Tuple.Create(r,0)));
   while(pending.Count>0){
    token.ThrowIfCancellationRequested();var node=pending.Pop();
    try{
     string dir=Engine.Canon(node.Item1);if(!seen.Add(dir))continue;
     Engine.NoLinks(dir,false);if((File.GetAttributes(dir)&UnsafeAttributes)!=0){report.Skipped++;continue;}
     foreach(string entry in Directory.EnumerateFileSystemEntries(dir)){
      token.ThrowIfCancellationRequested();if(++entries>20000||clock.Elapsed.TotalSeconds>30){report.Limited=true;return report;}
      try{
       var attr=File.GetAttributes(entry);if((attr&UnsafeAttributes)!=0){report.Skipped++;continue;}
       if((attr&FileAttributes.Directory)!=0){if(node.Item2<16)pending.Push(Tuple.Create(entry,node.Item2+1));else report.Limited=true;continue;}
       if(!String.Equals(Path.GetExtension(entry),".lnk",StringComparison.OrdinalIgnoreCase))continue;
       report.Examined++;var item=Inspect(entry);if(item!=null)report.Items.Add(item);
      }catch(IOException){report.Skipped++;}catch(UnauthorizedAccessException){report.Skipped++;}catch(System.Security.SecurityException){report.Skipped++;}
     }
    }catch(IOException){report.Skipped++;}catch(UnauthorizedAccessException){report.Skipped++;}catch(System.Security.SecurityException){report.Skipped++;}
   }
   return report;
  }

  /// <summary>Revalidates the real allowlist, snapshot hash and absent target, then atomically moves only the .lnk to the existing file vault.</summary>
  public static Backup Store(BrokenShortcut item){
   if(item==null)throw new ArgumentNullException("item");Advanced.ValidateFile(item.Path);
   var fresh=Inspect(item.Path);
   if(fresh==null||fresh.Hash!=item.Hash||!String.Equals(fresh.Target,item.Target,StringComparison.OrdinalIgnoreCase))throw new IOException(L.T("Shortcut đã thay đổi hoặc đích không còn được xác nhận là mất. Hãy quét lại."));
   Engine.NoLinks(Engine.Vault,false);
   if(!String.Equals(Path.GetPathRoot(fresh.Path),Path.GetPathRoot(Engine.Canon(Engine.Vault)),StringComparison.OrdinalIgnoreCase))throw new IOException(L.T("Shortcut và Kho khôi phục phải cùng ổ đĩa; mục này được giữ nguyên."));
   var b=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Original=fresh.Path,Kind="File",AppName=Path.GetFileNameWithoutExtension(fresh.Path),Purpose="BrokenShortcut",Payload="content"};
   Directory.CreateDirectory(Path.Combine(Engine.Vault,b.Id));Engine.SaveBackup(b);
   try{
    Advanced.ValidateFile(fresh.Path);
    if(Advanced.FileHash(fresh.Path)!=fresh.Hash||!DefinitelyMissing(fresh.Target))throw new IOException(L.T("Shortcut đã thay đổi hoặc đích không còn được xác nhận là mất. Hãy quét lại."));
    File.Move(fresh.Path,Path.Combine(Engine.Vault,b.Id,"content"));b.State="BackedUp";Engine.SaveBackup(b);return b;
   }catch(Exception e){b.State="NeedsReview";b.Error=e.Message;Engine.SaveBackup(b);throw;}
  }
 }
}
