using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TweekPro.Core;

namespace TweekPro.Health {
 /// <summary>How much attention one area needs; Good adds no penalty.</summary>
 public enum HealthSeverity { Good, Low, Medium, High }

 /// <summary>One measured area of the machine with a plain-language verdict and the tab that acts on it.</summary>
 public class HealthFinding {
  public string Area; public string Verdict; public string Detail; public string Tab;
  public HealthSeverity Severity; public long Bytes; public int Count; public bool Measured=true;
 }

 /// <summary>Raw measurements gathered by the UI probes; keeping them as plain data makes scoring pure and testable.</summary>
 public class HealthInputs {
  public long JunkBytes; public int JunkFiles; public int JunkLockedRules; public bool JunkMeasured=true;
  public int VaultBackups; public long VaultBytes; public int VaultStale; public long VaultStaleBytes; public int VaultStaleDays=90; public bool VaultMeasured=true;
  public int AutorunEntries; public int AutorunBroken; public bool AutorunMeasured=true;
  public int EmptyFolders; public string EmptyRoot; public bool EmptyMeasured=true;
  public int LeftoverCandidates; public bool LeftoverMeasured=true;
  public long DiskFreeBytes; public long DiskTotalBytes; public string DiskName; public bool DiskMeasured=true;
  public int PupCount; public int PupHigh; public bool PupMeasured=true;
 }

 /// <summary>Free and total bytes of one fixed drive, drawn as a ring on the Overview card.</summary>
 public class DriveGauge {
  public string Name; public long FreeBytes; public long TotalBytes; public string Label;
  public double UsedFraction { get { return TotalBytes<=0?0:Math.Min(1.0,Math.Max(0.0,1.0-(double)FreeBytes/TotalBytes)); } }
  public HealthSeverity Level { get { return HealthCheck.DriveLevel(FreeBytes,TotalBytes); } }
 }

 /// <summary>Score, grade and the ordered list of findings produced by one health check.</summary>
 public class HealthReport {
  public List<HealthFinding> Findings=new List<HealthFinding>();
  public int Score; public string Grade; public string GradeLabel; public string Headline; public long Reclaimable; public DateTime Generated;
  public int Issues { get { return Findings.Count(f=>f.Measured&&f.Severity!=HealthSeverity.Good); } }
 }

 /// <summary>
 /// Aggregates junk, leftovers, empty folders, vault, autorun and free-space measurements into one 0–100 score.
 /// Thresholds are deliberately conservative: only space that Tweek Pro can free safely counts as reclaimable
 /// (rule-matched junk and vault backups older than the purge age; restored backups are already empty).
 /// </summary>
 public static class HealthCheck {
  public const long MB=1024L*1024, GB=1024L*MB;
  public const int PenaltyLow=5, PenaltyMedium=12, PenaltyHigh=25;
  public const string TabJunk="Dọn rác", TabLeftovers="Phần còn sót", TabEmpty="Thư mục rỗng", TabVault="Kho khôi phục", TabAutorun="Khởi động", TabAnalyzer="Phân tích ổ đĩa", TabApps="Ứng dụng";

  /// <summary>Builds the full report from raw inputs in a stable area order.</summary>
  public static HealthReport Evaluate(HealthInputs i){
   if(i==null)throw new ArgumentNullException("i");
   var report=new HealthReport{Generated=DateTime.Now};
   report.Findings.Add(Junk(i));
   report.Findings.Add(Leftovers(i));
   report.Findings.Add(Empty(i));
   report.Findings.Add(Vault(i));
   report.Findings.Add(Autorun(i));
   report.Findings.Add(Disk(i));
   report.Findings.Add(Pup(i));
   report.Score=Score(report.Findings);
   report.Grade=Grade(report.Score);report.GradeLabel=GradeLabel(report.Grade);
   report.Reclaimable=report.Findings.Where(f=>f.Measured).Sum(f=>f.Bytes);
   report.Headline=Headline(report.Score,report.Reclaimable,report.Issues);
   return report;
  }

