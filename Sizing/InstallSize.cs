using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using TweekPro.Core;

namespace TweekPro.Sizing {
 /// <summary>Bounded, read-only folder measurement. Never throws; -1 means the folder could not be read at all.</summary>
 public static class FolderSize {
  public const int MaxFiles=250000;

  /// <summary>Sums file lengths under root within the time budget. The root itself may be a junction (WindowsApps packages moved to another drive); nested reparse points are skipped so loops cannot form.</summary>
  public static long Measure(string root,TimeSpan budget,out bool partial){
   partial=false;
   if(String.IsNullOrWhiteSpace(root))return -1;
   try{if(!Directory.Exists(root))return -1;}catch(Exception){return -1;}
   long bytes=0;int files=0;var watch=Stopwatch.StartNew();bool readAny=false;
   var stack=new Stack<string>();stack.Push(root);
   while(stack.Count>0){
    if(files>=MaxFiles||watch.Elapsed>budget){partial=true;break;}
    string current=stack.Pop();
    try{
     foreach(string file in Directory.EnumerateFiles(current)){
      if(files>=MaxFiles){partial=true;break;}
      FileInfo info;try{info=new FileInfo(file);if((info.Attributes&FileAttributes.ReparsePoint)!=0)continue;bytes+=info.Length;}catch(Exception){continue;}
      files++;
     }
     readAny=true;
     foreach(string sub in Directory.EnumerateDirectories(current)){
      try{if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)!=0)continue;}catch(Exception){continue;}
      stack.Push(sub);
     }
    }catch(UnauthorizedAccessException){}catch(IOException){}catch(System.Security.SecurityException){}
   }
   return readAny?bytes:-1;
  }
 }

 /// <summary>One Steam title as recorded by the client: library it lives in, folder name and bytes on disk.</summary>
 public class SteamApp {
  public long AppId; public string Name="", InstallDir="", Library=""; public long SizeOnDisk=-1;
  /// <summary>Full folder of the game when the library and installdir are known.</summary>
  public string Path { get { return Library==""||InstallDir==""?"":System.IO.Path.Combine(Library,"steamapps","common",InstallDir); } }
 }

 /// <summary>
 /// Reads Steam's own bookkeeping (libraryfolders.vdf + appmanifest_*.acf) so a game moved to another drive still shows
 /// its real folder and size even though the Uninstall key keeps the original path and never writes EstimatedSize.
 /// </summary>
 public static class SteamLibrary {
  static readonly Regex UninstallId=new Regex(@"steam://uninstall/(\d+)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
  static readonly Regex KeyId=new Regex(@"^Steam App (\d+)$",RegexOptions.IgnoreCase|RegexOptions.Compiled);
  static readonly Regex Token=new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"|([{}])",RegexOptions.Compiled);

  /// <summary>Steam appid from the Uninstall key name ("Steam App 578080") or its UninstallString (steam://uninstall/578080); 0 when the entry is not a Steam title.</summary>
  public static long AppIdOf(string keyName,string uninstallString){
   var m=KeyId.Match(Path.GetFileName(keyName??""));if(m.Success)return long.Parse(m.Groups[1].Value);
   m=UninstallId.Match(uninstallString??"");return m.Success?long.Parse(m.Groups[1].Value):0;
  }

  /// <summary>Minimal Valve KeyValues reader: nested quoted keys and values, braces for children. Keys are compared case-insensitively.</summary>
  public static Dictionary<string,object> ParseVdf(string text){
   var root=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
   if(String.IsNullOrEmpty(text))return root;
   var stack=new Stack<Dictionary<string,object>>();stack.Push(root);string pendingKey=null;int guard=0;
   foreach(Match m in Token.Matches(text)){
    if(++guard>200000)break;
    if(m.Groups[2].Success){
     if(m.Value=="{"){var child=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);if(pendingKey!=null)stack.Peek()[pendingKey]=child;stack.Push(child);pendingKey=null;}
     else if(stack.Count>1)stack.Pop();
     continue;
    }
    string token=Unescape(m.Groups[1].Value);
    if(pendingKey==null)pendingKey=token;else{stack.Peek()[pendingKey]=token;pendingKey=null;}
   }
   return root;
  }

  static string Unescape(string s){return s.Replace("\\\\","\\").Replace("\\\"","\"");}
  static Dictionary<string,object> Child(Dictionary<string,object> d,string key){object o;return d!=null&&d.TryGetValue(key,out o)?o as Dictionary<string,object>:null;}
  static string Text(Dictionary<string,object> d,string key){object o;return d!=null&&d.TryGetValue(key,out o)?o as string??"":"";}

  /// <summary>Libraries listed in libraryfolders.vdf with the appid → bytes map Steam keeps per library.</summary>
  public static List<KeyValuePair<string,Dictionary<long,long>>> ParseLibraryFolders(string vdf){
   var result=new List<KeyValuePair<string,Dictionary<long,long>>>();
   var root=ParseVdf(vdf);var folders=Child(root,"libraryfolders")??root;
   foreach(var entry in folders){
    var lib=entry.Value as Dictionary<string,object>;if(lib==null)continue;
    string path=Text(lib,"path");if(path=="")continue;
    var apps=new Dictionary<long,long>();var appNode=Child(lib,"apps");
    if(appNode!=null)foreach(var a in appNode){long id,bytes;if(long.TryParse(a.Key,out id)&&long.TryParse(a.Value as string??"",out bytes))apps[id]=bytes;}
    result.Add(new KeyValuePair<string,Dictionary<long,long>>(path,apps));
   }
   return result;
  }

  /// <summary>Reads name, installdir and SizeOnDisk from an appmanifest_*.acf body.</summary>
  public static SteamApp ParseManifest(string acf){
   var root=ParseVdf(acf);var state=Child(root,"AppState")??root;
   long id,size;long.TryParse(Text(state,"appid"),out id);
   var app=new SteamApp{AppId=id,Name=Text(state,"name"),InstallDir=Text(state,"installdir")};
   if(long.TryParse(Text(state,"SizeOnDisk"),out size))app.SizeOnDisk=size;
   return app;
  }

  /// <summary>Steam client folders from HKCU/HKLM plus the default location; Windows only, duplicates removed.</summary>
  public static List<string> Roots(){
   var roots=new List<string>();
   if(!StubbornFiles.IsWindows)return roots;
   Action<string> add=p=>{if(String.IsNullOrWhiteSpace(p))return;string full;try{full=Path.GetFullPath(p.Replace('/','\\')).TrimEnd('\\');}catch(Exception){return;}if(!roots.Contains(full,StringComparer.OrdinalIgnoreCase)&&Directory.Exists(full))roots.Add(full);};
   try{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))if(k!=null)add(k.GetValue("SteamPath") as string);}catch(Exception){}
   try{using(var b=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32))using(var k=b.OpenSubKey(@"SOFTWARE\Valve\Steam"))if(k!=null)add(k.GetValue("InstallPath") as string);}catch(Exception){}
   try{string pf=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);if(pf!="")add(Path.Combine(pf,"Steam"));}catch(Exception){}
   return roots;
  }

  /// <summary>Finds the library that holds appid across every Steam root; null when Steam is absent or the title is not installed.</summary>
  public static SteamApp Resolve(long appId){
   if(appId<=0)return null;
   foreach(string root in Roots()){
    foreach(string vdf in new[]{Path.Combine(root,"steamapps","libraryfolders.vdf"),Path.Combine(root,"config","libraryfolders.vdf")}){
     string text;try{if(!File.Exists(vdf))continue;text=File.ReadAllText(vdf);}catch(Exception){continue;}
     foreach(var lib in ParseLibraryFolders(text)){
      long bytes;if(!lib.Value.TryGetValue(appId,out bytes))continue;
      var app=new SteamApp{AppId=appId,Library=lib.Key,SizeOnDisk=bytes};
      string acf=Path.Combine(lib.Key,"steamapps","appmanifest_"+appId+".acf");
      try{if(File.Exists(acf)){var manifest=ParseManifest(File.ReadAllText(acf));app.Name=manifest.Name;app.InstallDir=manifest.InstallDir;if(manifest.SizeOnDisk>0)app.SizeOnDisk=manifest.SizeOnDisk;}}catch(Exception){}
      return app;
     }
    }
   }
   return null;
  }
 }

 /// <summary>Fills in sizes the Uninstall key or Get-AppxPackage do not report: Steam bookkeeping first, then a bounded walk of the install folder.</summary>
 public static class InstallSize {
  public static readonly TimeSpan PerFolder=TimeSpan.FromSeconds(6);

  /// <summary>True when a folder is specific enough to attribute to one application: not a drive root, not a shared parent such as Program Files, and not inside the Windows directory.</summary>
  public static bool Measurable(string path){
   if(String.IsNullOrWhiteSpace(path))return false;
   string full;try{full=Path.GetFullPath(path.Trim().Trim('"').Replace('/','\\')).TrimEnd('\\');}catch(Exception){return false;}
   if(full.StartsWith(@"\\"))return false;
   string root;try{root=Path.GetPathRoot(full).TrimEnd('\\');}catch(Exception){return false;}
   if(String.Equals(full,root,StringComparison.OrdinalIgnoreCase))return false;
   foreach(var folder in new[]{Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.CommonProgramFiles,Environment.SpecialFolder.CommonProgramFilesX86,Environment.SpecialFolder.CommonApplicationData,Environment.SpecialFolder.UserProfile,Environment.SpecialFolder.LocalApplicationData,Environment.SpecialFolder.ApplicationData,Environment.SpecialFolder.Windows,Environment.SpecialFolder.System}){
    string p;try{p=Environment.GetFolderPath(folder);}catch(Exception){continue;}
    if(String.IsNullOrEmpty(p))continue;p=p.TrimEnd('\\');
    if(String.Equals(full,p,StringComparison.OrdinalIgnoreCase))return false;
    if((folder==Environment.SpecialFolder.Windows||folder==Environment.SpecialFolder.System)&&full.StartsWith(p+"\\",StringComparison.OrdinalIgnoreCase))return false;
   }
   string users=Path.Combine(root+"\\","Users");
   if(String.Equals(full,users,StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetDirectoryName(full)??"",users,StringComparison.OrdinalIgnoreCase))return false;
   return true;
  }

  /// <summary>Folder to measure: the declared InstallLocation, else the folder holding the uninstaller or DisplayIcon executable (what Revo does when the installer declared nothing). Only used for sizing, never for scanning.</summary>
  public static string FolderOf(AppEntry a){
   if(a==null)return "";
   if(!String.IsNullOrWhiteSpace(a.Location))return a.Location;
   foreach(string candidate in new[]{Advanced.CommandExe(a.Command),IconFile(a.DisplayIcon)}){
    if(!Advanced.LocalPath(candidate))continue;
    string dir;try{dir=Path.GetDirectoryName(candidate);}catch(Exception){continue;}
    if(String.IsNullOrEmpty(dir))continue;
    if(String.Equals(Path.GetFileName(dir),"Temp",StringComparison.OrdinalIgnoreCase))continue;
    return dir;
   }
   return "";
  }

  static string IconFile(string displayIcon){
   if(String.IsNullOrWhiteSpace(displayIcon))return "";
   string s=Environment.ExpandEnvironmentVariables(displayIcon.Trim());
   if(s.StartsWith("\"")){int end=s.IndexOf('"',1);return end>1?s.Substring(1,end-1):"";}
   int comma=s.LastIndexOf(',');if(comma>2)s=s.Substring(0,comma);
   return s.Trim();
  }

  /// <summary>KB for the Size column; a folder that exists but holds only zero-length files still shows as 1 KB rather than "unknown".</summary>
  public static long ToKB(long bytes){return bytes<0?0:bytes==0?1:(bytes+1023)/1024;}

  /// <summary>Desktop apps without EstimatedSize: Steam titles read the client's size and current library; others measure their install folder (remembered in SizeCache for a day). Stops adding folder walks once the overall budget is spent.</summary>
  public static int Apply(IEnumerable<AppEntry> apps,TimeSpan budget){
   int filled=0;var watch=Stopwatch.StartNew();var now=DateTime.Now;
   foreach(var a in apps.Where(x=>x.Size<=0).ToList()){
    long steamId=SteamLibrary.AppIdOf(a.Key,a.Command);
    if(steamId>0){
     var steam=SteamLibrary.Resolve(steamId);
     if(steam!=null){
      if(steam.SizeOnDisk>0){a.Size=ToKB(steam.SizeOnDisk);a.SizeMeasured=true;filled++;}
      string path=steam.Path;bool registryExists=false;try{registryExists=!String.IsNullOrWhiteSpace(a.Location)&&Directory.Exists(a.Location);}catch(Exception){}
      if(path!=""&&!registryExists){try{if(Directory.Exists(path))a.Location=path;}catch(Exception){}}
      if(a.Size>0)continue;
     }
    }
    string folder=FolderOf(a);if(!Measurable(folder))continue;
    bool partial;long bytes;
    if(!SizeCache.TryGet(folder,now,out bytes,out partial)){
     if(watch.Elapsed>budget)continue;
     bytes=FolderSize.Measure(folder,PerFolder,out partial);
     if(bytes>=0)SizeCache.Put(folder,bytes,partial,now);
    }
    if(bytes>=0){a.Size=ToKB(bytes);a.SizeMeasured=true;a.SizePartial=partial;filled++;}
   }
   SizeCache.Flush(now);
   return filled;
  }
 }

 /// <summary>English strings for the size probes; merged by Core.L.</summary>
 public static class SizingLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Dung lượng đo từ thư mục cài (bộ cài không khai báo).","Size measured from the install folder (the installer did not report one)."},
   {"Dung lượng đo chưa đủ — thư mục quá lớn hoặc đọc quá lâu.","Measured size is incomplete — the folder is too large or took too long to read."},
   {"Dung lượng do Steam ghi nhận; thư mục thật: ","Size recorded by Steam; actual folder: "},
   {"Đã bổ sung dung lượng cho {0} ứng dụng từ Steam hoặc thư mục cài.","Filled in sizes for {0} applications from Steam or their install folders."},
   {"Đang đo dung lượng gói…","Measuring package sizes…"},
   {"Dung lượng gói đo từ thư mục cài; gói ở ổ khác được theo dõi qua liên kết của Windows.","Package size measured from its install folder; packages moved to another drive are followed through the Windows link."},
  };
 }
}
