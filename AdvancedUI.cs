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
  ListView autorunList=new SmoothListView(),toolList=new SmoothListView();ImageList startupIcons=new ImageList(),toolIcons=new ImageList();
  Label autorunNote,autorunOverlay;Button cancelScan;TabPage autorunTab,toolsTab;bool autorunGuard,autorunLoading;
  void BuildAdvancedTabs(){
   autorunTab=new TabPage(Core.L.T("Khởi động"));toolsTab=new TabPage(Core.L.T("Công cụ"));tabs.TabPages.Add(autorunTab);tabs.TabPages.Add(toolsTab);
   startupIcons.ColorDepth=ColorDepth.Depth32Bit;startupIcons.ImageSize=new Size(24,24);autorunList.SmallImageList=startupIcons;
   toolIcons.ColorDepth=ColorDepth.Depth32Bit;toolIcons.ImageSize=new Size(24,24);toolList.SmallImageList=toolIcons;
   FormClosed+=(s,e)=>{startupIcons.Dispose();toolIcons.Dispose();};
   SetupList(autorunList,new[]{"Tên khởi động","Trạng thái","Windows","Tác động","Chữ ký","Kích cỡ","Lệnh / đích","Vị trí"},new[]{200,150,110,130,150,80,340,250},true);
   autorunList.ShowGroups=true;
   autorunList.ItemCheck+=(s,e)=>{
    if(autorunGuard||e.Index<0||e.Index>=autorunList.Items.Count)return;
    var entry=autorunList.Items[e.Index].Tag as AutorunEntry;if(entry==null)return;
    bool want=e.NewValue==CheckState.Checked;
    if(want==AutorunChecked(entry))return;
    e.NewValue=e.CurrentValue;
    BeginInvoke((Action)(async()=>await Guard(async()=>await ApplyAutorunCheck(entry,want))));
   };
   var autorunHost=Theme.ListHost(autorunList,out autorunOverlay);
   var bar=Bar();
   Add(bar,"Tải danh sách",async()=>await LoadAutoruns());
   Add(bar,"Tắt (sao lưu)",async()=>await DisableAutorun(),ButtonStyle.Primary);
   Add(bar,"Bật lại từ kho",async()=>await EnableAutorun());
   Add(bar,"Sao chép vị trí",()=>{var a=CurrentAutorun();Clipboard.SetText(a.Item!=null?Presentation.CandidatePath(a.Item):a.Saved.Original);return Task.FromResult(0);});
   Add(bar,"Startup của Windows",()=>{WindowsTools.Launch(WindowsTools.Catalog().First(t=>t.Name=="Startup Apps"));return Task.FromResult(0);});
   Add(bar,"Xuất CSV",()=>{ExportAutoruns();return Task.FromResult(0);});
   autorunNote=Theme.Note("Run, shortcut Startup, ứng dụng Windows (Copilot, Terminal, Teams…) và dịch vụ tự chạy cùng máy. Bỏ dấu tích để tắt — trạng thái cũ vào Kho, bật lại được. Dịch vụ cốt lõi không tắt được và dịch vụ đang chạy không bị dừng. Mục đích không tồn tại được tô cam.",NoteKind.Info);
   autorunTab.Controls.Add(autorunHost);autorunTab.Controls.Add(autorunNote);autorunTab.Controls.Add(bar);
   Theme.SetOverlay(autorunOverlay,"Chưa tải mục khởi động.\r\nBấm Tải danh sách hoặc mở tab này để đọc Run, shortcut Startup, ứng dụng Windows và dịch vụ tự chạy.",NoteKind.Info);
   SetupList(toolList,new[]{"Công cụ","Dòng lệnh","Tình trạng","Công dụng"},new[]{220,420,120,360},false);
   var toolbar=Bar();Add(toolbar,"Mở công cụ",()=>{OpenTool();return Task.FromResult(0);},ButtonStyle.Primary);
   Add(toolbar,"Kiểm tra cập nhật",async()=>await CheckForUpdates());
   Add(toolbar,"Thêm công cụ…",()=>{AddUserTool();return Task.FromResult(0);});
   AddFeedbackButton(toolbar);
   Add(toolbar,"Tài liệu truy vết",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SCAN-GUIDE.md")){UseShellExecute=true});return Task.FromResult(0);});
   var note=Theme.Note("Mỗi mục dùng icon hệ thống của tệp .exe/.cpl/.msc tương ứng. Chỉ mở công cụ khi bạn chọn. Công cụ thiếu trên phiên bản Windows hiện tại sẽ được đánh dấu. SFC/chkdsk có thể sửa hệ thống và yêu cầu quyền quản trị. Kiểm tra cập nhật hỏi GitHub Releases (tag v*), không theo từng push nhánh.",NoteKind.Info);
   toolsTab.Controls.Add(toolList);toolsTab.Controls.Add(note);toolsTab.Controls.Add(toolbar);FillTools();
   toolList.DoubleClick+=async(s,e)=>await Guard(()=>{OpenTool();return Task.FromResult(0);});
   tabs.SelectedIndexChanged+=async(s,e)=>{if(tabs.SelectedTab!=autorunTab||autorunList.Items.Count>0||autorunLoading)return;autorunLoading=true;Theme.SetOverlay(autorunOverlay,"Đang đọc mục khởi động…",NoteKind.Info);try{await WhenIdle(async()=>{if(autorunList.Items.Count==0)await LoadAutoruns();});}finally{autorunLoading=false;if(autorunList.Items.Count==0)Theme.SetOverlay(autorunOverlay,"Chưa tải mục khởi động.\r\nBấm Tải danh sách hoặc mở tab này để đọc Run, shortcut Startup, ứng dụng Windows và dịch vụ tự chạy.",NoteKind.Info);}};
   cancelScan=Theme.Button("Dừng quét sâu",ButtonStyle.Secondary);cancelScan.AutoSize=false;cancelScan.Dock=DockStyle.Right;cancelScan.Width=150;cancelScan.Margin=new Padding(0);cancelScan.Visible=false;
   cancelScan.Click+=(s,e)=>{if(scanCancellation!=null)scanCancellation.Cancel();};status.Controls.Add(cancelScan);
   deepMode.Font=Theme.Body;
  }
  void FillTools(){
   toolList.BeginUpdate();toolList.Items.Clear();toolIcons.Images.Clear();var imageHandle=toolIcons.Handle;int index=0;int iconSize=toolIcons.ImageSize.Width;
   foreach(var t in WindowsTools.Catalog().Concat(UserTools.Load(Core.Paths.UserToolsFile))){
    using(var bitmap=WindowsTools.Icon(t,iconSize))toolIcons.Images.Add(bitmap);
    var row=new ListViewItem(new[]{t.Name,t.CommandLine,Core.L.T(t.Available?"Có sẵn":"Không có sẵn"),Core.L.T(t.Description)}){Tag=t,ImageIndex=index,ForeColor=t.Available?Theme.Text:Theme.Muted,ToolTipText=t.CommandLine+"\r\n"+Core.L.T(t.Description)+(t.Available?"":Core.L.T("\r\nCông cụ không có trên phiên bản Windows này."))};
    Theme.StripeRow(row,index++);toolList.Items.Add(row);
   }
   toolList.EndUpdate();
  }
  void OpenTool(){if(toolList.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn công cụ muốn mở."));var t=(WindowsTool)toolList.SelectedItems[0].Tag;if(t.Custom&&!Confirm(Core.L.F("Mở công cụ do bạn thêm?\r\n{0}\r\n{1}",t.Name,t.CommandLine)))return;if(t.Admin&&!Confirm(Core.L.T(t.Description)+Core.L.T("\r\nTiếp tục mở công cụ với quyền quản trị?")))return;WindowsTools.Launch(t);}
  void AddUserTool(){
   using(var dialog=new OpenFileDialog{Filter="Program|*.exe;*.msc;*.cpl"}){
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    string why=UserTools.Refuse(dialog.FileName);
    if(why!=null)throw new IOException(Core.L.T("Chỉ nhận tệp .exe, .msc hoặc .cpl trên máy này. Không nhận cmd, PowerShell hay script."));
    string name=Path.GetFileNameWithoutExtension(dialog.FileName);
    var list=UserTools.Load(Core.Paths.UserToolsFile);
    list.Add(new WindowsTool{Name=name,File=dialog.FileName,Description="Công cụ do bạn thêm.",Custom=true});
    UserTools.Save(Core.Paths.UserToolsFile,list);FillTools();
   }
  }
  AutorunEntry CurrentAutorun(){if(autorunList.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một mục khởi động."));return (AutorunEntry)autorunList.SelectedItems[0].Tag;}
  static int AutorunRank(AutorunEntry a){if(a.Saved!=null)return 3;if(a.Item!=null&&a.Item.Kind=="Service")return 2;if(a.Item!=null&&a.Item.Kind==Startup.StartupSources.BackupKind)return 1;return 0;}
  void ShowAutoruns(List<AutorunEntry> entries){
   entries.Sort((a,b)=>{int r=AutorunRank(a).CompareTo(AutorunRank(b));return r!=0?r:StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name);});
   autorunGuard=true;
   try{
    autorunList.BeginUpdate();autorunList.Items.Clear();autorunList.Groups.Clear();startupIcons.Images.Clear();var imageHandle=startupIcons.Handle;int index=0;
    foreach(var a in entries){
     var i=a.Insight??new Startup.StartupInsight();string target=i.Target!=""?i.Target:Advanced.CommandExe(a.Command);if(target==""&&Advanced.LocalPath(a.Command))target=a.Command;
     using(var bitmap=Presentation.AppIcon(new AppEntry{DisplayIcon=target}))startupIcons.Images.Add(bitmap);
     string windows=a.Item==null?"":i.Approval.Enabled?Core.L.T("Đang bật"):i.Approval.DisabledAt.HasValue?Core.L.F("Đã tắt {0}",i.Approval.DisabledAt.Value.ToString("dd/MM/yyyy")):Core.L.T("Đã tắt");
     string impact=a.Item==null?"":Core.L.T(Startup.StartupRating.ImpactLabel(i.Impact)),signer=i.Signed?i.Signer:i.TargetExists?Core.L.T(i.Signer):"",size=i.Bytes>=0?Presentation.SizeLabel(i.Bytes):"";
     string tip=a.Command+"\r\n"+Core.L.T(a.Source)+(i.Target==""?"":"\r\n"+Core.L.T("Đích: ")+i.Target)+(i.Flags.Count==0?"":"\r\n"+Core.L.T("Cảnh báo: ")+String.Join("; ",i.Flags.Select(Core.L.T)));
     string group=a.Saved!=null?"Kho Tweek Pro":a.Item!=null&&a.Item.Kind=="Service"?Startup.StartupSources.GroupServices:a.Item!=null&&a.Item.Kind==Startup.StartupSources.BackupKind?Startup.StartupSources.GroupApps:Startup.StartupSources.GroupRun;
     var row=new ListViewItem(new[]{a.Name,Core.L.T(a.State),windows,impact,signer,size,a.Command,Core.L.T(a.Source)}){Tag=a,ImageIndex=index,ToolTipText=tip,Group=AutorunGroup(group),Checked=AutorunChecked(a)};
     if(i.Broken||i.Flags.Count>0)row.ForeColor=Theme.Warning;else if(!row.Checked)row.ForeColor=Theme.Muted;
     Theme.StripeRow(row,index++);autorunList.Items.Add(row);
    }
    autorunList.EndUpdate();
   }finally{autorunGuard=false;}
   if(entries.Count==0)Theme.SetOverlay(autorunOverlay,"Không tìm thấy mục khởi động.\r\nMục toàn máy có thể cần quyền quản trị.",NoteKind.Info);
   else Theme.SetOverlay(autorunOverlay,null,NoteKind.Info);
  }
  ListViewGroup AutorunGroup(string key){foreach(ListViewGroup g in autorunList.Groups)if(g.Name==key)return g;var created=new ListViewGroup(key,Core.L.T(key));autorunList.Groups.Add(created);return created;}
  static bool AutorunChecked(AutorunEntry a){if(a.Saved!=null||a.Item==null)return false;if(a.Item.Kind=="Service")return true;var i=a.Insight;return i==null||i.Approval.Enabled;}
  static List<AutorunEntry> AnnotatedAutoruns(){var entries=Advanced.Autoruns();Startup.StartupInspector.Annotate(entries);return entries;}
  async Task LoadAutoruns(){
   Log(Core.L.T("Đang đọc mục khởi động…"));var entries=await Task.Run(()=>AnnotatedAutoruns());ShowAutoruns(entries);
   var live=entries.Where(a=>a.Item!=null&&a.Insight!=null).ToList();
   Log(Core.L.F("Đã đọc {0} mục khởi động: {1} đang bật, {2} đã tắt, {3} đích không tồn tại ({4} ứng dụng Windows, {5} dịch vụ tự chạy).",entries.Count,live.Count(a=>a.Insight.RunsAtLogon),live.Count(a=>!a.Insight.Approval.Enabled),live.Count(a=>a.Insight.Broken),live.Count(a=>a.Item.Kind==Startup.StartupSources.BackupKind),live.Count(a=>a.Item.Kind=="Service")));
  }
  async Task DisableAutorun(){await ApplyAutorunCheck(CurrentAutorun(),false);}
  async Task EnableAutorun(){await ApplyAutorunCheck(CurrentAutorun(),true);}
  async Task ApplyAutorunCheck(AutorunEntry a,bool enable){
   if(enable){
    if(a.Saved!=null){if(!Confirm(Core.L.F("Bật lại {0} từ Kho khôi phục?",a.Name)))return;await Task.Run(()=>Engine.Restore(a.Saved));}
    else if(a.Item!=null&&a.Item.Kind==Startup.StartupSources.BackupKind){if(!Confirm(Core.L.F("Bật khởi động của ứng dụng Windows {0}?\r\nÁp dụng từ lần đăng nhập sau. Trạng thái cũ được lưu vào Kho.",a.Name)))return;await Task.Run(()=>Startup.StartupSources.EnableTask(a.Item));}
    else if(a.Item!=null&&a.Insight!=null&&!a.Insight.Approval.Enabled&&Startup.StartupInspector.ApprovedSubKey(a.Item)!=null){if(!Confirm(Core.L.F("Bật lại {0}? Windows đang tắt mục này.\r\nTweek Pro chỉ bật cờ StartupApproved; lệnh Run hoặc shortcut giữ nguyên. Cờ cũ được lưu vào Kho.",a.Name)))return;await Task.Run(()=>Startup.StartupInspector.SetEnabled(a.Item));}
    else throw new IOException(Core.L.T("Mục này không có công tắc bật/tắt của Windows. Hãy tắt để đưa vào Kho, rồi bật lại từ Kho."));
   }else{
    if(a.Item==null)throw new IOException(Core.L.T("Mục này đã nằm trong kho; dùng Bật lại từ kho."));
    if(a.Item.Kind=="Service"){if(Network.ProcessControl.IsCoreService(a.Item.ValueName))throw new IOException(Core.L.F("{0} là dịch vụ cốt lõi của Windows — không tắt khởi động cùng máy.",a.Name));if(!Confirm(Core.L.F("Tắt khởi động cùng máy của dịch vụ {0}?\r\n\r\nDịch vụ đang chạy không bị dừng. Lần khởi động sau nó sẽ không tự chạy. Kiểu khởi động cũ được lưu vào Kho.",a.Name)))return;await Task.Run(()=>Network.ProcessControl.DisableAutostart(new Network.HostedService{Name=a.Item.ValueName,DisplayName=a.Name,StartMode=a.Item.Reason,PathName=a.Command}));}
    else if(a.Item.Kind==Startup.StartupSources.BackupKind){if(!Confirm(Core.L.F("Tắt khởi động của ứng dụng Windows {0}?\r\nÁp dụng từ lần đăng nhập sau. Trạng thái cũ được lưu vào Kho.",a.Name)))return;await Task.Run(()=>Startup.StartupSources.DisableTask(a.Item));}
    else {if(!Confirm(Core.L.F("Tắt mục khởi động này và lưu bản khôi phục?\r\n\r\n{0}\r\n{1}\r\n\r\nKhông dừng ứng dụng đang chạy. Thay đổi áp dụng cho lần đăng nhập sau.",a.Name,a.Command)))return;await Task.Run(()=>Advanced.Store(a.Item,true));}
   }
   await LoadAutoruns();LoadBackups();Log(enable?Core.L.T("Đã khôi phục đăng ký khởi động: ")+a.Name:Core.L.T("Đã tắt và sao lưu: ")+a.Name);
  }
  void ExportAutoruns(){string p=SavePath("TweekPro-autoruns.csv");if(p==null)return;var lines=new List<string>{"Name,State,WindowsEnabled,Impact,Signer,Bytes,Target,Flags,Command,Location"};lines.AddRange(autorunList.Items.Cast<ListViewItem>().Select(i=>(AutorunEntry)i.Tag).Select(a=>{var s=a.Insight??new Startup.StartupInsight();return String.Join(",",new[]{a.Name,a.State,a.Item==null?"":s.Approval.Enabled?"yes":"no",a.Item==null?"":s.Impact.ToString(),s.Signer,s.Bytes<0?"":s.Bytes.ToString(),s.Target,String.Join("; ",s.Flags),a.Command,a.Source}.Select(Engine.Csv));}));File.WriteAllLines(p,lines,new System.Text.UTF8Encoding(true));Log(Core.L.T("Đã xuất mục khởi động."));}
  async Task DeepHistory(){
   if(history.Count==0)throw new IOException(Core.L.T("Chọn ứng dụng ở tab Ứng dụng rồi Quét mục đang xem để ghi nhận trước; sau đó dùng Quét siêu sâu."));
   await ScanDeepEntries(history.ToArray(),false);
  }
  async Task ScanDeepEntries(IEnumerable<AppEntry> entries,bool append){
   scanCancellation=new CancellationTokenSource();cancelScan.Visible=true;var token=scanCancellation.Token;
   var all=append?candidates.ToList():new List<Candidate>();int warningCount=0;
   try{
    foreach(var app in entries){Log(Core.L.F("Quét sâu: {0} — có thể dừng bằng nút Dừng quét sâu.",app.Name));
     var result=await Task.Run(()=>Advanced.DeepScan(app,token));all.AddRange(result.Items);
     warningCount+=result.Notes.Count;foreach(string note in result.Notes.Take(30))Log(app.Name+": "+note);if(result.Notes.Count>30)Log(Core.L.F("Còn {0} ghi chú được rút gọn trong nhật ký.",result.Notes.Count-30));
     PresentCandidates(all);tabs.SelectedIndex=1;
    }
    Log(Core.L.F("Quét sâu xong: {0} mục; {1} ghi chú về phạm vi/đường dẫn bỏ qua. Xem Nhật ký; không bảo đảm mọi dấu vết đã được tìm thấy.",candidates.Count,warningCount));
   }catch(OperationCanceledException){PresentCandidates(all);Log(Core.L.T("Đã dừng quét. Giữ kết quả các ứng dụng đã quét xong; ứng dụng đang quét chưa được cộng vào."));}
   finally{cancelScan.Visible=false;scanCancellation.Dispose();scanCancellation=null;}
  }
  public void PreviewAdvanced(string mode){if(mode=="autorun"){ShowAutoruns(AnnotatedAutoruns());tabs.SelectedTab=autorunTab;}else if(mode=="junk")tabs.SelectedTab=junkTab;else if(mode=="network")PreviewNetwork();else if(mode=="services")PreviewServices();else if(mode=="ai")PreviewAi();else if(mode=="health")PreviewHealth();else if(mode=="store")PreviewStoreApps();else tabs.SelectedTab=toolsTab;}
 }
}
