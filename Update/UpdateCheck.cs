using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace TweekPro.Update {
 /// <summary>Result of asking GitHub for a newer release or version tag than the running build.</summary>
 public class UpdateInfo {
  public string Current="",Latest="",PageUrl="",InstallerUrl="",Notes="",Error="";
  public bool UpdateAvailable;
  /// <summary>True when the answer came from /tags because no GitHub Release exists yet.</summary>
  public bool FromTagOnly;
 }

 [DataContract] public class GhRelease {
  [DataMember(Name="tag_name")] public string TagName;
  [DataMember(Name="name")] public string Name;
  [DataMember(Name="html_url")] public string HtmlUrl;
  [DataMember(Name="body")] public string Body;
  [DataMember(Name="assets")] public GhAsset[] Assets;
 }
 [DataContract] public class GhAsset {
  [DataMember(Name="name")] public string Name;
  [DataMember(Name="browser_download_url")] public string BrowserDownloadUrl;
 }
 [DataContract] public class GhTag {
  [DataMember(Name="name")] public string Name;
 }

 /// <summary>
 /// Minimal update probe for a compiled WinForms exe. A push alone does not patch the running binary —
 /// the user must download a new build and replace/restart. Primary source: GitHub Releases/latest (publish a
 /// Release when tagging v*). Fallback: newest v* tag on the repo (no installer asset until a Release exists).
 /// </summary>
 public static class UpdateCheck {
  public const string Owner="tuantien0001",Repo="TweekPro";
  public static string ApiUrl="https://api.github.com/repos/"+Owner+"/"+Repo+"/releases/latest";
  public static string TagsUrl="https://api.github.com/repos/"+Owner+"/"+Repo+"/tags?per_page=30";
  public static string ReleasesPage="https://github.com/"+Owner+"/"+Repo+"/releases";
  /// <summary>Optional override for tests; when set, Check skips the network and parses this JSON as a Release.</summary>
  public static string TestJson;
  /// <summary>Optional override for tests; used when TestJson is null and tags fallback is exercised, or when releases return 404.</summary>
  public static string TestTagsJson;
  /// <summary>When true (default in tests via TestTagsJson path), treat missing releases as tags fallback without network.</summary>
  public static bool TestForceTagsFallback;

  public static UpdateInfo Check(string currentVersion){
   var info=new UpdateInfo{Current=Normalize(currentVersion)};
   try{
    if(TestForceTagsFallback)return FromTags(info,TestTagsJson??"[]");
    string json=TestJson;
    if(json==null){
     try{json=Fetch(ApiUrl);}
     catch(WebException e){
      var resp=e.Response as HttpWebResponse;
      if(resp!=null&&resp.StatusCode==HttpStatusCode.NotFound)return FromTags(info,TestTagsJson??Fetch(TagsUrl));
      info.Error=Describe(e);return info;
     }
    }
    if(String.IsNullOrWhiteSpace(json)){info.Error="Phản hồi GitHub trống.";return info;}
    var release=Parse(json);if(release==null||String.IsNullOrWhiteSpace(release.TagName)){info.Error="Không đọc được bản phát hành.";return info;}
    FillFromRelease(info,release);
   }catch(WebException e){info.Error=Describe(e);}
   catch(Exception e){info.Error=e.Message;}
   return info;
  }

  static UpdateInfo FromTags(UpdateInfo info,string json){
   info.FromTagOnly=true;
   if(String.IsNullOrWhiteSpace(json)){info.Error="Phản hồi GitHub trống.";return info;}
   var tags=ParseTags(json);if(tags==null||tags.Length==0){info.Error="Chưa có GitHub Release (cần gắn tag v*). Push nhánh chỉ tạo artifact Actions, không phải bản phát hành.";info.PageUrl=ReleasesPage;return info;}
   string best=null;
   foreach(var t in tags){
    if(t==null||String.IsNullOrWhiteSpace(t.Name))continue;
    string n=Normalize(t.Name);if(!LooksLikeVersion(n))continue;
    if(best==null||IsNewer(n,best))best=n;
   }
   if(best==null){info.Error="Chưa có GitHub Release (cần gắn tag v*). Push nhánh chỉ tạo artifact Actions, không phải bản phát hành.";info.PageUrl=ReleasesPage;return info;}
   info.Latest=best;info.PageUrl=ReleasesPage+"/tag/v"+best;info.InstallerUrl="";
   info.Notes="Chỉ có tag phiên bản trên GitHub, chưa có Release kèm bộ cài. Mở trang Releases để tải artifact hoặc đợi bản phát hành chính thức.";
   info.UpdateAvailable=IsNewer(info.Latest,info.Current);
   return info;
  }

  static void FillFromRelease(UpdateInfo info,GhRelease release){
   info.Latest=Normalize(release.TagName);info.PageUrl=String.IsNullOrWhiteSpace(release.HtmlUrl)?ReleasesPage:release.HtmlUrl;
   info.Notes=release.Body??"";info.InstallerUrl=PickAsset(release.Assets);
   info.UpdateAvailable=IsNewer(info.Latest,info.Current);
  }

  /// <summary>Startup banner rule: a real, newer release that the user has not dismissed with "skip this version".</summary>
  public static bool ShouldNotify(UpdateInfo info,string skipVersion){
   if(info==null||!info.UpdateAvailable||!String.IsNullOrEmpty(info.Error)||String.IsNullOrEmpty(info.Latest))return false;
   return !String.Equals(Normalize(skipVersion),info.Latest,StringComparison.OrdinalIgnoreCase);
  }

  public static string Normalize(string tag){
   if(String.IsNullOrWhiteSpace(tag))return "0";
   string t=tag.Trim();if(t.StartsWith("v",StringComparison.OrdinalIgnoreCase))t=t.Substring(1);
   int cut=t.IndexOfAny(new[]{'-','+'});if(cut>0)t=t.Substring(0,cut);
   return t;
  }

  public static bool IsNewer(string latest,string current){
   Version a,b;if(Version.TryParse(Pad(latest),out a)&&Version.TryParse(Pad(current),out b))return a>b;
   return String.Compare(latest,current,StringComparison.OrdinalIgnoreCase)>0;
  }

  static bool LooksLikeVersion(string n){
   if(String.IsNullOrWhiteSpace(n))return false;
   Version parsed;return char.IsDigit(n[0])&&Version.TryParse(Pad(n),out parsed);
  }

  static string Pad(string v){
   var parts=(v??"0").Split('.');if(parts.Length>=2)return v;
   return v+".0";
  }

  public static string PickAsset(GhAsset[] assets){
   if(assets==null||assets.Length==0)return "";
   Func<string,bool> prefer=n=>n!=null&&(n.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||n.IndexOf("setup",StringComparison.OrdinalIgnoreCase)>=0||n.IndexOf("installer",StringComparison.OrdinalIgnoreCase)>=0);
   var hit=assets.FirstOrDefault(a=>prefer(a.Name));if(hit!=null)return hit.BrowserDownloadUrl??"";
   hit=assets.FirstOrDefault(a=>a.Name!=null&&a.Name.EndsWith(".zip",StringComparison.OrdinalIgnoreCase));
   return hit!=null?(hit.BrowserDownloadUrl??""):(assets[0].BrowserDownloadUrl??"");
  }

  public static GhRelease Parse(string json){
   using(var ms=new MemoryStream(Encoding.UTF8.GetBytes(json))){
    var ser=new DataContractJsonSerializer(typeof(GhRelease));
    return ser.ReadObject(ms) as GhRelease;
   }
  }

  public static GhTag[] ParseTags(string json){
   using(var ms=new MemoryStream(Encoding.UTF8.GetBytes(json))){
    var ser=new DataContractJsonSerializer(typeof(GhTag[]));
    return ser.ReadObject(ms) as GhTag[];
   }
  }

  static string Fetch(string url){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var req=(HttpWebRequest)WebRequest.Create(url);
   req.UserAgent="TweekPro/"+Normalize(MainForm.Version);req.Accept="application/vnd.github+json";req.Timeout=15000;
   using(var resp=(HttpWebResponse)req.GetResponse())
   using(var stream=resp.GetResponseStream())
   using(var reader=new StreamReader(stream??Stream.Null,Encoding.UTF8))return reader.ReadToEnd();
  }

  static string Describe(WebException e){
   var resp=e.Response as HttpWebResponse;
   if(resp!=null&&resp.StatusCode==HttpStatusCode.NotFound)return "Chưa có GitHub Release (cần gắn tag v*). Push nhánh chỉ tạo artifact Actions, không phải bản phát hành.";
   return e.Message;
  }
 }
}
