using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TweekPro.Core;
using TweekPro.Store;

namespace TweekPro {
 /// <summary>Diff of uninstall-key ids taken before and after an install session. The UI supplies both lists; this class never opens the registry.</summary>
 public static class InstallWatch {
  public static HashSet<string> Ids(IEnumerable<AppEntry> apps){
   var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   if(apps==null)return set;
   foreach(var a in apps)if(a!=null&&!String.IsNullOrWhiteSpace(a.Id))set.Add(a.Id);
   return set;
  }
  public static List<AppEntry> Added(HashSet<string> before,IEnumerable<AppEntry> after){
   if(before==null)before=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var list=new List<AppEntry>();
   if(after==null)return list;
   foreach(var a in after){
    if(a==null||String.IsNullOrWhiteSpace(a.Id)||before.Contains(a.Id))continue;
    if(list.Any(x=>String.Equals(x.Id,a.Id,StringComparison.OrdinalIgnoreCase)))continue;
    list.Add(a);
   }
   list.Sort((x,y)=>StringComparer.CurrentCultureIgnoreCase.Compare(x.Name,y.Name));
   return list;
  }
 }

 /// <summary>One line per finished uninstall, stored as tab-separated text under the data folder.</summary>
 public static class UninstallHistory {
  public static void Append(string path,string name,string id,bool removed){
   if(String.IsNullOrWhiteSpace(path))throw new IOException("missing history path");
   string dir=Path.GetDirectoryName(path);if(!String.IsNullOrEmpty(dir))Directory.CreateDirectory(dir);
   string line=Clean(name)+"\t"+Clean(id)+"\t"+DateTime.Now.ToString("s")+"\t"+(removed?"removed":"still-registered");
   File.AppendAllText(path,line+"\r\n",new UTF8Encoding(true));
  }
  public static List<string[]> Load(string path){
   var list=new List<string[]>();
   if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))return list;
   foreach(string raw in File.ReadAllLines(path)){
    if(String.IsNullOrWhiteSpace(raw))continue;
    string[] parts=raw.Split('\t');
    if(parts.Length>=4)list.Add(parts);
   }
   return list;
  }
  static string Clean(string s){return (s??"").Replace("\t"," ").Replace("\r"," ").Replace("\n"," ");}
 }

 /// <summary>Other Windows profile folders under a Users directory. Skips the signed-in profile and the well-known shared folders. Does not open NTUSER.DAT.</summary>
 public static class ProfileScope {
  public static readonly string[] Skip={"Public","Default","Default User","All Users","DefaultAccount","WDAGUtilityAccount"};
  public static List<string> OtherProfiles(string usersRoot,string currentProfile){
   var list=new List<string>();
   if(String.IsNullOrWhiteSpace(usersRoot)||!Directory.Exists(usersRoot))return list;
   string current="";
   try{if(!String.IsNullOrWhiteSpace(currentProfile))current=Path.GetFullPath(currentProfile).TrimEnd('\\');}catch(Exception){current="";}
   foreach(string dir in Directory.GetDirectories(usersRoot)){
    string name=Path.GetFileName(dir);
    if(String.IsNullOrEmpty(name)||name.StartsWith(".",StringComparison.Ordinal))continue;
    if(Skip.Any(s=>String.Equals(s,name,StringComparison.OrdinalIgnoreCase)))continue;
    string full;try{full=Path.GetFullPath(dir).TrimEnd('\\');}catch(Exception){continue;}
    if(current!=""&&String.Equals(full,current,StringComparison.OrdinalIgnoreCase))continue;
    list.Add(full);
   }
   list.Sort(StringComparer.OrdinalIgnoreCase);
   return list;
  }
  public static string[] DataFolders(string profile){
   return new[]{Path.Combine(profile,"AppData","Local"),Path.Combine(profile,"AppData","Roaming")};
  }
 }

 /// <summary>Matches a window's executable to an installed application by install folder, then by the uninstall command.</summary>
 public static class HunterMatch {
  public static AppEntry Find(string exePath,IEnumerable<AppEntry> apps){
   if(String.IsNullOrWhiteSpace(exePath)||apps==null)return null;
   string full;try{full=Path.GetFullPath(exePath);}catch(Exception){return null;}
   string file=Path.GetFileName(full);
   if(file.Length<5||!file.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))return null;
   AppEntry byCommand=null;
   foreach(var a in apps){
    if(a==null)continue;
    string loc=(a.Location??"").Trim().TrimEnd('\\');
    if(loc.Length>2&&full.StartsWith(loc+"\\",StringComparison.OrdinalIgnoreCase))return a;
    if(byCommand==null&&(a.Command??"").IndexOf(file,StringComparison.OrdinalIgnoreCase)>=0)byCommand=a;
   }
   return byCommand;
  }
 }

 /// <summary>User-added tools. Only a local .exe, .msc or .cpl is accepted; shells and script hosts are refused.</summary>
 public static class UserTools {
  static readonly string[] Blocked={"cmd.exe","powershell.exe","pwsh.exe","wscript.exe","cscript.exe","mshta.exe","reg.exe","regedit.exe","bash.exe","wt.exe"};
  public static string Refuse(string file){
   if(String.IsNullOrWhiteSpace(file))return "empty";
   if(file.IndexOfAny(new[]{'\r','\n','\0'})>=0)return "bad";
   if(!Path.IsPathRooted(file))return "not-rooted";
   string full;try{full=Path.GetFullPath(file);}catch(Exception){return "bad";}
   string ext=Path.GetExtension(full);
   if(!ext.Equals(".exe",StringComparison.OrdinalIgnoreCase)&&!ext.Equals(".msc",StringComparison.OrdinalIgnoreCase)&&!ext.Equals(".cpl",StringComparison.OrdinalIgnoreCase))return "type";
   string name=Path.GetFileName(full);
   if(Blocked.Any(b=>String.Equals(b,name,StringComparison.OrdinalIgnoreCase)))return "blocked";
   return null;
  }
  public static void Save(string path,IEnumerable<WindowsTool> tools){
   string dir=Path.GetDirectoryName(path);if(!String.IsNullOrEmpty(dir))Directory.CreateDirectory(dir);
   var lines=new List<string>();
   if(tools!=null)foreach(var t in tools){
    if(t==null||Refuse(t.File)!=null)continue;
    lines.Add(Clean(t.Name)+"\t"+t.File+"\t"+Clean(t.Arguments)+"\t"+Clean(t.Description));
   }
   File.WriteAllLines(path,lines,new UTF8Encoding(true));
  }
  public static List<WindowsTool> Load(string path){
   var list=new List<WindowsTool>();
   if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))return list;
   foreach(string raw in File.ReadAllLines(path)){
    string[] p=raw.Split('\t');
    if(p.Length<2||Refuse(p[1])!=null)continue;
    list.Add(new WindowsTool{Name=p[0],File=p[1],Arguments=p.Length>2?p[2]:"",Description=p.Length>3?p[3]:"",Custom=true});
   }
   return list;
  }
  static string Clean(string s){return (s??"").Replace("\t"," ").Replace("\r"," ").Replace("\n"," ");}
 }

 /// <summary>Writes a settings file with the protected AI key removed.</summary>
 public static class SettingsPort {
  public static void Export(Settings settings,string path){
   if(settings==null)throw new IOException("missing settings");
   string key=settings.AiKeyProtected;
   settings.AiKeyProtected="";
   try{settings.Save(path);}
   finally{settings.AiKeyProtected=key;}
  }
 }

 /// <summary>Official installer launch. Only the project's GitHub release downloads are allowed. The running exe never writes Program Files.</summary>
 public static class UpdateInstall {
  public const string Arguments="/VERYSILENT /CLOSEAPPLICATIONS /NORESTART";
  public static bool Allowed(string url){
   Uri u;
   if(String.IsNullOrWhiteSpace(url)||!Uri.TryCreate(url.Trim(),UriKind.Absolute,out u))return false;
   if(!String.Equals(u.Scheme,"https",StringComparison.OrdinalIgnoreCase))return false;
   if(!String.Equals(u.Host,"github.com",StringComparison.OrdinalIgnoreCase))return false;
   return u.AbsolutePath.StartsWith("/tuantien0001/TweekPro/releases/download/",StringComparison.OrdinalIgnoreCase)
    &&u.AbsolutePath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase);
  }
 }

 /// <summary>Reset and terminate scripts for a Store package. Protected packages throw and never produce a command.</summary>
 public static class StoreActions {
  public static string ResetScript(WindowsApp a){return Script(a,"Reset-AppxPackage");}
  public static string StopScript(WindowsApp a){return Script(a,"Stop-AppxPackage");}
  static string Script(WindowsApp a,string cmd){
   if(a==null||WindowsApps.Classify(a)==AppxStatus.Protected)throw new IOException("protected");
   if(!System.Text.RegularExpressions.Regex.IsMatch(a.FullName??"",@"^[A-Za-z0-9._\-]+$"))throw new IOException("bad-name");
   return "$ErrorActionPreference='Stop'; "+cmd+" -Package '"+a.FullName.Replace("'","''")+"'; 'OK'";
  }
 }
}
