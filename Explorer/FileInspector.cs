using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using TweekPro.Core;

namespace TweekPro.Explorer {
 public enum RowKind { Normal, Warning, Note }
 /// <summary>One labelled line of the details view; Group is the Vietnamese section name.</summary>
 public class DetailRow { public string Group, Label, Value; public RowKind Kind; }

 /// <summary>Everything Tweek Pro can tell about one file without opening it in its application.</summary>
 public class FileDetails {
  public string Path="",Name="",Extension="",TypeName="",Owner="",Publisher="",FileDescription="",ProductName="",FileVersion="",Company="",Architecture="",Sha256="",Md5="",ReferrerUrl="",HostUrl="",DisguisedAs="";
  public long Bytes=-1;public DateTime Created,Modified,Accessed;public FileAttributes Attributes;
  public bool Exists,IsDirectory,FromInternet,Signed,Bidi,HashSkipped;public int ZoneId=-1;
  public List<string> Warnings=new List<string>(),Notes=new List<string>();
  public bool Hidden { get { return (Attributes&FileAttributes.Hidden)!=0; } }
  public bool System { get { return (Attributes&FileAttributes.System)!=0; } }
  public bool ReadOnly { get { return (Attributes&FileAttributes.ReadOnly)!=0; } }
  public bool ReparsePoint { get { return (Attributes&FileAttributes.ReparsePoint)!=0; } }
 }

 /// <summary>One row of the folder browser; hidden and system entries are listed regardless of Explorer settings.</summary>
 public class FolderEntry {
  public string Path="",Name="",Extension="",TypeName="";public long Bytes=-1;public DateTime Modified;public FileAttributes Attributes;public bool IsDirectory,IsDrive;
  public bool Hidden { get { return (Attributes&FileAttributes.Hidden)!=0; } }
  public bool System { get { return (Attributes&FileAttributes.System)!=0; } }
  public bool ReparsePoint { get { return (Attributes&FileAttributes.ReparsePoint)!=0; } }
 }
 /// <summary>Result of listing one folder: entries plus counts the UI shows in its status line.</summary>
 public class FolderListing { public string Path="";public List<FolderEntry> Entries=new List<FolderEntry>();public int Hidden,System,Folders,Files;public bool Truncated,IsDrives;public List<string> Notes=new List<string>(); }

 /// <summary>Read-only file inspection: identity, timestamps, attributes, hashes, signature, version resource, PE machine type, Mark of the Web and disguise warnings.</summary>
 public static class FileInspector {
  public const int MaxEntries=20000;

