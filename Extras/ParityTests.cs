using System;
using System.IO;
using System.Linq;
using TweekPro.Core;
using TweekPro.Store;

namespace TweekPro {
 public static class ParityTests {
  public static void Run(){
   var before=InstallWatch.Ids(new[]{new AppEntry{Hive="HKLM",View="64",Key="a",Name="A"},new AppEntry{Hive="HKLM",View="64",Key="b",Name="B"}});
   var added=InstallWatch.Added(before,new[]{new AppEntry{Hive="HKLM",View="64",Key="b",Name="B"},new AppEntry{Hive="HKLM",View="64",Key="c",Name="New"},new AppEntry{Hive="HKLM",View="64",Key="c",Name="New"}});
   if(added.Count!=1||added[0].Key!="c")throw new Exception("install watch must report only new ids");
   if(InstallWatch.Added(null,null).Count!=0)throw new Exception("install watch accepts empty lists");

   string dir=Path.Combine(Path.GetTempPath(),"tweek-parity-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(dir);
   try{
    string hist=Path.Combine(dir,"h.txt");
    UninstallHistory.Append(hist,"App\tX","id-1",true);
    var rows=UninstallHistory.Load(hist);
    if(rows.Count!=1||rows[0][0]!="App X"||rows[0][3]!="removed")throw new Exception("history line");
    if(UninstallHistory.Load(Path.Combine(dir,"missing.txt")).Count!=0)throw new Exception("missing history");

    Directory.CreateDirectory(Path.Combine(dir,"Users","Me"));
    Directory.CreateDirectory(Path.Combine(dir,"Users","Other"));
    Directory.CreateDirectory(Path.Combine(dir,"Users","Public"));
    Directory.CreateDirectory(Path.Combine(dir,"Users","Default"));
    var others=ProfileScope.OtherProfiles(Path.Combine(dir,"Users"),Path.Combine(dir,"Users","Me"));
    if(others.Count!=1||!others[0].EndsWith("Other",StringComparison.OrdinalIgnoreCase))throw new Exception("other profiles skip Public, Default and the current user");
    if(ProfileScope.DataFolders(others[0]).Length!=2)throw new Exception("data folders");

    var apps=new[]{
     new AppEntry{Name="Editor",Location=@"C:\Apps\Editor",Command=@"C:\Apps\Editor\uninstall.exe"},
     new AppEntry{Name="Other",Location=@"C:\Apps\Other",Command="msiexec /x {x}"}
    };
    var hit=HunterMatch.Find(@"C:\Apps\Editor\bin\editor.exe",apps);
    if(hit==null||hit.Name!="Editor")throw new Exception("hunter matches install folder");
    var byCmd=HunterMatch.Find(@"D:\tools\uninstall.exe",new[]{apps[0]});
    if(byCmd==null||byCmd.Name!="Editor")throw new Exception("hunter matches uninstall command file name");
    if(HunterMatch.Find(@"C:\Windows\notepad.exe",apps)!=null)throw new Exception("hunter must not guess");
    if(HunterMatch.Find("not-a-path",apps)!=null)throw new Exception("hunter rejects a bad path");

    if(UserTools.Refuse(@"C:\Windows\System32\cmd.exe")!="blocked")throw new Exception("cmd refused");
    if(UserTools.Refuse(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")!="blocked")throw new Exception("powershell refused");
    if(UserTools.Refuse("notepad.exe")!="not-rooted")throw new Exception("relative path refused");
    if(UserTools.Refuse(@"C:\Tools\note.txt")!="type")throw new Exception("non exe refused");
    if(UserTools.Refuse(@"C:\Tools\My.exe")!=null)throw new Exception("local exe allowed");
    string tools=Path.Combine(dir,"tools.txt");
    UserTools.Save(tools,new[]{new WindowsTool{Name="Mine",File=@"C:\Tools\My.exe",Arguments="",Description="d",Custom=true},new WindowsTool{Name="Bad",File=@"C:\Windows\System32\cmd.exe"}});
    var loaded=UserTools.Load(tools);
    if(loaded.Count!=1||loaded[0].Name!="Mine"||!loaded[0].Custom)throw new Exception("user tools drop blocked rows");

    var s=new Settings{AiKeyProtected="secret-token-xyz",Language="en",Dark=true};
    string settings=Path.Combine(dir,"settings.json");
    SettingsPort.Export(s,settings);
    if(s.AiKeyProtected!="secret-token-xyz")throw new Exception("export must restore the key");
    string json=File.ReadAllText(settings);
    if(json.IndexOf("secret-token-xyz",StringComparison.Ordinal)>=0)throw new Exception("export must not contain the AI key");
    var back=Settings.Load(settings);
    if(!back.Dark||back.Language!="en")throw new Exception("export keeps other settings");

    if(!UpdateInstall.Allowed("https://github.com/tuantien0001/TweekPro/releases/download/v0.7.9/TweekPro-0.7.9-Setup.exe"))throw new Exception("official setup allowed");
    if(UpdateInstall.Allowed("http://github.com/tuantien0001/TweekPro/releases/download/v0.7.9/Setup.exe"))throw new Exception("http refused");
    if(UpdateInstall.Allowed("https://github.com/other/TweekPro/releases/download/v1/Setup.exe"))throw new Exception("other repo refused");
    if(UpdateInstall.Allowed("https://github.com/tuantien0001/TweekPro/releases/download/v0.7.9/notes.zip"))throw new Exception("non-exe refused");
    if(UpdateInstall.Arguments.IndexOf("VERYSILENT",StringComparison.Ordinal)<0)throw new Exception("silent args");

    var ok=new WindowsApp{Name="Contoso.App",FullName="Contoso.App_1.0.0.0_x64__abc"};
    if(StoreActions.ResetScript(ok).IndexOf("Reset-AppxPackage",StringComparison.Ordinal)<0)throw new Exception("reset script");
    if(StoreActions.StopScript(ok).IndexOf("Stop-AppxPackage",StringComparison.Ordinal)<0)throw new Exception("stop script");
    bool refused=false;
    try{StoreActions.ResetScript(new WindowsApp{Name="Microsoft.Windows.StartMenuExperienceHost",FullName="Microsoft.Windows.StartMenuExperienceHost_1.0.0.0_neutral__cw5n1h2txyewy"});}catch(IOException){refused=true;}
    if(!refused)throw new Exception("protected package must not get a reset script");
    bool bad=false;
    try{StoreActions.StopScript(new WindowsApp{Name="Contoso.App",FullName="bad name"});}catch(IOException){bad=true;}
    if(!bad)throw new Exception("bad package name refused");
   }finally{try{Directory.Delete(dir,true);}catch(Exception){}}
  }
 }
}
