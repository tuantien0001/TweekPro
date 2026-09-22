using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TweekPro.Extensions {
 /// <summary>Fixture Chromium profile (manifest + _locales + Secure Preferences) and Firefox extensions.json exercise name resolution, state, provenance and warnings.</summary>
 public static class ExtensionsTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("ExtensionsTests: "+message);}

  public static void Run(){
   string fixture=Path.Combine(Path.GetTempPath(),"tweekpro-ext-"+Guid.NewGuid().ToString("N"));
   var originalReader=BrowserExtensions.ForcelistReader;
   try{
    string profile=Path.Combine(fixture,"Default");string extRoot=Path.Combine(profile,"Extensions");
    string good="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",bad="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",forced="cccccccccccccccccccccccccccccccc",unpacked="dddddddddddddddddddddddddddddddd";
    Write(Path.Combine(extRoot,good,"1.2.0_0","manifest.json"),"{\"name\":\"__MSG_appName__\",\"description\":\"__MSG_appDesc__\",\"version\":\"1.2.0\",\"default_locale\":\"vi\",\"permissions\":[\"storage\"]}");
    Write(Path.Combine(extRoot,good,"1.2.0_0","_locales","vi","messages.json"),"{\"appName\":{\"message\":\"Tiện ích tốt\"},\"appDesc\":{\"message\":\"Mô tả\"}}");
    Write(Path.Combine(extRoot,good,"1.1.0_0","manifest.json"),"{\"name\":\"Old\",\"version\":\"1.1.0\"}");
    Write(Path.Combine(extRoot,bad,"3.0_0","manifest.json"),"{\"name\":\"Sideloaded Bar\",\"version\":\"3.0\",\"permissions\":[\"tabs\",\"webRequest\"],\"host_permissions\":[\"<all_urls>\"]}");
    Write(Path.Combine(extRoot,forced,"4.1_0","manifest.json"),"{\"name\":\"Search Hijack\",\"version\":\"4.1\"}");
    Write(Path.Combine(extRoot,"notanid","1_0","manifest.json"),"{\"name\":\"Ignored\",\"version\":\"1\"}");
    Write(Path.Combine(profile,"Secure Preferences"),"{\"extensions\":{\"settings\":{"
     +"\""+good+"\":{\"state\":1,\"from_webstore\":true,\"location\":1,\"install_time\":\"13300000000000000\"},"
     +"\""+bad+"\":{\"state\":0,\"from_webstore\":false,\"location\":3},"
     +"\""+forced+"\":{\"state\":1,\"from_webstore\":true,\"location\":9},"
     +"\""+unpacked+"\":{\"state\":1,\"location\":4,\"path\":\"D:\\\\dev\\\\myext\",\"manifest\":{\"name\":\"Dev Ext\",\"version\":\"0.1\",\"permissions\":[\"cookies\"]}},"
     +"\"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee\":{\"state\":1,\"location\":5,\"manifest\":{\"name\":\"Component\",\"version\":\"1\"}}"
     +"}}}");
    Write(Path.Combine(profile,"Preferences"),"{\"profile\":{\"name\":\"Người dùng 1\"}}");
    BrowserExtensions.ForcelistReader=browser=>new HashSet<string>(StringComparer.OrdinalIgnoreCase){forced};
    var p=new BrowserProfile{Browser="Chrome",Folder=profile,Name="Default"};
    var list=BrowserExtensions.ScanChromium(p,BrowserExtensions.Forcelist("Chrome"));
    Assert(list.Count==4,"four extensions listed (component skipped, bad id skipped): "+list.Count);
    var g=list.First(e=>e.Id==good);
    Assert(g.Name=="Tiện ích tốt"&&g.Description=="Mô tả"&&g.Version=="1.2.0"&&g.Enabled&&g.FromStore&&g.Source=="Cửa hàng"&&g.Warning==""&&g.Folder.EndsWith("1.2.0_0"),"localized name, newest version folder, store provenance");
    Assert(g.Installed.Year>=2022&&g.Installed.Year<=2023,"chrome install_time converted: "+g.Installed);
    var b=list.First(e=>e.Id==bad);
    Assert(!b.Enabled&&!b.FromStore&&b.Source=="Cài ngoài (registry/pref)"&&b.Warning.Contains("Không cài từ cửa hàng")&&b.Warning.Contains("Quyền rộng")&&b.Warning.Contains("<all_urls>"),"sideloaded disabled extension flagged with broad permissions: "+b.Warning);
    var f=list.First(e=>e.Id==forced);
    Assert(f.Policy&&f.Source=="Chính sách (ép cài)"&&f.Warning.StartsWith("Ép cài bằng chính sách"),"policy-forced extension flagged");
    var u=list.First(e=>e.Id==unpacked);
    Assert(u.Name=="Dev Ext"&&u.Location==4&&u.Folder==@"D:\dev\myext"&&u.Source=="Giải nén (developer)"&&!u.Warning.Contains("cửa hàng")&&u.Warning.Contains("cookies"),"unpacked extension read from preferences manifest");
    Assert(BrowserExtensions.Localize("__MSG_missing__",Path.Combine(extRoot,good,"1.2.0_0"),"vi")=="__MSG_missing__"&&BrowserExtensions.Localize("Plain",null,null)=="Plain","localization fallbacks");

    string ff=Path.Combine(fixture,"ff.default");
    Write(Path.Combine(ff,"extensions.json"),"{\"addons\":["
     +"{\"id\":\"ublock@x\",\"type\":\"extension\",\"location\":\"app-profile\",\"version\":\"1.6\",\"active\":true,\"userDisabled\":false,\"installDate\":1700000000000,\"path\":\"C:\\\\p\\\\ublock@x.xpi\",\"defaultLocale\":{\"name\":\"uBlock\",\"description\":\"Blocker\"},\"userPermissions\":{\"permissions\":[\"webRequest\"],\"origins\":[\"<all_urls>\"]}},"
     +"{\"id\":\"sys@x\",\"type\":\"extension\",\"location\":\"winreg-app-global\",\"version\":\"2\",\"active\":true,\"userDisabled\":true,\"defaultLocale\":{\"name\":\"Injected\"}},"
     +"{\"id\":\"builtin@mozilla\",\"type\":\"extension\",\"location\":\"app-builtin\",\"version\":\"1\",\"active\":true,\"defaultLocale\":{\"name\":\"Builtin\"}},"
     +"{\"id\":\"theme@x\",\"type\":\"theme\",\"location\":\"app-profile\",\"version\":\"1\",\"active\":false,\"defaultLocale\":{\"name\":\"Dark\"}}"
     +"]}");
    var fx=BrowserExtensions.ScanFirefox(new BrowserProfile{Browser="Firefox",Folder=ff,Name="ff.default",Firefox=true});
    Assert(fx.Count==3,"firefox: builtin skipped, theme kept: "+fx.Count);
    var ub=fx.First(e=>e.Id=="ublock@x");
    Assert(ub.Name=="uBlock"&&ub.Enabled&&ub.Installed.Year==2023&&ub.Warning.Contains("<all_urls>")&&ub.Source=="Hồ sơ người dùng","firefox profile extension parsed");
    var sys=fx.First(e=>e.Id=="sys@x");
    Assert(!sys.Enabled&&sys.Warning.Contains("registry")&&!sys.FromStore,"firefox registry-injected extension flagged");
    Assert(fx.First(e=>e.Id=="theme@x").Source=="Giao diện","theme labelled");

    var notes=new List<string>();
    var all=BrowserExtensions.Scan(new[]{p,new BrowserProfile{Browser="Firefox",Folder=ff,Name="ff.default",Firefox=true},new BrowserProfile{Browser="Edge",Folder=Path.Combine(fixture,"missing"),Name="x"}},notes);
    Assert(all.Count==7&&all.First().Browser=="Chrome","scan merges profiles in browser order: "+all.Count);
    Removal(fixture,profile,extRoot,list,fx,ff);
   }finally{BrowserExtensions.ForcelistReader=originalReader;ExtensionRemoval.PolicyKeyOverride=null;try{Directory.Delete(fixture,true);}catch(Exception){}}
  }

  /// <summary>Removes a store extension and a Firefox add-on through a temp vault, checks the preference surgery and puts both back; on Windows also exercises Forcelist removal against a fixture policy key.</summary>
  static void Removal(string fixture,string profile,string extRoot,List<BrowserExtension> list,List<BrowserExtension> fx,string ff){
   string originalVault=Engine.Vault;Engine.Vault=Path.Combine(fixture,"Vault");Directory.CreateDirectory(Engine.Vault);
   ExtensionRemoval.RunningOverride=browser=>new List<string>();
   try{
    string good=list.First(e=>e.Source=="Cửa hàng").Id,bad=list.First(e=>e.Source=="Cài ngoài (registry/pref)").Id;
    Write(Path.Combine(profile,"Secure Preferences"),"{\"extensions\":{\"settings\":{\""+good+"\":{\"state\":1,\"from_webstore\":true,\"location\":1},\""+bad+"\":{\"state\":0,\"location\":3}},\"pinned_extensions\":[\""+bad+"\",\""+good+"\"]},\"protection\":{\"macs\":{\"extensions\":{\"settings\":{\""+good+"\":\"AAAA\",\""+bad+"\":\"BBBB\"}},\"homepage\":\"CCCC\"},\"super_mac\":\"DDDD\"},\"keep\":{\"unicode\":\"Tiếng Việt\",\"num\":1.5}}");
    Write(Path.Combine(profile,"Preferences"),"{\"profile\":{\"name\":\"Người dùng 1\"},\"extensions\":{\"toolbar\":[\""+good+"\"]}}");
    Assert(ExtensionRemoval.RefuseReason(new BrowserExtension{Browser="Chrome",Id=good,Location=5})!=null,"component extension refused");
    Assert(ExtensionRemoval.RefuseReason(new BrowserExtension{Browser="Chrome",Id="short",Location=1})!=null,"bad id refused");
    Assert(ExtensionRemoval.RefuseReason(fx.First(e=>e.Id=="sys@x"))!=null,"firefox registry-injected add-on refused");
    Assert(ExtensionRemoval.RefuseReason(list.First(e=>e.Id==good))==null,"store extension removable");
    var target=list.First(e=>e.Id==good);target.ProfileFolder=profile;target.Name="Tiện ích tốt";
    var backup=ExtensionRemoval.Remove(target,profile);
    Assert(backup.Kind==ExtensionRemoval.BackupKind&&backup.State=="BackedUp"&&backup.Purpose=="Chrome|"+good,"extension backup created");
    Assert(!Directory.Exists(Path.Combine(extRoot,good))&&Directory.Exists(Path.Combine(extRoot,bad))&&File.Exists(Path.Combine(Engine.Vault,backup.Id,"folder","1.2.0_0","manifest.json")),"extension folder moved to the vault, others untouched");
    string secure=File.ReadAllText(Path.Combine(profile,"Secure Preferences"));
    Assert(!secure.Contains(good)&&secure.Contains("\""+bad+"\":{\"state\":0")&&secure.Contains("\"BBBB\"")&&secure.Contains("\"homepage\":\"CCCC\"")&&secure.Contains("\"super_mac\":\"DDDD\"")&&secure.Contains("Tiếng Việt")&&secure.Contains("1.5"),"settings entry, MAC and pin cut out; everything else byte-compatible: "+secure);
    Assert(secure.Contains("\"pinned_extensions\":[\""+bad+"\"]"),"pinned list keeps the other id");
    Assert(!File.ReadAllText(Path.Combine(profile,"Preferences")).Contains(good),"toolbar reference removed from Preferences");
    var info=Engine.Load<ExtensionBackupInfo>(Path.Combine(Engine.Vault,backup.Id,"extension.xml"));
    Assert(info.Prefs.Count==2&&info.Prefs[0].File=="Secure Preferences"&&info.Prefs[0].Settings.Contains("from_webstore")&&info.Prefs[0].Mac=="\"AAAA\""&&info.Prefs[0].Pinned==1,"payload records settings, MAC and pin index");
    Assert(File.Exists(Path.Combine(Engine.Vault,backup.Id,"prefs","Secure Preferences")),"original preference files copied");
    var rescan=BrowserExtensions.ScanChromium(new BrowserProfile{Browser="Chrome",Folder=profile,Name="Default"},new HashSet<string>());
    Assert(!rescan.Any(e=>e.Id==good)&&rescan.Any(e=>e.Id==bad),"rescan no longer lists the removed extension");
    ExtensionRemoval.Restore(backup);
    Assert(backup.State=="Restored"&&File.Exists(Path.Combine(extRoot,good,"1.2.0_0","manifest.json")),"folder restored");
    secure=File.ReadAllText(Path.Combine(profile,"Secure Preferences"));
    Assert(secure.Contains("\""+good+"\":{\"state\":1,\"from_webstore\":true,\"location\":1}")&&secure.Contains("\""+good+"\":\"AAAA\"")&&secure.Contains("\"pinned_extensions\":[\""+bad+"\",\""+good+"\"]"),"settings, MAC and pin merged back: "+secure);
    ExtensionRemoval.Restore(backup);
    Assert(File.ReadAllText(Path.Combine(profile,"Secure Preferences"))==secure,"second restore is a no-op");
    // Unpacked extension: only the settings entry goes, the developer folder is never touched.
    string dev=Path.Combine(fixture,"dev");Write(Path.Combine(dev,"manifest.json"),"{\"name\":\"Dev\",\"version\":\"1\"}");
    string unpackedId="dddddddddddddddddddddddddddddddd";
    Write(Path.Combine(profile,"Secure Preferences"),"{\"extensions\":{\"settings\":{\""+unpackedId+"\":{\"state\":1,\"location\":4,\"path\":"+System.Text.Json.JsonSerializer.Serialize(dev)+"}}}}");
    var devBackup=ExtensionRemoval.Remove(new BrowserExtension{Browser="Chrome",Profile="Default",ProfileFolder=profile,Id=unpackedId,Name="Dev",Location=4,Folder=dev},profile);
    Assert(File.Exists(Path.Combine(dev,"manifest.json"))&&!File.ReadAllText(Path.Combine(profile,"Secure Preferences")).Contains(unpackedId)&&devBackup.State=="BackedUp","unpacked extension unregistered, folder kept");
    // Firefox: the .xpi moves out of the profile and comes back.
    string xpi=Path.Combine(ff,"extensions","ublock@x.xpi");Write(xpi,"PK");
    var ub=fx.First(e=>e.Id=="ublock@x");ub.Folder=xpi;ub.ProfileFolder=ff;
    var ffBackup=ExtensionRemoval.Remove(ub,ff);
    Assert(!File.Exists(xpi)&&File.Exists(Path.Combine(Engine.Vault,ffBackup.Id,"folder","ublock@x.xpi")),"firefox xpi moved to the vault");
    ExtensionRemoval.Restore(ffBackup);Assert(File.Exists(xpi),"firefox xpi restored");
    Write(Path.Combine(fixture,"outside.xpi"),"PK");ub.Folder=Path.Combine(fixture,"outside.xpi");
    bool refused=false;try{ExtensionRemoval.Remove(ub,ff);}catch(IOException){refused=true;}Assert(refused,"xpi outside the profile refused");
    ExtensionRemoval.RunningOverride=browser=>new List<string>{"chrome"};
    refused=false;try{ExtensionRemoval.Remove(target,profile);}catch(IOException){refused=true;}Assert(refused,"removal refused while the browser runs");
    ExtensionRemoval.RunningOverride=browser=>new List<string>();
    if(Environment.OSVersion.Platform==PlatformID.Win32NT)ForcelistRoundTrip();
   }finally{Engine.Vault=originalVault;ExtensionRemoval.RunningOverride=null;}
  }

  static void ForcelistRoundTrip(){
   const string key=@"Software\TweekProTest\Forcelist";
   ExtensionRemoval.PolicyKeyOverride=new Dictionary<string,string>{{"Chrome",key}};
   using(var hive=Engine.Base("HKCU","64")){
    try{hive.DeleteSubKeyTree(@"Software\TweekProTest\Forcelist",false);}catch(Exception){}
    using(var k=hive.CreateSubKey(key)){k.SetValue("1","cccccccccccccccccccccccccccccccc;https://clients2.google.com/service/update2/crx");k.SetValue("2","ffffffffffffffffffffffffffffffff");}
    var entries=ExtensionRemoval.ForcelistEntries().Where(f=>f.Key==key).ToList();
    Assert(entries.Count==2&&entries.Any(f=>f.Id=="cccccccccccccccccccccccccccccccc"&&f.ValueName=="1")&&entries.Any(f=>f.Id=="ffffffffffffffffffffffffffffffff"),"forcelist entries read from HKCU fixture: "+entries.Count);
    bool refused=false;try{ExtensionRemoval.RemoveForcelist(new[]{new ForcelistEntry{Browser="Chrome",Hive="HKCU",View="64",Key=@"Software\Microsoft\Windows\CurrentVersion\Run",ValueName="x",Value="y",Id="y"}});}catch(IOException){refused=true;}
    Assert(refused,"forcelist removal refuses keys outside the policy table");
    var backup=ExtensionRemoval.RemoveForcelist(entries);
    Assert(backup.Payload=="forcelist.xml"&&backup.State=="BackedUp"&&hive.OpenSubKey(key)==null,"forcelist values removed and the emptied key deleted");
    ExtensionRemoval.Restore(backup);
    using(var k=hive.OpenSubKey(key))Assert(k!=null&&Convert.ToString(k.GetValue("1")).StartsWith("cccccccccccccccccccccccccccccccc;")&&Convert.ToString(k.GetValue("2"))=="ffffffffffffffffffffffffffffffff","forcelist values restored");
    try{hive.DeleteSubKeyTree(@"Software\TweekProTest\Forcelist",false);}catch(Exception){}
   }
   ExtensionRemoval.PolicyKeyOverride=null;
  }

  static void Write(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
 }
}
