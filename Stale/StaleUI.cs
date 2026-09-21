using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Stale;

namespace TweekPro {
 public partial class MainForm {
  ListView staleList=new SmoothListView();Label staleOverlay,staleSummary,staleStage;TabPage staleTab;
  RadioButton staleVaultMode=new RadioButton(),staleDirectMode=new RadioButton();
  CheckBox staleDownloads=new CheckBox(),staleDesktop=new CheckBox();NumericUpDown staleAge=new NumericUpDown(),staleLarge=new NumericUpDown();
  Button staleCleanButton;StaleScanResult staleResult=new StaleScanResult();CancellationTokenSource staleCancellation;

  /// <summary>Builds the Old Downloads tab: old installers, archives, disk images, partial downloads and large files under Downloads/Desktop.</summary>
  void BuildStaleTab(){
   var tab=staleTab=new TabPage(Core.L.T("Tệp tải về cũ"));
   SetupList(staleList,new[]{"Tệp","Loại","Dung lượng","Tuổi (ngày)","Sửa lần cuối","Thư mục"},new[]{340,110,120,100,150,420},true);
   staleList.ItemChecked+=(s,e)=>UpdateStaleSummary();
   staleList.DoubleClick+=async(s,e)=>await Guard(()=>{OpenStaleLocation();return Task.FromResult(0);});
   var host=Theme.ListHost(staleList,out staleOverlay);

   var bar=Bar();
   Add(bar,"Quét tệp cũ",async()=>await ScanStale(),ButtonStyle.Primary);
   Add(bar,"Chọn tất cả",()=>{SetStaleChecks(true);return Task.FromResult(0);});
   Add(bar,"Bỏ chọn",()=>{SetStaleChecks(false);return Task.FromResult(0);});
   Add(bar,"Dọn tệp đã chọn",async()=>await QuarantineStale(),ButtonStyle.Danger);
   staleCleanButton=actions[actions.Count-1];
   Add(bar,"Mở vị trí",()=>{OpenStaleLocation();return Task.FromResult(0);});

   var optionPanel=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(16,8,16,4),BackColor=Theme.Surface};
   var whereLabel=new Label{Text=Core.L.T("Quét trong:"),AutoSize=true,Margin=new Padding(0,6,12,0),ForeColor=Theme.Muted,Font=Theme.Small};
   staleDownloads.Text="Downloads";staleDownloads.Checked=true;staleDownloads.AutoSize=true;staleDownloads.Margin=new Padding(0,4,16,0);staleDownloads.ForeColor=Theme.Text;staleDownloads.Font=Theme.Body;
   staleDesktop.Text="Desktop";staleDesktop.Checked=settings.StaleIncludeDesktop;staleDesktop.AutoSize=true;staleDesktop.Margin=new Padding(0,4,24,0);staleDesktop.ForeColor=Theme.Text;staleDesktop.Font=Theme.Body;
   staleDesktop.CheckedChanged+=(s,e)=>settings.StaleIncludeDesktop=staleDesktop.Checked;
   var ageLabel=new Label{Text=Core.L.T("Cũ hơn (ngày):"),AutoSize=true,Margin=new Padding(0,6,6,0),ForeColor=Theme.Muted,Font=Theme.Small};
   staleAge.Minimum=0;staleAge.Maximum=3650;staleAge.Value=settings.StaleMinAgeDays;staleAge.Width=70;staleAge.Margin=new Padding(0,2,20,0);staleAge.Font=Theme.Body;
   staleAge.ValueChanged+=(s,e)=>settings.StaleMinAgeDays=(int)staleAge.Value;
   var largeLabel=new Label{Text=Core.L.T("Tệp lớn từ (MB):"),AutoSize=true,Margin=new Padding(0,6,6,0),ForeColor=Theme.Muted,Font=Theme.Small};
   staleLarge.Minimum=0;staleLarge.Maximum=1048576;staleLarge.Value=settings.StaleLargeMB;staleLarge.Width=80;staleLarge.Margin=new Padding(0,2,0,0);staleLarge.Font=Theme.Body;
   staleLarge.ValueChanged+=(s,e)=>settings.StaleLargeMB=(int)staleLarge.Value;
   optionPanel.Controls.Add(whereLabel);optionPanel.Controls.Add(staleDownloads);optionPanel.Controls.Add(staleDesktop);optionPanel.Controls.Add(ageLabel);optionPanel.Controls.Add(staleAge);optionPanel.Controls.Add(largeLabel);optionPanel.Controls.Add(staleLarge);

