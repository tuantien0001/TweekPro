using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace TweekPro.RegClean {
 /// <summary>Target parsing, verifiability, boundary refusals; on Windows a fixture Uninstall/MUICache parent under HKCU proves scan → clean → restore.</summary>
 public static class RegCleanTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("RegCleanTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   Assert(!RegCleanSafety.Verifiable("")&&!RegCleanSafety.Verifiable("foo.dll")&&!RegCleanSafety.Verifiable(@"\\server\share\x.exe")&&!RegCleanSafety.Verifiable(@"C:\a\*.exe"),"relative, UNC and wildcard paths are not judgeable");
   Assert(RegistryCleaner.CommandTarget("MsiExec.exe /X{GUID}")==""&&RegistryCleaner.CommandTarget(@"rundll32.exe C:\x\y.dll,Entry")==""&&RegistryCleaner.CommandTarget("")=="","msiexec/rundll32/empty never judged");
   Assert(RegistryCleaner.MuiCacheTarget(@"C:\Program Files\X\x.exe.FriendlyAppName").EndsWith("x.exe",StringComparison.OrdinalIgnoreCase)||!RegCleanSafety.Verifiable(@"C:\Program Files\X\x.exe"),"MUICache FriendlyAppName path extracted");
   Assert(RegistryCleaner.MuiCacheTarget("@shell32.dll,-1234.FriendlyAppName")==""&&RegistryCleaner.MuiCacheTarget("LangID")=="","MUICache resource names and LangID ignored");
   var cats=RegistryCleaner.Categories();
   Assert(cats.Count==6&&cats.Select(c=>c.Id).Distinct().Count()==6&&cats.All(c=>c.Parents.Count>0&&(c.Values?c.ValueTarget!=null:c.KeyTarget!=null)),"category catalog complete");
   Assert(cats.First(c=>c.Id=="shell-approved").DefaultChecked==false,"shell extensions start unchecked");
   var parents=cats.SelectMany(c=>c.Parents).ToList();
   MustFail(()=>RegCleanSafety.Validate(new RegFinding{Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"},parents),"parent itself refused");
   MustFail(()=>RegCleanSafety.Validate(new RegFinding{Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\A\B"},parents),"grandchild refused");
   MustFail(()=>RegCleanSafety.Validate(new RegFinding{Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\Foo"},parents),"Run refused");
   MustFail(()=>RegCleanSafety.Validate(new RegFinding{Hive="HKLM",View="64",Key=@"SOFTWARE\Classes\CLSID\{X}"},parents),"CLSID refused");
   MustFail(()=>RegCleanSafety.Validate(new RegFinding{Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\IE40"},parents),"Windows-owned uninstall leaf kept");
   MustFail(()=>RegCleanSafety.Validate(new RegFinding{Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs",ValueName=""},parents),"default value refused");
   MustFail(()=>RegCleanSafety.Validate(new RegFinding{Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\..\Run"},parents),"traversal refused");
   RegCleanSafety.Validate(new RegFinding{Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OldTool_is1"},parents);
   RegCleanSafety.Validate(new RegFinding{Hive="HKLM",View="32",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs",ValueName=@"C:\x\y.dll"},parents);

   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return;
   string originalVault=Engine.Vault;string fixture=Path.Combine(Path.GetTempPath(),"tweekpro-regclean-"+Guid.NewGuid().ToString("N"));
   string root=RegistryCleaner.FixtureRoot;
   try{
    Directory.CreateDirectory(fixture);Engine.Vault=Path.Combine(fixture,"Vault");
    string present=Path.Combine(fixture,"present.exe");File.WriteAllBytes(present,new byte[8]);string missing=Path.Combine(fixture,"missing.exe");
    using(var hive=Engine.Base("HKCU","64")){
     try{hive.DeleteSubKeyTree(root,false);}catch(Exception){}
     using(var k=hive.CreateSubKey(root+@"\uninstall\Orphan_is1")){k.SetValue("DisplayName","Orphan Tool");k.SetValue("UninstallString","\""+missing+"\" /SILENT");}
     using(var k=hive.CreateSubKey(root+@"\uninstall\Alive_is1")){k.SetValue("DisplayName","Alive Tool");k.SetValue("UninstallString","\""+present+"\"");}
     using(var k=hive.CreateSubKey(root+@"\uninstall\Msi")){k.SetValue("DisplayName","MSI Tool");k.SetValue("UninstallString","MsiExec.exe /X{1}");k.SetValue("WindowsInstaller",1);}
     using(var k=hive.CreateSubKey(root+@"\uninstall\HasFolder")){k.SetValue("DisplayName","Folder Tool");k.SetValue("UninstallString","\""+missing+"\"");k.SetValue("InstallLocation",fixture);}
     using(var k=hive.CreateSubKey(root+@"\mui-cache")){k.SetValue(missing+".FriendlyAppName","Gone");k.SetValue(present+".FriendlyAppName","Here");k.SetValue("LangID",new byte[]{9,4},RegistryValueKind.Binary);}
     var cats2=RegistryCleaner.Categories(root).Where(c=>c.Id=="uninstall"||c.Id=="mui-cache").Select(c=>{c.Parents=c.Parents.Where(p=>p.Key.StartsWith(root)).ToList();return c;}).ToList();
     var scan=RegistryCleaner.Scan(cats2,CancellationToken.None);
     var uninstall=scan.First(r=>r.Category.Id=="uninstall");var mui=scan.First(r=>r.Category.Id=="mui-cache");
     Assert(uninstall.Count==1&&uninstall.Findings[0].Key.EndsWith("Orphan_is1")&&uninstall.Findings[0].Detail=="Orphan Tool","only the orphan uninstall key is proposed (alive, MSI, has-folder kept)");
     Assert(mui.Count==1&&mui.Findings[0].ValueName.StartsWith(missing),"only the MUICache value of the missing exe is proposed");
     var report=RegistryCleaner.Clean(scan,CancellationToken.None);
     Assert(report.Cleaned==2&&report.Failed==0&&report.Backups.Count==2,"two entries cleaned into two backups");
     using(var k=hive.OpenSubKey(root+@"\uninstall\Orphan_is1"))Assert(k==null,"orphan key deleted");
     using(var k=hive.OpenSubKey(root+@"\uninstall\Alive_is1"))Assert(k!=null,"alive key kept");
     using(var k=hive.OpenSubKey(root+@"\mui-cache"))Assert(k.GetValueNames().Length==2&&!k.GetValueNames().Contains(missing+".FriendlyAppName"),"orphan value deleted, others kept");
     var backups=Engine.Backups().Where(b=>b.Kind==RegistryCleaner.BackupKind).ToList();
     Assert(backups.Count==2&&backups.All(b=>b.State=="BackedUp"&&b.Payload=="entries.xml"),"backups listed");
     foreach(var b in backups)Engine.Restore(b);
     using(var k=hive.OpenSubKey(root+@"\uninstall\Orphan_is1"))Assert(k!=null&&Engine.Read(k,"DisplayName")=="Orphan Tool","orphan key restored with values");
     using(var k=hive.OpenSubKey(root+@"\mui-cache"))Assert(Convert.ToString(k.GetValue(missing+".FriendlyAppName"))=="Gone","orphan value restored");
     Assert(backups.All(b=>b.State=="Restored"),"backups marked restored");
     // Re-appeared target is kept at clean time.
     var again=RegistryCleaner.Scan(cats2,CancellationToken.None);File.WriteAllBytes(missing,new byte[8]);
     var kept=RegistryCleaner.Clean(again,CancellationToken.None);
     Assert(kept.Cleaned==0&&kept.Failed==2,"targets that reappeared are not deleted");
     try{hive.DeleteSubKeyTree(@"SOFTWARE\TweekProTest",false);}catch(Exception){}
    }
   }finally{Engine.Vault=originalVault;try{Directory.Delete(fixture,true);}catch(Exception){}}
  }
 }
}
