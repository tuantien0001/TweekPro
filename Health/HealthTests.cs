using System;
using System.Linq;

namespace TweekPro.Health {
 /// <summary>Platform-neutral checks for the health scoring engine: thresholds, penalties, grades, unmeasured areas and reclaimable totals.</summary>
 public static class HealthTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("HealthTests: "+message);}

  static HealthInputs Clean(){return new HealthInputs{DiskFreeBytes=200*HealthCheck.GB,DiskTotalBytes=500*HealthCheck.GB,DiskName="Ổ C:"};}

  public static void Run(){
   var clean=HealthCheck.Evaluate(Clean());
   Assert(clean.Score==100&&clean.Grade=="A","clean machine scores 100/A, got "+clean.Score+"/"+clean.Grade);
   Assert(clean.Findings.Count==6,"six areas");
   Assert(clean.Findings.All(f=>f.Measured&&f.Severity==HealthSeverity.Good),"all good");
   Assert(clean.Issues==0&&clean.Reclaimable==0,"no issues, nothing reclaimable");
   Assert(clean.Findings.Select(f=>f.Area).SequenceEqual(new[]{"Tệp rác","Phần còn sót","Thư mục rỗng","Kho khôi phục","Khởi động cùng Windows","Dung lượng trống"}),"stable area order");
   Assert(clean.Findings[0].Tab==HealthCheck.TabJunk&&clean.Findings[5].Tab==HealthCheck.TabAnalyzer,"tab hints");

   // Junk thresholds: 100 MB low, 500 MB medium, 2 GB high; bytes count as reclaimable.
   var junk=Clean();junk.JunkBytes=99*HealthCheck.MB;junk.JunkFiles=10;
   Assert(HealthCheck.Evaluate(junk).Findings[0].Severity==HealthSeverity.Good,"junk <100MB good");
   junk.JunkBytes=100*HealthCheck.MB;Assert(HealthCheck.Evaluate(junk).Findings[0].Severity==HealthSeverity.Low,"junk 100MB low");
   junk.JunkBytes=500*HealthCheck.MB;Assert(HealthCheck.Evaluate(junk).Findings[0].Severity==HealthSeverity.Medium,"junk 500MB medium");
   junk.JunkBytes=3*HealthCheck.GB;var heavy=HealthCheck.Evaluate(junk);
   Assert(heavy.Findings[0].Severity==HealthSeverity.High,"junk 3GB high");
   Assert(heavy.Score==100-HealthCheck.PenaltyHigh&&heavy.Grade=="B","one high finding: "+heavy.Score+"/"+heavy.Grade);
   Assert(heavy.Reclaimable==3*HealthCheck.GB,"junk bytes reclaimable");
   Assert(heavy.Issues==1&&heavy.Headline.Contains("1 khu vực"),"headline counts issues");
   junk.JunkLockedRules=2;Assert(HealthCheck.Evaluate(junk).Findings[0].Detail.Contains("2 nhóm đang bị khóa"),"locked rules mentioned");

   // Leftovers: 1–5 low, more medium.
   var left=Clean();left.LeftoverCandidates=3;Assert(HealthCheck.Evaluate(left).Findings[1].Severity==HealthSeverity.Low,"3 leftovers low");
   left.LeftoverCandidates=6;Assert(HealthCheck.Evaluate(left).Findings[1].Severity==HealthSeverity.Medium,"6 leftovers medium");

   // Empty folders never exceed low.
   var empty=Clean();empty.EmptyFolders=400;empty.EmptyRoot=@"C:\Users\a\Downloads";var er=HealthCheck.Evaluate(empty);
   Assert(er.Findings[2].Severity==HealthSeverity.Low&&er.Findings[2].Detail.Contains("Downloads"),"empty folders low with root");

   // Vault: only restored backups count as reclaimable; 200 MB low, 1 GB medium.
   var vault=Clean();vault.VaultBackups=5;vault.VaultBytes=3*HealthCheck.GB;vault.VaultRestored=0;
   Assert(HealthCheck.Evaluate(vault).Findings[3].Severity==HealthSeverity.Good&&HealthCheck.Evaluate(vault).Reclaimable==0,"unrestored backups are not reclaimable");
   vault.VaultRestored=2;vault.VaultRestoredBytes=200*HealthCheck.MB;Assert(HealthCheck.Evaluate(vault).Findings[3].Severity==HealthSeverity.Low,"restored 200MB low");
   vault.VaultRestoredBytes=HealthCheck.GB;var vr=HealthCheck.Evaluate(vault);
   Assert(vr.Findings[3].Severity==HealthSeverity.Medium&&vr.Reclaimable==HealthCheck.GB,"restored 1GB medium and reclaimable");
   var emptyVault=Clean();Assert(HealthCheck.Evaluate(emptyVault).Findings[3].Verdict=="Kho trống","empty vault verdict");

   // Autorun: ≤8 good, ≤15 low, ≤25 medium, else high.
   var auto=Clean();auto.AutorunEntries=8;Assert(HealthCheck.Evaluate(auto).Findings[4].Severity==HealthSeverity.Good,"8 autoruns good");
   auto.AutorunEntries=9;Assert(HealthCheck.Evaluate(auto).Findings[4].Severity==HealthSeverity.Low,"9 autoruns low");
   auto.AutorunEntries=16;Assert(HealthCheck.Evaluate(auto).Findings[4].Severity==HealthSeverity.Medium,"16 autoruns medium");
   auto.AutorunEntries=26;Assert(HealthCheck.Evaluate(auto).Findings[4].Severity==HealthSeverity.High,"26 autoruns high");

   // Disk: 20% good, 10% low, 5% medium, else high; missing total means unmeasured.
   var disk=Clean();disk.DiskFreeBytes=50*HealthCheck.GB;Assert(HealthCheck.Evaluate(disk).Findings[5].Severity==HealthSeverity.Low,"10% free low");
   disk.DiskFreeBytes=30*HealthCheck.GB;Assert(HealthCheck.Evaluate(disk).Findings[5].Severity==HealthSeverity.Medium,"6% free medium");
   disk.DiskFreeBytes=10*HealthCheck.GB;var dr=HealthCheck.Evaluate(disk);
   Assert(dr.Findings[5].Severity==HealthSeverity.High&&dr.Findings[5].Detail.Contains("2%"),"2% free high with percentage");
   disk.DiskTotalBytes=0;Assert(!HealthCheck.Evaluate(disk).Findings[5].Measured,"zero total is unmeasured");

   // Unmeasured areas add no penalty and are labelled as such.
   var partial=Clean();partial.JunkMeasured=false;partial.AutorunMeasured=false;partial.JunkBytes=5*HealthCheck.GB;var pr=HealthCheck.Evaluate(partial);
   Assert(pr.Score==100&&!pr.Findings[0].Measured&&pr.Findings[0].Verdict=="Chưa đo được"&&pr.Reclaimable==0,"unmeasured areas ignored");
   Assert(HealthCheck.Summary(pr).Contains("[chưa đo]"),"summary marks unmeasured");

   // Penalties accumulate and clamp at zero.
   var worst=new HealthInputs{JunkBytes=10*HealthCheck.GB,LeftoverCandidates=50,EmptyFolders=10,VaultBackups=3,VaultRestored=3,VaultRestoredBytes=5*HealthCheck.GB,AutorunEntries=40,DiskFreeBytes=1,DiskTotalBytes=100*HealthCheck.GB};
   var wr=HealthCheck.Evaluate(worst);
   int expected=100-HealthCheck.PenaltyHigh-HealthCheck.PenaltyMedium-HealthCheck.PenaltyLow-HealthCheck.PenaltyMedium-HealthCheck.PenaltyHigh-HealthCheck.PenaltyHigh;
   Assert(wr.Score==Math.Max(0,expected),"cumulative penalties: "+wr.Score+" vs "+expected);
   Assert(wr.Grade=="E"&&wr.Issues==6,"worst case grade E with six issues");
   Assert(wr.Reclaimable==15*HealthCheck.GB,"reclaimable sums junk + restored vault");
   var hundredHighs=Enumerable.Range(0,10).Select(_=>new HealthFinding{Severity=HealthSeverity.High}).ToList();
   Assert(HealthCheck.Score(hundredHighs)==0,"score clamps at zero");

   // Grade boundaries.
   Assert(HealthCheck.Grade(90)=="A"&&HealthCheck.Grade(89)=="B"&&HealthCheck.Grade(75)=="B"&&HealthCheck.Grade(74)=="C"&&HealthCheck.Grade(60)=="C"&&HealthCheck.Grade(59)=="D"&&HealthCheck.Grade(40)=="D"&&HealthCheck.Grade(39)=="E","grade boundaries");
   Assert(HealthCheck.GradeLabel("A")=="Rất tốt"&&HealthCheck.GradeLabel("E")=="Cần xử lý ngay","grade labels");
   Assert(HealthCheck.SeverityLabel(HealthSeverity.High)=="Khẩn"&&HealthCheck.SeverityLabel(HealthSeverity.Good)=="Tốt","severity labels");
   Assert(HealthCheck.Headline(100,0,0).StartsWith("Máy đang sạch"),"clean headline");
   Assert(HealthCheck.Headline(50,HealthCheck.GB,3).Contains("Nên dọn ngay"),"low score urges cleanup");

   // Renderer colors follow the score.
   Assert(HealthRenderer.ScoreColor(90)==Theme.Success&&HealthRenderer.ScoreColor(20)==Theme.Danger,"score colors");
   Assert(HealthRenderer.SeverityColor(HealthSeverity.Good)==Theme.Success&&HealthRenderer.SeverityColor(HealthSeverity.High)==Theme.Danger,"severity colors");
  }
 }
}