   var modePanel=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(16,4,16,6),BackColor=Theme.Surface};
   Theme.BorderBottom(modePanel);
   var modeLabel=new Label{Text=Core.L.T("Chế độ dọn:"),AutoSize=true,Margin=new Padding(0,6,12,0),ForeColor=Theme.Muted,Font=Theme.Small};
   staleVaultMode.Text=Core.L.T("Chuyển vào Kho khôi phục (có thể hoàn tác)");staleVaultMode.Checked=true;staleVaultMode.AutoSize=true;staleVaultMode.Margin=new Padding(0,4,24,0);staleVaultMode.ForeColor=Theme.Text;staleVaultMode.Font=Theme.Body;
   staleDirectMode.Text=Core.L.T("Xóa thẳng — KHÔNG thể khôi phục");staleDirectMode.AutoSize=true;staleDirectMode.Margin=new Padding(0,4,0,0);staleDirectMode.ForeColor=Theme.Danger;staleDirectMode.Font=Theme.Strong;
   staleVaultMode.CheckedChanged+=(s,e)=>UpdateStaleSummary();staleDirectMode.CheckedChanged+=(s,e)=>UpdateStaleSummary();
   modePanel.Controls.Add(modeLabel);modePanel.Controls.Add(staleVaultMode);modePanel.Controls.Add(staleDirectMode);

