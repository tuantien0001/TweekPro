using System;
using System.Linq;
using System.IO;
using System.Threading;
using Microsoft.Win32;
namespace AppCare {
 public static class AdvancedTests {
  static void Assert(bool value,string message){if(!value)throw new Exception(message);}
  static void Reject(Action action){bool rejected=false;try{action();}catch{rejected=true;}Assert(rejected,"Expected conflict rejection.");}
  public static void Run(){
   string id="AppCareFixture"+Guid.NewGuid().ToString("N"),local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   string originalVault=Engine.Vault,fixture=Path.Combine(local,id),startup=Environment.GetFolderPath(Environment.SpecialFolder.Startup),link=Path.Combine(startup,id+".lnk");
   string view=Advanced.Views[0];string appId="HKCU|"+view+"|SOFTWARE\\"+id+"Missing";
   string valueText="\""+Path.Combine(fixture,"sample.exe")+"\" --test";
   try{
    Directory.CreateDirectory(fixture);Engine.Vault=Path.Combine(fixture,"Backups");
    using(var root=Engine.Base("HKCU",view))using(var key=root.CreateSubKey(Advanced.Run)){key.SetValue(id,valueText,RegistryValueKind.ExpandString);}
    Candidate c;using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(Advanced.Run))c=new Candidate{Kind="RegistryValue",Path=Advanced.Run,Hive="HKCU",View=view,ValueName=id,AppId=appId,AppName=id,ExpectedHash=Advanced.Fingerprint(Advanced.ReadValue(key,id))};
    var entry=Advanced.Autoruns().First(a=>a.Name==id);Assert(entry.Item.ValueName==id,"Autorun enumeration");
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
    var result=Advanced.DeepScan(app,CancellationToken.None,new[]{fixture});Assert(result.Items.Any(x=>x.Kind=="RegistryValue"&&x.ValueName==id),"Deep scan Run executable reference");
    var cancelled=new CancellationTokenSource();cancelled.Cancel();Reject(()=>Advanced.DeepScan(app,cancelled.Token,new[]{fixture}));cancelled.Dispose();
    Assert(WindowsTools.Catalog().Count>=15&&WindowsTools.Catalog().Any(t=>t.Name=="Services"&&t.Available),"Windows tool catalog");
   }finally{
    Engine.Vault=originalVault;
    using(var root=Engine.Base("HKCU",view))using(var key=root.OpenSubKey(Advanced.Run,true))if(key!=null)key.DeleteValue(id,false);
    if(Path.GetFileName(link)==id+".lnk"&&Engine.Under(Engine.Canon(link),Engine.Canon(startup))&&File.Exists(link))File.Delete(link);
    if(Path.GetFileName(fixture)==id&&Engine.Under(Engine.Canon(fixture),Engine.Canon(local))&&Directory.Exists(fixture))Directory.Delete(fixture,true);
   }
  }
 }
}
