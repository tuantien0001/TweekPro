using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TweekPro.Remnants {
 /// <summary>
 /// Builds a synthetic application record for a program that no longer has (or never had) an Uninstall registry entry, so the
 /// regular deep scan and vault flow can hunt its remains. The record never points at a registered key, so Engine.Installed is
 /// false and every candidate still passes the usual boundaries (ValidateFolder, ValidateRegistry, Review-only for shared roots).
 /// </summary>
 public static class ForcedUninstall {
  public const string KeyPrefix=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TweekPro-Forced-";
  public const int MaxExecutables=200;

  /// <summary>Roots under which a user-picked folder is refused outright: the folder itself would be the OS, a shared root or the whole profile.</summary>
  public static List<string> RefusedFolders(){
   var list=new List<string>();
   Action<string> add=p=>{if(!String.IsNullOrWhiteSpace(p)){try{list.Add(Engine.Canon(p));}catch(Exception){}}};
   foreach(var f in new[]{Environment.SpecialFolder.Windows,Environment.SpecialFolder.System,Environment.SpecialFolder.SystemX86,Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.CommonProgramFiles,Environment.SpecialFolder.CommonProgramFilesX86,Environment.SpecialFolder.CommonApplicationData,Environment.SpecialFolder.ApplicationData,Environment.SpecialFolder.LocalApplicationData,Environment.SpecialFolder.UserProfile,Environment.SpecialFolder.MyDocuments,Environment.SpecialFolder.DesktopDirectory})add(Environment.GetFolderPath(f));
   add(Engine.LocalLow());add(Engine.LocalPrograms());add(Core.Paths.Root);add(Engine.Vault);
   string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   if(!String.IsNullOrWhiteSpace(profile))foreach(string leaf in new[]{"Downloads","Pictures","Videos","Music","OneDrive"})add(Path.Combine(profile,leaf));
   return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>Throws when the folder is a drive root, lies inside the Windows tree, or is one of the shared/profile roots themselves.</summary>
  public static void ValidatePickedFolder(string folder,IList<string> refused,string windowsRoot){
   if(String.IsNullOrWhiteSpace(folder))throw new IOException("Chưa chọn thư mục.");
   string p;try{p=Engine.Canon(folder);}catch(Exception){throw new IOException("Đường dẫn thư mục không hợp lệ.");}
   if(p.Length<=3||String.Equals(p,Path.GetPathRoot(p).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new IOException("Không nhận gốc ổ đĩa làm thư mục ứng dụng.");
   if(!String.IsNullOrWhiteSpace(windowsRoot)&&(String.Equals(p,windowsRoot,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,windowsRoot)))throw new IOException("Thư mục nằm trong Windows; không phải ứng dụng.");
   foreach(string r in refused)if(String.Equals(p,r,StringComparison.OrdinalIgnoreCase))throw new IOException("Đây là thư mục dùng chung hoặc thư mục người dùng, không phải thư mục của một ứng dụng: "+p);
   foreach(string r in refused)if(Engine.Under(p,r)&&p.Substring(r.Length).Trim('\\').IndexOf('\\')<0&&new[]{"Microsoft","Windows","Common Files","Packages","WindowsApps"}.Contains(Path.GetFileName(p),StringComparer.OrdinalIgnoreCase))throw new IOException("Thư mục hệ thống dùng chung; không xử lý: "+p);
  }
  public static void ValidatePickedFolder(string folder){ValidatePickedFolder(folder,RefusedFolders(),Environment.GetFolderPath(Environment.SpecialFolder.Windows));}

  /// <summary>Whether a name alone is specific enough to drive a scan (long or multi-word, not a generic word).</summary>
  public static bool NameIsSpecific(string name){return Engine.SafeName(name)&&TraceHunter.Strong(name.Trim());}

  /// <summary>Sanitized key leaf so the synthetic Id stays a valid registry path that can never collide with a real entry.</summary>
  public static string KeyFor(string name){
   var sb=new System.Text.StringBuilder();
   foreach(char c in (name??"").Trim()){sb.Append(Char.IsLetterOrDigit(c)?c:'-');if(sb.Length>=60)break;}
   return KeyPrefix+sb;
  }

  /// <summary>
  /// Creates the record. Name is mandatory; a short or generic name is accepted only together with a folder or executable.
  /// A folder inside the cleanable roots becomes InstallLocation; any other allowed folder only contributes its executables.
  /// Refuses when an application of the same name is still registered (use the normal uninstall then).
  /// </summary>
  public static AppEntry Build(string name,string folder,string exe,string publisher,IEnumerable<AppEntry> inventory){
   name=(name??"").Trim();folder=(folder??"").Trim().Trim('"');exe=(exe??"").Trim().Trim('"');publisher=(publisher??"").Trim();
   if(!Engine.SafeName(name))throw new IOException("Nhập tên ứng dụng (ít nhất 3 ký tự, không chứa ký tự cấm).");
   if(!NameIsSpecific(name)&&folder==""&&exe=="")throw new IOException("Tên quá ngắn hoặc quá chung để quét một mình; hãy chọn thêm thư mục cài hoặc tệp .exe của ứng dụng.");
   if(inventory!=null){var registered=inventory.FirstOrDefault(a=>String.Equals((a.Name??"").Trim(),name,StringComparison.OrdinalIgnoreCase));if(registered!=null)throw new IOException("«"+registered.Name+"» vẫn còn trong danh sách ứng dụng đã cài. Hãy dùng Gỡ mục đã chọn; Gỡ cưỡng bức chỉ dành cho ứng dụng không còn đăng ký.");}
   var app=new AppEntry{Name=name,Publisher=publisher,Version="",Hive="HKCU",View="64",Key=KeyFor(name),Command="",Location="",KnownExecutables=new List<string>(),InstallDate=""};
   if(exe!=""){
    if(!File.Exists(exe)||!Path.GetExtension(exe).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Tệp .exe không tồn tại: "+exe);
    Engine.NoLinks(exe,false);app.KnownExecutables.Add(Engine.Canon(exe));
    if(folder=="")folder=Path.GetDirectoryName(Engine.Canon(exe));
   }
   if(folder!=""){
    ValidatePickedFolder(folder);
    string canon=Engine.Canon(folder);if(!Directory.Exists(canon))throw new IOException("Thư mục không tồn tại: "+canon);
    Engine.NoLinks(canon,false);
    bool cleanable=false;try{Engine.ValidateFolder(canon,false);cleanable=true;}catch(IOException){}
    if(cleanable)app.Location=canon;
    try{foreach(string file in Directory.EnumerateFiles(canon,"*.exe",SearchOption.TopDirectoryOnly).Take(MaxExecutables)){try{Engine.NoLinks(file,false);app.KnownExecutables.Add(Engine.Canon(file));}catch(IOException){}}}catch(IOException){}catch(UnauthorizedAccessException){}
    if(!cleanable&&app.KnownExecutables.Count==0)throw new IOException("Thư mục nằm ngoài vùng Tweek Pro được phép dọn và không chứa tệp .exe nào để đối chiếu: "+canon);
   }
   app.KnownExecutables=app.KnownExecutables.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
   return app;
  }

  /// <summary>Review-only row for a folder outside the cleanable roots so the user still sees where the program lives.</summary>
  public static Candidate ReviewFolder(AppEntry app,string folder){
   return new Candidate{Kind="Review",ReviewOnly=true,Path=Engine.Canon(folder),AppId=app.Id,AppName=app.Name,Reason="Thư mục bạn chỉ định nằm ngoài vùng Tweek Pro tự dọn (Program Files, AppData, ProgramData); xóa thủ công trong Explorer sau khi đã kiểm tra."};
  }
 }
}