   var note=Theme.Note("Chỉ quét Downloads và Desktop của tài khoản hiện tại: bộ cài (.exe/.msi/.msix), tệp nén, ảnh đĩa, tệp tải dở và tệp lớn cũ hơn số ngày đã chọn. Bỏ qua shortcut, tệp ẩn/hệ thống, thư mục OneDrive/Dropbox. Chưa có gì bị xóa cho đến khi bạn đánh dấu và xác nhận; mặc định chuyển vào Kho khôi phục.",NoteKind.Warning);
   staleStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   staleSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(staleSummary);
   tab.Controls.Add(host);tab.Controls.Add(staleSummary);tab.Controls.Add(staleStage);tab.Controls.Add(note);tab.Controls.Add(modePanel);tab.Controls.Add(optionPanel);tab.Controls.Add(bar);
   Theme.SetOverlay(staleOverlay,"Chưa quét.\r\nBấm Quét tệp cũ để liệt kê bộ cài, tệp nén, ảnh đĩa, tệp tải dở và tệp lớn đã lâu không đụng tới trong Downloads (và Desktop nếu chọn).",NoteKind.Info);
   UpdateStaleSummary();
  }

  List<string> StaleRoots(){
   var roots=new List<string>();var allowed=StaleSafety.AllowedRoots();
   string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   if(staleDownloads.Checked&&!String.IsNullOrWhiteSpace(profile)){string d=Path.Combine(profile,"Downloads");if(Directory.Exists(d))roots.Add(d);}
   if(staleDesktop.Checked){string d=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);if(!String.IsNullOrWhiteSpace(d)&&Directory.Exists(d))roots.Add(d);}
   return roots.Where(r=>allowed.Any(a=>String.Equals(a,Engine.Canon(r),StringComparison.OrdinalIgnoreCase))).ToList();
  }

  void RenderStale(bool check){
   staleList.BeginUpdate();staleList.Items.Clear();
   foreach(var f in staleResult.Files){
    var row=new ListViewItem(new[]{f.Name,StaleFinder.KindLabel(f.Kind),Presentation.BytesLabel(f.Bytes),f.AgeDays.ToString("N0"),f.LastWrite==DateTime.MinValue?"—":f.LastWrite.ToString("dd/MM/yyyy HH:mm"),Presentation.ShortPath(f.Folder,64)}){Tag=f,ToolTipText=f.Path+"\r\n"+Core.L.F("Tạo: {0}  •  Sửa: {1}  •  {2} ngày",f.Created==DateTime.MinValue?"—":f.Created.ToString("dd/MM/yyyy"),f.LastWrite==DateTime.MinValue?"—":f.LastWrite.ToString("dd/MM/yyyy"),f.AgeDays)};
    if(f.Kind==StaleKind.Partial)row.ForeColor=Theme.Warning;
    Theme.StripeRow(row,staleList.Items.Count);staleList.Items.Add(row);
    row.Checked=check&&(f.Kind==StaleKind.Installer||f.Kind==StaleKind.Partial);
   }
   staleList.EndUpdate();UpdateStaleSummary();
  }

  void SetStaleChecks(bool value){staleList.BeginUpdate();foreach(ListViewItem item in staleList.Items)item.Checked=value;staleList.EndUpdate();UpdateStaleSummary();}
  List<StaleFile> CheckedStale(){return staleList.CheckedItems.Cast<ListViewItem>().Select(i=>(StaleFile)i.Tag).ToList();}

  void UpdateStaleSummary(){
   var selected=CheckedStale();long bytes=selected.Sum(f=>f.Bytes);bool direct=staleDirectMode.Checked;
   staleSummary.Text=Core.L.F("{0} tệp cũ ({1})  •  Đã chọn {2} tệp, {3}",staleResult.Files.Count.ToString("N0"),Presentation.BytesLabel(staleResult.TotalBytes),selected.Count.ToString("N0"),Presentation.BytesLabel(bytes))+Core.L.T(direct?"  •  XÓA THẲNG: không thể khôi phục":"  •  Chuyển vào Kho: chưa giải phóng cho đến khi xóa vĩnh viễn");
   staleSummary.ForeColor=direct?Theme.Danger:Theme.Muted;
   if(staleCleanButton!=null)staleCleanButton.Text=Core.L.T(direct?"Xóa thẳng tệp đã chọn":"Dọn tệp đã chọn (vào kho)");
  }

  /// <summary>Runs the read-only stale scan on a worker thread and lists the results, pre-checking installers and partial downloads.</summary>
  async Task ScanStale(){
   var roots=StaleRoots();if(roots.Count==0)throw new IOException(Core.L.T("Chọn ít nhất một thư mục (Downloads hoặc Desktop) còn tồn tại."));
   int minAge=(int)staleAge.Value;long minLarge=(long)staleLarge.Value*1024*1024;
   staleCancellation=new CancellationTokenSource();var token=staleCancellation.Token;
   staleStage.Visible=true;staleStage.Text=Core.L.T("Đang quét…");Theme.SetOverlay(staleOverlay,null,NoteKind.Info);
   Log(Core.L.F("Tệp tải về cũ: đang quét (chỉ đọc) {0}, cũ hơn {1} ngày, tệp lớn từ {2} MB.",String.Join("; ",roots),minAge,(int)staleLarge.Value));
   try{
    staleResult=await Task.Run(()=>StaleFinder.Scan(roots,minAge,minLarge,token,ReportStale),token);
    RenderStale(true);
    staleStage.Text=Core.L.F("Quét xong: {0} tệp cũ ({1}) trong {2} tệp đã duyệt",staleResult.Files.Count.ToString("N0"),Presentation.BytesLabel(staleResult.TotalBytes),staleResult.Scanned.ToString("N0"))+(staleResult.Partial?Core.L.T("  •  (chưa đủ)"):"");
    Log(Core.L.F("Tệp tải về cũ: {0} tệp, {1}.",staleResult.Files.Count,Presentation.BytesLabel(staleResult.TotalBytes)));
    foreach(string n in staleResult.Notes.Take(5))Log(Core.L.T("Tệp tải về cũ: ")+Core.L.T(n));
    if(staleResult.Files.Count==0)Theme.SetOverlay(staleOverlay,Core.L.F("Không có tệp cũ hơn {0} ngày khớp tiêu chí trong thư mục đã chọn.",minAge),NoteKind.Info);
   }catch(OperationCanceledException){staleStage.Text=Core.L.T("Đã dừng quét.");}
   finally{staleCancellation.Dispose();staleCancellation=null;}
  }

  void ReportStale(StaleProgress p){
   if(IsDisposed||!IsHandleCreated)return;
   try{BeginInvoke((Action)(()=>{if(!IsDisposed)staleStage.Text=Core.L.T(p.Stage)+(String.IsNullOrEmpty(p.Current)?"":"   "+Presentation.ShortPath(p.Current,70))+(p.Files>0?"   •   "+Core.L.F("{0} tệp",p.Files.ToString("N0")):"");}));}catch(InvalidOperationException){}
  }

  /// <summary>Confirms and moves (or deletes) the checked files; a direct delete needs two confirmations.</summary>
  async Task QuarantineStale(){
   var selected=CheckedStale();if(selected.Count==0)throw new IOException(Core.L.T("Quét rồi đánh dấu tệp muốn dọn."));
   bool direct=staleDirectMode.Checked;long bytes=selected.Sum(f=>f.Bytes);
   string list=String.Join("\r\n",selected.Take(8).Select(f=>"• "+f.Name+" ("+Presentation.BytesLabel(f.Bytes)+", "+Core.L.F("{0} ngày",f.AgeDays)+")"))+(selected.Count>8?"\r\n… +"+(selected.Count-8):"");
   if(direct){
    var answer=MessageBox.Show(this,Core.L.F("XÓA THẲNG {0} tệp ({1}) mà KHÔNG lưu bản khôi phục?\r\n\r\n{2}\r\n\r\nThao tác này không thể hoàn tác.",selected.Count.ToString("N0"),Presentation.BytesLabel(bytes),list),Core.L.T("Xác nhận xóa vĩnh viễn"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
    if(answer!=DialogResult.Yes)return;
    if(!Confirm(Core.L.F("Xác nhận lần cuối: xóa thẳng {0} tệp, không có bản sao lưu?",selected.Count.ToString("N0"))))return;
   }else{
    if(!Confirm(Core.L.F("Chuyển {0} tệp ({1}) vào Kho khôi phục?\r\n\r\n{2}\r\n\r\nCó thể khôi phục hoặc xóa vĩnh viễn sau trong tab Kho khôi phục. Dung lượng ổ đĩa chưa được giải phóng cho đến khi xóa vĩnh viễn.",selected.Count.ToString("N0"),Presentation.BytesLabel(bytes),list)))return;
   }
   staleCancellation=new CancellationTokenSource();var token=staleCancellation.Token;staleStage.Visible=true;staleStage.Text=Core.L.T(direct?"Đang xóa thẳng…":"Đang chuyển vào kho…");
   Log(Core.L.F(direct?"Tệp tải về cũ: bắt đầu xóa thẳng {0} tệp.":"Tệp tải về cũ: bắt đầu chuyển vào kho {0} tệp.",selected.Count.ToString("N0")));
   var snapshot=selected.ToList();StaleReport report;
   try{report=await Task.Run(()=>StaleFinder.Quarantine(snapshot,direct,token,ReportStale),token);}
   catch(OperationCanceledException){staleStage.Text=Core.L.T("Đã dừng dọn.");return;}
   finally{staleCancellation.Dispose();staleCancellation=null;}
   LoadBackups();
   string summary=Core.L.F(direct?"Đã xóa thẳng {0} tệp ({1}). Bỏ qua vì đang dùng: {2}. Lỗi: {3}.":"Đã chuyển vào kho {0} tệp ({1}). Bỏ qua vì đang dùng: {2}. Lỗi: {3}.",report.Quarantined.ToString("N0"),Presentation.BytesLabel(report.Bytes),report.SkippedInUse.ToString("N0"),report.Failed.ToString("N0"));
   staleStage.Text=summary;Log(Core.L.T("Tệp tải về cũ: ")+summary);
   foreach(string err in report.Errors.Take(20))Log(Core.L.T("Dọn tệp cũ lỗi: ")+err);
   MessageBox.Show(this,summary+(report.Errors.Count>0?Core.L.T("\r\n\r\nLỗi đầu tiên:\r\n")+String.Join("\r\n",report.Errors.Take(5)):"")+(direct?"":Core.L.T("\r\n\r\nCó thể khôi phục hoặc xóa vĩnh viễn trong tab Kho khôi phục.")),Core.L.T("Kết quả dọn tệp cũ"),MessageBoxButtons.OK,report.Failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
   await ScanStale();
  }

  void OpenStaleLocation(){
   if(staleList.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một tệp để mở vị trí."));
   var f=(StaleFile)staleList.SelectedItems[0].Tag;
   if(!File.Exists(f.Path))throw new IOException(Core.L.T("Tệp không còn tồn tại: ")+f.Path);
   Process.Start("explorer.exe","/select,\""+f.Path+"\"");
  }

  /// <summary>Fills the tab with illustrative rows for --preview stale; no scan runs.</summary>
  public void PreviewStale(){
   string d=@"C:\Users\ADMIN\Downloads";var now=DateTime.Now;
   staleResult=new StaleScanResult{Roots={d},Scanned=1240,Files={
    new StaleFile{Path=Path.Combine(d,"Win11_24H2_Vietnamese_x64.iso"),Bytes=6L*1024*1024*1024,Kind=StaleKind.DiskImage,AgeDays=212,Created=now.AddDays(-212),LastWrite=now.AddDays(-212)},
    new StaleFile{Path=Path.Combine(d,"VisualStudioSetup.exe"),Bytes=4L*1024*1024,Kind=StaleKind.Installer,AgeDays=140,Created=now.AddDays(-140),LastWrite=now.AddDays(-140)},
    new StaleFile{Path=Path.Combine(d,"project-backup-2025.zip"),Bytes=840L*1024*1024,Kind=StaleKind.Archive,AgeDays=95,Created=now.AddDays(-95),LastWrite=now.AddDays(-95)},
    new StaleFile{Path=Path.Combine(d,"Unconfirmed 48213.crdownload"),Bytes=312L*1024*1024,Kind=StaleKind.Partial,AgeDays=61,Created=now.AddDays(-61),LastWrite=now.AddDays(-61)},
    new StaleFile{Path=Path.Combine(d,"lecture-recording.mp4"),Bytes=1900L*1024*1024,Kind=StaleKind.Large,AgeDays=45,Created=now.AddDays(-45),LastWrite=now.AddDays(-45)}}};
   staleResult.TotalBytes=staleResult.Files.Sum(f=>f.Bytes);
   RenderStale(true);Theme.SetOverlay(staleOverlay,null,NoteKind.Info);tabs.SelectedTab=staleTab;
  }
 }
}
