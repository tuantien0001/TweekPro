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
   Assert(catalog.Count>=25,"catalog grew with the 0.7.6 sources");
   Assert(catalog.Where(r=>r.Source==TrackSource.Registry).All(r=>r.Paths.All(p=>{try{TracksSafety.ValidateKey("HKCU",p.Replace("*","x"));return true;}catch(IOException){return false;}})),"registry rules only name allowed keys");
   Assert(catalog.Where(r=>r.SqlCapable).All(r=>r.Patterns.Any(p=>p.Equals(r.SqlFile,StringComparison.OrdinalIgnoreCase))),"sql rules name their database in the patterns");
   Assert(catalog.First(r=>r.Id=="regedit-lastkey").Values.Contains("LastKey"),"regedit rule keeps Favorites by targeting LastKey only");
   MustFail(()=>TracksSafety.ValidateKey("HKCU",@"Software\Microsoft\Office"),"Office root refused");
   MustFail(()=>TracksSafety.ValidateKey("HKCU",@"Software\Microsoft\Office\16.0\Word"),"Office app key itself refused");
   MustFail(()=>TracksSafety.ValidateKey("HKCU",@"Software\Microsoft\Office\16.0\Word\Options"),"Office options refused");
   TracksSafety.ValidateKey("HKCU",@"Software\Microsoft\Office\16.0\Word\File MRU");TracksSafety.ValidateKey("HKCU",@"Software\Microsoft\Office\16.0\Excel\User MRU\LiveId_ABC\Place MRU");
   MustFail(()=>TracksSafety.ValidateKey("HKCU",@"Software\Microsoft\Office\*\*\File MRU"),"wildcard must be expanded before validation");
   var keep=TracksCleaner.ParseKeep("Google.com, https://mail.example.org/inbox\r\n.github.com; bad host ;x");
   Assert(keep.Count==3&&keep[0]=="google.com"&&keep[1]=="mail.example.org"&&keep[2]=="github.com","cookie whitelist parsing");
   Assert(catalog.Where(r=>r.Sensitive).All(r=>!r.DefaultChecked),"sensitive rules start unchecked");
   Assert(!catalog.Any(r=>r.Patterns.Any(p=>p.Equals("Login Data",StringComparison.OrdinalIgnoreCase)||p.Equals("Bookmarks",StringComparison.OrdinalIgnoreCase)||p.Equals("logins.json",StringComparison.OrdinalIgnoreCase)||p.Equals("key4.db",StringComparison.OrdinalIgnoreCase)||p.Equals("Web Data",StringComparison.OrdinalIgnoreCase))),"bookmarks, passwords and autofill never targeted");
   Assert(catalog.Where(r=>r.Patterns.Any(p=>p.Equals("places.sqlite",StringComparison.OrdinalIgnoreCase))).All(r=>r.SqlFile=="places.sqlite"&&r.SqlStatements!=null&&!r.SqlStatements.Any(s=>s.IndexOf("moz_bookmarks",StringComparison.OrdinalIgnoreCase)>=0&&s.TrimStart().StartsWith("DELETE FROM moz_bookmarks",StringComparison.OrdinalIgnoreCase))),"places.sqlite only through bookmark-preserving SQL, never moved as a file");

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
    Assert(TracksCleaner.ExpandFolders(Path.Combine(profile,"*","*","Microsoft","Windows")).Count==1,"two-star folder expansion");
    Assert(TracksCleaner.ExpandFolders(Path.Combine(profile,"*","*","Nope")).Count==0,"two-star expansion with no match");

    if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return;
    CookieWhitelist(profile);
    using(var hive=Engine.Base("HKCU","64")){
     try{hive.DeleteSubKeyTree(@"Software\TweekProTest\Tracks",false);}catch(Exception){}
     try{hive.DeleteSubKeyTree(@"Software\TweekProTest\TracksWild",false);}catch(Exception){}
     using(var k=hive.CreateSubKey(@"Software\TweekProTest\TracksWild\A\Sub"))k.SetValue("v","1");
     using(var k=hive.CreateSubKey(@"Software\TweekProTest\TracksWild\B\Sub"))k.SetValue("v","2");
     using(var k=hive.CreateSubKey(@"Software\TweekProTest\TracksWild\C\Other"))k.SetValue("v","3");
     var wild=TracksCleaner.ExpandKeys(hive,@"Software\TweekProTest\TracksWild\*\Sub");
     Assert(wild.Count==2&&wild.All(p=>p.EndsWith("\\Sub")),"wildcard key expansion lists only existing matches, got "+wild.Count);
     var wildRule=new TrackRule{Id="wild",Name="Wild",Group="Test",Source=TrackSource.Registry,Paths={@"Software\TweekProTest\TracksWild\*\Sub"}};
     var wildPreview=TracksCleaner.Preview(new[]{wildRule},profile,CancellationToken.None);
     Assert(wildPreview[0].Count==2&&wildPreview[0].Roots.Count==2,"wildcard registry preview");
     using(var k=hive.CreateSubKey(@"Software\TweekProTest\Tracks\Applet")){k.SetValue("LastKey","HKLM\\X");k.SetValue("Keep","yes");using(var fav=k.CreateSubKey("Favorites"))fav.SetValue("f","1");}
     var valueRule=new TrackRule{Id="values",Name="Values",Group="Test",Source=TrackSource.Registry,Paths={@"Software\TweekProTest\Tracks\Applet"},Values={"LastKey"}};
     var valuePreview=TracksCleaner.Preview(new[]{valueRule},profile,CancellationToken.None);
     Assert(valuePreview[0].Count==1&&valuePreview[0].Items[0].Path.EndsWith(":: LastKey"),"value filter lists only the named value");
     var valueReport=TracksCleaner.Clean(valuePreview,profile,CancellationToken.None);
     using(var k=hive.OpenSubKey(@"Software\TweekProTest\Tracks\Applet")){Assert(k.GetValue("LastKey")==null&&Convert.ToString(k.GetValue("Keep"))=="yes"&&k.OpenSubKey("Favorites")!=null,"value filter removes only LastKey, keeps other values and subkeys");}
     TracksCleaner.Restore(Engine.Backups().First(b=>b.Id==valueReport.Backups[0].Id));
     using(var k=hive.OpenSubKey(@"Software\TweekProTest\Tracks\Applet"))Assert(Convert.ToString(k.GetValue("LastKey"))=="HKLM\\X","filtered value restored");
     string fixtureKey=@"Software\TweekProTest\Tracks\RunMRU";
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
    Engine.Vault=originalVault;TracksCleaner.CookieKeep=new List<string>();TracksCleaner.BlockerOverride=null;
    try{Directory.Delete(profile,true);}catch(Exception){}
   }
  }

  /// <summary>Builds a Chromium-shaped cookie database with winsqlite3, cleans with a whitelist and checks only the kept hosts survive, then restores the copy from the vault.</summary>
  static void CookieWhitelist(string profile){
   if(!Core.WinSqlite.Available)return;
   string network=Path.Combine(profile,"AppData","Local","Google","Chrome","User Data","Default","Network");Directory.CreateDirectory(network);
   string db=Path.Combine(network,"Cookies");
   Core.WinSqlite.Execute(db,"CREATE TABLE cookies (host_key TEXT, name TEXT, value TEXT)");
   foreach(var pair in new[]{new[]{".google.com","SID"},new[]{"accounts.google.com","LSID"},new[]{".facebook.com","c_user"},new[]{"github.com","user_session"},new[]{"notgoogle.com","x"},new[]{".ads.example","tracker"}})Core.WinSqlite.Execute(db,"INSERT INTO cookies VALUES (?,?,?)",pair[0],pair[1],"v");
   Assert(Core.WinSqlite.Count(db,"cookies")==6,"fixture cookie rows");
   var rule=TracksCleaner.Catalog().First(r=>r.Id=="chromium-cookies");rule.Paths=new List<string>{Path.Combine(profile,"AppData","Local","Google","Chrome","User Data","*","Network")};rule.BlockedProcesses=new List<string>();
   TracksCleaner.CookieKeep=TracksCleaner.ParseKeep("google.com github.com");
   var preview=TracksCleaner.Preview(new[]{rule},profile,CancellationToken.None);
   Assert(preview[0].Sql&&preview[0].Count==1&&preview[0].Items[0].Rows==6&&preview[0].Note.Contains("google.com"),"sql preview counts rows and names the whitelist");
   var report=TracksCleaner.Clean(preview,profile,CancellationToken.None);
   Assert(report.Cleaned==3&&report.Backups.Count==1&&File.Exists(db),"whitelist clean deleted 3 rows in place, got "+report.Cleaned);
   var left=Core.WinSqlite.Query(db,"SELECT host_key FROM cookies ORDER BY host_key").Select(r=>r[0]).ToList();
   Assert(left.SequenceEqual(new[]{".google.com","accounts.google.com","github.com"}),"only whitelisted hosts remain: "+String.Join(",",left));
   var backup=Engine.Backups().First(b=>b.Id==report.Backups[0].Id);
   Assert(backup.Payload=="content-copy","sql backup is a copy");
   var restoreRoot=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   if(Engine.Under(Engine.Canon(profile),Engine.Canon(restoreRoot))){
    TracksCleaner.BlockerOverride=r=>new List<string>{"chrome"};
    bool blocked=false;try{TracksCleaner.Restore(backup);}catch(IOException){blocked=true;}Assert(blocked,"database restore refused while the browser runs");
    TracksCleaner.BlockerOverride=r=>new List<string>();
    TracksCleaner.Restore(backup);Assert(Core.WinSqlite.Count(db,"cookies")==6,"restore copies the full database back");
    TracksCleaner.BlockerOverride=null;
   }
   TracksCleaner.CookieKeep=new List<string>();
   var all=TracksCleaner.Preview(new[]{rule},profile,CancellationToken.None);
   Assert(!all[0].Sql,"without a whitelist the cookie rule moves the whole file");
  }
 }
}
