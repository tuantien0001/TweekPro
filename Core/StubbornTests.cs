using System;
using System.IO;
using System.Linq;

namespace TweekPro.Core {
 /// <summary>Self-tests for the stubborn-file escalation ladder; platform-neutral (ownership and reboot scheduling are Windows-only and only checked for graceful no-ops here).</summary>
 public static class StubbornTests {
  static void Assert(bool ok,string what){if(!ok)throw new Exception("Stubborn test failed: "+what);}
  static void MustFail<T>(Action a,string what) where T:Exception{try{a();}catch(T){return;}throw new Exception("Stubborn test failed: expected "+typeof(T).Name+" for "+what);}

  public static void Run(){
   string root=Path.Combine(Path.GetTempPath(),"tweekpro-stubborn-"+Guid.NewGuid().ToString("N"));
   try{
    Directory.CreateDirectory(Path.Combine(root,"sub"));
    string a=Path.Combine(root,"a.txt"),b=Path.Combine(root,"sub","b.txt"),c=Path.Combine(root,"sub","c.txt");
    File.WriteAllText(a,"a");File.WriteAllText(b,"b");File.WriteAllText(c,"c");
    File.SetAttributes(a,FileAttributes.ReadOnly);File.SetAttributes(b,FileAttributes.ReadOnly);
    Assert((File.GetAttributes(a)&FileAttributes.ReadOnly)!=0,"read-only set for the test");

    int cleared=StubbornFiles.ClearAttributes(root);
    Assert(cleared>=2,"attributes cleared on the tree ("+cleared+")");
    Assert((File.GetAttributes(a)&FileAttributes.ReadOnly)==0&&(File.GetAttributes(b)&FileAttributes.ReadOnly)==0,"read-only removed recursively");
    Assert(StubbornFiles.ClearAttributes(root)==0,"second pass finds nothing to clear");
    Assert(StubbornFiles.ClearAttributes(Path.Combine(root,"missing"))==0,"missing path is a no-op");

    // Ladder: permission error → clear attributes → retry succeeds.
    File.SetAttributes(c,FileAttributes.ReadOnly);
    int calls=0;
    var outcome=StubbornFiles.Escalate(root,()=>{calls++;if(calls==1)throw new UnauthorizedAccessException("denied");});
    Assert(outcome.Succeeded&&outcome.Attempts==2&&outcome.AttributesCleared==1&&calls==2,"retry after clearing attributes");
    Assert(outcome.Steps.Length>0,"steps described: "+outcome.Steps);

    // Ladder: first attempt succeeds → nothing escalated.
    var direct=StubbornFiles.Escalate(root,()=>{});
    Assert(direct.Succeeded&&direct.Attempts==1&&direct.AttributesCleared==0&&!direct.TookOwnership&&direct.Steps=="","no escalation when not needed");

    // Ladder: permission error that never clears → ladder exhausted, original exception surfaces, bounded attempts.
    int always=0;
    MustFail<UnauthorizedAccessException>(()=>StubbornFiles.Escalate(root,()=>{always++;throw new UnauthorizedAccessException("still denied");}),"exhausted ladder");
    Assert(always>=1&&always<=3,"bounded retries ("+always+")");

    // Locked files are not retried: the lock will not go away by changing ACLs.
    int locked=0;var sharing=new IOException("in use",unchecked((int)0x80070020));
    Assert(StubbornFiles.IsLocked(sharing)&&!StubbornFiles.IsAccessDenied(sharing),"sharing violation classified as locked");
    MustFail<IOException>(()=>StubbornFiles.Escalate(root,()=>{locked++;throw sharing;}),"locked path");
    Assert(locked==1,"locked path attempted once");

    // Unrelated I/O errors surface immediately.
    int other=0;
    MustFail<IOException>(()=>StubbornFiles.Escalate(root,()=>{other++;throw new IOException("disk full");}),"unrelated error");
    Assert(other==1,"unrelated error not retried");
    Assert(StubbornFiles.IsAccessDenied(new UnauthorizedAccessException())&&StubbornFiles.IsAccessDenied(new IOException("x",unchecked((int)0x80070005)))&&!StubbornFiles.IsAccessDenied(new IOException("y")),"access-denied classification");

    if(!StubbornFiles.IsWindows){
     Assert(!StubbornFiles.TakeOwnership(root),"ownership takeover is a no-op off Windows");
     Assert(StubbornFiles.ScheduleDeleteOnReboot(root)==0,"reboot scheduling is a no-op off Windows");
     // Engine refuses to record a PendingReboot entry when Windows did not accept the schedule.
     int before=Engine.Backups().Count;
     MustFail<IOException>(()=>Engine.ScheduleOnReboot(new Candidate{Kind="Folder",Path=root,AppId="HKCU|64|SOFTWARE\\TweekProStubbornTestMissing",AppName="Stubborn"}),"schedule without OS support");
     Assert(Engine.Backups().Count==before,"no vault entry without a schedule");
    }
    Assert(Directory.Exists(root)&&File.Exists(a)&&File.Exists(b)&&File.Exists(c),"test tree untouched by the ladder");
   }finally{try{StubbornFiles.ClearAttributes(root);Directory.Delete(root,true);}catch(Exception){}}
  }
 }
}
