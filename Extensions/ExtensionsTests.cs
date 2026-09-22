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
   }finally{BrowserExtensions.ForcelistReader=originalReader;try{Directory.Delete(fixture,true);}catch(Exception){}}
  }

  static void Write(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
 }
}
