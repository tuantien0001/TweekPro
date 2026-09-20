using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Cleaner;

namespace TweekPro {
 public partial class MainForm {
  ListView emptyList=new SmoothListView();Label emptyOverlay,emptySummary,emptyStage,emptyRootLabel;TabPage emptyTab;
  EmptyFolderResult emptyResult=new EmptyFolderResult();string emptyRoot;CancellationTokenSource emptyCancellation;

  /// <summary>Builds the Empty Folders tab: pick a folder, find truly empty directory branches and delete the selected ones.</summary>
  void BuildEmptyTab(){
   var tab=emptyTab=new TabPage("Thư mục rỗng");
   SetupList(emptyList,new[]{"Thư mục rỗng (xóa sẽ dọn cả nhánh con rỗng)","Số nhánh con"},new[]{700,110},true);
   emptyList.ItemChecked+=(s,e)=>UpdateEmptySummary();
   emptyList.DoubleClick+=(s,e)=>OpenEmptyLocation();
   var host=Theme.ListHost(emptyList,out emptyOverlay);

   var bar=Bar();
   Add(bar,"Chọn thư mục…",()=>{ChooseEmptyRoot();return Task.FromResult(0);});
   Add(bar,"Quét thư mục rỗng",async()=>await ScanEmpty(),ButtonStyle.Primary);
   Add(bar,"Chọn tất cả",()=>{SetEmptyChecks(true);return Task.FromResult(0);});
   Add(bar,"Bỏ chọn",()=>{SetEmptyChecks(false);return Task.FromResult(0);});
   Add(bar,"Xóa thư mục đã chọn",async()=>await RemoveEmpty(),ButtonStyle.Danger);
   Add(bar,"Mở vị trí",()=>{OpenEmptyLocation();return Task.FromResult(0);});

   emptyRootLabel=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true};Theme.BorderBottom(emptyRootLabel);
   var note=Theme.Note("Chỉ liệt kê thư mục hoàn toàn rỗng (không chứa tệp nào ở mọi cấp con). Bỏ qua liên kết và không đụng thư mục hệ thống, Program Files hay dữ liệu Tweek Pro. Thư mục rỗng không chứa dữ liệu nên được xóa thẳng sau khi xác nhận; ứng dụng có thể tự tạo lại khi cần.",NoteKind.Warning);
   emptyStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   emptySummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(emptySummary);
   tab.Controls.Add(host);tab.Controls.Add(emptySummary);tab.Controls.Add(emptyStage);tab.Controls.Add(note);tab.Controls.Add(emptyRootLabel);tab.Controls.Add(bar);
   Theme.SetOverlay(emptyOverlay,"Chưa quét.\r\nChọn một thư mục rồi bấm Quét thư mục rỗng. Chưa có gì bị thay đổi cho đến khi bạn bấm Xóa thư mục đã chọn và xác nhận.",NoteKind.Info);
   emptyRoot=DefaultEmptyRoot();emptyRootLabel.Text="Thư mục quét: "+(String.IsNullOrEmpty(emptyRoot)?"(chưa chọn)":emptyRoot);
   UpdateEmptySummary();
  }

