using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using TweekPro.Cleaner;

namespace TweekPro.Dupes {
 /// <summary>
 /// Platform-neutral self-tests for the duplicate finder: keeper selection, size+hash grouping, protected-folder skipping,
 /// quarantine to the vault, restore and permanent purge. Fixtures live under %LOCALAPPDATA%\Temp\TweekProDupe*.
 /// </summary>
 public static class DupeTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("DupeTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   KeeperSelection();
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   string baseDir=Path.Combine(local,"Temp","TweekProDupe"+Guid.NewGuid().ToString("N"));
   string scanRoot=Path.Combine(baseDir,"scan");
   string originalVault=Engine.Vault;
   try{
    Directory.CreateDirectory(Path.Combine(scanRoot,"a"));Directory.CreateDirectory(Path.Combine(scanRoot,"b"));Directory.CreateDirectory(Path.Combine(scanRoot,"c"));
    // Put the vault under the scan root to prove the walk refuses to enter protected areas.
    Engine.Vault=Path.Combine(scanRoot,"TweekProVault");Directory.CreateDirectory(Engine.Vault);

    byte[] payload=Encoding.UTF8.GetBytes(new string('D',4096));
    string keeper=Path.Combine(scanRoot,"a","first.bin"),dupe1=Path.Combine(scanRoot,"b","second.bin"),dupe2=Path.Combine(scanRoot,"c","third.bin");
    string unique=Path.Combine(scanRoot,"unique.bin"),tiny=Path.Combine(scanRoot,"tiny.bin"),inVault=Path.Combine(Engine.Vault,"copy.bin");
    File.WriteAllBytes(keeper,payload);File.WriteAllBytes(dupe1,payload);File.WriteAllBytes(dupe2,payload);
    File.WriteAllBytes(unique,Encoding.UTF8.GetBytes(new string('U',4096)));
    File.WriteAllBytes(tiny,new byte[16]);File.WriteAllBytes(inVault,payload);
    // The keeper has the oldest last-write time so keep-oldest selects it (creation time is unreliable on Mono).
    File.SetLastWriteTime(keeper,DateTime.Now.AddDays(-5));File.SetLastWriteTime(dupe1,DateTime.Now.AddDays(-2));File.SetLastWriteTime(dupe2,DateTime.Now.AddDays(-1));

    // Safety: drive root, missing folder and the protected vault are all refused; a real folder is accepted.
    MustFail(()=>DupeSafety.ValidateScanRoot(Path.GetPathRoot(scanRoot)),"Drive root refused");
    MustFail(()=>DupeSafety.ValidateScanRoot(Path.Combine(baseDir,"missing")),"Missing folder refused");
    MustFail(()=>DupeSafety.ValidateScanRoot(Engine.Vault),"Vault refused");
    DupeSafety.ValidateScanRoot(scanRoot);

    var result=DuplicateFinder.Scan(scanRoot,1024,CancellationToken.None);
    Assert(result.Groups.Count==1,"One duplicate group found: "+result.Groups.Count);
    var group=result.Groups[0];
    Assert(group.Count==3&&group.Bytes==4096&&group.Wasted==8192,"Group has three 4 KB copies wasting 8 KB");
    Assert(result.FilesScanned==4&&result.Hashed==4,"tiny.bin below threshold and the vault copy are excluded: scanned="+result.FilesScanned+" hashed="+result.Hashed);
    Assert(result.WastedBytes==8192,"Wasted bytes reported");
    Assert(group.Files.Any(f=>f.Path.Equals(keeper,StringComparison.OrdinalIgnoreCase))&&!group.Files.Any(f=>f.Path.Equals(inVault,StringComparison.OrdinalIgnoreCase)),"Vault copy never scanned");
    Assert(group.Keeper.Path.Equals(keeper,StringComparison.OrdinalIgnoreCase),"Oldest copy kept: "+group.Keeper.Path);

    // Vault mode moves the two redundant copies and keeps the keeper; the run produces one restorable backup.
    var report=DuplicateFinder.Quarantine(scanRoot,new[]{group},false,CancellationToken.None);
    Assert(report.Quarantined==2&&report.Backups.Count==1,"Two copies quarantined into one backup");
    Assert(File.Exists(keeper)&&!File.Exists(dupe1)&&!File.Exists(dupe2)&&File.Exists(unique),"Keeper and unique kept; duplicates removed");
    var backup=Engine.Backups().FirstOrDefault(b=>b.Id==report.Backups[0].Id);
    Assert(backup!=null&&backup.Kind=="Duplicate"&&backup.State=="BackedUp"&&File.Exists(Path.Combine(Engine.Vault,backup.Id,"files.xml")),"Duplicate backup listed with index");
    Assert(Engine.BackupSize(backup)>=8192,"Backup size measured");

    Engine.Restore(backup);
    Assert(File.Exists(dupe1)&&File.Exists(dupe2)&&backup.State=="Restored","Restore puts both copies back");
    long freed=Engine.Purge(backup);
    Assert(freed>=0&&!Directory.Exists(Path.Combine(Engine.Vault,backup.Id)),"Purge removes the backup folder");

    // Direct mode deletes the redundant copies without any backup.
    result=DuplicateFinder.Scan(scanRoot,1024,CancellationToken.None);
    group=result.Groups[0];group.Keeper=DuplicateFinder.SelectKeeper(group.Files,false);
    report=DuplicateFinder.Quarantine(scanRoot,new[]{group},true,CancellationToken.None);
    Assert(report.Quarantined==2&&report.Backups.Count==0,"Direct mode deletes without backup");
    Assert(File.Exists(group.Keeper.Path)&&Engine.Backups().Count(b=>b.Kind=="Duplicate")==0,"Keeper survives; no duplicate backup created");

    // A copy held open is skipped, never deleted.
    File.WriteAllBytes(dupe1,payload);File.WriteAllBytes(dupe2,payload);
    result=DuplicateFinder.Scan(scanRoot,1024,CancellationToken.None);group=result.Groups[0];
    var victim=group.Redundant.First();
    using(new FileStream(victim.Path,FileMode.Open,FileAccess.Read,FileShare.Read)){
     report=DuplicateFinder.Quarantine(scanRoot,new[]{group},true,CancellationToken.None);
     Assert(report.SkippedInUse>=1&&File.Exists(victim.Path),"In-use duplicate skipped");
    }

    // Stale snapshot guard: a copy edited after the scan is no longer a duplicate and must survive both modes.
    File.WriteAllBytes(dupe1,payload);File.WriteAllBytes(dupe2,payload);
    result=DuplicateFinder.Scan(scanRoot,1024,CancellationToken.None);group=result.Groups[0];
    var edited=group.Redundant.First();var other=group.Redundant.Skip(1).First();
    var changed=(byte[])payload.Clone();changed[0]^=0xFF;File.WriteAllBytes(edited.Path,changed);File.SetLastWriteTime(edited.Path,DateTime.Now.AddMinutes(5));
    Assert(!DuplicateFinder.Unchanged(edited,group.Bytes)&&DuplicateFinder.Unchanged(other,group.Bytes),"Unchanged detects the edited copy only");
    report=DuplicateFinder.Quarantine(scanRoot,new[]{group},true,CancellationToken.None);
    Assert(File.Exists(edited.Path)&&!File.Exists(other.Path)&&report.Quarantined==1&&report.Failed==1&&report.Errors.Any(e=>e.Contains("đã thay đổi")),"Edited copy kept, untouched copy removed, failure reported");

    // Same-size rewrite with identical timestamp still fails the direct-mode rehash.
    File.WriteAllBytes(dupe1,payload);File.WriteAllBytes(dupe2,payload);
    result=DuplicateFinder.Scan(scanRoot,1024,CancellationToken.None);group=result.Groups[0];
    var tampered=group.Redundant.First();File.WriteAllBytes(tampered.Path,changed);tampered.LastWrite=new FileInfo(tampered.Path).LastWriteTime;
    Assert(DuplicateFinder.Unchanged(tampered,group.Bytes),"Size and timestamp look unchanged, only the hash differs");
    report=DuplicateFinder.Quarantine(scanRoot,new[]{group},true,CancellationToken.None);
    Assert(File.Exists(tampered.Path)&&report.Errors.Any(e=>e.Contains("không còn trùng")),"Direct mode re-hashes and refuses the tampered copy");

    // Missing keeper: the whole group is skipped rather than deleting the remaining copies.
    File.WriteAllBytes(dupe1,payload);File.WriteAllBytes(dupe2,payload);
    result=DuplicateFinder.Scan(scanRoot,1024,CancellationToken.None);group=result.Groups[0];
    File.Delete(group.Keeper.Path);
    report=DuplicateFinder.Quarantine(scanRoot,new[]{group},false,CancellationToken.None);
    Assert(report.Quarantined==0&&report.Failed==1&&group.Redundant.All(f=>File.Exists(f.Path))&&report.Backups.Count==0,"Group without its keeper is skipped entirely");

    // Group handed over without a keeper gets one chosen instead of losing every copy.
    File.WriteAllBytes(keeper,payload);File.WriteAllBytes(dupe1,payload);File.WriteAllBytes(dupe2,payload);
    result=DuplicateFinder.Scan(scanRoot,1024,CancellationToken.None);group=result.Groups[0];group.Keeper=null;
    report=DuplicateFinder.Quarantine(scanRoot,new[]{group},false,CancellationToken.None);
    Assert(group.Keeper!=null&&File.Exists(group.Keeper.Path)&&report.Quarantined==2,"Keeper auto-selected when missing");
    foreach(var b in Engine.Backups().Where(x=>x.Kind=="Duplicate"))Engine.Purge(b);
   }finally{
    Engine.Vault=originalVault;
    if(Path.GetFileName(baseDir).StartsWith("TweekProDupe")&&Engine.Under(Engine.Canon(baseDir),Engine.Canon(Path.Combine(local,"Temp")))&&Directory.Exists(baseDir))Directory.Delete(baseDir,true);
   }
  }

  /// <summary>Keeper selection is deterministic: oldest or newest first, then shorter path, then ordinal path.</summary>
  static void KeeperSelection(){
   var older=new DuplicateFile{Path="/data/z/older.bin",Created=new DateTime(2020,1,1),LastWrite=new DateTime(2020,1,1)};
   var newer=new DuplicateFile{Path="/data/a/newer.bin",Created=new DateTime(2023,1,1),LastWrite=new DateTime(2023,1,1)};
   Assert(ReferenceEquals(DuplicateFinder.SelectKeeper(new[]{newer,older},false),older),"Keep oldest by default");
   Assert(ReferenceEquals(DuplicateFinder.SelectKeeper(new[]{older,newer},true),newer),"Keep newest on request");
   DateTime same=new DateTime(2022,6,1);
   var longPath=new DuplicateFile{Path="/data/aa/longer-name.bin",Created=same,LastWrite=same};
   var shortPath=new DuplicateFile{Path="/data/a/x.bin",Created=same,LastWrite=same};
   Assert(ReferenceEquals(DuplicateFinder.SelectKeeper(new[]{longPath,shortPath},false),shortPath),"Ties break to the shorter path");
  }
 }
}
