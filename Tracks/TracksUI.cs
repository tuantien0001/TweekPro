using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Tracks;

namespace TweekPro {
 public partial class MainForm {
  ListView tracksList=new SmoothListView();Label tracksOverlay,tracksSummary,tracksStage;TabPage tracksTab;CheckBox tracksExit;
  List<TrackResult> tracksResults=new List<TrackResult>();CancellationTokenSource tracksCancellation;bool tracksExitDone,tracksMeasured,tracksRendering;

  /// <summary>Builds the Privacy Tracks tab: one row per trace rule with count, size and lock state; cleaning always goes to the vault.</summary>
  void BuildTracksTab(){
   var tab=tracksTab=new TabPage(Core.L.T("Dấu vết"));tabs.TabPages.Add(tab);
   SetupList(tracksList,new[]{"Dấu vết","Số mục","Dung lượng","Trạng thái","Vị trí","Mô tả"},new[]{300,80,100,220,300,380},true,true);
   tracksList.ItemCheck+=(s,e)=>{var r=(TrackResult)tracksList.Items[e.Index].Tag;if(r.Locked||r.Count==0)e.NewValue=CheckState.Unchecked;};
   tracksList.ItemChecked+=(s,e)=>{UpdateTracksSummary();RememberTracksExitRules();};
   tracksList.DoubleClick+=(s,e)=>ShowTrackDetails();
   var host=Theme.ListHost(tracksList,out tracksOverlay);
   var bar=Bar();
   Add(bar,"Xem trước",async()=>await PreviewTracks(),ButtonStyle.Primary);
   Add(bar,"Chọn mặc định",()=>{SetTracksChecks(true);return Task.FromResult(0);});
   Add(bar,"Bỏ chọn",()=>{SetTracksChecks(false);return Task.FromResult(0);});
   Add(bar,"Dọn dấu vết đã chọn (vào kho)",async()=>await CleanTracks(),ButtonStyle.Danger);
   Add(bar,"Cookie giữ lại…",()=>{EditCookieKeep();return Task.FromResult(0);});
   Add(bar,"Xem mục",()=>{ShowTrackDetails();return Task.FromResult(0);});
   tracksExit=new CheckBox{Text=Core.L.T("Dọn khi thoát Tweek Pro"),AutoSize=true,Margin=new Padding(12,8,16,0),ForeColor=Theme.Text,Checked=settings.TracksCleanOnExit};
   tracksExit.CheckedChanged+=(s,e)=>{settings.TracksCleanOnExit=tracksExit.Checked;RememberTracksExitRules();SaveSettings();if(tracksExit.Checked)Log(Core.L.T("Dấu vết: các loại đang đánh dấu sẽ được dọn (vào Kho) mỗi khi đóng Tweek Pro; trình duyệt đang chạy sẽ được bỏ qua."));};
   bar.Controls.Add(tracksExit);
   TracksCleaner.CookieKeep=TracksCleaner.ParseKeep(settings.TracksCookieKeep);
   var note=Theme.Note("Dấu vết là dữ liệu Windows và trình duyệt ghi lại thói quen dùng máy: tệp mở gần đây, Jump List, lịch sử Run, hộp Mở/Lưu, từ khóa tìm, Office/Acrobat gần đây, Remote Desktop, PowerShell, lịch sử duyệt web. Chỉ những khóa HKCU và thư mục trong danh sách cho phép mới được dọn; không đụng mật khẩu, dấu trang, tiện ích. Mọi thứ đi vào Kho khôi phục. Cookie mặc định không chọn; «Cookie giữ lại» giữ đăng nhập của các trang bạn nêu tên. «Dọn khi thoát» dọn lại các loại đang đánh dấu mỗi lần đóng Tweek Pro.",NoteKind.Warning);
   tracksStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   tracksSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(tracksSummary);
   tab.Controls.Add(host);tab.Controls.Add(tracksSummary);tab.Controls.Add(tracksStage);tab.Controls.Add(note);tab.Controls.Add(bar);
   Theme.SetOverlay(tracksOverlay,"Chưa xem trước.\r\nBấm Xem trước để đếm tệp và giá trị Registry của từng loại dấu vết. Chưa có gì bị thay đổi cho đến khi bạn bấm Dọn và xác nhận.",NoteKind.Info);
   tracksResults=TracksCleaner.Catalog().Select(r=>new TrackResult{Rule=r}).ToList();
   RenderTracks(false);
  }

