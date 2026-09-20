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
  ListView junkList=new SmoothListView();Label junkOverlay,junkSummary,junkStage;TabPage junkTab;
  RadioButton junkVaultMode=new RadioButton(),junkDirectMode=new RadioButton();
  Button junkCleanButton;List<JunkRuleResult> junkResults=new List<JunkRuleResult>();JunkRuleSet junkRules;
  CancellationTokenSource junkCancellation;

  /// <summary>Builds the Junk Cleaner tab: rule list with per-rule checkboxes, mode selector and preview/clean actions.</summary>
  void BuildJunkTab(){
   var tab=junkTab=new TabPage("Dọn rác");tabs.TabPages.Add(tab);
   SetupList(junkList,new[]{"Quy tắc","Tệp","Dung lượng","Trạng thái","Vị trí","Mô tả"},new[]{300,80,110,220,300,360},true,true);
   junkList.ItemCheck+=(s,e)=>{var r=(JunkRuleResult)junkList.Items[e.Index].Tag;if(r.Locked||r.Count==0)e.NewValue=CheckState.Unchecked;};
   junkList.ItemChecked+=(s,e)=>UpdateJunkSummary();
   junkList.DoubleClick+=(s,e)=>ShowJunkDetails();
   var host=Theme.ListHost(junkList,out junkOverlay);

   var bar=Bar();
   Add(bar,"Xem trước",async()=>await PreviewJunk(),ButtonStyle.Primary);
   Add(bar,"Chọn tất cả",()=>{SetJunkChecks(true);return Task.FromResult(0);});
   Add(bar,"Bỏ chọn",()=>{SetJunkChecks(false);return Task.FromResult(0);});
   Add(bar,"Dọn mục đã chọn",async()=>await CleanJunk(),ButtonStyle.Danger);
   junkCleanButton=actions[actions.Count-1];
   Add(bar,"Xem tệp",()=>{ShowJunkDetails();return Task.FromResult(0);});
   Add(bar,"Nạp lại quy tắc",()=>{LoadJunkRules(true);return Task.FromResult(0);});
   Add(bar,"Mở thư mục dữ liệu",()=>{Directory.CreateDirectory(Core.Paths.Root);System.Diagnostics.Process.Start("explorer.exe","\""+Core.Paths.Root+"\"");return Task.FromResult(0);});

   var modePanel=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(16,8,16,6),BackColor=Theme.Surface};
   Theme.BorderBottom(modePanel);
   var modeLabel=new Label{Text="Chế độ dọn:",AutoSize=true,Margin=new Padding(0,6,12,0),ForeColor=Theme.Muted,Font=Theme.Small};
   junkVaultMode.Text="Chuyển vào Kho khôi phục (có thể hoàn tác)";junkVaultMode.Checked=true;junkVaultMode.AutoSize=true;junkVaultMode.Margin=new Padding(0,4,24,0);junkVaultMode.ForeColor=Theme.Text;junkVaultMode.Font=Theme.Body;
   junkDirectMode.Text="Xóa thẳng — KHÔNG thể khôi phục";junkDirectMode.AutoSize=true;junkDirectMode.Margin=new Padding(0,4,0,0);junkDirectMode.ForeColor=Theme.Danger;junkDirectMode.Font=Theme.Strong;
   junkVaultMode.CheckedChanged+=(s,e)=>UpdateJunkSummary();junkDirectMode.CheckedChanged+=(s,e)=>UpdateJunkSummary();
   modePanel.Controls.Add(modeLabel);modePanel.Controls.Add(junkVaultMode);modePanel.Controls.Add(junkDirectMode);

   var note=Theme.Note("Chỉ dọn tệp tạm, dump, báo cáo lỗi và bộ đệm theo quy tắc; không bao giờ chạm vào Documents, Desktop, Downloads hay dữ liệu ứng dụng. Bỏ qua tệp mới và tệp đang mở. Nhóm cache trình duyệt bị khóa khi trình duyệt còn chạy.",NoteKind.Warning);
   junkStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   junkSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(junkSummary);
   tab.Controls.Add(host);tab.Controls.Add(junkSummary);tab.Controls.Add(junkStage);tab.Controls.Add(note);tab.Controls.Add(modePanel);tab.Controls.Add(bar);
   Theme.SetOverlay(junkOverlay,"Chưa xem trước.\r\nBấm Xem trước để liệt kê tệp rác theo từng quy tắc cùng dung lượng. Chưa có gì bị thay đổi cho đến khi bạn bấm Dọn mục đã chọn và xác nhận.",NoteKind.Info);
   LoadJunkRules(false);
   UpdateJunkSummary();
  }

  /// <summary>Loads rules from the override file or the embedded defaults and shows them as unmeasured rows.</summary>
  void LoadJunkRules(bool announce){
   try{junkRules=JunkRules.Load(Core.Paths.JunkRulesOverride);}
   catch(Exception e){junkRules=new JunkRuleSet{Rules=new List<JunkRule>()};Log("Không đọc được quy tắc dọn rác: "+e.Message);}
   junkResults=junkRules.Rules.Select(r=>new JunkRuleResult{Rule=r}).ToList();
   RenderJunk(false);
   if(announce)Log("Đã nạp "+junkRules.Rules.Count+" quy tắc dọn rác từ "+junkRules.Source+".");
  }

  /// <summary>Redraws the rule list; measured is false before the first preview so sizes show as pending.</summary>
  void RenderJunk(bool measured){
   junkList.BeginUpdate();junkList.Items.Clear();junkList.Groups.Clear();
   foreach(var r in junkResults){
    string state=r.Locked?"Bị khóa":!measured?"Chưa xem trước":r.Count==0?"Không có gì để dọn":r.Partial?"Có thể dọn (chưa đủ)":"Có thể dọn";
    var row=new ListViewItem(new[]{r.Rule.Name,measured?r.Count.ToString("N0"):"…",measured?Presentation.BytesLabel(r.Bytes):"…",state+(r.Rule.RequiresAdmin?"  •  cần quản trị":""),r.Roots.Count>0?String.Join(" | ",r.Roots.Select(p=>Presentation.ShortPath(p,60))):String.Join(" | ",r.Rule.Paths),r.Rule.Description}){Tag=r,ToolTipText=r.Rule.Description+"\r\n"+String.Join("\r\n",r.Rule.Paths)+(r.LockReason==""?"":"\r\n\r\n"+r.LockReason)+(r.Note==""?"":"\r\n"+r.Note)};
    if(r.Locked)row.ForeColor=Theme.Danger;else if(measured&&r.Count==0)row.ForeColor=Theme.Muted;
    Theme.AssignGroup(junkList,row,r.Rule.Group,r.Rule.Group);
    Theme.StripeRow(row,junkList.Items.Count);junkList.Items.Add(row);
    row.Checked=measured&&!r.Locked&&r.Count>0&&r.Rule.DefaultChecked;
   }
   junkList.EndUpdate();UpdateJunkSummary();
  }

  void SetJunkChecks(bool value){junkList.BeginUpdate();foreach(ListViewItem item in junkList.Items){var r=(JunkRuleResult)item.Tag;item.Checked=value&&!r.Locked&&r.Count>0;}junkList.EndUpdate();UpdateJunkSummary();}

  List<JunkRuleResult> CheckedJunk(){return junkList.CheckedItems.Cast<ListViewItem>().Select(i=>(JunkRuleResult)i.Tag).Where(r=>!r.Locked&&r.Count>0).ToList();}

  void UpdateJunkSummary(){
   var selected=CheckedJunk();long bytes=selected.Sum(r=>r.Bytes);int files=selected.Sum(r=>r.Count);
   bool direct=junkDirectMode.Checked;
   junkSummary.Text=(junkResults.Count+" quy tắc  •  Đã chọn "+selected.Count+" nhóm, "+files.ToString("N0")+" tệp, "+Presentation.BytesLabel(bytes))+(direct?"  •  XÓA THẲNG: không thể khôi phục":"  •  Chuyển vào Kho: chưa giải phóng dung lượng cho đến khi xóa vĩnh viễn trong kho");
   junkSummary.ForeColor=direct?Theme.Danger:Theme.Muted;
   if(junkCleanButton!=null)junkCleanButton.Text=direct?"Xóa thẳng mục đã chọn":"Dọn mục đã chọn (vào kho)";
  }

  /// <summary>Runs the read-only preview on a worker thread and fills sizes per rule.</summary>
  async Task PreviewJunk(){
   if(junkRules==null||junkRules.Rules.Count==0)throw new IOException("Không có quy tắc dọn rác.");
   junkCancellation=new CancellationTokenSource();var token=junkCancellation.Token;
   junkStage.Visible=true;junkStage.Text="Đang xem trước…";Theme.SetOverlay(junkOverlay,null,NoteKind.Info);
   Log("Dọn rác: đang xem trước "+junkRules.Rules.Count+" quy tắc (chỉ đọc).");
   try{
    var rules=junkRules.Rules.ToList();bool elevated=Core.Elevation.IsElevated;int minAge=settings.JunkMinAgeHours;
    junkResults=await Task.Run(()=>JunkCleaner.Preview(rules,elevated,minAge,token,ReportJunk),token);
    RenderJunk(true);
    long total=junkResults.Sum(r=>r.Bytes);int files=junkResults.Sum(r=>r.Count);int locked=junkResults.Count(r=>r.Locked);
    junkStage.Text="Xem trước xong: "+files.ToString("N0")+" tệp, "+Presentation.BytesLabel(total)+(locked>0?"  •  "+locked+" nhóm bị khóa (di chuột lên dòng đỏ để xem lý do)":"");
    Log("Dọn rác: xem trước xong — "+files.ToString("N0")+" tệp, "+Presentation.BytesLabel(total)+", "+locked+" nhóm bị khóa.");
    if(files==0)Theme.SetOverlay(junkOverlay,"Không có tệp rác đủ điều kiện theo quy tắc hiện tại.\r\nTệp mới hơn "+settings.JunkMinAgeHours+" giờ và tệp đang mở không được tính.",NoteKind.Info);
   }catch(OperationCanceledException){junkStage.Text="Đã dừng xem trước.";}
   finally{junkCancellation.Dispose();junkCancellation=null;}
  }

  void ReportJunk(JunkProgress p){
   if(IsDisposed||!IsHandleCreated)return;
   try{BeginInvoke((Action)(()=>{if(!IsDisposed)junkStage.Text=p.Stage+(String.IsNullOrEmpty(p.Current)?"":"   "+Presentation.ShortPath(p.Current,70))+(p.Files>0?"   •   "+p.Files.ToString("N0")+" tệp, "+Presentation.BytesLabel(p.Bytes):"");}));}catch(InvalidOperationException){}
  }

  /// <summary>Confirms and cleans the checked rules in the chosen mode, then reports counts, bytes and skipped files.</summary>
  async Task CleanJunk(){
   var selected=CheckedJunk();if(selected.Count==0)throw new IOException("Xem trước rồi đánh dấu nhóm muốn dọn.");
   bool direct=junkDirectMode.Checked;long bytes=selected.Sum(r=>r.Bytes);int files=selected.Sum(r=>r.Count);
   string list=String.Join("\r\n",selected.Select(r=>"• "+r.Rule.Name+" — "+r.Count.ToString("N0")+" tệp, "+Presentation.BytesLabel(r.Bytes)));
   if(direct){
    var answer=MessageBox.Show(this,"XÓA THẲNG "+files.ToString("N0")+" tệp ("+Presentation.BytesLabel(bytes)+") mà KHÔNG lưu bản khôi phục?\r\n\r\n"+list+"\r\n\r\nThao tác này không thể hoàn tác. Chỉ tiếp tục khi chắc chắn các nhóm trên chỉ chứa bộ đệm/tệp tạm.","Xác nhận xóa vĩnh viễn",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
    if(answer!=DialogResult.Yes)return;
    if(!Confirm("Xác nhận lần cuối: xóa thẳng "+files.ToString("N0")+" tệp, không có bản sao lưu?"))return;
   }else{
    if(!Confirm("Chuyển "+files.ToString("N0")+" tệp ("+Presentation.BytesLabel(bytes)+") vào Kho khôi phục?\r\n\r\n"+list+"\r\n\r\nMỗi nhóm tạo một bản sao lưu; có thể khôi phục hoặc xóa vĩnh viễn sau trong tab Kho khôi phục. Dung lượng ổ đĩa chưa được giải phóng cho đến khi xóa vĩnh viễn."))return;
   }
   junkCancellation=new CancellationTokenSource();var token=junkCancellation.Token;junkStage.Visible=true;junkStage.Text=direct?"Đang xóa thẳng…":"Đang chuyển vào kho…";
   Log("Dọn rác: bắt đầu "+(direct?"xóa thẳng ":"chuyển vào kho ")+files.ToString("N0")+" tệp trong "+selected.Count+" nhóm.");
   JunkReport report;
   try{report=await Task.Run(()=>JunkCleaner.Clean(selected,direct,token,ReportJunk),token);}
   catch(OperationCanceledException){junkStage.Text="Đã dừng dọn.";return;}
   finally{junkCancellation.Dispose();junkCancellation=null;}
   LoadBackups();
   string summary=(direct?"Đã xóa thẳng ":"Đã chuyển vào kho ")+report.Cleaned.ToString("N0")+" tệp ("+Presentation.BytesLabel(report.Bytes)+"). Bỏ qua vì đang dùng: "+report.SkippedInUse.ToString("N0")+". Lỗi: "+report.Failed.ToString("N0")+"."+(direct?"":" Bản sao lưu: "+report.Backups.Count+".");
   junkStage.Text=summary;Log("Dọn rác: "+summary);
   foreach(string err in report.Errors.Take(20))Log("Dọn rác lỗi: "+err);
   MessageBox.Show(this,summary+(report.Errors.Count>0?"\r\n\r\nLỗi đầu tiên:\r\n"+String.Join("\r\n",report.Errors.Take(5)):"")+(direct?"":"\r\n\r\nCó thể khôi phục hoặc xóa vĩnh viễn trong tab Kho khôi phục."),"Kết quả dọn rác",MessageBoxButtons.OK,report.Failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
   await PreviewJunk();
  }

  /// <summary>Opens a window listing the files that a rule would clean.</summary>
  void ShowJunkDetails(){
   if(junkList.SelectedItems.Count==0)throw new IOException("Chọn một quy tắc để xem tệp.");
   var r=(JunkRuleResult)junkList.SelectedItems[0].Tag;
   using(var form=new JunkDetailForm(r))form.ShowDialog(this);
  }
 }

 /// <summary>Read-only list of files matched by one junk rule so the user can inspect before cleaning.</summary>
 public class JunkDetailForm:Form {
  public JunkDetailForm(JunkRuleResult result){
   Text="Tệp sẽ dọn — "+result.Rule.Name;Size=new Size(960,620);MinimumSize=new Size(720,420);StartPosition=FormStartPosition.CenterParent;ShowIcon=false;
   Font=Theme.Body;BackColor=Theme.Canvas;ForeColor=Theme.Text;AutoScaleMode=AutoScaleMode.Dpi;
   var header=Theme.HeaderBand(result.Rule.Name,result.Count.ToString("N0")+" tệp  •  "+Presentation.BytesLabel(result.Bytes)+(result.Locked?"  •  "+result.LockReason:""),84);
   var list=new SmoothListView();Theme.StyleList(list);list.Columns.Add("Đường dẫn",620);list.Columns.Add("Dung lượng",110);list.Columns.Add("Sửa lần cuối",150);
   int shown=0;list.BeginUpdate();
   foreach(var item in result.Items.OrderByDescending(i=>i.Bytes).Take(2000)){var row=new ListViewItem(new[]{item.Path,Presentation.BytesLabel(item.Bytes),item.LastWrite.ToString("dd/MM/yyyy HH:mm")}){ToolTipText=item.Path};Theme.StripeRow(row,shown++);list.Items.Add(row);}
   list.EndUpdate();
   string noteText=result.Count>2000?"Hiển thị 2.000 tệp lớn nhất trong "+result.Count.ToString("N0")+" tệp.":result.Count==0?"Chưa xem trước hoặc không có tệp đủ điều kiện.":"Danh sách chỉ để xem; chưa có gì bị thay đổi.";
   var note=Theme.Note(noteText+(result.Note==""?"":"  "+result.Note),result.Locked?NoteKind.Warning:NoteKind.Info);
   var footer=new Panel{Dock=DockStyle.Bottom,Height=64,BackColor=Theme.Surface,Padding=new Padding(20,14,20,14)};Theme.BorderTop(footer);
   var close=Theme.Button("Đóng",ButtonStyle.Primary);close.Dock=DockStyle.Right;close.AutoSize=false;close.Width=120;close.Click+=(s,e)=>Close();
   var open=Theme.Button("Mở thư mục gốc",ButtonStyle.Secondary);open.Dock=DockStyle.Left;open.AutoSize=false;open.Width=160;open.Enabled=result.Roots.Count>0;
   open.Click+=(s,e)=>{try{System.Diagnostics.Process.Start("explorer.exe","\""+result.Roots[0]+"\"");}catch(Exception ex){MessageBox.Show(this,ex.Message,"Tweek Pro");}};
   footer.Controls.Add(open);footer.Controls.Add(close);
   var host=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Surface};host.Controls.Add(list);
   Controls.Add(host);Controls.Add(note);Controls.Add(header);Controls.Add(footer);
  }
 }
}
