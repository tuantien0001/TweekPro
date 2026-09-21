using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32;
using TweekPro.Core;

namespace TweekPro.Explorer {
 /// <summary>Self-tests for the Explorer tab: catalog sanity, value interpretation, vault payload round-trip, allow-list refusals, registry apply/restore on a fixture key (Windows) and the file inspector rules.</summary>
 public static class ExplorerTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("ExplorerTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   Catalog();Interpretation();Payloads();Safety();
   if(ExplorerTweaks.IsWindows)RegistryRoundTrip();
   Disguise();PeHeader();Zone();InspectFixture();Listing();
   Assert(L.Has("Explorer")&&Branding.GlyphKey("Explorer")=="explorer"&&Branding.GlyphKey("File Explorer")=="explorer","tab title translated with glyph");
  }

  /// <summary>Folder browser lists hidden and system entries with extension and type, folders first, and navigates up to the drive list.</summary>
  static void Listing(){
   string root=Path.Combine(Path.GetTempPath(),"TweekProBrowse"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   try{
    Directory.CreateDirectory(Path.Combine(root,"zeta"));string hiddenDir=Path.Combine(root,".cache");Directory.CreateDirectory(hiddenDir);File.SetAttributes(hiddenDir,FileAttributes.Directory|FileAttributes.Hidden);
    File.WriteAllText(Path.Combine(root,"b.txt"),"b");string hidden=Path.Combine(root,"a.pdf.exe");File.WriteAllBytes(hidden,new byte[]{1,2,3});File.SetAttributes(hidden,FileAttributes.Hidden);
    string sys=Path.Combine(root,"desktop.ini");File.WriteAllText(sys,"[.ShellClassInfo]");File.SetAttributes(sys,FileAttributes.Hidden|FileAttributes.System);
    File.WriteAllText(Path.Combine(root,"README"),"plain");
    var listing=FileInspector.ListFolder(root,CancellationToken.None);
    Assert(listing.Entries.Count==6&&listing.Folders==2&&listing.Files==4,"lists every entry including hidden and system: "+listing.Entries.Count);
    Assert(listing.Hidden==3&&listing.System==1,"hidden/system counts: "+listing.Hidden+"/"+listing.System);
    Assert(listing.Entries[0].IsDirectory&&listing.Entries[1].IsDirectory&&!listing.Entries[2].IsDirectory,"folders sort first");
    var exe=listing.Entries.First(e=>e.Name=="a.pdf.exe");Assert(exe.Extension==".exe"&&exe.Hidden&&!exe.System&&exe.Bytes==3&&exe.TypeName!="","hidden exe keeps real extension, size and type: "+exe.TypeName);
    var ini=listing.Entries.First(e=>e.Name=="desktop.ini");Assert(ini.Hidden&&ini.System&&ini.Extension==".ini","system file flagged");
    var plain=listing.Entries.First(e=>e.Name=="README");Assert(plain.Extension==""&&plain.TypeName!="","extensionless file still has a type label: "+plain.TypeName);
    Assert(listing.Entries.First(e=>e.Name==".cache").Hidden&&listing.Entries.First(e=>e.Name=="zeta").TypeName==L.T("Thư mục"),"folder rows");
    Assert(String.Equals(FileInspector.ParentOf(hidden),root,StringComparison.OrdinalIgnoreCase),"parent of file is its folder");
    Assert(String.Equals(FileInspector.ParentOf(root+"\\"),Path.GetDirectoryName(root),StringComparison.OrdinalIgnoreCase),"parent ignores trailing separator");
    string driveRoot=Path.GetPathRoot(root);Assert(FileInspector.ParentOf(driveRoot)==""&&FileInspector.ParentOf("")=="","drive root goes to drive list");
    var drives=FileInspector.ListFolder("",CancellationToken.None);Assert(drives.IsDrives&&drives.Entries.Count>0&&drives.Entries.All(e=>e.IsDrive&&e.IsDirectory),"drive list");
    MustFail(()=>FileInspector.ListFolder(Path.Combine(root,"missing"),CancellationToken.None),"missing folder throws");
    var cancelled=new CancellationTokenSource();cancelled.Cancel();MustFail(()=>FileInspector.ListFolder(root,cancelled.Token),"cancellation honoured");
   }finally{try{foreach(string f in Directory.GetFiles(root))File.SetAttributes(f,FileAttributes.Normal);Directory.Delete(root,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
  }

  static void Catalog(){
   var ids=ExplorerTweaks.Catalog.Select(t=>t.Id).ToList();Assert(ids.Distinct().Count()==ids.Count&&ids.Count>=12,"unique ids");
   foreach(var t in ExplorerTweaks.Catalog){
    Assert(ExplorerSafety.Allowed(t)&&ExplorerSafety.AllowedKey(t.KeyPath),"catalog entry allowed: "+t.Id);
    Assert(t.OnValue!=t.OffValue&&!String.IsNullOrWhiteSpace(t.ValueName),"distinct states: "+t.Id);
    Assert(L.Has(t.Name)&&L.Has(t.Description),"translated: "+t.Id);
   }
   Assert(ExplorerTweaks.Catalog.Count(t=>t.KeyPath==ExplorerSafety.TestKey)==0,"fixture key is not in the catalog");
   var ext=ExplorerTweaks.Catalog.First(t=>t.Id=="show-ext");Assert(ext.ValueName=="HideFileExt"&&ext.OnValue==0&&ext.OffValue==1,"HideFileExt is inverted");
   var hidden=ExplorerTweaks.Catalog.First(t=>t.Id=="show-hidden");Assert(hidden.ValueName=="Hidden"&&hidden.OnValue==1&&hidden.OffValue==2,"Hidden uses 1/2");
   foreach(TweakGroup g in Enum.GetValues(typeof(TweakGroup)))Assert(!String.IsNullOrEmpty(ExplorerTweaks.GroupLabel(g)),"group label "+g);
   var states=ExplorerTweaks.Read();Assert(states.Count==ExplorerTweaks.Catalog.Length&&states.All(s=>s.Tweak!=null),"Read covers the catalog on every platform");
  }

  static void Interpretation(){
   var ext=ExplorerTweaks.Catalog.First(t=>t.Id=="show-ext");bool known;
   Assert(ExplorerTweaks.Interpret(ext,0,out known)&&known,"HideFileExt=0 means extensions shown");
   Assert(!ExplorerTweaks.Interpret(ext,1,out known)&&known,"HideFileExt=1 means hidden");
   Assert(ExplorerTweaks.Interpret(ext,null,out known)==ext.DefaultOn&&!known,"missing value = default, unknown");
   Assert(ExplorerTweaks.Interpret(ext,7,out known)==ext.DefaultOn&&!known,"unexpected number = default, unknown");
   Assert(ExplorerTweaks.Interpret(ext,"0",out known)&&known,"string digits are tolerated");
   Assert(ExplorerTweaks.Interpret(ext,"abc",out known)==ext.DefaultOn&&!known,"garbage string = unknown");
   var status=ExplorerTweaks.Catalog.First(t=>t.Id=="status-bar");Assert(ExplorerTweaks.Interpret(status,null,out known)&&!known,"status bar defaults on");
  }

  static void Payloads(){
   object v;RegistryValueKind k;
   Assert(ExplorerTweaks.SerializePrior(null)=="absent"&&ExplorerTweaks.TryParsePrior("absent",out v,out k)&&v==null,"absent round-trip");
   Assert(ExplorerTweaks.SerializePrior(2)=="dword:2"&&ExplorerTweaks.TryParsePrior("dword:2",out v,out k)&&(int)v==2&&k==RegistryValueKind.DWord,"dword round-trip");
   Assert(ExplorerTweaks.SerializePrior(5L)=="qword:5"&&ExplorerTweaks.TryParsePrior("qword:5",out v,out k)&&(long)v==5&&k==RegistryValueKind.QWord,"qword round-trip");
   Assert(ExplorerTweaks.SerializePrior("a:b")=="string:a:b"&&ExplorerTweaks.TryParsePrior("string:a:b",out v,out k)&&(string)v=="a:b","string with colon round-trip");
   Assert(ExplorerTweaks.SerializePrior(new byte[]{1,255})=="binary:01FF"&&ExplorerTweaks.TryParsePrior("binary:01FF",out v,out k)&&((byte[])v)[1]==255&&k==RegistryValueKind.Binary,"binary round-trip");
   Assert(!ExplorerTweaks.TryParsePrior(null,out v,out k)&&!ExplorerTweaks.TryParsePrior("",out v,out k)&&!ExplorerTweaks.TryParsePrior("dword:x",out v,out k)&&!ExplorerTweaks.TryParsePrior("other:1",out v,out k)&&!ExplorerTweaks.TryParsePrior("binary:0",out v,out k),"foreign payloads rejected");
  }

  static void Safety(){
   Assert(!ExplorerSafety.AllowedKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"),"policy key refused");
   Assert(!ExplorerSafety.AllowedKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\Sub"),"child key refused");
   Assert(!ExplorerSafety.AllowedKey(@"Software\Microsoft\Windows\CurrentVersion"),"parent key refused");
   Assert(!ExplorerSafety.AllowedKey(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced")&&!ExplorerSafety.AllowedKey(@"Software\..\Explorer\Advanced")&&!ExplorerSafety.AllowedKey("")&&!ExplorerSafety.AllowedKey(null),"hive prefix, dot-dot and empty refused");
   Assert(ExplorerSafety.AllowedKey(ExplorerSafety.Advanced+"\\")&&ExplorerSafety.AllowedKey(ExplorerSafety.Advanced.ToLowerInvariant()),"trailing slash and case tolerated");
   var foreign=new ExplorerTweak{Id="lua",KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Policies\System",ValueName="EnableLUA",OnValue=1,OffValue=0};
   Assert(!ExplorerSafety.Allowed(foreign),"foreign tweak not allowed");
   MustFail(()=>ExplorerTweaks.Apply(foreign,false),"Apply refuses a non-catalog key on every platform");
   var forged=new ExplorerTweak{Id="show-ext",KeyPath=ExplorerSafety.Advanced,ValueName="EnableLUA",OnValue=0,OffValue=1};
   Assert(!ExplorerSafety.Allowed(forged),"catalog id with a different value name is refused");
   MustFail(()=>ExplorerTweaks.Apply(forged,true),"Apply refuses forged catalog entry");
   MustFail(()=>ExplorerTweaks.Apply(null,true),"Apply refuses null");
   MustFail(()=>ExplorerTweaks.Restore(new Backup{Kind="Explorer",Original=@"Software\Microsoft\Windows\CurrentVersion\Policies\System",ValueName="EnableLUA",Payload="dword:1"}),"Restore refuses foreign key");
   MustFail(()=>ExplorerTweaks.Restore(new Backup{Kind="Explorer",Original=ExplorerSafety.Advanced,ValueName="Sub\\Hidden",Payload="dword:1"}),"Restore refuses value names with separators");
   MustFail(()=>ExplorerTweaks.Restore(new Backup{Kind="Service",Original=ExplorerSafety.Advanced,ValueName="Hidden",Payload="dword:1"}),"Restore refuses other kinds");
   MustFail(()=>ExplorerTweaks.Restore(new Backup{Kind="Explorer",Original=ExplorerSafety.Advanced,ValueName="Hidden",Payload="other:1"}),"Restore refuses unparseable payload");
   MustFail(()=>ExplorerTweaks.Restore(null),"Restore refuses null");
  }

  /// <summary>Applies and restores a fixture tweak under HKCU\Software\TweekProTest\Explorer, proving absent and pre-existing values both come back.</summary>
  static void RegistryRoundTrip(){
   const string fixtureRoot=@"Software\TweekProTest";
   var t=new ExplorerTweak{Id="fixture",Name="Fixture",Description="Fixture",KeyPath=ExplorerSafety.TestKey,ValueName="HideFileExt",OnValue=0,OffValue=1,DefaultOn=false};
   try{
    Registry.CurrentUser.DeleteSubKeyTree(ExplorerSafety.TestKey,false);
    var b1=ExplorerTweaks.Apply(t,true);
    Assert(b1.Kind=="Explorer"&&b1.State=="BackedUp"&&b1.Payload=="absent"&&b1.Original==ExplorerSafety.TestKey&&b1.ValueName=="HideFileExt"&&File.Exists(Path.Combine(Engine.Vault,b1.Id,"manifest.xml")),"first apply recorded absent prior");
    using(var k=Registry.CurrentUser.OpenSubKey(ExplorerSafety.TestKey))Assert(k!=null&&(int)k.GetValue("HideFileExt")==0,"value written = on");
    bool known;var read=ExplorerTweaks.Interpret(t,Registry.CurrentUser.OpenSubKey(ExplorerSafety.TestKey).GetValue("HideFileExt"),out known);Assert(read&&known,"interpreted as on");
    var b2=ExplorerTweaks.Apply(t,false);
    Assert(b2.Payload=="dword:0","second apply recorded prior dword");
    using(var k=Registry.CurrentUser.OpenSubKey(ExplorerSafety.TestKey))Assert((int)k.GetValue("HideFileExt")==1,"value written = off");
    ExplorerTweaks.Restore(b2);
    using(var k=Registry.CurrentUser.OpenSubKey(ExplorerSafety.TestKey))Assert((int)k.GetValue("HideFileExt")==0&&b2.State=="Restored","restore put the dword back");
    ExplorerTweaks.Restore(b1);
    using(var k=Registry.CurrentUser.OpenSubKey(ExplorerSafety.TestKey))Assert(k==null||k.GetValue("HideFileExt")==null,"restore of absent prior deleted the value");
    Assert(b1.State=="Restored","first backup restored");
    Assert(Engine.Backups().Any(x=>x.Id==b1.Id&&x.Kind=="Explorer"),"Explorer backups listed in the vault");
    Engine.Purge(b1);Engine.Purge(b2);Assert(!Directory.Exists(Path.Combine(Engine.Vault,b1.Id))&&!Directory.Exists(Path.Combine(Engine.Vault,b2.Id)),"fixture backups purged");
   }finally{try{Registry.CurrentUser.DeleteSubKeyTree(ExplorerSafety.TestKey,false);Registry.CurrentUser.DeleteSubKey(fixtureRoot,false);}catch(Exception){}}
  }

  static void Disguise(){
   Assert(FileInspector.DisguisedAs("hoadon.pdf.exe")==".pdf","pdf.exe disguised");
   Assert(FileInspector.DisguisedAs("Photo.JPG.SCR")==".jpg","case-insensitive, lower-cased result");
   Assert(FileInspector.DisguisedAs("report.docx.lnk")==".docx","shortcut disguise");
   Assert(FileInspector.DisguisedAs("setup.exe")==""&&FileInspector.DisguisedAs("archive.tar.gz")==""&&FileInspector.DisguisedAs("notes.txt")==""&&FileInspector.DisguisedAs("")==""&&FileInspector.DisguisedAs(null)=="","honest names");
   Assert(FileInspector.DisguisedAs("my.pdf")==""&&FileInspector.DisguisedAs("v1.2.exe")=="","version-like segments are not documents");
   Assert(FileInspector.HasBidiOverride("photo\u202Egnp.exe")&&!FileInspector.HasBidiOverride("photo.png")&&!FileInspector.HasBidiOverride(null),"RLO detection");
   var d=new FileDetails{Name="a.pdf.exe",Extension=".exe",Bytes=10,FromInternet=true,ZoneId=3,Architecture="x86"};d.DisguisedAs=FileInspector.DisguisedAs(d.Name);FileInspector.Warn(d);
   Assert(d.Warnings.Count==2&&d.Notes.Count==0,"disguised unsigned download: 2 warnings, got "+d.Warnings.Count+"/"+d.Notes.Count);
   var pe=new FileDetails{Name="doc.pdf",Extension=".pdf",Bytes=10,Architecture="x64"};FileInspector.Warn(pe);Assert(pe.Warnings.Count==1,"PE content behind .pdf warns");
   var fake=new FileDetails{Name="tool.exe",Extension=".exe",Bytes=10,Architecture=""};FileInspector.Warn(fake);Assert(fake.Warnings.Count==1,"non-PE .exe warns");
   var hidden=new FileDetails{Name="desktop.ini",Extension=".ini",Bytes=10,Attributes=FileAttributes.Hidden|FileAttributes.System};FileInspector.Warn(hidden);Assert(hidden.Warnings.Count==0&&hidden.Notes.Count==1,"hidden system file is a note, not a warning");
   var signed=new FileDetails{Name="app.exe",Extension=".exe",Bytes=10,Architecture="x64",Signed=true,Publisher="Contoso",FromInternet=true,ZoneId=3};FileInspector.Warn(signed);Assert(signed.Warnings.Count==0&&signed.Notes.Count==2,"signed download: notes only");
   var dir=new FileDetails{Name="x",IsDirectory=true,Attributes=FileAttributes.Hidden};FileInspector.Warn(dir);Assert(dir.Warnings.Count==0&&dir.Notes.Count==0,"directories get no rules");
   Assert(FileInspector.AttributeLabel(FileAttributes.Normal)!=""&&FileInspector.AttributeLabel(FileAttributes.Hidden|FileAttributes.ReadOnly).Contains(","),"attribute labels");
   Assert(FileInspector.Rows(d).Count>=8&&FileInspector.Rows(d)[0].Kind==RowKind.Warning&&FileInspector.Report(d).Contains(d.Path??""),"rows start with warnings; report renders");
   Assert(FileInspector.Rows(null).Count==0,"null details = no rows");
  }

  static byte[] Pe(ushort machine){var b=new byte[0x60];b[0]=(byte)'M';b[1]=(byte)'Z';BitConverter.GetBytes(0x40).CopyTo(b,0x3C);b[0x40]=(byte)'P';b[0x41]=(byte)'E';BitConverter.GetBytes(machine).CopyTo(b,0x44);return b;}
  static void PeHeader(){
   Assert(FileInspector.PeArchitecture(Pe(0x8664))=="x64"&&FileInspector.PeArchitecture(Pe(0x14c))=="x86"&&FileInspector.PeArchitecture(Pe(0xAA64))=="ARM64"&&FileInspector.PeArchitecture(Pe(0x1c4))=="ARM"&&FileInspector.PeArchitecture(Pe(0x1234))=="PE","machine types");
   var noSig=Pe(0x8664);noSig[0x41]=(byte)'X';Assert(FileInspector.PeArchitecture(noSig)=="","missing PE signature");
   var badOffset=Pe(0x8664);BitConverter.GetBytes(0x7FFFFFF0).CopyTo(badOffset,0x3C);Assert(FileInspector.PeArchitecture(badOffset)=="","e_lfanew beyond the file");
   Assert(FileInspector.PeArchitecture(new byte[]{1,2,3})==""&&FileInspector.PeArchitecture((byte[])null)==""&&FileInspector.PeArchitecture(System.Text.Encoding.ASCII.GetBytes("%PDF-1.7 "+new string(' ',100)))=="","non-PE inputs");
  }

  static void Zone(){
   int z;string r,h;
   Assert(FileInspector.ParseZone("[ZoneTransfer]\r\nZoneId=3\r\nReferrerUrl=https://a.example/\r\nHostUrl=https://b.example/x.exe\r\n",out z,out r,out h)&&z==3&&r=="https://a.example/"&&h=="https://b.example/x.exe","zone parsed");
   Assert(FileInspector.ParseZone("[ZoneTransfer]\nzoneid = 2\n",out z,out r,out h)&&z==2&&r==""&&h=="","case and spacing tolerated");
   Assert(!FileInspector.ParseZone("",out z,out r,out h)&&!FileInspector.ParseZone(null,out z,out r,out h)&&!FileInspector.ParseZone("[ZoneTransfer]\nHostUrl=x",out z,out r,out h),"no ZoneId = not marked");
   Assert(FileInspector.ZoneLabel(3)!=FileInspector.ZoneLabel(0)&&FileInspector.ZoneLabel(99)!="","zone labels");
  }

  /// <summary>Inspects a real temp file: size, hashes, extension, clean warnings; on Windows also the Zone.Identifier stream and a disguised copy.</summary>
  static void InspectFixture(){
   string root=Path.Combine(Path.GetTempPath(),"TweekProExplorer"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   try{
    string file=Path.Combine(root,"sample.bin");var data=new byte[300*1024+7];new Random(7).NextBytes(data);File.WriteAllBytes(file,data);
    string sha;using(var s=SHA256.Create())sha=FileInspector.Hex(s.ComputeHash(data));
    var d=FileInspector.Inspect(file,true,CancellationToken.None);
    Assert(d.Exists&&!d.IsDirectory&&d.Bytes==data.Length&&d.Extension==".bin"&&d.Name=="sample.bin","basic facts");
    Assert(d.Sha256==sha&&d.Md5.Length==32&&!d.HashSkipped,"sha256 matches");
    Assert(d.Warnings.Count==0&&d.Architecture==""&&!d.FromInternet&&!d.Signed,"plain data file is clean");
    Assert(!String.IsNullOrEmpty(d.TypeName),"type name present");
    var noHash=FileInspector.Inspect(file,false,CancellationToken.None);Assert(noHash.Sha256==""&&noHash.Md5=="","hashing optional");
    var dirDetails=FileInspector.Inspect(root,true,CancellationToken.None);Assert(dirDetails.IsDirectory&&dirDetails.Exists&&dirDetails.Bytes==-1,"directory inspected");
    MustFail(()=>FileInspector.Inspect(Path.Combine(root,"missing.txt"),false,CancellationToken.None),"missing file throws");
    MustFail(()=>FileInspector.Inspect("",false,CancellationToken.None),"empty path throws");
    string pe=Path.Combine(root,"invoice.pdf.exe");File.WriteAllBytes(pe,Pe(0x14c));
    var fake=FileInspector.Inspect(pe,false,CancellationToken.None);
    Assert(fake.Architecture=="x86"&&fake.DisguisedAs==".pdf"&&fake.Warnings.Count>=1,"disguised PE detected: "+fake.Warnings.Count);
    if(ExplorerTweaks.IsWindows){
     try{
      File.WriteAllText(pe+":Zone.Identifier","[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://example.test/invoice.pdf.exe\r\n");
      var marked=FileInspector.Inspect(pe,false,CancellationToken.None);
      Assert(marked.FromInternet&&marked.ZoneId==3&&marked.HostUrl.EndsWith("invoice.pdf.exe")&&marked.Warnings.Count==2,"Mark of the Web read: "+marked.Warnings.Count);
      File.SetAttributes(file,FileAttributes.Hidden);var hidden=FileInspector.Inspect(file,false,CancellationToken.None);Assert(hidden.Hidden&&hidden.Notes.Count==1,"hidden attribute noted");File.SetAttributes(file,FileAttributes.Normal);
      Assert(!String.IsNullOrEmpty(marked.Owner),"owner resolved on Windows");
     }catch(IOException e){Log.Warn("ExplorerTests: Zone.Identifier not supported here: "+e.Message);}catch(NotSupportedException e){Log.Warn("ExplorerTests: Zone.Identifier not supported here: "+e.Message);}
    }
    var cts=new CancellationTokenSource();cts.Cancel();MustFail(()=>FileInspector.Inspect(file,true,cts.Token),"cancellation honoured");
   }finally{try{foreach(var f in Directory.GetFiles(root))File.SetAttributes(f,FileAttributes.Normal);Directory.Delete(root,true);}catch(Exception){}}
  }
 }
}
