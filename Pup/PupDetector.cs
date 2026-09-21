using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using TweekPro.Core;
using TweekPro.Store;

namespace TweekPro.Pup {
 /// <summary>Why a program is considered unwanted; drives the badge text and the plain-language explanation.</summary>
 public enum PupCategory { Toolbar, Adware, Optimizer, TrialSecurity, BundledUpdater, OemBloat, PromoStore }

 /// <summary>How strongly Tweek Pro recommends looking at the entry; High never triggers removal by itself.</summary>
 public enum PupSeverity { Low, Medium, High }

 /// <summary>One data-driven detection rule: name/publisher globs for desktop apps, package-name globs for Appx packages.</summary>
 [DataContract]
 public class PupRule {
  [DataMember(Name="id")] public string Id;
  [DataMember(Name="category")] public string CategoryName;
  [DataMember(Name="severity")] public string SeverityName;
  [DataMember(Name="names")] public List<string> Names=new List<string>();
  [DataMember(Name="publishers")] public List<string> Publishers=new List<string>();
  [DataMember(Name="packages")] public List<string> Packages=new List<string>();
  [DataMember(Name="reason")] public string Reason;
  public PupCategory Category;public PupSeverity Severity;
  public List<Regex> NameRegex=new List<Regex>(),PublisherRegex=new List<Regex>(),PackageRegex=new List<Regex>();
  public override string ToString(){return Id+" ("+Category+"/"+Severity+")";}
 }

 [DataContract]
 public class PupRuleSet {
  [DataMember(Name="version")] public int Version;
  [DataMember(Name="rules")] public List<PupRule> Rules=new List<PupRule>();
 }

 /// <summary>Result of classifying one program: which rule matched and the human explanation. Null verdict means clean.</summary>
 public class PupVerdict {
  public PupRule Rule;public string Target;public string MatchedOn;
  public PupCategory Category { get { return Rule.Category; } }
  public PupSeverity Severity { get { return Rule.Severity; } }
  public string Reason { get { return L.T(Rule.Reason); } }
  public string Badge { get { return PupDetector.CategoryLabel(Category); } }
 }

 /// <summary>
 /// Code-level boundary for the detector: publishers and products that are never flagged, whatever the rule file says.
 /// The detector is read-only, so this list is about credibility (no false alarms on core vendors), not about protection.
 /// </summary>
 public static class PupSafety {
  /// <summary>
  /// Publisher prefixes that rule globs may never flag for desktop apps (case-insensitive prefix match). OEMs (HP, Dell, Lenovo…),
  /// Apple, Oracle and Adobe are deliberately absent: their preloads and legacy runtimes are exactly what the OEM/bundled rules target.
  /// </summary>
  public static readonly string[] TrustedPublishers={"Microsoft Corporation","Microsoft","Google LLC","Google Inc","Mozilla","NVIDIA Corporation","Intel Corporation","Intel(R) Corporation","Advanced Micro Devices","AMD","Realtek","Valve","JetBrains","Docker","Python Software Foundation","Node.js","GitHub","Brave Software","VideoLAN","7-Zip","Igor Pavlov","Notepad++","Don HO","The Git Development Community","Canonical","Zoom Video Communications","Telegram FZ-LLC","Discord Inc","Logitech","Razer","Corsair","SteelSeries"};
  /// <summary>Product names never flagged even when a glob is loose: exact match, or the name followed by a version/architecture token.</summary>
  public static readonly string[] TrustedNames={"Microsoft Edge","Microsoft Edge WebView2 Runtime","Google Chrome","Mozilla Firefox","Windows Terminal","PowerShell","Visual Studio Code","Microsoft Visual C++ Redistributable","Microsoft .NET Runtime","Windows SDK","OpenJDK","Adobe Acrobat","Adobe Acrobat Reader","Steam","Discord","Zoom","Telegram Desktop","7-Zip","VLC media player","Notepad++","Git","Docker Desktop","Python"};

