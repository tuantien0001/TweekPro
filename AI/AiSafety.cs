using System;
using System.IO;
using System.Linq;

namespace TweekPro.AI {
 /// <summary>
 /// Defence in depth for the assistant's automatic mode: every path the AI asks to remove is checked here before the engine's
 /// own boundary checks run. The Windows folder tree, drive roots and top-level profile/program roots are always refused, no
 /// matter what the model or the user typed.
 /// </summary>
 public static class AiSafety {
  /// <summary>Folder names that mark a Windows-owned tree wherever they appear in the path.</summary>
  public static readonly string[] CoreMarkers={"System32","SysWOW64","WinSxS","servicing","Microsoft.NET","SystemApps","SystemResources","Common Files","Windows Defender","WindowsApps"};

  /// <summary>Returns a localized reason when the path must never be removed, or null when the engine may go on to its own checks.</summary>
  public static string ProtectedReason(string path){
   return ProtectedReason(path,Environment.GetFolderPath(Environment.SpecialFolder.Windows),new[]{
    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetEnvironmentVariable("ProgramW6432"),
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),Environment.GetEnvironmentVariable("SystemDrive")==null?null:Environment.GetEnvironmentVariable("SystemDrive")+@"\Users"});
  }

  /// <summary>Testable core: windir is the Windows folder, roots are top-level folders that may contain app data but must not be removed themselves.</summary>
  public static string ProtectedReason(string path,string windir,string[] roots){
   if(String.IsNullOrWhiteSpace(path))return Core.L.T("Đường dẫn trống.");
   string p;try{p=Normalize(path);}catch(ArgumentException){return Core.L.T("Đường dẫn không hợp lệ hoặc không phải đường dẫn tuyệt đối trên ổ đĩa.");}
   if(p.Length<=3||Path.GetPathRoot(p).Length>=p.Length)return Core.L.T("Không bao giờ xóa gốc ổ đĩa.");
   if(!String.IsNullOrWhiteSpace(windir)){
    string w=Normalize(windir);
    if(Same(p,w)||Under(p,w))return Core.L.T("Nằm trong thư mục Windows (System32, WinSxS…) — Tweek Pro không bao giờ xóa.");
   }
   foreach(var root in roots.Where(r=>!String.IsNullOrWhiteSpace(r))){
    string r=Normalize(root);
    if(Same(p,r))return Core.L.F("\"{0}\" là thư mục gốc hệ thống/người dùng, không được xóa.",r);
   }
   var parts=p.Split(new[]{'\\'},StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
   foreach(var part in parts)if(CoreMarkers.Contains(part,StringComparer.OrdinalIgnoreCase))return Core.L.F("Đi qua thư mục hệ thống \"{0}\" — bị chặn.",part);
   return null;
  }

  /// <summary>Canonicalizes absolute drive paths without relying on the host OS path syntax; rejects ambiguous Windows aliases.</summary>
  static string Normalize(string path){
   string p=path.Trim().Replace('/','\\');
   if(p.StartsWith(@"\\?\",StringComparison.Ordinal))p=p.Substring(4);
   if(p.Length<3||!Char.IsLetter(p[0])||p[1]!=':'||p[2]!='\\')throw new ArgumentException("Absolute drive path required");
   var parts=new System.Collections.Generic.List<string>();
   foreach(var part in p.Substring(3).Split(new[]{'\\'},StringSplitOptions.RemoveEmptyEntries)){
    if(part==".")continue;
    if(part==".."){if(parts.Count>0)parts.RemoveAt(parts.Count-1);continue;}
    if(part.EndsWith(".")||part.EndsWith(" ")||part.Any(c=>c<32||"<>:\"|?*".IndexOf(c)>=0))throw new ArgumentException("Ambiguous path");
    parts.Add(part);
   }
   return p.Substring(0,3)+String.Join("\\",parts);
  }
  static bool Same(string a,string b){return String.Equals(a,b,StringComparison.OrdinalIgnoreCase);}
  static bool Under(string p,string root){return p.StartsWith(root.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}
 }
}