  void RenderTracks(bool measured){
   var remembered=TracksRemembered;tracksMeasured=measured;tracksRendering=true;
   tracksList.BeginUpdate();tracksList.Items.Clear();tracksList.Groups.Clear();
   foreach(var r in tracksResults){
    string state=Core.L.T(r.Locked?"Bị khóa":!measured?"Chưa xem trước":r.Count==0?"Không có gì để dọn":"Có thể dọn")+(r.Rule.Sensitive?Core.L.T("  •  nhạy cảm"):"")+(r.Sql?Core.L.T("  •  xóa theo dòng (SQL)"):"");
    var row=new ListViewItem(new[]{Core.L.T(r.Rule.Name),measured?r.Count.ToString("N0"):"…",r.Rule.Source==TrackSource.Registry?"—":measured?Presentation.BytesLabel(r.Bytes):"…",state,r.Roots.Count>0?String.Join(" | ",r.Roots.Select(p=>Presentation.ShortPath(p,60))):String.Join(" | ",r.Rule.Paths),Core.L.T(r.Rule.Description)}){Tag=r,ToolTipText=Core.L.T(r.Rule.Description)+"\r\n"+String.Join("\r\n",r.Rule.Paths)+(r.LockReason==""?"":"\r\n\r\n"+Core.L.T(r.LockReason))+(r.Note==""?"":"\r\n"+Core.L.T(r.Note))};
    if(r.Locked)row.ForeColor=Theme.Danger;else if(r.Rule.Sensitive)row.ForeColor=Theme.Warning;else if(measured&&r.Count==0)row.ForeColor=Theme.Muted;
    Theme.AssignGroup(tracksList,row,(Array.IndexOf(new[]{"Windows","Explorer","Ứng dụng","Trình duyệt"},r.Rule.Group)+1)+"-"+r.Rule.Group,Core.L.T(r.Rule.Group));
    Theme.StripeRow(row,tracksList.Items.Count);tracksList.Items.Add(row);
    row.Checked=measured&&!r.Locked&&r.Count>0&&(remembered!=null?remembered.Contains(r.Rule.Id):r.Rule.DefaultChecked);
   }
   tracksList.EndUpdate();tracksRendering=false;UpdateTracksSummary();RememberTracksExitRules();
  }

  /// <summary>Rule ids remembered for clean-on-exit, or null when the feature is off (default checks apply then).</summary>
  HashSet<string> TracksRemembered { get { return settings.TracksCleanOnExit&&!String.IsNullOrWhiteSpace(settings.TracksExitRules)?new HashSet<string>(settings.TracksExitRules.Split(','),StringComparer.OrdinalIgnoreCase):null; } }

  void SetTracksChecks(bool value){tracksRendering=true;tracksList.BeginUpdate();foreach(ListViewItem item in tracksList.Items){var r=(TrackResult)item.Tag;item.Checked=value&&!r.Locked&&r.Count>0&&r.Rule.DefaultChecked;}tracksList.EndUpdate();tracksRendering=false;UpdateTracksSummary();RememberTracksExitRules();}

  /// <summary>While clean-on-exit is on and a preview has run, the checked rule ids are persisted so the exit run uses the user's latest selection.</summary>
  void RememberTracksExitRules(){
   if(tracksRendering||!tracksMeasured||tracksExit==null||!tracksExit.Checked||tracksList.Items.Count==0)return;
   string ids=String.Join(",",tracksList.CheckedItems.Cast<ListViewItem>().Select(i=>((TrackResult)i.Tag).Rule.Id));
   if(ids!=settings.TracksExitRules){settings.TracksExitRules=ids;SaveSettings();}
  }

  /// <summary>Runs the remembered rules once while the window closes; skipped in preview mode, when nothing is remembered, or when already done.</summary>
  void RunTracksOnExit(){
   if(tracksExitDone||!settings.TracksCleanOnExit||String.IsNullOrWhiteSpace(settings.TracksExitRules))return;
   tracksExitDone=true;
   try{TracksCleaner.CookieKeep=TracksCleaner.ParseKeep(settings.TracksCookieKeep);string line=TracksCleaner.CleanOnExit(settings.TracksExitRules.Split(','));Core.Log.Info(line);}
   catch(Exception e){Core.Log.Warn("Dọn khi thoát lỗi: "+e.Message);}
  }

