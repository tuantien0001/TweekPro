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
  HealthReport healthReport;CancellationTokenSource healthCancellation;Button healthStop;CheckBox healthAuto;

  /// <summary>Builds the Overview tab: one read-only health check that scores the machine and points to the tab that fixes each finding.</summary>
  void BuildHealthTab(){
   var tab=healthTab=new TabPage(Core.L.T("Tổng quan"));
   SetupList(healthList,new[]{"Khu vực","Đánh giá","Chi tiết","Có thể giải phóng","Mức","Xử lý ở tab"},new[]{180,210,470,130,90,150},false);
   healthList.DoubleClick+=async(s,e)=>await Guard(()=>{if(healthList.SelectedItems.Count>0)OpenHealthTab();return Task.FromResult(0);});
   var host=Theme.ListHost(healthList,out healthOverlay);

   var bar=Bar();
   Add(bar,"Kiểm tra ngay",async()=>await RunHealthCheck(),ButtonStyle.Primary);
   Add(bar,"Mở tab xử lý",()=>{OpenHealthTab();return Task.FromResult(0);});
   Add(bar,"Sao chép báo cáo",()=>{CopyHealthReport();return Task.FromResult(0);});
   Add(bar,"Kiểm tra cập nhật",async()=>await CheckForUpdates());
   // Deliberately not registered through Add(): it must stay enabled while Guard disables every other action.
   healthStop=Theme.Button("Dừng kiểm tra",ButtonStyle.Secondary);healthStop.Margin=new Padding(0,0,8,8);healthStop.Visible=false;
   healthStop.Click+=(s,e)=>{if(healthCancellation!=null)healthCancellation.Cancel();};bar.Controls.Add(healthStop);
   healthAuto=new CheckBox{Text=Core.L.T("Tự kiểm tra khi mở"),AutoSize=true,Margin=new Padding(8,8,12,0),ForeColor=Theme.Text,Checked=settings.HealthAutoCheck};
   healthAuto.CheckedChanged+=(s,e)=>{settings.HealthAutoCheck=healthAuto.Checked;SaveSettings();};bar.Controls.Add(healthAuto);

   healthGauge.Dock=DockStyle.Top;healthGauge.Height=176;
   healthStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   var note=Theme.Note("Kiểm tra sức khỏe chỉ đọc: đo tệp rác theo quy tắc, mục còn sót chờ duyệt, thư mục rỗng trong Downloads, bản sao lưu cũ hơn tuổi dọn kho, số mục khởi động, dung lượng trống ổ hệ thống và phần mềm không mong muốn (PUP/bloatware). Không xóa gì; bấm đúp một dòng để mở tab xử lý tương ứng.",NoteKind.Info);
   tab.Controls.Add(host);tab.Controls.Add(healthStage);tab.Controls.Add(note);tab.Controls.Add(healthGauge);tab.Controls.Add(bar);tab.Controls.Add(BuildUpdateBanner());
   Theme.SetOverlay(healthOverlay,"Chưa kiểm tra.\r\nBấm Kiểm tra ngay để chấm điểm máy. Mỗi dòng kết quả chỉ ra tab có thể dọn an toàn.",NoteKind.Info);
  }

  /// <summary>Reads the fixed drives off the UI thread and shows them as rings; called at startup and after every check.</summary>
  async Task RefreshDriveRings(){
   var drives=await Task.Run(()=>HealthCheck.ReadDrives());
   if(!IsDisposed)healthGauge.Drives=drives;
  }

  /// <summary>Startup hook: drive rings first, then the full check unless the user turned it off. Runs outside Guard so every other button stays usable while the probes work at low priority.</summary>
  async Task AutoHealthCheck(){
   try{await RefreshDriveRings();if(settings.HealthAutoCheck)await RunHealthCheck();}
   catch(Exception e){Log(Core.L.T("LỖI: ")+e.Message);}
  }

  /// <summary>Runs every probe on a low-priority worker thread, then scores and renders the report; a second call while one is running is ignored.</summary>
  async Task RunHealthCheck(){
   if(healthCancellation!=null){Log(Core.L.T("Đang kiểm tra sức khỏe, chờ lượt hiện tại xong."));return;}
   healthStage.Visible=true;healthGauge.Report=null;healthGauge.BusyText=Core.L.T("Đang kiểm tra…");
   if(healthReport==null)Theme.SetOverlay(healthOverlay,"Đang kiểm tra sức khỏe máy…\r\nKết quả từng khu vực sẽ hiện ở đây; bấm Dừng kiểm tra nếu muốn bỏ qua.",NoteKind.Info);
   var inputs=new HealthInputs{LeftoverCandidates=candidates.Count};
   bool elevated=Core.Elevation.IsElevated;int minAge=settings.JunkMinAgeHours;int purgeDays=settings.PurgeDefaultDays;
   var desktopSnapshot=inventory.ToList();var storeSnapshot=storeLoaded?storeApps.ToList():null;
   if(desktopSnapshot.Count==0&&inventoryError==null)inputs.PupMeasured=false;
   Action<string> stage=text=>{try{BeginInvoke((Action)(()=>healthStage.Text=text));}catch(InvalidOperationException){}};
   healthCancellation=new CancellationTokenSource();var token=healthCancellation.Token;healthStop.Visible=true;
   try{
   await Task.Run(()=>LowPriority<object>(()=>{
    stage(Core.L.T("Đang đo tệp rác theo quy tắc…"));
    try{var rules=JunkRules.Load(Core.Paths.JunkRulesOverride).Rules;var preview=JunkCleaner.Preview(rules,elevated,minAge,token);
     inputs.JunkBytes=preview.Where(r=>!r.Locked).Sum(r=>r.Bytes);inputs.JunkFiles=preview.Where(r=>!r.Locked).Sum(r=>r.Count);inputs.JunkLockedRules=preview.Count(r=>r.Locked);}
    catch(OperationCanceledException){throw;}
    catch(Exception e){inputs.JunkMeasured=false;Core.Log.Warn("Health: junk probe failed: "+e.Message);}
    stage(Core.L.T("Đang đo kho khôi phục…"));
    // Restored backups are already empty; what still occupies disk is old backups the purge dialog would select.
    try{var all=Engine.Backups();inputs.VaultBackups=all.Count;inputs.VaultStaleDays=purgeDays;
     var stale=new HashSet<string>(Engine.SelectForPurge(all,purgeDays,DateTime.Now,true).Select(b=>b.Id));
     foreach(var b in all){token.ThrowIfCancellationRequested();long bytes=Engine.BackupSize(b);if(bytes>0)inputs.VaultBytes+=bytes;if(stale.Contains(b.Id)){inputs.VaultStale++;if(bytes>0)inputs.VaultStaleBytes+=bytes;}}}
    catch(OperationCanceledException){throw;}
    catch(Exception e){inputs.VaultMeasured=false;Core.Log.Warn("Health: vault probe failed: "+e.Message);}
    stage(Core.L.T("Đang đếm mục khởi động…"));
    try{var autoruns=Advanced.Autoruns(false).Where(a=>a.Item!=null).ToList();Startup.StartupInspector.Annotate(autoruns);inputs.AutorunEntries=autoruns.Count(a=>a.Insight!=null&&a.Insight.Approval.Enabled);inputs.AutorunBroken=autoruns.Count(a=>a.Insight!=null&&a.Insight.Broken);}
    catch(Exception e){inputs.AutorunMeasured=false;Core.Log.Warn("Health: autorun probe failed: "+e.Message);}
    token.ThrowIfCancellationRequested();
    stage(Core.L.T("Đang tìm thư mục rỗng trong Downloads…"));
    // Only Downloads is probed: falling back to the whole profile could take minutes while the window is busy.
    try{string root=DownloadsFolder();if(root==null)inputs.EmptyMeasured=false;else{inputs.EmptyRoot=root;inputs.EmptyFolders=EmptyFolders.Find(root,token).Folders.Count;}}
    catch(OperationCanceledException){throw;}
    catch(Exception e){inputs.EmptyMeasured=false;Core.Log.Warn("Health: empty-folder probe failed: "+e.Message);}
    stage(Core.L.T("Đang đọc dung lượng trống…"));
    try{var drive=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));inputs.DiskName=Core.L.F("Ổ {0}",drive.Name.TrimEnd('\\','/'));inputs.DiskFreeBytes=drive.AvailableFreeSpace;inputs.DiskTotalBytes=drive.TotalSize;}
    catch(Exception e){inputs.DiskMeasured=false;Core.Log.Warn("Health: disk probe failed: "+e.Message);}
    var rings=HealthCheck.ReadDrives();try{BeginInvoke((Action)(()=>healthGauge.Drives=rings));}catch(InvalidOperationException){}
    token.ThrowIfCancellationRequested();
    stage(Core.L.T("Đang phân loại phần mềm không mong muốn…"));
    // Desktop inventory is already in memory; Store packages only count when that tab has loaded them (no PowerShell here).
    try{var verdicts=Pup.PupDetector.Scan(desktopSnapshot,storeSnapshot);inputs.PupCount=verdicts.Count;inputs.PupHigh=verdicts.Count(v=>v.Severity==Pup.PupSeverity.High);}
    catch(Exception e){inputs.PupMeasured=false;Core.Log.Warn("Health: pup probe failed: "+e.Message);}
    return null;
   }),token);
   }catch(OperationCanceledException){if(!IsDisposed){healthGauge.Report=healthReport;if(healthReport==null)Theme.SetOverlay(healthOverlay,"Chưa kiểm tra.\r\nBấm Kiểm tra ngay để chấm điểm máy. Mỗi dòng kết quả chỉ ra tab có thể dọn an toàn.",NoteKind.Info);Log(Core.L.T("Đã dừng kiểm tra sức khỏe."));}return;}
   finally{healthCancellation.Dispose();healthCancellation=null;if(!IsDisposed){healthStage.Visible=false;healthStop.Visible=false;healthGauge.BusyText=null;}}
   if(IsDisposed)return;
   healthReport=HealthCheck.Evaluate(inputs);
   healthGauge.Report=healthReport;
   RenderHealth();
   Log(Core.L.F("Kiểm tra sức khỏe: {0}/100 ({1}). {2}",healthReport.Score,healthReport.Grade,healthReport.Headline));
  }

  static string DownloadsFolder(){
   try{string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);if(String.IsNullOrWhiteSpace(profile))return null;string d=Path.Combine(profile,"Downloads");return Directory.Exists(d)?d:null;}
   catch(Exception){return null;}
  }

  void RenderHealth(){
   healthList.BeginUpdate();healthList.Items.Clear();
   foreach(var f in healthReport.Findings){
    var row=new ListViewItem(new[]{f.Area,f.Verdict,f.Detail,f.Bytes>0?Presentation.BytesLabel(f.Bytes):"—",f.Measured?HealthCheck.SeverityLabel(f.Severity):Core.L.T("Chưa đo"),Core.L.T(f.Tab)}){Tag=f,ToolTipText=f.Detail};
    if(!f.Measured)row.ForeColor=Theme.Muted;else if(f.Severity!=HealthSeverity.Good)row.ForeColor=HealthRenderer.SeverityColor(f.Severity);
    Theme.StripeRow(row,healthList.Items.Count);healthList.Items.Add(row);
   }
   healthList.EndUpdate();
   Theme.SetOverlay(healthOverlay,null,NoteKind.Info);
  }

  /// <summary>Switches to the tab that acts on the selected finding.</summary>
  void OpenHealthTab(){
   if(healthList.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một dòng kết quả trước."));
   var f=(HealthFinding)healthList.SelectedItems[0].Tag;
   var page=tabs.TabPages.Cast<TabPage>().FirstOrDefault(p=>String.Equals(p.Text,Core.L.T(f.Tab),StringComparison.OrdinalIgnoreCase));
   if(page==null)throw new IOException(Core.L.F("Không tìm thấy tab {0}.",Core.L.T(f.Tab)));
   tabs.SelectedTab=page;
   // The autorun list loads lazily on tab change, but that handler skips while Guard holds busy; queue the load for after this action.
   if(page==autorunTab&&autorunList.Items.Count==0)BeginInvoke((Action)(async()=>await Guard(async()=>await LoadAutoruns())));
  }

  /// <summary>Fills the Overview tab with illustrative numbers for --preview health; no probes run.</summary>
  public void PreviewHealth(){
   var parts=Version.Split('.');int patch;string newer=parts.Length==3&&int.TryParse(parts[2],out patch)?parts[0]+"."+parts[1]+"."+(patch+1):Version;
   ShowUpdateBanner(new TweekPro.Update.UpdateInfo{UpdateAvailable=true,Latest=newer,Current=Version,PageUrl=TweekPro.Update.UpdateCheck.ReleasesPage});
   var sample=new HealthInputs{JunkBytes=730L*HealthCheck.MB,JunkFiles=4812,JunkLockedRules=1,LeftoverCandidates=3,EmptyFolders=17,EmptyRoot=@"C:\Users\ADMIN\Downloads",VaultBackups=9,VaultBytes=2200L*HealthCheck.MB,VaultStale=4,VaultStaleBytes=640L*HealthCheck.MB,AutorunEntries=12,DiskName="Ổ C:",DiskFreeBytes=38L*HealthCheck.GB,DiskTotalBytes=476L*HealthCheck.GB,PupCount=3,PupHigh=1};
   var drives=new List<DriveGauge>{new DriveGauge{Name="C:",FreeBytes=38L*HealthCheck.GB,TotalBytes=476L*HealthCheck.GB},new DriveGauge{Name="D:",FreeBytes=610L*HealthCheck.GB,TotalBytes=931L*HealthCheck.GB},new DriveGauge{Name="E:",FreeBytes=120L*HealthCheck.GB,TotalBytes=1863L*HealthCheck.GB}};
   foreach(var d in drives)d.Label=HealthCheck.DriveLabel(d);
   healthGauge.Drives=drives;
   healthReport=HealthCheck.Evaluate(sample);healthGauge.Report=healthReport;RenderHealth();tabs.SelectedTab=healthTab;
  }

  void CopyHealthReport(){
   if(healthReport==null)throw new IOException(Core.L.T("Chưa có báo cáo. Bấm Kiểm tra ngay trước."));
   Clipboard.SetText(HealthCheck.Summary(healthReport));Log(Core.L.T("Đã sao chép báo cáo sức khỏe vào clipboard."));
  }
 }
}
