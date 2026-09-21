using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Dupes;

namespace TweekPro {
 public partial class MainForm {
  ListView dupeList=new SmoothListView();Label dupeOverlay,dupeSummary,dupeStage,dupeRootLabel;
  RadioButton dupeVaultMode=new RadioButton(),dupeDirectMode=new RadioButton();
  RadioButton dupeKeepOldest=new RadioButton(),dupeKeepNewest=new RadioButton();
  Button dupeQuarantineButton;TabPage dupeTab;
  DuplicateScanResult dupeResult=new DuplicateScanResult();string dupeRoot;
  CancellationTokenSource dupeCancellation;

  /// <summary>Builds the Duplicate Finder tab: pick a folder, scan for byte-identical files and quarantine the redundant copies.</summary>
  void BuildDuplicateTab(){
   var tab=dupeTab=new TabPage(Core.L.T("Tệp trùng lặp"));
   SetupList(dupeList,new[]{"Nhóm (bản giữ lại)","Số bản","Dung lượng mỗi tệp","Tiết kiệm được","Thư mục bản giữ lại"},new[]{340,80,150,150,430},true);
   dupeList.ItemCheck+=(s,e)=>{var g=(DuplicateGroup)dupeList.Items[e.Index].Tag;if(g.Count<2)e.NewValue=CheckState.Unchecked;};
   dupeList.ItemChecked+=(s,e)=>UpdateDupeSummary();
   dupeList.DoubleClick+=async(s,e)=>await Guard(()=>{if(dupeList.SelectedItems.Count>0)ShowDupeDetails();return Task.FromResult(0);});
   var host=Theme.ListHost(dupeList,out dupeOverlay);

   var bar=Bar();
   Add(bar,"Chọn thư mục…",()=>{ChooseDupeRoot();return Task.FromResult(0);});
   Add(bar,"Quét trùng lặp",async()=>await ScanDupes(),ButtonStyle.Primary);
   Add(bar,"Chọn tất cả",()=>{SetDupeChecks(true);return Task.FromResult(0);});
   Add(bar,"Bỏ chọn",()=>{SetDupeChecks(false);return Task.FromResult(0);});
   Add(bar,"Dọn bản trùng đã chọn",async()=>await QuarantineDupes(),ButtonStyle.Danger);
   dupeQuarantineButton=actions[actions.Count-1];
   Add(bar,"Xem tệp",()=>{ShowDupeDetails();return Task.FromResult(0);});

   var keepPanel=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(16,8,16,4),BackColor=Theme.Surface};
   var keepLabel=new Label{Text=Core.L.T("Giữ lại bản:"),AutoSize=true,Margin=new Padding(0,6,12,0),ForeColor=Theme.Muted,Font=Theme.Small};
   dupeKeepOldest.Text=Core.L.T("Cũ nhất (bản gốc)");dupeKeepOldest.Checked=!settings.DuplicateKeepNewest;dupeKeepOldest.AutoSize=true;dupeKeepOldest.Margin=new Padding(0,4,16,0);dupeKeepOldest.ForeColor=Theme.Text;dupeKeepOldest.Font=Theme.Body;
   dupeKeepNewest.Text=Core.L.T("Mới nhất");dupeKeepNewest.Checked=settings.DuplicateKeepNewest;dupeKeepNewest.AutoSize=true;dupeKeepNewest.Margin=new Padding(0,4,0,0);dupeKeepNewest.ForeColor=Theme.Text;dupeKeepNewest.Font=Theme.Body;
   dupeKeepNewest.CheckedChanged+=(s,e)=>{settings.DuplicateKeepNewest=dupeKeepNewest.Checked;RenderDupes(dupeResult.Groups.Count>0);};
   keepPanel.Controls.Add(keepLabel);keepPanel.Controls.Add(dupeKeepOldest);keepPanel.Controls.Add(dupeKeepNewest);