  /// <summary>Edits the cookie whitelist (hosts kept when the cookie rules run) and re-previews so the note reflects it.</summary>
  void EditCookieKeep(){
   using(var form=new CookieKeepForm(settings.TracksCookieKeep)){
    if(form.ShowDialog(this)!=DialogResult.OK)return;
    settings.TracksCookieKeep=form.Text2;TracksCleaner.CookieKeep=TracksCleaner.ParseKeep(form.Text2);SaveSettings();
    Log(Core.L.F("Dấu vết: giữ cookie của {0} trang: {1}",TracksCleaner.CookieKeep.Count,String.Join(", ",TracksCleaner.CookieKeep.Take(10))));
    foreach(var r in tracksResults)if(r.Rule.SqlTable!=null){r.Sql=r.Rule.SqlCapable&&Core.WinSqlite.Available&&TracksCleaner.CookieKeep.Count>0;r.Note=r.Sql?"Giữ cookie của: "+String.Join(", ",TracksCleaner.CookieKeep.Take(6)):"";}
    RenderTracks(tracksResults.Any(r=>r.Roots.Count>0));
   }
  }
  List<TrackResult> CheckedTracks(){return tracksList.CheckedItems.Cast<ListViewItem>().Select(i=>(TrackResult)i.Tag).Where(r=>!r.Locked&&r.Count>0).ToList();}

  void UpdateTracksSummary(){
   var selected=CheckedTracks();int items=selected.Sum(r=>r.Count);long bytes=selected.Sum(r=>r.Bytes);
   tracksSummary.Text=Core.L.F("{0} loại dấu vết  •  Đã chọn {1} loại, {2} mục, {3}  •  Chuyển vào Kho: khôi phục được, không ghi đè mục mới hơn",tracksResults.Count,selected.Count,items.ToString("N0"),Presentation.BytesLabel(bytes));
  }

  /// <summary>Counts every rule on a worker thread; nothing is changed.</summary>
  async Task PreviewTracks(){
   tracksCancellation=new CancellationTokenSource();var token=tracksCancellation.Token;
   tracksStage.Visible=true;tracksStage.Text=Core.L.T("Đang xem trước…");Theme.SetOverlay(tracksOverlay,null,NoteKind.Info);
   Log(Core.L.F("Dấu vết: đang xem trước {0} loại (chỉ đọc).",tracksResults.Count));
   try{
    var rules=TracksCleaner.Catalog();
    tracksResults=await Task.Run(()=>TracksCleaner.Preview(rules,token),token);
    RenderTracks(true);
    int items=tracksResults.Sum(r=>r.Count);long bytes=tracksResults.Sum(r=>r.Bytes);int locked=tracksResults.Count(r=>r.Locked);
    tracksStage.Text=Core.L.F("Xem trước xong: {0} mục, {1} tệp",items.ToString("N0"),Presentation.BytesLabel(bytes))+(locked>0?Core.L.F("  •  {0} loại bị khóa (đóng trình duyệt rồi xem trước lại)",locked):"");
    Log(Core.L.F("Dấu vết: {0} mục, {1}, {2} loại bị khóa.",items.ToString("N0"),Presentation.BytesLabel(bytes),locked));
    if(items==0)Theme.SetOverlay(tracksOverlay,"Không tìm thấy dấu vết nào trong các vị trí cho phép.",NoteKind.Info);
   }catch(OperationCanceledException){tracksStage.Text=Core.L.T("Đã dừng xem trước.");}
   finally{tracksCancellation.Dispose();tracksCancellation=null;}
  }