  static string DefaultEmptyRoot(){
   try{string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);if(!String.IsNullOrWhiteSpace(profile)){string d=Path.Combine(profile,"Downloads");if(Directory.Exists(d))return d;if(Directory.Exists(profile))return profile;}}catch(Exception){}
   return null;
  }

  void ChooseEmptyRoot(){
   using(var dialog=new FolderBrowserDialog{Description="Chọn thư mục để tìm thư mục rỗng",ShowNewFolderButton=false}){
    if(!String.IsNullOrEmpty(emptyRoot)&&Directory.Exists(emptyRoot))dialog.SelectedPath=emptyRoot;
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    emptyRoot=dialog.SelectedPath;emptyRootLabel.Text="Thư mục quét: "+emptyRoot;
   }
  }

  void RenderEmpty(){
   emptyList.BeginUpdate();emptyList.Items.Clear();
   foreach(string folder in emptyResult.Folders){
    int subBranches=CountSubdirectories(folder);
    var row=new ListViewItem(new[]{Presentation.ShortPath(folder,90),subBranches.ToString("N0")}){Tag=folder,ToolTipText=folder,Checked=true};
    Theme.StripeRow(row,emptyList.Items.Count);emptyList.Items.Add(row);
   }
   emptyList.EndUpdate();UpdateEmptySummary();
   if(emptyResult.Folders.Count==0)Theme.SetOverlay(emptyOverlay,"Không tìm thấy thư mục rỗng nào trong thư mục này.",NoteKind.Info);
   else Theme.SetOverlay(emptyOverlay,null,NoteKind.Info);
  }

  static int CountSubdirectories(string folder){
   try{return Directory.EnumerateDirectories(folder,"*",SearchOption.AllDirectories).Count();}catch(Exception){return 0;}
  }

  void SetEmptyChecks(bool value){emptyList.BeginUpdate();foreach(ListViewItem item in emptyList.Items)item.Checked=value;emptyList.EndUpdate();UpdateEmptySummary();}
  List<string> CheckedEmpty(){return emptyList.CheckedItems.Cast<ListViewItem>().Select(i=>(string)i.Tag).ToList();}

  void UpdateEmptySummary(){
   emptySummary.Text=emptyResult.Folders.Count+" nhánh thư mục rỗng  •  Đã chọn "+CheckedEmpty().Count+(emptyResult.Partial?"  •  (chưa đủ — đã chạm giới hạn)":"")+"  •  Nhấp đúp để mở vị trí.";
  }

  /// <summary>Runs the read-only empty-folder scan on a worker thread and lists the results.</summary>
  async Task ScanEmpty(){
   if(String.IsNullOrEmpty(emptyRoot))throw new IOException("Chọn một thư mục để quét trước.");
   emptyCancellation=new CancellationTokenSource();var token=emptyCancellation.Token;
   emptyStage.Visible=true;emptyStage.Text="Đang quét…";Theme.SetOverlay(emptyOverlay,null,NoteKind.Info);
   Log("Thư mục rỗng: đang quét (chỉ đọc) "+emptyRoot+".");
   string root=emptyRoot;
   try{
    emptyResult=await Task.Run(()=>EmptyFolders.Find(root,token,ReportEmpty),token);
    RenderEmpty();
    emptyStage.Text="Quét xong: "+emptyResult.Folders.Count+" nhánh rỗng trong "+emptyResult.Scanned.ToString("N0")+" thư mục đã duyệt"+(emptyResult.Partial?"  •  (chưa đủ)":"");
    Log("Thư mục rỗng: tìm thấy "+emptyResult.Folders.Count+" nhánh rỗng.");
   }catch(OperationCanceledException){emptyStage.Text="Đã dừng quét.";}
   finally{emptyCancellation.Dispose();emptyCancellation=null;}
  }

  void ReportEmpty(string current){
   if(IsDisposed||!IsHandleCreated)return;
   try{BeginInvoke((Action)(()=>{if(!IsDisposed)emptyStage.Text="Đang quét   "+Presentation.ShortPath(current,70);}));}catch(InvalidOperationException){}
  }

  /// <summary>Confirms and deletes the checked empty folders (each rechecked as empty at delete time).</summary>
  async Task RemoveEmpty(){
   var selected=CheckedEmpty();if(selected.Count==0)throw new IOException("Quét rồi đánh dấu thư mục rỗng muốn xóa.");
   if(!Confirm("Xóa "+selected.Count+" nhánh thư mục rỗng?\r\n\r\n"+String.Join("\r\n",selected.Take(10).Select(f=>Presentation.ShortPath(f,80)))+(selected.Count>10?"\r\n… và "+(selected.Count-10)+" mục khác":"")+"\r\n\r\nCác thư mục này không chứa tệp nào. Thao tác xóa thẳng (không sao lưu vì không có dữ liệu); Windows/ứng dụng có thể tự tạo lại khi cần."))return;
   emptyCancellation=new CancellationTokenSource();var token=emptyCancellation.Token;emptyStage.Visible=true;emptyStage.Text="Đang xóa…";
   string root=emptyRoot;var folders=selected;
   EmptyFolderReport report;
   try{report=await Task.Run(()=>EmptyFolders.Remove(root,folders,token),token);}
   catch(OperationCanceledException){emptyStage.Text="Đã dừng xóa.";return;}
   finally{emptyCancellation.Dispose();emptyCancellation=null;}
   string summary="Đã xóa "+report.Removed+" nhánh thư mục rỗng. Bỏ qua: "+report.Skipped+". Lỗi: "+report.Failed+".";
   emptyStage.Text=summary;Log("Thư mục rỗng: "+summary);
   foreach(string err in report.Errors.Take(20))Log("Thư mục rỗng lỗi: "+err);
   MessageBox.Show(this,summary,"Kết quả dọn thư mục rỗng",MessageBoxButtons.OK,report.Failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
   await ScanEmpty();
  }

  void OpenEmptyLocation(){
   if(emptyList.SelectedItems.Count==0)throw new IOException("Chọn một thư mục.");
   string folder=(string)emptyList.SelectedItems[0].Tag;
   if(Directory.Exists(folder))System.Diagnostics.Process.Start("explorer.exe","\""+folder+"\"");
  }
 }
}
