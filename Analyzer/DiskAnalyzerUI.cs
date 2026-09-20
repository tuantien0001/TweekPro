using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Analyzer;

namespace TweekPro {
 public partial class MainForm {
  ListView analyzerList=new SmoothListView();Label analyzerOverlay,analyzerSummary,analyzerStage,analyzerRootLabel;TabPage analyzerTab;
  DiskReport analyzerReport=new DiskReport();string analyzerRoot;CancellationTokenSource analyzerCancellation;

  /// <summary>Builds the read-only Disk Analyzer tab: pick a folder and see heavy child folders, usage by extension and the largest files.</summary>
  void BuildAnalyzerTab(){
   var tab=analyzerTab=new TabPage("Phân tích ổ đĩa");
   SetupList(analyzerList,new[]{"Mục","Dung lượng","Chi tiết"},new[]{620,150,240},false,true);
   analyzerList.DoubleClick+=async(s,e)=>await Guard(()=>{if(analyzerList.SelectedItems.Count>0)OpenAnalyzerLocation(true);return Task.FromResult(0);});
   var host=Theme.ListHost(analyzerList,out analyzerOverlay);

   var bar=Bar();
   Add(bar,"Chọn thư mục…",()=>{ChooseAnalyzerRoot();return Task.FromResult(0);});
   Add(bar,"Phân tích",async()=>await AnalyzeDisk(),ButtonStyle.Primary);
   Add(bar,"Mở vị trí",()=>{OpenAnalyzerLocation();return Task.FromResult(0);});
   Add(bar,"Xuất CSV",()=>{ExportAnalyzer();return Task.FromResult(0);});

   analyzerRootLabel=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true};Theme.BorderBottom(analyzerRootLabel);
   var note=Theme.Note("Chỉ đọc — không thay đổi gì trên đĩa. Hiển thị thư mục con chiếm nhiều dung lượng nhất, dung lượng theo phần mở rộng và các tệp lớn nhất để bạn tự quyết định. Bỏ qua liên kết (junction/symlink); giới hạn 1.000.000 tệp / 120 giây.",NoteKind.Info);
   analyzerStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   analyzerSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(analyzerSummary);
   tab.Controls.Add(host);tab.Controls.Add(analyzerSummary);tab.Controls.Add(analyzerStage);tab.Controls.Add(note);tab.Controls.Add(analyzerRootLabel);tab.Controls.Add(bar);
   Theme.SetOverlay(analyzerOverlay,"Chưa phân tích.\r\nChọn một thư mục rồi bấm Phân tích để xem dung lượng bị chiếm bởi thư mục con, phần mở rộng và tệp lớn nhất. Thao tác chỉ đọc, an toàn tuyệt đối.",NoteKind.Info);
   analyzerRoot=DefaultAnalyzerRoot();analyzerRootLabel.Text="Thư mục phân tích: "+(String.IsNullOrEmpty(analyzerRoot)?"(chưa chọn)":analyzerRoot);
   UpdateAnalyzerSummary();
  }

