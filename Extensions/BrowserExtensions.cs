using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace TweekPro.Extensions {
 /// <summary>One browser profile folder that may contain extensions.</summary>
 public class BrowserProfile { public string Browser, Folder, Name; public bool Firefox; }

 /// <summary>One installed extension, read-only.</summary>
 public class BrowserExtension {
  public string Browser, Profile, ProfileFolder, Id, Name, Version, Description, Folder, Source, Warning=""; public bool Enabled=true, Policy, FromStore=true; public int Location;
  public long Bytes=-1; public DateTime Installed=DateTime.MinValue; public List<string> Permissions=new List<string>();
  public string Key { get { return Browser+"|"+Profile+"|"+Id; } }
 }

 /// <summary>Read-only inventory of Chromium-family and Firefox extensions across every profile, with provenance and permission warnings.</summary>
 public static class BrowserExtensions {
  /// <summary>Test hook: force-installed extension ids per browser; null reads the Windows policy keys.</summary>
  public static Func<string,HashSet<string>> ForcelistReader;
  public static readonly string[] BroadPermissions={"<all_urls>","*://*/*","http://*/*","https://*/*","tabs","webRequest","webRequestBlocking","history","cookies","nativeMessaging","debugger","proxy","privacy","management","downloads","clipboardRead","declarativeNetRequest"};
  static readonly Dictionary<string,string> ChromiumRoots=new Dictionary<string,string>{{"Chrome",@"%LOCALAPPDATA%\Google\Chrome\User Data"},{"Edge",@"%LOCALAPPDATA%\Microsoft\Edge\User Data"},{"Brave",@"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data"},{"Vivaldi",@"%LOCALAPPDATA%\Vivaldi\User Data"},{"Chromium",@"%LOCALAPPDATA%\Chromium\User Data"},{"Opera",@"%APPDATA%\Opera Software\Opera Stable"}};
  static readonly Dictionary<string,string> PolicyKeys=new Dictionary<string,string>{{"Chrome",@"SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist"},{"Edge",@"SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallForcelist"},{"Brave",@"SOFTWARE\Policies\BraveSoftware\Brave\ExtensionInstallForcelist"},{"Chromium",@"SOFTWARE\Policies\Chromium\ExtensionInstallForcelist"}};

  /// <summary>Every existing profile folder of every supported browser for the current user.</summary>
  public static List<BrowserProfile> Profiles(){
   var list=new List<BrowserProfile>();
   foreach(var pair in ChromiumRoots){
    string root=Environment.ExpandEnvironmentVariables(pair.Value);if(root.IndexOf('%')>=0||!Directory.Exists(root))continue;
    if(pair.Key=="Opera"){list.Add(new BrowserProfile{Browser=pair.Key,Folder=root,Name="Default"});continue;}
    try{foreach(string dir in Directory.EnumerateDirectories(root)){string leaf=Path.GetFileName(dir);if(leaf=="Default"||leaf.StartsWith("Profile ",StringComparison.OrdinalIgnoreCase))if(Directory.Exists(Path.Combine(dir,"Extensions"))||File.Exists(Path.Combine(dir,"Preferences")))list.Add(new BrowserProfile{Browser=pair.Key,Folder=dir,Name=ProfileName(dir,leaf)});}}catch(IOException){}catch(UnauthorizedAccessException){}
   }
   string firefox=Environment.ExpandEnvironmentVariables(@"%APPDATA%\Mozilla\Firefox\Profiles");
   if(firefox.IndexOf('%')<0&&Directory.Exists(firefox))try{foreach(string dir in Directory.EnumerateDirectories(firefox))if(File.Exists(Path.Combine(dir,"extensions.json")))list.Add(new BrowserProfile{Browser="Firefox",Folder=dir,Name=Path.GetFileName(dir),Firefox=true});}catch(IOException){}catch(UnauthorizedAccessException){}
   return list;
  }

  static string ProfileName(string dir,string leaf){
   try{using(var doc=ReadJson(Path.Combine(dir,"Preferences"))){JsonElement p,n;if(doc!=null&&doc.RootElement.TryGetProperty("profile",out p)&&p.TryGetProperty("name",out n)&&n.ValueKind==JsonValueKind.String)return n.GetString()+" ("+leaf+")";}}catch(Exception){}
   return leaf;
  }

  /// <summary>Opens a JSON file shared with a running browser; null when missing or unparsable.</summary>
  public static JsonDocument ReadJson(string path){
   try{if(!File.Exists(path))return null;using(var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var r=new StreamReader(s,Encoding.UTF8,true))return JsonDocument.Parse(r.ReadToEnd(),new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip});}
   catch(Exception){return null;}
  }

  /// <summary>Lists extensions of every profile; errors per profile are collected into notes instead of thrown.</summary>
  public static List<BrowserExtension> Scan(IEnumerable<BrowserProfile> profiles,List<string> notes){
   var list=new List<BrowserExtension>();
   foreach(var p in profiles){
    try{if(p.Firefox)list.AddRange(ScanFirefox(p));else list.AddRange(ScanChromium(p,Forcelist(p.Browser)));}
    catch(Exception e){if(notes!=null)notes.Add(p.Browser+" / "+p.Name+": "+e.Message);}
   }
   return list.GroupBy(e=>e.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(e=>e.Browser).ThenBy(e=>e.Profile).ThenBy(e=>e.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
  }

  /// <summary>Ids forced by ExtensionInstallForcelist policy (HKLM and HKCU) for the browser.</summary>
  public static HashSet<string> Forcelist(string browser){
   if(ForcelistReader!=null)return ForcelistReader(browser)??new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);string key;
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT||!PolicyKeys.TryGetValue(browser,out key))return ids;
   foreach(string hive in new[]{"HKLM","HKCU"})foreach(string view in Advanced.Views)try{using(var root=Engine.Base(hive,view))using(var k=root.OpenSubKey(key)){if(k==null)continue;foreach(string name in k.GetValueNames()){string v=Convert.ToString(k.GetValue(name));int semi=v.IndexOf(';');ids.Add(semi>0?v.Substring(0,semi):v);}}}catch(Exception){}
   return ids;
  }

  /// <summary>Walks Extensions\&lt;id&gt;\&lt;version&gt;\manifest.json and enriches from Preferences / Secure Preferences (state, provenance, install time).</summary>
  public static List<BrowserExtension> ScanChromium(BrowserProfile p,HashSet<string> forced){
   var list=new List<BrowserExtension>();var settings=new Dictionary<string,JsonElement>(StringComparer.OrdinalIgnoreCase);var docs=new List<JsonDocument>();
   try{
    foreach(string file in new[]{"Secure Preferences","Preferences"}){var doc=ReadJson(Path.Combine(p.Folder,file));if(doc==null)continue;docs.Add(doc);JsonElement ext,set;if(doc.RootElement.TryGetProperty("extensions",out ext)&&ext.TryGetProperty("settings",out set)&&set.ValueKind==JsonValueKind.Object)foreach(var prop in set.EnumerateObject())if(!settings.ContainsKey(prop.Name))settings[prop.Name]=prop.Value;}
    string extRoot=Path.Combine(p.Folder,"Extensions");var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if(Directory.Exists(extRoot))foreach(string idDir in Directory.EnumerateDirectories(extRoot)){
     string id=Path.GetFileName(idDir);if(id.Length!=32||!id.All(c=>c>='a'&&c<='p'))continue;
     string versionDir=null;try{versionDir=Directory.EnumerateDirectories(idDir).Where(d=>File.Exists(Path.Combine(d,"manifest.json"))).OrderByDescending(d=>Path.GetFileName(d),StringComparer.OrdinalIgnoreCase).FirstOrDefault();}catch(IOException){}
     if(versionDir==null)continue;
     var e=FromManifest(Path.Combine(versionDir,"manifest.json"));if(e==null)continue;
     e.Browser=p.Browser;e.Profile=p.Name;e.ProfileFolder=p.Folder;e.Id=id;e.Folder=versionDir;seen.Add(id);
     JsonElement s;if(settings.TryGetValue(id,out s))Enrich(e,s);
     Finish(e,forced);list.Add(e);
    }
    // Unpacked or component extensions live outside the Extensions folder but still appear in settings with a path.
    foreach(var pair in settings){
     if(seen.Contains(pair.Key)||pair.Key.Length!=32)continue;JsonElement path,manifest;
     if(!pair.Value.TryGetProperty("manifest",out manifest)||manifest.ValueKind!=JsonValueKind.Object)continue;
     var e=FromManifestElement(manifest,null);if(e==null)continue;
     e.Browser=p.Browser;e.Profile=p.Name;e.ProfileFolder=p.Folder;e.Id=pair.Key;e.Folder=pair.Value.TryGetProperty("path",out path)&&path.ValueKind==JsonValueKind.String?path.GetString():"";
     if(e.Folder!=""&&!Path.IsPathRooted(e.Folder))e.Folder=Path.Combine(p.Folder,"Extensions",e.Folder);
     Enrich(e,pair.Value);if(e.Location==5||e.Location==10)continue;
     Finish(e,forced);list.Add(e);
    }
   }finally{foreach(var d in docs)d.Dispose();}
   return list;
  }

  static void Enrich(BrowserExtension e,JsonElement s){
   JsonElement v;
   if(s.TryGetProperty("state",out v)&&v.ValueKind==JsonValueKind.Number)e.Enabled=v.GetInt32()==1;
   if(s.TryGetProperty("disable_reasons",out v)&&v.ValueKind==JsonValueKind.Number&&v.GetInt32()!=0)e.Enabled=false;
   if(s.TryGetProperty("from_webstore",out v)&&(v.ValueKind==JsonValueKind.True||v.ValueKind==JsonValueKind.False))e.FromStore=v.GetBoolean();
   if(s.TryGetProperty("location",out v)&&v.ValueKind==JsonValueKind.Number)e.Location=v.GetInt32();
   if(s.TryGetProperty("install_time",out v)&&v.ValueKind==JsonValueKind.String){long micro;if(long.TryParse(v.GetString(),out micro)&&micro>0)e.Installed=ChromeTime(micro);}
   if(e.Permissions.Count==0&&s.TryGetProperty("granted_permissions",out v)&&v.ValueKind==JsonValueKind.Object){JsonElement api,hosts;if(v.TryGetProperty("api",out api)&&api.ValueKind==JsonValueKind.Array)e.Permissions.AddRange(api.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()));if(v.TryGetProperty("explicit_host",out hosts)&&hosts.ValueKind==JsonValueKind.Array)e.Permissions.AddRange(hosts.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()));}
  }

  /// <summary>Chromium stores times as microseconds since 1601-01-01 UTC.</summary>
  public static DateTime ChromeTime(long micro){try{return new DateTime(1601,1,1,0,0,0,DateTimeKind.Utc).AddTicks(micro*10).ToLocalTime();}catch(Exception){return DateTime.MinValue;}}

  static void Finish(BrowserExtension e,HashSet<string> forced){
   e.Policy=forced!=null&&forced.Contains(e.Id)||e.Location==7||e.Location==9;
   e.Source=e.Policy?"Chính sách (ép cài)":e.Location==4?"Giải nén (developer)":e.Location==2||e.Location==3||e.Location==6?"Cài ngoài (registry/pref)":e.FromStore?"Cửa hàng":"Không rõ";
   var warnings=new List<string>();
   if(e.Policy)warnings.Add("Ép cài bằng chính sách — trên máy cá nhân thường là phần mềm không mong muốn");
   else if(!e.FromStore&&e.Location!=4)warnings.Add("Không cài từ cửa hàng chính thức");
   var broad=e.Permissions.Where(x=>BroadPermissions.Contains(x,StringComparer.OrdinalIgnoreCase)).Distinct().ToList();
   if(broad.Count>0)warnings.Add("Quyền rộng: "+String.Join(", ",broad.Take(4)));
   e.Warning=String.Join("; ",warnings);
  }

  /// <summary>Reads name/version/description/permissions from a manifest, resolving __MSG_key__ through _locales.</summary>
  public static BrowserExtension FromManifest(string manifestPath){
   using(var doc=ReadJson(manifestPath)){if(doc==null)return null;return FromManifestElement(doc.RootElement,Path.GetDirectoryName(manifestPath));}
  }

  static BrowserExtension FromManifestElement(JsonElement m,string folder){
   if(m.ValueKind!=JsonValueKind.Object)return null;
   var e=new BrowserExtension();JsonElement v;
   string locale=m.TryGetProperty("default_locale",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
   e.Name=Localize(m.TryGetProperty("name",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():"",folder,locale);
   e.Description=Localize(m.TryGetProperty("description",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():"",folder,locale);
   e.Version=m.TryGetProperty("version",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
   foreach(string key in new[]{"permissions","host_permissions"})if(m.TryGetProperty(key,out v)&&v.ValueKind==JsonValueKind.Array)e.Permissions.AddRange(v.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()));
   if(String.IsNullOrWhiteSpace(e.Name))e.Name="(không tên)";
   return e;
  }

  /// <summary>Resolves "__MSG_key__" placeholders against _locales/&lt;locale&gt;/messages.json (falling back to en / en_US); other text is returned unchanged.</summary>
  public static string Localize(string text,string folder,string defaultLocale){
   if(String.IsNullOrEmpty(text)||!text.StartsWith("__MSG_")||!text.EndsWith("__")||folder==null)return text??"";
   string key=text.Substring(6,text.Length-8);
   foreach(string locale in new[]{defaultLocale,"en","en_US","en_GB"}.Where(l=>!String.IsNullOrEmpty(l)).Distinct()){
    string file=Path.Combine(folder,"_locales",locale,"messages.json");
    using(var doc=ReadJson(file)){
     if(doc==null)continue;
     foreach(var prop in doc.RootElement.EnumerateObject())if(String.Equals(prop.Name,key,StringComparison.OrdinalIgnoreCase)){JsonElement msg;if(prop.Value.TryGetProperty("message",out msg)&&msg.ValueKind==JsonValueKind.String)return msg.GetString();}
    }
   }
   return text;
  }

  /// <summary>Firefox: extensions.json → addons of type extension (themes and dictionaries are labelled, built-ins skipped).</summary>
  public static List<BrowserExtension> ScanFirefox(BrowserProfile p){
   var list=new List<BrowserExtension>();
   using(var doc=ReadJson(Path.Combine(p.Folder,"extensions.json"))){
    if(doc==null)return list;JsonElement addons;if(!doc.RootElement.TryGetProperty("addons",out addons)||addons.ValueKind!=JsonValueKind.Array)return list;
    foreach(var a in addons.EnumerateArray()){
     JsonElement v;string type=a.TryGetProperty("type",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
     string location=a.TryGetProperty("location",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
     if(location.StartsWith("app-builtin")||location=="app-system-defaults"||location=="app-system-addons")continue;
     if(type!="extension"&&type!="theme"&&type!="dictionary"&&type!="locale")continue;
     var e=new BrowserExtension{Browser=p.Browser,Profile=p.Name,ProfileFolder=p.Folder};
     e.Id=a.TryGetProperty("id",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
     JsonElement dl;e.Name=a.TryGetProperty("defaultLocale",out dl)&&dl.TryGetProperty("name",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():e.Id;
     e.Description=dl.ValueKind==JsonValueKind.Object&&dl.TryGetProperty("description",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
     e.Version=a.TryGetProperty("version",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
     e.Folder=a.TryGetProperty("path",out v)&&v.ValueKind==JsonValueKind.String?v.GetString():"";
     bool active=a.TryGetProperty("active",out v)&&v.ValueKind==JsonValueKind.True;bool userDisabled=a.TryGetProperty("userDisabled",out v)&&v.ValueKind==JsonValueKind.True;
     e.Enabled=active&&!userDisabled;
     if(a.TryGetProperty("installDate",out v)&&v.ValueKind==JsonValueKind.Number){long ms;if(v.TryGetInt64(out ms)&&ms>0)e.Installed=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddMilliseconds(ms).ToLocalTime();}
     JsonElement up;e.Permissions=a.TryGetProperty("userPermissions",out up)&&up.ValueKind==JsonValueKind.Object?new[]{"permissions","origins"}.SelectMany(k=>{JsonElement arr;return up.TryGetProperty(k,out arr)&&arr.ValueKind==JsonValueKind.Array?arr.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()).ToList():new List<string>();}).ToList():new List<string>();
     bool system=location=="app-global"||location=="winreg-app-global"||location=="winreg-app-user";
     e.FromStore=!system;e.Policy=false;
     e.Source=type!="extension"?(type=="theme"?"Giao diện":type=="dictionary"?"Từ điển":"Gói ngôn ngữ"):system?"Cài ngoài (hệ thống/registry)":"Hồ sơ người dùng";
     var warnings=new List<string>();if(system)warnings.Add("Cài từ ngoài Firefox (registry hoặc thư mục hệ thống)");
     var broad=e.Permissions.Where(x=>BroadPermissions.Contains(x,StringComparer.OrdinalIgnoreCase)).Distinct().ToList();if(broad.Count>0)warnings.Add("Quyền rộng: "+String.Join(", ",broad.Take(4)));
     e.Warning=String.Join("; ",warnings);
     list.Add(e);
    }
   }
   return list;
  }

  /// <summary>Command line that opens the browser's own extension manager; null when the browser executable cannot be located.</summary>
  public static System.Diagnostics.ProcessStartInfo ManagerPage(string browser){
   string url=browser=="Firefox"?"about:addons":browser=="Edge"?"edge://extensions":browser=="Brave"?"brave://extensions":browser=="Vivaldi"?"vivaldi://extensions":browser=="Opera"?"opera://extensions":"chrome://extensions";
   string exe=BrowserExecutable(browser);if(exe==null)return null;
   return new System.Diagnostics.ProcessStartInfo(exe,url){UseShellExecute=true};
  }

  static string BrowserExecutable(string browser){
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return null;
   string appName=browser=="Chrome"?"chrome.exe":browser=="Edge"?"msedge.exe":browser=="Brave"?"brave.exe":browser=="Vivaldi"?"vivaldi.exe":browser=="Opera"?"opera.exe":browser=="Firefox"?"firefox.exe":"chromium.exe";
   foreach(string hive in new[]{"HKCU","HKLM"})foreach(string view in Advanced.Views)try{using(var root=Engine.Base(hive,view))using(var k=root.OpenSubKey(Advanced.AppPaths+"\\"+appName)){if(k==null)continue;string path=Engine.ExpandTrim(Engine.Read(k,""));if(File.Exists(path))return path;}}catch(Exception){}
   return null;
  }
 }
}
