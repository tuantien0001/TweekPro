using System;
using System.Linq;
using TweekPro.Core;

namespace TweekPro.Services {
 /// <summary>Platform-neutral self-tests for the service knowledge base and explanation heuristics.</summary>
 public static class ServiceCatalogTests {
  static void Assert(bool ok,string what){if(!ok)throw new Exception("ServiceCatalog test failed: "+what);}

  public static void Run(){
   var kb=ServiceCatalog.LoadEmbedded();
   Assert(kb.Services.Count>=100,"knowledge base has at least 100 services");
   Assert(kb.Patterns.Count>=5,"knowledge base has name patterns");
   Assert(kb.Services.Select(s=>s.Name.ToLowerInvariant()).Distinct().Count()==kb.Services.Count,"no duplicate service names");
   foreach(var s in kb.Services)Assert(s.Friendly!=""&&s.Vi!=""&&s.En!=""&&s.Safety!="","entry "+s.Name+" complete");
   foreach(var c in ProcessControl_CoreServices())Assert(kb.Services.Any(s=>String.Equals(s.Name,c,StringComparison.OrdinalIgnoreCase)),"core service "+c+" is documented");

   Assert(ServiceCatalog.BaseNameOf("cbdhsvc_4a2b1")=="cbdhsvc","per-user suffix stripped");
   Assert(ServiceCatalog.BaseNameOf("AarSvc_a3ac9")=="AarSvc","per-user suffix stripped (5 hex)");
   Assert(ServiceCatalog.BaseNameOf("Spooler")=="Spooler","no suffix untouched");
   Assert(ServiceCatalog.BaseNameOf("postgresql-x64-16")=="postgresql-x64-16","dash names untouched");

   Assert(ServiceCatalog.ExecutableOf("\"C:\\Program Files\\Adobe\\Adobe Acrobat\\armsvc.exe\"")=="C:\\Program Files\\Adobe\\Adobe Acrobat\\armsvc.exe","quoted path");
   Assert(ServiceCatalog.ExecutableOf("C:\\WINDOWS\\system32\\svchost.exe -k netsvcs -p")=="C:\\WINDOWS\\system32\\svchost.exe","svchost group");
   Assert(ServiceCatalog.ExecutableOf("C:\\Windows\\System32\\spoolsv.exe")=="C:\\Windows\\System32\\spoolsv.exe","plain path");
   Assert(ServiceCatalog.ExecutableOf("")=="","empty path");

   Assert(ServiceCatalog.AppNameOf("Brave Update Service (brave)","brave")=="Brave","app name from display name");
   Assert(ServiceCatalog.AppNameOf("","Zoom")=="Zoom","falls back to name");

   string saved=L.Lang;
   try{
    L.Lang=L.Vietnamese;
    var spooler=new ServiceEntry{Name="Spooler",DisplayName="Print Spooler",PathName="C:\\Windows\\System32\\spoolsv.exe"};
    ServiceCatalog.Explain(spooler);
    Assert(spooler.Safety==ServiceSafety.Optional,"Spooler optional");
    Assert(spooler.Friendly=="Print Spooler"&&spooler.Explanation.Contains("máy in"),"Spooler explained in Vietnamese");

    var rpc=new ServiceEntry{Name="RpcSs",DisplayName="Remote Procedure Call (RPC)"};
    ServiceCatalog.Explain(rpc);
    Assert(rpc.Safety==ServiceSafety.Core,"RpcSs core");

    var perUser=new ServiceEntry{Name="cbdhsvc_4a2b1",DisplayName="Clipboard User Service_4a2b1"};
    ServiceCatalog.Explain(perUser);
    Assert(perUser.PerUser&&perUser.Friendly=="Clipboard User Service"&&perUser.Explanation.Contains("theo tài khoản"),"per-user service resolved to base entry");

    var updater=new ServiceEntry{Name="ZoomUpdateService",DisplayName="Zoom Update Service",PathName="\"C:\\Program Files\\Zoom\\bin\\ZoomUpdater.exe\""};
    ServiceCatalog.Explain(updater);
    Assert(updater.Safety==ServiceSafety.ThirdParty&&updater.Category=="updater"&&updater.Explanation.Contains("Zoom")&&!updater.Explanation.Contains("{app}"),"updater pattern with app name");

    var unknownThird=new ServiceEntry{Name="FooSvc",DisplayName="Foo Background",PathName="C:\\Program Files\\Foo\\foo.exe",Publisher="Foo Inc.",Description="Foo background worker."};
    ServiceCatalog.Explain(unknownThird);
    Assert(unknownThird.Safety==ServiceSafety.ThirdParty&&unknownThird.Explanation.Contains("Foo Inc.")&&unknownThird.Explanation.Contains("Foo background worker."),"unknown third-party explained with publisher and description");

    if(Environment.OSVersion.Platform==PlatformID.Win32NT){
     string windir=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
     var unknownWin=new ServiceEntry{Name="MysterySvc",DisplayName="Mystery",PathName=windir+"\\System32\\svchost.exe -k foo",Description="Does things."};
     ServiceCatalog.Explain(unknownWin);
     Assert(unknownWin.Safety==ServiceSafety.Windows&&unknownWin.Explanation=="Does things.","unknown Windows service uses description");
    }

    L.Lang=L.EnglishCode;
    var spoolerEn=new ServiceEntry{Name="Spooler",DisplayName="Print Spooler"};
    ServiceCatalog.Explain(spoolerEn);
    Assert(spoolerEn.Explanation.Contains("printers"),"Spooler explained in English");
    Assert(ServiceCatalog.SafetyLabel(ServiceSafety.Core).StartsWith("Core"),"safety label localized");
    Assert(ServiceCatalog.CategoryLabel("updater")=="Updates","category label localized");
   }finally{L.Lang=saved;}

   Assert(new ServiceEntry{State="Running"}.Running&&!new ServiceEntry{State="Stopped"}.Running,"Running flag");
   var hosted=new ServiceEntry{Name="X",DisplayName="Y",State="Running",StartMode="Auto",PathName="p",Pid=7}.ToHosted();
   Assert(hosted.Name=="X"&&hosted.Pid==7&&hosted.StartMode=="Auto","ToHosted copies fields");

   if(Environment.OSVersion.Platform!=PlatformID.Win32NT){
    Assert(ServiceCatalog.List().Count==0,"List empty off Windows");
    bool threw=false;try{ServiceCatalog.Start("Spooler");}catch(System.IO.IOException){threw=true;}
    Assert(threw,"Start refuses off Windows");
   }
   bool bad=false;try{ServiceCatalog.Start("bad name;rm");}catch(System.IO.IOException){bad=true;}
   Assert(bad,"Start validates service name");
  }

  static string[] ProcessControl_CoreServices(){return Network.ProcessControl.CoreServices;}
 }
}