  /// <summary>Trusted publisher check; rules that explicitly target a publisher glob still lose to this list.</summary>
  public static bool IsTrustedPublisher(string publisher){
   if(String.IsNullOrWhiteSpace(publisher))return false;
   string p=publisher.Trim();
   return TrustedPublishers.Any(t=>p.StartsWith(t,StringComparison.OrdinalIgnoreCase));
  }
  /// <summary>"7-Zip 23.01 (x64)" and "Python 3.12 (64-bit)" are trusted; "Steam Cleaner Pro" is not, because the remainder is a word rather than a version.</summary>
  public static bool IsTrustedName(string name){
   if(String.IsNullOrWhiteSpace(name))return false;
   string n=name.Trim();
   foreach(string t in TrustedNames){
    if(String.Equals(t,n,StringComparison.OrdinalIgnoreCase))return true;
    if(!n.StartsWith(t+" ",StringComparison.OrdinalIgnoreCase))continue;
    string rest=n.Substring(t.Length).TrimStart();
    if(rest.Length>0&&(Char.IsDigit(rest[0])||rest[0]=='('||rest.StartsWith("version",StringComparison.OrdinalIgnoreCase)||rest.StartsWith("x64",StringComparison.OrdinalIgnoreCase)||rest.StartsWith("x86",StringComparison.OrdinalIgnoreCase)||rest.StartsWith("(x64",StringComparison.OrdinalIgnoreCase)))return true;
   }
   return false;
  }
 }

 /// <summary>
 /// Read-only classification of installed desktop programs and Appx packages against the embedded PUP/bloatware rules.
 /// Never removes anything; the UI only shows a badge and links to the existing uninstall flows.
 /// </summary>
 public static class PupDetector {
  public const string ResourceName="TweekPro.Pup.pup-rules.json";
  static readonly Lazy<PupRuleSet> embedded=new Lazy<PupRuleSet>(LoadEmbedded);
  public static PupRuleSet Rules { get { return embedded.Value; } }