  /// <summary>100 minus a fixed penalty per measured finding, clamped to 0–100; unmeasured areas never count.</summary>
  public static int Score(IEnumerable<HealthFinding> findings){
   int score=100;
   foreach(var f in findings){if(!f.Measured)continue;score-=Penalty(f.Severity);}
   return Math.Max(0,Math.Min(100,score));
  }

  public static int Penalty(HealthSeverity s){return s==HealthSeverity.High?PenaltyHigh:s==HealthSeverity.Medium?PenaltyMedium:s==HealthSeverity.Low?PenaltyLow:0;}

  /// <summary>Letter grade: A ≥90, B ≥75, C ≥60, D ≥40, otherwise E.</summary>
  public static string Grade(int score){return score>=90?"A":score>=75?"B":score>=60?"C":score>=40?"D":"E";}

  public static string GradeLabel(string grade){
   switch(grade){case "A":return L.T("Rất tốt");case "B":return L.T("Tốt");case "C":return L.T("Nên dọn");case "D":return L.T("Cần dọn");default:return L.T("Cần xử lý ngay");}
  }

  public static string SeverityLabel(HealthSeverity s){return s==HealthSeverity.High?L.T("Khẩn"):s==HealthSeverity.Medium?L.T("Cần dọn"):s==HealthSeverity.Low?L.T("Nên xem"):L.T("Tốt");}

  /// <summary>One-sentence summary for the gauge card.</summary>
  public static string Headline(int score,long reclaimable,int issues){
   if(issues==0)return L.T("Máy đang sạch. Không có gì cần dọn ở các khu vực đã kiểm tra.");
   string space=reclaimable>0?L.F(" Có thể giải phóng khoảng {0}.",Presentation.BytesLabel(reclaimable)):"";
   return L.F("{0} khu vực cần chú ý.",issues)+space+(score<60?L.T(" Nên dọn ngay để máy nhẹ hơn."):"");
  }

  static HealthFinding Junk(HealthInputs i){
   var f=new HealthFinding{Area=L.T("Tệp rác"),Tab=TabJunk,Measured=i.JunkMeasured,Bytes=i.JunkBytes,Count=i.JunkFiles};
   if(!i.JunkMeasured){Unmeasured(f);return f;}
   f.Severity=i.JunkBytes>=2*GB?HealthSeverity.High:i.JunkBytes>=500*MB?HealthSeverity.Medium:i.JunkBytes>=100*MB?HealthSeverity.Low:HealthSeverity.Good;
   f.Verdict=L.T(f.Severity==HealthSeverity.Good?"Rất ít tệp rác":f.Severity==HealthSeverity.Low?"Có một ít tệp rác":f.Severity==HealthSeverity.Medium?"Nhiều tệp rác":"Rất nhiều tệp rác");
   f.Detail=L.F("{0} tệp, {1}",i.JunkFiles.ToString("N0"),Presentation.BytesLabel(i.JunkBytes))+(i.JunkLockedRules>0?L.F("; {0} nhóm đang bị khóa (trình duyệt còn chạy hoặc cần quyền quản trị)",i.JunkLockedRules):"");
   return f;
  }

  static HealthFinding Leftovers(HealthInputs i){
   var f=new HealthFinding{Area=L.T("Phần còn sót"),Tab=TabLeftovers,Measured=i.LeftoverMeasured,Count=i.LeftoverCandidates};
   if(!i.LeftoverMeasured){Unmeasured(f);return f;}
   f.Severity=i.LeftoverCandidates==0?HealthSeverity.Good:i.LeftoverCandidates<=5?HealthSeverity.Low:HealthSeverity.Medium;
   f.Verdict=L.T(f.Severity==HealthSeverity.Good?"Không có mục chờ duyệt":"Có mục còn sót chờ duyệt");
   f.Detail=i.LeftoverCandidates==0?L.T("Danh sách chờ duyệt trống."):L.F("{0} mục từ các lần gỡ trước chưa được xử lý.",i.LeftoverCandidates);
   return f;
  }

