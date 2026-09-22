using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TweekPro.Shredder {
 /// <summary>Self-tests for the shredder: boundary refusals, plan walk (links skipped, sizes summed), overwrite really replaces the bytes, shred removes files + folders under random names and never follows reparse points.</summary>
 public static class ShredderTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("ShredderTests: "+message);}
  static void Throws(Action a,string message){bool threw=false;try{a();}catch(IOException){threw=true;}catch(ArgumentException){threw=true;}Assert(threw,message);}

  public static void Run(){
   string fixture=Path.Combine(Path.GetTempPath(),"tweekpro-shred-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(fixture);
   try{
    Refusals(fixture);
    PlanAndShred(fixture);
    OverwriteReplacesBytes(fixture);
    Passes(fixture);
   }finally{try{if(Directory.Exists(fixture))Directory.Delete(fixture,true);}catch(Exception){}}
  }

  static void Refusals(string fixture){
   var forbidden=new List<string>{Path.Combine(fixture,"Windows"),Path.Combine(fixture,"Program Files")};
   Throws(()=>ShredSafety.Validate("",forbidden),"empty path must be refused");
   string root=Path.GetPathRoot(fixture);
   Throws(()=>ShredSafety.Validate(root,forbidden),"drive root must be refused");
   Throws(()=>ShredSafety.Validate(Path.Combine(fixture,"Windows","System32","x.dll"),forbidden),"inside a forbidden root must be refused");
   Throws(()=>ShredSafety.Validate(Path.Combine(fixture,"Program Files"),forbidden),"the forbidden root itself must be refused");
   Throws(()=>ShredSafety.Validate(fixture,forbidden),"a folder that contains a forbidden root must be refused");
   Throws(()=>ShredSafety.Validate(@"\\server\share\file.txt",forbidden),"UNC paths must be refused");
   string ok=Path.Combine(fixture,"docs","a.txt");Directory.CreateDirectory(Path.GetDirectoryName(ok));File.WriteAllText(ok,"x");
   ShredSafety.Validate(ok,forbidden);
   Throws(()=>ShredSafety.Validate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),"the profile folder must be refused");
   var real=ShredSafety.ForbiddenRoots();
   Assert(real.Any(r=>r.EndsWith("Backups",StringComparison.OrdinalIgnoreCase)||String.Equals(r,Engine.Canon(Engine.Vault),StringComparison.OrdinalIgnoreCase)),"the vault must be a forbidden root");
   if(Core.StubbornFiles.IsWindows){
    string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    Throws(()=>ShredSafety.Validate(Path.Combine(windows,"System32","kernel32.dll")),"System32 must be refused on Windows");
    Throws(()=>ShredSafety.Validate(Path.Combine(Engine.Vault,"anything.bin")),"the vault must be refused");
   }
   Throws(()=>FileShredder.Shred(new ShredPlan{Target=Path.Combine(fixture,"docs"),IsDirectory=true,Truncated=true},1,null,CancellationToken.None),"a truncated plan must not run");
   Throws(()=>FileShredder.Shred(new ShredPlan{Target=ok},2,null,CancellationToken.None),"only 1 or 3 passes are allowed");
  }

  static void PlanAndShred(string fixture){
   string target=Path.Combine(fixture,"tree");
   Directory.CreateDirectory(Path.Combine(target,"sub","deep"));
   File.WriteAllBytes(Path.Combine(target,"a.bin"),new byte[3000]);
   File.WriteAllBytes(Path.Combine(target,"sub","b.bin"),new byte[5000]);
   File.WriteAllText(Path.Combine(target,"sub","deep","c.txt"),"hello");
   File.SetAttributes(Path.Combine(target,"sub","b.bin"),FileAttributes.ReadOnly);
   string outside=Path.Combine(fixture,"outside");Directory.CreateDirectory(outside);File.WriteAllText(Path.Combine(outside,"keep.txt"),"keep");
   bool linked=false;
   if(Core.StubbornFiles.IsWindows){
    try{var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+Path.Combine(target,"link")+"\" \""+outside+"\"") {UseShellExecute=false,CreateNoWindow=true});p.WaitForExit(10000);linked=Directory.Exists(Path.Combine(target,"link"));}catch(Exception){}
   }
   var plan=FileShredder.Plan(target,CancellationToken.None);
   Assert(plan.IsDirectory&&plan.Files.Count==3,"plan must list 3 files, got "+plan.Files.Count);
   Assert(plan.Bytes==3000+5000+5,"plan bytes must be summed, got "+plan.Bytes);
   Assert(plan.Folders.Count==3,"plan must list 3 folders, got "+plan.Folders.Count);
   if(linked){Assert(plan.SkippedLinks.Count==1,"the junction must be reported as skipped");Assert(!plan.Files.Any(f=>f.EndsWith("keep.txt",StringComparison.OrdinalIgnoreCase)),"files behind a junction must not be planned");}
   var single=FileShredder.Plan(Path.Combine(target,"a.bin"),CancellationToken.None);
   Assert(!single.IsDirectory&&single.Files.Count==1&&single.Bytes==3000,"single file plan");
   var stages=new List<string>();
   var report=FileShredder.Shred(plan,1,stages.Add,CancellationToken.None);
   Assert(report.Files==3&&report.Failed.Count==0,"shred must overwrite 3 files without failures: "+String.Join("; ",report.Failed));
   Assert(report.Bytes==8005,"shred must report bytes written, got "+report.Bytes);
   Assert(stages.Contains("a.bin")&&stages.Contains("c.txt"),"stage callback per file");
   Assert(!Directory.Exists(target),"the target folder must be gone");
   if(linked)Assert(report.Links==1,"the junction itself must be removed (not followed)");
   Assert(File.Exists(Path.Combine(outside,"keep.txt")),"content behind a junction must survive");
   Assert(!Directory.EnumerateFileSystemEntries(fixture).Any(e=>Path.GetFileName(e).Length==16&&!e.EndsWith("outside")),"no random-named leftovers");
  }

  static void OverwriteReplacesBytes(string fixture){
   string file=Path.Combine(fixture,"secret.txt");
   var original=System.Text.Encoding.ASCII.GetBytes(String.Concat(Enumerable.Repeat("TOP-SECRET-PAYLOAD-",300)));
   File.WriteAllBytes(file,original);
   long written=FileShredder.Overwrite(file,1,CancellationToken.None);
   Assert(written==original.Length,"one pass must write exactly the file length, got "+written);
   Assert(new FileInfo(file).Length==0,"file must be truncated after overwrite");
   FileShredder.Erase(file);
   Assert(!File.Exists(file),"erase must delete the file");
   string empty=Path.Combine(fixture,"empty.bin");File.WriteAllBytes(empty,new byte[0]);
   Assert(FileShredder.Overwrite(empty,3,CancellationToken.None)==0,"empty file overwrite writes nothing");
   FileShredder.Erase(empty);Assert(!File.Exists(empty),"empty file erased");
  }

  static void Passes(string fixture){
   string big=Path.Combine(fixture,"big.bin");
   var data=new byte[FileShredder.ChunkSize+12345];for(int i=0;i<data.Length;i++)data[i]=(byte)(i*7);
   File.WriteAllBytes(big,data);
   long written=FileShredder.Overwrite(big,3,CancellationToken.None);
   Assert(written==3L*data.Length,"three passes must write 3x the length across chunk boundaries, got "+written);
   FileShredder.Erase(big);
   Assert(!File.Exists(big),"big file erased");
  }
 }
}
