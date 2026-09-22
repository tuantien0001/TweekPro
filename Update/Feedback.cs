using System;
using System.Collections.Generic;
using System.Text;

namespace TweekPro.Update {
 /// <summary>Builds GitHub Issues links (bug report, feature request, feedback) prefilled with version and Windows build so users only describe the problem. Pure logic, no UI.</summary>
 public static class Feedback {
  public const string IssuesPage="https://github.com/"+UpdateCheck.Owner+"/"+UpdateCheck.Repo+"/issues";
  public const string NewIssue=IssuesPage+"/new";
  public const string BugTemplate="bug_report.yml",FeatureTemplate="feature_request.yml",FeedbackTemplate="feedback.yml";
  /// <summary>Browsers and GitHub reject very long URLs; prefilled text is trimmed so the whole link stays under this.</summary>
  public const int MaxUrlLength=7000;

  /// <summary>Prefilled "Báo lỗi" form: version, Windows label and an optional context line (which tab was open, last error) in the description.</summary>
  public static string BugUrl(string version,string windows,string context){
   var fields=new List<KeyValuePair<string,string>>{Pair("template",BugTemplate),Pair("version",version),Pair("windows",windows)};
   if(!String.IsNullOrWhiteSpace(context))fields.Add(Pair("what",context.Trim()));
   return Build(fields);
  }

  public static string FeatureUrl(string version){return Build(new[]{Pair("template",FeatureTemplate),Pair("version",version)});}

  public static string FeedbackUrl(string version,string windows){return Build(new[]{Pair("template",FeedbackTemplate),Pair("version",version),Pair("windows",windows)});}

  /// <summary>Human-readable Windows edition + display version + build from the registry; falls back to Environment.OSVersion on other platforms or when the key is unreadable.</summary>
  public static string WindowsLabel(){
   if(Environment.OSVersion.Platform==PlatformID.Win32NT){
    try{
     using(var k=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")){
      if(k!=null){
       string product=Convert.ToString(k.GetValue("ProductName","")),display=Convert.ToString(k.GetValue("DisplayVersion","")),build=Convert.ToString(k.GetValue("CurrentBuild",""));
       int buildNumber;if(int.TryParse(build,out buildNumber)&&buildNumber>=22000&&product.StartsWith("Windows 10",StringComparison.OrdinalIgnoreCase))product="Windows 11"+product.Substring(10);
       if(!String.IsNullOrWhiteSpace(product))return Compose(product,display,build);
      }
     }
    }catch(Exception){}
   }
   return Environment.OSVersion.VersionString+(Environment.Is64BitOperatingSystem?" (64-bit)":" (32-bit)");
  }

  /// <summary>"Windows 11 Pro 24H2 (build 26100)" style label; empty parts are skipped.</summary>
  public static string Compose(string product,string display,string build){
   var sb=new StringBuilder(product.Trim());
   if(!String.IsNullOrWhiteSpace(display))sb.Append(' ').Append(display.Trim());
   if(!String.IsNullOrWhiteSpace(build))sb.Append(" (build ").Append(build.Trim()).Append(')');
   return sb.ToString();
  }

  static KeyValuePair<string,string> Pair(string k,string v){return new KeyValuePair<string,string>(k,v??"");}

  /// <summary>Query string with RFC 3986 escaping (GitHub decodes %20 and %0A into spaces and new lines in form fields); long values are cut to respect MaxUrlLength.</summary>
  public static string Build(IEnumerable<KeyValuePair<string,string>> fields){
   var sb=new StringBuilder(NewIssue);char sep='?';
   foreach(var f in fields){
    if(String.IsNullOrEmpty(f.Value))continue;
    string value=f.Value;
    int room=MaxUrlLength-sb.Length-f.Key.Length-2;
    string encoded=Uri.EscapeDataString(value);
    while(encoded.Length>room&&value.Length>0){value=value.Substring(0,Math.Max(0,value.Length-Math.Max(1,(encoded.Length-room)/3+1)));encoded=Uri.EscapeDataString(value)+(value.Length>0?"%E2%80%A6":"");}
    if(value.Length==0&&f.Key!="template")continue;
    sb.Append(sep).Append(f.Key).Append('=').Append(encoded);sep='&';
   }
   return sb.ToString();
  }
 }
}
