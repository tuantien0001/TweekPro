using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppCare {
 /// <summary>
 /// Dedicated leftover-review window shown right after an uninstall finishes.
 /// It scans in the background with live progress, lists files, folders and registry
 /// entries with their footprint, and deletes (with backup) only the items the user checks.
 /// </summary>
 public class LeftoverScanForm:Form {
  readonly List<AppEntry> apps;
  readonly bool deep;
  readonly Action<string> log;
  readonly ListView list=new SmoothListView();
  readonly ProgressBar progress=new ProgressBar();
  readonly Label stage=new Label(),counters=new Label(),summary=new Label(),banner;
  readonly Button selectAll,selectNone,delete,stop,close;
  readonly Dictionary<Candidate,ListViewItem> rows=new Dictionary<Candidate,ListViewItem>();
  readonly Dictionary<Candidate,Footprint> footprints=new Dictionary<Candidate,Footprint>();
  readonly Stopwatch throttle=Stopwatch.StartNew();
  CancellationTokenSource cancellation;
  bool scanning,deleting,locked,previewMode,closeRequested;
  int visited,noteCount;

  public List<Candidate> Remaining=new List<Candidate>();
  public List<string> Notes=new List<string>();
  public int Found,Deleted,Failed;

  /// <summary>Creates the window for the given applications; scanning starts when the window is shown.</summary>
  public LeftoverScanForm(IEnumerable<AppEntry> targets,bool deepScan,Action<string> logger){
   apps=targets.ToList();deep=deepScan;log=logger??(s=>{});
   string names=String.Join(", ",apps.Select(a=>a.Name));
   Text="Phần còn sót — "+names;Size=new Size(1180,760);MinimumSize=new Size(960,600);StartPosition=FormStartPosition.CenterParent;
   Font=Theme.Body;BackColor=Theme.Canvas;ForeColor=Theme.Text;AutoScaleMode=AutoScaleMode.Dpi;ShowIcon=false;

   var header=Theme.HeaderBand("Quét phần còn sót",(deep?"Quét sâu":"Quét nhanh")+"  •  "+names,92);

   var progressPanel=new Panel{Dock=DockStyle.Top,Height=84,BackColor=Theme.Surface,Padding=new Padding(28,16,28,14)};
   Theme.BorderBottom(progressPanel);
   progress.Dock=DockStyle.Bottom;progress.Height=6;progress.Style=ProgressBarStyle.Marquee;progress.MarqueeAnimationSpeed=30;
   counters.Dock=DockStyle.Right;counters.Width=360;counters.TextAlign=ContentAlignment.MiddleRight;counters.ForeColor=Theme.Muted;counters.Font=Theme.Small;
   stage.Dock=DockStyle.Fill;stage.TextAlign=ContentAlignment.MiddleLeft;stage.AutoEllipsis=true;stage.Font=Theme.Strong;stage.Text="Đang chuẩn bị quét…";
   var textRow=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,0,0,10)};
   textRow.Controls.Add(stage);textRow.Controls.Add(counters);
   progressPanel.Controls.Add(textRow);progressPanel.Controls.Add(progress);

   banner=Theme.Note("",NoteKind.Info);banner.Visible=false;banner.Height=48;
   var note=Theme.Note("Các mục dưới đây chỉ là nghi ngờ còn sót và có thể chứa dữ liệu cá nhân. Hãy kiểm tra đường dẫn trước khi chọn. Xóa sẽ chuyển mục vào Kho khôi phục, không xóa vĩnh viễn.",NoteKind.Warning);

   Theme.StyleList(list);list.CheckBoxes=true;
   foreach(var column in new[]{new{Name="Loại",Width=120},new{Name="Đường dẫn",Width=520},new{Name="Dung lượng",Width=110},new{Name="Trạng thái",Width=150},new{Name="Cơ sở đề xuất",Width=420}})list.Columns.Add(column.Name,column.Width);
   list.ItemCheck+=(s,e)=>{var c=(Candidate)list.Items[e.Index].Tag;if(c.ReviewOnly||IsDone(list.Items[e.Index]))e.NewValue=CheckState.Unchecked;};
   list.ItemChecked+=(s,e)=>UpdateSummary();
   list.DoubleClick+=(s,e)=>Inspect();

   var footer=new Panel{Dock=DockStyle.Bottom,Height=68,BackColor=Theme.Surface,Padding=new Padding(28,16,20,16)};
   Theme.BorderTop(footer);
   summary.Dock=DockStyle.Fill;summary.TextAlign=ContentAlignment.MiddleLeft;summary.ForeColor=Theme.Muted;summary.AutoEllipsis=true;
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Right,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Padding=new Padding(0)};
   stop=Theme.Button("Dừng quét",ButtonStyle.Secondary);
   selectAll=Theme.Button("Chọn tất cả",ButtonStyle.Secondary);
   selectNone=Theme.Button("Bỏ chọn",ButtonStyle.Secondary);
   delete=Theme.Button("Xóa đã chọn",ButtonStyle.Danger);
   close=Theme.Button("Đóng",ButtonStyle.Primary);close.Margin=new Padding(0);
   stop.Click+=(s,e)=>{if(cancellation!=null)cancellation.Cancel();stop.Enabled=false;};
   selectAll.Click+=(s,e)=>SetChecks(true);
   selectNone.Click+=(s,e)=>SetChecks(false);
   delete.Click+=async(s,e)=>await DeleteChecked();
   close.Click+=(s,e)=>Close();
   buttons.Controls.AddRange(new Control[]{stop,selectAll,selectNone,delete,close});
   footer.Controls.Add(summary);footer.Controls.Add(buttons);

   Controls.Add(list);
   Controls.Add(note);
   Controls.Add(banner);
   Controls.Add(progressPanel);
   Controls.Add(header);
   Controls.Add(footer);

   SetBusy(true);
   Shown+=async(s,e)=>{if(!previewMode)await RunScan();};
   FormClosing+=(s,e)=>{
    if(deleting){e.Cancel=true;MessageBox.Show(this,"Đang sao lưu và xóa. Hãy chờ thao tác hoàn tất.","AppCare",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
    if(scanning&&cancellation!=null){closeRequested=true;cancellation.Cancel();e.Cancel=true;return;}
    Remaining=list.Items.Cast<ListViewItem>().Where(i=>!IsDone(i)).Select(i=>(Candidate)i.Tag).ToList();
   };
   UpdateSummary();
  }

  /// <summary>Runs the scan for every application, then measures footprints and unlocks the review actions.</summary>
  async Task RunScan(){
   scanning=true;cancellation=new CancellationTokenSource();var token=cancellation.Token;bool cancelled=false;SetBusy(true);
   try{
    locked=await Task.Run(()=>apps.Any(a=>{try{return Engine.Installed(a.Id);}catch(Exception){return false;}}));
    foreach(var app in apps){
     var target=app;
     log((deep?"Quét sâu: ":"Quét nhanh: ")+target.Name);
     var result=await Task.Run(()=>deep?Advanced.DeepScan(target,token,null,Report):Advanced.QuickScan(target,Report),token);
     visited+=result.Visited;noteCount+=result.Notes.Count;Notes.AddRange(result.Notes);
     foreach(string n in result.Notes.Take(30))log(target.Name+": "+n);
     if(result.Notes.Count>30)log("Còn "+(result.Notes.Count-30)+" ghi chú được rút gọn.");
     AddCandidates(result.Items);
    }
   }catch(OperationCanceledException){cancelled=true;}
   catch(Exception e){ShowBanner("Không hoàn tất quét: "+e.Message,NoteKind.Error);log("LỖI quét: "+e.Message);}
   finally{scanning=false;}
   if(!token.IsCancellationRequested){
    stage.Text="Đang tính dung lượng các mục tìm thấy…";
    try{await MeasureAll(token);}catch(OperationCanceledException){cancelled=true;}
   }
   cancellation.Dispose();cancellation=null;
   progress.Style=ProgressBarStyle.Continuous;progress.Value=100;
   Found=list.Items.Count;
   stage.Text=(cancelled?"Đã dừng quét — kết quả có thể chưa đầy đủ.":"Hoàn tất quét.")+"  Tìm thấy "+Found+" mục.";
   counters.Text=visited.ToString("N0")+" thư mục đã duyệt  •  "+noteCount+" ghi chú (xem Nhật ký)";
   if(Found==0)ShowBanner(cancelled?"Chưa tìm thấy mục nào trước khi dừng. Có thể quét lại từ tab Phần còn sót.":"Không tìm thấy phần còn sót theo các quy tắc hiện tại. Điều này không bảo đảm hệ thống đã sạch hoàn toàn.",NoteKind.Success);
   else if(locked)ShowBanner("Ứng dụng vẫn còn đăng ký cài đặt nên chỉ có thể xem. Hãy gỡ chính thức rồi quét lại để dọn.",NoteKind.Warning);
   SetBusy(false);
   if(closeRequested){Close();return;}
   log("Cửa sổ quét: "+Found+" mục, "+visited.ToString("N0")+" thư mục đã duyệt, "+noteCount+" ghi chú.");
  }

  /// <summary>Marshals a worker-thread progress snapshot to the UI, throttled to avoid flooding the message loop.</summary>
  void Report(ScanProgress p){
   if(IsDisposed||throttle.ElapsedMilliseconds<120&&p.Stage!="Hoàn tất quét")return;
   throttle.Restart();
   try{BeginInvoke((Action)(()=>{
    if(IsDisposed)return;
    stage.Text=p.Stage+(String.IsNullOrEmpty(p.Current)?"":"   "+Presentation.ShortPath(p.Current,70));
    counters.Text=(visited+p.Visited).ToString("N0")+" thư mục đã duyệt  •  "+(list.Items.Count+p.Found)+" mục tìm thấy";
   }));}catch(InvalidOperationException){}
  }

  /// <summary>Adds deduplicated candidates to the list, keeping the rows ordered by kind then path.</summary>
  void AddCandidates(IEnumerable<Candidate> items){
   list.BeginUpdate();
   foreach(var c in items.GroupBy(Advanced.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.First())){
    if(rows.Keys.Any(k=>String.Equals(Advanced.Id(k),Advanced.Id(c),StringComparison.OrdinalIgnoreCase)))continue;
    var item=new ListViewItem(new[]{KindOf(c),PathOf(c),c.Kind=="Folder"||c.Kind=="File"?"…":"—",c.ReviewOnly?"Chỉ xem":"Chờ duyệt",c.Reason}){Tag=c,ToolTipText=PathOf(c)+"\r\n"+c.Reason};
    if(c.ReviewOnly)item.ForeColor=Theme.Muted;
    Theme.StripeRow(item,list.Items.Count);
    list.Items.Add(item);rows[c]=item;
   }
   list.EndUpdate();
   UpdateSummary();
  }

  /// <summary>Measures every file and folder candidate on a worker thread and fills the size column as results arrive.</summary>
  async Task MeasureAll(CancellationToken token){
   var pending=rows.Keys.ToList();
   await Task.Run(()=>{
    foreach(var c in pending){
     token.ThrowIfCancellationRequested();
     var fp=Advanced.Measure(c,token);var candidate=c;
     BeginInvoke((Action)(()=>{
      if(IsDisposed)return;
      footprints[candidate]=fp;ListViewItem item;
      if(rows.TryGetValue(candidate,out item)){
       item.SubItems[2].Text=fp.Bytes>=0?Presentation.BytesLabel(fp.Bytes)+(fp.Partial?"+":""):(fp.Detail==""?"—":fp.Detail);
       if(fp.Detail!="")item.ToolTipText=PathOf(candidate)+"\r\n"+fp.Detail+"\r\n"+candidate.Reason;
      }
      UpdateSummary();
     }));
    }
   },token);
  }

  /// <summary>Checks or unchecks every deletable row.</summary>
  void SetChecks(bool value){
   list.BeginUpdate();
   foreach(ListViewItem item in list.Items)item.Checked=value&&!((Candidate)item.Tag).ReviewOnly&&!IsDone(item);
   list.EndUpdate();
   UpdateSummary();
  }

  /// <summary>Backs up and removes the checked items one by one, then reports the outcome in the window.</summary>
  async Task DeleteChecked(){
   var selected=list.Items.Cast<ListViewItem>().Where(i=>i.Checked&&!((Candidate)i.Tag).ReviewOnly&&!IsDone(i)).ToList();
   if(selected.Count==0){MessageBox.Show(this,"Hãy đánh dấu những mục đã kiểm tra và muốn xóa.","AppCare",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   long bytes=selected.Sum(i=>{Footprint fp;return footprints.TryGetValue((Candidate)i.Tag,out fp)&&fp.Bytes>0?fp.Bytes:0;});
   string preview=String.Join("\r\n",selected.Take(8).Select(i=>PathOf((Candidate)i.Tag)));
   if(selected.Count>8)preview+="\r\n… và "+(selected.Count-8)+" mục khác";
   var answer=MessageBox.Show(this,"Sao lưu và xóa "+selected.Count+" mục đã chọn"+(bytes>0?" (khoảng "+Presentation.BytesLabel(bytes)+")":"")+"?\r\n\r\n"+preview+"\r\n\r\nFile và thư mục được chuyển vào Kho khôi phục trên cùng ổ đĩa. Registry được lưu giá trị và khóa con trước khi xóa.","Xác nhận xóa",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
   if(answer!=DialogResult.Yes)return;
   deleting=true;SetBusy(true);banner.Visible=false;
   progress.Style=ProgressBarStyle.Continuous;progress.Maximum=selected.Count;progress.Value=0;
   int ok=0,failed=0;
   foreach(var item in selected){
    var c=(Candidate)item.Tag;
    stage.Text="Đang sao lưu và xóa "+(ok+failed+1)+"/"+selected.Count+"   "+Presentation.ShortPath(PathOf(c),70);
    try{
     await Task.Run(()=>Engine.Quarantine(c));
     ok++;item.Checked=false;item.SubItems[3].Text="Đã xóa (có sao lưu)";item.ForeColor=Theme.Muted;item.Font=new Font(Theme.Body,FontStyle.Strikeout);
     log("Đã sao lưu và xóa: "+PathOf(c));
    }catch(Exception e){
     failed++;item.Checked=false;item.SubItems[3].Text="Lỗi";item.ForeColor=Theme.Danger;item.ToolTipText=PathOf(c)+"\r\n"+e.Message;
     log("Giữ lại "+PathOf(c)+": "+e.Message);
    }
    progress.Value=ok+failed;
   }
   Deleted+=ok;Failed+=failed;deleting=false;
   stage.Text="Hoàn tất xóa.  Còn "+list.Items.Cast<ListViewItem>().Count(i=>!IsDone(i))+" mục chờ duyệt.";
   string outcome="Đã xóa "+ok+" mục và lưu bản khôi phục"+(failed>0?"; "+failed+" mục chưa xóa được — di chuột lên dòng màu đỏ để xem lý do.":".")+" Có thể khôi phục trong tab Kho khôi phục.";
   ShowBanner(outcome,failed>0?NoteKind.Warning:NoteKind.Success);
   SetBusy(false);
  }

  /// <summary>Opens the selected folder or shortcut in Explorer, or shows registry details in a dialog.</summary>
  void Inspect(){
   if(list.SelectedItems.Count==0)return;var c=(Candidate)list.SelectedItems[0].Tag;
   try{
    if(c.Kind=="Folder"&&Directory.Exists(c.Path))Process.Start("explorer.exe","\""+c.Path+"\"");
    else if(c.Kind=="File"&&File.Exists(c.Path))Process.Start("explorer.exe","/select,\""+c.Path+"\"");
    else MessageBox.Show(this,PathOf(c)+"\r\n\r\n"+c.Reason,"Chi tiết mục còn sót",MessageBoxButtons.OK,MessageBoxIcon.Information);
   }catch(Exception e){MessageBox.Show(this,e.Message,"AppCare",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }

  /// <summary>Shows the colored result strip above the list.</summary>
  void ShowBanner(string text,NoteKind kind){banner.Text=text;Theme.Tint(banner,kind);banner.Visible=true;}

  /// <summary>Enables or disables the action buttons while scanning or deleting is in progress.</summary>
  void SetBusy(bool busy){
   bool hasDeletable=list.Items.Cast<ListViewItem>().Any(i=>!((Candidate)i.Tag).ReviewOnly&&!IsDone(i));
   stop.Visible=scanning;stop.Enabled=scanning;
   selectAll.Enabled=!busy&&hasDeletable;selectNone.Enabled=!busy&&hasDeletable;
   delete.Enabled=!busy&&hasDeletable&&!locked;
   close.Enabled=!busy||scanning;
   list.Enabled=!deleting;
  }

  /// <summary>Refreshes the footer summary with counts and the total measured size of checked rows.</summary>
  void UpdateSummary(){
   int total=list.Items.Count;int checkedCount=list.CheckedItems.Count;
   long bytes=list.CheckedItems.Cast<ListViewItem>().Sum(i=>{Footprint fp;return footprints.TryGetValue((Candidate)i.Tag,out fp)&&fp.Bytes>0?fp.Bytes:0;});
   long all=footprints.Values.Where(f=>f.Bytes>0).Sum(f=>f.Bytes);
   summary.Text="Tìm thấy "+total+" mục"+(all>0?" (~"+Presentation.BytesLabel(all)+")":"")+"  •  Đã chọn "+checkedCount+(bytes>0?" (~"+Presentation.BytesLabel(bytes)+")":"")+(Deleted+Failed>0?"  •  Đã xóa "+Deleted+", lỗi "+Failed:"");
   if(!scanning&&!deleting)SetBusy(false);
  }

  static bool IsDone(ListViewItem item){return item.SubItems[3].Text.StartsWith("Đã xóa");}
  static string KindOf(Candidate c){return c.ReviewOnly?"Chỉ xem":c.Kind=="Folder"?"Thư mục":c.Kind=="File"?"Shortcut":c.Kind=="RegistryValue"?"Giá trị Registry":"Khóa Registry";}
  static string PathOf(Candidate c){return (c.Hive==null?"":c.Hive+" ["+c.View+"]\\")+c.Path+(c.ValueName==null?"":" :: "+c.ValueName);}

  /// <summary>Fills the window with illustrative rows so the layout can be rendered without a real scan.</summary>
  public void PopulateForPreview(){
   previewMode=true;
   AddCandidates(new[]{
    new Candidate{Kind="Folder",AppName="Ứng dụng mẫu",Path=@"C:\Program Files\Example App",Reason="Thư mục InstallLocation do ứng dụng khai báo; cần duyệt nội dung."},
    new Candidate{Kind="Folder",AppName="Ứng dụng mẫu",Path=@"C:\Users\Người dùng\AppData\Local\Example App",Reason="Quét sâu: tên thư mục trùng chính xác tên ứng dụng."},
    new Candidate{Kind="File",AppName="Ứng dụng mẫu",Path=@"C:\Users\Người dùng\Desktop\Example App.lnk",Reason="Shortcut trỏ tới executable của ứng dụng."},
    new Candidate{Kind="Registry",AppName="Ứng dụng mẫu",Path=@"SOFTWARE\Example App",Hive="HKCU",View="64",Reason="Khóa trùng tên ứng dụng; cần kiểm tra quyền sở hữu."},
    new Candidate{Kind="Review",AppName="Ứng dụng mẫu",Path="Service: ExampleUpdater",ReviewOnly=true,Reason="Dịch vụ tham chiếu đường dẫn ứng dụng; kiểm tra trong Services."}
   });
   progress.Style=ProgressBarStyle.Continuous;progress.Value=100;stage.Text="Hoàn tất quét.  Tìm thấy 5 mục.";counters.Text="17.060 thư mục đã duyệt  •  17 ghi chú (xem Nhật ký)";
   scanning=false;SetBusy(false);SetChecks(true);
  }
 }
}
