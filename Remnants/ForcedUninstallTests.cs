using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TweekPro.Remnants {
 /// <summary>Boundaries of the forced-uninstall record builder: name specificity, refused folders, synthetic key never registered.</summary>
 public static class ForcedUninstallTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("ForcedUninstallTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   var refused=new[]{@"C:\Program Files",@"C:\Program Files (x86)",@"C:\ProgramData",@"C:\Users\me\AppData\Local",@"C:\Users\me\AppData\Roaming",@"C:\Users\me",@"C:\Users\me\Documents"}.Select(Engine.Canon).ToList();
   string windows=Engine.Canon(@"C:\Windows");
   MustFail(()=>ForcedUninstall.ValidatePickedFolder(@"D:\",refused,windows),"drive root refused");
   MustFail(()=>ForcedUninstall.ValidatePickedFolder(@"C:\Windows\System32",refused,windows),"Windows tree refused");
   MustFail(()=>ForcedUninstall.ValidatePickedFolder(@"C:\Program Files",refused,windows),"Program Files root refused");
   MustFail(()=>ForcedUninstall.ValidatePickedFolder(@"C:\Users\me",refused,windows),"profile root refused");
   MustFail(()=>ForcedUninstall.ValidatePickedFolder(@"C:\Program Files\Common Files",refused,windows),"shared Common Files refused");
   MustFail(()=>ForcedUninstall.ValidatePickedFolder(@"C:\Users\me\AppData\Local\Microsoft",refused,windows),"AppData\\Microsoft refused");
   ForcedUninstall.ValidatePickedFolder(@"C:\Program Files\Example App",refused,windows);
   ForcedUninstall.ValidatePickedFolder(@"D:\Games\Example",refused,windows);

   Assert(ForcedUninstall.NameIsSpecific("Example Player")&&ForcedUninstall.NameIsSpecific("Notepad++Portable")&&!ForcedUninstall.NameIsSpecific("app")&&!ForcedUninstall.NameIsSpecific("Zoom"),"name specificity");
   Assert(ForcedUninstall.KeyFor("Foo Bar/Baz").EndsWith("TweekPro-Forced-Foo-Bar-Baz"),"key leaf sanitized");
   MustFail(()=>ForcedUninstall.Build("ab","","","",null),"too-short name refused");
   MustFail(()=>ForcedUninstall.Build("Zoom","","","",null),"weak name without folder refused");
   MustFail(()=>ForcedUninstall.Build("Example Player","","","",new[]{new AppEntry{Name="Example Player",Hive="HKLM",View="64",Key="x"}}),"registered app refused");
   MustFail(()=>ForcedUninstall.Build("Example Player",@"C:\Windows","","",null),"Windows folder refused");

   var app=ForcedUninstall.Build("Example Player","","","Example Corp",new List<AppEntry>());
   Assert(app.Name=="Example Player"&&app.Publisher=="Example Corp"&&app.Location==""&&app.KnownExecutables.Count==0,"name-only record");
   Assert(app.Key.StartsWith(ForcedUninstall.KeyPrefix)&&app.Id.Split('|').Length==3,"synthetic id shape");
   if(Environment.OSVersion.Platform==PlatformID.Win32NT)Assert(!Engine.Installed(app.Id),"synthetic key never registered");

   string fixture=Path.Combine(Path.GetTempPath(),"tweekpro-forced-"+Guid.NewGuid().ToString("N"));
   try{
    Directory.CreateDirectory(fixture);File.WriteAllBytes(Path.Combine(fixture,"player.exe"),new byte[16]);File.WriteAllBytes(Path.Combine(fixture,"helper.exe"),new byte[16]);
    var withFolder=ForcedUninstall.Build("Zoom",fixture,"","",null);
    Assert(withFolder.KnownExecutables.Count==2&&withFolder.KnownExecutables.All(Path.IsPathRooted),"folder outside cleanable roots contributes executables only");
    Assert(withFolder.Location==""||Engine.Under(withFolder.Location,Engine.Canon(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))),"location set only inside cleanable roots");
    var withExe=ForcedUninstall.Build("Zoom","",Path.Combine(fixture,"player.exe"),"",null);
    Assert(withExe.KnownExecutables.Count==2&&withExe.KnownExecutables.Any(e=>e.EndsWith("player.exe",StringComparison.OrdinalIgnoreCase)),"exe implies its folder");
    MustFail(()=>ForcedUninstall.Build("Zoom","",Path.Combine(fixture,"missing.exe"),"",null),"missing exe refused");
    var review=ForcedUninstall.ReviewFolder(withFolder,fixture);
    Assert(review.ReviewOnly&&review.Kind=="Review"&&review.AppId==withFolder.Id,"review row for outside folder");
   }finally{try{Directory.Delete(fixture,true);}catch(Exception){}}
  }
 }
}