  static HealthFinding Empty(HealthInputs i){
   var f=new HealthFinding{Area=L.T("Thư mục rỗng"),Tab=TabEmpty,Measured=i.EmptyMeasured,Count=i.EmptyFolders};
   if(!i.EmptyMeasured){Unmeasured(f);return f;}
   f.Severity=i.EmptyFolders==0?HealthSeverity.Good:HealthSeverity.Low;
   f.Verdict=L.T(i.EmptyFolders==0?"Không có thư mục rỗng":"Có thư mục rỗng");
   f.Detail=(i.EmptyFolders==0?L.T("Không tìm thấy nhánh rỗng"):L.F("{0} nhánh thư mục hoàn toàn rỗng",i.EmptyFolders))+(String.IsNullOrEmpty(i.EmptyRoot)?"":L.F(" trong {0}",i.EmptyRoot))+".";
   return f;
  }

  static HealthFinding Vault(HealthInputs i){
   var f=new HealthFinding{Area=L.T("Kho khôi phục"),Tab=TabVault,Measured=i.VaultMeasured,Bytes=i.VaultStaleBytes,Count=i.VaultStale};
   if(!i.VaultMeasured){Unmeasured(f);return f;}
   f.Severity=i.VaultStaleBytes>=1*GB?HealthSeverity.Medium:i.VaultStaleBytes>=200*MB?HealthSeverity.Low:HealthSeverity.Good;
   f.Verdict=L.T(i.VaultBackups==0?"Kho trống":f.Severity==HealthSeverity.Good?"Kho gọn":"Kho giữ bản sao lưu cũ");
   f.Detail=i.VaultBackups==0?L.T("Chưa có bản sao lưu."):L.F("{0} bản sao lưu ({1}); {2} bản cũ hơn {3} ngày có thể xóa vĩnh viễn bằng Dọn kho theo tuổi… ({4}).",i.VaultBackups,Presentation.BytesLabel(i.VaultBytes),i.VaultStale,i.VaultStaleDays,Presentation.BytesLabel(i.VaultStaleBytes));
   return f;
  }

  static HealthFinding Autorun(HealthInputs i){
   var f=new HealthFinding{Area=L.T("Khởi động cùng Windows"),Tab=TabAutorun,Measured=i.AutorunMeasured,Count=i.AutorunEntries};
   if(!i.AutorunMeasured){Unmeasured(f);return f;}
   f.Severity=i.AutorunEntries<=8?HealthSeverity.Good:i.AutorunEntries<=15?HealthSeverity.Low:i.AutorunEntries<=25?HealthSeverity.Medium:HealthSeverity.High;
   f.Verdict=L.T(f.Severity==HealthSeverity.Good?"Ít mục khởi động":f.Severity==HealthSeverity.Low?"Khá nhiều mục khởi động":"Quá nhiều mục khởi động");
   if(i.AutorunBroken>0&&f.Severity==HealthSeverity.Good){f.Severity=HealthSeverity.Low;f.Verdict=L.T("Có mục khởi động hỏng");}
   f.Detail=i.AutorunBroken>0?L.F("{0} mục đang bật khi đăng nhập (Run, Startup, ứng dụng Windows); {1} mục trỏ tới tệp không còn tồn tại. Mỗi mục kéo dài thời gian mở máy.",i.AutorunEntries,i.AutorunBroken):L.F("{0} mục đang bật khi đăng nhập (Run, Startup, ứng dụng Windows). Mỗi mục kéo dài thời gian mở máy.",i.AutorunEntries);
   return f;
  }

  /// <summary>Free-space severity shared by the system-drive finding and the drive rings: 20% good, 10% low, 5% medium, else high.</summary>
  public static HealthSeverity DriveLevel(long free,long total){
   if(total<=0)return HealthSeverity.Good;
   double ratio=(double)free/total;
   return ratio>=0.20?HealthSeverity.Good:ratio>=0.10?HealthSeverity.Low:ratio>=0.05?HealthSeverity.Medium:HealthSeverity.High;
  }

