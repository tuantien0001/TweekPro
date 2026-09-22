using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using TweekPro.Cleaner;
using TweekPro.Core;
using TweekPro.Network;

namespace TweekPro {
 /// <summary>
 /// Windows-only self-tests added in 0.7: junk preview/clean/restore through the Vault, direct delete, locked rules,
 /// vault purge, legacy AppCare vault listing and the connection table. Fixtures live under %LOCALAPPDATA%\Temp\TweekProTest*.
 /// </summary>
 public static class Tests07 {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("Tests07: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  /// <summary>The exported .ico must be a valid multi-size container whose entries are PNG images (what Explorer and Inno Setup expect).</summary>
  static void IconFile(string path){
   Branding.WriteIconFile(path);
   byte[] data=File.ReadAllBytes(path);
   Assert(BitConverter.ToUInt16(data,0)==0&&BitConverter.ToUInt16(data,2)==1,"ico header");
   int count=BitConverter.ToUInt16(data,4);Assert(count==11,"approved ico has 11 sizes");
   var sizes=new List<int>();
   for(int i=0;i<count;i++){
    int entry=6+16*i;int width=data[entry]==0?256:data[entry];sizes.Add(width);
    Assert(data[entry]==data[entry+1],"ico entry is square");
    Assert(BitConverter.ToUInt16(data,entry+6)==32,"ico entry is 32bpp");
    int length=BitConverter.ToInt32(data,entry+8),offset=BitConverter.ToInt32(data,entry+12);
    Assert(offset>=6+16*count&&offset+length<=data.Length,"ico entry inside file");
    Assert(data[offset]==0x89&&data[offset+1]==(byte)'P'&&data[offset+2]==(byte)'N'&&data[offset+3]==(byte)'G',"ico entry is PNG");
    using(var stream=new MemoryStream(data,offset,length))using(var image=Image.FromStream(stream))Assert(image.Width==width&&image.Height==width,"ico PNG dimensions");
   }
   Assert(sizes.SequenceEqual(new[]{16,20,24,32,40,48,64,96,128,192,256}),"approved icon frames cover Windows DPI sizes");
   foreach(int size in new[]{16,32,48,256})using(var icon=Branding.AppIcon(size))Assert(icon.Width==size&&icon.Height==size,"runtime icon size");
   Assert(!string.IsNullOrEmpty(Branding.AppUserModelId)&&Branding.AppUserModelId.IndexOf("tpc",StringComparison.OrdinalIgnoreCase)>=0,"taskbar AppUserModelID marks TPC branding");
  }

  public static void Run(){
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   string id="TweekProTest"+Guid.NewGuid().ToString("N");
   string fixture=Path.Combine(local,"Temp",id);string junkRoot=Path.Combine(fixture,"Cache");
   string originalVault=Engine.Vault,originalLegacy=Paths.Legacy;
   try{
    Directory.CreateDirectory(Path.Combine(junkRoot,"nested"));
    IconFile(Path.Combine(fixture,"logo.ico"));
    Engine.Vault=Path.Combine(fixture,"Vault");Paths.Legacy=Path.Combine(fixture,"AppCareLegacy");
    // Creation time cannot be back-dated on Linux/Mono, so the age filter (and the fresh-file fixture) only apply on Windows.
    bool windows=Environment.OSVersion.Platform==PlatformID.Win32NT;
    string old=Path.Combine(junkRoot,"nested","old.bin"),fresh=Path.Combine(junkRoot,"fresh.bin");
    File.WriteAllBytes(old,new byte[2048]);if(windows)File.WriteAllText(fresh,"fresh");
    Action age=()=>{File.SetCreationTime(old,DateTime.Now.AddDays(-2));File.SetLastWriteTime(old,DateTime.Now.AddDays(-2));File.SetLastAccessTime(old,DateTime.Now.AddDays(-2));};
    age();
    var rule=new JunkRule{Id="fixture",Name="Fixture",Group="Test",Paths=new List<string>{junkRoot},Patterns=new List<string>{"*"},Recurse=true,MinAgeHours=windows?24:0};

    // Platform-neutral suites added alongside the 0.7 line; each restores any global state it touches.
    Dupes.DupeTests.Run();
    Analyzer.AnalyzerTests.Run();
    Network.NetworkStatsTests.Run();
    Network.ProcessControlTests.Run();
    Network.FirewallBlockTests.Run();
    Services.ServiceCatalogTests.Run();
    AI.AiTests.Run();
    Network.PacketAnimatorTests.Run();
    Cleaner.EmptyFolderTests.Run();
    Health.HealthTests.Run();
    Core.LangTests.Run();
    Core.StubbornTests.Run();
    Store.WindowsAppsTests.Run();
    Sizing.InstallSizeTests.Run();
    Pup.PupTests.Run();
    Stale.StaleTests.Run();
    Startup.StartupTests.Run();
    Startup.StartupSourcesTests.Run();
    Update.UpdateTests.Run();
    Explorer.ExplorerTests.Run();
    Remnants.TraceHunterTests.Run();
    Remnants.ForcedUninstallTests.Run();
    Tracks.TracksTests.Run();
    RegClean.RegCleanTests.Run();
    SysSpace.SystemSpaceTests.Run();

    // Safety boundary: rules cannot reach user documents, the data folder or a whole AppData root.
    MustFail(()=>JunkSafety.ValidateRoot(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),"Documents refused");
    MustFail(()=>JunkSafety.ValidateRoot(local),"LocalAppData root refused");
    MustFail(()=>JunkSafety.ValidateRoot(Paths.Root),"Own data folder refused");
    MustFail(()=>JunkSafety.ValidateRoot(Path.Combine(local,"Google","Chrome","User Data","Default")),"Browser profile without cache leaf refused");
    JunkSafety.ValidateRoot(junkRoot);

    // System junk boundary (explicit lists so it runs off-Windows): elevated rules may reach Windows-owned caches, never the OS image.
    var winAllowed=new[]{@"C:\Windows\Temp",@"C:\Windows\SoftwareDistribution\Download",@"C:\Windows\Logs\CBS",@"C:\Windows\Minidump"}.Select(Engine.Canon).ToList();
    var winForbidden=new[]{@"C:\Windows\System32",@"C:\Windows\SysWOW64",@"C:\Windows\WinSxS",@"C:\Program Files"}.Select(Engine.Canon).ToList();
    var noBrowser=new List<string>();
    MustFail(()=>JunkSafety.ValidateRoot(@"C:\Windows\System32",winAllowed,noBrowser,winForbidden),"System32 refused");
    MustFail(()=>JunkSafety.ValidateRoot(@"C:\Windows\System32\drivers",winAllowed,noBrowser,winForbidden),"System32 subfolder refused");
    MustFail(()=>JunkSafety.ValidateRoot(@"C:\Windows\System32\config",winAllowed,noBrowser,winForbidden),"Registry hives folder refused");
    MustFail(()=>JunkSafety.ValidateRoot(@"C:\Windows\WinSxS\Temp",winAllowed,noBrowser,winForbidden),"WinSxS refused even with a Temp leaf");
    MustFail(()=>JunkSafety.ValidateRoot(@"C:\Windows",winAllowed,noBrowser,winForbidden),"Windows root refused because it contains protected folders");
    MustFail(()=>JunkSafety.ValidateRoot(@"C:\",winAllowed,noBrowser,winForbidden),"Drive root refused");
    MustFail(()=>JunkSafety.ValidateRoot(@"C:\Windows\Prefetch",winAllowed,noBrowser,winForbidden),"Folder outside the allow-list refused");
    MustFail(()=>JunkSafety.ValidateRoot(@"C:\Program Files\Example",winAllowed,noBrowser,winForbidden),"Program Files refused");
    JunkSafety.ValidateRoot(@"C:\Windows\SoftwareDistribution\Download",winAllowed,noBrowser,winForbidden);
    JunkSafety.ValidateRoot(@"C:\Windows\Logs\CBS",winAllowed,noBrowser,winForbidden);
    JunkSafety.ValidateRoot(@"C:\Windows\Minidump",winAllowed,noBrowser,winForbidden);
    Assert(new[]{"System32","SysWOW64","WinSxS","servicing","Boot","Fonts","Installer"}.All(JunkSafety.WindowsCoreFolders.Contains),"Core Windows folders are hard-blocked");
    var embedded=JunkRules.LoadEmbedded();
    var systemRules=embedded.Rules.Where(r=>r.Paths.Any(p=>p.StartsWith("%WINDIR%",StringComparison.OrdinalIgnoreCase))).ToList();
    Assert(systemRules.Count>=6&&systemRules.All(r=>r.RequiresAdmin),"Every rule under %WINDIR% requires administrator rights");
    Assert(embedded.Rules.All(r=>r.Paths.All(p=>!JunkSafety.WindowsCoreFolders.Any(core=>p.IndexOf("\\"+core+"\\",StringComparison.OrdinalIgnoreCase)>=0||p.EndsWith("\\"+core,StringComparison.OrdinalIgnoreCase)))),"No embedded rule names a core Windows folder");
    Assert(embedded.Rules.Any(r=>r.Id=="windows-update-cache"&&r.MinAgeHours>=168)&&embedded.Rules.Any(r=>r.Id=="cbs-logs")&&embedded.Rules.Any(r=>r.Id=="memory-dumps"),"System junk rules present with a conservative age floor");

    var preview=JunkCleaner.Preview(new[]{rule},Elevation.IsElevated,-1,CancellationToken.None);
    Assert(preview.Count==1&&preview[0].Count==1&&preview[0].Items[0].Path.Equals(old,StringComparison.OrdinalIgnoreCase)&&preview[0].Bytes==2048,"Preview finds only the old file: count="+preview[0].Count+" roots="+preview[0].Roots.Count+" note="+preview[0].Note+" locked="+preview[0].Locked+" items="+String.Join(";",preview[0].Items.Select(i=>i.Path+"="+i.Bytes)));
    var report=JunkCleaner.Clean(preview,false,CancellationToken.None);
    Assert(report.Cleaned==1&&report.Backups.Count==1&&!File.Exists(old)&&(!windows||File.Exists(fresh)),"Vault mode moved the old file only");
    var backup=Engine.Backups().FirstOrDefault(b=>b.Id==report.Backups[0].Id);
    Assert(backup!=null&&backup.Kind=="Junk"&&backup.State=="BackedUp"&&File.Exists(Path.Combine(Engine.Vault,backup.Id,"files.xml")),"Junk backup listed with index");
    Assert(Engine.BackupSize(backup)>=2048,"Backup size measured");
    Engine.Restore(backup);
    Assert(File.Exists(old)&&new FileInfo(old).Length==2048&&backup.State=="Restored","Junk restore puts the file back");
    long freed=Engine.Purge(backup);
    Assert(!Directory.Exists(Path.Combine(Engine.Vault,backup.Id))&&freed>=0,"Purge removes the backup folder");
    MustFail(()=>Engine.Purge(backup),"Second purge fails");

    // Direct mode deletes without a backup.
    preview=JunkCleaner.Preview(new[]{rule},Elevation.IsElevated,-1,CancellationToken.None);
    Assert(preview[0].Count==1,"Preview after restore");
    report=JunkCleaner.Clean(preview,true,CancellationToken.None);
    Assert(report.Cleaned==1&&report.Backups.Count==0&&!File.Exists(old)&&Engine.Backups().Count==0,"Direct mode deleted without backup");

    // A file held open is skipped, never deleted.
    Directory.CreateDirectory(Path.GetDirectoryName(old));File.WriteAllText(old,"held");age();
    using(new FileStream(old,FileMode.Open,FileAccess.Read,FileShare.None)){
     preview=JunkCleaner.Preview(new[]{rule},Elevation.IsElevated,-1,CancellationToken.None);
     report=JunkCleaner.Clean(preview,true,CancellationToken.None);
     Assert(report.SkippedInUse==1&&report.Cleaned==0&&File.Exists(old),"In-use file skipped");
    }

    // A rule whose blocking process is running stays locked and cannot be cleaned.
    var locked=new JunkRule{Id="locked",Name="Locked",Paths=new List<string>{junkRoot},Patterns=new List<string>{"*"},MinAgeHours=0,BlockedProcesses=new List<string>{Process.GetCurrentProcess().ProcessName}};
    var lockedPreview=JunkCleaner.Preview(new[]{locked},Elevation.IsElevated,-1,CancellationToken.None);
    Assert(lockedPreview[0].Locked&&lockedPreview[0].Count>0,"Rule locked while process runs");
    MustFail(()=>JunkCleaner.Clean(lockedPreview,false,CancellationToken.None),"Locked rule cannot be cleaned");

    // Legacy AppCare vault is listed alongside the current one and resolves to its own folder.
    string legacyVault=Paths.LegacyBackups;string legacyId=Guid.NewGuid().ToString("N");
    Directory.CreateDirectory(Path.Combine(legacyVault,legacyId));
    Engine.Save(Path.Combine(legacyVault,legacyId,"manifest.xml"),new Backup{Id=legacyId,Created=DateTime.Now.AddDays(-200).ToString("s"),State="Restored",Kind="Folder",Original=Path.Combine(fixture,"gone"),AppName="Legacy",Payload="content"});
    var listed=Engine.Backups().FirstOrDefault(b=>b.Id==legacyId);
    Assert(listed!=null&&listed.VaultPath!=null&&String.Equals(Engine.Canon(Engine.VaultOf(listed)),Engine.Canon(legacyVault),StringComparison.OrdinalIgnoreCase),"Legacy backup listed with its vault path");
    Assert(Engine.SelectForPurge(new[]{listed},90,DateTime.Now,false).Count==1,"Legacy restored backup selectable for purge");
    Assert(Engine.Purge(listed)>=0&&!Directory.Exists(Path.Combine(legacyVault,legacyId)),"Legacy backup purged from its own vault");

    // Connection table and process resolution work without administrator rights (Windows only: iphlpapi/kernel32).
    if(!windows)return;
    var connections=ConnectionTable.Snapshot();
    Assert(connections!=null,"Connection table readable");
    Assert(connections.All(c=>c.Protocol.StartsWith("TCP")||c.Protocol.StartsWith("UDP")),"Protocols labelled");
    var me=ProcessResolver.Resolve(Process.GetCurrentProcess().Id);
    Assert(me.Name.Equals(Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName),StringComparison.OrdinalIgnoreCase),"Own process resolved by limited query");
    Assert(ConnectionTable.StateName(5)=="Đã kết nối"&&ConnectionTable.StateName(2)=="Đang lắng nghe","State names");
    Assert(EtwNetworkSession.CanStart==Elevation.IsElevated,"ETW availability tracks elevation");
   }finally{
    Engine.Vault=originalVault;Paths.Legacy=originalLegacy;
    if(Path.GetFileName(fixture)==id&&Engine.Under(Engine.Canon(fixture),Engine.Canon(Path.Combine(local,"Temp")))&&Directory.Exists(fixture))Directory.Delete(fixture,true);
   }
  }
 }
}
