using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Core;
using TweekPro.Shortcuts;

namespace TweekPro {
 public sealed class BrokenShortcutsForm:Form {
  readonly ListView list=new SmoothListView();readonly Label summary=new Label();
  readonly Button scan,all,none,clean,stop;CancellationTokenSource cancellation;

  /// <summary>Independent Tools dialog with explicit selection, cancellable work and existing vault restoration.</summary>
  public BrokenShortcutsForm(){
   Text=L.T("Shortcut hỏng");Size=new Size(1100,650);MinimumSize=new Size(800,500);StartPosition=FormStartPosition.CenterParent;
   Font=Theme.Body;BackColor=Theme.Surface;ForeColor=Theme.Text;ShowInTaskbar=false;Icon=Branding.AppIcon(32);
   Theme.StyleList(list);list.Dock=DockStyle.Fill;list.CheckBoxes=true;list.ShowGroups=false;
   string[] headers={"Tên shortcut","Đích không tồn tại","Vị trí shortcut","Kết quả"};int[] widths={190,300,380,180};
   for(int i=0;i<headers.Length;i++)list.Columns.Add(L.T(headers[i]),widths[i]);
   var bar=Theme.Toolbar();scan=Theme.Button("Quét shortcut",ButtonStyle.Primary);all=Theme.Button("Chọn tất cả",ButtonStyle.Secondary);none=Theme.Button("Bỏ chọn tất cả",ButtonStyle.Secondary);clean=Theme.Button("Chuyển shortcut vào Kho",ButtonStyle.Danger);stop=Theme.Button("Dừng quét / dọn",ButtonStyle.Secondary);stop.Enabled=false;
   bar.Controls.AddRange(new Control[]{scan,all,none,clean,stop});
   var note=Theme.Note("Chỉ tìm shortcut .lnk có đích bị mất trên ổ đĩa cố định trong Desktop, Start Menu và Startup. Bỏ qua liên kết mạng, ổ rời, shortcut cài theo yêu cầu và mục không xác định được đích. Chỉ chuyển shortcut vào Kho khôi phục; không xóa tệp đích.",NoteKind.Info);
   summary.Dock=DockStyle.Bottom;summary.Height=52;summary.Padding=new Padding(16,8,16,0);summary.ForeColor=Theme.Muted;summary.Text=L.T("Bấm Quét shortcut để xem trước. Chưa có mục nào được chọn để dọn.");
   Controls.Add(list);Controls.Add(summary);Controls.Add(note);Controls.Add(bar);
   scan.Click+=async(s,e)=>await Work(Scan);clean.Click+=async(s,e)=>await Work(Clean);
   all.Click+=(s,e)=>{foreach(ListViewItem row in list.Items)row.Checked=true;};none.Click+=(s,e)=>{foreach(ListViewItem row in list.Items)row.Checked=false;};
   stop.Click+=(s,e)=>{if(cancellation!=null)cancellation.Cancel();};
   FormClosing+=(s,e)=>{if(cancellation!=null){e.Cancel=true;cancellation.Cancel();}};
   FormClosed+=(s,e)=>Icon.Dispose();
  }

  /// <summary>Keeps scan/cleanup exclusive and prevents disposal while background work owns the dialog.</summary>
  async Task Work(Func<CancellationToken,Task> action){
   if(cancellation!=null)return;cancellation=new CancellationTokenSource();scan.Enabled=all.Enabled=none.Enabled=clean.Enabled=list.Enabled=false;stop.Enabled=true;
   try{await action(cancellation.Token);}
   catch(OperationCanceledException){summary.Text=L.T("Đã dừng. Những shortcut đã chuyển vẫn còn trong Kho khôi phục.");}
   catch(Exception e){summary.Text=e.Message;Core.Log.Error("Broken shortcuts: "+e);MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   finally{cancellation.Dispose();cancellation=null;scan.Enabled=all.Enabled=none.Enabled=clean.Enabled=list.Enabled=true;stop.Enabled=false;}
  }

  /// <summary>Displays only definite missing targets, unchecked by default, and reports incomplete scans.</summary>
  async Task Scan(CancellationToken token){
   summary.Text=L.T("Đang kiểm tra shortcut…");list.Items.Clear();
   var report=await Task.Run(()=>BrokenShortcuts.Scan(token),token);
   Render(report);
  }
  void Render(ShortcutScan report){
   list.BeginUpdate();list.Items.Clear();
   foreach(var item in report.Items.OrderBy(i=>i.Path)){
    var row=new ListViewItem(new[]{System.IO.Path.GetFileNameWithoutExtension(item.Path),item.Target,item.Path,L.T("Đích bị mất")}){Tag=item,ToolTipText=item.Path+"\r\n"+item.Target};
    Theme.StripeRow(row,list.Items.Count);list.Items.Add(row);
   }
   list.EndUpdate();summary.Text=L.F("Đã kiểm tra {0} shortcut • Tìm thấy {1} shortcut hỏng • Bỏ qua {2} mục không đọc được/liên kết.",report.Examined,report.Items.Count,report.Skipped)+(report.Limited?"\r\n"+L.T("Đã chạm giới hạn quét; kết quả chưa đầy đủ."):"");
  }

  /// <summary>Confirms the selected paths and revalidates each on a worker before vault movement; successful rows cannot be submitted again.</summary>
  async Task Clean(CancellationToken token){
   var rows=list.CheckedItems.Cast<ListViewItem>().ToList();if(rows.Count==0){summary.Text=L.T("Đánh dấu shortcut muốn chuyển vào Kho.");return;}
   if(MessageBox.Show(this,L.F("Chuyển {0} shortcut vào Kho khôi phục?\r\n\r\n{1}\r\n\r\nKhôi phục trong tab Kho khôi phục. Không xóa tệp đích.",rows.Count,String.Join("\r\n",rows.Take(8).Select(r=>((BrokenShortcut)r.Tag).Path))),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   int moved=0,failed=0;
   foreach(var row in rows){
    if(token.IsCancellationRequested)break;
    try{var item=(BrokenShortcut)row.Tag;await Task.Run(()=>BrokenShortcuts.Store(item));Core.Log.Info("Broken shortcut stored: "+item.Path);list.Items.Remove(row);moved++;}
    catch(Exception e){failed++;row.Checked=false;row.SubItems[3].Text=e.Message;row.ToolTipText=e.Message;row.ForeColor=Theme.Warning;Core.Log.Warn("Broken shortcut kept: "+e.Message);}
    summary.Text=L.F("Đã chuyển {0} shortcut vào Kho; {1} mục được giữ lại do thay đổi/lỗi.",moved,failed);
   }
   if(token.IsCancellationRequested)summary.Text+=" "+L.T("Đã dừng. Những shortcut đã chuyển vẫn còn trong Kho khôi phục.");
  }

  /// <summary>Synthetic preview data; never scans or changes the user's shortcuts.</summary>
  public void PopulateForPreview(){
   var report=new ShortcutScan{Examined=124,Skipped=3};
   report.Items.Add(new BrokenShortcut{Path=@"C:\Users\User\Desktop\Old Editor.lnk",Target=@"C:\Program Files\Old Editor\Editor.exe"});
   report.Items.Add(new BrokenShortcut{Path=@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Sample Tool.lnk",Target=@"C:\Tools\Sample Tool\Tool.exe"});
   Render(report);
  }
 }
 public partial class MainForm {
  /// <summary>Opens the shortcut cleaner from Tools and refreshes the shared recovery vault afterwards.</summary>
  void OpenBrokenShortcuts(){using(var dialog=new BrokenShortcutsForm())dialog.ShowDialog(this);LoadBackups();}
 }
}
