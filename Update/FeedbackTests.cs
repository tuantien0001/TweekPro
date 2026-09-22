using System;
using System.Collections.Generic;

namespace TweekPro.Update {
 /// <summary>Self-tests for the GitHub Issues link builder (no network).</summary>
 public static class FeedbackTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("FeedbackTests: "+message);}

  public static void Run(){
   string bug=Feedback.BugUrl("0.7.7","Windows 11 Pro 24H2 (build 26100)","Tab Ứng dụng: lỗi khi gỡ");
   Assert(bug.StartsWith(Feedback.NewIssue+"?template=bug_report.yml&"),"bug link targets the bug form: "+bug);
   Assert(bug.Contains("&version=0.7.7")&&bug.Contains("&windows=Windows%2011%20Pro%2024H2%20%28build%2026100%29"),"version and windows prefilled and escaped");
   Assert(bug.Contains("&what=Tab%20%E1%BB%A8ng%20d%E1%BB%A5ng%3A%20l%E1%BB%97i%20khi%20g%E1%BB%A1"),"Vietnamese context UTF-8 escaped: "+bug);
   Assert(!Feedback.BugUrl("0.7.7","Win","").Contains("what="),"empty context adds no field");
   Assert(Feedback.FeatureUrl("0.7.7")==Feedback.NewIssue+"?template=feature_request.yml&version=0.7.7","feature link");
   Assert(Feedback.FeedbackUrl("0.7.7","Windows 10 22H2")==Feedback.NewIssue+"?template=feedback.yml&version=0.7.7&windows=Windows%2010%2022H2","feedback link");
   Assert(Feedback.FeedbackUrl("0.7.7",null)==Feedback.NewIssue+"?template=feedback.yml&version=0.7.7","null field skipped");

   string longText=new string('x',20000);
   string cut=Feedback.BugUrl("0.7.7","Win",longText);
   Assert(cut.Length<=Feedback.MaxUrlLength&&cut.EndsWith("%E2%80%A6")&&cut.Contains("&what=xxxx"),"long context trimmed with ellipsis, url within limit: "+cut.Length);
   Assert(Feedback.Build(new List<KeyValuePair<string,string>>())==Feedback.NewIssue,"no fields → plain new-issue page");

   Assert(Feedback.Compose("Windows 11 Pro","24H2","26100")=="Windows 11 Pro 24H2 (build 26100)","compose full label");
   Assert(Feedback.Compose("Windows 10 Home","","19045")=="Windows 10 Home (build 19045)","compose without display version");
   Assert(Feedback.Compose(" Windows 7 Ultimate ",null,null)=="Windows 7 Ultimate","compose trims and skips empties");
   Assert(Feedback.Product("Windows 10 Pro","22631")=="Windows 11 Pro"&&Feedback.Product("Windows 10 Home","19045")=="Windows 10 Home","Windows 10 ProductName with build ≥22000 is Windows 11");
   Assert(Feedback.Product("Windows Server 2022 Datacenter","20348")=="Windows Server 2022 Datacenter"&&Feedback.Product("Windows Server 2025 Datacenter","26100")=="Windows Server 2025 Datacenter","server editions untouched");
   Assert(Feedback.Product("Windows 10 Pro","abc")=="Windows 10 Pro","unparseable build leaves the name alone");
   string live=Feedback.WindowsLabel();Assert(!String.IsNullOrWhiteSpace(live),"live label never empty");
  }
 }
}
