using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Cleaner;
using TweekPro.Health;

namespace TweekPro {
 public partial class MainForm {
  ListView healthList=new SmoothListView();Label healthOverlay,healthStage;TabPage healthTab;HealthGaugePanel healthGauge=new HealthGaugePanel();
  HealthReport healthReport;

  /// <summary>Builds the Overview tab: one read-only health check that scores the machine and points to the tab that fixes each finding.</summary>
  void BuildHealthTab(){
   var tab=healthTab=new TabPage("Tổng quan");
   SetupList(healthList,new[]{"Khu vực","Đánh giá","Chi tiết","Có thể giải phóng","Mức","Xử lý ở tab"},new[]{180,210,470,130,90,150},false);
   healthList.DoubleClick+=(s,e)=>OpenHealthTab();
   var host=Theme.ListHost(healthList,out healthOverlay);

   var bar=Bar();
   Add(bar,"Kiểm tra ngay",async()=>await RunHealthCheck(),ButtonStyle.Primary);
   Add(bar,"Mở tab xử lý",()=>{OpenHealthTab();return Task.FromResult(0);});
   Add(bar,"Sao chép báo cáo",()=>{CopyHealthReport();return Task.FromResult(0);});

   healthGauge.Dock=DockStyle.Top;healthGauge.Height=176;
   healthStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   var note=Theme.Note("Kiểm tra sức khỏe chỉ đọc: đo tệp rác theo quy tắc, mục còn sót chờ duyệt, thư mục rỗng trong Downloads, bản đã khôi phục còn chiếm chỗ trong Kho, số mục khởi động và dung lượng trống ổ hệ thống. Không xóa gì; bấm đúp một dòng để mở tab xử lý tương ứng.",NoteKind.Info);
   tab.Controls.Add(host);tab.Controls.Add(healthStage);tab.Controls.Add(note);tab.Controls.Add(healthGauge);tab.Controls.Add(bar);
   Theme.SetOverlay(healthOverlay,"Chưa kiểm tra.\r\nBấm Kiểm tra ngay để chấm điểm máy. Mỗi dòng kết quả chỉ ra tab có thể dọn an toàn.",NoteKind.Info);
  }

  /// <summary>Runs every probe on a worker thread, then scores and renders the report.</summary>
  async Task RunHealthCheck(){
   healthStage.Visible=true;healthGauge.Report=null;healthGauge.BusyText="Đang kiểm tra…";
   var inputs=new HealthInputs{LeftoverCandidates=candidates.Count};
   bool elevated=Core.Elevation.IsElevated;int minAge=settings.JunkMinAgeHours;
   Action<string> stage=text=>{try{BeginInvoke((Action)(()=>healthStage.Text=text));}catch(InvalidOperationException){}};
   await Task.Run(()=>{
    stage("Đang đo tệp rác theo quy tắc…");
    try{var rules=JunkRules.Load(Core.Paths.JunkRulesOverride).Rules;var preview=JunkCleaner.Preview(rules,elevated,minAge,CancellationToken.None);
     inputs.JunkBytes=preview.Where(r=>!r.Locked).Sum(r=>r.Bytes);inputs.JunkFiles=preview.Where(r=>!r.Locked).Sum(r=>r.Count);inputs.JunkLockedRules=preview.Count(r=>r.Locked);}
    catch(Exception e){inputs.JunkMeasured=false;Core.Log.Warn("Health: junk probe failed: "+e.Message);}
    stage("Đang đo kho khôi phục…");
    try{var all=Engine.Backups();inputs.VaultBackups=all.Count;
     foreach(var b in all){long bytes=Engine.BackupSize(b);if(bytes>0)inputs.VaultBytes+=bytes;if(b.State=="Restored"){inputs.VaultRestored++;if(bytes>0)inputs.VaultRestoredBytes+=bytes;}}}
    catch(Exception e){inputs.VaultMeasured=false;Core.Log.Warn("Health: vault probe failed: "+e.Message);}
    stage("Đang đếm mục khởi động…");
    try{inputs.AutorunEntries=Advanced.Autoruns().Count(a=>a.State=="Có đăng ký");}
    catch(Exception e){inputs.AutorunMeasured=false;Core.Log.Warn("Health: autorun probe failed: "+e.Message);}
    stage("Đang tìm thư mục rỗng trong Downloads…");
    try{string root=DefaultEmptyRoot();if(root==null)inputs.EmptyMeasured=false;else{inputs.EmptyRoot=root;inputs.EmptyFolders=EmptyFolders.Find(root,CancellationToken.None).Folders.Count;}}
    catch(Exception e){inputs.EmptyMeasured=false;Core.Log.Warn("Health: empty-folder probe failed: "+e.Message);}
    stage("Đang đọc dung lượng trống…");
    try{var drive=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));inputs.DiskName="Ổ "+drive.Name.TrimEnd('\\','/');inputs.DiskFreeBytes=drive.AvailableFreeSpace;inputs.DiskTotalBytes=drive.TotalSize;}
    catch(Exception e){inputs.DiskMeasured=false;Core.Log.Warn("Health: disk probe failed: "+e.Message);}
   });
   healthReport=HealthCheck.Evaluate(inputs);
   healthStage.Visible=false;healthGauge.Report=healthReport;
   RenderHealth();
   Log("Kiểm tra sức khỏe: "+healthReport.Score+"/100 ("+healthReport.Grade+"). "+healthReport.Headline);
  }

  void RenderHealth(){
   healthList.BeginUpdate();healthList.Items.Clear();
   foreach(var f in healthReport.Findings){
    var row=new ListViewItem(new[]{f.Area,f.Verdict,f.Detail,f.Bytes>0?Presentation.BytesLabel(f.Bytes):"—",f.Measured?HealthCheck.SeverityLabel(f.Severity):"Chưa đo",f.Tab}){Tag=f,ToolTipText=f.Detail};
    if(!f.Measured)row.ForeColor=Theme.Muted;else if(f.Severity!=HealthSeverity.Good)row.ForeColor=HealthRenderer.SeverityColor(f.Severity);
    Theme.StripeRow(row,healthList.Items.Count);healthList.Items.Add(row);
   }
   healthList.EndUpdate();
   Theme.SetOverlay(healthOverlay,null,NoteKind.Info);
  }

  /// <summary>Switches to the tab that acts on the selected finding.</summary>
  void OpenHealthTab(){
   if(healthList.SelectedItems.Count==0)throw new IOException("Chọn một dòng kết quả trước.");
   var f=(HealthFinding)healthList.SelectedItems[0].Tag;
   var page=tabs.TabPages.Cast<TabPage>().FirstOrDefault(p=>String.Equals(p.Text,f.Tab,StringComparison.OrdinalIgnoreCase));
   if(page==null)throw new IOException("Không tìm thấy tab "+f.Tab+".");
   tabs.SelectedTab=page;
  }

  void CopyHealthReport(){
   if(healthReport==null)throw new IOException("Chưa có báo cáo. Bấm Kiểm tra ngay trước.");
   Clipboard.SetText(HealthCheck.Summary(healthReport));Log("Đã sao chép báo cáo sức khỏe vào clipboard.");
  }
 }
}
