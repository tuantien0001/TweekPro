using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace TweekPro {
 public partial class MainForm {
  CheckBox deepMode=new CheckBox();CancellationTokenSource scanCancellation;
  ListView autorunList=new SmoothListView(),toolList=new SmoothListView();ImageList startupIcons=new ImageList();
  Label autorunNote,autorunOverlay;Button cancelScan;TabPage autorunTab,toolsTab;
  void BuildAdvancedTabs(){
   autorunTab=new TabPage("Khởi động");toolsTab=new TabPage("Công cụ");tabs.TabPages.Add(autorunTab);tabs.TabPages.Add(toolsTab);
   startupIcons.ColorDepth=ColorDepth.Depth32Bit;startupIcons.ImageSize=new Size(24,24);autorunList.SmallImageList=startupIcons;FormClosed+=(s,e)=>startupIcons.Dispose();
   SetupList(autorunList,new[]{"Tên khởi động","Trạng thái","Lệnh / đích","Vị trí"},new[]{230,180,440,360},false);
   var autorunHost=Theme.ListHost(autorunList,out autorunOverlay);
   var bar=Bar();
   Add(bar,"Tải danh sách",async()=>await LoadAutoruns());
   Add(bar,"Tắt (sao lưu)",async()=>await DisableAutorun(),ButtonStyle.Primary);
   Add(bar,"Bật lại từ kho",async()=>await EnableAutorun());
   Add(bar,"Sao chép vị trí",()=>{var a=CurrentAutorun();Clipboard.SetText(a.Item!=null?Presentation.CandidatePath(a.Item):a.Saved.Original);return Task.FromResult(0);});
   Add(bar,"Startup của Windows",()=>{WindowsTools.Launch(WindowsTools.Catalog().First(t=>t.Name=="Startup Apps"));return Task.FromResult(0);});
   Add(bar,"Xuất CSV",()=>{ExportAutoruns();return Task.FromResult(0);});
   autorunNote=Theme.Note("Run, RunOnce và shortcut Startup của tài khoản hiện tại/toàn máy. Có đăng ký không có nghĩa Windows đang cho chạy. Bật lại áp dụng cho mục đã tắt bằng Tweek Pro.",NoteKind.Info);
   autorunTab.Controls.Add(autorunHost);autorunTab.Controls.Add(autorunNote);autorunTab.Controls.Add(bar);
   Theme.SetOverlay(autorunOverlay,"Chưa tải mục khởi động.\r\nBấm Tải danh sách hoặc mở tab này để đọc Run/RunOnce và shortcut Startup.",NoteKind.Info);
   SetupList(toolList,new[]{"Công cụ","Tình trạng","Công dụng"},new[]{240,160,710},false);
   var toolbar=Bar();Add(toolbar,"Mở công cụ",()=>{OpenTool();return Task.FromResult(0);});
   Add(toolbar,"Tài liệu truy vết",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SCAN-GUIDE.md")){UseShellExecute=true});return Task.FromResult(0);});
   var note=Theme.Note("Chỉ mở công cụ khi bạn chọn. Công cụ thiếu trên phiên bản Windows hiện tại sẽ được đánh dấu. SFC có thể sửa file hệ thống và yêu cầu quyền quản trị.",NoteKind.Info);
   toolsTab.Controls.Add(toolList);toolsTab.Controls.Add(note);toolsTab.Controls.Add(toolbar);FillTools();
   toolList.DoubleClick+=async(s,e)=>await Guard(()=>{OpenTool();return Task.FromResult(0);});
   tabs.SelectedIndexChanged+=async(s,e)=>{if(tabs.SelectedTab==autorunTab&&autorunList.Items.Count==0&&!busy)await Guard(async()=>await LoadAutoruns());};
   cancelScan=Theme.Button("Dừng quét sâu",ButtonStyle.Secondary);cancelScan.AutoSize=false;cancelScan.Dock=DockStyle.Right;cancelScan.Width=150;cancelScan.Margin=new Padding(0);cancelScan.Visible=false;
   cancelScan.Click+=(s,e)=>{if(scanCancellation!=null)scanCancellation.Cancel();};status.Controls.Add(cancelScan);
   deepMode.Font=Theme.Body;
  }
  void FillTools(){toolList.Items.Clear();int index=0;foreach(var t in WindowsTools.Catalog()){var row=new ListViewItem(new[]{t.Name,t.Available?"Có sẵn":"Không có",t.Description}){Tag=t,ForeColor=t.Available?Theme.Text:Theme.Muted,ToolTipText=t.Description+(t.Available?"":"\r\nCông cụ không có trên phiên bản Windows này.")};Theme.StripeRow(row,index++);toolList.Items.Add(row);}}
  void OpenTool(){if(toolList.SelectedItems.Count==0)throw new IOException("Chọn công cụ muốn mở.");var t=(WindowsTool)toolList.SelectedItems[0].Tag;if(t.Admin&&!Confirm(t.Description+"\r\nTiếp tục mở công cụ với quyền quản trị?"))return;WindowsTools.Launch(t);}
  AutorunEntry CurrentAutorun(){if(autorunList.SelectedItems.Count==0)throw new IOException("Chọn một mục khởi động.");return (AutorunEntry)autorunList.SelectedItems[0].Tag;}
  void ShowAutoruns(List<AutorunEntry> entries){
   autorunList.BeginUpdate();autorunList.Items.Clear();startupIcons.Images.Clear();var imageHandle=startupIcons.Handle;int index=0;
   foreach(var a in entries){string target=Advanced.CommandExe(a.Command);if(target==""&&Advanced.LocalPath(a.Command))target=a.Command;using(var bitmap=Presentation.AppIcon(new AppEntry{DisplayIcon=target}))startupIcons.Images.Add(bitmap);var row=new ListViewItem(new[]{a.Name,a.State,a.Command,a.Source}){Tag=a,ImageIndex=index,ToolTipText=a.Command+"\r\n"+a.Source};Theme.StripeRow(row,index++);autorunList.Items.Add(row);}
   autorunList.EndUpdate();
   if(entries.Count==0)Theme.SetOverlay(autorunOverlay,"Không tìm thấy mục Run/RunOnce hoặc shortcut Startup.\r\nMục toàn máy có thể cần Run as administrator với cùng tài khoản Windows.",NoteKind.Info);
   else Theme.SetOverlay(autorunOverlay,null,NoteKind.Info);
  }
  async Task LoadAutoruns(){Log("Đang đọc mục khởi động…");var entries=await Task.Run(()=>Advanced.Autoruns());ShowAutoruns(entries);Log("Đã đọc "+entries.Count+" mục khởi động và bản đã tắt. Chưa gồm service/driver/tác vụ lịch.");}
  async Task DisableAutorun(){var a=CurrentAutorun();if(a.Item==null)throw new IOException("Mục này đã nằm trong kho; dùng Bật lại từ kho.");if(!Confirm("Tắt mục khởi động này và lưu bản khôi phục?\r\n\r\n"+a.Name+"\r\n"+a.Command+"\r\n\r\nKhông dừng ứng dụng đang chạy. Thay đổi áp dụng cho lần đăng nhập sau."))return;await Task.Run(()=>Advanced.Store(a.Item,true));await LoadAutoruns();LoadBackups();Log("Đã tắt và sao lưu: "+a.Name);}
  async Task EnableAutorun(){var a=CurrentAutorun();if(a.Saved==null)throw new IOException("Mục này vẫn có đăng ký. Nếu bị Windows tắt, hãy dùng Startup của Windows.");if(!Confirm("Khôi phục mục khởi động "+a.Name+"?\r\nKhông ghi đè mục đã tồn tại. Windows vẫn có thể chặn theo cài đặt Startup riêng."))return;await Task.Run(()=>Engine.Restore(a.Saved));await LoadAutoruns();LoadBackups();Log("Đã khôi phục đăng ký khởi động: "+a.Name);}
  void ExportAutoruns(){string p=SavePath("TweekPro-autoruns.csv");if(p==null)return;var lines=new List<string>{"Name,State,Command,Location"};lines.AddRange(autorunList.Items.Cast<ListViewItem>().Select(i=>(AutorunEntry)i.Tag).Select(a=>String.Join(",",new[]{a.Name,a.State,a.Command,a.Source}.Select(Engine.Csv))));File.WriteAllLines(p,lines,new System.Text.UTF8Encoding(true));Log("Đã xuất mục khởi động.");}
  async Task DeepHistory(){
   if(history.Count==0)throw new IOException("Chọn ứng dụng ở tab Ứng dụng rồi Quét mục đang xem để ghi nhận trước; sau đó dùng Quét siêu sâu.");
   await ScanDeepEntries(history.ToArray(),false);
  }
  async Task ScanDeepEntries(IEnumerable<AppEntry> entries,bool append){
   scanCancellation=new CancellationTokenSource();cancelScan.Visible=true;var token=scanCancellation.Token;
   var all=append?candidates.ToList():new List<Candidate>();int warningCount=0;
   try{
    foreach(var app in entries){Log("Quét sâu: "+app.Name+" — có thể dừng bằng nút Dừng quét sâu.");
     var result=await Task.Run(()=>Advanced.DeepScan(app,token));all.AddRange(result.Items);
     warningCount+=result.Notes.Count;foreach(string note in result.Notes.Take(30))Log(app.Name+": "+note);if(result.Notes.Count>30)Log("Còn "+(result.Notes.Count-30)+" ghi chú được rút gọn trong nhật ký.");
     PresentCandidates(all);tabs.SelectedIndex=1;
    }
    Log("Quét sâu xong: "+candidates.Count+" mục; "+warningCount+" ghi chú về phạm vi/đường dẫn bỏ qua. Xem Nhật ký; không bảo đảm mọi dấu vết đã được tìm thấy.");
   }catch(OperationCanceledException){PresentCandidates(all);Log("Đã dừng quét. Giữ kết quả các ứng dụng đã quét xong; ứng dụng đang quét chưa được cộng vào.");}
   finally{cancelScan.Visible=false;scanCancellation.Dispose();scanCancellation=null;}
  }
  public void PreviewAdvanced(string mode){if(mode=="autorun"){ShowAutoruns(Advanced.Autoruns());tabs.SelectedTab=autorunTab;}else if(mode=="junk")tabs.SelectedTab=junkTab;else if(mode=="network")tabs.SelectedTab=netTab;else tabs.SelectedTab=toolsTab;}
 }
}
