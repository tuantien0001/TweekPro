using System;
using System.Linq;
using System.IO;
using System.Threading;
using Microsoft.Win32;
namespace TweekPro {
 public static class AdvancedTests {
  static void Assert(bool value,string message){if(!value)throw new Exception(message);}
  static void Reject(Action action){bool rejected=false;try{action();}catch{rejected=true;}Assert(rejected,"Expected conflict rejection.");}
  public static void Run(){
   string id="TweekProFixture"+Guid.NewGuid().ToString("N"),local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   string originalVault=Engine.Vault,fixture=Path.Combine(local,id),startup=Environment.GetFolderPath(Environment.SpecialFolder.Startup),link=Path.Combine(startup,id+".lnk");
   string view=Advanced.Views[0];string appId="HKCU|"+view+"|SOFTWARE\\"+id+"Missing";
   string valueText="\""+Path.Combine(fixture,"sample.exe")+"\" --test";
   try{
    Directory.CreateDirectory(fixture);Engine.Vault=Path.Combine(fixture,"Backups");
    using(var root=Engine.Base("HKCU",view))using(var key=root.CreateSubKey(Advanced.Run)){key.SetValue(id,valueText,RegistryValueKind.ExpandString);}
    Candidate c;using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(Advanced.Run))c=new Candidate{Kind="RegistryValue",Path=Advanced.Run,Hive="HKCU",View=view,ValueName=id,AppId=appId,AppName=id,ExpectedHash=Advanced.Fingerprint(Advanced.ReadValue(key,id))};
    var entry=Advanced.Autoruns().First(a=>a.Name==id);Assert(entry.Item.ValueName==id,"Autorun enumeration");
    var light=Advanced.Autoruns(false);Assert(light.Any(a=>a.Name==id)&&light.All(a=>a.Item==null||a.Item.Kind!="Service"),"Autoruns(false) keeps Run entries and skips services");
    var b=Advanced.Store(c,true);using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(Advanced.Run))Assert(!key.GetValueNames().Contains(id),"Autorun disabled");
    Engine.Restore(b);using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(Advanced.Run))Assert((string)key.GetValue(id,null,RegistryValueOptions.DoNotExpandEnvironmentNames)==valueText,"Autorun restore content");
    Reject(()=>Engine.Restore(b));
    using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(Advanced.Run,true))key.SetValue(id,valueText+" changed",RegistryValueKind.ExpandString);
    Reject(()=>Advanced.Store(c,true));
    using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(Advanced.Run,true))key.SetValue(id,valueText,RegistryValueKind.ExpandString);
    var cleaned=Engine.Quarantine(c);Engine.Restore(cleaned);
    Directory.CreateDirectory(startup);File.WriteAllText(link,"Synthetic shortcut fixture; not an executable and not a real link.");
    var file=new Candidate{Kind="File",Path=link,ExpectedHash=Advanced.FileHash(link),AppName=id};var fb=Advanced.Store(file,true);Assert(!File.Exists(link),"Startup file disabled");Engine.Restore(fb);Assert(File.Exists(link),"Startup file restored");
    Reject(()=>Advanced.ValidateValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion"));Reject(()=>Advanced.ValidateFile(Path.Combine(local,"unrelated.exe")));
    var app=new AppEntry{Name=id,Location=fixture,Hive="HKCU",View=view,Key="SOFTWARE\\"+id+"Missing"};
    Assert(Advanced.MatchesPath(app,Path.Combine(fixture,"sample.exe")),"Exact child path");Assert(!Advanced.MatchesPath(app,fixture+"Other\\sample.exe"),"Path boundary");
    Directory.CreateDirectory(Path.Combine(fixture,"Programs",id));
    var nested=Advanced.DeepScan(app,CancellationToken.None,new[]{fixture});
    Assert(nested.Items.Any(x=>x.Kind=="RegistryValue"&&x.ValueName==id),"Deep scan Run executable reference");
    Assert(nested.Items.Any(x=>x.Kind=="Folder"&&x.Path.IndexOf(Path.Combine("Programs",id),StringComparison.OrdinalIgnoreCase)>=0),"Deep scan nested Local\\Programs-style leftover");
    var cancelled=new CancellationTokenSource();cancelled.Cancel();Reject(()=>Advanced.DeepScan(app,cancelled.Token,new[]{fixture}));cancelled.Dispose();
    Assert(WindowsTools.Catalog().Count>=18&&WindowsTools.Catalog().Any(t=>t.Name=="Services"&&t.Available),"Windows tool catalog");
    Assert(WindowsTools.Catalog().Any(t=>t.Name=="Services"&&t.IconPath.EndsWith("services.msc",StringComparison.OrdinalIgnoreCase)),"Services uses msc icon path");
    var netInfo=WindowsTools.Catalog().First(t=>t.Name=="Network Information");Assert(netInfo.IconPath.EndsWith("cmd.exe",StringComparison.OrdinalIgnoreCase),"cmd wrappers keep cmd.exe icon");
    var sec=WindowsTools.Catalog().FirstOrDefault(t=>t.Name=="Security Center");if(sec!=null)Assert(sec.IconPath.EndsWith("wscui.cpl",StringComparison.OrdinalIgnoreCase),"Security Center cpl icon path");
    using(var bmp=WindowsTools.Icon(WindowsTools.Catalog().First(t=>t.Name=="Services"),24))Assert(bmp.Width==24&&bmp.Height==24,"Services shell icon size");
    using(var bmp=Presentation.ShellIcon(Path.Combine(Environment.SystemDirectory,"rstrui.exe"),32))Assert(bmp.Width==32&&bmp.Height==32,"ShellIcon scales exe");
   }finally{
    Engine.Vault=originalVault;
    using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(Advanced.Run,true))if(key!=null)key.DeleteValue(id,false);
    if(Path.GetFileName(link)==id+".lnk"&&Engine.Under(Engine.Canon(link),Engine.Canon(startup))&&File.Exists(link))File.Delete(link);
    if(Path.GetFileName(fixture)==id&&Engine.Under(Engine.Canon(fixture),Engine.Canon(local))&&Directory.Exists(fixture))Directory.Delete(fixture,true);
   }
  }
 }
}
