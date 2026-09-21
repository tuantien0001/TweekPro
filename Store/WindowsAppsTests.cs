using System;
using System.IO;
using System.Linq;
using TweekPro.Core;

namespace TweekPro.Store {
 /// <summary>Self-tests for the Windows-apps engine; everything except the PowerShell call is platform-neutral.</summary>
 public static class WindowsAppsTests {
  static void Assert(bool ok,string what){if(!ok)throw new Exception("WindowsApps test failed: "+what);}
  static void MustFail(Action a,string what){try{a();}catch(IOException){return;}throw new Exception("WindowsApps test failed: expected refusal for "+what);}

  /// <summary>Realistic Get-AppxPackage output used by the tests and by --preview store.</summary>
  public const string SampleJsonForPreview=SampleJson;
  const string SampleJson="[{\"Name\":\"Microsoft.WindowsCalculator\",\"PackageFullName\":\"Microsoft.WindowsCalculator_11.2409.0.0_x64__8wekyb3d8bbwe\",\"PackageFamilyName\":\"Microsoft.WindowsCalculator_8wekyb3d8bbwe\",\"Version\":\"11.2409.0.0\",\"Publisher\":\"CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US\",\"InstallLocation\":\"C:\\\\Program Files\\\\WindowsApps\\\\Microsoft.WindowsCalculator_11.2409.0.0_x64__8wekyb3d8bbwe\",\"IsFramework\":false,\"NonRemovable\":false,\"SignatureKind\":3,\"Architecture\":\"X64\"},"
   +"{\"Name\":\"Microsoft.Windows.ShellExperienceHost\",\"PackageFullName\":\"Microsoft.Windows.ShellExperienceHost_10.0.22621.1_neutral_neutral_cw5n1h2txyewy\",\"PackageFamilyName\":\"Microsoft.Windows.ShellExperienceHost_cw5n1h2txyewy\",\"Version\":\"10.0.22621.1\",\"Publisher\":\"CN=Microsoft Windows, O=Microsoft Corporation, L=Redmond, S=Washington, C=US\",\"InstallLocation\":\"C:\\\\Windows\\\\SystemApps\\\\ShellExperienceHost_cw5n1h2txyewy\",\"IsFramework\":false,\"NonRemovable\":true,\"SignatureKind\":4,\"Architecture\":\"Neutral\"},"
   +"{\"Name\":\"Microsoft.VCLibs.140.00\",\"PackageFullName\":\"Microsoft.VCLibs.140.00_14.0.33519.0_x64__8wekyb3d8bbwe\",\"PackageFamilyName\":\"Microsoft.VCLibs.140.00_8wekyb3d8bbwe\",\"Version\":\"14.0.33519.0\",\"Publisher\":\"CN=Microsoft Corporation\",\"InstallLocation\":\"C:\\\\Program Files\\\\WindowsApps\\\\Microsoft.VCLibs.140.00_14.0.33519.0_x64__8wekyb3d8bbwe\",\"IsFramework\":true,\"NonRemovable\":false,\"SignatureKind\":3,\"Architecture\":\"X64\"},"
   +"{\"Name\":\"Microsoft.WindowsStore\",\"PackageFullName\":\"Microsoft.WindowsStore_22409.1401.6.0_x64__8wekyb3d8bbwe\",\"PackageFamilyName\":\"Microsoft.WindowsStore_8wekyb3d8bbwe\",\"Version\":\"22409.1401.6.0\",\"Publisher\":\"CN=Microsoft Corporation\",\"InstallLocation\":\"C:\\\\Program Files\\\\WindowsApps\\\\Microsoft.WindowsStore_22409.1401.6.0_x64__8wekyb3d8bbwe\",\"IsFramework\":false,\"NonRemovable\":false,\"SignatureKind\":3,\"Architecture\":\"X64\"},"
   +"{\"Name\":\"Microsoft.Windows.Search\",\"PackageFullName\":\"Microsoft.Windows.Search_1.14.0.19041_neutral_neutral_cw5n1h2txyewy\",\"PackageFamilyName\":\"Microsoft.Windows.Search_cw5n1h2txyewy\",\"Version\":\"1.14.0.19041\",\"Publisher\":\"CN=Microsoft Windows\",\"InstallLocation\":\"C:\\\\Windows\\\\SystemApps\\\\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\",\"IsFramework\":false,\"NonRemovable\":false,\"SignatureKind\":4,\"Architecture\":\"Neutral\"},"
   +"{\"Name\":\"king.com.CandyCrushSaga\",\"PackageFullName\":\"king.com.CandyCrushSaga_1.2.3.0_x86__kgqvnymyfvs32\",\"PackageFamilyName\":\"king.com.CandyCrushSaga_kgqvnymyfvs32\",\"Version\":\"1.2.3.0\",\"Publisher\":\"CN=6A90E7A4-0FAA-4342-A76D-3AF7D2AC8811\",\"InstallLocation\":\"C:\\\\Program Files\\\\WindowsApps\\\\king.com.CandyCrushSaga_1.2.3.0_x86__kgqvnymyfvs32\",\"IsFramework\":false,\"NonRemovable\":false,\"SignatureKind\":3,\"Architecture\":\"X86\"}]";

