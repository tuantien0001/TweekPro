using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TweekPro.SysSpace {
 /// <summary>Catalog shape, driver projection parsing and duplicate selection, exact tool command lines, bounded measurement of a fixture folder.</summary>
 public static class SystemSpaceTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("SystemSpaceTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   var items=SystemSpace.Catalog();
   Assert(items.Count==9&&items.Select(i=>i.Id).Distinct().Count()==9&&items.All(i=>!String.IsNullOrEmpty(i.ToolCommand)&&!String.IsNullOrEmpty(i.ToolLabel)),"catalog complete");
   Assert(items.Where(i=>!i.Info).All(i=>!String.IsNullOrEmpty(i.Warning)),"every action item carries a warning");
   foreach(var it in items.Where(i=>!i.Vaulted)){var cmds=SystemSpace.Commands(it,new[]{new DriverPackage{Published="oem12.inf"}});Assert(cmds.Count>=1,"command for "+it.Id);}
   Assert(items.Count(i=>i.Vaulted)==1&&SystemSpace.Commands(items.First(i=>i.Vaulted)).Count==0,"the vaulted item has no external command");
   Assert(SystemSpace.Commands(items.First(i=>i.Id=="windows-old"))[0].Value=="/sagerun:"+SystemSpace.SageSet,"cleanmgr sageset");
   Assert(SystemSpace.Commands(items.First(i=>i.Id=="winsxs"))[0].Value=="/Online /Cleanup-Image /StartComponentCleanup","DISM never uses /ResetBase");
   var pnp=SystemSpace.Commands(items.First(i=>i.Id=="driver-store"),new[]{new DriverPackage{Published="oem12.inf"},new DriverPackage{Published="nvhda.inf"},new DriverPackage{Published="oem7.inf; & del"}});
   Assert(pnp.Count==1&&pnp[0].Value=="/delete-driver oem12.inf"&&!pnp[0].Value.Contains("force"),"pnputil only for oemNN.inf published names, never /force");
   Assert(SystemSpace.Commands(items.First(i=>i.Id=="hiberfil"))[0].Value=="/hibernate off","powercfg hibernate off");

   string sample=String.Join("\n",new[]{
    @"oem12.inf|C:\Windows\System32\DriverStore\FileRepository\nvhda.inf_amd64_aaa\nvhda.inf|NVIDIA Corporation|MEDIA|1.4.1.2|2023-05-17|False|False",
    @"oem31.inf|C:\Windows\System32\DriverStore\FileRepository\nvhda.inf_amd64_bbb\nvhda.inf|NVIDIA Corporation|MEDIA|1.3.40.14|2022-11-02|False|False",
    @"oem40.inf|C:\Windows\System32\DriverStore\FileRepository\nvhda.inf_amd64_ccc\nvhda.inf|NVIDIA Corporation|MEDIA|1.3.38.16|2022-01-10|False|False",
    @"oem5.inf|C:\Windows\System32\DriverStore\FileRepository\iastorac.inf_amd64_ddd\iastorac.inf|Intel|SCSIAdapter|18.1.0.1015|2021-06-01|True|False",
    @"oem6.inf|C:\Windows\System32\DriverStore\FileRepository\iastorac.inf_amd64_eee\iastorac.inf|Intel|SCSIAdapter|17.9.0.1008|2020-06-01|True|False",
    @"oem9.inf|C:\Windows\System32\DriverStore\FileRepository\rt640x64.inf_amd64_fff\rt640x64.inf|Realtek|Net|10.50.511.2021|2021-05-11|False|False",
    @"usbxhci.inf|C:\Windows\System32\DriverStore\FileRepository\usbxhci.inf_amd64_ggg\usbxhci.inf|Microsoft|USB|10.0.22621.1|2006-06-21|True|True",
    @"usbxhci.inf|C:\Windows\System32\DriverStore\FileRepository\usbxhci.inf_amd64_hhh\usbxhci.inf|Microsoft|USB|10.0.22000.1|2006-06-21|True|True",
    "garbage line without pipes","oemX.inf|only|three"});
   var drivers=SystemSpace.ParseDrivers(sample);
   Assert(drivers.Count==8&&drivers[0].Published=="oem12.inf"&&drivers[0].Inf=="nvhda.inf"&&drivers[3].BootCritical&&drivers[6].Inbox,"driver projection parsed, malformed lines skipped");
   var older=SystemSpace.Duplicates(drivers);
   Assert(older.Count==2&&older.All(d=>d.Inf=="nvhda.inf")&&older.Select(d=>d.Published).OrderBy(s=>s).SequenceEqual(new[]{"oem31.inf","oem40.inf"}),"only older third-party duplicates proposed; newest, boot-critical and inbox kept: "+String.Join(",",older.Select(d=>d.Published)));
   Assert(SystemSpace.ParseVersion("1.3.40.14")<SystemSpace.ParseVersion("1.4.1.2")&&SystemSpace.ParseVersion("10")==new Version(10,0)&&SystemSpace.ParseVersion("x.y")==new Version(0,0),"lenient version parsing");

   string fixture=Path.Combine(Path.GetTempPath(),"tweekpro-space-"+Guid.NewGuid().ToString("N"));
   try{
    Directory.CreateDirectory(Path.Combine(fixture,"sub"));File.WriteAllBytes(Path.Combine(fixture,"a.bin"),new byte[4096]);File.WriteAllBytes(Path.Combine(fixture,"sub","b.bin"),new byte[1024]);
    var old=new SpaceItem{Id="windows-old",Location=fixture};SystemSpace.Measure(old,TimeSpan.FromSeconds(5));
    Assert(old.Available&&old.Bytes==5120&&!old.Partial,"folder item measured: "+old.Bytes);
    var missing=new SpaceItem{Id="windows-old",Location=Path.Combine(fixture,"nope")};SystemSpace.Measure(missing,TimeSpan.FromSeconds(1));
    Assert(!missing.Available&&missing.Bytes==-1&&missing.Note!="","missing folder reported as unavailable");
    var hib=new SpaceItem{Id="hiberfil",Location=Path.Combine(fixture,"a.bin")};SystemSpace.Measure(hib,TimeSpan.FromSeconds(1));
    Assert(hib.Available&&hib.Bytes==4096,"single-file item measured");
    var store=new SpaceItem{Id="driver-store"};SystemSpace.Measure(store,TimeSpan.FromSeconds(1),drivers);
    Assert(store.Available&&store.Details.Count==2&&store.Note.StartsWith("2 "),"driver-store item summarizes older packages from supplied list");
    Assert(old.Reclaim==old.Bytes&&hib.Reclaim==4096&&store.Reclaim==store.Bytes,"reclaim estimate equals the measured size for whole-folder items");
    Assert(SystemSpace.Tail(new ToolResult{Output="a\r\nb\r\n\r\nc",Error="d"},2)=="c\r\nd","tail keeps the last lines");
    var lines=new List<string>();string all=SystemSpace.ReadLines(new StringReader("Deployment Image\r\n[====   10.0%   ]\r[====   55.5%   ]\r\nThe operation completed successfully.\n"),lines.Add);
    Assert(lines.Count==4&&lines[1]=="[====   10.0%   ]"&&lines[2]=="[====   55.5%   ]"&&all.Split('\n').Length==5,"CR-separated progress surfaces as separate lines: "+lines.Count);
    var dism=SystemSpace.ParseAnalyze("Deployment Image Servicing and Management tool\r\nVersion: 10.0.22621.1\r\n\r\nImage Version: 10.0.22631.4317\r\n\r\n[==========================100.0%==========================]\r\n\r\nComponent Store (WinSxS) information:\r\n\r\nWindows Explorer Reported Size of Component Store : 8.02 GB\r\n\r\nActual Size of Component Store : 7.81 GB\r\n\r\n    Shared with Windows : 6.49 GB\r\n    Backups and Disabled Features : 1.31 GB\r\n    Cache and Temporary Data :  0 bytes\r\n\r\nDate of Last Cleanup : 2024-01-01 10:00:00\r\n\r\nNumber of Reclaimable Packages : 3\r\nComponent Store Cleanup Recommended : Yes\r\n\r\nThe operation completed successfully.");
    Assert(dism.Count==5&&Math.Abs(dism[0]-8.02*1024*1024*1024)<2&&Math.Abs(dism[3]-1.31*1024*1024*1024)<2&&dism[4]==0,"DISM analysis sizes parsed in order: "+String.Join(",",dism));
    var dismVi=SystemSpace.ParseAnalyze("Kích cỡ theo Explorer : 8,02 GB\r\nKích cỡ thực : 7,81 GB\r\n    Chia sẻ với Windows : 6,49 GB\r\n    Bản sao lưu và tính năng đã tắt : 512 MB\r\n    Bộ đệm và dữ liệu tạm : 12 KB\r\n");
    Assert(dismVi.Count==5&&dismVi[3]==512L*1024*1024&&dismVi[4]==12*1024,"localized labels and decimal commas still parse");
    InstallerOrphans(fixture);
    if(Environment.OSVersion.Platform!=PlatformID.Win32NT)MustFail(()=>SystemSpace.Run(old,null,CancellationToken.None),"tools never run off Windows");
   }finally{try{Directory.Delete(fixture,true);}catch(Exception){}}
  }

  /// <summary>Orphan detection on a fixture "Installer" folder (referenced, fresh, non-package and nested files are all kept), then the vault move and restore of the orphans.</summary>
  static void InstallerOrphans(string fixture){
   string installer=Path.Combine(fixture,"Installer");Directory.CreateDirectory(Path.Combine(installer,"{GUID}"));
   var oldTime=DateTime.Now.AddDays(-30);
   foreach(string name in new[]{"kept.msi","orphan1.msi","orphan2.msp","fresh.msi","notes.txt"}){string p=Path.Combine(installer,name);File.WriteAllBytes(p,new byte[name.StartsWith("orphan1")?4096:1024]);if(name!="fresh.msi"){File.SetLastWriteTime(p,oldTime);File.SetCreationTime(p,oldTime);}}
   string nested=Path.Combine(installer,"{GUID}","nested.msi");File.WriteAllBytes(nested,new byte[512]);File.SetLastWriteTime(nested,oldTime);File.SetCreationTime(nested,oldTime);
   var referenced=new HashSet<string>(StringComparer.OrdinalIgnoreCase){Path.Combine(installer,"KEPT.MSI").ToUpperInvariant()};
   var orphans=SystemSpace.Orphans(installer,referenced,DateTime.Now-SystemSpace.InstallerMinAge);
   Assert(orphans.Count==2&&orphans[0].Name=="orphan1.msi"&&orphans[1].Name=="orphan2.msp","only unreferenced, old, top-level .msi/.msp files are orphans (largest first): "+String.Join(",",orphans.Select(o=>o.Name)));
   Assert(SystemSpace.Orphans(installer,new HashSet<string>(StringComparer.OrdinalIgnoreCase),DateTime.Now.AddDays(-60)).Count==0,"nothing older than 60 days");
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return;
   string originalVault=Engine.Vault;Engine.Vault=Path.Combine(fixture,"Vault");
   try{
    var item=new SpaceItem{Id="installer-orphans",Name="Installer",Location=installer};item.Paths.AddRange(orphans.Select(o=>o.FullName));
    item.Paths.Add(Path.Combine(installer,"notes.txt"));item.Paths.Add(Path.Combine(fixture,"outside.msi"));
    int products;var live=SystemSpace.ReferencedPackages(out products);
    if(products==0){Core.Log.Warn("SystemSpaceTests: no MSI products readable, vault move skipped.");return;}
    Assert(live.All(p=>p.IndexOf('\\')>0),"referenced packages are full paths");
    var backup=SystemSpace.MoveOrphans(item,CancellationToken.None);
    Assert(backup.Kind==SystemSpace.InstallerBackupKind&&backup.State=="NeedsReview"&&backup.Error.Contains("notes.txt")&&backup.Bytes==4096+1024,"orphans moved; the .txt and the outside file were refused and reported: "+backup.Error);
    Assert(!File.Exists(Path.Combine(installer,"orphan1.msi"))&&!File.Exists(Path.Combine(installer,"orphan2.msp"))&&File.Exists(Path.Combine(installer,"kept.msi"))&&File.Exists(Path.Combine(installer,"fresh.msi"))&&File.Exists(Path.Combine(installer,"notes.txt")),"only the orphans left the folder");
    var listed=Engine.Backups().First(b=>b.Id==backup.Id);
    SystemSpace.RestoreInstaller(listed);
    Assert(listed.State=="Restored"&&File.Exists(Path.Combine(installer,"orphan1.msi"))&&File.Exists(Path.Combine(installer,"orphan2.msp")),"orphans restored into the Installer folder");
    var bogus=new Backup{Id=backup.Id,Kind="Junk"};MustFail(()=>SystemSpace.RestoreInstaller(bogus),"restore refuses other kinds");
   }finally{Engine.Vault=originalVault;}
  }
 }
}
