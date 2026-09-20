using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace TweekPro.Cleaner {
 /// <summary>One data-driven junk rule: where to look, which file names qualify, and when the rule must stay locked.</summary>
 [DataContract]
 public class JunkRule {
  [DataMember(Name="id")] public string Id;
  [DataMember(Name="group")] public string Group;
  [DataMember(Name="name")] public string Name;
  [DataMember(Name="description")] public string Description;
  [DataMember(Name="paths")] public List<string> Paths=new List<string>();
  [DataMember(Name="patterns")] public List<string> Patterns=new List<string>();
  [DataMember(Name="recurse")] public bool Recurse=true;
  [DataMember(Name="minAgeHours")] public int MinAgeHours=24;
  [DataMember(Name="requiresAdmin")] public bool RequiresAdmin;
  [DataMember(Name="blockedProcesses")] public List<string> BlockedProcesses=new List<string>();
  [DataMember(Name="defaultChecked")] public bool DefaultChecked=true;
  public override string ToString(){return Id+": "+Name;}
 }

 [DataContract]
 public class JunkRuleSet {
  [DataMember(Name="version")] public int Version;
  [DataMember(Name="rules")] public List<JunkRule> Rules=new List<JunkRule>();
  /// <summary>Where the rules came from (embedded resource or override file path) for display in the UI.</summary>
  public string Source;
 }

 /// <summary>Loads junk rules from the embedded JSON or from an override file in the data directory and expands rule paths.</summary>
 public static class JunkRules {
  public const string ResourceName="TweekPro.Cleaner.junk-rules.json";

  /// <summary>Loads the override file when present and valid, otherwise the embedded default rules.</summary>
  public static JunkRuleSet Load(string overridePath){
   if(!String.IsNullOrEmpty(overridePath)&&File.Exists(overridePath)){
    try{using(var stream=File.OpenRead(overridePath)){var set=Parse(stream);set.Source=overridePath;return set;}}
    catch(Exception e){Core.Log.Warn("Không đọc được junk-rules.json tùy chỉnh, dùng quy tắc mặc định: "+e.Message);}
   }
   return LoadEmbedded();
  }

  /// <summary>Reads the rules compiled into the executable.</summary>
  public static JunkRuleSet LoadEmbedded(){
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)){
    if(stream==null)throw new IOException("Thiếu tài nguyên quy tắc dọn rác trong tệp thực thi.");
    var set=Parse(stream);set.Source="(mặc định, đóng gói trong ứng dụng)";return set;
   }
  }

  /// <summary>Deserializes a rule set and rejects malformed entries so a bad file cannot widen the clean scope.</summary>
  public static JunkRuleSet Parse(Stream json){
   var set=(JunkRuleSet)new DataContractJsonSerializer(typeof(JunkRuleSet)).ReadObject(json);
   if(set==null||set.Rules==null)throw new IOException("Tệp quy tắc trống.");
   var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var rule in set.Rules){
    if(String.IsNullOrWhiteSpace(rule.Id)||!ids.Add(rule.Id))throw new IOException("Quy tắc thiếu id hoặc trùng id.");
    if(String.IsNullOrWhiteSpace(rule.Name))rule.Name=rule.Id;
    if(String.IsNullOrWhiteSpace(rule.Group))rule.Group="Khác";
    if(rule.Paths==null||rule.Paths.Count==0)throw new IOException("Quy tắc "+rule.Id+" không có đường dẫn.");
    if(rule.Patterns==null||rule.Patterns.Count==0)rule.Patterns=new List<string>{"*"};
    if(rule.BlockedProcesses==null)rule.BlockedProcesses=new List<string>();
    if(rule.MinAgeHours<0)rule.MinAgeHours=0;
    foreach(string pattern in rule.Patterns)if(pattern.IndexOfAny(new[]{'\\','/',':'})>=0)throw new IOException("Mẫu tên tệp không được chứa dấu phân cách đường dẫn: "+pattern);
   }
   return set;
  }

  /// <summary>Expands environment variables and single-level "*" directory segments into concrete existing folders.</summary>
  public static List<string> ExpandPath(string template){
   var results=new List<string>();
   if(String.IsNullOrWhiteSpace(template))return results;
   string expanded=Environment.ExpandEnvironmentVariables(template.Trim());
   if(expanded.IndexOf('%')>=0)return results;
   string root;
   try{root=Path.GetPathRoot(expanded);}catch(ArgumentException){return results;}
   if(String.IsNullOrEmpty(root)||root.StartsWith(@"\\")||!Path.IsPathRooted(expanded))return results;
   var segments=expanded.Substring(root.Length).Split(new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries).ToList();
   if(segments.Count==0)return results;
   var current=new List<string>{root};
   for(int i=0;i<segments.Count;i++){
    string segment=segments[i];var next=new List<string>();
    foreach(string parent in current){
     if(segment=="*"){
      try{if(Directory.Exists(parent))foreach(string dir in Directory.EnumerateDirectories(parent))if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)==0)next.Add(dir);}
      catch(IOException){}catch(UnauthorizedAccessException){}
     }else if(segment.IndexOfAny(new[]{'*','?'})>=0){
      // Partial wildcards are refused; a rule may only wildcard a whole profile-style segment.
      return new List<string>();
     }else next.Add(Path.Combine(parent,segment));
    }
    current=next;if(current.Count==0)break;
   }
   foreach(string path in current){try{results.Add(Engine.Canon(path));}catch(ArgumentException){}catch(NotSupportedException){}}
   return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>Converts a file-name glob (* and ?) into an anchored case-insensitive regex.</summary>
  public static Regex GlobToRegex(string pattern){
   string escaped=Regex.Escape(pattern??"*").Replace("\\*",".*").Replace("\\?",".");
   return new Regex("^"+escaped+"$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  }

  /// <summary>True when the file name matches at least one glob of the rule.</summary>
  public static bool MatchesName(IEnumerable<Regex> patterns,string fileName){
   foreach(var regex in patterns)if(regex.IsMatch(fileName))return true;
   return false;
  }
 }

 /// <summary>Code-level boundary for the junk cleaner. Rules are data; this class decides which folders rules may ever reach.</summary>
 public static class JunkSafety {
  /// <summary>Leaf folder names that qualify as browser cache; a browser rule path must end in one of them.</summary>
  public static readonly string[] CacheLeaves={"Cache","Code Cache","GPUCache","ShaderCache","GrShaderCache","DawnCache","DawnGraphiteCache","DawnWebGPUCache","Media Cache","cache2","startupCache","shader-cache","OfflineCache","jumpListCache","thumbnails"};
  /// <summary>Folders that cache-only browser rules may target; the leaf must be a cache folder.</summary>
  static readonly string[] BrowserRoots={@"Google\Chrome\User Data",@"Microsoft\Edge\User Data",@"BraveSoftware\Brave-Browser\User Data",@"Vivaldi\User Data",@"Chromium\User Data",@"Mozilla\Firefox\Profiles",@"Opera Software"};

  /// <summary>Allowed junk areas relative to well-known folders. Returned as canonical absolute paths that exist or not.</summary>
  public static List<string> AllowedRoots(){
   var list=new List<string>();
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   string programData=Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
   string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
   Action<string> add=p=>{if(!String.IsNullOrWhiteSpace(p)){try{list.Add(Engine.Canon(p));}catch(ArgumentException){}}};
   try{add(Path.GetTempPath());}catch(Exception){}
   if(!String.IsNullOrWhiteSpace(local)){
    add(Path.Combine(local,"Temp"));add(Path.Combine(local,"CrashDumps"));
    add(Path.Combine(local,@"Microsoft\Windows\WER"));add(Path.Combine(local,@"Microsoft\Windows\Explorer"));
    add(Path.Combine(local,@"Microsoft\Windows\INetCache"));add(Path.Combine(local,"D3DSCache"));add(Path.Combine(local,@"NVIDIA\DXCache"));add(Path.Combine(local,@"NVIDIA\GLCache"));add(Path.Combine(local,@"AMD\DxCache"));
   }
   if(!String.IsNullOrWhiteSpace(programData))add(Path.Combine(programData,@"Microsoft\Windows\WER"));
   if(!String.IsNullOrWhiteSpace(windows))add(Path.Combine(windows,"Temp"));
   return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>Browser profile roots under %LOCALAPPDATA% and %APPDATA%.</summary>
  public static List<string> BrowserProfileRoots(){
   var list=new List<string>();
   foreach(string basePath in new[]{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}){
    if(String.IsNullOrWhiteSpace(basePath))continue;
    foreach(string rel in BrowserRoots){try{list.Add(Engine.Canon(Path.Combine(basePath,rel)));}catch(ArgumentException){}}
   }
   return list;
  }

  /// <summary>Folders the cleaner must never enter regardless of rule content: documents, media, downloads, cloud folders, our own data.</summary>
  public static List<string> ForbiddenRoots(){
   var list=new List<string>();
   Action<string> add=p=>{if(!String.IsNullOrWhiteSpace(p)){try{list.Add(Engine.Canon(p));}catch(ArgumentException){}}};
   foreach(var folder in new[]{Environment.SpecialFolder.MyDocuments,Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.MyPictures,Environment.SpecialFolder.MyMusic,Environment.SpecialFolder.MyVideos,Environment.SpecialFolder.Favorites,Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.System,Environment.SpecialFolder.SystemX86})add(Environment.GetFolderPath(folder));
   string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   if(!String.IsNullOrWhiteSpace(profile)){add(Path.Combine(profile,"Downloads"));add(Path.Combine(profile,"OneDrive"));add(Path.Combine(profile,"Dropbox"));add(Path.Combine(profile,"Google Drive"));}
   add(Environment.GetEnvironmentVariable("OneDrive"));add(Environment.GetEnvironmentVariable("OneDriveCommercial"));
   add(Core.Paths.Root);add(Core.Paths.Legacy);add(Engine.Vault);
   // A redirected "Documents" that equals the profile root would otherwise forbid AppData itself; ancestors of the
   // allowed areas are dropped because the allow list already restricts the cleaner to those subfolders.
   var anchors=new List<string>();
   try{anchors.Add(Engine.Canon(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));}catch(Exception){}
   try{anchors.Add(Engine.Canon(Path.GetTempPath()));}catch(Exception){}
   return list.Distinct(StringComparer.OrdinalIgnoreCase).Where(f=>!anchors.Any(a=>String.Equals(a,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(a,f))).ToList();
  }

  /// <summary>Validates a concrete expanded rule folder against the code-level allow and deny lists. Throws IOException when refused.</summary>
  public static void ValidateRoot(string folder){
   ValidateRoot(folder,AllowedRoots(),BrowserProfileRoots(),ForbiddenRoots());
  }

  /// <summary>Testable overload of ValidateRoot with explicit boundary lists.</summary>
  public static void ValidateRoot(string folder,IList<string> allowed,IList<string> browserRoots,IList<string> forbidden){
   string p=Engine.Canon(folder);
   if(p.Length<=3)throw new IOException("Không dọn gốc ổ đĩa.");
   foreach(string f in forbidden)if(String.Equals(p,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,f)||Engine.Under(f,p))throw new IOException("Đường dẫn chạm vào thư mục người dùng hoặc hệ thống được bảo vệ: "+folder);
   bool ok=allowed.Any(a=>String.Equals(p,a,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,a));
   if(!ok){
    bool browser=browserRoots.Any(b=>Engine.Under(p,b)&&HasCacheSegment(p,b));
    if(!browser)throw new IOException("Đường dẫn nằm ngoài vùng rác được phép: "+folder);
   }
   Engine.NoLinks(p,false);
  }

  /// <summary>True when some folder between the browser profile root and the path is a recognised cache folder.</summary>
  public static bool HasCacheSegment(string path,string browserRoot){
   string rest=path.Substring(browserRoot.TrimEnd('\\').Length).Trim('\\');
   return rest.Split('\\').Any(segment=>CacheLeaves.Contains(segment,StringComparer.OrdinalIgnoreCase));
  }

  /// <summary>Checks that a file found during enumeration is still under its validated root and is not a link or a very recent file.</summary>
  public static bool AcceptFile(FileInfo file,string root,int minAgeHours,DateTime now){
   try{
    if((file.Attributes&FileAttributes.ReparsePoint)!=0)return false;
    if(String.Equals(file.Name,"desktop.ini",StringComparison.OrdinalIgnoreCase))return false;
    string full=file.FullName;
    if(!Engine.Under(full,root))return false;
    if(minAgeHours>0){
     DateTime threshold=now.AddHours(-minAgeHours);
     if(file.LastWriteTime>threshold||file.CreationTime>threshold||file.LastAccessTime>threshold)return false;
    }
    return true;
   }catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
  }
 }
}