  /// <summary>Lists every entry of a folder (hidden and system included) with extension and shell type; an empty path lists the drives.</summary>
  public static FolderListing ListFolder(string path,CancellationToken token){
   var listing=new FolderListing();
   if(String.IsNullOrWhiteSpace(path)){
    listing.IsDrives=true;
    foreach(var drive in DriveInfo.GetDrives()){
     try{
      string root=drive.RootDirectory.FullName;var e=new FolderEntry{Path=root,Name=drive.IsReady&&drive.VolumeLabel!=""?drive.VolumeLabel+" ("+root.TrimEnd('\\')+")":root.TrimEnd('\\'),IsDirectory=true,IsDrive=true,TypeName=DriveLabel(drive.DriveType)};
      if(drive.IsReady){e.Bytes=drive.TotalSize-drive.TotalFreeSpace;e.Extension=Presentation.BytesLabel(drive.TotalFreeSpace)+" "+L.T("trống");}
      listing.Entries.Add(e);listing.Folders++;
     }catch(IOException){}catch(UnauthorizedAccessException){}
    }
    return listing;
   }
   path=path.Trim().Trim('"');
   if(!Directory.Exists(path))throw new DirectoryNotFoundException(L.T("Thư mục không tồn tại: ")+path);
   var dir=new DirectoryInfo(path);listing.Path=dir.FullName;
   IEnumerable<FileSystemInfo> items;
   try{items=dir.EnumerateFileSystemInfos("*",SearchOption.TopDirectoryOnly);}
   catch(UnauthorizedAccessException e){listing.Notes.Add(L.T("Không đủ quyền đọc: ")+e.Message);return listing;}
   var dirs=new List<FolderEntry>();var files=new List<FolderEntry>();
   try{
    foreach(var item in items){
     token.ThrowIfCancellationRequested();
     if(listing.Entries.Count+dirs.Count+files.Count>=MaxEntries){listing.Truncated=true;break;}
     var e=new FolderEntry{Path=item.FullName,Name=item.Name,Attributes=item.Attributes,IsDirectory=(item.Attributes&FileAttributes.Directory)!=0};
     try{e.Modified=item.LastWriteTime;}catch(ArgumentOutOfRangeException){}
     if(e.IsDirectory){e.TypeName=e.ReparsePoint?L.T("Liên kết thư mục"):L.T("Thư mục");listing.Folders++;dirs.Add(e);}
     else{var fi=item as FileInfo;e.Extension=(fi!=null?fi.Extension:System.IO.Path.GetExtension(item.Name))??"";try{if(fi!=null)e.Bytes=fi.Length;}catch(IOException){}catch(UnauthorizedAccessException){}e.TypeName=TypeNameOf(item.FullName,e.Extension);listing.Files++;files.Add(e);}
     if(e.Hidden)listing.Hidden++;if(e.System)listing.System++;
    }
   }catch(UnauthorizedAccessException e){listing.Notes.Add(L.T("Không đủ quyền đọc: ")+e.Message);}
   catch(IOException e){listing.Notes.Add(L.T("Không đọc được: ")+e.Message);}
   dirs.Sort((a,b)=>String.Compare(a.Name,b.Name,StringComparison.CurrentCultureIgnoreCase));files.Sort((a,b)=>String.Compare(a.Name,b.Name,StringComparison.CurrentCultureIgnoreCase));
   listing.Entries.AddRange(dirs);listing.Entries.AddRange(files);
   return listing;
  }

  static string DriveLabel(DriveType t){switch(t){case DriveType.Fixed:return L.T("Ổ đĩa cục bộ");case DriveType.Removable:return L.T("Ổ đĩa di động");case DriveType.Network:return L.T("Ổ đĩa mạng");case DriveType.CDRom:return L.T("Ổ đĩa quang");case DriveType.Ram:return L.T("Ổ đĩa RAM");default:return L.T("Ổ đĩa");}}

