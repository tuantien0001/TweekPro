using System;
using System.IO;
using System.Linq;
using System.Text;
using TweekPro.Core;
using TweekPro.Store;

namespace TweekPro.Pup {
 /// <summary>Platform-neutral checks for the PUP/bloatware detector: rule file validity, matching, trusted-vendor refusals, Appx protection and translations.</summary>
 public static class PupTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("PupTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}
  static PupRuleSet ParseJson(string json){using(var s=new MemoryStream(Encoding.UTF8.GetBytes(json)))return PupDetector.Parse(s);}
  static AppEntry App(string name,string publisher){return new AppEntry{Name=name,Publisher=publisher,Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\"+name};}
  static WindowsApp Pkg(string name,int signature=3){return new WindowsApp{Name=name,FullName=name+"_1.0.0.0_x64__8wekyb3d8bbwe",Publisher="CN=Test",SignatureKind=signature};}

  public static void Run(){
   var set=PupDetector.Rules;
   Assert(set.Rules.Count>=10&&set.Rules.Select(r=>r.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==set.Rules.Count,"embedded rules load with unique ids");
   Assert(set.Rules.All(r=>!String.IsNullOrWhiteSpace(r.Reason)&&r.NameRegex.Count+r.PublisherRegex.Count+r.PackageRegex.Count>0),"every rule has a reason and patterns");
   foreach(var r in set.Rules)Assert(L.Has(r.Reason),"rule reason translated: "+r.Id);
   foreach(PupCategory c in Enum.GetValues(typeof(PupCategory)))Assert(L.Has(PupDetector.CategoryLabel(c)),"category label translated: "+c);
   foreach(PupSeverity s in Enum.GetValues(typeof(PupSeverity)))Assert(L.Has(PupDetector.SeverityLabel(s)),"severity label translated: "+s);

   // Desktop matches by name and by publisher; the strongest rule wins.
   var toolbar=PupDetector.Classify(App("Ask Toolbar","APN, LLC"));
   Assert(toolbar!=null&&toolbar.Category==PupCategory.Toolbar&&toolbar.Severity==PupSeverity.High,"Ask Toolbar is a high toolbar");
   var generic=PupDetector.Classify(App("Weather Toolbar","Some Vendor"));
   Assert(generic!=null&&generic.Category==PupCategory.Toolbar&&generic.Severity==PupSeverity.Medium&&generic.MatchedOn=="name","generic toolbar matched on name");
   var byPublisher=PupDetector.Classify(App("System Speedup 3","IObit Information Technology"));
   Assert(byPublisher!=null&&byPublisher.Category==PupCategory.Optimizer&&byPublisher.MatchedOn=="publisher","optimizer matched on publisher");
   var mcafee=PupDetector.Classify(App("McAfee LiveSafe","McAfee, LLC"));
   Assert(mcafee!=null&&mcafee.Category==PupCategory.TrialSecurity&&mcafee.Severity==PupSeverity.Medium,"McAfee LiveSafe flagged as bundled security (medium beats low suite rule)");
   var oem=PupDetector.Classify(App("HP JumpStart Bridge","HP Inc."));
   Assert(oem!=null&&oem.Category==PupCategory.OemBloat&&oem.Severity==PupSeverity.Low,"HP preload flagged (OEMs are not trusted publishers on purpose)");
   Assert(PupDetector.Classify(App("Adobe Flash Player 32 NPAPI","Adobe Systems Incorporated"))!=null,"legacy runtime flagged");
   Assert(PupDetector.Classify(App("Java 8 Update 401","Oracle Corporation"))!=null,"legacy Java flagged as bundled/legacy runtime");
   Assert(PupDetector.Classify(App("Brave","Brave Software Inc"))==null,"Brave is clean");
   Assert(PupDetector.Classify(App("MySQL Server 8.0","Oracle Corporation"))==null,"MySQL is clean");
   Assert(PupDetector.Classify(App("Adobe Acrobat (64-bit)","Adobe"))==null,"Acrobat is clean");
   Assert(PupDetector.Classify(App("HP Smart","HP Inc."))==null,"HP Smart is clean");
   Assert(PupDetector.Classify(App("Visual Studio Community 2022","Microsoft Corporation"))==null,"Visual Studio is clean");

   // Trusted vendors and trusted product names are never flagged, even when a loose glob would match.
   var loose=ParseJson("{\"version\":1,\"rules\":[{\"id\":\"loose\",\"category\":\"Adware\",\"severity\":\"High\",\"names\":[\"*Edge*\",\"*Chrome*\",\"*Firefox*\",\"Steam*\"],\"publishers\":[\"NVIDIA*\"],\"reason\":\"test\"}]}");
   Assert(PupDetector.Classify(App("Microsoft Edge","Microsoft Corporation"),loose)==null,"Microsoft Edge never flagged");
   Assert(PupDetector.Classify(App("Google Chrome","Google LLC"),loose)==null,"Google Chrome never flagged");
   Assert(PupDetector.Classify(App("Mozilla Firefox (x64 vi)","Mozilla"),loose)==null,"Mozilla publisher trusted");
   Assert(PupDetector.Classify(App("NVIDIA Graphics Driver 560.94","NVIDIA Corporation"),loose)==null,"publisher glob loses to the trusted list");
   Assert(PupDetector.Classify(App("Steam","Unknown"),loose)==null,"trusted product name wins over unknown publisher");
   Assert(PupDetector.Classify(App("Edge Toolbar Deluxe","Shady Ltd"),loose)!=null,"unknown publisher still matches");
   Assert(PupDetector.Classify(App("Steam Cleaner Pro","Shady Ltd"),loose)!=null,"trusted name needs a version-like remainder");
   Assert(PupSafety.IsTrustedPublisher("Microsoft Corporation")&&PupSafety.IsTrustedPublisher("NVIDIA Corporation")&&!PupSafety.IsTrustedPublisher("Mindspark Interactive")&&!PupSafety.IsTrustedPublisher("HP Inc.")&&!PupSafety.IsTrustedPublisher(""),"trusted publisher list");
   Assert(PupSafety.IsTrustedName("7-Zip 23.01 (x64)")&&PupSafety.IsTrustedName("Python 3.12.1 (64-bit)")&&PupSafety.IsTrustedName("Steam")&&!PupSafety.IsTrustedName("Steam Cleaner Pro")&&!PupSafety.IsTrustedName(null),"trusted name matching");

   // Appx: promo and optional inbox packages are flagged; protected/caution packages never are, whatever the rule says.
   var candy=PupDetector.Classify(Pkg("king.com.CandyCrushSaga"));
   Assert(candy!=null&&candy.Category==PupCategory.PromoStore&&candy.Severity==PupSeverity.Low,"Candy Crush is store promo");
   Assert(PupDetector.Classify(Pkg("Microsoft.MicrosoftSolitaireCollection"))!=null,"Solitaire is optional inbox");
   Assert(PupDetector.Classify(Pkg("Microsoft.WindowsStore"))==null,"Store (caution) never flagged");
   Assert(PupDetector.Classify(Pkg("Microsoft.Windows.ShellExperienceHost",4))==null,"core protected package never flagged");
   Assert(PupDetector.Classify(Pkg("Microsoft.WindowsCalculator"))==null,"Calculator (caution) never flagged");
   var loosePkg=ParseJson("{\"version\":1,\"rules\":[{\"id\":\"pk\",\"category\":\"PromoStore\",\"severity\":\"Low\",\"packages\":[\"Microsoft.*\"],\"reason\":\"test\"}]}");
   Assert(PupDetector.Classify(Pkg("Microsoft.Windows.Search",4),loosePkg)==null,"protected inbox stays clean under a loose package glob");
   Assert(PupDetector.Classify(new WindowsApp{Name="Microsoft.VCLibs.140.00",IsFramework=true},loosePkg)==null,"framework never flagged");
   Assert(PupDetector.Classify(Pkg("Microsoft.BingNews"),loosePkg)!=null,"removable package matches loose glob");

   // Scan orders by severity then name and tolerates missing sources.
   var scan=PupDetector.Scan(new[]{App("Brave","Brave Software Inc"),App("WildTangent Games","WildTangent"),App("Superfish Inc. VisualDiscovery","Superfish Inc.")},new[]{Pkg("SpotifyAB.SpotifyMusic"),Pkg("Microsoft.WindowsStore")});
   Assert(scan.Count==3&&scan[0].Severity==PupSeverity.High&&scan[0].Category==PupCategory.Adware,"scan sorted with adware first: "+String.Join(",",scan.Select(v=>v.Target)));
   Assert(PupDetector.Scan(null,null).Count==0,"empty scan");
   Assert(PupDetector.Summary(scan).StartsWith("3 mục")&&PupDetector.Summary(PupDetector.Scan(null,null)).StartsWith("Không phát hiện"),"summary text");

   // Rule file validation refuses malformed or overly broad rules so a bad file cannot flag everything.
   MustFail(()=>ParseJson("{\"version\":1,\"rules\":[]}"),"empty rule set refused");
   MustFail(()=>ParseJson("{\"version\":1,\"rules\":[{\"id\":\"a\",\"category\":\"Nope\",\"severity\":\"Low\",\"names\":[\"Foo*\"],\"reason\":\"x\"}]}"),"unknown category refused");
   MustFail(()=>ParseJson("{\"version\":1,\"rules\":[{\"id\":\"a\",\"category\":\"Adware\",\"severity\":\"Huge\",\"names\":[\"Foo*\"],\"reason\":\"x\"}]}"),"unknown severity refused");
   MustFail(()=>ParseJson("{\"version\":1,\"rules\":[{\"id\":\"a\",\"category\":\"Adware\",\"severity\":\"Low\",\"names\":[\"*\"],\"reason\":\"x\"}]}"),"match-all glob refused");
   MustFail(()=>ParseJson("{\"version\":1,\"rules\":[{\"id\":\"a\",\"category\":\"Adware\",\"severity\":\"Low\",\"names\":[\"a*\"],\"reason\":\"x\"}]}"),"single-letter glob refused");
   MustFail(()=>ParseJson("{\"version\":1,\"rules\":[{\"id\":\"a\",\"category\":\"Adware\",\"severity\":\"Low\",\"names\":[\"Foo*\"]}]}"),"missing reason refused");
   MustFail(()=>ParseJson("{\"version\":1,\"rules\":[{\"id\":\"a\",\"category\":\"Adware\",\"severity\":\"Low\",\"reason\":\"x\"}]}"),"rule without patterns refused");
   MustFail(()=>ParseJson("{\"version\":1,\"rules\":[{\"id\":\"a\",\"category\":\"Adware\",\"severity\":\"Low\",\"names\":[\"Foo*\"],\"reason\":\"x\"},{\"id\":\"A\",\"category\":\"Adware\",\"severity\":\"Low\",\"names\":[\"Bar*\"],\"reason\":\"x\"}]}"),"duplicate id refused");

   // English output when the language switches.
   string before=L.Lang;
   try{L.Lang="en";Assert(toolbar.Badge=="Toolbar"&&PupDetector.SeverityLabel(PupSeverity.High)=="Remove"&&toolbar.Reason.StartsWith("Search/home-page hijacker"),"english labels");}
   finally{L.Lang=before;}
  }
 }
}