  /// <summary>Suggests the current user's profile folder as the initial analysis target.</summary>
  static string DefaultAnalyzerRoot(){
   try{string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);if(!String.IsNullOrWhiteSpace(profile)&&Directory.Exists(profile))return profile;}catch(Exception){}
   return null;
  }

  void ChooseAnalyzerRoot(){
   using(var dialog=new FolderBrowserDialog{Description="Chọn thư mục để phân tích dung lượng",ShowNewFolderButton=false}){
    if(!String.IsNullOrEmpty(analyzerRoot)&&Directory.Exists(analyzerRoot))dialog.SelectedPath=analyzerRoot;
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    analyzerRoot=dialog.SelectedPath;analyzerRootLabel.Text="Thư mục phân tích: "+analyzerRoot;
   }
  }

  void UpdateAnalyzerSummary(){
   analyzerSummary.Text=Presentation.BytesLabel(analyzerReport.TotalBytes)+" trong "+analyzerReport.TotalFiles.ToString("N0")+" tệp, "+analyzerReport.TotalFolders.ToString("N0")+" thư mục con"+(analyzerReport.Partial?"  •  (chưa đủ — đã chạm giới hạn)":"")+"  •  Nhấp đúp một dòng để mở vị trí.";
  }

  /// <summary>Runs the read-only analysis on a worker thread and renders folders, extensions and the largest files.</summary>
  async Task AnalyzeDisk(){
   if(String.IsNullOrEmpty(analyzerRoot))throw new IOException("Chọn một thư mục để phân tích trước.");
   analyzerCancellation=new CancellationTokenSource();var token=analyzerCancellation.Token;
   analyzerStage.Visible=true;analyzerStage.Text="Đang phân tích…";Theme.SetOverlay(analyzerOverlay,null,NoteKind.Info);
   Log("Phân tích ổ đĩa: đang quét (chỉ đọc) "+analyzerRoot+".");
   string root=analyzerRoot;
   try{
    analyzerReport=await Task.Run(()=>DiskAnalyzer.Analyze(root,200,token,ReportAnalyze),token);
    RenderAnalyzer();
    analyzerStage.Text="Xong: "+Presentation.BytesLabel(analyzerReport.TotalBytes)+" trong "+analyzerReport.TotalFiles.ToString("N0")+" tệp"+(analyzerReport.Partial?"  •  (chưa đủ)":"");
    Log("Phân tích ổ đĩa: "+Presentation.BytesLabel(analyzerReport.TotalBytes)+", "+analyzerReport.TotalFiles.ToString("N0")+" tệp, "+analyzerReport.Folders.Count+" nhóm thư mục.");
    if(analyzerReport.TotalFiles==0)Theme.SetOverlay(analyzerOverlay,"Thư mục này không có tệp nào đọc được.",NoteKind.Info);
   }catch(OperationCanceledException){analyzerStage.Text="Đã dừng phân tích.";}
   finally{analyzerCancellation.Dispose();analyzerCancellation=null;}
  }

  void ReportAnalyze(AnalyzeProgress p){
   if(IsDisposed||!IsHandleCreated)return;
   try{BeginInvoke((Action)(()=>{if(!IsDisposed)analyzerStage.Text=p.Stage+(String.IsNullOrEmpty(p.Current)?"":"   "+Presentation.ShortPath(p.Current,70))+(p.Files>0?"   •   "+p.Files.ToString("N0")+" tệp, "+Presentation.BytesLabel(p.Bytes):"");}));}catch(InvalidOperationException){}
  }

  void RenderAnalyzer(){
   analyzerList.BeginUpdate();analyzerList.Items.Clear();analyzerList.Groups.Clear();
   foreach(var folder in analyzerReport.Folders.Take(200)){
    var row=new ListViewItem(new[]{Presentation.ShortPath(folder.Path,80),Presentation.BytesLabel(folder.Bytes),folder.Files.ToString("N0")+" tệp"}){Tag=folder,ToolTipText=folder.Path};
    Theme.AssignGroup(analyzerList,row,"1-folder","Thư mục con nặng nhất");Theme.StripeRow(row,analyzerList.Items.Count);analyzerList.Items.Add(row);
   }
   foreach(var ext in analyzerReport.Extensions.Take(80)){
    var row=new ListViewItem(new[]{ext.Extension,Presentation.BytesLabel(ext.Bytes),ext.Files.ToString("N0")+" tệp"}){Tag=ext,ToolTipText="Tổng dung lượng của các tệp "+ext.Extension};
    Theme.AssignGroup(analyzerList,row,"2-ext","Dung lượng theo phần mở rộng");Theme.StripeRow(row,analyzerList.Items.Count);analyzerList.Items.Add(row);
   }
   foreach(var file in analyzerReport.LargestFiles.Take(200)){
    var row=new ListViewItem(new[]{Presentation.ShortPath(file.Path,80),Presentation.BytesLabel(file.Bytes),file.LastWrite==DateTime.MinValue?"":file.LastWrite.ToString("dd/MM/yyyy HH:mm")}){Tag=file,ToolTipText=file.Path};
    Theme.AssignGroup(analyzerList,row,"3-file","Tệp lớn nhất");Theme.StripeRow(row,analyzerList.Items.Count);analyzerList.Items.Add(row);
   }
   analyzerList.EndUpdate();UpdateAnalyzerSummary();
   if(analyzerReport.Folders.Count==0&&analyzerReport.LargestFiles.Count==0)Theme.SetOverlay(analyzerOverlay,"Không có dữ liệu để hiển thị.",NoteKind.Info);
   else Theme.SetOverlay(analyzerOverlay,null,NoteKind.Info);
  }

  /// <summary>Opens Explorer at the selected folder, or selects the chosen file; extension rows have no location.</summary>
  /// <summary>Opens the selected folder or file in Explorer; extension rows have no location and are ignored quietly on double-click.</summary>
  void OpenAnalyzerLocation(bool quiet=false){
   if(analyzerList.SelectedItems.Count==0)throw new IOException("Chọn một dòng thư mục hoặc tệp.");
   object tag=analyzerList.SelectedItems[0].Tag;
   var folder=tag as FolderUsage;var file=tag as LargeFile;
   if(folder!=null&&Directory.Exists(folder.Path))System.Diagnostics.Process.Start("explorer.exe","\""+folder.Path+"\"");
   else if(file!=null&&File.Exists(file.Path))System.Diagnostics.Process.Start("explorer.exe","/select,\""+file.Path+"\"");
   else if(!quiet)throw new IOException("Dòng theo phần mở rộng không có vị trí cụ thể để mở.");
  }

  void ExportAnalyzer(){
   if(analyzerReport.TotalFiles==0)throw new IOException("Chưa có kết quả để xuất. Hãy Phân tích trước.");
   string path=SavePath("TweekPro-disk-analysis.csv");if(path==null)return;
   var lines=new List<string>{"Category,Item,Bytes,Files,LastWrite"};
   lines.AddRange(analyzerReport.Folders.Select(f=>String.Join(",",new[]{"Folder",f.Path,f.Bytes.ToString(),f.Files.ToString(),""}.Select(Engine.Csv))));
   lines.AddRange(analyzerReport.Extensions.Select(e=>String.Join(",",new[]{"Extension",e.Extension,e.Bytes.ToString(),e.Files.ToString(),""}.Select(Engine.Csv))));
   lines.AddRange(analyzerReport.LargestFiles.Select(f=>String.Join(",",new[]{"File",f.Path,f.Bytes.ToString(),"1",f.LastWrite==DateTime.MinValue?"":f.LastWrite.ToString("s")}.Select(Engine.Csv))));
   File.WriteAllLines(path,lines,new UTF8Encoding(true));Log("Đã xuất phân tích ổ đĩa: "+path);
  }
 }
}
