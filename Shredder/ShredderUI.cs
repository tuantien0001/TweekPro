using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Explorer;
using TweekPro.Shredder;

namespace TweekPro {
 /// <summary>Confirmation dialog for the shredder: shows the plan, lets the user pick the pass count and requires an explicit acknowledgement checkbox before the danger button unlocks.</summary>
 public sealed class ShredConfirmForm:Form {
  readonly RadioButton one=new RadioButton(),three=new RadioButton();readonly CheckBox ack=new CheckBox();
  public int Passes { get { return three.Checked?3:1; } }

  public ShredConfirmForm(ShredPlan plan){
   Text=Core.L.T("Xóa không phục hồi");Size=new Size(640,470);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
   Font=Theme.Body;BackColor=Theme.Surface;ForeColor=Theme.Text;
   var body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16,12,16,8)};
   var target=new Label{Dock=DockStyle.Top,AutoSize=true,Font=Theme.Strong,Text=plan.Target,AutoEllipsis=true,Margin=new Padding(0,0,0,6)};
   string summary=plan.IsDirectory?Core.L.F("Thư mục: {0} tệp trong {1} thư mục, {2}.",plan.Files.Count.ToString("N0"),plan.Folders.Count.ToString("N0"),Presentation.BytesLabel(plan.Bytes)):Core.L.F("Tệp: {0}.",Presentation.BytesLabel(plan.Bytes));
   if(plan.SkippedLinks.Count>0)summary+="  "+Core.L.F("{0} liên kết (symlink/junction) sẽ được bỏ qua.",plan.SkippedLinks.Count);
   var count=new Label{Dock=DockStyle.Top,AutoSize=true,Text=summary,ForeColor=Theme.Muted,Font=Theme.Small,Margin=new Padding(0,0,0,8)};
   var notes=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.FixedSingle,Font=Theme.Small,BackColor=Theme.Canvas,ForeColor=Theme.Muted};
   var lines=plan.Files.Take(200).Select(f=>Engine.Under(f,plan.Target)?f.Substring(plan.Target.Length+1):Path.GetFileName(f)).ToList();
   if(plan.Files.Count>200)lines.Add(Core.L.F("… và {0} tệp khác",plan.Files.Count-200));
   if(plan.Notes.Count>0){lines.Add("");lines.AddRange(plan.Notes.Select(n=>"⚠ "+n));}
   notes.Text=String.Join("\r\n",lines);
   var passes=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(0,6,0,0)};
   one.Text=Core.L.T("1 lần ghi ngẫu nhiên (khuyến nghị, đủ cho ổ hiện đại)");one.AutoSize=true;one.Checked=true;one.Margin=new Padding(0,0,16,0);
   three.Text=Core.L.T("3 lần (0x00, 0xFF, ngẫu nhiên — chậm gấp ba)");three.AutoSize=true;
   passes.Controls.Add(one);passes.Controls.Add(three);
   ack.Text=Core.L.T("Tôi hiểu: dữ liệu bị ghi đè, KHÔNG vào Kho khôi phục và KHÔNG thể lấy lại bằng bất kỳ công cụ nào.");ack.AutoSize=true;ack.Dock=DockStyle.Top;ack.ForeColor=Theme.Danger;ack.Font=Theme.Strong;ack.Padding=new Padding(0,8,0,4);
   var medium=Theme.Note(FileShredder.MediumNote(plan.Target),NoteKind.Warning);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(16,8,16,12),AutoSize=true,BackColor=Theme.Canvas};
   var cancel=Theme.Button("Hủy",ButtonStyle.Secondary);cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};
   var ok=Theme.Button("Xóa vĩnh viễn",ButtonStyle.Danger);ok.Enabled=false;ok.Click+=(s,e)=>{DialogResult=DialogResult.OK;Close();};
   ack.CheckedChanged+=(s,e)=>ok.Enabled=ack.Checked;
   buttons.Controls.Add(cancel);buttons.Controls.Add(ok);
   body.Controls.Add(notes);body.Controls.Add(ack);body.Controls.Add(passes);body.Controls.Add(count);body.Controls.Add(target);
   Controls.Add(body);Controls.Add(medium);Controls.Add(buttons);
   CancelButton=cancel;
  }
 }

 public partial class MainForm {
  /// <summary>Shreds the entry selected in the Explorer browser (or the inspected file): plan on a worker thread, explicit confirmation, overwrite + delete, then refresh the listing. Never touches the vault.</summary>
  async Task ShredSelected(){
   var entry=SelectedEntry();
   string target=entry!=null?entry.Path:currentFile!=null&&currentFile.Exists?currentFile.Path:null;
   if(String.IsNullOrEmpty(target))throw new IOException(Core.L.T("Chọn một tệp hoặc thư mục trong danh sách bên trái trước."));
   if(entry!=null&&entry.IsDrive)throw new IOException(Core.L.T("Không xóa gốc ổ đĩa."));
   fileStage.Visible=true;fileStage.ForeColor=Theme.Muted;fileStage.Text=Core.L.T("Đang liệt kê nội dung sẽ xóa…");
   var plan=await Task.Run(()=>FileShredder.Plan(target,CancellationToken.None));
   if(plan.Files.Count==0&&!plan.IsDirectory){fileStage.Text=Core.L.T("Không có gì để xóa.");return;}
   int passes;
   using(var dialog=new ShredConfirmForm(plan)){if(dialog.ShowDialog(this)!=DialogResult.OK){fileStage.Text=Core.L.T("Đã hủy.");return;}passes=dialog.Passes;}
   Log(Core.L.F("Xóa không phục hồi: {0} ({1} tệp, {2}, {3} lần ghi)…",plan.Target,plan.Files.Count,Presentation.BytesLabel(plan.Bytes),passes));
   var report=await Task.Run(()=>FileShredder.Shred(plan,passes,name=>{if(!IsDisposed&&IsHandleCreated)try{BeginInvoke((Action)(()=>{if(!IsDisposed)fileStage.Text=Core.L.T("Đang ghi đè: ")+name;}));}catch(InvalidOperationException){}},CancellationToken.None));
   if(currentFile!=null&&(String.Equals(currentFile.Path,plan.Target,StringComparison.OrdinalIgnoreCase)||Engine.Under(currentFile.Path,plan.Target))){currentFile=null;fileList.Items.Clear();fileList.Groups.Clear();Theme.SetOverlay(fileOverlay,"Tệp đã bị xóa không phục hồi.",NoteKind.Info);}
   string done=Core.L.F("Đã ghi đè và xóa {0} tệp ({1}) + {2} thư mục trong {3:0.0} giây.",report.Files.ToString("N0"),Presentation.BytesLabel(report.Bytes),report.Folders,report.Elapsed.TotalSeconds);
   if(report.Failed.Count>0){done+="  "+Core.L.F("{0} mục thất bại (xem nhật ký).",report.Failed.Count);foreach(string f in report.Failed.Take(30))Log(Core.L.T("Không xóa được: ")+f);}
   fileStage.Text=done;fileStage.ForeColor=report.Failed.Count>0?Theme.Danger:Theme.Muted;Log(done);
   if(!String.IsNullOrEmpty(browserPath)&&Directory.Exists(browserPath))await BrowseFolder(browserPath);else if(!String.IsNullOrEmpty(browserPath))await BrowseFolder(FileInspector.ParentOf(browserPath));
  }
 }
}