   var modePanel=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(16,4,16,6),BackColor=Theme.Surface};
   Theme.BorderBottom(modePanel);
   var modeLabel=new Label{Text=Core.L.T("Chế độ dọn:"),AutoSize=true,Margin=new Padding(0,6,12,0),ForeColor=Theme.Muted,Font=Theme.Small};
   dupeVaultMode.Text=Core.L.T("Chuyển vào Kho khôi phục (có thể hoàn tác)");dupeVaultMode.Checked=true;dupeVaultMode.AutoSize=true;dupeVaultMode.Margin=new Padding(0,4,24,0);dupeVaultMode.ForeColor=Theme.Text;dupeVaultMode.Font=Theme.Body;
   dupeDirectMode.Text=Core.L.T("Xóa thẳng — KHÔNG thể khôi phục");dupeDirectMode.AutoSize=true;dupeDirectMode.Margin=new Padding(0,4,0,0);dupeDirectMode.ForeColor=Theme.Danger;dupeDirectMode.Font=Theme.Strong;
   dupeVaultMode.CheckedChanged+=(s,e)=>UpdateDupeSummary();dupeDirectMode.CheckedChanged+=(s,e)=>UpdateDupeSummary();
   modePanel.Controls.Add(modeLabel);modePanel.Controls.Add(dupeVaultMode);modePanel.Controls.Add(dupeDirectMode);