  /// <summary>Reads the rules compiled into the executable.</summary>
  public static PupRuleSet LoadEmbedded(){
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)){
    if(stream==null)throw new IOException("Thiếu tài nguyên quy tắc PUP trong tệp thực thi.");
    return Parse(stream);
   }
  }

  /// <summary>Deserializes and validates a rule set: ids unique, category/severity known, at least one pattern, a reason.</summary>
  public static PupRuleSet Parse(Stream json){
   var set=(PupRuleSet)new DataContractJsonSerializer(typeof(PupRuleSet)).ReadObject(json);
   if(set==null||set.Rules==null||set.Rules.Count==0)throw new IOException("Tệp quy tắc PUP trống.");
   var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var r in set.Rules){
    if(String.IsNullOrWhiteSpace(r.Id)||!ids.Add(r.Id))throw new IOException("Quy tắc PUP thiếu id hoặc trùng id.");
    if(!Enum.TryParse(r.CategoryName??"",true,out r.Category))throw new IOException("Quy tắc PUP "+r.Id+" có loại không hợp lệ: "+r.CategoryName);
    if(!Enum.TryParse(r.SeverityName??"",true,out r.Severity))throw new IOException("Quy tắc PUP "+r.Id+" có mức không hợp lệ: "+r.SeverityName);
    if(String.IsNullOrWhiteSpace(r.Reason))throw new IOException("Quy tắc PUP "+r.Id+" thiếu lý do.");
    r.Names=r.Names??new List<string>();r.Publishers=r.Publishers??new List<string>();r.Packages=r.Packages??new List<string>();
    if(r.Names.Count+r.Publishers.Count+r.Packages.Count==0)throw new IOException("Quy tắc PUP "+r.Id+" không có mẫu nào.");
    foreach(string g in r.Names.Concat(r.Publishers).Concat(r.Packages))if(g.Trim().Length<3||g.Trim().Trim('*','?').Length<2)throw new IOException("Mẫu PUP quá rộng trong "+r.Id+": "+g);
    r.NameRegex=r.Names.Select(Glob).ToList();r.PublisherRegex=r.Publishers.Select(Glob).ToList();r.PackageRegex=r.Packages.Select(Glob).ToList();
   }
   return set;
  }

  static Regex Glob(string pattern){return Cleaner.JunkRules.GlobToRegex(pattern.Trim());}

  /// <summary>Classifies one desktop program; trusted publishers/names win over every rule. Null when clean.</summary>
  public static PupVerdict Classify(AppEntry a){return Classify(a,Rules);}
  public static PupVerdict Classify(AppEntry a,PupRuleSet set){
   if(a==null||String.IsNullOrWhiteSpace(a.Name))return null;
   if(PupSafety.IsTrustedName(a.Name)||PupSafety.IsTrustedPublisher(a.Publisher))return null;
   string name=a.Name.Trim(),publisher=(a.Publisher??"").Trim();
   PupVerdict best=null;
   foreach(var r in set.Rules){
    string on=null;
    if(r.NameRegex.Any(x=>x.IsMatch(name)))on="name";
    else if(publisher!=""&&r.PublisherRegex.Any(x=>x.IsMatch(publisher)))on="publisher";
    if(on==null)continue;
    var v=new PupVerdict{Rule=r,Target=name,MatchedOn=on};
    if(best==null||v.Severity>best.Severity)best=v;
   }
   return best;
  }

  /// <summary>Classifies one Appx package by package name; protected and caution packages are never flagged. Null when clean.</summary>
  public static PupVerdict Classify(WindowsApp a){return Classify(a,Rules);}
  public static PupVerdict Classify(WindowsApp a,PupRuleSet set){
   if(a==null||String.IsNullOrWhiteSpace(a.Name))return null;
   if(WindowsApps.Classify(a)!=AppxStatus.Removable)return null;
   string name=a.Name.Trim();
   PupVerdict best=null;
   foreach(var r in set.Rules){
    if(!r.PackageRegex.Any(x=>x.IsMatch(name)))continue;
    var v=new PupVerdict{Rule=r,Target=a.DisplayName,MatchedOn="package"};
    if(best==null||v.Severity>best.Severity)best=v;
   }
   return best;
  }

  /// <summary>Scans both inventories; either list may be null when that source has not been loaded.</summary>
  public static List<PupVerdict> Scan(IEnumerable<AppEntry> desktop,IEnumerable<WindowsApp> packages){
   var list=new List<PupVerdict>();
   if(desktop!=null)foreach(var a in desktop){var v=Classify(a);if(v!=null)list.Add(v);}
   if(packages!=null)foreach(var p in packages){var v=Classify(p);if(v!=null)list.Add(v);}
   return list.OrderByDescending(v=>v.Severity).ThenBy(v=>v.Target,StringComparer.CurrentCultureIgnoreCase).ToList();
  }

  public static string CategoryLabel(PupCategory c){
   switch(c){
    case PupCategory.Toolbar:return L.T("Thanh công cụ");
    case PupCategory.Adware:return L.T("Quảng cáo / dọa lỗi");
    case PupCategory.Optimizer:return L.T("Tối ưu tiếp thị");
    case PupCategory.TrialSecurity:return L.T("Bảo mật cài sẵn");
    case PupCategory.BundledUpdater:return L.T("Đi kèm / runtime cũ");
    case PupCategory.OemBloat:return L.T("Cài sẵn theo máy");
    default:return L.T("Store cài sẵn");
   }
  }
  public static string SeverityLabel(PupSeverity s){return s==PupSeverity.High?L.T("Nên gỡ"):s==PupSeverity.Medium?L.T("Cân nhắc gỡ"):L.T("Không cần thiết");}

  /// <summary>Plain-text summary for the log or clipboard.</summary>
  public static string Summary(IList<PupVerdict> verdicts){
   if(verdicts==null||verdicts.Count==0)return L.T("Không phát hiện phần mềm không mong muốn.");
   return L.F("{0} mục nghi không mong muốn: {1} nên gỡ, {2} cân nhắc, {3} không cần thiết.",verdicts.Count,verdicts.Count(v=>v.Severity==PupSeverity.High),verdicts.Count(v=>v.Severity==PupSeverity.Medium),verdicts.Count(v=>v.Severity==PupSeverity.Low));
  }
 }
}
