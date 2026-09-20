using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace TweekPro.Analyzer {
 /// <summary>
 /// Platform-neutral self-tests for the disk analyser: totals, per-child-folder sizes, usage by extension,
 /// largest-file ordering and the bounded top-N. Fixtures live under %LOCALAPPDATA%\Temp\TweekProAnalyze*.
 /// </summary>
 public static class AnalyzerTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("AnalyzerTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   string root=Path.Combine(local,"Temp","TweekProAnalyze"+Guid.NewGuid().ToString("N"));
   try{
    Directory.CreateDirectory(Path.Combine(root,"sub1"));
    Directory.CreateDirectory(Path.Combine(root,"sub2","nested"));
    Write(Path.Combine(root,"a.txt"),100);
    Write(Path.Combine(root,"big.bin"),5000);
    Write(Path.Combine(root,"sub1","b.txt"),200);
    Write(Path.Combine(root,"sub1","c.log"),300);
    Write(Path.Combine(root,"sub2","d.bin"),4000);
    Write(Path.Combine(root,"sub2","nested","e.txt"),50);

    MustFail(()=>DiskAnalyzer.Analyze(Path.Combine(root,"missing"),100,CancellationToken.None),"Missing folder refused");

    var report=DiskAnalyzer.Analyze(root,100,CancellationToken.None);
    Assert(report.TotalFiles==6&&report.TotalBytes==9650,"Totals: files="+report.TotalFiles+" bytes="+report.TotalBytes);
    Assert(!report.Partial,"Small tree scanned fully");

    // Per-child-folder sizes plus the root-direct bucket.
    long rootBucket=report.Folders.First(f=>f.Path.Equals(Engine.Canon(root),StringComparison.OrdinalIgnoreCase)).Bytes;
    long sub1=report.Folders.First(f=>f.Path.EndsWith("sub1")).Bytes;
    long sub2=report.Folders.First(f=>f.Path.EndsWith("sub2")).Bytes;
    Assert(rootBucket==5100&&sub1==500&&sub2==4050,"Folder buckets: root="+rootBucket+" sub1="+sub1+" sub2="+sub2);
    Assert(report.Folders[0].Bytes==5100,"Heaviest folder listed first");

    // Usage by extension, largest first.
    var bin=report.Extensions.First(e=>e.Extension==".bin");
    var txt=report.Extensions.First(e=>e.Extension==".txt");
    var log=report.Extensions.First(e=>e.Extension==".log");
    Assert(bin.Bytes==9000&&bin.Files==2,"bin extension aggregated");
    Assert(txt.Bytes==350&&txt.Files==3,"txt extension aggregated");
    Assert(log.Bytes==300&&log.Files==1,"log extension aggregated");
    Assert(report.Extensions[0].Extension==".bin","Largest extension first");

    // Largest files in descending order.
    Assert(report.LargestFiles.Count==6,"All files listed when top exceeds count");
    Assert(report.LargestFiles[0].Bytes==5000&&report.LargestFiles[1].Bytes==4000&&report.LargestFiles[5].Bytes==50,"Largest files ordered");

    // Bounded top-N returns exactly the biggest files.
    var top2=DiskAnalyzer.Analyze(root,2,CancellationToken.None);
    Assert(top2.LargestFiles.Count==2&&top2.LargestFiles[0].Bytes==5000&&top2.LargestFiles[1].Bytes==4000,"Top-2 keeps only the two largest");
    Assert(top2.TotalFiles==6&&top2.TotalBytes==9650,"Totals independent of top-N");
   }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
  }

  static void Write(string path,int bytes){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,new byte[bytes]);}
 }
}
