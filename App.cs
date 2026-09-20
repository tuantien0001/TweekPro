using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AppCare {
 public partial class MainForm:Form {
  ListView apps=new SmoothListView(),remnants=new SmoothListView(),backups=new SmoothListView();
  TextBox search=new TextBox(),details=new TextBox(),log=new TextBox();
  Label selectionSummary=new Label();Label status=new Label();TabControl tabs=new TabControl();
  List<AppEntry> inventory=new List<AppEntry>(),history=new List<AppEntry>();
  List<Candidate> candidates=new List<Candidate>();List<Button> actions=new List<Button>();
  ImageList appIcons=new ImageList(); Label appCount=new Label();
  bool busy;string sessions=Path.Combine(Path.GetDirectoryName(Engine.Vault),"sessions.xml");
  public MainForm(bool preview=false){
   Text="AppCare 0.4 • Quản lý ứng dụng Windows";Size=new Size(1220,800);MinimumSize=new Size(1120,700);StartPosition=FormStartPosition.CenterScreen;
   Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(243,246,250);AutoScaleMode=AutoScaleMode.Dpi;
   var title=new Label{Text="AppCare",Font=new Font("Segoe UI",24,FontStyle.Bold),ForeColor=Color.White,BackColor=Color.FromArgb(21,36,59),Dock=DockStyle.Top,Height=66,Padding=new Padding(24,12,0,0)};
   var subtitle=new Label{Text="Gỡ theo hàng đợi  •  Duyệt phần còn sót  •  Sao lưu và khôi phục",Dock=DockStyle.Top,Height=38,Padding=new Padding(26,0,0,0),ForeColor=Color.FromArgb(181,200,226),BackColor=Color.FromArgb(21,36,59)};
   status.Dock=DockStyle.Bottom;status.Height=30;status.Padding=new Padding(12,5,0,0);status.Text="Sẵn sàng. AppCare chỉ thay đổi dữ liệu khi bạn xác nhận.";
   tabs.Dock=DockStyle.Fill;tabs.Padding=new Point(22,12);tabs.DrawMode=TabDrawMode.OwnerDrawFixed;tabs.ItemSize=new Size(152,44);tabs.SizeMode=TabSizeMode.Fixed;
   tabs.DrawItem+=(s,e)=>{bool active=e.Index==tabs.SelectedIndex;using(var bg=new SolidBrush(active?Color.White:Color.FromArgb(235,240,247)))e.Graphics.FillRectangle(bg,e.Bounds);TextRenderer.DrawText(e.Graphics,tabs.TabPages[e.Index].Text,Font,e.Bounds,active?Color.FromArgb(31,94,203):Color.FromArgb(78,94,116),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(active)using(var line=new Pen(Color.FromArgb(43,108,224),3))e.Graphics.DrawLine(line,e.Bounds.Left+14,e.Bounds.Bottom-2,e.Bounds.Right-14,e.Bounds.Bottom-2);};
   appIcons.ColorDepth=ColorDepth.Depth32Bit;appIcons.ImageSize=new Size(32,32);apps.SmallImageList=appIcons;apps.ShowItemToolTips=true;
   FormClosed+=(s,e)=>appIcons.Dispose();
   var installed=new TabPage("Ứng dụng");var clean=new TabPage("Phần còn sót");var vault=new TabPage("Kho khôi phục");var logs=new TabPage("Nhật ký");tabs.TabPages.AddRange(new[]{installed,clean,vault,logs});
   SetupList(apps,new[]{"Ứng dụng","Phiên bản","Nhà phát hành","Dung lượng *","Phạm vi","Ngày cài / cập nhật"},new[]{330,140,240,120,125,165},true);
   var bar=Bar();bar.Controls.Add(new Label{Text="Tìm kiếm",AutoSize=true,Margin=new Padding(6,12,2,0)});search.Width=200;search.Margin=new Padding(6,8,8,0);search.TextChanged+=(s,e)=>Filter();bar.Controls.Add(search);
   Add(bar,"Làm mới",async()=>await Reload());Add(bar,"Gỡ mục đã chọn",async()=>await Uninstall());Add(bar,"Quét mục đang xem",async()=>await ScanSelected());Add(bar,"Xuất CSV",()=>{ExportApps();return Task.FromResult(0);});deepMode.Text="Quét sâu sau gỡ";deepMode.Checked=true;deepMode.AutoSize=true;deepMode.Margin=new Padding(8,13,0,0);bar.Controls.Add(deepMode);
   details.Dock=DockStyle.Bottom;details.Height=84;details.Multiline=true;details.ReadOnly=true;details.ScrollBars=ScrollBars.Vertical;details.BackColor=Color.FromArgb(242,246,251);details.ForeColor=Color.FromArgb(66,83,106);details.BorderStyle=BorderStyle.None;
   details.Text="Chọn một ứng dụng để xem thông tin. Dùng ô tìm kiếm để lọc theo tên hoặc nhà phát hành.";
   apps.SelectedIndexChanged+=(s,e)=>{var a=Selected();details.Text=a==null?"Chọn một ứng dụng để xem thông tin.":a.Name+"  •  "+a.Version+"\r\n"+a.Publisher+"  |  "+Presentation.SizeLabel(a.Size)+"  |  "+Presentation.DateLabel(a.InstallDate)+"\r\nThư mục: "+(String.IsNullOrWhiteSpace(a.Location)?"Chưa được ứng dụng khai báo":a.Location)+"\r\nNgày do bộ cài cung cấp, có thể là ngày cập nhật. Giá trị gốc: "+(String.IsNullOrWhiteSpace(a.InstallDate)?"không có":a.InstallDate);};
   appCount.Dock=DockStyle.Top;appCount.Height=36;appCount.Padding=new Padding(14,8,0,0);appCount.BackColor=Color.FromArgb(246,248,252);appCount.ForeColor=Color.FromArgb(76,95,118);
   installed.Controls.Add(apps);installed.Controls.Add(details);installed.Controls.Add(appCount);installed.Controls.Add(bar);
   SetupList(remnants,new[]{"Loại","Ứng dụng","Đường dẫn","Cơ sở đề xuất"},new[]{90,200,460,430},true);
   var cleanbar=Bar();cleanbar.WrapContents=true;cleanbar.Height=90;Add(cleanbar,"Quét lại lịch sử gỡ",async()=>await ScanHistory());Add(cleanbar,"Quét siêu sâu",async()=>await DeepHistory());Add(cleanbar,"Chọn tất cả",()=>{SetRemnantChecks(true);return Task.FromResult(0);});Add(cleanbar,"Bỏ chọn tất cả",()=>{SetRemnantChecks(false);return Task.FromResult(0);});Add(cleanbar,"Xem mục",()=>{InspectCandidate();return Task.FromResult(0);});Add(cleanbar,"Xóa đã chọn (có sao lưu)",async()=>await Cleanup());Add(cleanbar,"Xuất báo cáo",()=>{ExportCandidates();return Task.FromResult(0);});
   var note=new Label{Text="Mục nghi còn sót, chưa chắc thuộc riêng ứng dụng. Có thể chứa dữ liệu cá nhân. Chỉ cho dọn sau khi đăng ký cài đặt đã biến mất.",Dock=DockStyle.Top,Height=50,Padding=new Padding(10),BackColor=Color.FromArgb(255,245,219)};
   selectionSummary.Dock=DockStyle.Bottom;selectionSummary.Height=30;selectionSummary.Padding=new Padding(10,5,0,0);
   remnants.ItemCheck+=(s,e)=>{if(((Candidate)remnants.Items[e.Index].Tag).ReviewOnly)e.NewValue=CheckState.Unchecked;};remnants.ItemChecked+=(s,e)=>UpdateSelectionSummary();
   clean.Controls.Add(remnants);clean.Controls.Add(selectionSummary);clean.Controls.Add(note);clean.Controls.Add(cleanbar);
   SetupList(backups,new[]{"Ngày","Ứng dụng","Loại","Trạng thái","Đường dẫn gốc"},new[]{175,210,90,150,520},false);
   var backupbar=Bar();Add(backupbar,"Làm mới kho",()=>{LoadBackups();return Task.FromResult(0);});Add(backupbar,"Khôi phục mục đang chọn",async()=>await Restore());Add(backupbar,"Mở kho",()=>{Directory.CreateDirectory(Engine.Vault);Process.Start("explorer.exe","\""+Engine.Vault+"\"");return Task.FromResult(0);});
   var backupnote=new Label{Text="Khôi phục file/giá trị Registry đã dọn; không cài lại ứng dụng. Không ghi đè đích đã tồn tại. Kho lưu trên máy này, không phải bản sao lưu chống hỏng ổ đĩa.",Dock=DockStyle.Top,Height=50,Padding=new Padding(10),BackColor=Color.FromArgb(230,242,255)};
   vault.Controls.Add(backups);vault.Controls.Add(backupnote);vault.Controls.Add(backupbar);
   log.Dock=DockStyle.Fill;log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Both;logs.Controls.Add(log);
   BuildAdvancedTabs();
   Controls.Add(tabs);Controls.Add(subtitle);Controls.Add(title);Controls.Add(status);
   if(!preview) Shown+=async(s,e)=>await Guard(async()=>await Reload());FormClosing+=(s,e)=>{if(busy){e.Cancel=true;MessageBox.Show(this,"Đang xử lý. Hãy chờ thao tác hiện tại hoàn tất.");}};
   try{if(File.Exists(sessions))history=Engine.Load<List<AppEntry>>(sessions);}catch(Exception e){Log("Không đọc được lịch sử: "+e.Message);}
  }
  FlowLayoutPanel Bar(){return new FlowLayoutPanel{Dock=DockStyle.Top,Height=64,Padding=new Padding(8),WrapContents=false,AutoScroll=true,BackColor=Color.White};}
  void Add(FlowLayoutPanel bar,string text,Func<Task> action){var b=new Button{Text=text,AutoSize=true,Height=38,MinimumSize=new Size(80,38),FlatStyle=FlatStyle.Flat,Margin=new Padding(4),BackColor=Color.FromArgb(237,243,251),ForeColor=Color.FromArgb(27,63,104)};b.Padding=new Padding(7,2,7,2);b.Cursor=Cursors.Hand;b.FlatAppearance.BorderColor=Color.FromArgb(215,224,238);
   if(text.StartsWith("Gỡ ")||text.StartsWith("Xóa đã")){b.BackColor=Color.FromArgb(37,99,215);b.ForeColor=Color.White;b.FlatAppearance.BorderSize=0;}
b.Click+=async(s,e)=>await Guard(action);bar.Controls.Add(b);actions.Add(b);}
  void SetupList(ListView list,string[] names,int[] widths,bool check){list.Dock=DockStyle.Fill;list.View=View.Details;list.FullRowSelect=true;list.CheckBoxes=check;list.HideSelection=false;list.GridLines=false;list.BorderStyle=BorderStyle.None;list.ForeColor=Color.FromArgb(38,53,74);list.MultiSelect=false;list.BackColor=Color.White;for(int i=0;i<names.Length;i++)list.Columns.Add(names[i],widths[i]);}
  async Task Guard(Func<Task> action){if(busy)return;busy=true;foreach(var b in actions)b.Enabled=false;deepMode.Enabled=false;search.Enabled=false;apps.Enabled=false;remnants.Enabled=false;backups.Enabled=false;try{await action();}catch(Exception e){Log("LỖI: "+e.Message);MessageBox.Show(this,e.Message,"AppCare",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{busy=false;foreach(var b in actions)b.Enabled=true;deepMode.Enabled=true;search.Enabled=true;apps.Enabled=true;remnants.Enabled=true;backups.Enabled=true;}}
  void Log(string value){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+value+"\r\n");status.Text=value;try{string dir=Path.GetDirectoryName(Engine.Vault);Directory.CreateDirectory(dir);File.AppendAllText(Path.Combine(dir,"activity.log"),DateTime.Now.ToString("s")+" "+value+Environment.NewLine,Encoding.UTF8);}catch{}}
  bool Confirm(string text){return MessageBox.Show(this,text,"Xác nhận thao tác",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;}
  AppEntry Selected(){return apps.SelectedItems.Count==0?null:(AppEntry)apps.SelectedItems[0].Tag;}
  List<AppEntry> CheckedApps(){var list=apps.CheckedItems.Cast<ListViewItem>().Select(i=>(AppEntry)i.Tag).ToList();if(list.Count==0&&Selected()!=null)list.Add(Selected());return list;}
  public void PopulateForPreview(){inventory=Engine.Inventory();LoadAppIcons();Filter();LoadBackups();}
  public void PreviewRemnants(){PresentCandidates(new[]{new Candidate{Kind="Folder",AppName="Ứng dụng mẫu",Path=@"C:\Program Files\Example App",Reason="Dữ liệu minh họa giao diện, không phải kết quả quét."},new Candidate{Kind="Registry",AppName="Ứng dụng mẫu",Path=@"SOFTWARE\Example App",Hive="HKCU",View="64",Reason="Dữ liệu minh họa giao diện."}});tabs.SelectedIndex=1;SetRemnantChecks(true);}
  async Task Reload(){Log("Đang đọc danh sách ứng dụng…");inventory=await Task.Run(()=>Engine.Inventory());LoadAppIcons();Filter();LoadBackups();Log("Đã đọc "+inventory.Count+" ứng dụng desktop. Chưa bao gồm toàn bộ ứng dụng Microsoft Store.");}
  void LoadAppIcons(){appIcons.Images.Clear();var imageHandle=appIcons.Handle;foreach(var a in inventory){using(var bitmap=Presentation.AppIcon(a))appIcons.Images.Add(a.Id,bitmap);}}
  void Filter(){var checkedIds=new HashSet<string>(apps.CheckedItems.Cast<ListViewItem>().Select(i=>((AppEntry)i.Tag).Id));apps.BeginUpdate();apps.Items.Clear();details.Clear();string q=search.Text.Trim();foreach(var a in inventory.Where(a=>(a.Name+" "+a.Publisher).IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0)){
   var item=new ListViewItem(new[]{a.Name,a.Version,a.Publisher,Presentation.SizeLabel(a.Size),a.Hive=="HKLM"?"Toàn máy":"Tài khoản",Presentation.DateLabel(a.InstallDate)}){Tag=a,ImageKey=a.Id,Checked=checkedIds.Contains(a.Id),BackColor=apps.Items.Count%2==0?Color.White:Color.FromArgb(247,249,252),ToolTipText="Ngày bộ cài khai báo: "+(String.IsNullOrWhiteSpace(a.InstallDate)?"Không có":a.InstallDate)};apps.Items.Add(item);
  }apps.EndUpdate();appCount.Text=apps.Items.Count+" ứng dụng hiển thị  /  "+inventory.Count+" ứng dụng trên máy";}
  void Remember(IEnumerable<AppEntry> entries){foreach(var a in entries){history.RemoveAll(x=>x.Id==a.Id);history.Add(a);}Directory.CreateDirectory(Path.GetDirectoryName(sessions));Engine.Save(sessions,history);}
  async Task Uninstall(){
   var queue=CheckedApps();if(queue.Count==0)throw new IOException("Chọn ứng dụng muốn gỡ.");
   if(!Confirm("Mở trình gỡ chính thức lần lượt cho "+queue.Count+" ứng dụng?\r\n\r\n"+String.Join("\r\n",queue.Take(12).Select(a=>a.Name))+"\r\n\r\nTrình gỡ có thể xóa dữ liệu và yêu cầu quyền quản trị. Kho AppCare chỉ khôi phục phần dọn sau đó, không hoàn tác trình gỡ."))return;
   foreach(var entry in queue)Advanced.Capture(entry);Remember(queue);candidates.Clear();RenderCandidates();
   for(int i=0;i<queue.Count;i++){
    var a=queue[i];if(i>0&&!Confirm("Tiếp tục gỡ "+a.Name+"?\r\nChọn No để dừng hàng đợi."))break;
    try{
     var info=Engine.UninstallInfo(a);Log("Mở trình gỡ: "+a.Name);
     using(var process=Process.Start(info)){if(process!=null)await Task.Run(()=>process.WaitForExit());}
     bool remains=Engine.Installed(a.Id);
     if(remains){
      await Task.Delay(1500);remains=Engine.Installed(a.Id);
      if(remains){MessageBox.Show(this,"Trình gỡ của "+a.Name+" đã kết thúc tiến trình chính nhưng ứng dụng vẫn còn đăng ký. Nếu có cửa sổ gỡ khác, hãy hoàn tất hoặc hủy rồi nhấn OK. Nếu cần khởi động lại, có thể quét lại lịch sử sau đó.","Kiểm tra trình gỡ",MessageBoxButtons.OK,MessageBoxIcon.Information);remains=Engine.Installed(a.Id);}
     }
     Log(a.Name+": "+(remains?"vẫn còn đăng ký cài đặt; không tự quét/dọn.":"đã bỏ đăng ký; tự động quét phần còn sót."));
     if(!remains){if(deepMode.Checked)await ScanDeepEntries(new[]{a},true);else await ScanEntries(new[]{a},true);}
    }catch(Exception e){Log(a.Name+": "+e.Message);MessageBox.Show(this,a.Name+"\r\n"+e.Message,"Không hoàn tất thao tác");}
   }
   await Reload();tabs.SelectedIndex=1;
   Log("Đã kết thúc hàng đợi. Có "+candidates.Count+" mục cần duyệt. Dùng Chọn tất cả → Xóa đã chọn (có sao lưu).");
  }
  async Task ScanSelected(){var a=Selected();if(a==null)throw new IOException("Chọn một dòng ứng dụng trước.");Advanced.Capture(a);Remember(new[]{a});await ScanEntries(new[]{a});}
  async Task ScanHistory(){if(history.Count==0)throw new IOException("Chưa có lịch sử. Hãy chọn ứng dụng và quét hoặc gỡ trước.");await ScanEntries(history.ToArray());}
  async Task ScanEntries(IEnumerable<AppEntry> entries,bool append=false){
   var input=entries.ToArray();Log("Đang quét "+input.Length+" ứng dụng…");
   var found=await Task.Run(()=>input.SelectMany(a=>Engine.Scan(a)).ToList());
   PresentCandidates(append?candidates.Concat(found):found);tabs.SelectedIndex=1;
   Log("Tìm thấy "+candidates.Count+" mục cần duyệt. Không tìm thấy không có nghĩa đã sạch toàn bộ.");
  }
  internal void PresentCandidates(IEnumerable<Candidate> items){
   candidates=items.GroupBy(Advanced.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();RenderCandidates();
  }
  internal int CheckedRemnantCount { get { return remnants.CheckedItems.Count; } }
  internal void SetRemnantChecks(bool check){remnants.BeginUpdate();foreach(ListViewItem item in remnants.Items)item.Checked=check&&!((Candidate)item.Tag).ReviewOnly;remnants.EndUpdate();UpdateSelectionSummary();}
  void UpdateSelectionSummary(){selectionSummary.Text="Tìm thấy: "+remnants.Items.Count+" mục  •  Đã chọn: "+remnants.CheckedItems.Count+"  •  Xóa sẽ lưu bản khôi phục trước.";}
  void RenderCandidates(){remnants.BeginUpdate();remnants.Items.Clear();foreach(var c in candidates)remnants.Items.Add(new ListViewItem(new[]{CandidateKind(c),c.AppName,CandidatePath(c),(c.ReviewOnly?"CHỈ XEM • ":"")+c.Reason}){Tag=c});remnants.EndUpdate();UpdateSelectionSummary();}
  void InspectCandidate(){
   if(remnants.SelectedItems.Count==0)throw new IOException("Chọn một mục để xem.");var c=(Candidate)remnants.SelectedItems[0].Tag;
   if(c.Kind=="Folder"){Engine.ValidateFolder(c.Path,false);Process.Start("explorer.exe","\""+c.Path+"\"");}
   else if(c.Kind=="File"){Advanced.ValidateFile(c.Path);Process.Start("explorer.exe","/select,\""+c.Path+"\"");}
   else MessageBox.Show(this,CandidatePath(c)+"\r\n\r\n"+c.Reason+(c.ReviewOnly?"\r\n\r\nChỉ xem: không tự xóa mục này.":"\r\n\r\nKiểm tra đúng khóa hoặc giá trị trước khi chọn dọn."),"Chi tiết mục còn sót");
  }
  async Task Cleanup(){
   var selected=remnants.CheckedItems.Cast<ListViewItem>().Select(i=>(Candidate)i.Tag).Where(c=>!c.ReviewOnly).ToList();if(selected.Count==0)throw new IOException("Đánh dấu những mục đã kiểm tra và muốn dọn.");
   if(!Confirm("Sao lưu và dọn "+selected.Count+" mục đã đánh dấu?\r\n\r\n"+String.Join("\r\n",selected.Take(8).Select(c=>c.Path))+"\r\n\r\nCác mục có thể chứa cài đặt, dự án hoặc dữ liệu cá nhân. Chỉ tiếp tục khi đã kiểm tra. File được chuyển vào kho nên chưa giải phóng dung lượng ổ đĩa. Registry được lưu giá trị và khóa con, không lưu quyền ACL."))return;
   int removedFolders=0,removedKeys=0,failed=0;
   foreach(var c in selected){try{Log("Đang sao lưu: "+c.Path);await Task.Run(()=>Engine.Quarantine(c));Log("Đã sao lưu và dọn: "+c.Path);candidates.Remove(c);if(c.Kind=="Folder"||c.Kind=="File")removedFolders++;else removedKeys++;}catch(Exception e){failed++;Log("Giữ lại / cần kiểm tra "+c.Path+": "+e.Message);MessageBox.Show(this,c.Path+"\r\n"+e.Message,"Mục chưa dọn xong");}}
   RenderCandidates();LoadBackups();
   string summary="Đã dọn và sao lưu: "+removedFolders+" mục file/thư mục, "+removedKeys+" mục Registry. Chưa dọn: "+failed+" mục. Còn hiển thị: "+candidates.Count+" mục.";
   Log(summary);MessageBox.Show(this,summary+"\r\n\r\nCó thể khôi phục trong Kho khôi phục. File trong kho vẫn chiếm dung lượng.","Kết quả dọn",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }
  void LoadBackups(){backups.Items.Clear();foreach(var b in Engine.Backups()){string state=b.State=="BackedUp"?"Đã sao lưu":b.State=="Restored"?"Đã khôi phục":b.State=="Pending"?"Cần kiểm tra":"Cần kiểm tra";backups.Items.Add(new ListViewItem(new[]{b.Created,b.AppName,b.Kind,state,b.Original}){Tag=b});}}
  async Task Restore(){if(backups.SelectedItems.Count==0)throw new IOException("Chọn một bản sao lưu.");var b=(Backup)backups.SelectedItems[0].Tag;if(b.State=="Restored")throw new IOException("Mục này đã khôi phục.");if(!Confirm("Khôi phục về vị trí gốc?\r\n"+b.Original+"\r\n\r\nKhông ghi đè nếu đích đã tồn tại. Nếu lần dọn trước bị gián đoạn, kiểm tra cả vị trí gốc và kho."))return;await Task.Run(()=>Engine.Restore(b));LoadBackups();Log("Đã khôi phục: "+b.Original);}
  string SavePath(string name){using(var dialog=new SaveFileDialog{Filter="CSV UTF-8|*.csv",FileName=name})return dialog.ShowDialog(this)==DialogResult.OK?dialog.FileName:null;}
  void ExportApps(){string p=SavePath("AppCare-applications.csv");if(p==null)return;var lines=new List<string>{"Name,Version,Publisher,SizeKB,RegistryView,InstallLocation,UninstallCommand"};lines.AddRange(inventory.Select(a=>String.Join(",",new[]{a.Name,a.Version,a.Publisher,a.Size.ToString(),a.Hive+"/"+a.View,a.Location,a.Command}.Select(Engine.Csv))));File.WriteAllLines(p,lines, new UTF8Encoding(true));Log("Đã xuất danh sách: "+p);}
  void ExportCandidates(){string p=SavePath("AppCare-review.csv");if(p==null)return;var lines=new List<string>{"App,Kind,Path,Hive,View,Reason"};lines.AddRange(candidates.Select(c=>String.Join(",",new[]{c.AppName,c.Kind,CandidatePath(c),c.Hive,c.View,(c.ReviewOnly?"CHỈ XEM: ":"")+c.Reason}.Select(Engine.Csv))));File.WriteAllLines(p,lines,new UTF8Encoding(true));Log("Đã xuất báo cáo: "+p);}
 }
 public static class Program {
  [STAThread]public static void Main(string[] args){
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   if(args.Length>0&&args[0]=="--self-test"){try{Tests.Run();AdvancedTests.Run();File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),"PASS: path boundaries, protected folders, command parsing, file backup/restore, registry value round-trip, destination collision, live inventory (read-only), exact install-folder alias scan, bulk selection, deduplication, install-date formatting, icon resource parsing, autorun value/shortcut backup and restore, stale-value conflict protection, deep scan, tool catalog.\r\n"+DateTime.Now.ToString("s"));Environment.ExitCode=0;}catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),e.ToString());Environment.ExitCode=1;}return;}
   if(args.Length>0&&args[0]=="--scan-smoke"){try{var app=Engine.Inventory().First(a=>a.Name=="Brave");Advanced.Capture(app);var result=Advanced.DeepScan(app,System.Threading.CancellationToken.None);File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"scan-smoke.txt"),new[]{"Read-only deep scan: "+app.Name,"Visited folders: "+result.Visited,"Candidates: "+result.Items.Count,"Notes: "+result.Notes.Count}.Concat(result.Notes));}catch(Exception error){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"scan-smoke.txt"),error.ToString());Environment.ExitCode=1;}return;}
   if(args.Length>0&&args[0]=="--preview"){try{using(var form=new MainForm(true)){form.PopulateForPreview();if(args.Length>1){if(args[1]=="autorun"||args[1]=="tools")form.PreviewAdvanced(args[1]);else form.PreviewRemnants();}form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"AppCare-preview.png"));}}}catch(Exception error){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-error.txt"),error.ToString());Environment.ExitCode=1;}return;}
   Application.Run(new MainForm());
  }
 }
 public static class Tests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
  static void MustFail(Action a){bool failed=false;try{a();}catch{failed=true;}Assert(failed,"Expected safety check to reject operation.");}
  public static void Run(){
   Assert(Presentation.DateLabel("20260919")=="19/09/2026","MSI compact date");
   Assert(Presentation.DateLabel("9/18/2026")=="18/09/2026","Installer US date");
   Assert(Presentation.DateLabel("2026-09-02")=="02/09/2026","ISO date");
   Assert(Presentation.DateLabel("")=="Không rõ"&&Presentation.DateLabel("20260230")=="Không rõ","Missing or invalid date");
   string iconPath;int iconIndex;Assert(Presentation.ParseIcon("\"C:\\Apps\\Example.dll\",-12",out iconPath,out iconIndex)&&iconIndex==-12&&!iconPath.Contains("\""),"Icon resource index");
   Assert(!Presentation.ParseIcon(@"\\server\share\icon.ico",out iconPath,out iconIndex),"Avoid network icon paths");

   Assert(Engine.Under(@"C:\Apps\A",@"C:\Apps"),"Child path");Assert(!Engine.Under(@"C:\AppsOther",@"C:\Apps"),"Prefix confusion");
   MustFail(()=>Engine.ValidateFolder(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
   MustFail(()=>Engine.ValidateFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
   MustFail(()=>Engine.ValidateRegistry(@"SOFTWARE\Microsoft"));MustFail(()=>Engine.ValidateRegistry(@"SOFTWARE\Classes\Something"));
   var msi=new AppEntry{Msi=true,Key=@"SOFTWARE\Uninstall\{11111111-2222-3333-4444-555555555555}"};Assert(Engine.UninstallInfo(msi).Arguments.StartsWith("/x "),"MSI remove mode");
   string exe=System.Reflection.Assembly.GetExecutingAssembly().Location;
   var info=Engine.UninstallInfo(new AppEntry{Key="Test",Command="\""+exe+"\" /test \"with space\""});Assert(info.FileName==exe&&info.Arguments=="/test \"with space\"","Command parsing");
   MustFail(()=>Engine.UninstallInfo(new AppEntry{Key="Test",Command="cmd.exe /c anything"}));
   Assert(Engine.Csv("=1+1").Contains("'=1+1"),"CSV formula protection"); string name="AppCareTest"+Guid.NewGuid().ToString("N");string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);string folder=Path.Combine(local,name);string vault=Path.Combine(local,name+"Vault");string originalVault=Engine.Vault;string key="SOFTWARE\\"+name;string view=Environment.Is64BitOperatingSystem?"64":"32";
   try{
    Engine.Vault=vault;Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"sample.txt"),"sample data — kiểm thử");
    var sampleApp=new AppEntry{Name="Friendly "+name,Location=folder,Publisher="PublisherFixture",Hive="HKCU",View=view,Key="SOFTWARE\\"+name+"Missing"};
    Assert(Engine.Scan(sampleApp).Any(x=>x.Path==folder),"Install-folder alias scan");
    using(var form=new MainForm(true)){
     var sample=new Candidate{Kind="Folder",Path=folder,AppName=name};form.PresentCandidates(new[]{sample,sample,new Candidate{Kind="Registry",Path=key,AppName=name,Hive="HKCU",View=view},new Candidate{Kind="Review",Path="Service: fixture",ReviewOnly=true}});
     Assert(form.CheckedRemnantCount==0,"No implicit selection");form.SetRemnantChecks(true);Assert(form.CheckedRemnantCount==2,"Select all and deduplicate");form.SetRemnantChecks(false);Assert(form.CheckedRemnantCount==0,"Deselect all");
    }
    var c=new Candidate{Kind="Folder",Path=folder,AppId="HKCU|"+view+"|SOFTWARE\\"+name+"Missing",AppName=name};
    var b=Engine.Quarantine(c);Assert(!Directory.Exists(folder),"Folder moved");Engine.Restore(b);Assert(File.ReadAllText(Path.Combine(folder,"sample.txt"))=="sample data — kiểm thử","Folder restore");MustFail(()=>Engine.Restore(b));
    using(var root=Engine.Base("HKCU",view))using(var r=root.CreateSubKey(key)){r.SetValue("String","hello");r.SetValue("Expand","%TEMP%",RegistryValueKind.ExpandString);r.SetValue("DWord",-1,RegistryValueKind.DWord);r.SetValue("QWord",long.MaxValue,RegistryValueKind.QWord);r.SetValue("Multi",new[]{"a","b"},RegistryValueKind.MultiString);r.SetValue("Binary",new byte[]{0,255,12},RegistryValueKind.Binary);using(var child=r.CreateSubKey("Child"))child.SetValue("","default");}
    Assert(Engine.Scan(sampleApp).Any(x=>x.Kind=="Registry"&&x.Path==key),"Registry matched by recorded install-folder alias, not display name");
    var rc=new Candidate{Kind="Registry",Path=key,Hive="HKCU",View=view,AppId=c.AppId,AppName=name};var rb=Engine.Quarantine(rc);using(var root=Engine.Base("HKCU",view))using(var r=root.OpenSubKey(key))Assert(r==null,"Registry deleted only after backup");Engine.Restore(rb);
    using(var root=Engine.Base("HKCU",view))using(var r=root.OpenSubKey(key)){Assert((int)r.GetValue("DWord")==-1,"DWORD");Assert((long)r.GetValue("QWord")==long.MaxValue,"QWORD");Assert((string)r.GetValue("Expand",null,RegistryValueOptions.DoNotExpandEnvironmentNames)=="%TEMP%","Expand value");Assert(((string[])r.GetValue("Multi")).SequenceEqual(new[]{"a","b"}),"Multi value");Assert(((byte[])r.GetValue("Binary")).SequenceEqual(new byte[]{0,255,12}),"Binary");using(var child=r.OpenSubKey("Child"))Assert((string)child.GetValue("")=="default","Child default");}
    MustFail(()=>Engine.Restore(rb));Assert(Engine.Inventory().Count>0,"Live inventory read");
   }finally{
    Engine.Vault=originalVault;
    // Test cleanup is restricted to the randomly named fixtures created above.
    if(Path.GetFileName(folder)==name&&Engine.Under(Engine.Canon(folder),Engine.Canon(local))&&Directory.Exists(folder))Directory.Delete(folder,true);
    if(Path.GetFileName(vault)==name+"Vault"&&Engine.Under(Engine.Canon(vault),Engine.Canon(local))&&Directory.Exists(vault))Directory.Delete(vault,true);
    using(var root=Engine.Base("HKCU",view)){if(key=="SOFTWARE\\"+name)root.DeleteSubKeyTree(key,false);}
   }
  }
 }
}
