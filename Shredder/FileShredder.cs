using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace TweekPro.Shredder {
 /// <summary>Read-only inventory of what a shred would touch: files, bytes, skipped links and the reasons anything was refused.</summary>
 public sealed class ShredPlan {
  public string Target="";public bool IsDirectory;public List<string> Files=new List<string>();public List<string> Folders=new List<string>();public List<string> SkippedLinks=new List<string>();
  public long Bytes;public List<string> Notes=new List<string>();public bool Truncated;
 }

 /// <summary>Outcome of a shred: what was overwritten and removed, and what failed (left in place, partially overwritten).</summary>
 public sealed class ShredReport {
  public int Files,Folders,Links,Passes;public long Bytes;public List<string> Failed=new List<string>();public TimeSpan Elapsed;
 }

 /// <summary>Hard boundaries for the shredder: no drive roots, nothing inside Windows/Program Files, nothing of Tweek Pro itself, never through a reparse point.</summary>
 public static class ShredSafety {
  public const int MaxFiles=20000;
  /// <summary>Roots that are always refused (canonical, no trailing separator).</summary>
  public static List<string> ForbiddenRoots(){
   var list=new List<string>();
   Action<string> add=p=>{if(!String.IsNullOrWhiteSpace(p)){try{list.Add(Engine.Canon(p));}catch(ArgumentException){}catch(NotSupportedException){}}};
   foreach(var f in new[]{Environment.SpecialFolder.Windows,Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.CommonProgramFiles,Environment.SpecialFolder.CommonProgramFilesX86})add(Environment.GetFolderPath(f));
   add(Environment.GetEnvironmentVariable("ProgramW6432"));add(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));add(Environment.GetEnvironmentVariable("SystemRoot"));
   string programData=Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);if(!String.IsNullOrWhiteSpace(programData))add(Path.Combine(programData,"Microsoft"));
   add(Core.Paths.Root);add(Core.Paths.Legacy);add(Engine.Vault);
   try{add(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));}catch(Exception){}
   return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>Throws when the path may not be shredded: empty, a drive root, directly under a drive root as a system file, inside a forbidden root, or a reparse point.</summary>
  public static void Validate(string path){Validate(path,ForbiddenRoots());}

  /// <summary>Testable overload with an explicit forbidden list.</summary>
  public static void Validate(string path,IList<string> forbidden){
   if(String.IsNullOrWhiteSpace(path))throw new IOException("Chưa chọn tệp hoặc thư mục để xóa.");
   string p;try{p=Engine.Canon(path);}catch(Exception){throw new IOException("Đường dẫn không hợp lệ.");}
   string root=Path.GetPathRoot(p)??"";
   if(p.Length<=3||String.Equals(p,root.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))throw new IOException("Không xóa gốc ổ đĩa.");
   if(p.StartsWith(@"\\",StringComparison.Ordinal)&&!p.StartsWith(@"\\?\",StringComparison.Ordinal))throw new IOException("Không xóa không phục hồi trên đường dẫn mạng.");
   string profile="";try{profile=Engine.Canon(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));}catch(Exception){}
   if(profile!=""&&String.Equals(p,profile,StringComparison.OrdinalIgnoreCase))throw new IOException("Không xóa cả thư mục hồ sơ người dùng.");
   string users=profile!=""?Path.GetDirectoryName(profile):null;
   if(!String.IsNullOrEmpty(users)&&(String.Equals(p,users,StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetDirectoryName(p),users,StringComparison.OrdinalIgnoreCase)))throw new IOException("Không xóa thư mục hồ sơ người dùng.");
   foreach(string f in forbidden)if(String.Equals(p,f,StringComparison.OrdinalIgnoreCase)||Engine.Under(p,f))throw new IOException("Từ chối: nằm trong vùng hệ thống được bảo vệ ("+f+").");
   foreach(string f in forbidden)if(Engine.Under(f,p))throw new IOException("Từ chối: thư mục này chứa vùng hệ thống được bảo vệ ("+f+").");
   if(String.Equals(Path.GetDirectoryName(p),root.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)&&File.Exists(p)){
    var attr=File.GetAttributes(p);if((attr&FileAttributes.System)!=0)throw new IOException("Từ chối: tệp hệ thống ở gốc ổ đĩa (pagefile, hiberfil, boot…).");
   }
   if(IsLink(p))throw new IOException("Từ chối: đây là liên kết (symlink/junction). Xóa liên kết bằng Explorer, nội dung thật nằm ở nơi khác.");
  }

  /// <summary>True when the path itself is a reparse point (symlink, junction, mount point). Shredding through one would destroy the target instead.</summary>
  public static bool IsLink(string path){
   try{if(File.Exists(path))return (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;if(Directory.Exists(path))return (new DirectoryInfo(path).Attributes&FileAttributes.ReparsePoint)!=0;}catch(Exception){}
   return false;
  }
 }

 /// <summary>Overwrites file contents in place, renames to a random name and deletes; nothing goes to the vault, which is the whole point. The plan is computed first so the UI can show exactly what is at stake.</summary>
 public static class FileShredder {
  public const int ChunkSize=1<<20;
  public static readonly int[] AllowedPasses={1,3};

  /// <summary>Walks the target without following reparse points and sums sizes; refuses via ShredSafety before touching anything.</summary>
  public static ShredPlan Plan(string path,CancellationToken token){
   ShredSafety.Validate(path);
   var plan=new ShredPlan{Target=Engine.Canon(path)};
   if(File.Exists(plan.Target)){var fi=new FileInfo(plan.Target);plan.Files.Add(fi.FullName);plan.Bytes=Math.Max(0,fi.Length);AnnotateFile(plan,fi);return plan;}
   if(!Directory.Exists(plan.Target))throw new IOException("Không tìm thấy tệp hoặc thư mục: "+path);
   plan.IsDirectory=true;
   var stack=new Stack<string>();stack.Push(plan.Target);
   while(stack.Count>0){
    token.ThrowIfCancellationRequested();
    string dir=stack.Pop();plan.Folders.Add(dir);
    IEnumerable<string> entries;try{entries=Directory.EnumerateFileSystemEntries(dir);}catch(UnauthorizedAccessException){plan.Notes.Add("Không đọc được: "+dir);continue;}catch(IOException){plan.Notes.Add("Không đọc được: "+dir);continue;}
    foreach(string e in entries){
     token.ThrowIfCancellationRequested();
     FileAttributes attr;try{attr=File.GetAttributes(e);}catch(Exception){plan.Notes.Add("Không đọc được: "+e);continue;}
     if((attr&FileAttributes.ReparsePoint)!=0){plan.SkippedLinks.Add(e);continue;}
     if((attr&FileAttributes.Directory)!=0){stack.Push(e);continue;}
     if(plan.Files.Count>=ShredSafety.MaxFiles){plan.Truncated=true;break;}
     var fi=new FileInfo(e);plan.Files.Add(fi.FullName);plan.Bytes+=Math.Max(0,fi.Length);AnnotateFile(plan,fi);
    }
    if(plan.Truncated)break;
   }
   if(plan.Truncated)plan.Notes.Add("Quá "+ShredSafety.MaxFiles.ToString("N0")+" tệp — hãy xóa theo từng thư mục con.");
   return plan;
  }

  static void AnnotateFile(ShredPlan plan,FileInfo fi){
   var a=fi.Attributes;
   if((a&FileAttributes.Compressed)!=0||(a&FileAttributes.SparseFile)!=0||(a&FileAttributes.Encrypted)!=0)if(plan.Notes.Count<50)plan.Notes.Add("Tệp nén/thưa/mã hóa NTFS, ghi đè có thể không trúng cụm gốc: "+fi.Name);
  }

  /// <summary>Executes the plan: overwrite each file with the chosen passes, rename it to a random name, delete; unlink (never follow) reparse points; then remove folders bottom-up under random names. Refusal is re-checked so a plan cannot be replayed against another path.</summary>
  public static ShredReport Shred(ShredPlan plan,int passes,Action<string> stage,CancellationToken token){
   if(plan==null)throw new ArgumentNullException("plan");
   if(Array.IndexOf(AllowedPasses,passes)<0)throw new ArgumentOutOfRangeException("passes");
   if(plan.Truncated)throw new IOException("Danh sách bị cắt ngắn — không chạy để tránh xóa thiếu. Hãy xóa theo từng thư mục con.");
   ShredSafety.Validate(plan.Target);
   var report=new ShredReport{Passes=passes};var watch=Stopwatch.StartNew();
   foreach(string file in plan.Files){
    token.ThrowIfCancellationRequested();
    if(stage!=null)stage(Path.GetFileName(file));
    try{
     if(!Engine.Under(file,plan.Target)&&!String.Equals(file,plan.Target,StringComparison.OrdinalIgnoreCase))throw new IOException("Ngoài mục tiêu.");
     if(ShredSafety.IsLink(file)){report.Failed.Add(file+" — liên kết, bỏ qua");continue;}
     report.Bytes+=Overwrite(file,passes,token);
     Erase(file);report.Files++;
    }catch(OperationCanceledException){throw;}
    catch(Exception e){report.Failed.Add(file+" — "+e.Message);}
   }
   if(plan.IsDirectory){
    foreach(string link in plan.SkippedLinks){
     token.ThrowIfCancellationRequested();
     try{if(!ShredSafety.IsLink(link))continue;if(Directory.Exists(link))Directory.Delete(link);else File.Delete(link);report.Links++;}
     catch(Exception e){report.Failed.Add(link+" — "+e.Message);}
    }
    foreach(string dir in plan.Folders.OrderByDescending(d=>d.Length)){
     token.ThrowIfCancellationRequested();
     try{if(!Directory.Exists(dir))continue;if(Directory.EnumerateFileSystemEntries(dir).Any()){report.Failed.Add(dir+" — còn nội dung");continue;}EraseDirectory(dir);report.Folders++;}
     catch(Exception e){report.Failed.Add(dir+" — "+e.Message);}
    }
   }
   report.Elapsed=watch.Elapsed;return report;
  }

  /// <summary>Overwrites every byte of the file passes times (1: random; 3: zeros, 0xFF, random), flushing to disk after each pass, then truncates it to zero length. Returns the bytes written.</summary>
  public static long Overwrite(string file,int passes,CancellationToken token){
   var fi=new FileInfo(file);if(!fi.Exists)throw new FileNotFoundException("Không thấy tệp.",file);
   if((fi.Attributes&(FileAttributes.ReadOnly|FileAttributes.Hidden|FileAttributes.System))!=0)File.SetAttributes(file,FileAttributes.Normal);
   long length=fi.Length,written=0;
   using(var fs=new FileStream(file,FileMode.Open,FileAccess.Write,FileShare.None,ChunkSize,FileOptions.WriteThrough)){
    var buffer=new byte[(int)Math.Min(ChunkSize,Math.Max(1,length))];
    using(var rng=RandomNumberGenerator.Create()){
     for(int pass=0;pass<passes;pass++){
      byte? fill=passes==3?(pass==0?(byte?)0x00:pass==1?(byte?)0xFF:null):null;
      if(fill.HasValue)for(int i=0;i<buffer.Length;i++)buffer[i]=fill.Value;
      fs.Position=0;long left=length;
      while(left>0){
       token.ThrowIfCancellationRequested();
       int n=(int)Math.Min(buffer.Length,left);
       if(!fill.HasValue)rng.GetBytes(buffer);
       fs.Write(buffer,0,n);left-=n;written+=n;
      }
      fs.Flush(true);
     }
    }
    fs.SetLength(0);fs.Flush(true);
   }
   return written;
  }

  /// <summary>Renames the (already emptied) file to a random name inside the same folder so the original name does not linger in the MFT record, then deletes it.</summary>
  public static void Erase(string file){
   string dir=Path.GetDirectoryName(file);string random=RandomName(dir,"");
   try{File.Move(file,random);File.Delete(random);}catch(IOException){File.Delete(file);}catch(UnauthorizedAccessException){File.Delete(file);}
  }

  static void EraseDirectory(string dir){
   string parent=Path.GetDirectoryName(dir);
   if(String.IsNullOrEmpty(parent)){Directory.Delete(dir);return;}
   string random=RandomName(parent,"");
   try{Directory.Move(dir,random);Directory.Delete(random);}catch(IOException){Directory.Delete(dir);}catch(UnauthorizedAccessException){Directory.Delete(dir);}
  }

  static string RandomName(string dir,string ext){
   var bytes=new byte[8];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);
   string name=BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant()+ext;
   return String.IsNullOrEmpty(dir)?name:Path.Combine(dir,name);
  }

  /// <summary>Human-readable note about the medium: overwriting is only meaningful on spinning disks; SSDs remap blocks, so the user should also rely on TRIM/encryption.</summary>
  public static string MediumNote(string path){
   try{
    string root=Path.GetPathRoot(Engine.Canon(path));if(String.IsNullOrEmpty(root))return "";
    var drive=new DriveInfo(root);
    if(drive.DriveType==DriveType.Removable)return "Ổ tháo lắp (USB/thẻ nhớ): bộ điều khiển flash có thể giữ bản sao cũ; ghi đè giảm rủi ro nhưng không tuyệt đối.";
    if(drive.DriveType==DriveType.Network)return "Ổ mạng: máy chủ quyết định dữ liệu có thật sự bị ghi đè hay không.";
   }catch(Exception){}
   return "Trên SSD/NVMe, bộ điều khiển có thể ghi vào ô nhớ khác nên ghi đè không đảm bảo 100%; hãy bật BitLocker hoặc TRIM để bảo vệ trọn vẹn. Trên HDD, ghi đè một lần đã đủ với phần cứng hiện đại.";
  }
 }
}
