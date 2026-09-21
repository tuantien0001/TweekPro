using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TweekPro.Core;

namespace TweekPro.Stale {
 /// <summary>
 /// Platform-neutral self-tests for the stale-file cleaner: classification, age, allow/deny boundaries, scan filtering,
 /// quarantine to the vault, restore, direct delete and translations. Fixtures live under %LOCALAPPDATA%\Temp\TweekProStale*.
 /// </summary>
 public static class StaleTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("StaleTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}
  const long MB=1024L*1024;

  public static void Run(){
   // Classification and age are pure functions.
   Assert(StaleSafety.Classify(@"C:\d\setup.exe",10,0)==StaleKind.Installer&&StaleSafety.Classify(@"C:\d\pkg.MSIX",10,0)==StaleKind.Installer,"installer extensions (case-insensitive)");
   Assert(StaleSafety.Classify(@"C:\d\a.zip",10,0)==StaleKind.Archive&&StaleSafety.Classify(@"C:\d\w.iso",10,0)==StaleKind.DiskImage&&StaleSafety.Classify(@"C:\d\x.crdownload",10,0)==StaleKind.Partial,"archive/image/partial");
   Assert(StaleSafety.Classify(@"C:\d\video.mp4",300*MB,200*MB)==StaleKind.Large&&StaleSafety.Classify(@"C:\d\video.mp4",100*MB,200*MB)==null&&StaleSafety.Classify(@"C:\d\video.mp4",900*MB,0)==null,"large threshold; 0 disables large");
   Assert(StaleSafety.Classify(@"C:\d\app.lnk",900*MB,1)==null&&StaleSafety.Classify(@"C:\d\desktop.ini",900*MB,1)==null&&StaleSafety.Classify(@"C:\d\notes.txt",10,0)==null,"shortcuts, ini and plain documents never stale");
   var now=new DateTime(2026,9,21,12,0,0);
   Assert(StaleSafety.AgeDays(now.AddDays(-40),now.AddDays(-10),now)==10&&StaleSafety.AgeDays(now.AddDays(-3),now.AddDays(-30),now)==3&&StaleSafety.AgeDays(now.AddDays(5),now.AddDays(-30),now)==0,"age uses the newer stamp and clamps the future to zero");

   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   string baseDir=Path.Combine(local,"Temp","TweekProStale"+Guid.NewGuid().ToString("N"));
   string downloads=Path.Combine(baseDir,"Downloads"),desktop=Path.Combine(baseDir,"Desktop"),documents=Path.Combine(baseDir,"Documents");
   string originalVault=Engine.Vault;
   try{
    Directory.CreateDirectory(Path.Combine(downloads,"sub"));Directory.CreateDirectory(desktop);Directory.CreateDirectory(documents);
    Engine.Vault=Path.Combine(baseDir,"Vault");Directory.CreateDirectory(Engine.Vault);
    var allowed=new List<string>{Engine.Canon(downloads),Engine.Canon(desktop)};
    var forbidden=new List<string>{Engine.Canon(documents),Engine.Canon(Engine.Vault),Engine.Canon(Path.Combine(downloads,"OneDrive"))};
    Directory.CreateDirectory(Path.Combine(downloads,"OneDrive"));

    // Boundaries: only Downloads/Desktop (or folders under them) are accepted; Documents, the vault, the drive root and a cloud folder are refused.
    StaleSafety.ValidateRoot(downloads,allowed,forbidden);StaleSafety.ValidateRoot(Path.Combine(downloads,"sub"),allowed,forbidden);
    MustFail(()=>StaleSafety.ValidateRoot(documents,allowed,forbidden),"Documents refused");
    MustFail(()=>StaleSafety.ValidateRoot(baseDir,allowed,forbidden),"parent of Downloads refused");
    MustFail(()=>StaleSafety.ValidateRoot(Path.GetPathRoot(baseDir),allowed,forbidden),"drive root refused");
    MustFail(()=>StaleSafety.ValidateRoot(Engine.Vault,allowed,forbidden),"vault refused");
    MustFail(()=>StaleSafety.ValidateRoot(Path.Combine(downloads,"OneDrive"),allowed,forbidden),"cloud folder under Downloads refused");
    MustFail(()=>StaleSafety.ValidateRemovable(Path.Combine(documents,"x.exe"),allowed,forbidden),"file under Documents not removable");
    MustFail(()=>StaleSafety.ValidateRemovable(Path.Combine(downloads,"x.lnk"),allowed,forbidden),"shortcut never removable");
    MustFail(()=>StaleSafety.ValidateRemovable(Path.Combine(downloads,"OneDrive","x.exe"),allowed,forbidden),"file inside cloud folder not removable");

    string oldExe=Path.Combine(downloads,"old-setup.exe"),newExe=Path.Combine(downloads,"fresh-setup.exe"),oldZip=Path.Combine(downloads,"sub","archive.zip");
    string oldBig=Path.Combine(downloads,"movie.bin"),oldSmall=Path.Combine(downloads,"small.bin"),oldTxt=Path.Combine(downloads,"notes.txt"),partial=Path.Combine(desktop,"Unconfirmed.crdownload");
    string cloud=Path.Combine(downloads,"OneDrive","cloud.exe"),hidden=Path.Combine(downloads,"hidden.exe"),doc=Path.Combine(documents,"doc.exe");
    foreach(string p in new[]{oldExe,newExe,oldZip,oldSmall,oldTxt,partial,cloud,hidden,doc})File.WriteAllBytes(p,new byte[64]);
    File.WriteAllBytes(oldBig,new byte[3000]);
    DateTime old=DateTime.Now.AddDays(-45),fresh=DateTime.Now.AddDays(-2);
    foreach(string p in new[]{oldExe,oldZip,oldBig,oldSmall,oldTxt,partial,cloud,hidden,doc}){File.SetCreationTime(p,old);File.SetLastWriteTime(p,old);}
    File.SetCreationTime(newExe,fresh);File.SetLastWriteTime(newExe,fresh);
    File.SetAttributes(hidden,FileAttributes.Hidden);

    var result=StaleFinder.Scan(new[]{downloads,desktop,downloads},30,2000,DateTime.Now,allowed,forbidden,CancellationToken.None);
    var names=result.Files.Select(f=>f.Name).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToArray();
    Assert(names.SequenceEqual(new[]{"archive.zip","movie.bin","old-setup.exe","Unconfirmed.crdownload"}),"scan picks old installer, nested archive, large file and partial only: "+String.Join(",",names));
    Assert(result.Roots.Count==2&&result.Files.All(f=>f.AgeDays>=44&&f.AgeDays<=46),"duplicate root collapsed; ages around 45 days");
    Assert(result.Files[0].Name=="movie.bin"&&result.Files[0].Kind==StaleKind.Large&&result.TotalBytes==3000+64*3,"largest first; totals summed");
    Assert(!result.Files.Any(f=>f.Name=="fresh-setup.exe"||f.Name=="hidden.exe"||f.Name=="cloud.exe"||f.Name=="doc.exe"||f.Name=="notes.txt"||f.Name=="small.bin"),"fresh, hidden, cloud, documents, plain and small files excluded");
    Assert(StaleFinder.Scan(new[]{downloads},0,0,DateTime.Now,allowed,forbidden,CancellationToken.None).Files.Count==3,"age 0 and large disabled: installers/archives only (3 in Downloads)");
    MustFail(()=>StaleFinder.Scan(new[]{documents},0,0,DateTime.Now,allowed,forbidden,CancellationToken.None),"scan of Documents refused");

    // Vault mode: files move into one Stale backup; a file changed after the scan is left alone; restore puts them back.
    var target=result.Files.Where(f=>f.Name!="movie.bin").ToList();
    var changed=result.Files.First(f=>f.Name=="movie.bin");File.WriteAllBytes(oldBig,new byte[3001]);
    var report=StaleFinder.Quarantine(target.Concat(new[]{changed}),false,allowed,forbidden,CancellationToken.None);
    Assert(report.Quarantined==3&&report.Failed==1&&report.Backups.Count==1&&report.Bytes==64*3,"three moved, one refused as changed: q="+report.Quarantined+" f="+report.Failed);
    Assert(!File.Exists(oldExe)&&!File.Exists(oldZip)&&!File.Exists(partial)&&File.Exists(oldBig)&&File.Exists(newExe),"stale files gone, changed and fresh files kept");
    var backup=Engine.Backups().FirstOrDefault(b=>b.Id==report.Backups[0].Id);
    Assert(backup!=null&&backup.Kind==StaleFinder.BackupKind&&backup.State=="BackedUp"&&File.Exists(Path.Combine(Engine.Vault,backup.Id,"files.xml")),"Stale backup listed with index");
    Engine.Restore(backup);
    Assert(File.Exists(oldExe)&&File.Exists(oldZip)&&File.Exists(partial)&&backup.State=="Restored","restore via Engine dispatch puts every file back");
    var again=StaleFinder.Quarantine(new[]{result.Files.First(f=>f.Name=="old-setup.exe")},false,allowed,forbidden,CancellationToken.None);
    Assert(again.Quarantined==1,"re-quarantine after restore works (stamps preserved by move)");
    File.WriteAllBytes(oldExe,new byte[1]);
    Engine.Restore(again.Backups[0]);
    Assert(again.Backups[0].State=="Restored"&&again.Backups[0].Error.Contains("Bỏ qua 1")&&new FileInfo(oldExe).Length==1,"restore never overwrites an existing destination; it is skipped and reported");
   }finally{Engine.Vault=originalVault;try{Directory.Delete(baseDir,true);}catch(Exception){}}

   // Direct mode deletes without a backup; in-use files are skipped rather than failed.
   string dir2=Path.Combine(local,"Temp","TweekProStale"+Guid.NewGuid().ToString("N"),"Downloads");
   try{
    Directory.CreateDirectory(dir2);var allowed2=new List<string>{Engine.Canon(dir2)};var forbidden2=new List<string>();
    string a=Path.Combine(dir2,"a.msi"),b=Path.Combine(dir2,"b.7z");
    File.WriteAllBytes(a,new byte[10]);File.WriteAllBytes(b,new byte[10]);
    foreach(string p in new[]{a,b}){File.SetCreationTime(p,DateTime.Now.AddDays(-90));File.SetLastWriteTime(p,DateTime.Now.AddDays(-90));}
    var scan=StaleFinder.Scan(new[]{dir2},30,0,DateTime.Now,allowed2,forbidden2,CancellationToken.None);
    Assert(scan.Files.Count==2,"two direct-mode candidates");
    StaleReport direct;
    using(new FileStream(b,FileMode.Open,FileAccess.ReadWrite,FileShare.None))direct=StaleFinder.Quarantine(scan.Files,true,allowed2,forbidden2,CancellationToken.None);
    Assert(direct.Direct&&direct.Quarantined==1&&direct.SkippedInUse==1&&direct.Backups.Count==0&&!File.Exists(a)&&File.Exists(b),"direct delete removed the free file, skipped the locked one, no backup");
   }finally{try{Directory.Delete(Path.GetDirectoryName(dir2),true);}catch(Exception){}}

   // Settings written before this feature existed must still yield the documented defaults (DataContract skips field initializers).
   string settingsFile=Path.Combine(Path.GetTempPath(),"tweekpro-stale-"+Guid.NewGuid().ToString("N")+".json");
   try{
    File.WriteAllText(settingsFile,"{\"version\":1,\"purgeDefaultDays\":45}");
    var legacy=Settings.Load(settingsFile);
    Assert(legacy.StaleMinAgeDays==30&&legacy.StaleLargeMB==200&&!legacy.StaleIncludeDesktop&&legacy.PurgeDefaultDays==45,"old settings file gets stale defaults: "+legacy.StaleMinAgeDays+"/"+legacy.StaleLargeMB);
    legacy.StaleMinAgeDays=7;legacy.StaleLargeMB=50;legacy.StaleIncludeDesktop=true;legacy.Save(settingsFile);
    var round=Settings.Load(settingsFile);Assert(round.StaleMinAgeDays==7&&round.StaleLargeMB==50&&round.StaleIncludeDesktop,"stale settings round-trip");
    File.WriteAllText(settingsFile,"{\"staleMinAgeDays\":-5,\"staleLargeMB\":99999999}");var clamped=Settings.Load(settingsFile);
    Assert(clamped.StaleMinAgeDays==0&&clamped.StaleLargeMB==1048576,"stale settings clamped");
   }finally{try{File.Delete(settingsFile);}catch(Exception){}}

   // Translations: every kind label and the tab title exist in English.
   foreach(StaleKind k in Enum.GetValues(typeof(StaleKind)))Assert(L.Has(StaleFinder.KindLabel(k)),"kind label translated: "+k);
   Assert(L.Has("Tệp tải về cũ")&&Branding.GlyphKey("Tệp tải về cũ")=="download"&&Branding.GlyphKey("Old Downloads")=="download","tab title translated with glyph");
  }
 }
}
