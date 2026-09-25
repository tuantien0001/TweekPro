using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace TweekPro {
 public class ScanResult {
  public List<Candidate> Items=new List<Candidate>(); public List<string> Notes=new List<string>(); public int Visited;
 }
 /// <summary>Snapshot of deep-scan progress reported from the worker thread to the UI.</summary>
 public class ScanProgress {
  public string Stage, Current; public int Visited, Found;
 }
 /// <summary>Measured on-disk footprint of a leftover candidate; Bytes is negative when not applicable.</summary>
 public class Footprint {
  public long Bytes=-1; public int Files; public bool Partial; public string Detail="";
 }
 public class AutorunEntry {
  public string Name,Command,Source,Publisher,State; public Candidate Item; public Backup Saved; public Startup.StartupInsight Insight;
 }
 public static class Advanced {
  public const string Run=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
  public const string RunOnce=@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
  public const string Compat=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store";
  public const string Layers=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
  public const string AppPaths=@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
  public static string[] Views {get{return Environment.Is64BitOperatingSystem?new[]{"64","32"}:new[]{"32"};}}
  public static string Id(Candidate c){return c.Kind+"|"+c.Hive+"|"+c.View+"|"+c.Path+"|"+c.ValueName;}
  public static string CommandExe(string command){
   if(String.IsNullOrWhiteSpace(command))return "";string c=Environment.ExpandEnvironmentVariables(command.Trim());
   if(c.StartsWith("\"")){int end=c.IndexOf('"',1);return end>1?c.Substring(1,end-1):"";}
   var m=Regex.Match(c,@"^(.+?\.(?:exe|com))(?:\s|$)",RegexOptions.IgnoreCase);return m.Success?m.Groups[1].Value:"";
  }
  public static bool LocalPath(string p){return !String.IsNullOrEmpty(p)&&p.Length>2&&p[1]==':'&&!p.StartsWith(@"\\")&&Path.IsPathRooted(p);}
  public static string[] ShortcutRoots(){return new[]{Environment.GetFolderPath(Environment.SpecialFolder.Startup),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}.Where(p=>!String.IsNullOrWhiteSpace(p)).Select(Engine.Canon).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
  public static string[] StartupRoots(){return new[]{Environment.GetFolderPath(Environment.SpecialFolder.Startup),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)}.Where(p=>!String.IsNullOrWhiteSpace(p)).Select(Engine.Canon).ToArray();}
  public static void ValidateFile(string path){
   string p=Engine.Canon(path);if(!Path.GetExtension(p).Equals(".lnk",StringComparison.OrdinalIgnoreCase)||!ShortcutRoots().Any(r=>Engine.Under(p,r)))throw new IOException("Chỉ xử lý shortcut .lnk trong Start Menu, Desktop và Startup.");Engine.NoLinks(p,false);
  }
  public static void ValidateValue(string key){if(!new[]{Run,RunOnce,Compat,Layers}.Concat(Remnants.TraceHunter.ValueKeys).Contains(key,StringComparer.OrdinalIgnoreCase))throw new IOException("Vị trí giá trị Registry không được hỗ trợ.");}
  public static string Fingerprint(RegValue value){using(var ms=new MemoryStream()){new XmlSerializer(typeof(RegValue)).Serialize(ms,value);using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(ms.ToArray()));}}
  public static RegValue ReadValue(RegistryKey key,string name){
   if(!key.GetValueNames().Contains(name,StringComparer.OrdinalIgnoreCase))throw new IOException("Giá trị không còn tồn tại.");
   var r=new RegValue{Name=name,Kind=key.GetValueKind(name)};object value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
   if(r.Kind==RegistryValueKind.Binary||r.Kind==RegistryValueKind.None)r.Bytes=(byte[])value;
   else if(r.Kind==RegistryValueKind.MultiString)r.Texts=(string[])value;
   else r.Text=Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);
   return r;
  }
  public static string FileHash(string path){using(var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(stream));}
  public static string ShortcutTarget(string path){
   object shell=null,shortcut=null;try{
    Engine.NoLinks(path,false);if(((int)File.GetAttributes(path)&(0x1000|0x40000|0x400000))!=0)return "";
    shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic s=shell;shortcut=s.CreateShortcut(path);dynamic link=shortcut;return Convert.ToString(link.TargetPath);
   }catch(Exception){return "";}finally{if(shortcut!=null&&Marshal.IsComObject(shortcut))Marshal.FinalReleaseComObject(shortcut);if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}
  }
  public static bool MatchesPath(AppEntry app,string path){
   try{
    if(!LocalPath(path))return false;string p=Engine.Canon(path);
    if(app.KnownExecutables!=null&&app.KnownExecutables.Any(e=>String.Equals(e,p,StringComparison.OrdinalIgnoreCase)))return true;
    if(!String.IsNullOrWhiteSpace(app.Location)){string root=Engine.Canon(app.Location);Engine.ValidateFolder(root,false);return Engine.Under(p,root);}
   }catch(Exception){}return false;
  }
  public static void Capture(AppEntry app){
   if(app.KnownExecutables==null)app.KnownExecutables=new List<string>();
   string icon;int index;
   if(Presentation.ParseIcon(app.DisplayIcon,out icon,out index)&&Path.GetExtension(icon).Equals(".exe",StringComparison.OrdinalIgnoreCase)){
    try{Engine.ValidateFolder(Path.GetDirectoryName(icon),false);app.KnownExecutables.Add(Engine.Canon(icon));}catch(Exception){}
   }
   if(!String.IsNullOrWhiteSpace(app.Location))try{
    string root=Engine.Canon(app.Location);Engine.ValidateFolder(root,false);
    if(Directory.Exists(root))foreach(string exe in Directory.EnumerateFiles(root,"*.exe",SearchOption.TopDirectoryOnly).Take(200)){Engine.NoLinks(exe,false);app.KnownExecutables.Add(Engine.Canon(exe));}
   }catch(Exception){}
   app.KnownExecutables=app.KnownExecutables.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }
  /// <summary>Run/RunOnce values, Startup shortcuts, packaged startup tasks, vault copies and (unless includeServices is false) automatic services; the health probe skips services because listing them means a WMI query plus a signature check per third-party service.</summary>
  public static List<AutorunEntry> Autoruns(bool includeServices=true){
   var items=new List<AutorunEntry>();
   foreach(string h in new[]{"HKCU","HKLM"})foreach(string v in Views)foreach(string path in new[]{Run,RunOnce})try{
    using(var b=Engine.Base(h,v))using(var key=b.OpenSubKey(path)){
     if(key==null)continue;foreach(string name in key.GetValueNames()){
      var value=ReadValue(key,name);if(value.Kind!=RegistryValueKind.String&&value.Kind!=RegistryValueKind.ExpandString)continue;
      var c=new Candidate{Kind="RegistryValue",Path=path,Hive=h,View=v,ValueName=name,ExpectedHash=Fingerprint(value),AppName=name,Reason="Mục khởi động đã đăng ký."};
      items.Add(new AutorunEntry{Name=name,Command=value.Text,Source=h+" / "+v+" / "+Path.GetFileName(path),State="Có đăng ký",Item=c});
     }
    }
   }catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
   foreach(string root in StartupRoots())try{
    if(!Directory.Exists(root))continue;Engine.NoLinks(root,false);
    foreach(string file in Directory.GetFiles(root,"*.lnk")){ValidateFile(file);items.Add(new AutorunEntry{Name=Path.GetFileName(file),Command=ShortcutTarget(file),Source=root,State="Có đăng ký",Item=new Candidate{Kind="File",Path=file,AppName=Path.GetFileName(file),ExpectedHash=FileHash(file),Reason="Shortcut Startup."}});}
   }catch(IOException){}catch(UnauthorizedAccessException){}
   // HKCU Software may be shared across registry views; suppress duplicate commands.
   items=items.GroupBy(x=>x.Item.Kind+"|"+x.Item.Hive+"|"+x.Item.Path+"|"+x.Item.ValueName+"|"+x.Command,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
   Startup.StartupSources.Append(items,includeServices);
   foreach(var backup in Engine.Backups().Where(b=>b.Purpose=="Autorun"&&b.State!="Restored"&&b.Kind!="StartupTask"))items.Add(new AutorunEntry{Name=backup.AppName,Command=backup.Original+(backup.ValueName==null?"":" :: "+backup.ValueName),Source="Kho Tweek Pro",State=backup.State=="BackedUp"?"Đã tắt bằng Tweek Pro":"Cần kiểm tra",Saved=backup});
   return items.OrderBy(x=>x.Name).ToList();
  }
  public static Backup Store(Candidate c,bool autorun){
   if(c.ReviewOnly)throw new IOException("Mục này chỉ để kiểm tra, không tự dọn.");
   if(!autorun&&Engine.Installed(c.AppId))throw new IOException("Ứng dụng vẫn còn đăng ký cài đặt.");
   if(c.Kind=="File"){ValidateFile(c.Path);if(autorun&&!StartupRoots().Any(r=>Engine.Under(Engine.Canon(c.Path),r)))throw new IOException("Không phải shortcut Startup.");}
   else {ValidateValue(c.Path);if(autorun&&c.Path!=Run&&c.Path!=RunOnce)throw new IOException("Không phải Run/RunOnce.");}
   Engine.NoLinks(Engine.Vault,false);
   var b=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Original=c.Path,Kind=c.Kind,Hive=c.Hive,View=c.View,AppName=c.AppName,ValueName=c.ValueName,Purpose=autorun?"Autorun":"Cleanup",Payload=c.Kind=="File"?"content":"value.xml"};
   Directory.CreateDirectory(Path.Combine(Engine.Vault,b.Id));Engine.SaveBackup(b);
   try{
    string payload=Path.Combine(Engine.Vault,b.Id,b.Payload);
    if(c.Kind=="File"){
     if(c.ExpectedHash!=FileHash(c.Path))throw new IOException("Shortcut đã thay đổi sau khi quét; hãy tải lại.");
     if(Path.GetPathRoot(c.Path)!=Path.GetPathRoot(payload))throw new IOException("Kho khôi phục và shortcut phải cùng ổ đĩa.");
     Core.StubbornFiles.Escalate(c.Path,()=>File.Move(c.Path,payload));
    }else using(var root=Engine.Base(c.Hive,c.View))using(var key=root.OpenSubKey(c.Path,true)){
     if(key==null)throw new IOException("Không mở được khóa.");var value=ReadValue(key,c.ValueName);
     if(c.ExpectedHash!=Fingerprint(value))throw new IOException("Giá trị đã thay đổi sau khi quét; hãy tải lại.");
     Engine.Save(payload,value);var verified=Engine.Load<RegValue>(payload);if(Fingerprint(verified)!=c.ExpectedHash)throw new IOException("Bản sao lưu không khớp.");
     if(Fingerprint(ReadValue(key,c.ValueName))!=c.ExpectedHash)throw new IOException("Giá trị vừa thay đổi; không xóa.");key.DeleteValue(c.ValueName,true);
    }
    b.State="BackedUp";Engine.SaveBackup(b);return b;
   }catch(Exception e){b.State="NeedsReview";b.Error=e.Message;Engine.SaveBackup(b);throw;}
  }
  public static void Restore(Backup b){
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   string payload=Path.Combine(Engine.VaultOf(b),b.Id,b.Kind=="File"?"content":"value.xml");Engine.NoLinks(payload,false);
   if(b.Kind=="File"){
    ValidateFile(b.Original);if(File.Exists(b.Original)||Directory.Exists(b.Original))throw new IOException("Đích đã tồn tại; không ghi đè.");
    Directory.CreateDirectory(Path.GetDirectoryName(b.Original));Engine.NoLinks(b.Original,false);File.Move(payload,b.Original);
   }else{
    ValidateValue(b.Original);RegValue value=Engine.Load<RegValue>(payload);if(value.Name!=b.ValueName)throw new IOException("Bản sao lưu không khớp tên giá trị.");
    using(var root=Engine.Base(b.Hive,b.View))using(var key=root.CreateSubKey(b.Original)){
     if(key.GetValueNames().Contains(value.Name,StringComparer.OrdinalIgnoreCase))throw new IOException("Giá trị đã tồn tại; không ghi đè.");
     var node=new RegNode();node.Values.Add(value);Engine.WriteTree(key,node);
    }
   }
   b.State="Restored";b.Error="";Engine.SaveBackup(b);
  }
  static HashSet<string> Names(AppEntry app){
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);if(Engine.SafeName(app.Name))names.Add(app.Name);
   if(!String.IsNullOrEmpty(app.Location))try{Engine.ValidateFolder(app.Location,false);string leaf=Path.GetFileName(Engine.Canon(app.Location));if(Engine.SafeName(leaf)&&!String.Equals(leaf,app.Publisher,StringComparison.OrdinalIgnoreCase))names.Add(leaf);}catch(Exception){}
   if(app.KnownExecutables!=null)foreach(string exe in app.KnownExecutables)try{
    string dir=Path.GetDirectoryName(exe);if(String.IsNullOrWhiteSpace(dir))continue;Engine.ValidateFolder(dir,false);
    string leaf=Path.GetFileName(Engine.Canon(dir));if(Engine.SafeName(leaf)&&!String.Equals(leaf,app.Publisher,StringComparison.OrdinalIgnoreCase))names.Add(leaf);
   }catch(Exception){}
   return names;
  }
  /// <summary>Adds exact name and publisher\app children of one folder without walking the rest of the tree.</summary>
  static void AddFingerprintChildren(string parent,HashSet<string> names,string publisher,ScanResult result,string reason){
   if(String.IsNullOrWhiteSpace(parent)||!Directory.Exists(parent))return;
   foreach(string n in names){
    TryFolder(Path.Combine(parent,n),result,reason);
    if(Engine.SafeName(publisher))TryFolder(Path.Combine(parent,publisher,n),result,reason);
   }
  }
  static void TryFolder(string path,ScanResult result,string reason){
   try{Engine.ValidateFolder(path,false);if(Directory.Exists(path))result.Items.Add(new Candidate{Kind="Folder",Path=Engine.Canon(path),Reason=reason});}catch(IOException){}catch(ArgumentException){}catch(UnauthorizedAccessException){}
  }

  static void Release(object o){if(o!=null&&Marshal.IsComObject(o))Marshal.FinalReleaseComObject(o);}
  static void ScanTasks(AppEntry identity,ScanResult result,System.Threading.CancellationToken cancel){
   object service=null;var folders=new Queue<string>();folders.Enqueue("\\");int count=0;var time=Stopwatch.StartNew();
   try{
    service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));dynamic scheduler=service;scheduler.Connect();
    while(folders.Count>0&&count<3000&&time.Elapsed.TotalSeconds<15){
     cancel.ThrowIfCancellationRequested();string path=folders.Dequeue();object folder=null,tasks=null,children=null;
     try{
      folder=scheduler.GetFolder(path);dynamic f=folder;tasks=f.GetTasks(1);dynamic collection=tasks;
      for(int i=1;i<=collection.Count&&count<3000;i++){
       cancel.ThrowIfCancellationRequested();count++;object task=null,definition=null,actions=null;
       try{
        task=collection[i];dynamic t=task;definition=t.Definition;dynamic d=definition;actions=d.Actions;dynamic acts=actions;
        for(int j=1;j<=acts.Count;j++){object action=null;try{action=acts[j];dynamic act=action;if((int)act.Type==0&&MatchesPath(identity,Convert.ToString(act.Path))){result.Items.Add(new Candidate{Kind="Review",Path="Task: "+Convert.ToString(t.Path),ReviewOnly=true,Reason="Tác vụ theo lịch tham chiếu executable của ứng dụng; kiểm tra bằng Task Scheduler."});break;}}finally{Release(action);}}
       }catch(COMException){result.Notes.Add("Không đọc được một tác vụ trong "+path);}finally{Release(actions);Release(definition);Release(task);}
      }
      children=f.GetFolders(0);dynamic sub=children;for(int i=1;i<=sub.Count;i++){object child=null;try{child=sub[i];dynamic c=child;folders.Enqueue(Convert.ToString(c.Path));}finally{Release(child);}}
     }catch(COMException){result.Notes.Add("Không đọc được thư mục Task Scheduler: "+path);}finally{Release(children);Release(tasks);Release(folder);}
    }
    if(folders.Count>0||count>=3000)result.Notes.Add("Đã chạm giới hạn 3.000 tác vụ / 15 giây; kiểm tra thêm trong Task Scheduler.");
   }catch(COMException){result.Notes.Add("Không kết nối được Task Scheduler.");}finally{Release(service);}
  }
  static void ScanVendorRegistry(AppEntry app,HashSet<string> names,ScanResult result,System.Threading.CancellationToken cancel){
   if(!Engine.SafeName(app.Publisher))return;
   string basePath="SOFTWARE\\"+app.Publisher;try{Engine.ValidateRegistry(basePath);}catch(IOException){return;}
   foreach(string h in new[]{"HKCU","HKLM"})foreach(string v in Views){
    var queue=new Queue<Tuple<string,int>>();queue.Enqueue(Tuple.Create(basePath,0));int count=0;
    using(var root=Engine.Base(h,v))while(queue.Count>0&&count<6000){
     cancel.ThrowIfCancellationRequested();var current=queue.Dequeue();count++;
     try{using(var key=root.OpenSubKey(current.Item1)){
      if(key==null)continue;string leaf=current.Item1.Substring(current.Item1.LastIndexOf('\\')+1);
      if(current.Item2>0&&names.Contains(leaf)){
       bool review=current.Item1.Split('\\').Length>3;
       result.Items.Add(new Candidate{Kind=review?"Review":"Registry",Path=current.Item1,Hive=h,View=v,ReviewOnly=review,Reason=review?"Khóa lồng sâu trong nhánh nhà phát hành trùng tên ứng dụng; chỉ xem, chưa tự dọn.":"Khóa riêng dưới nhánh nhà phát hành trùng tên ứng dụng; cần duyệt."});continue;
      }
      if(current.Item2<5)foreach(string sub in key.GetSubKeyNames())queue.Enqueue(Tuple.Create(current.Item1+"\\"+sub,current.Item2+1));
     }}catch(UnauthorizedAccessException){result.Notes.Add("Không đọc được khóa: "+h+"\\"+current.Item1);}catch(System.Security.SecurityException){result.Notes.Add("Không đủ quyền khóa: "+current.Item1);}
    }
    if(queue.Count>0)result.Notes.Add("Đã chạm giới hạn khóa trong nhánh "+basePath);
   }
  }
  /// <summary>Runs the bounded deep scan for one application, optionally reporting stage and folder progress.</summary>
  public static ScanResult DeepScan(AppEntry app,System.Threading.CancellationToken cancel,string[] folderRoots=null,Action<ScanProgress> progress=null){
   var result=new ScanResult();
   Action<string,string> report=(stage,current)=>{if(progress!=null)progress(new ScanProgress{Stage=stage,Current=current,Visited=result.Visited,Found=result.Items.Count});};
   report("Đang đối chiếu thông tin cài đặt đã ghi nhận…",null);
   result.Items.AddRange(Engine.Scan(app));
   // Exclude broad shared install roots before correlating executable paths.
   var others=Engine.Inventory().Where(a=>a.Id!=app.Id).ToList();
   var identity=new AppEntry{Name=app.Name,Location=app.Location,KnownExecutables=(app.KnownExecutables??new List<string>()).ToList()};
   if(!String.IsNullOrWhiteSpace(identity.Location))foreach(var other in others)try{if(!String.IsNullOrWhiteSpace(other.Location)&&Engine.Overlap(Engine.Canon(identity.Location),Engine.Canon(other.Location))){identity.Location="";result.Notes.Add("Không dùng thư mục cài chung làm bằng chứng: "+app.Location);break;}}catch(ArgumentException){}
   identity.KnownExecutables=identity.KnownExecutables.Where(exe=>!others.Any(other=>{string icon;int i;return Presentation.ParseIcon(other.DisplayIcon,out icon,out i)&&String.Equals(icon,exe,StringComparison.OrdinalIgnoreCase);})).ToList();var names=Names(app);var watch=Stopwatch.StartNew();
   var variants=Remnants.TraceHunter.NameVariants(app);var weakVariants=new HashSet<string>(variants.Where(v=>!Remnants.TraceHunter.Strong(v)),StringComparer.OrdinalIgnoreCase);foreach(string v in variants)if(Remnants.TraceHunter.Strong(v))names.Add(v);
   if(app.KnownExecutables!=null)foreach(string exe in app.KnownExecutables)try{string dir=Path.GetDirectoryName(exe);if(!String.IsNullOrWhiteSpace(dir))TryFolder(dir,result,"Thư mục chứa executable đã ghi nhận trước khi gỡ; cần duyệt nội dung.");}catch(ArgumentException){}
   foreach(string temp in Engine.TempRoots())AddFingerprintChildren(temp,names,app.Publisher,result,"Thư mục tạm trùng tên ứng dụng hoặc thư mục cài; chỉ đưa vào khi khớp dấu vân tay, không quét toàn bộ Temp.");
   var scanRoots=(folderRoots??Engine.ScanRoots()).Where(Directory.Exists).Select(Engine.Canon).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   var rootSet=new HashSet<string>(scanRoots,StringComparer.OrdinalIgnoreCase);
   foreach(string root in scanRoots){
    var queue=new Queue<Tuple<string,int>>();queue.Enqueue(Tuple.Create(root,0));
    report("Đang duyệt thư mục trong "+root,root);
    while(queue.Count>0){
     cancel.ThrowIfCancellationRequested();if(result.Visited>=30000||watch.Elapsed.TotalSeconds>45){result.Notes.Add("Đã chạm giới hạn 30.000 thư mục / 45 giây; kết quả có thể chưa đủ.");queue.Clear();break;}
     var current=queue.Dequeue();try{
      Engine.NoLinks(current.Item1,false);
      foreach(string dir in Directory.EnumerateDirectories(current.Item1)){
       cancel.ThrowIfCancellationRequested();if(result.Visited>=30000||watch.Elapsed.TotalSeconds>45)break;result.Visited++;
       if(result.Visited%150==0)report("Đang duyệt thư mục trong "+root,dir);
       if((File.GetAttributes(dir)&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0)continue;
       string leaf=Path.GetFileName(dir);
       if(new[]{"Microsoft","Windows","Common Files","Packages",Core.Paths.ProductFolder,Core.Paths.LegacyFolder,"node_modules",".git"}.Contains(leaf,StringComparer.OrdinalIgnoreCase))continue;
       if(String.Equals(leaf,"Temp",StringComparison.OrdinalIgnoreCase)){
        AddFingerprintChildren(dir,names,app.Publisher,result,"Thư mục tạm trùng tên ứng dụng hoặc thư mục cài; chỉ đưa vào khi khớp dấu vân tay, không quét toàn bộ Temp.");
        continue;
       }
       if(names.Contains(leaf)){
        TryFolder(dir,result,"Quét sâu: tên thư mục trùng chính xác tên ứng dụng hoặc thư mục cài; cần duyệt dữ liệu.");
        continue;
       }
       if(weakVariants.Contains(leaf)&&current.Item2>0){result.Items.Add(new Candidate{Kind="Review",Path=dir,ReviewOnly=true,Reason="Tên thư mục trùng tên rút gọn «"+leaf+"» của ứng dụng (có thể thuộc phần mềm khác); chỉ xem."});continue;}
       string canonDir;try{canonDir=Engine.Canon(dir);}catch(ArgumentException){continue;}
       if(rootSet.Contains(canonDir))continue;
       if(current.Item2<5)queue.Enqueue(Tuple.Create(dir,current.Item2+1));
      }
     }catch(UnauthorizedAccessException){result.Notes.Add("Không đủ quyền đọc: "+current.Item1);}catch(IOException){result.Notes.Add("Bỏ qua đường dẫn không đọc được: "+current.Item1);}
    }
    if(result.Visited>=30000||watch.Elapsed.TotalSeconds>45)break;
   }
   report("Đang kiểm tra Run, RunOnce và AppCompatFlags…",null);
   foreach(string h in new[]{"HKCU","HKLM"})foreach(string v in Views)foreach(string path in new[]{Run,RunOnce,Compat,Layers}){
    cancel.ThrowIfCancellationRequested();try{using(var root=Engine.Base(h,v))using(var key=root.OpenSubKey(path)){
     if(key==null)continue;foreach(string n in key.GetValueNames()){
      var value=ReadValue(key,n);bool match=(path==Run||path==RunOnce)?MatchesPath(identity,CommandExe(value.Text)):MatchesPath(identity,n);
      if(match)result.Items.Add(new Candidate{Kind="RegistryValue",Path=path,Hive=h,View=v,ValueName=n,ExpectedHash=Fingerprint(value),Reason="Đường dẫn executable liên hệ với ứng dụng; chỉ xóa giá trị này, giữ khóa cha."});
     }
    }}catch(UnauthorizedAccessException){result.Notes.Add("Không đọc được: "+h+"\\"+path);}catch(System.Security.SecurityException){result.Notes.Add("Không đủ quyền Registry: "+path);}
   }
   report("Đang kiểm tra shortcut trong Start Menu, Desktop và Startup…",null);
   foreach(string root in ShortcutRoots()){
    if(!Directory.Exists(root))continue;var queue=new Queue<Tuple<string,int>>();queue.Enqueue(Tuple.Create(root,0));int count=0;
    while(queue.Count>0&&count<5000){cancel.ThrowIfCancellationRequested();var current=queue.Dequeue();try{
     Engine.NoLinks(current.Item1,false);foreach(string file in Directory.EnumerateFiles(current.Item1,"*.lnk")){
      count++;if(MatchesPath(identity,ShortcutTarget(file))){ValidateFile(file);result.Items.Add(new Candidate{Kind="File",Path=file,ExpectedHash=FileHash(file),Reason="Shortcut trỏ tới executable thuộc đường dẫn ứng dụng đã ghi nhận."});}
     }
     if(current.Item2<6)foreach(string dir in Directory.EnumerateDirectories(current.Item1))if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)==0)queue.Enqueue(Tuple.Create(dir,current.Item2+1));
    }catch(IOException){result.Notes.Add("Không đọc được shortcut: "+current.Item1);}catch(UnauthorizedAccessException){result.Notes.Add("Không đủ quyền shortcut: "+current.Item1);}}
    if(count>=5000)result.Notes.Add("Đã chạm giới hạn shortcut tại "+root);
   }
   // Registered application paths and service ImagePath are reviewed, never deleted as a shared parent.
   report("Đang kiểm tra App Paths và dịch vụ Windows…",null);
   foreach(string h in new[]{"HKCU","HKLM"})foreach(string v in Views)try{using(var root=Engine.Base(h,v))using(var key=root.OpenSubKey(AppPaths)){
    if(key==null)continue;foreach(string sub in key.GetSubKeyNames()){cancel.ThrowIfCancellationRequested();using(var k=key.OpenSubKey(sub))if(k!=null&&MatchesPath(identity,Engine.Read(k,"").Trim('"')))result.Items.Add(new Candidate{Kind="Review",Path=h+"\\"+AppPaths+"\\"+sub,ReviewOnly=true,Reason="App Paths liên hệ với executable; chỉ xem, chưa tự xóa đăng ký hệ thống."});}
   }}catch(UnauthorizedAccessException){result.Notes.Add("Không đọc được App Paths.");}
   using(var root=Engine.Base("HKLM",Views[0]))using(var services=root.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))if(services!=null)foreach(string name in services.GetSubKeyNames())try{
    cancel.ThrowIfCancellationRequested();using(var key=services.OpenSubKey(name))if(key!=null&&MatchesPath(identity,CommandExe(Engine.Read(key,"ImagePath"))))result.Items.Add(new Candidate{Kind="Review",Path="Service: "+name,ReviewOnly=true,Reason="Dịch vụ tham chiếu đường dẫn ứng dụng; kiểm tra trong Công cụ → Services."});
   }catch(UnauthorizedAccessException){result.Notes.Add("Không đọc được dịch vụ "+name);}
   report("Đang kiểm tra nhánh Registry nhà phát hành và tác vụ theo lịch…",null);
   ScanVendorRegistry(app,names,result,cancel);ScanTasks(identity,result,cancel);
   Remnants.TraceHunter.Extend(app,identity,names,result,cancel,report);
   foreach(var c in result.Items){c.AppId=app.Id;c.AppName=app.Name;}
   result.Items=result.Items.GroupBy(Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
   result.Notes=result.Notes.Distinct().ToList();
   report("Hoàn tất quét",null);
   return result;
  }
  /// <summary>Wraps the basic scan in a ScanResult so both scan modes share the same presentation path.</summary>
  public static ScanResult QuickScan(AppEntry app,Action<ScanProgress> progress=null){
   var result=new ScanResult();
   if(progress!=null)progress(new ScanProgress{Stage="Đang kiểm tra thư mục cài và khóa Registry trùng tên…"});
   result.Items.AddRange(Engine.Scan(app));
   if(progress!=null)progress(new ScanProgress{Stage="Hoàn tất quét",Found=result.Items.Count});
   return result;
  }
  /// <summary>Measures a candidate's footprint with bounded effort; folders stop after 20,000 files or five seconds.</summary>
  public static Footprint Measure(Candidate c,System.Threading.CancellationToken cancel){
   var fp=new Footprint();
   try{
    if(c.Kind=="File"){fp.Bytes=new FileInfo(c.Path).Length;fp.Files=1;fp.Detail="1 tệp";}
    else if(c.Kind=="Folder"){
     var watch=Stopwatch.StartNew();var queue=new Queue<string>();queue.Enqueue(c.Path);long total=0;int files=0;
     while(queue.Count>0){
      cancel.ThrowIfCancellationRequested();
      if(files>=20000||watch.Elapsed.TotalSeconds>5){fp.Partial=true;break;}
      string dir=queue.Dequeue();
      try{
       foreach(string file in Directory.EnumerateFiles(dir)){files++;try{total+=new FileInfo(file).Length;}catch(IOException){}catch(UnauthorizedAccessException){}}
       foreach(string sub in Directory.EnumerateDirectories(dir))if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)==0)queue.Enqueue(sub);
      }catch(UnauthorizedAccessException){fp.Partial=true;}catch(IOException){fp.Partial=true;}
     }
     fp.Bytes=total;fp.Files=files;fp.Detail=files.ToString("N0")+(fp.Partial?"+ tệp":" tệp");
    }
    else if(c.Kind=="Registry"){
     using(var root=Engine.Base(c.Hive,c.View))using(var key=root.OpenSubKey(c.Path))if(key!=null)fp.Detail=key.ValueCount+" giá trị, "+key.SubKeyCount+" khóa con";
    }
    else if(c.Kind=="RegistryValue")fp.Detail="1 giá trị";
   }catch(OperationCanceledException){throw;}catch(Exception){fp.Partial=true;}
   return fp;
  }
 }
 public class WindowsTool {
  public string Name,File,Arguments,Description;public bool Admin,Custom;
  public bool Available{get{return File.StartsWith("ms-settings:")||System.IO.File.Exists(File);}}
  /// <summary>Best icon source: .msc/.cpl quoted in Arguments (mmc host), else File, else SystemSettings/shell32 for ms-settings: URIs. Cmd wrappers keep cmd.exe like Revo.</summary>
  public string IconPath{get{
   string nested=QuotedPath(Arguments);
   if(nested!=""&&System.IO.File.Exists(nested)){
    string ext=Path.GetExtension(nested);
    if(ext.Equals(".msc",StringComparison.OrdinalIgnoreCase)||ext.Equals(".cpl",StringComparison.OrdinalIgnoreCase))return nested;
   }
   if(File.StartsWith("ms-settings:",StringComparison.OrdinalIgnoreCase)){
    string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"ImmersiveControlPanel","SystemSettings.exe");
    if(System.IO.File.Exists(settings))return settings;
    string shell=Path.Combine(Environment.SystemDirectory,"shell32.dll");return System.IO.File.Exists(shell)?shell+",15":"";
   }
   return File;
  }}
  /// <summary>Full launch string shown in the Tools list (Revo-style command-line column).</summary>
  public string CommandLine{get{return File.StartsWith("ms-settings:",StringComparison.OrdinalIgnoreCase)?File:(String.IsNullOrEmpty(Arguments)?File:File+" "+Arguments);}}
  static string QuotedPath(string args){
   if(String.IsNullOrWhiteSpace(args))return "";
   var m=System.Text.RegularExpressions.Regex.Match(args,@"[""']([A-Za-z]:\\[^""']+\.(?:msc|cpl|exe|dll))[""']",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
   if(m.Success)return m.Groups[1].Value;
   m=System.Text.RegularExpressions.Regex.Match(args,@"([A-Za-z]:\\[^\s""']+\.(?:msc|cpl|exe))",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
   return m.Success?m.Groups[1].Value:"";
  }
 }
 public static class WindowsTools {
  /// <summary>Native system icon for a catalog tool, sized for the Tools ImageList.</summary>
  public static System.Drawing.Bitmap Icon(WindowsTool tool,int size){return Presentation.ShellIcon(tool==null?"":tool.IconPath,size);}
  public static List<WindowsTool> Catalog(){
   string sys=Environment.SystemDirectory;var list=new List<WindowsTool>();
   Action<string,string,string,string,bool> add=(n,f,a,d,u)=>list.Add(new WindowsTool{Name=n,File=Path.Combine(sys,f),Arguments=a,Description=d,Admin=u});
   Action<string,string,string> cpl=(n,f,d)=>{string p=Path.Combine(sys,f);if(File.Exists(p))list.Add(new WindowsTool{Name=n,File=p,Description=d});};
   add("System Restore","rstrui.exe","","Mở trình khôi phục hệ thống; bạn chọn điểm khôi phục trong Windows.",false);
   cpl("Network Connections","ncpa.cpl","Kết nối mạng và adapter.");
   cpl("Security Center","wscui.cpl","Trung tâm bảo mật / trạng thái bảo vệ Windows.");
   cpl("System Properties","sysdm.cpl","Thuộc tính hệ thống, tên máy và điểm khôi phục.");
   add("System Information","msinfo32.exe","","Thông tin phần cứng và phần mềm.",false);
   add("Task Manager","Taskmgr.exe","","Tiến trình, hiệu năng và ứng dụng khởi động.",false);
   foreach(var item in new[]{new[]{"Services","services.msc","Quản lý dịch vụ."},new[]{"Task Scheduler","taskschd.msc","Quản lý tác vụ theo lịch."},new[]{"Device Manager","devmgmt.msc","Quản lý thiết bị và driver."},new[]{"Event Viewer","eventvwr.msc","Xem nhật ký Windows."},new[]{"Disk Management","diskmgmt.msc","Quản lý phân vùng và ổ đĩa."},new[]{"Shared Folders","fsmgmt.msc","Quản lý thư mục chia sẻ."},new[]{"Group Policy","gpedit.msc","Chính sách nhóm; có thể không có trên Windows Home."}}){
    if(File.Exists(Path.Combine(sys,item[1])))add(item[0],"mmc.exe","\""+Path.Combine(sys,item[1])+"\"",item[2],false);
    else list.Add(new WindowsTool{Name=item[0],File=Path.Combine(sys,item[1]),Description=item[2]});
   }
   add("Windows Features","OptionalFeatures.exe","","Bật/tắt thành phần Windows.",false);
   add("Optimize Drives","dfrgui.exe","","Mở công cụ tối ưu ổ đĩa.",false);
   add("Resource Monitor","resmon.exe","","Xem tài nguyên CPU, RAM, đĩa và mạng.",false);
   add("On-Screen Keyboard","osk.exe","","Bàn phím trên màn hình.",false);
   add("Windows Backup","sdclt.exe","","Sao lưu và khôi phục Windows.",false);
   if(File.Exists(Path.Combine(sys,"mrt.exe")))add("Malicious Software Removal Tool","mrt.exe","","Công cụ gỡ phần mềm độc hại của Microsoft.",true);
   add("Network Information","cmd.exe","/k \"\""+Path.Combine(sys,"ipconfig.exe")+"\" /all\"","Hiển thị cấu hình mạng; không thay đổi mạng.",false);
   add("TCP/IP Netstat","cmd.exe","/k \"\""+Path.Combine(sys,"netstat.exe")+"\" -a -b\"","Liệt kê kết nối TCP/IP và tiến trình sở hữu.",false);
   add("Check Disk","cmd.exe","/k \"\""+Path.Combine(sys,"chkdsk.exe")+"\"\"","Kiểm tra ổ đĩa; sửa lỗi cần quyền quản trị và có thể yêu cầu khởi động lại.",true);
   add("System File Checker","cmd.exe","/k \"\""+Path.Combine(sys,"sfc.exe")+"\" /scannow\"","Kiểm tra và sửa file hệ thống; cần quyền quản trị.",true);
   add("DISM RestoreHealth","dism.exe","/Online /Cleanup-Image /RestoreHealth","Sửa kho thành phần Windows (DISM). Chạy lâu, cần quyền quản trị, không xóa ứng dụng.",true);
   add("Flush DNS","cmd.exe","/k \"\""+Path.Combine(sys,"ipconfig.exe")+"\" /flushdns\"","Xóa bộ nhớ đệm DNS. Không đổi cài đặt mạng.",false);
   add("DirectX Diagnostic","dxdiag.exe","","Mở chẩn đoán DirectX.",false);
   list.Add(new WindowsTool{Name="Windows Security",File="ms-settings:windowsdefender",Description="Mở cài đặt bảo mật Windows."});
   list.Add(new WindowsTool{Name="Startup Apps",File="ms-settings:startupapps",Description="Xem trạng thái bật/tắt khởi động do Windows quản lý."});
   return list;
  }
  public static void Launch(WindowsTool tool){if(!tool.Available)throw new IOException("Công cụ không có trên phiên bản Windows này.");var p=new ProcessStartInfo(tool.File,tool.Arguments??""){UseShellExecute=true};if(tool.Admin)p.Verb="runas";Process.Start(p);}
 }
}