  /// <summary>Parent folder of a path, or "" (drive list) when already at a drive root.</summary>
  public static string ParentOf(string path){
   if(String.IsNullOrWhiteSpace(path))return "";
   try{string p=System.IO.Path.GetFullPath(path.Trim().Trim('"'));if(String.Equals(System.IO.Path.GetPathRoot(p),p,StringComparison.OrdinalIgnoreCase))return "";var parent=Directory.GetParent(p.TrimEnd('\\'));return parent==null?"":parent.FullName;}catch(ArgumentException){return "";}catch(NotSupportedException){return "";}catch(IOException){return "";}
  }
  public const long MaxHashBytes=2L*1024*1024*1024;
  public static readonly HashSet<string> Executable=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".exe",".com",".scr",".pif",".bat",".cmd",".ps1",".psm1",".vbs",".vbe",".js",".jse",".wsf",".wsh",".hta",".msi",".msp",".msix",".msixbundle",".appx",".appxbundle",".reg",".lnk",".url",".dll",".cpl",".sys",".jar",".application"};
  public static readonly HashSet<string> PortableExecutable=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".exe",".dll",".scr",".cpl",".sys",".ocx",".drv",".efi"};
  public static readonly HashSet<string> DocumentLike=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".pdf",".doc",".docx",".xls",".xlsx",".ppt",".pptx",".txt",".rtf",".odt",".csv",".jpg",".jpeg",".png",".gif",".bmp",".webp",".mp3",".wav",".mp4",".avi",".mkv",".mov",".zip",".rar",".7z",".html",".htm",".xml",".json"};
  static readonly char[] BidiControls={'\u202A','\u202B','\u202C','\u202D','\u202E','\u2066','\u2067','\u2068','\u2069'};

  /// <summary>Returns the document extension an executable hides behind ("hoadon.pdf.exe" → ".pdf"); empty when the name is honest.</summary>
  public static string DisguisedAs(string fileName){
   if(String.IsNullOrEmpty(fileName))return "";
   string last=System.IO.Path.GetExtension(fileName);if(!Executable.Contains(last))return "";
   string inner=System.IO.Path.GetExtension(System.IO.Path.GetFileNameWithoutExtension(fileName));
   return DocumentLike.Contains(inner)?inner.ToLowerInvariant():"";
  }
  /// <summary>True when the name carries Unicode bidirectional controls that can visually reverse the real extension.</summary>
  public static bool HasBidiOverride(string fileName){return !String.IsNullOrEmpty(fileName)&&fileName.IndexOfAny(BidiControls)>=0;}

  /// <summary>Reads the machine type of a PE image ("x86", "x64", "ARM64", "ARM", "PE") or "" when the stream is not a PE file.</summary>
  public static string PeArchitecture(Stream s){
   try{
    if(s==null||!s.CanSeek||s.Length<0x40)return "";
    var head=new byte[0x40];s.Position=0;if(Fill(s,head)<0x40||head[0]!=(byte)'M'||head[1]!=(byte)'Z')return "";
    int lfanew=BitConverter.ToInt32(head,0x3C);if(lfanew<=0||lfanew>s.Length-6)return "";
    var pe=new byte[6];s.Position=lfanew;if(Fill(s,pe)<6||pe[0]!=(byte)'P'||pe[1]!=(byte)'E'||pe[2]!=0||pe[3]!=0)return "";
    ushort machine=BitConverter.ToUInt16(pe,4);
    switch(machine){case 0x14c:return "x86";case 0x8664:return "x64";case 0xAA64:return "ARM64";case 0x1c0:case 0x1c4:return "ARM";default:return "PE";}
   }catch(IOException){return "";}catch(NotSupportedException){return "";}
  }
  public static string PeArchitecture(byte[] bytes){using(var ms=new MemoryStream(bytes??new byte[0]))return PeArchitecture(ms);}
  static int Fill(Stream s,byte[] buffer){int total=0;while(total<buffer.Length){int n=s.Read(buffer,total,buffer.Length-total);if(n<=0)break;total+=n;}return total;}

  /// <summary>Parses a Zone.Identifier stream ([ZoneTransfer] ZoneId=3, ReferrerUrl, HostUrl).</summary>
  public static bool ParseZone(string text,out int zoneId,out string referrer,out string host){
   zoneId=-1;referrer="";host="";if(String.IsNullOrWhiteSpace(text))return false;
   foreach(var raw in text.Split('\n')){
    string line=raw.Trim();int eq=line.IndexOf('=');if(eq<=0)continue;
    string key=line.Substring(0,eq).Trim(),value=line.Substring(eq+1).Trim();
    if(key.Equals("ZoneId",StringComparison.OrdinalIgnoreCase)){int z;if(Int32.TryParse(value,out z))zoneId=z;}
    else if(key.Equals("ReferrerUrl",StringComparison.OrdinalIgnoreCase))referrer=value;
    else if(key.Equals("HostUrl",StringComparison.OrdinalIgnoreCase))host=value;
   }
   return zoneId>=0;
  }
  /// <summary>Human label for a URL security zone id.</summary>
  public static string ZoneLabel(int zone){switch(zone){case 0:return L.T("Máy này");case 1:return L.T("Mạng nội bộ");case 2:return L.T("Trang tin cậy");case 3:return L.T("Internet");case 4:return L.T("Trang hạn chế");default:return L.T("không rõ");}}

  /// <summary>Derives the warning and note lists from the collected facts; pure so the rules are testable without files.</summary>
  public static void Warn(FileDetails d){
   if(d==null)return;d.Warnings.Clear();d.Notes.Clear();if(d.IsDirectory)return;
   bool exe=Executable.Contains(d.Extension);
   if(d.DisguisedAs!="")d.Warnings.Add(L.F("Đuôi kép: tệp thực thi {0} giả dạng tài liệu {1}. Không mở nếu không chắc nguồn gốc.",d.Extension.ToLowerInvariant(),d.DisguisedAs));
   if(d.Bidi)d.Warnings.Add(L.T("Tên tệp chứa ký tự đảo chiều Unicode — phần mở rộng hiển thị có thể không phải phần mở rộng thật."));
   if(exe&&d.FromInternet&&!d.Signed)d.Warnings.Add(L.T("Tệp thực thi tải từ Internet và chưa có chữ ký số: kiểm tra nguồn trước khi chạy."));
   if(d.Architecture!=""&&!PortableExecutable.Contains(d.Extension)&&!Executable.Contains(d.Extension))d.Warnings.Add(L.F("Nội dung là chương trình Windows (PE {0}) dù phần mở rộng là {1}.",d.Architecture,d.Extension==""?L.T("(không có)"):d.Extension));
   if(d.Architecture==""&&PortableExecutable.Contains(d.Extension)&&d.Bytes>0)d.Warnings.Add(L.F("Phần mở rộng {0} nhưng nội dung không phải tệp PE hợp lệ.",d.Extension.ToLowerInvariant()));
   if(d.FromInternet&&!(exe&&!d.Signed))d.Notes.Add(L.F("Tải từ vùng «{0}» (Mark of the Web): Windows còn cảnh báo khi mở.",ZoneLabel(d.ZoneId)));
   if(d.Hidden&&d.System)d.Notes.Add(L.T("Thuộc tính Ẩn + Hệ thống: Explorer chỉ hiện khi bật «Hiện tệp hệ thống được bảo vệ»."));
   else if(d.Hidden)d.Notes.Add(L.T("Thuộc tính Ẩn: Explorer chỉ hiện khi bật «Hiện tệp và thư mục ẩn»."));
   if(d.ReparsePoint)d.Notes.Add(L.T("Liên kết (symlink / junction): nội dung thật nằm ở nơi khác."));
   if(d.ReadOnly)d.Notes.Add(L.T("Chỉ đọc: cần bỏ thuộc tính trước khi sửa hoặc xóa."));
   if(exe&&d.Signed&&d.Publisher!="")d.Notes.Add(L.F("Chữ ký số hợp lệ của {0}.",d.Publisher));
  }

  /// <summary>Collects the details of one path; hashing is optional, cancellable and skipped above MaxHashBytes.</summary>
  public static FileDetails Inspect(string path,bool hash,CancellationToken token,Action<string> progress=null){
   if(String.IsNullOrWhiteSpace(path))throw new IOException(L.T("Đường dẫn trống."));
   path=path.Trim().Trim('"');
   var d=new FileDetails{Path=path,Name=System.IO.Path.GetFileName(path)};
   if(Directory.Exists(path)){var di=new DirectoryInfo(path);d.Exists=true;d.IsDirectory=true;d.Attributes=di.Attributes;d.Created=di.CreationTime;d.Modified=di.LastWriteTime;d.Accessed=di.LastAccessTime;d.TypeName=L.T("Thư mục");d.Owner=OwnerOf(path);return d;}
   if(!File.Exists(path))throw new FileNotFoundException(L.T("Tệp không còn tồn tại: ")+path);
   var fi=new FileInfo(path);d.Exists=true;d.Bytes=fi.Length;d.Attributes=fi.Attributes;d.Created=fi.CreationTime;d.Modified=fi.LastWriteTime;d.Accessed=fi.LastAccessTime;
   d.Extension=fi.Extension??"";d.DisguisedAs=DisguisedAs(d.Name);d.Bidi=HasBidiOverride(d.Name);
   Report(progress,"Đang đọc loại tệp…");d.TypeName=TypeNameOf(path,d.Extension);
   token.ThrowIfCancellationRequested();
   if(!d.ReparsePoint){try{using(var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))d.Architecture=PeArchitecture(s);}catch(IOException){}catch(UnauthorizedAccessException){}}
   try{var v=FileVersionInfo.GetVersionInfo(path);d.FileDescription=v.FileDescription??"";d.ProductName=v.ProductName??"";d.FileVersion=v.FileVersion??"";d.Company=v.CompanyName??"";}catch(Exception){}
   if(d.Architecture!=""||Executable.Contains(d.Extension)){Report(progress,"Đang kiểm tra chữ ký số…");string signer=StubbornFiles.IsWindows?Network.ProcessResolver.PublisherOf(path):"";d.Signed=signer!=""&&signer!="Không ký";d.Publisher=d.Signed?signer:"";}
   d.Owner=OwnerOf(path);
   string zone=ReadZone(path);int zoneId;string referrer,host;
   if(ParseZone(zone,out zoneId,out referrer,out host)){d.ZoneId=zoneId;d.FromInternet=zoneId>=3;d.ReferrerUrl=referrer;d.HostUrl=host;}
   token.ThrowIfCancellationRequested();
   if(hash){if(d.Bytes>MaxHashBytes)d.HashSkipped=true;else Hash(d,token,progress);}
   Warn(d);
   return d;
  }

  static void Report(Action<string> progress,string stage){if(progress!=null)progress(L.T(stage));}

  static void Hash(FileDetails d,CancellationToken token,Action<string> progress){
   try{
    using(var sha=SHA256.Create())using(var md5=MD5.Create())using(var s=new FileStream(d.Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,1<<16)){
     var buffer=new byte[1<<20];long done=0;int n;var last=DateTime.UtcNow;
     while((n=s.Read(buffer,0,buffer.Length))>0){
      token.ThrowIfCancellationRequested();
      sha.TransformBlock(buffer,0,n,null,0);md5.TransformBlock(buffer,0,n,null,0);done+=n;
      if(progress!=null&&(DateTime.UtcNow-last).TotalMilliseconds>200){last=DateTime.UtcNow;progress(L.F("Đang băm… {0}%",d.Bytes>0?done*100/d.Bytes:100));}
     }
     sha.TransformFinalBlock(buffer,0,0);md5.TransformFinalBlock(buffer,0,0);
     d.Sha256=Hex(sha.Hash);d.Md5=Hex(md5.Hash);
    }
   }catch(IOException e){d.Notes.Add(L.T("Không băm được: ")+e.Message);}catch(UnauthorizedAccessException e){d.Notes.Add(L.T("Không băm được: ")+e.Message);}
  }
  public static string Hex(byte[] bytes){var sb=new StringBuilder(bytes.Length*2);foreach(byte b in bytes)sb.Append(b.ToString("x2"));return sb.ToString();}

  /// <summary>Reads the Zone.Identifier alternate data stream; empty when absent or unsupported (non-NTFS, Mono).</summary>
  public static string ReadZone(string path){
   if(!StubbornFiles.IsWindows)return "";
   try{using(var s=new FileStream(path+":Zone.Identifier",FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var r=new StreamReader(s,Encoding.Default,true))return r.ReadToEnd();}
   catch(FileNotFoundException){return "";}catch(IOException){return "";}catch(UnauthorizedAccessException){return "";}catch(NotSupportedException){return "";}catch(ArgumentException){return "";}
  }

  static string OwnerOf(string path){
   if(!StubbornFiles.IsWindows)return "";
   try{var owner=File.GetAccessControl(path).GetOwner(typeof(NTAccount));return owner==null?"":owner.Value;}
   catch(Exception){try{var sid=File.GetAccessControl(path).GetOwner(typeof(SecurityIdentifier));return sid==null?"":sid.Value;}catch(Exception){return "";}}
  }

  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHGetFileInfo(string path,uint attributes,ref ShFileInfo info,uint size,uint flags);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct ShFileInfo{public IntPtr hIcon;public int iIcon;public uint dwAttributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string szDisplayName;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string szTypeName;}
  const uint ShgfiTypeName=0x400,ShgfiUseFileAttributes=0x10,FileAttributeNormal=0x80,ShgfiIcon=0x100,ShgfiSmallIcon=0x1,ShgfiLargeIcon=0x0,FileAttributeDirectory=0x10;
  [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr handle);

  /// <summary>Shell icon for a folder, drive or file; folders and known extensions resolve by attributes only, so browsing never opens the files themselves.</summary>
  public static System.Drawing.Bitmap ShellIcon(FolderEntry e,int size){
   if(e==null||!StubbornFiles.IsWindows)return null;
   var info=new ShFileInfo();uint flags=ShgfiIcon|(size<=16?ShgfiSmallIcon:ShgfiLargeIcon);
   try{
    bool byAttributes=e.IsDirectory&&!e.IsDrive||(!e.IsDirectory&&e.Extension!=""&&!PortableExecutable.Contains(e.Extension)&&!e.Extension.Equals(".ico",StringComparison.OrdinalIgnoreCase)&&!e.Extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase));
    if(byAttributes)SHGetFileInfo(e.IsDirectory?"folder":e.Extension,e.IsDirectory?FileAttributeDirectory:FileAttributeNormal,ref info,(uint)Marshal.SizeOf(typeof(ShFileInfo)),flags|ShgfiUseFileAttributes);
    else SHGetFileInfo(e.Path,0,ref info,(uint)Marshal.SizeOf(typeof(ShFileInfo)),flags);
    if(info.hIcon==IntPtr.Zero)return null;
    using(var icon=(System.Drawing.Icon)System.Drawing.Icon.FromHandle(info.hIcon).Clone())return icon.ToBitmap();
   }catch(Exception){return null;}
   finally{if(info.hIcon!=IntPtr.Zero)DestroyIcon(info.hIcon);}
  }

  /// <summary>Shell type description ("PDF Document", "Ứng dụng"); falls back to the extension when the shell cannot answer.</summary>
  public static string TypeNameOf(string path,string extension){
   if(StubbornFiles.IsWindows){
    try{var info=new ShFileInfo();SHGetFileInfo(path,FileAttributeNormal,ref info,(uint)Marshal.SizeOf(typeof(ShFileInfo)),ShgfiTypeName|ShgfiUseFileAttributes);if(!String.IsNullOrWhiteSpace(info.szTypeName))return info.szTypeName;}
    catch(Exception){}
   }
   return extension==""?L.T("Tệp không có phần mở rộng"):L.F("Tệp {0}",extension.TrimStart('.').ToUpperInvariant());
  }

  /// <summary>Rows for the details list, grouped and localized; warnings come first so they are never scrolled out of view.</summary>
  public static List<DetailRow> Rows(FileDetails d){
   var rows=new List<DetailRow>();if(d==null)return rows;
   string warn=L.T("Cảnh báo an toàn"),general=L.T("Chung"),time=L.T("Thời gian"),security=L.T("Bảo mật"),version=L.T("Phiên bản tệp"),hashes=L.T("Mã băm");
   foreach(var w in d.Warnings)rows.Add(new DetailRow{Group=warn,Label=L.T("Cảnh báo an toàn"),Value=w,Kind=RowKind.Warning});
   foreach(var n in d.Notes)rows.Add(new DetailRow{Group=warn,Label=L.T("Ghi chú"),Value=n,Kind=RowKind.Note});
   rows.Add(new DetailRow{Group=general,Label=L.T("Tên"),Value=d.Name});
   rows.Add(new DetailRow{Group=general,Label=L.T("Đường dẫn"),Value=d.Path});
   rows.Add(new DetailRow{Group=general,Label=L.T("Loại"),Value=d.TypeName+(d.IsDirectory?"":d.Extension==""?"  ("+L.T("không có phần mở rộng")+")":"  ("+d.Extension.ToLowerInvariant()+")")});
   if(!d.IsDirectory)rows.Add(new DetailRow{Group=general,Label=L.T("Dung lượng"),Value=Presentation.BytesLabel(d.Bytes)+"  ("+d.Bytes.ToString("N0")+" bytes)"});
   rows.Add(new DetailRow{Group=general,Label=L.T("Thuộc tính"),Value=AttributeLabel(d.Attributes)});
   if(d.Architecture!="")rows.Add(new DetailRow{Group=general,Label=L.T("Kiến trúc"),Value=d.Architecture});
   rows.Add(new DetailRow{Group=time,Label=L.T("Tạo"),Value=Stamp(d.Created)});
   rows.Add(new DetailRow{Group=time,Label=L.T("Sửa lần cuối"),Value=Stamp(d.Modified)});
   rows.Add(new DetailRow{Group=time,Label=L.T("Truy cập lần cuối"),Value=Stamp(d.Accessed)});
   if(d.Owner!="")rows.Add(new DetailRow{Group=security,Label=L.T("Chủ sở hữu"),Value=d.Owner});
   if(!d.IsDirectory)rows.Add(new DetailRow{Group=security,Label=L.T("Chữ ký"),Value=d.Signed?d.Publisher:Executable.Contains(d.Extension)||d.Architecture!=""?L.T("Không ký"):L.T("không áp dụng")});
   if(d.ZoneId>=0)rows.Add(new DetailRow{Group=security,Label=L.T("Nguồn tải"),Value=ZoneLabel(d.ZoneId)+(d.HostUrl!=""?"  •  "+d.HostUrl:"")+(d.ReferrerUrl!=""?"  •  "+L.T("từ")+" "+d.ReferrerUrl:"")});
   else if(!d.IsDirectory)rows.Add(new DetailRow{Group=security,Label=L.T("Nguồn tải"),Value=L.T("Không có dấu tải từ Internet")});
   if(d.FileDescription!="")rows.Add(new DetailRow{Group=version,Label=L.T("Mô tả"),Value=d.FileDescription});
   if(d.ProductName!="")rows.Add(new DetailRow{Group=version,Label=L.T("Sản phẩm"),Value=d.ProductName});
   if(d.FileVersion!="")rows.Add(new DetailRow{Group=version,Label=L.T("Phiên bản"),Value=d.FileVersion});
   if(d.Company!="")rows.Add(new DetailRow{Group=version,Label=L.T("Công ty"),Value=d.Company});
   if(d.Sha256!="")rows.Add(new DetailRow{Group=hashes,Label="SHA-256",Value=d.Sha256});
   if(d.Md5!="")rows.Add(new DetailRow{Group=hashes,Label="MD5",Value=d.Md5});
   if(d.HashSkipped)rows.Add(new DetailRow{Group=hashes,Label=L.T("Mã băm"),Value=L.T("Bỏ qua: tệp lớn hơn 2 GB."),Kind=RowKind.Note});
   return rows;
  }

  static string Stamp(DateTime t){return t==DateTime.MinValue?"—":t.ToString("dd/MM/yyyy HH:mm:ss");}
  /// <summary>Readable attribute list ("Ẩn, Hệ thống, Chỉ đọc") or "Bình thường".</summary>
  public static string AttributeLabel(FileAttributes a){
   var parts=new List<string>();
   if((a&FileAttributes.Hidden)!=0)parts.Add(L.T("Ẩn"));if((a&FileAttributes.System)!=0)parts.Add(L.T("Hệ thống"));if((a&FileAttributes.ReadOnly)!=0)parts.Add(L.T("Chỉ đọc"));
   if((a&FileAttributes.ReparsePoint)!=0)parts.Add(L.T("Liên kết"));if((a&FileAttributes.Compressed)!=0)parts.Add(L.T("Nén NTFS"));if((a&FileAttributes.Encrypted)!=0)parts.Add(L.T("Mã hóa EFS"));
   if((a&FileAttributes.Archive)!=0)parts.Add(L.T("Lưu trữ"));if((a&FileAttributes.Temporary)!=0)parts.Add(L.T("Tạm"));if((a&FileAttributes.Offline)!=0)parts.Add(L.T("Ngoại tuyến"));
   return parts.Count==0?L.T("Bình thường"):String.Join(", ",parts);
  }

  /// <summary>Plain-text report for the clipboard.</summary>
  public static string Report(FileDetails d){
   var sb=new StringBuilder();sb.AppendLine("Tweek Pro — "+L.T("Chi tiết tệp"));string group=null;
   foreach(var r in Rows(d)){if(r.Group!=group){group=r.Group;sb.AppendLine();sb.AppendLine("["+group+"]");}sb.AppendLine(r.Label+": "+r.Value);}
   return sb.ToString();
  }
 }
}
