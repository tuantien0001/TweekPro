using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace TweekPro.Cleaner {
 /// <summary>Platform-neutral self-tests for the empty-folder finder: top-most detection, safety refusal and removal.</summary>
 public static class EmptyFolderTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("EmptyFolderTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   string root=Path.Combine(local,"Temp","TweekProEmpty"+Guid.NewGuid().ToString("N"));
   try{
    Directory.CreateDirectory(Path.Combine(root,"withfile"));File.WriteAllText(Path.Combine(root,"withfile","keep.txt"),"x");
    Directory.CreateDirectory(Path.Combine(root,"emptyA"));
    Directory.CreateDirectory(Path.Combine(root,"emptyB","emptyB1"));
    Directory.CreateDirectory(Path.Combine(root,"mixed","sub_empty"));File.WriteAllText(Path.Combine(root,"mixed","data.txt"),"y");

    MustFail(()=>EmptyFolders.Find(Path.GetPathRoot(root),CancellationToken.None),"Drive root refused");
    MustFail(()=>EmptyFolders.Find(Path.Combine(root,"missing"),CancellationToken.None),"Missing folder refused");

    var result=EmptyFolders.Find(root,CancellationToken.None);
    var names=result.Folders.Select(f=>f.Substring(root.Length).Trim('\\','/').Replace('\\','/')).OrderBy(s=>s).ToArray();
    Assert(names.SequenceEqual(new[]{"emptyA","emptyB","mixed/sub_empty"}),"Top-most empty folders: "+String.Join(", ",names));
    Assert(!result.Folders.Any(f=>f.EndsWith("emptyB1")),"Nested empty child not reported separately");
    Assert(!result.Folders.Any(f=>f.EndsWith("withfile")||f.EndsWith("mixed")),"Folders with files not reported");

    var report=EmptyFolders.Remove(root,result.Folders,CancellationToken.None);
    Assert(report.Removed==3&&report.Failed==0,"Removed three empty branches: removed="+report.Removed+" failed="+report.Failed);
    Assert(!Directory.Exists(Path.Combine(root,"emptyA"))&&!Directory.Exists(Path.Combine(root,"emptyB"))&&!Directory.Exists(Path.Combine(root,"mixed","sub_empty")),"Empty folders deleted");
    Assert(File.Exists(Path.Combine(root,"withfile","keep.txt"))&&File.Exists(Path.Combine(root,"mixed","data.txt")),"Files and their folders kept");

    Assert(EmptyFolders.Find(root,CancellationToken.None).Folders.Count==0,"No empty folders remain after removal");
   }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
  }
 }
}
