using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TweekPro.Sizing {
 /// <summary>Self-tests for size probes: Steam id/VDF parsing, bounded folder measurement, the shared-folder guard and Apply on synthetic entries. No registry writes.</summary>
 public static class InstallSizeTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("InstallSizeTests: "+message);}

  const string LibraryVdf="\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t\t\"label\"\t\t\"\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"228980\"\t\t\"498906162\"\n\t\t}\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t\t\"totalsize\"\t\t\"1000203087872\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"730\"\t\t\"71589381210\"\n\t\t\t\"578080\"\t\t\"52089920020\"\n\t\t}\n\t}\n}\n";
  const string Manifest="\"AppState\"\n{\n\t\"appid\"\t\t\"578080\"\n\t\t\"name\"\t\t\"PUBG: BATTLEGROUNDS\"\n\t\"installdir\"\t\t\"PUBG\"\n\t\"SizeOnDisk\"\t\t\"52089920020\"\n\t\"UserConfig\"\n\t{\n\t\t\"language\"\t\t\"english\"\n\t}\n}\n";

  public static void Run(){
   Assert(SteamLibrary.AppIdOf("Steam App 578080","\"C:\\Program Files (x86)\\Steam\\steam.exe\" steam://uninstall/578080")==578080,"appid from key name");
   Assert(SteamLibrary.AppIdOf(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 730","")==730,"appid from full key path");
   Assert(SteamLibrary.AppIdOf("{GUID}","\"C:\\x\\steam.exe\" steam://uninstall/1466860")==1466860,"appid from uninstall string");
   Assert(SteamLibrary.AppIdOf("Steam","\"C:\\Program Files (x86)\\Steam\\uninstall.exe\"")==0&&SteamLibrary.AppIdOf(null,null)==0,"non-Steam entries give 0");

   var libs=SteamLibrary.ParseLibraryFolders(LibraryVdf);
   Assert(libs.Count==2&&libs[0].Key==@"C:\Program Files (x86)\Steam"&&libs[1].Key==@"D:\SteamLibrary","two libraries with unescaped paths");
   Assert(libs[1].Value[578080]==52089920020L&&libs[1].Value[730]==71589381210L&&libs[0].Value[228980]==498906162L,"per-library app sizes");
   var manifest=SteamLibrary.ParseManifest(Manifest);
   Assert(manifest.AppId==578080&&manifest.Name=="PUBG: BATTLEGROUNDS"&&manifest.InstallDir=="PUBG"&&manifest.SizeOnDisk==52089920020L,"manifest fields");
   Assert(new SteamApp{Library=@"D:\SteamLibrary",InstallDir="PUBG"}.Path==@"D:\SteamLibrary\steamapps\common\PUBG"&&new SteamApp().Path=="","game folder from library + installdir");
   Assert(SteamLibrary.ParseVdf("").Count==0&&SteamLibrary.ParseVdf("\"a\" { \"b\" \"c\" ").Count==1,"empty and truncated VDF do not throw");
   Assert(SteamLibrary.Resolve(0)==null&&SteamLibrary.Resolve(999999999)==null,"unknown appid resolves to nothing");

   Assert(!InstallSize.Measurable(null)&&!InstallSize.Measurable("")&&!InstallSize.Measurable(@"C:\")&&!InstallSize.Measurable(@"\\server\share\app"),"root, empty and UNC are not measurable");
   string pf=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),win=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
   if(!String.IsNullOrEmpty(pf))Assert(!InstallSize.Measurable(pf)&&!InstallSize.Measurable(pf+"\\")&&InstallSize.Measurable(Path.Combine(pf,"VideoLAN","VLC")),"Program Files itself refused, a vendor folder accepted");
   if(!String.IsNullOrEmpty(win))Assert(!InstallSize.Measurable(win)&&!InstallSize.Measurable(Path.Combine(win,"System32","drivers")),"Windows tree refused");
   Assert(InstallSize.ToKB(-1)==0&&InstallSize.ToKB(0)==1&&InstallSize.ToKB(1)==1&&InstallSize.ToKB(1024)==1&&InstallSize.ToKB(1025)==2,"bytes to KB rounds up");

   string root=Path.Combine(Path.GetTempPath(),"tweekpro-size-"+Guid.NewGuid().ToString("N"));
   try{
    Directory.CreateDirectory(Path.Combine(root,"sub","deeper"));
    File.WriteAllBytes(Path.Combine(root,"a.bin"),new byte[3000]);File.WriteAllBytes(Path.Combine(root,"sub","b.bin"),new byte[1000]);File.WriteAllBytes(Path.Combine(root,"sub","deeper","c.bin"),new byte[96]);
    bool partial;
    Assert(FolderSize.Measure(root,TimeSpan.FromSeconds(10),out partial)==4096&&!partial,"recursive sum");
    Assert(FolderSize.Measure(Path.Combine(root,"missing"),TimeSpan.FromSeconds(1),out partial)==-1,"missing folder is -1");
    Assert(FolderSize.Measure(null,TimeSpan.FromSeconds(1),out partial)==-1,"null is -1");
    Assert(InstallSize.Measurable(root),"temp fixture is specific enough to measure");
    var declared=new AppEntry{Name="Declared",Size=500,Location=root,Key="X",Command=""};
    var folder=new AppEntry{Name="Folder",Size=0,Location=root,Key="Y",Command=""};
    var gone=new AppEntry{Name="Gone",Size=0,Location=Path.Combine(root,"nope"),Key="Z",Command=""};
    var steam=new AppEntry{Name="Steam title",Size=0,Location=Path.Combine(root,"nope"),Key="Steam App 999999999",Command="steam://uninstall/999999999"};
    var blank=new AppEntry{Name="Blank",Size=0,Location="",Key="W",Command=""};
    var viaUninstaller=new AppEntry{Name="Uninstaller",Size=0,Location="",Key="U",Command="\""+Path.Combine(root,"sub","unins000.exe")+"\" /SILENT"};
    var viaIcon=new AppEntry{Name="Icon",Size=0,Location="",Key="I",Command="MsiExec.exe /X{1}",DisplayIcon=Path.Combine(root,"sub","deeper","app.exe")+",0"};
    Assert(InstallSize.FolderOf(viaUninstaller)==Path.Combine(root,"sub")&&InstallSize.FolderOf(viaIcon)==Path.Combine(root,"sub","deeper")&&InstallSize.FolderOf(blank)==""&&InstallSize.FolderOf(declared)==root,"folder from location, uninstaller or icon");
    Assert(InstallSize.FolderOf(new AppEntry{Command="\"C:\\Users\\x\\AppData\\Local\\Temp\\setup.exe\" /u"})==""&&InstallSize.FolderOf(new AppEntry{Command="MsiExec.exe /X{1}"})=="","Temp and relative commands give no folder");
    int filled=InstallSize.Apply(new List<AppEntry>{declared,folder,gone,steam,blank,viaUninstaller,viaIcon},TimeSpan.FromSeconds(10));
    Assert(filled==3&&declared.Size==500&&!declared.SizeMeasured,"declared size untouched");
    Assert(folder.Size==4&&folder.SizeMeasured&&!folder.SizePartial,"folder measured to 4 KB: "+folder.Size);
    Assert(viaUninstaller.Size==2&&viaIcon.Size==1,"uninstaller folder 1096 B -> 2 KB, icon folder 96 B -> 1 KB");
    Assert(gone.Size==0&&steam.Size==0&&blank.Size==0,"missing, unknown Steam and blank locations stay unknown");
   }finally{try{Directory.Delete(root,true);}catch(Exception){}}
  }
 }
}