  /// <summary>Confirms, then moves files / saves and empties registry keys for the checked rules.</summary>
  async Task CleanTracks(){
   var selected=CheckedTracks();if(selected.Count==0)throw new IOException(Core.L.T("Xem trước rồi đánh dấu loại dấu vết muốn dọn."));
   int items=selected.Sum(r=>r.Count);
   string list=String.Join("\r\n",selected.Select(r=>"• "+Core.L.T(r.Rule.Name)+" — "+Core.L.F("{0} mục",r.Count.ToString("N0"))));
   if(selected.Any(r=>r.Rule.Sensitive)&&!Confirm(Core.L.T("Bạn đã chọn cookie đăng nhập: mọi trang web sẽ yêu cầu đăng nhập lại. Tiếp tục?")))return;
   if(!Confirm(Core.L.F("Dọn {0} mục dấu vết vào Kho khôi phục?\r\n\r\n{1}\r\n\r\nMỗi loại tạo một bản sao lưu; giá trị Registry được ghi lại khi khôi phục nếu chưa có mục mới hơn.",items.ToString("N0"),list)))return;
   tracksCancellation=new CancellationTokenSource();var token=tracksCancellation.Token;tracksStage.Visible=true;tracksStage.Text=Core.L.T("Đang chuyển vào kho…");
   TrackReport report;
   try{report=await Task.Run(()=>TracksCleaner.Clean(selected,token),token);}
   catch(OperationCanceledException){tracksStage.Text=Core.L.T("Đã dừng dọn.");return;}
   finally{tracksCancellation.Dispose();tracksCancellation=null;}
   LoadBackups();
   string summary=Core.L.F("Đã dọn {0} mục ({1}). Bỏ qua vì đang dùng: {2}. Lỗi: {3}. Bản sao lưu: {4}.",report.Cleaned.ToString("N0"),Presentation.BytesLabel(report.Bytes),report.SkippedInUse.ToString("N0"),report.Failed.ToString("N0"),report.Backups.Count);
   tracksStage.Text=summary;Log(Core.L.T("Dấu vết: ")+summary);
   foreach(string err in report.Errors.Take(20))Log(Core.L.T("Dọn dấu vết lỗi: ")+err);
   MessageBox.Show(this,summary+(report.Errors.Count>0?Core.L.T("\r\n\r\nLỗi đầu tiên:\r\n")+String.Join("\r\n",report.Errors.Take(5)):"")+Core.L.T("\r\n\r\nCó thể khôi phục hoặc xóa vĩnh viễn trong tab Kho khôi phục."),Core.L.T("Kết quả dọn dấu vết"),MessageBoxButtons.OK,report.Failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
   await PreviewTracks();
  }

  void ShowTrackDetails(){
   if(tracksList.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một loại dấu vết để xem mục."));
   var r=(TrackResult)tracksList.SelectedItems[0].Tag;
   using(var form=new TrackDetailForm(r))form.ShowDialog(this);
  }

  /// <summary>Fills the tab with illustrative counts for --preview tracks; nothing is read from the machine.</summary>
  public void PreviewTracks(bool show){
   var rules=TracksCleaner.Catalog();var rnd=new[]{48,12,3,9,14,120,37,6,210,5,1840,0,7};int i=0;
   tracksResults=rules.Select(r=>{var res=new TrackResult{Rule=r};int n=rnd[i++%rnd.Length];for(int k=0;k<n;k++)res.Items.Add(new TrackItem{Path=r.Source==TrackSource.Files?@"C:\Users\ADMIN\AppData\Roaming\Microsoft\Windows\Recent\item"+k+".lnk":"HKCU\\"+r.Paths[0]+" :: "+(char)('a'+k%26),Bytes=r.Source==TrackSource.Files?2048+k*311:0});res.Bytes=res.Items.Sum(x=>x.Bytes);res.Roots.Add(r.Source==TrackSource.Files?Environment.ExpandEnvironmentVariables(r.Paths[0]):"HKCU\\"+r.Paths[0]);return res;}).ToList();
   var cookies=tracksResults.First(r=>r.Rule.Id=="chromium-history");cookies.Locked=true;cookies.LockReason="Đang chạy: chrome — đóng trình duyệt rồi xem trước lại.";
   RenderTracks(true);Theme.SetOverlay(tracksOverlay,null,NoteKind.Info);if(show)tabs.SelectedTab=tracksTab;
  }
 }

 /// <summary>Multi-line editor for the cookie whitelist; Text2 returns the raw text (parsed by TracksCleaner.ParseKeep).</summary>
 public class CookieKeepForm:Form {
  TextBox box;
  public string Text2 { get { return box.Text; } }
  public CookieKeepForm(string current){
   Text=Core.L.T("Cookie giữ lại");Size=new Size(640,520);MinimumSize=new Size(520,400);StartPosition=FormStartPosition.CenterParent;ShowIcon=false;
   Font=Theme.Body;BackColor=Theme.Canvas;ForeColor=Theme.Text;AutoScaleMode=AutoScaleMode.Dpi;
   var header=Theme.HeaderBand(Core.L.T("Cookie giữ lại"),Core.L.T("Mỗi dòng một tên miền (ví dụ google.com giữ cả mail.google.com). Khi dọn cookie, chỉ các trang này còn đăng nhập; cookie khác bị xóa theo dòng bằng SQL và bản sao đầy đủ nằm trong Kho."),96);
   box=new TextBox{Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Font=Theme.Mono,AcceptsReturn=true,Text=(current??"").Replace("\n","\r\n").Replace("\r\r","\r"),BackColor=Theme.Surface,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle};
   var host=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16,12,16,12),BackColor=Theme.Canvas};host.Controls.Add(box);
   var hint=Theme.Note(Core.WinSqlite.Available?"Danh sách áp dụng cho Chrome / Edge / Brave và Firefox. Để trống = xóa toàn bộ cookie (chuyển cả tệp vào Kho).":"Máy này không có winsqlite3.dll (Windows 10+), nên danh sách chưa dùng được: cookie sẽ bị dọn toàn bộ.",Core.WinSqlite.Available?NoteKind.Info:NoteKind.Warning);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(16,0,16,12),AutoSize=true,BackColor=Theme.Canvas};
   var cancel=Theme.Button("Hủy",ButtonStyle.Secondary);cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};
   var ok=Theme.Button("Lưu",ButtonStyle.Primary);ok.Click+=(s,e)=>{var parsed=TracksCleaner.ParseKeep(box.Text);box.Text=String.Join("\r\n",parsed);DialogResult=DialogResult.OK;Close();};
   buttons.Controls.Add(cancel);buttons.Controls.Add(ok);
   Controls.Add(host);Controls.Add(hint);Controls.Add(buttons);Controls.Add(header);AcceptButton=null;CancelButton=cancel;
  }
 }

 /// <summary>Read-only list of the files or registry values one track rule would remove.</summary>
 public class TrackDetailForm:Form {
  public TrackDetailForm(TrackResult result){
   Text=Core.L.T("Mục sẽ dọn — ")+Core.L.T(result.Rule.Name);Size=new Size(960,620);MinimumSize=new Size(720,420);StartPosition=FormStartPosition.CenterParent;ShowIcon=false;
   Font=Theme.Body;BackColor=Theme.Canvas;ForeColor=Theme.Text;AutoScaleMode=AutoScaleMode.Dpi;
   var header=Theme.HeaderBand(Core.L.T(result.Rule.Name),result.Count.ToString("N0")+Core.L.T(" mục")+(result.Rule.Source==TrackSource.Files?"  •  "+Presentation.BytesLabel(result.Bytes):"")+(result.Locked?"  •  "+Core.L.T(result.LockReason):""),84);
   var list=new SmoothListView();Theme.StyleList(list);list.Columns.Add(Core.L.T("Đường dẫn / giá trị"),560);list.Columns.Add(Core.L.T("Dung lượng"),100);list.Columns.Add(Core.L.T("Chi tiết"),260);
   list.BeginUpdate();int i=0;foreach(var item in result.Items.Take(5000)){var row=new ListViewItem(new[]{item.Path,item.Bytes>0?Presentation.BytesLabel(item.Bytes):"—",item.Detail??""}){ToolTipText=item.Path+(String.IsNullOrEmpty(item.Detail)?"":"\r\n"+item.Detail)};Theme.StripeRow(row,i++);list.Items.Add(row);}list.EndUpdate();
   var host=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16,12,16,12),BackColor=Theme.Canvas};list.Dock=DockStyle.Fill;host.Controls.Add(list);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(16,0,16,12),AutoSize=true,BackColor=Theme.Canvas};
   var close=Theme.Button("Đóng",ButtonStyle.Secondary);close.Click+=(s,e)=>Close();buttons.Controls.Add(close);
   Controls.Add(host);Controls.Add(buttons);Controls.Add(header);CancelButton=close;
  }
 }
}