  /// <summary>Ready fixed drives in letter order with a "free / total" label; drives that cannot report a size are skipped, never thrown.</summary>
  public static List<DriveGauge> ReadDrives(){
   var list=new List<DriveGauge>();
   System.IO.DriveInfo[] drives;
   try{drives=System.IO.DriveInfo.GetDrives();}catch(Exception){return list;}
   foreach(var d in drives){
    try{
     if(d.DriveType!=System.IO.DriveType.Fixed||!d.IsReady||d.TotalSize<=0)continue;
     var g=new DriveGauge{Name=d.Name.TrimEnd('\\','/'),FreeBytes=d.AvailableFreeSpace,TotalBytes=d.TotalSize};
     g.Label=DriveLabel(g);list.Add(g);
    }catch(Exception){}
   }
   return list.OrderBy(g=>g.Name,StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>"38 GB trống / 476 GB" for a drive ring caption.</summary>
  public static string DriveLabel(DriveGauge g){return L.F("{0} trống / {1}",Presentation.BytesLabel(g.FreeBytes),Presentation.BytesLabel(g.TotalBytes));}

  static HealthFinding Disk(HealthInputs i){
   var f=new HealthFinding{Area=L.T("Dung lượng trống"),Tab=TabAnalyzer,Measured=i.DiskMeasured&&i.DiskTotalBytes>0};
   if(!f.Measured){Unmeasured(f);return f;}
   double ratio=(double)i.DiskFreeBytes/i.DiskTotalBytes;
   f.Severity=DriveLevel(i.DiskFreeBytes,i.DiskTotalBytes);
   f.Verdict=L.T(f.Severity==HealthSeverity.Good?"Còn đủ chỗ trống":f.Severity==HealthSeverity.Low?"Chỗ trống bắt đầu ít":f.Severity==HealthSeverity.Medium?"Sắp đầy":"Gần như đầy");
   f.Detail=L.F("{0} còn trống {1} / {2} ({3}%).",String.IsNullOrEmpty(i.DiskName)?L.T("Ổ hệ thống"):i.DiskName,Presentation.BytesLabel(i.DiskFreeBytes),Presentation.BytesLabel(i.DiskTotalBytes),(ratio*100).ToString("N0"));
   return f;
  }

  /// <summary>PUP/bloatware: any high-severity match is High; 3+ items Medium; 1–2 Low. Counts only, no bytes (removal goes through the uninstall flows).</summary>
  static HealthFinding Pup(HealthInputs i){
   var f=new HealthFinding{Area=L.T("Ứng dụng không mong muốn"),Tab=TabApps,Measured=i.PupMeasured,Count=i.PupCount};
   if(!i.PupMeasured){Unmeasured(f);return f;}
   f.Severity=i.PupCount==0?HealthSeverity.Good:i.PupHigh>0?HealthSeverity.High:i.PupCount>=3?HealthSeverity.Medium:HealthSeverity.Low;
   f.Verdict=L.T(f.Severity==HealthSeverity.Good?"Không có phần mềm không mong muốn":f.Severity==HealthSeverity.High?"Có phần mềm nên gỡ":"Có phần mềm không mong muốn");
   f.Detail=i.PupCount==0?L.T("Không mục nào khớp quy tắc PUP/bloatware."):L.F("{0} mục ({1} nên gỡ). Xem cột Cảnh báo ở tab Ứng dụng / Ứng dụng Windows.",i.PupCount,i.PupHigh);
   return f;
  }

  static void Unmeasured(HealthFinding f){f.Severity=HealthSeverity.Good;f.Verdict=L.T("Chưa đo được");f.Detail=L.T("Không đọc được dữ liệu khu vực này trong lần kiểm tra vừa rồi.");f.Bytes=0;}

  /// <summary>Plain-text report suitable for the clipboard or the log.</summary>
  public static string Summary(HealthReport r){
   var sb=new StringBuilder();
   sb.AppendLine(L.F("Tweek Pro – Kiểm tra sức khỏe máy {0}",r.Generated.ToString("dd/MM/yyyy HH:mm")));
   sb.AppendLine(L.F("Điểm: {0}/100 ({1} – {2}). {3}",r.Score,r.Grade,r.GradeLabel,r.Headline));
   foreach(var f in r.Findings)sb.AppendLine("- "+f.Area+": "+f.Verdict+" ["+(f.Measured?SeverityLabel(f.Severity):L.T("chưa đo"))+"] – "+f.Detail);
   return sb.ToString();
  }
 }
}
