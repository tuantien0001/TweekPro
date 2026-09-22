using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace TweekPro.Tracks {
 /// <summary>Catalog sanity, boundary refusals, file-rule preview/clean/restore through the vault; registry round trip on Windows via an HKCU fixture key.</summary>
 public static class TracksTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("TracksTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   var catalog=TracksCleaner.Catalog();
   Assert(catalog.Count>=12&&catalog.Select(r=>r.Id).Distinct().Count()==catalog.Count,"catalog ids unique");
   Assert(catalog.Where(r=>r.Source==TrackSource.Files).All(r=>r.Patterns.Count>0&&r.Paths.All(p=>p.StartsWith("%APPDATA%")||p.StartsWith("%LOCALAPPDATA%"))),"file rules stay inside the profile's AppData");
   Assert(catalog.Where(r=>r.Source==TrackSource.Registry).All(r=>r.Paths.All(p=>{try{TracksSafety.ValidateKey("HKCU",p.Replace("\\*",""));return true;}catch(IOException){return false;}})),"registry rules only name allowed keys");
   Assert(catalog.Where(r=>r.Sensitive).All(r=>!r.DefaultChecked),"sensitive rules start unchecked");
   Assert(!catalog.Any(r=>r.Patterns.Any(p=>p.Equals("places.sqlite",StringComparison.OrdinalIgnoreCase)||p.Equals("Login Data",StringComparison.OrdinalIgnoreCase)||p.Equals("Bookmarks",StringComparison.OrdinalIgnoreCase))),"bookmarks and passwords never targeted");

   MustFail(()=>TracksSafety.ValidateKey("HKLM",TracksSafety.Explorer+@"\RunMRU"),"HKLM refused");
   MustFail(()=>TracksSafety.ValidateKey("HKCU",@"Software\Microsoft\Windows\CurrentVersion\Run"),"Run key refused");
   MustFail(()=>TracksSafety.ValidateKey("HKCU",TracksSafety.Explorer),"Explorer root itself refused");
   MustFail(()=>TracksSafety.ValidateKey("HKCU",TracksSafety.Explorer+@"\RunMRU\..\Shell Folders"),"parent traversal refused");
   TracksSafety.ValidateKey("HKCU",TracksSafety.Explorer+@"\RunMRU");TracksSafety.ValidateKey("HKCU",TracksSafety.Explorer+@"\UserAssist\{GUID}\Count");

   string profile=Path.Combine(Path.GetTempPath(),"tweekpro-tracks-"+Guid.NewGuid().ToString("N"));
   string recent=Path.Combine(profile,"AppData","Roaming","Microsoft","Windows","Recent");
   string originalVault=Engine.Vault;
   try{
    Directory.CreateDirectory(recent);Engine.Vault=Path.Combine(profile,"Vault");
    MustFail(()=>TracksSafety.ValidateRoot(profile,profile),"profile root refused");
    MustFail(()=>TracksSafety.ValidateRoot(Path.Combine(profile,"AppData","Roaming"),profile),"AppData root refused");
    MustFail(()=>TracksSafety.ValidateRoot(@"C:\Windows\Temp",profile),"folder outside profile refused");
    TracksSafety.ValidateRoot(recent,profile);
    File.WriteAllText(Path.Combine(recent,"report.docx.lnk"),"lnk");File.WriteAllText(Path.Combine(recent,"desktop.ini"),"ini");File.WriteAllText(Path.Combine(recent,"notes.txt"),"txt");
    var rule=new TrackRule{Id="fixture",Name="Fixture",Group="Test",Source=TrackSource.Files,Paths={recent},Patterns={"*.lnk"}};
    var preview=TracksCleaner.Preview(new[]{rule},profile,CancellationToken.None);
    Assert(preview.Count==1&&preview[0].Count==1&&preview[0].Items[0].Path.EndsWith("report.docx.lnk")&&preview[0].Roots.Count==1,"preview matches the .lnk only");
    MustFail(()=>TracksSafety.ValidateFile(Path.Combine(recent,"desktop.ini"),preview[0].Roots,rule.Patterns),"desktop.ini refused");
    MustFail(()=>TracksSafety.ValidateFile(Path.Combine(recent,"notes.txt"),preview[0].Roots,rule.Patterns),"non-matching file refused");
    MustFail(()=>TracksSafety.ValidateFile(Path.Combine(profile,"x.lnk"),preview[0].Roots,rule.Patterns),"file outside root refused");
    var report=TracksCleaner.Clean(preview,profile,CancellationToken.None);
    Assert(report.Cleaned==1&&report.Backups.Count==1&&!File.Exists(Path.Combine(recent,"report.docx.lnk"))&&File.Exists(Path.Combine(recent,"desktop.ini")),"clean moved the .lnk only");
    var backup=Engine.Backups().First(b=>b.Id==report.Backups[0].Id);
    Assert(backup.Kind==TracksCleaner.BackupKind&&backup.Purpose=="fixture"&&backup.State=="BackedUp","tracks backup listed");
    // Restore validates the destination against the live profile, so only the manifest round trip is checked here; the Windows registry path is exercised below.
    var moved=Engine.Load<List<Cleaner.JunkMoved>>(Path.Combine(Engine.Vault,backup.Id,"files.xml"));
    Assert(moved.Count==1&&File.Exists(Path.Combine(Engine.Vault,backup.Id,"content",moved[0].Stored)),"payload indexed");
    var lockedRule=new TrackRule{Id="locked",Name="Locked",Group="Test",Source=TrackSource.Files,Paths={recent},Patterns={"*"},BlockedProcesses={System.Diagnostics.Process.GetCurrentProcess().ProcessName}};
    var lockedPreview=TracksCleaner.Preview(new[]{lockedRule},profile,CancellationToken.None);
    Assert(lockedPreview[0].Locked,"rule locked while its process runs");
    MustFail(()=>TracksCleaner.Clean(lockedPreview,profile,CancellationToken.None),"locked rule cannot be cleaned");
    Assert(TracksCleaner.ExpandFolders(Path.Combine(profile,"AppData","*","Microsoft")).Count==1,"single-star folder expansion");
    MustFail(()=>TracksCleaner.ExpandFolders(Path.Combine(profile,"*","*")),"two stars refused");

    if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return;
    string fixtureKey=@"Software\TweekProTest\Tracks\RunMRU";
    using(var hive=Engine.Base("HKCU","64")){
     try{hive.DeleteSubKeyTree(@"Software\TweekProTest\Tracks",false);}catch(Exception){}
     using(var key=hive.CreateSubKey(fixtureKey)){key.SetValue("a","cmd\\1");key.SetValue("b","regedit\\1");key.SetValue("MRUList","ba");using(var sub=key.CreateSubKey("Nested"))sub.SetValue("x",1,RegistryValueKind.DWord);}
     var regRule=new TrackRule{Id="reg-fixture",Name="RegFixture",Group="Test",Source=TrackSource.Registry,Paths={fixtureKey}};
     var regPreview=TracksCleaner.Preview(new[]{regRule},profile,CancellationToken.None);
     Assert(regPreview[0].Count==3&&regPreview[0].Roots[0]=="HKCU\\"+fixtureKey,"registry preview counts values (MRUList hidden) incl. nested");
     var regReport=TracksCleaner.Clean(regPreview,profile,CancellationToken.None);
     using(var key=hive.OpenSubKey(fixtureKey))Assert(key!=null&&key.GetValueNames().Length==0&&key.GetSubKeyNames().Length==0,"key emptied but kept");
     var regBackup=Engine.Backups().First(b=>b.Id==regReport.Backups[0].Id);
     Assert(regBackup.Payload=="registry.xml"&&regBackup.Hive=="HKCU"&&regReport.Cleaned==4,"registry backup saved");
     using(var key=hive.OpenSubKey(fixtureKey,true))key.SetValue("a","newer");
     TracksCleaner.Restore(regBackup);
     using(var key=hive.OpenSubKey(fixtureKey)){Assert(Convert.ToString(key.GetValue("a"))=="newer"&&Convert.ToString(key.GetValue("b"))=="regedit\\1"&&Convert.ToString(key.GetValue("MRUList"))=="ba","restore merges without overwriting newer values");using(var sub=key.OpenSubKey("Nested"))Assert(sub!=null&&(int)sub.GetValue("x")==1,"nested key restored");}
     Assert(regBackup.State=="Restored","registry backup marked restored");
     var outside=new Backup{Id=regBackup.Id,Kind=TracksCleaner.BackupKind,Payload="registry.xml",Hive="HKCU",Original=@"Software\Microsoft\Windows\CurrentVersion\Run"};
     MustFail(()=>TracksCleaner.Restore(outside),"restore refuses a key outside the allow-list");
     try{hive.DeleteSubKeyTree(@"Software\TweekProTest",false);}catch(Exception){}
    }
   }finally{
    Engine.Vault=originalVault;
    try{Directory.Delete(profile,true);}catch(Exception){}
   }
  }
 }
}
