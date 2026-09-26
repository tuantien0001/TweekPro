using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace TweekPro.Shortcuts {
 public static class ShortcutsTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("Shortcuts: "+message);}
  static void MustFail(Action action,string message){bool failed=false;try{action();}catch(Exception){failed=true;}Assert(failed,message);}

  /// <summary>Creates a shell link without executing its target, releasing COM references immediately.</summary>
  static void Link(string path,string target){
   object shell=null,link=null;
   try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic s=shell;link=s.CreateShortcut(path);dynamic l=link;l.TargetPath=target;l.Save();}
   finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
  }

  /// <summary>Proves conservative target classification and real Windows vault round trips using an isolated, uniquely named Startup fixture.</summary>
  public static void Run(){
   foreach(string path in new[]{null,"",@"\\server\share\app.exe",@"\\?\C:\app.exe","https://example.com","shell:AppsFolder",@"C:relative.exe",@"C:\%MISSING%\x.exe",@"C:\x\..\y.exe",@"C:\x:stream"})Assert(!BrokenShortcuts.LocalTarget(path),"ambiguous target kept");
   Assert(BrokenShortcuts.LocalTarget(@"C:\Apps\Missing.exe"),"qualified local path");
   string testTarget=@"C:\Apps\Missing.exe";
   Func<string,FileAttributes> absent=p=>{if(p==testTarget)throw new FileNotFoundException();return FileAttributes.Directory;};
   Assert(BrokenShortcuts.TargetMissing(testTarget,absent,r=>true),"confirmed file absence");
   Assert(!BrokenShortcuts.TargetMissing(testTarget,absent,r=>false),"offline/removable drive kept");
   Assert(!BrokenShortcuts.TargetMissing(testTarget,p=>{if(p==testTarget)throw new UnauthorizedAccessException();return FileAttributes.Directory;},r=>true),"access denied is not missing");
   Assert(!BrokenShortcuts.TargetMissing(testTarget,p=>{throw new IOException("device error");},r=>true),"device error is not missing");
   foreach(var unsafeAttr in new[]{FileAttributes.ReparsePoint,FileAttributes.Offline,(FileAttributes)0x40000,(FileAttributes)0x400000})
    Assert(!BrokenShortcuts.TargetMissing(testTarget,p=>p==@"C:\Apps"?unsafeAttr:absent(p),r=>true),"reparse/cloud ancestors kept");
   Assert(!BrokenShortcuts.TargetMissing(testTarget,p=>FileAttributes.Normal,r=>true),"existing target kept");
   foreach(var pair in ShortcutsLang.Table)Assert(Core.L.Has(pair.Key),"feature translation registered");
   foreach(string text in new[]{"Kết quả","Tên shortcut","Vị trí shortcut","Đích không tồn tại","Chọn tất cả","Bỏ chọn tất cả"})Assert(Core.L.Has(text),"dialog header/action translated");
   byte[] header=new byte[76];BitConverter.GetBytes(76u).CopyTo(header,0);new Guid("00021401-0000-0000-c000-000000000046").ToByteArray().CopyTo(header,4);
   Assert(BrokenShortcuts.OrdinaryLink(header),"normal shell header");header[21]=0x10;Assert(!BrokenShortcuts.OrdinaryLink(header),"MSI advertised link kept");Assert(!BrokenShortcuts.OrdinaryLink(new byte[20]),"malformed kept");
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return;
   string parent=Environment.GetFolderPath(Environment.SpecialFolder.Startup);if(String.IsNullOrEmpty(parent))throw new Exception("No Startup fixture root");
   string root=Path.Combine(parent,"TweekProTestShortcuts-"+Guid.NewGuid().ToString("N"));string oldVault=Engine.Vault;
   try{
    Directory.CreateDirectory(root);Engine.Vault=Path.Combine(root,"Vault");
    string target=Path.Combine(root,"missing.exe"),lnk=Path.Combine(root,"missing.lnk"),alive=Path.Combine(root,"alive.exe");File.WriteAllText(alive,"fixture");
    Link(lnk,target);Link(Path.Combine(root,"alive.lnk"),alive);Link(Path.Combine(root,"folder.lnk"),root);File.WriteAllText(Path.Combine(root,"malformed.lnk"),"fixture");
    byte[] advertised=File.ReadAllBytes(lnk);advertised[21]|=0x10;File.WriteAllBytes(Path.Combine(root,"advertised.lnk"),advertised);
    var scan=BrokenShortcuts.ScanRoots(new[]{root,root},CancellationToken.None);
    Assert(scan.Items.Count==1&&scan.Items[0].Path==lnk,"only missing target, no duplicates or folder/alive/advertised/malformed links");
    var item=scan.Items.Single();
    File.WriteAllText(target,"reappeared");MustFail(()=>BrokenShortcuts.Store(item),"target reappeared refuses cleanup");File.Delete(target);
    Link(lnk,Path.Combine(root,"different.exe"));MustFail(()=>BrokenShortcuts.Store(item),"changed shortcut refuses cleanup");Link(lnk,target);item=BrokenShortcuts.Inspect(lnk);
    string outside=Path.Combine(Path.GetTempPath(),"TweekProTestShortcut-"+Guid.NewGuid().ToString("N")+".lnk");
    try{Link(outside,target);var foreign=BrokenShortcuts.Inspect(outside);Assert(foreign!=null,"outside inspect read only");MustFail(()=>BrokenShortcuts.Store(foreign),"outside allowlist refused");}finally{if(File.Exists(outside))File.Delete(outside);}
    var backup=BrokenShortcuts.Store(item);Assert(backup.State=="BackedUp"&&!File.Exists(lnk)&&File.Exists(Path.Combine(Engine.Vault,backup.Id,"content")),"stored in real vault");
    Link(lnk,alive);MustFail(()=>Engine.Restore(backup),"restore refuses overwrite");File.Delete(lnk);Engine.Restore(backup);
    Assert(backup.State=="Restored"&&Advanced.FileHash(lnk)==item.Hash,"restore preserves exact bytes");
    var stop=new CancellationTokenSource();stop.Cancel();MustFail(()=>BrokenShortcuts.ScanRoots(new[]{root},stop.Token),"scan cancellation");stop.Dispose();
   }finally{Engine.Vault=oldVault;if(Engine.Under(Engine.Canon(root),Engine.Canon(parent))&&Path.GetFileName(root).StartsWith("TweekProTestShortcuts-")&&Directory.Exists(root))Directory.Delete(root,true);}
  }
 }
}
