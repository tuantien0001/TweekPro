using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TweekPro.Cleaner {
 /// <summary>Regression tests for changed previews and incomplete vault restores using isolated files.</summary>
 public static class JunkTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("JunkTests: "+message);}
  public static void Run(){
   string parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp");
   string root=Path.Combine(parent,"TweekProJunkTests"+Guid.NewGuid().ToString("N")),source=Path.Combine(root,"source"),originalVault=Engine.Vault;
   try{
    Directory.CreateDirectory(source);Engine.Vault=Path.Combine(root,"vault");
    var rule=new JunkRule{Id="regression",Name="Regression",Paths=new List<string>{source},Patterns=new List<string>{"*.tmp"},Recurse=false,MinAgeHours=0};
    string file=Path.Combine(source,"sample.tmp");
    foreach(bool direct in new[]{false,true})foreach(bool sameSize in new[]{false,true}){
     File.WriteAllText(file,"old data");File.SetLastWriteTime(file,DateTime.Now.AddDays(-3));
     var preview=JunkCleaner.Preview(new[]{rule},true,-1,CancellationToken.None);
     Assert(preview[0].Count==1,"Fixture appears in preview");
     File.WriteAllText(file,sameSize?"new data":"longer new data");File.SetLastWriteTime(file,DateTime.Now);
     var report=JunkCleaner.Clean(preview,direct,CancellationToken.None);
     Assert(File.Exists(file)&&report.Cleaned==0&&report.Changed==1&&report.Failed==0&&report.Backups.Count==0,"Changed file survives both cleanup modes, including same-size edits");
     Assert(report.Outcomes.Count==1&&report.Outcomes[0].Kind==JunkOutcomeKind.Changed&&report.Outcomes[0].Path==file,"Changed outcome has its own category and full path");
    }
    if(Environment.OSVersion.Platform==PlatformID.Win32NT){
     rule.MinAgeHours=1;
     File.SetCreationTime(file,DateTime.Now.AddDays(-3));File.SetLastWriteTime(file,DateTime.Now.AddDays(-3));
     var preview=JunkCleaner.Preview(new[]{rule},true,48,CancellationToken.None);
     Assert(preview[0].MinAgeHours==48&&preview[0].Count==1,"Preview retains effective settings age floor");
     File.SetCreationTime(file,DateTime.Now);
     var report=JunkCleaner.Clean(preview,true,CancellationToken.None);
     Assert(File.Exists(file)&&report.Cleaned==0&&report.Changed==1&&report.Failed==0,"Recreated file with unchanged size and last-write survives");
     rule.MinAgeHours=0;
    }
    var missingPreview=JunkCleaner.Preview(new[]{rule},true,-1,CancellationToken.None);File.Delete(file);
    var missing=JunkCleaner.Clean(missingPreview,false,CancellationToken.None);
    Assert(missing.Missing==1&&missing.Failed==0&&missing.Outcomes.Single().Kind==JunkOutcomeKind.Missing,"Missing preview files are counted separately");
    File.WriteAllText(file,"original backup");
    var refusedPreview=JunkCleaner.Preview(new[]{rule},true,-1,CancellationToken.None);
    string outside=Path.Combine(root,"outside.tmp");File.WriteAllText(outside,"keep");refusedPreview[0].Items[0].Path=outside;
    var refusedReport=JunkCleaner.Clean(refusedPreview,true,CancellationToken.None);
    Assert(refusedReport.Failed==1&&refusedReport.Outcomes.Single().Kind==JunkOutcomeKind.Failed&&File.ReadAllText(outside)=="keep","Out-of-root refusal appears as an error without mutation");
    var clean=JunkCleaner.Clean(JunkCleaner.Preview(new[]{rule},true,-1,CancellationToken.None),false,CancellationToken.None);
    Assert(clean.Outcomes.Single().Kind==JunkOutcomeKind.Cleaned,"Successful move is recorded");
    var backup=clean.Backups.Single();File.WriteAllText(file,"new destination");
    bool refused=false;try{Engine.Restore(backup);}catch(IOException){refused=true;}
    Assert(refused&&backup.State=="NeedsReview"&&File.ReadAllText(file)=="new destination","Collision leaves backup retryable and destination intact");
    Assert(Engine.Backups().First(b=>b.Id==backup.Id).State=="NeedsReview"&&Engine.SelectForPurge(new[]{backup},0,DateTime.Now.AddDays(1),false).Count==0,"Incomplete restore persists and is not purgeable as restored");
    Assert(Directory.GetFiles(Path.Combine(Engine.Vault,backup.Id,"content")).Length==1,"Conflicting payload stays in vault");
    File.Delete(file);Engine.Restore(backup);
    Assert(backup.State=="Restored"&&File.ReadAllText(file)=="original backup","Retry restores original payload");
   }finally{
    Engine.Vault=originalVault;
    if(Path.GetFileName(root).StartsWith("TweekProJunkTests")&&Engine.Under(Engine.Canon(root),Engine.Canon(parent))&&Directory.Exists(root))Directory.Delete(root,true);
   }
  }
 }
}