  public static void Run(){
   var apps=WindowsApps.Parse(SampleJson);
   Assert(apps.Count==6,"six packages parsed ("+apps.Count+")");
   var calc=apps.First(a=>a.Name=="Microsoft.WindowsCalculator");var shell=apps.First(a=>a.Name.EndsWith("ShellExperienceHost"));var vclibs=apps.First(a=>a.Name.StartsWith("Microsoft.VCLibs"));var store=apps.First(a=>a.Name=="Microsoft.WindowsStore");var search=apps.First(a=>a.Name=="Microsoft.Windows.Search");var candy=apps.First(a=>a.Name.StartsWith("king.com"));
   Assert(calc.Version=="11.2409.0.0"&&calc.InstallLocation.StartsWith(@"C:\Program Files\WindowsApps\")&&calc.Origin=="Store","fields parsed");
   Assert(calc.DisplayName=="Calculator"&&calc.PublisherName=="Microsoft Corporation","friendly name and publisher");
   Assert(candy.DisplayName=="Candy Crush Saga"&&candy.Status==AppxStatus.Removable,"third-party bloat is removable: "+candy.DisplayName);
   Assert(calc.Status==AppxStatus.Caution&&store.Status==AppxStatus.Caution,"useful inbox apps are Caution, not blocked");
   Assert(shell.Status==AppxStatus.Protected&&vclibs.Status==AppxStatus.Protected&&search.Status==AppxStatus.Protected,"shell, framework and Search protected");
   Assert(apps.First().Status==AppxStatus.Removable&&apps.Last().Status==AppxStatus.Protected,"sorted removable first, protected last");
   Assert(WindowsApps.Parse(SampleJson.Substring(1,SampleJson.IndexOf("},{")-1)+"}").Count==1,"single object accepted");
   Assert(WindowsApps.Parse("").Count==0&&WindowsApps.Parse("[]").Count==0,"empty input");
   var legacy=WindowsApps.Parse("[{\"Name\":\"Microsoft.BingWeather\",\"PackageFullName\":\"Microsoft.BingWeather_4.0_x64__8wekyb3d8bbwe\",\"PackageFamilyName\":\"Microsoft.BingWeather_8wekyb3d8bbwe\",\"Version\":null,\"Publisher\":null,\"InstallLocation\":null,\"IsFramework\":null,\"NonRemovable\":null,\"SignatureKind\":null,\"Architecture\":null}]");
   Assert(legacy.Count==1&&!legacy[0].NonRemovable&&!legacy[0].IsFramework&&legacy[0].SignatureKind==0&&legacy[0].Version==""&&legacy[0].Status==AppxStatus.Removable,"Windows 8.1/older 10 output with null properties parses");
   Assert(WindowsApps.ListScript(false).Contains("[bool]$_.NonRemovable")&&WindowsApps.ListScript(false).Contains("[string]$_.Version"),"list script casts nullable properties");
   MustFail(()=>WindowsApps.RegisterScript("C:\\x`$(calc)"),"register script with expansion characters");
   Assert(WindowsApps.RemoveScript(new WindowsApp{Name="Some.App",FullName="Some.App_1.0_x64__abc"},false,true).Contains("-eq 'Some.App'"),"deprovision filter quotes the name");
   MustFail(()=>WindowsApps.RemoveScript(new WindowsApp{Name="Some.App'; calc; '",FullName="Some.App_1.0_x64__abc"},false,true),"injection in package Name");

   // Protected prefixes stay protected unless explicitly whitelisted.
   Assert(WindowsApps.Classify(new WindowsApp{Name="Microsoft.Windows.SomethingNew",FullName="x"})==AppxStatus.Protected,"unknown inbox package protected by prefix");
   Assert(WindowsApps.Classify(new WindowsApp{Name="Microsoft.Windows.Photos",FullName="x"})!=AppxStatus.Protected,"Photos whitelisted");
   Assert(WindowsApps.Classify(new WindowsApp{Name="MicrosoftWindows.Client.WebExperience",FullName="x"})==AppxStatus.Removable,"Widgets removable");
   Assert(WindowsApps.Classify(new WindowsApp{Name="Microsoft.XboxGamingOverlay",FullName="x"})==AppxStatus.Removable,"Xbox Game Bar removable");
   Assert(WindowsApps.Classify(new WindowsApp{Name="Microsoft.BingWeather",FullName="x",NonRemovable=true})==AppxStatus.Protected,"NonRemovable flag wins");
   Assert(WindowsApps.Classify(null)==AppxStatus.Protected&&WindowsApps.Classify(new WindowsApp())==AppxStatus.Protected,"null/empty protected");
   Assert(WindowsApps.FriendlyName("Microsoft.BingWeather")=="Weather"&&WindowsApps.FriendlyName("Microsoft.XboxGamingOverlay")=="Xbox Game Bar"&&WindowsApps.FriendlyName("SpotifyAB.SpotifyMusic")=="Spotify Music"&&WindowsApps.FriendlyName("Microsoft.549981C3F5F10")=="Cortana","friendly names");
   Assert(WindowsApps.PublisherLabel("CN=Microsoft Windows, O=Microsoft Corporation, L=Redmond")=="Microsoft Windows"&&WindowsApps.PublisherLabel("CN=6A90E7A4-0FAA")=="6A90E7A4-0FAA"&&WindowsApps.PublisherLabel("")=="","publisher label");

   // Scripts: protected packages are refused before any script is produced; quoting and flags are exact.
   MustFail(()=>WindowsApps.RemoveScript(shell,true,true),"removing ShellExperienceHost");
   MustFail(()=>WindowsApps.RemoveScript(vclibs,false,false),"removing a framework");
   MustFail(()=>WindowsApps.RemoveScript(new WindowsApp{Name="Evil.App",FullName="Evil_1.0'; Remove-Item C:\\ -Recurse #"},false,false),"injection in package name");
   string s1=WindowsApps.RemoveScript(candy,false,false);
   Assert(s1.Contains("Remove-AppxPackage -Package 'king.com.CandyCrushSaga_1.2.3.0_x86__kgqvnymyfvs32'")&&!s1.Contains("-AllUsers")&&!s1.Contains("Provisioned"),"user-only removal: "+s1);
   string s2=WindowsApps.RemoveScript(candy,true,true);
   Assert(s2.Contains("-AllUsers")&&s2.Contains("Get-AppxProvisionedPackage -Online")&&s2.Contains("$_.DisplayName -eq 'king.com.CandyCrushSaga'")&&s2.Contains("Remove-AppxProvisionedPackage -Online"),"all-users + deprovision: "+s2);
   string reg=WindowsApps.RegisterScript(@"C:\Windows\SystemApps\Foo");
   Assert(reg.StartsWith("$ErrorActionPreference='Stop'; Add-AppxPackage -DisableDevelopmentMode -Register 'C:\\Windows\\SystemApps\\Foo")&&reg.Contains("AppxManifest.xml'; 'OK'"),"register script: "+reg);
   Assert(WindowsApps.ListScript(true).Contains("Get-AppxPackage -AllUsers")&&WindowsApps.ListScript(false).Contains("Get-AppxPackage |")&&WindowsApps.ListScript(false).Contains("ConvertTo-Json -InputObject @($p)"),"list script forces an array");
   Assert(WindowsApps.StoreLink("Microsoft.WindowsCalculator_8wekyb3d8bbwe")=="ms-windows-store://pdp/?PFN=Microsoft.WindowsCalculator_8wekyb3d8bbwe","store link");
   Assert(PowerShell.Decode(PowerShell.Encode("Get-AppxPackage 'ăâđ'"))=="Get-AppxPackage 'ăâđ'"&&PowerShell.Arguments("x").StartsWith("-NoProfile -NonInteractive -ExecutionPolicy Bypass"),"encoded command round-trip");

   // Logo resolution from a synthetic package folder.
   string root=Path.Combine(Path.GetTempPath(),"tweekpro-appx-"+Guid.NewGuid().ToString("N"));
   try{
    Directory.CreateDirectory(Path.Combine(root,"Assets"));
    File.WriteAllText(Path.Combine(root,"AppxManifest.xml"),"<?xml version=\"1.0\"?><Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\"><Identity Name=\"Test.App\" Version=\"1.0.0.0\" Publisher=\"CN=T\"/><Properties><DisplayName>Test</DisplayName><PublisherDisplayName>T</PublisherDisplayName><Logo>Assets\\StoreLogo.png</Logo></Properties><Applications><Application Id=\"App\"><uap:VisualElements xmlns:uap=\"http://schemas.microsoft.com/appx/manifest/uap/windows10\" DisplayName=\"x\" Description=\"x\" BackgroundColor=\"transparent\" Square150x150Logo=\"Assets\\Wide.png\" Square44x44Logo=\"Assets\\Small.png\"/></Application></Applications></Package>");
    foreach(string f in new[]{"StoreLogo.scale-200.png","StoreLogo.scale-100.png","StoreLogo.scale-400.png","StoreLogo.targetsize-16.png","Wide.png"})File.WriteAllBytes(Path.Combine(root,"Assets",f),new byte[]{1});
    string logo=WindowsApps.ResolveLogo(root);
    Assert(logo!=null&&Path.GetFileName(logo)=="StoreLogo.scale-100.png","smallest scale >= 100 preferred: "+logo);
    File.WriteAllBytes(Path.Combine(root,"Assets","StoreLogo.png"),new byte[]{1});
    Assert(Path.GetFileName(WindowsApps.ResolveLogo(root))=="StoreLogo.png","exact file wins");
    File.Delete(Path.Combine(root,"Assets","StoreLogo.png"));File.Delete(Path.Combine(root,"Assets","StoreLogo.scale-100.png"));File.Delete(Path.Combine(root,"Assets","StoreLogo.scale-200.png"));File.Delete(Path.Combine(root,"Assets","StoreLogo.scale-400.png"));
    Assert(Path.GetFileName(WindowsApps.ResolveLogo(root))=="StoreLogo.targetsize-16.png","any variant as last resort");
    Assert(WindowsApps.ResolveLogo(Path.Combine(root,"missing"))==null&&WindowsApps.ResolveLogo(null)==null,"missing folder yields null");
    Assert(WindowsApps.ResolveAsset(root,@"Assets\Nope.png")==null,"missing asset yields null");
   }finally{try{Directory.Delete(root,true);}catch(Exception){}}

   // Off Windows the PowerShell bridge refuses cleanly instead of crashing.
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT){
    Assert(PowerShell.Executable==null,"no PowerShell off Windows");
    MustFail(()=>WindowsApps.List(false),"listing off Windows");
    int before=Engine.Backups().Count;
    MustFail(()=>WindowsApps.Remove(candy,false,false),"removing off Windows");
    Assert(Engine.Backups().Count==before,"no vault record when PowerShell is unavailable");
   }
  }
 }
}
