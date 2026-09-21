using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TweekPro.Remnants {
 /// <summary>Self-tests for the trace hunter: name variants, cache/rule parsers, matchers, the value allow-list and a bounded smoke run on Windows.</summary>
 public static class TraceHunterTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("TraceHunterTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   Variants();Parsers();Matchers();AllowList();
   if(Core.StubbornFiles.IsWindows)Smoke();
  }

  static void Variants(){
   var ps=TraceHunter.NameVariants(new AppEntry{Name="Adobe Photoshop 2026",Publisher="Adobe Inc.",KnownExecutables=new List<string>{@"C:\Program Files\Adobe\Adobe Photoshop 2026\Photoshop.exe",@"C:\Program Files\Adobe\Adobe Photoshop 2026\Uninstall.exe",@"C:\Program Files\Adobe\Adobe Photoshop 2026\setup.exe"}});
   Assert(ps.Contains("Adobe Photoshop")&&ps.Contains("Photoshop")&&ps.Contains("AdobePhotoshop"),"version, publisher prefix and collapsed variants: "+String.Join(", ",ps));
   Assert(!ps.Contains("2026")&&!ps.Contains("Adobe")&&!ps.Contains("Uninstall")&&!ps.Contains("setup")&&!ps.Contains("Adobe Photoshop 2026"),"no year, publisher, generic exe or original name: "+String.Join(", ",ps));
   var zip=TraceHunter.NameVariants(new AppEntry{Name="7-Zip 23.01 (x64)",Publisher="Igor Pavlov"});
   Assert(zip.Contains("7-Zip")&&!zip.Any(v=>v.Contains("23")),"numeric version and parenthesis stripped: "+String.Join(", ",zip));
   var edge=TraceHunter.NameVariants(new AppEntry{Name="Microsoft Edge",Publisher="Microsoft Corporation"});
   Assert(edge.Contains("Edge")&&!TraceHunter.Strong("Edge")&&TraceHunter.Strong("Photoshop")&&TraceHunter.Strong("Adobe Photoshop")&&!TraceHunter.Strong("Tools")&&!TraceHunter.Strong("12345678"),"strength: short single words are weak, generic and numeric never strong");
   var tools=TraceHunter.NameVariants(new AppEntry{Name="Tools",Publisher="Acme"});Assert(tools.Count==0,"generic name yields no variants");
   var vs=TraceHunter.NameVariants(new AppEntry{Name="Microsoft Visual Studio Code (User)",Publisher="Microsoft Corporation",KnownExecutables=new List<string>{@"C:\Users\x\AppData\Local\Programs\Microsoft VS Code\Code.exe"}});
   Assert(vs.Contains("Microsoft Visual Studio Code")&&vs.Contains("Visual Studio Code")&&vs.Contains("Code")&&!TraceHunter.Strong("Code"),"VS Code variants: "+String.Join(", ",vs));
   Assert(TraceHunter.NameVariants(null).Count==0&&TraceHunter.NameVariants(new AppEntry()).Count==0,"null-safe");
   Assert(TraceHunter.ExeBaseNames(new AppEntry{KnownExecutables=new List<string>{@"C:\A\Agent.exe",@"C:\A\agent.exe",@"C:\A\unins000.exe",@"C:\A\App.exe",@"C:\A\ab.exe"}}).Count==1,"exe base names distinct, generic and short excluded");
  }

  static void Parsers(){
   Assert(TraceHunter.MuiCacheProgram(@"C:\Tools\agent.exe.FriendlyAppName")==@"C:\Tools\agent.exe"&&TraceHunter.MuiCacheProgram(@"C:\Tools\agent.exe.ApplicationCompany")==@"C:\Tools\agent.exe"&&TraceHunter.MuiCacheProgram("LangID")=="LangID"&&TraceHunter.MuiCacheProgram(null)=="","MuiCache value names");
   Assert(TraceHunter.FirewallProgram(@"v2.31|Action=Allow|Active=TRUE|Dir=In|Protocol=6|App=C:\Tools\agent.exe|Name=Agent|")==@"C:\Tools\agent.exe"&&TraceHunter.FirewallProgram("v2.31|Action=Allow|Name=x|")=="","firewall App field");
   var entries=TraceHunter.PathEntries(@"C:\Tools\bin;""C:\Program Files\Vendor\bin\"";;%SystemRoot%\system32");
   Assert(entries.Count==3&&(!Core.StubbornFiles.IsWindows||entries[0]==@"C:\Tools\bin"&&entries[1]==@"C:\Program Files\Vendor\bin"),"PATH entries split, unquoted, canonical: "+String.Join(" | ",entries));
   Assert(TraceHunter.PathEntries(null).Count==0&&TraceHunter.PathEntries("  ").Count==0,"empty PATH");
  }

  static void Matchers(){
   var exes=new List<string>{"Photoshop","AgentSvc"};
   Assert(TraceHunter.WerMatches("AppCrash_Photoshop.exe_1a2b3c4d5e6f_cab_0123",exes)&&TraceHunter.WerMatches("AppHang_agentsvc_ffff",exes)&&!TraceHunter.WerMatches("AppCrash_PhotoshopHelper.exe_x",exes)&&!TraceHunter.WerMatches("Kernel_0_0",exes)&&!TraceHunter.WerMatches("",exes),"WER folder matcher");
   Assert(TraceHunter.TraceFileMatches("PHOTOSHOP.EXE-9A8B7C6D.pf",exes)&&TraceHunter.TraceFileMatches("agentsvc.exe.4321.dmp",exes)&&!TraceHunter.TraceFileMatches("PHOTOSHOPX.EXE-1.pf",exes)&&!TraceHunter.TraceFileMatches("NTOSKRNL.EXE-1.pf",exes),"Prefetch / CrashDumps matcher");
   var identity=new AppEntry{Location=@"C:\Program Files\Vendor\Tool",KnownExecutables=new List<string>{@"C:\Users\x\AppData\Local\Vendor\Tool\tool.exe"}};
   Assert(TraceHunter.UnderApp(identity,@"C:\Program Files\Vendor\Tool\bin")&&TraceHunter.UnderApp(identity,@"C:\Program Files\Vendor\Tool")&&(!Core.StubbornFiles.IsWindows||TraceHunter.UnderApp(identity,@"C:\Users\x\AppData\Local\Vendor\Tool\plugins\a.dll")),"under install folder or exe folder");
   Assert(!TraceHunter.UnderApp(identity,@"C:\Program Files\Vendor\Toolkit")&&!TraceHunter.UnderApp(identity,@"C:\Program Files\Vendor")&&!TraceHunter.UnderApp(identity,@"\\server\share\Tool")&&!TraceHunter.UnderApp(identity,""),"boundaries: sibling, parent, UNC, empty");
  }

  static void AllowList(){
   foreach(string key in TraceHunter.ValueKeys)Advanced.ValidateValue(key);
   Advanced.ValidateValue(Advanced.Run);
   MustFail(()=>Advanced.ValidateValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion"),"arbitrary key refused");
   MustFail(()=>Advanced.ValidateValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage"),"FeatureUsage parent refused (only its counter subkeys allowed)");
   MustFail(()=>Advanced.ValidateValue(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy"),"firewall policy parent refused");
   MustFail(()=>Advanced.ValidateValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved"),"StartupApproved parent refused");
  }

  /// <summary>Extend runs on the live machine for a fixture app that never existed: bounded time, no exception, no cleanable item that lies outside the fixture identity.</summary>
  static void Smoke(){
   string id="TweekProTrace"+Guid.NewGuid().ToString("N");
   var app=new AppEntry{Name=id+" Studio 2026",Publisher=id+" Labs",Location=Path.Combine(Path.GetTempPath(),id),KnownExecutables=new List<string>{Path.Combine(Path.GetTempPath(),id,id+".exe")}};
   var identity=new AppEntry{Name=app.Name,Location=app.Location,KnownExecutables=app.KnownExecutables.ToList()};
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase){app.Name};var result=new ScanResult();var stages=new List<string>();
   var watch=System.Diagnostics.Stopwatch.StartNew();
   TraceHunter.Extend(app,identity,names,result,CancellationToken.None,(stage,current)=>stages.Add(stage));
   Assert(stages.Count>=1,"stages reported");
   Assert(result.Items.All(c=>c.ReviewOnly||c.Kind=="Review"||c.Path.IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0||(c.ValueName??"").IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0),"nothing cleanable found for an app that never existed: "+String.Join("; ",result.Items.Where(c=>!c.ReviewOnly).Select(c=>c.ToString()).Take(3)));
   Assert(!result.Items.Any(c=>c.Path.IndexOf(id,StringComparison.OrdinalIgnoreCase)<0&&(c.ValueName??"").IndexOf(id,StringComparison.OrdinalIgnoreCase)<0&&c.Kind!="Review"),"no false positives");
   var cancelled=new CancellationTokenSource();cancelled.Cancel();
   MustFail(()=>TraceHunter.Extend(app,identity,names,new ScanResult(),cancelled.Token,null),"cancellation honoured");
   Assert(watch.Elapsed.TotalSeconds<60,"bounded runtime: "+watch.Elapsed.TotalSeconds.ToString("0.0")+"s");
  }
 }
}
