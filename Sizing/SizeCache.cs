using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TweekPro.Sizing {
 /// <summary>One remembered folder measurement.</summary>
 public class SizeCacheEntry { public string Folder; public long Bytes; public bool Partial; public DateTime MeasuredAt; }

 /// <summary>
 /// Remembers install-folder measurements so the folder walk — the single largest CPU cost at startup — runs at most once per
 /// MaxAge per folder instead of on every launch and every Reload. Plain XML list in the data folder; any read or write failure
 /// simply falls back to measuring again.
 /// </summary>
 public static class SizeCache {
  public static TimeSpan MaxAge=TimeSpan.FromHours(24), Keep=TimeSpan.FromDays(30);
  /// <summary>Tests point this at a temp file so they never touch the user's cache.</summary>
  public static string FileOverride;
  static List<SizeCacheEntry> entries;static bool dirty;static readonly object gate=new object();

  public static string File { get { return FileOverride??Path.Combine(Core.Paths.Root,"install-sizes.xml"); } }

  static List<SizeCacheEntry> Entries(){
   if(entries!=null)return entries;
   try{entries=System.IO.File.Exists(File)?Engine.Load<List<SizeCacheEntry>>(File):new List<SizeCacheEntry>();}catch(Exception){entries=new List<SizeCacheEntry>();}
   return entries;
  }

  /// <summary>Cached bytes for the folder when measured within MaxAge of now.</summary>
  public static bool TryGet(string folder,DateTime now,out long bytes,out bool partial){
   bytes=-1;partial=false;if(String.IsNullOrWhiteSpace(folder))return false;
   lock(gate){
    var e=Entries().FirstOrDefault(x=>String.Equals(x.Folder,folder,StringComparison.OrdinalIgnoreCase));
    if(e==null||now-e.MeasuredAt>MaxAge||e.MeasuredAt>now.AddMinutes(5))return false;
    bytes=e.Bytes;partial=e.Partial;return true;
   }
  }

  public static void Put(string folder,long bytes,bool partial,DateTime now){
   if(String.IsNullOrWhiteSpace(folder)||bytes<0)return;
   lock(gate){
    var list=Entries();list.RemoveAll(x=>String.Equals(x.Folder,folder,StringComparison.OrdinalIgnoreCase));
    list.Add(new SizeCacheEntry{Folder=folder,Bytes=bytes,Partial=partial,MeasuredAt=now});dirty=true;
   }
  }

  /// <summary>Writes the cache if anything changed, dropping entries older than Keep.</summary>
  public static void Flush(DateTime now){
   lock(gate){
    if(!dirty||entries==null)return;
    entries.RemoveAll(x=>now-x.MeasuredAt>Keep);
    try{Directory.CreateDirectory(Path.GetDirectoryName(File));Engine.Save(File,entries);dirty=false;}catch(Exception e){Core.Log.Warn("Size cache not saved: "+e.Message);}
   }
  }

  /// <summary>Forgets the in-memory copy so the next call re-reads the file (tests switch files between runs).</summary>
  public static void Reset(){lock(gate){entries=null;dirty=false;}}
 }
}