   dupeRootLabel=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true};Theme.BorderBottom(dupeRootLabel);
   var note=Theme.Note("So khớp nội dung bằng SHA-256; chỉ các tệp giống hệt nhau mới được gom nhóm. Không quét thư mục hệ thống, Program Files hay dữ liệu Tweek Pro. Bản giữ lại không bao giờ bị xóa; các bản còn lại chuyển vào kho (mặc định) và có thể khôi phục.",NoteKind.Warning);
   dupeStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   dupeSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(dupeSummary);
   tab.Controls.Add(host);tab.Controls.Add(dupeSummary);tab.Controls.Add(dupeStage);tab.Controls.Add(note);tab.Controls.Add(dupeRootLabel);tab.Controls.Add(modePanel);tab.Controls.Add(keepPanel);tab.Controls.Add(bar);
   Theme.SetOverlay(dupeOverlay,"Chưa quét.\r\nChọn một thư mục (ví dụ Downloads) rồi bấm Quét trùng lặp để tìm các tệp giống hệt nhau. Chưa có gì bị thay đổi cho đến khi bạn bấm Dọn bản trùng đã chọn và xác nhận.",NoteKind.Info);
   dupeRoot=DefaultDupeRoot();dupeRootLabel.Text="Thư mục quét: "+(String.IsNullOrEmpty(dupeRoot)?"(chưa chọn)":dupeRoot);
   UpdateDupeSummary();
  }

  /// <summary>Suggests the current user's Downloads folder as the initial scan target when it exists.</summary>
  static string DefaultDupeRoot(){
   try{string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    if(!String.IsNullOrWhiteSpace(profile)){string downloads=Path.Combine(profile,"Downloads");if(Directory.Exists(downloads))return downloads;}
   }catch(Exception){}
   return null;
  }

  void ChooseDupeRoot(){
   using(var dialog=new FolderBrowserDialog{Description="Chọn thư mục để tìm tệp trùng lặp",ShowNewFolderButton=false}){
    if(!String.IsNullOrEmpty(dupeRoot)&&Directory.Exists(dupeRoot))dialog.SelectedPath=dupeRoot;
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    dupeRoot=dialog.SelectedPath;dupeRootLabel.Text="Thư mục quét: "+dupeRoot;
   }
  }

  /// <summary>Redraws the group list; recomputes the keeper of every group from the current keep-mode.</summary>
  void RenderDupes(bool measured){
   bool keepNewest=dupeKeepNewest.Checked;
   dupeList.BeginUpdate();dupeList.Items.Clear();
   foreach(var g in dupeResult.Groups){
    g.Keeper=DuplicateFinder.SelectKeeper(g.Files,keepNewest);
    var row=new ListViewItem(new[]{Path.GetFileName(g.Keeper.Path),g.Count.ToString("N0"),Presentation.BytesLabel(g.Bytes),Presentation.BytesLabel(g.Wasted),Presentation.ShortPath(Path.GetDirectoryName(g.Keeper.Path)??"",64)}){Tag=g,ToolTipText="Giữ lại: "+g.Keeper.Path+"\r\nSố bản: "+g.Count+"\r\nTiết kiệm nếu dọn: "+Presentation.BytesLabel(g.Wasted)};
    Theme.StripeRow(row,dupeList.Items.Count);dupeList.Items.Add(row);
    row.Checked=measured;
   }
   dupeList.EndUpdate();UpdateDupeSummary();
  }

  void SetDupeChecks(bool value){dupeList.BeginUpdate();foreach(ListViewItem item in dupeList.Items){var g=(DuplicateGroup)item.Tag;item.Checked=value&&g.Count>=2;}dupeList.EndUpdate();UpdateDupeSummary();}

  List<DuplicateGroup> CheckedDupes(){return dupeList.CheckedItems.Cast<ListViewItem>().Select(i=>(DuplicateGroup)i.Tag).Where(g=>g.Count>=2).ToList();}

  void UpdateDupeSummary(){
   var selected=CheckedDupes();long wasted=selected.Sum(g=>g.Wasted);int copies=selected.Sum(g=>g.Count-1);
   bool direct=dupeDirectMode.Checked;
   dupeSummary.Text=dupeResult.Groups.Count+" nhóm trùng lặp  •  Đã chọn "+selected.Count+" nhóm, "+copies.ToString("N0")+" bản dư, giải phóng ~"+Presentation.BytesLabel(wasted)+(direct?"  •  XÓA THẲNG: không thể khôi phục":"  •  Chuyển vào Kho: chưa giải phóng cho đến khi xóa vĩnh viễn");
   dupeSummary.ForeColor=direct?Theme.Danger:Theme.Muted;
   if(dupeQuarantineButton!=null)dupeQuarantineButton.Text=direct?"Xóa thẳng bản trùng đã chọn":"Dọn bản trùng đã chọn (vào kho)";
  }

  /// <summary>Runs the read-only duplicate scan on a worker thread and shows the resulting groups.</summary>
  async Task ScanDupes(){
   if(String.IsNullOrEmpty(dupeRoot))throw new IOException("Chọn một thư mục để quét trước.");
   DupeSafety.ValidateScanRoot(dupeRoot);
   dupeCancellation=new CancellationTokenSource();var token=dupeCancellation.Token;
   dupeStage.Visible=true;dupeStage.Text="Đang quét…";Theme.SetOverlay(dupeOverlay,null,NoteKind.Info);
   Log("Tìm trùng lặp: đang quét (chỉ đọc) "+dupeRoot+".");
   long minBytes=(long)Math.Max(0,settings.DuplicateMinKB)*1024;string root=dupeRoot;
   try{
    dupeResult=await Task.Run(()=>DuplicateFinder.Scan(root,minBytes,token,ReportDupe),token);
    RenderDupes(true);
    dupeStage.Text="Quét xong: "+dupeResult.Groups.Count+" nhóm trùng, "+dupeResult.FilesScanned.ToString("N0")+" tệp đã duyệt, tiết kiệm tối đa "+Presentation.BytesLabel(dupeResult.WastedBytes)+(dupeResult.Partial?"  •  (chưa đủ)":"");
    Log("Tìm trùng lặp: "+dupeResult.Groups.Count+" nhóm, có thể giải phóng "+Presentation.BytesLabel(dupeResult.WastedBytes)+".");
    if(dupeResult.Groups.Count==0)Theme.SetOverlay(dupeOverlay,"Không tìm thấy tệp trùng lặp trong thư mục này (ngưỡng tối thiểu "+settings.DuplicateMinKB+" KB).",NoteKind.Info);
   }catch(OperationCanceledException){dupeStage.Text="Đã dừng quét.";}
   finally{dupeCancellation.Dispose();dupeCancellation=null;}
  }

  void ReportDupe(DuplicateProgress p){
   if(IsDisposed||!IsHandleCreated)return;
   try{BeginInvoke((Action)(()=>{if(!IsDisposed)dupeStage.Text=p.Stage+(String.IsNullOrEmpty(p.Current)?"":"   "+Presentation.ShortPath(p.Current,70))+(p.Files>0?"   •   "+p.Files.ToString("N0")+" tệp":"");}));}catch(InvalidOperationException){}
  }

  /// <summary>Confirms and quarantines (or deletes) the redundant copies of the checked groups, keeping one copy each.</summary>
  async Task QuarantineDupes(){
   var selected=CheckedDupes();if(selected.Count==0)throw new IOException("Quét rồi đánh dấu nhóm muốn dọn.");
   bool keepNewest=dupeKeepNewest.Checked;foreach(var g in selected)g.Keeper=DuplicateFinder.SelectKeeper(g.Files,keepNewest);
   bool direct=dupeDirectMode.Checked;long wasted=selected.Sum(g=>g.Wasted);int copies=selected.Sum(g=>g.Count-1);
   string list=String.Join("\r\n",selected.Take(8).Select(g=>"• Giữ "+Path.GetFileName(g.Keeper.Path)+" — bỏ "+(g.Count-1)+" bản ("+Presentation.BytesLabel(g.Wasted)+")"));
   if(direct){
    var answer=MessageBox.Show(this,"XÓA THẲNG "+copies.ToString("N0")+" bản trùng ("+Presentation.BytesLabel(wasted)+") mà KHÔNG lưu bản khôi phục?\r\n\r\n"+list+"\r\n\r\nBản giữ lại của mỗi nhóm vẫn được giữ. Thao tác này không thể hoàn tác.","Xác nhận xóa vĩnh viễn",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
    if(answer!=DialogResult.Yes)return;
    if(!Confirm("Xác nhận lần cuối: xóa thẳng "+copies.ToString("N0")+" bản trùng, không có bản sao lưu?"))return;
   }else{
    if(!Confirm("Chuyển "+copies.ToString("N0")+" bản trùng ("+Presentation.BytesLabel(wasted)+") vào Kho khôi phục?\r\n\r\n"+list+"\r\n\r\nLần dọn này tạo một bản sao lưu; có thể khôi phục hoặc xóa vĩnh viễn sau trong tab Kho khôi phục. Dung lượng ổ đĩa chưa được giải phóng cho đến khi xóa vĩnh viễn."))return;
   }
   dupeCancellation=new CancellationTokenSource();var token=dupeCancellation.Token;dupeStage.Visible=true;dupeStage.Text=direct?"Đang xóa thẳng…":"Đang chuyển vào kho…";
   Log("Tìm trùng lặp: bắt đầu "+(direct?"xóa thẳng ":"chuyển vào kho ")+copies.ToString("N0")+" bản trùng trong "+selected.Count+" nhóm.");
   // Snapshot the groups so a keeper switch on the UI thread cannot change what the worker removes.
   string root=dupeRoot;var groups=selected.Select(g=>new DuplicateGroup{Hash=g.Hash,Bytes=g.Bytes,Files=g.Files.ToList(),Keeper=g.Keeper}).ToList();
   DuplicateReport report;
   try{report=await Task.Run(()=>DuplicateFinder.Quarantine(root,groups,direct,token,ReportDupe),token);}
   catch(OperationCanceledException){dupeStage.Text="Đã dừng dọn.";return;}
   finally{dupeCancellation.Dispose();dupeCancellation=null;}
   LoadBackups();
   string summary=(direct?"Đã xóa thẳng ":"Đã chuyển vào kho ")+report.Quarantined.ToString("N0")+" bản trùng ("+Presentation.BytesLabel(report.Bytes)+"). Bỏ qua vì đang dùng: "+report.SkippedInUse.ToString("N0")+". Lỗi: "+report.Failed.ToString("N0")+"."+(direct?"":" Bản sao lưu: "+report.Backups.Count+".");
   dupeStage.Text=summary;Log("Tìm trùng lặp: "+summary);
   foreach(string err in report.Errors.Take(20))Log("Dọn trùng lỗi: "+err);
   MessageBox.Show(this,summary+(report.Errors.Count>0?"\r\n\r\nLỗi đầu tiên:\r\n"+String.Join("\r\n",report.Errors.Take(5)):"")+(direct?"":"\r\n\r\nCó thể khôi phục hoặc xóa vĩnh viễn trong tab Kho khôi phục."),"Kết quả dọn trùng lặp",MessageBoxButtons.OK,report.Failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
   await ScanDupes();
  }

  void ShowDupeDetails(){
   if(dupeList.SelectedItems.Count==0)throw new IOException("Chọn một nhóm để xem các bản.");
   var g=(DuplicateGroup)dupeList.SelectedItems[0].Tag;
   using(var form=new DuplicateDetailForm(g))form.ShowDialog(this);
  }
 }

 /// <summary>Read-only list of the files in one duplicate group, marking the copy that will be kept.</summary>
 public class DuplicateDetailForm:Form {
  public DuplicateDetailForm(DuplicateGroup group){
   Text="Các bản trùng — "+Path.GetFileName(group.Keeper.Path);Size=new Size(980,560);MinimumSize=new Size(720,420);StartPosition=FormStartPosition.CenterParent;ShowIcon=false;
   Font=Theme.Body;BackColor=Theme.Canvas;ForeColor=Theme.Text;AutoScaleMode=AutoScaleMode.Dpi;
   var header=Theme.HeaderBand("Nhóm trùng lặp",group.Count+" bản  •  "+Presentation.BytesLabel(group.Bytes)+" mỗi tệp  •  tiết kiệm "+Presentation.BytesLabel(group.Wasted),84);
   var list=new SmoothListView();Theme.StyleList(list);list.Columns.Add("Vai trò",90);list.Columns.Add("Đường dẫn",640);list.Columns.Add("Sửa lần cuối",150);
   int shown=0;list.BeginUpdate();
   foreach(var file in group.Files.OrderBy(f=>f.Path,StringComparer.OrdinalIgnoreCase)){
    bool keeper=ReferenceEquals(file,group.Keeper);
    var row=new ListViewItem(new[]{keeper?"Giữ lại":"Trùng",file.Path,file.LastWrite.ToString("dd/MM/yyyy HH:mm")}){ToolTipText=file.Path};
    if(keeper)row.ForeColor=Theme.Success;
    Theme.StripeRow(row,shown++);list.Items.Add(row);
   }
   list.EndUpdate();
   var note=Theme.Note("Bản “Giữ lại” không bị xóa. Danh sách chỉ để xem; chưa có gì bị thay đổi.",NoteKind.Info);
   var footer=new Panel{Dock=DockStyle.Bottom,Height=64,BackColor=Theme.Surface,Padding=new Padding(20,14,20,14)};Theme.BorderTop(footer);
   var close=Theme.Button("Đóng",ButtonStyle.Primary);close.Dock=DockStyle.Right;close.AutoSize=false;close.Width=120;close.Click+=(s,e)=>Close();
   footer.Controls.Add(close);
   var hostPanel=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Surface};hostPanel.Controls.Add(list);
   Controls.Add(hostPanel);Controls.Add(note);Controls.Add(header);Controls.Add(footer);
  }
 }
}
