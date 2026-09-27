using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TweekPro.Cleaner;

namespace TweekPro {
 public partial class MainForm {
  void ShowJunkResult(JunkReport report){using(var dialog=new JunkResultForm(report))if(dialog.ShowDialog(this)==DialogResult.OK){var page=tabs.TabPages.Cast<TabPage>().FirstOrDefault(p=>Branding.GlyphKey(p.Text)=="shield");if(page!=null)tabs.SelectedTab=page;}}
 }
 /// <summary>Read-only cleanup outcomes with filtering, full paths and an explicit vault explanation.</summary>
 public sealed class JunkResultForm:Form {
  readonly JunkReport report;readonly ListView list=new SmoothListView();readonly ComboBox filter=new ComboBox();readonly TextBox detail=new TextBox();
  public JunkResultForm(JunkReport result){
   report=result;Text=Core.L.T("Kết quả dọn rác");Size=new Size(940,620);MinimumSize=new Size(760,480);StartPosition=FormStartPosition.CenterParent;ShowIcon=false;
   AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=Theme.Body;BackColor=Theme.Canvas;ForeColor=Theme.Text;
   var header=Theme.HeaderBand(Core.L.T(result.Direct?"Đã xóa vĩnh viễn":"Đã chuyển vào Kho"),Core.L.F("Đã xử lý {0} tệp • {1}",result.Cleaned.ToString("N0"),Presentation.BytesLabel(result.Bytes)),84);
   var bar=Theme.Toolbar();filter.DropDownStyle=ComboBoxStyle.DropDownList;filter.Width=250;filter.Font=Theme.Body;
   filter.Items.Add(Core.L.F("Tất cả ({0})",result.Cleaned+result.SkippedInUse+result.Changed+result.Missing+result.Failed));
   int[] counts={result.Cleaned,result.SkippedInUse,result.Changed,result.Missing,result.Failed};
   for(int i=0;i<counts.Length;i++)filter.Items.Add(Label((JunkOutcomeKind)i)+" ("+counts[i].ToString("N0")+")");
   filter.SelectedIndex=0;filter.SelectedIndexChanged+=(s,e)=>Render();bar.Controls.Add(filter);
   var count=new Label{Text=Core.L.F("Đang dùng: {0} • Đã thay đổi: {1} • Không còn: {2} • Lỗi: {3}",result.SkippedInUse,result.Changed,result.Missing,result.Failed),AutoSize=true,ForeColor=result.Failed>0?Theme.Danger:Theme.Muted,Margin=new Padding(12,6,0,6)};bar.Controls.Add(count);
   Theme.StyleList(list);list.Columns.Add(Core.L.T("Trạng thái"),210);list.Columns.Add(Core.L.T("Đường dẫn"),420);list.Columns.Add(Core.L.T("Chi tiết"),200);list.SizeChanged+=(s,e)=>{float scale=list.DeviceDpi/96f;list.Columns[0].Width=(int)(210*scale);list.Columns[2].Width=(int)(200*scale);list.Columns[1].Width=Math.Max((int)(180*scale),list.ClientSize.Width-list.Columns[0].Width-list.Columns[2].Width-24);};
   list.SelectedIndexChanged+=(s,e)=>{if(list.SelectedItems.Count>0){var item=(JunkOutcome)list.SelectedItems[0].Tag;detail.Text=item.Path+"\r\n"+Label(item.Kind)+(String.IsNullOrEmpty(item.Detail)?"":" — "+item.Detail);}};
   detail.Multiline=true;detail.ReadOnly=true;detail.ScrollBars=ScrollBars.Vertical;detail.Dock=DockStyle.Fill;detail.BorderStyle=BorderStyle.None;detail.BackColor=Theme.Stripe;detail.ForeColor=Theme.Text;detail.Font=Theme.Small;
   var details=new Panel{Dock=DockStyle.Bottom,Height=72,Padding=new Padding(16,8,16,8),BackColor=Theme.Stripe};details.Controls.Add(detail);
   var note=Theme.Note(result.Direct?"Các tệp đã xóa vĩnh viễn không thể khôi phục. Các mục bị bỏ qua vẫn được giữ nguyên.":"Tệp đã chuyển vẫn chiếm dung lượng trong Kho. Mở Kho khôi phục để lấy lại hoặc xóa vĩnh viễn sau khi kiểm tra.",result.Direct?NoteKind.Warning:NoteKind.Info);
   var footer=Theme.Toolbar();footer.Dock=DockStyle.Bottom;
   var close=Theme.Button("Đóng",ButtonStyle.Secondary);close.Click+=(s,e)=>Close();footer.Controls.Add(close);CancelButton=close;
   if(!result.Direct){var vault=Theme.Button("Mở Kho khôi phục",ButtonStyle.Primary);vault.Click+=(s,e)=>{DialogResult=DialogResult.OK;Close();};footer.Controls.Add(vault);}
   if(result.Outcomes.Count<result.Cleaned+result.SkippedInUse+result.Changed+result.Missing+result.Failed){var limit=new Label{Text=Core.L.F("Danh sách hiển thị tối đa {0} tệp; số tổng bao gồm toàn bộ lượt dọn.",JunkCleaner.MaxOutcomeDetails),AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(8,8,0,0)};footer.Controls.Add(limit);}
   Controls.Add(list);Controls.Add(details);Controls.Add(note);Controls.Add(bar);Controls.Add(header);Controls.Add(footer);Render();
  }
  static string Label(JunkOutcomeKind kind){switch(kind){case JunkOutcomeKind.Cleaned:return Core.L.T("Đã xử lý");case JunkOutcomeKind.InUse:return Core.L.T("Đang dùng / không truy cập được");case JunkOutcomeKind.Changed:return Core.L.T("Đã thay đổi — giữ lại");case JunkOutcomeKind.Missing:return Core.L.T("Không còn tồn tại");default:return Core.L.T("Lỗi xử lý");}}
  void Render(){
   list.BeginUpdate();list.Items.Clear();detail.Clear();
   foreach(var item in report.Outcomes.Where(o=>filter.SelectedIndex==0||(int)o.Kind==filter.SelectedIndex-1)){
    var row=new ListViewItem(new[]{Label(item.Kind),item.Path,item.Detail??""}){Tag=item,ToolTipText=item.Path};Theme.StripeRow(row,list.Items.Count);if(item.Kind==JunkOutcomeKind.Failed)row.ForeColor=Theme.Danger;else if(item.Kind!=JunkOutcomeKind.Cleaned)row.ForeColor=Theme.Warning;list.Items.Add(row);
   }
   list.EndUpdate();if(list.Items.Count>0)list.Items[0].Selected=true;else detail.Text=Core.L.T("Không có tệp trong nhóm này.");
  }
  public static JunkReport PreviewReport(){
   var report=new JunkReport{Cleaned=1,Bytes=340*1024*1024,SkippedInUse=1,Changed=1,Failed=1};
   foreach(JunkOutcomeKind kind in new[]{JunkOutcomeKind.Cleaned,JunkOutcomeKind.InUse,JunkOutcomeKind.Changed,JunkOutcomeKind.Failed})report.Outcomes.Add(new JunkOutcome{Kind=kind,Path=@"C:\Users\ADMIN\AppData\Local\Temp\"+kind+".tmp",Detail=kind==JunkOutcomeKind.Changed?Core.L.T("Tệp đã thay đổi hoặc không còn đủ điều kiện; hãy xem trước lại."):""});
   return report;
  }
 }
}
