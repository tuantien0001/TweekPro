using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TweekPro {
 /// <summary>Age-based vault purge dialog: shows how many backups and how many bytes would be released before anything is deleted.</summary>
 public class PurgeForm:Form {
  readonly List<Backup> all;
  readonly NumericUpDown days=new NumericUpDown();
  readonly CheckBox includeUnrestored=new CheckBox();
  readonly Label preview=new Label();
  readonly Button ok;
  public List<Backup> Selected=new List<Backup>();
  public int Days { get { return (int)days.Value; } }

  public PurgeForm(List<Backup> backups,int defaultDays){
   all=backups;
   Text="Dọn kho theo tuổi";Size=new Size(640,420);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;ShowIcon=false;
   Font=Theme.Body;BackColor=Theme.Canvas;ForeColor=Theme.Text;AutoScaleMode=AutoScaleMode.Dpi;
   var header=Theme.HeaderBand("Xóa vĩnh viễn bản sao lưu cũ","Chỉ xóa những gì bạn xác nhận; dung lượng được giải phóng ngay.",84);
   var body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(28,20,28,12),BackColor=Theme.Surface};
   var row1=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=false,Padding=new Padding(0,0,0,12)};
   row1.Controls.Add(new Label{Text="Xóa bản sao lưu cũ hơn",AutoSize=true,Margin=new Padding(0,7,8,0)});
   days.Minimum=0;days.Maximum=3650;days.Value=Math.Min(3650,Math.Max(0,defaultDays));days.Width=80;days.Font=Theme.Body;days.ValueChanged+=(s,e)=>Recompute();
   row1.Controls.Add(days);row1.Controls.Add(new Label{Text="ngày (0 = tất cả)",AutoSize=true,Margin=new Padding(8,7,0,0),ForeColor=Theme.Muted});
   includeUnrestored.Text="Bao gồm cả bản CHƯA khôi phục (mất khả năng hoàn tác các lần dọn đó)";includeUnrestored.AutoSize=true;includeUnrestored.ForeColor=Theme.Danger;includeUnrestored.Font=Theme.Strong;includeUnrestored.Dock=DockStyle.Top;includeUnrestored.Padding=new Padding(0,0,0,12);includeUnrestored.CheckedChanged+=(s,e)=>Recompute();
   preview.Dock=DockStyle.Top;preview.AutoSize=false;preview.Height=110;preview.Font=Theme.Body;preview.ForeColor=Theme.Text;
   var hint=new Label{Dock=DockStyle.Top,AutoSize=false,Height=48,Text="Mặc định chỉ xóa bản đã khôi phục (thư mục gần như trống). Bản đã sao lưu nhưng chưa khôi phục là cách duy nhất để hoàn tác lần dọn tương ứng.",ForeColor=Theme.Muted,Font=Theme.Small};
   body.Controls.Add(hint);body.Controls.Add(preview);body.Controls.Add(includeUnrestored);body.Controls.Add(row1);
   var footer=new Panel{Dock=DockStyle.Bottom,Height=64,BackColor=Theme.Surface,Padding=new Padding(20,14,20,14)};Theme.BorderTop(footer);
   ok=Theme.Button("Tiếp tục xóa vĩnh viễn…",ButtonStyle.Danger);ok.Dock=DockStyle.Right;ok.AutoSize=false;ok.Width=220;ok.Click+=(s,e)=>{DialogResult=DialogResult.OK;Close();};
   var cancel=Theme.Button("Hủy",ButtonStyle.Secondary);cancel.Dock=DockStyle.Left;cancel.AutoSize=false;cancel.Width=110;cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};
   footer.Controls.Add(cancel);footer.Controls.Add(ok);
   Controls.Add(body);Controls.Add(header);Controls.Add(footer);
   CancelButton=cancel;
   Recompute();
  }

  /// <summary>Recomputes the selection and the size preview from the current age and scope options.</summary>
  void Recompute(){
   Selected=Engine.SelectForPurge(all,(int)days.Value,DateTime.Now,includeUnrestored.Checked);
   long bytes=Selected.Where(b=>b.Bytes>0).Sum(b=>b.Bytes);int unrestored=Selected.Count(b=>b.State!="Restored");
   preview.Text=Selected.Count==0?"Không có bản sao lưu nào khớp.\r\nKho hiện có "+all.Count+" bản; thử giảm số ngày hoặc bật tùy chọn bao gồm bản chưa khôi phục.":"Sẽ xóa vĩnh viễn "+Selected.Count+" / "+all.Count+" bản sao lưu.\r\nDung lượng được giải phóng: "+Presentation.BytesLabel(bytes)+".\r\n"+(unrestored>0?"Trong đó "+unrestored+" bản chưa khôi phục.":"Tất cả đều đã khôi phục về vị trí gốc.")+"\r\nCũ nhất: "+Presentation.StampLabel(Selected.Min(b=>b.Created))+"  •  Mới nhất: "+Presentation.StampLabel(Selected.Max(b=>b.Created));
   ok.Enabled=Selected.Count>0;
  }
 }
}
