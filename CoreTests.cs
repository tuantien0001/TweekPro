using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TweekPro.Cleaner;
using TweekPro.Core;

namespace TweekPro {
 /// <summary>
 /// Platform-neutral self-tests for non-UI logic: junk rule parsing and matching, purge selection, data-dir migration,
 /// settings and log rotation. They only touch randomly named folders under the temp directory.
 /// </summary>
 public static class CoreTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("CoreTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   Rules();Purge();Migration();SettingsRoundTrip();LogRotation();
  }

  static void Rules(){
   var set=JunkRules.LoadEmbedded();
   Assert(set.Rules.Count>=8,"Embedded rules present");
   Assert(set.Rules.Select(r=>r.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==set.Rules.Count,"Unique rule ids");
   Assert(set.Rules.Any(r=>r.Id=="chrome-cache"&&r.BlockedProcesses.Contains("chrome")),"Browser rule blocks its process");
   Assert(set.Rules.All(r=>r.Paths.Count>0&&r.Patterns.Count>0),"Every rule has paths and patterns");
   var json="{\"version\":1,\"rules\":[{\"id\":\"a\",\"name\":\"A\",\"paths\":[\"%TEMP%\"],\"patterns\":[\"*.tmp\"]},{\"id\":\"a\",\"name\":\"B\",\"paths\":[\"%TEMP%\"]}]}";
   MustFail(()=>JunkRules.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json))),"Duplicate ids rejected");
   MustFail(()=>JunkRules.Parse(new MemoryStream(Encoding.UTF8.GetBytes("{\"version\":1,\"rules\":[{\"id\":\"x\",\"paths\":[\"%TEMP%\"],\"patterns\":[\"..\\\\*\"]}]}"))),"Pattern with path separator rejected");
   var parsed=JunkRules.Parse(new MemoryStream(Encoding.UTF8.GetBytes("{\"version\":1,\"rules\":[{\"id\":\"x\",\"paths\":[\"%TEMP%\"]}]}")));
   Assert(parsed.Rules[0].Patterns.SequenceEqual(new[]{"*"})&&parsed.Rules[0].Name=="x"&&parsed.Rules[0].Group=="Khác","Defaults applied to sparse rule");
   var patterns=new[]{"thumbcache_*.db","*.dmp"}.Select(JunkRules.GlobToRegex).ToList();
   Assert(JunkRules.MatchesName(patterns,"thumbcache_256.db")&&JunkRules.MatchesName(patterns,"APP.DMP")&&!JunkRules.MatchesName(patterns,"thumbcache_256.db.bak")&&!JunkRules.MatchesName(patterns,"notes.txt"),"Glob matching");
   Assert(JunkRules.ExpandPath("%UNDEFINED_TWEEKPRO_VAR%\\x").Count==0,"Unresolved variables yield no roots");
   Assert(JunkRules.ExpandPath("").Count==0,"Empty template yields no roots");
   Assert(JunkSafety.HasCacheSegment(@"C:\U\AppData\Local\Google\Chrome\User Data\Default\Cache\Cache_Data",@"C:\U\AppData\Local\Google\Chrome\User Data"),"Cache segment detected below profile");
   Assert(!JunkSafety.HasCacheSegment(@"C:\U\AppData\Local\Google\Chrome\User Data\Default\Login Data",@"C:\U\AppData\Local\Google\Chrome\User Data"),"Profile data is not cache");
   Assert(JunkCleaner.StoredName(7,@"C:\Temp\a:b?.tmp")=="000007_a_b_.tmp","Stored name sanitised");
   Assert(Engine.Under(@"C:\Users\a\AppData\Local\Temp\x.tmp",@"C:\Users\a\AppData\Local\Temp")&&!Engine.Under(@"C:\Users\a\AppData\Local\TempX\x.tmp",@"C:\Users\a\AppData\Local\Temp"),"Under boundary");
   // A real temp fixture: files older than the age threshold qualify, fresh files and links do not.
   string fixture=Path.Combine(Path.GetTempPath(),"TweekProCore"+Guid.NewGuid().ToString("N"));
   try{
    Directory.CreateDirectory(Path.Combine(fixture,"sub"));
    string old=Path.Combine(fixture,"sub","old.tmp");string fresh=Path.Combine(fixture,"fresh.tmp");
    File.WriteAllText(old,"old");File.WriteAllText(fresh,"fresh");
    File.SetCreationTime(old,DateTime.Now.AddDays(-3));File.SetLastWriteTime(old,DateTime.Now.AddDays(-3));File.SetLastAccessTime(old,DateTime.Now.AddDays(-3));
    // Creation time cannot be back-dated on Linux/Mono, so the positive age check only runs on Windows.
    if(Environment.OSVersion.Platform==PlatformID.Win32NT)Assert(JunkSafety.AcceptFile(new FileInfo(old),fixture,24,DateTime.Now),"Old file accepted");
    Assert(!JunkSafety.AcceptFile(new FileInfo(fresh),fixture,24,DateTime.Now),"Fresh file skipped");
    Assert(JunkSafety.AcceptFile(new FileInfo(fresh),fixture,0,DateTime.Now),"Age filter disabled accepts fresh file");
    Assert(!JunkSafety.AcceptFile(new FileInfo(Path.Combine(fixture,"desktop.ini")),fixture,0,DateTime.Now),"desktop.ini never accepted");
    Assert(JunkCleaner.RemoveEmptyDirectories(fixture)==0,"Non-empty subfolder kept");
    File.Delete(old);Assert(JunkCleaner.RemoveEmptyDirectories(fixture)==1&&Directory.Exists(fixture),"Empty subfolder removed, root kept");
   }finally{if(Directory.Exists(fixture))Directory.Delete(fixture,true);}
  }

  static void Purge(){
   DateTime now=new DateTime(2026,9,20,12,0,0);
   var list=new List<Backup>{
    new Backup{Id="1",Created=now.AddDays(-100).ToString("s"),State="Restored"},
    new Backup{Id="2",Created=now.AddDays(-100).ToString("s"),State="BackedUp"},
    new Backup{Id="3",Created=now.AddDays(-10).ToString("s"),State="Restored"},
    new Backup{Id="4",Created="garbage",State="Restored"},
    new Backup{Id="5",Created=now.AddDays(-100).ToString("s"),State="NeedsReview"}
   };
   var restoredOnly=Engine.SelectForPurge(list,90,now,false);
   Assert(restoredOnly.Select(b=>b.Id).SequenceEqual(new[]{"1"}),"Age filter keeps only old restored backups");
   var everything=Engine.SelectForPurge(list,90,now,true);
   Assert(everything.Select(b=>b.Id).OrderBy(x=>x).SequenceEqual(new[]{"1","2","5"}),"Unrestored included on request; unparsable dates never selected");
   Assert(Engine.SelectForPurge(list,0,now,true).Count==4,"Zero days selects every dated backup");
   Assert(Engine.SelectForPurge(list,1000,now,true).Count==0,"Nothing old enough");
   MustFail(()=>Engine.Purge(new Backup{Id="not-a-guid"}),"Purge refuses malformed id");
   MustFail(()=>Engine.Purge(new Backup{Id=Guid.NewGuid().ToString("N"),VaultPath=Path.GetTempPath()}),"Purge refuses missing folder");
  }

  static void Migration(){
   string baseDir=Path.Combine(Path.GetTempPath(),"TweekProMig"+Guid.NewGuid().ToString("N"));
   string source=Path.Combine(baseDir,"AppCare"),target=Path.Combine(baseDir,"TweekPro");
   try{
    Assert(Paths.Migrate(source,target).Outcome==MigrationOutcome.NothingToMigrate,"No source, nothing to migrate");
    Directory.CreateDirectory(Path.Combine(source,"Backups","abc"));File.WriteAllText(Path.Combine(source,"Backups","abc","manifest.xml"),"<x/>");File.WriteAllText(Path.Combine(source,"sessions.xml"),"<s/>");
    var moved=Paths.Migrate(source,target);
    Assert(moved.Outcome==MigrationOutcome.Moved&&!Directory.Exists(source)&&File.Exists(Path.Combine(target,"Backups","abc","manifest.xml"))&&File.Exists(Path.Combine(target,"sessions.xml")),"Folder moved intact");
    Assert(Paths.Migrate(source,target).Outcome==MigrationOutcome.NothingToMigrate,"Second run is a no-op");
    Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"late.txt"),"x");
    Assert(Paths.Migrate(source,target).Outcome==MigrationOutcome.AlreadyMigrated&&File.Exists(Path.Combine(source,"late.txt")),"Existing target is never merged or overwritten");
    string blocked=Path.Combine(baseDir,"blocked.txt");File.WriteAllText(blocked,"file");
    Assert(Paths.Migrate(Path.Combine(baseDir,"missing"),blocked).Outcome==MigrationOutcome.NothingToMigrate,"Missing source with file at target");
    Assert(Paths.Migrate(source,Path.Combine(blocked,"child")).Outcome==MigrationOutcome.Failed,"Impossible target reports Failed instead of throwing");
   }finally{if(Directory.Exists(baseDir))Directory.Delete(baseDir,true);}
  }

  static void SettingsRoundTrip(){
   string file=Path.Combine(Path.GetTempPath(),"TweekProSettings"+Guid.NewGuid().ToString("N"),"settings.json");
   try{
    var defaults=Settings.Load(file);Assert(defaults.NetworkRefreshSeconds==2&&defaults.DeepScanAfterUninstall&&!defaults.NetworkResolveHosts,"Defaults when file missing");
    var s=new Settings{DeepScanAfterUninstall=false,NetworkRefreshSeconds=99,NetworkResolveHosts=true,PurgeDefaultDays=30,WindowWidth=1300,WindowHeight=900};
    s.Save(file);
    string text=File.ReadAllText(file);Assert(text.Contains("\"networkRefreshSeconds\"")&&text.Contains("\"deepScanAfterUninstall\""),"JSON keys written");
    var back=Settings.Load(file);
    Assert(!back.DeepScanAfterUninstall&&back.NetworkRefreshSeconds==30&&back.NetworkResolveHosts&&back.PurgeDefaultDays==30&&back.WindowWidth==1300,"Round trip with clamping");
    File.WriteAllText(file,"{ not json");Assert(Settings.Load(file).NetworkRefreshSeconds==2,"Corrupt file falls back to defaults");
    s.Save(file);Assert(Settings.Load(file).PurgeDefaultDays==30&&!File.Exists(file+".tmp"),"Atomic overwrite leaves no temp file");
   }finally{string dir=Path.GetDirectoryName(file);if(Directory.Exists(dir))Directory.Delete(dir,true);}
  }

  static void LogRotation(){
   string dir=Path.Combine(Path.GetTempPath(),"TweekProLog"+Guid.NewGuid().ToString("N"));
   string previous=Log.Directory;
   try{
    Directory.CreateDirectory(dir);DateTime now=DateTime.Now;
    for(int i=0;i<20;i++)File.WriteAllText(Path.Combine(dir,Log.FileFor(now.AddDays(-i))),"x");
    File.WriteAllText(Path.Combine(dir,"unrelated.log"),"keep");File.WriteAllText(Path.Combine(dir,Log.Prefix+"notadate.log"),"keep");
    int removed=Log.Prune(dir,14,now);
    Assert(removed==6&&Directory.GetFiles(dir,Log.Prefix+"*.log").Length==15&&File.Exists(Path.Combine(dir,"unrelated.log")),"Prune keeps 14 days plus non-matching files");
    Log.Configure(dir,14);Log.Info("hello");Log.Warn("careful");Log.Error("boom",new IOException("io"));
    string today=File.ReadAllText(Path.Combine(dir,Log.FileFor(now)));
    Assert(today.Contains("[INFO ] hello")&&today.Contains("[WARN ] careful")&&today.Contains("[ERROR] boom | IOException: io"),"Levels written to today's file");
    DateTime stamp;Assert(Log.TryParseStamp("tweekpro-20260920.log",out stamp)&&stamp==new DateTime(2026,9,20)&&!Log.TryParseStamp("tweekpro-2026.log",out stamp),"Stamp parsing");
   }finally{Log.Configure(previous,14);if(Directory.Exists(dir))Directory.Delete(dir,true);}
  }
 }
}
