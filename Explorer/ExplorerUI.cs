using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Explorer;

namespace TweekPro {
 public partial class MainForm {
  TabPage explorerTab;ListView tweakList=new SmoothListView(),fileList=new SmoothListView();Label tweakOverlay,tweakSummary,fileOverlay,fileStage;Panel explorerTweakPane,explorerBrowserPane;Button explorerBrowserButton,explorerTweakButton;
  TextBox filePathBox=new TextBox();PictureBox fileIconBox;CheckBox fileHashOption=new CheckBox();Button tweakApplyButton;
  List<TweakState> tweakStates=new List<TweakState>();FileDetails currentFile;bool tweakLoading;CancellationTokenSource fileCancellation;
  ListView browserList=new SmoothListView();ImageList browserIcons=new ImageList();Label browserOverlay,browserSummary;CheckBox browserOnlyHidden=new CheckBox();FolderListing browserListing;string browserPath="";Font browserHiddenFont;

  /// <summary>Builds the Explorer tab: File Explorer display switches (vault-backed) on top, the read-only file inspector below.</summary>
  void BuildExplorerTab(){
   var tab=explorerTab=new TabPage(Core.L.T("Explorer"));
   SetupList(tweakList,new[]{"Tùy chỉnh","Trạng thái","Mô tả"},new[]{330,140,760},true,true);
   tweakList.ItemChecked+=(s,e)=>{if(!tweakLoading&&tweakList.IsHandleCreated&&tweakList.Items.Count==tweakStates.Count)UpdateTweakSummary();};
   var tweakHost=Theme.ListHost(tweakList,out tweakOverlay);
   var tweakBar=Bar();
   Add(tweakBar,"Áp dụng thay đổi",async()=>await ApplyTweaks(),ButtonStyle.Primary);tweakApplyButton=actions[actions.Count-1];
   Add(tweakBar,"Đọc lại",()=>{ReloadTweaks();return Task.FromResult(0);});
   Add(tweakBar,"Mặc định Windows",()=>{SetTweakChecks(t=>t.Tweak.DefaultOn);return Task.FromResult(0);});
   Add(tweakBar,"Khuyến nghị an toàn",()=>{SetTweakChecks(t=>t.Tweak.Id=="show-ext"||t.Tweak.Id=="status-bar"||t.Tweak.Id=="infotip"||t.Tweak.Id=="ntfs-color"||(t.Enabled&&t.Tweak.Id!="sync-ads"&&t.Tweak.Id!="show-superhidden"));return Task.FromResult(0);});
   Add(tweakBar,"Khởi động lại Explorer",async()=>await RestartExplorerPrompt(),ButtonStyle.Danger);
   Add(tweakBar,"Mở Tùy chọn thư mục",()=>{Process.Start("control.exe","folders");return Task.FromResult(0);});
   var tweakNote=Theme.Note("Tích hoặc bỏ tích rồi bấm Áp dụng thay đổi. Mỗi giá trị được ghi vào HKCU của tài khoản hiện tại và giá trị cũ lưu vào Kho khôi phục (loại Explorer) để bật lại bằng một nút. Cửa sổ Explorer đang mở tự làm mới; mục thanh tác vụ cần Khởi động lại Explorer.",NoteKind.Info);
   tweakSummary=new Label{Dock=DockStyle.Bottom,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true};Theme.BorderTop(tweakSummary);
   var top=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Surface};
   top.Controls.Add(tweakHost);top.Controls.Add(tweakSummary);top.Controls.Add(tweakNote);top.Controls.Add(tweakBar);

   SetupList(browserList,new[]{"Tên","Đuôi","Mô tả (loại)","Dung lượng","Sửa lần cuối","Thuộc tính"},new[]{300,70,220,100,140,160},false,false);
   browserList.SmallImageList=browserIcons;browserIcons.ColorDepth=ColorDepth.Depth32Bit;browserIcons.ImageSize=new Size(16,16);var browserIconsHandle=browserIcons.Handle;
   browserList.DoubleClick+=async(s,e)=>{var entry=SelectedEntry();if(entry==null)return;if(entry.IsDirectory)await Guard(async()=>await BrowseFolder(entry.Path));else await Guard(async()=>await InspectPath(entry.Path,true));};
   browserList.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;var entry=SelectedEntry();if(entry!=null)await Guard(async()=>{if(entry.IsDirectory)await BrowseFolder(entry.Path);else await InspectPath(entry.Path,true);});}else if(e.KeyCode==Keys.Back){e.SuppressKeyPress=true;await Guard(async()=>await BrowseFolder(FileInspector.ParentOf(browserPath)));}};
   browserList.SelectedIndexChanged+=async(s,e)=>{var entry=SelectedEntry();if(entry==null||entry.IsDirectory||busy)return;try{await InspectPath(entry.Path,false);}catch(Exception error){fileStage.Visible=true;fileStage.Text=error.Message;fileStage.ForeColor=Theme.Danger;}};
   browserOnlyHidden.Text=Core.L.T("Chỉ hiện mục ẩn / hệ thống");browserOnlyHidden.AutoSize=true;browserOnlyHidden.Margin=new Padding(8,6,0,0);browserOnlyHidden.ForeColor=Theme.Text;browserOnlyHidden.Font=Theme.Body;browserOnlyHidden.CheckedChanged+=(s,e)=>RenderBrowser();
   var browserHost=Theme.ListHost(browserList,out browserOverlay);
   browserSummary=new Label{Dock=DockStyle.Bottom,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true};Theme.BorderTop(browserSummary);
   var browserPanel=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Surface};browserPanel.Controls.Add(browserHost);browserPanel.Controls.Add(browserSummary);

   SetupList(fileList,new[]{"Thuộc tính","Giá trị"},new[]{170,600},false,true);
   fileList.DoubleClick+=(s,e)=>{if(fileList.SelectedItems.Count>0)try{Clipboard.SetText(fileList.SelectedItems[0].SubItems[1].Text);Log(Core.L.T("Đã sao chép giá trị."));}catch(Exception){}};
   var fileHost=Theme.ListHost(fileList,out fileOverlay);
   var fileBar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(10,6,0,0),BackColor=Theme.Surface};Theme.BorderBottom(fileBar);
   Add(fileBar,"Xem chi tiết + băm",async()=>await InspectCurrentFile(),ButtonStyle.Primary);
   Add(fileBar,"Sao chép báo cáo",()=>{if(currentFile==null)throw new IOException(Core.L.T("Chưa có tệp nào được xem."));Clipboard.SetText(FileInspector.Report(currentFile));Log(Core.L.T("Đã sao chép báo cáo chi tiết tệp."));return Task.FromResult(0);});
   Add(fileBar,"Mở trong Explorer",()=>{var entry=SelectedEntry();string target=entry!=null?entry.Path:currentFile!=null?currentFile.Path:browserPath;if(String.IsNullOrEmpty(target))Process.Start("explorer.exe");else ExplorerShell.Reveal(target);return Task.FromResult(0);});
   Add(fileBar,"Thuộc tính Windows",()=>{var entry=SelectedEntry();string target=entry!=null?entry.Path:currentFile!=null?currentFile.Path:null;if(target==null)throw new IOException(Core.L.T("Chưa có tệp nào được xem."));ExplorerShell.ShowProperties(Handle,target);return Task.FromResult(0);});
   browserOnlyHidden.Dock=DockStyle.Right;browserOnlyHidden.AutoSize=false;browserOnlyHidden.Width=230;browserOnlyHidden.Margin=Padding.Empty;browserOnlyHidden.Padding=new Padding(12,0,0,0);
   var pathPanel=new Panel{Dock=DockStyle.Top,Height=48,Padding=new Padding(16,8,16,8),BackColor=Theme.Surface};Theme.BorderBottom(pathPanel);
   fileIconBox=new PictureBox{Dock=DockStyle.Left,Width=40,SizeMode=PictureBoxSizeMode.CenterImage};
   Func<string,Func<Task>,Button> nav=(text,action)=>{var b=Theme.Button(text,ButtonStyle.Secondary);b.Dock=DockStyle.Left;b.AutoSize=false;b.Width=TextRenderer.MeasureText(b.Text,b.Font).Width+26;b.Margin=Padding.Empty;b.Click+=async(s,e)=>await Guard(action);actions.Add(b);return b;};
   var upButton=nav("▲ Lên",async()=>await BrowseFolder(FileInspector.ParentOf(browserPath)));var pcButton=nav("This PC",async()=>await BrowseFolder(""));var homeButton=nav("Người dùng",async()=>await BrowseFolder(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));var pickButton=nav("Chọn tệp…",async()=>await PickFile());
   var navGap=new Panel{Dock=DockStyle.Left,Width=10};
   filePathBox.Dock=DockStyle.Fill;filePathBox.Font=Theme.Body;filePathBox.BorderStyle=BorderStyle.FixedSingle;
   filePathBox.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Guard(async()=>await InspectPath(filePathBox.Text,true));}};
   pathPanel.Controls.Add(filePathBox);pathPanel.Controls.Add(browserOnlyHidden);pathPanel.Controls.Add(fileIconBox);pathPanel.Controls.Add(navGap);pathPanel.Controls.Add(pickButton);pathPanel.Controls.Add(homeButton);pathPanel.Controls.Add(pcButton);pathPanel.Controls.Add(upButton);
   fileStage=new Label{Dock=DockStyle.Top,Height=28,Padding=new Padding(12,0,12,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};Theme.BorderBottom(fileStage);
   var browserSplit=new SplitContainer{Size=new Size(1200,400),Dock=DockStyle.Fill,Orientation=Orientation.Vertical,SplitterWidth=6,BackColor=Theme.Border,Panel1MinSize=200,Panel2MinSize=200};
   browserSplit.Panel1.Controls.Add(browserPanel);browserSplit.Panel2.Controls.Add(fileHost);browserSplit.Panel2.Controls.Add(fileStage);browserSplit.Panel2.Controls.Add(fileBar);
   double browserRatio=0.55;bool browserResizing=false,browserPlaced=false;browserSplit.SplitterMoved+=(s,e)=>{if(browserPlaced&&!browserResizing&&browserSplit.Width>0)browserRatio=(double)browserSplit.SplitterDistance/browserSplit.Width;};
   browserSplit.SizeChanged+=(s,e)=>{if(browserSplit.Width<450)return;browserResizing=true;try{browserSplit.SplitterDistance=(int)(browserSplit.Width*browserRatio);browserPlaced=true;}catch(InvalidOperationException){}finally{browserResizing=false;}};
   var bottom=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Surface};
   bottom.Controls.Add(browserSplit);bottom.Controls.Add(pathPanel);
   foreach(Control c in new Control[]{bottom,browserList,browserHost,browserOverlay,fileList,fileHost,fileOverlay,filePathBox}){c.AllowDrop=true;c.DragEnter+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};c.DragDrop+=async(s,e)=>{var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length>0)await Guard(async()=>await InspectPath(files[0],true));};}

   explorerTweakPane=top;explorerBrowserPane=bottom;
   var switcher=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Padding=new Padding(16,10,8,0),BackColor=Theme.Canvas};
   explorerBrowserButton=Theme.Button("Duyệt tệp và chi tiết tệp",ButtonStyle.Primary);explorerTweakButton=Theme.Button("Tùy chỉnh File Explorer",ButtonStyle.Secondary);
   explorerBrowserButton.Click+=(s,e)=>ShowExplorerView(true);explorerTweakButton.Click+=(s,e)=>ShowExplorerView(false);
   var switchHint=new Label{Text=Core.L.T("Duyệt tệp hiện mọi mục ẩn / hệ thống với đuôi và mô tả; Tùy chỉnh bật/tắt các tùy chọn của File Explorer."),AutoSize=true,Margin=new Padding(12,10,0,0),ForeColor=Theme.Muted,Font=Theme.Small};
   switcher.Controls.Add(explorerBrowserButton);switcher.Controls.Add(explorerTweakButton);switcher.Controls.Add(switchHint);
   tab.Controls.Add(bottom);tab.Controls.Add(top);tab.Controls.Add(switcher);
   ShowExplorerView(true);
   Theme.SetOverlay(fileOverlay,"Chọn một tệp bên trái (hoặc kéo thả / dán đường dẫn) để xem loại thật, thuộc tính ẩn/hệ thống, chữ ký số, mã băm và cảnh báo giả dạng (hoadon.pdf.exe).",NoteKind.Info);
   Theme.SetOverlay(browserOverlay,"Đang mở thư mục…",NoteKind.Info);
   tabs.SelectedIndexChanged+=async(s,e)=>{if(tabs.SelectedTab==explorerTab&&browserListing==null)await Guard(async()=>await BrowseFolder(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));};
   ReloadTweaks();
  }

  /// <summary>Switches the Explorer tab between the file browser and the tweak list; both need the full tab height on a 1240×820 window.</summary>
  public void ShowExplorerView(bool browser){
   explorerBrowserPane.Visible=browser;explorerTweakPane.Visible=!browser;
   StyleToggle(explorerBrowserButton,browser);StyleToggle(explorerTweakButton,!browser);
  }
  static void StyleToggle(Button b,bool active){b.BackColor=active?Theme.Primary:Theme.Surface;b.ForeColor=active?Color.White:Theme.Text;b.FlatAppearance.BorderColor=active?Theme.Primary:Theme.Border;b.FlatAppearance.MouseOverBackColor=active?Theme.PrimaryDark:Theme.SoftButton;b.FlatAppearance.MouseDownBackColor=active?Theme.PrimaryDark:Theme.SoftButtonHover;}

  /// <summary>Re-reads every switch from HKCU and repaints the list; the check state mirrors the current value.</summary>
  void ReloadTweaks(){
   tweakStates=ExplorerTweaks.Read();
   tweakLoading=true;tweakList.BeginUpdate();tweakList.Items.Clear();tweakList.Groups.Clear();
   int index=0;
   foreach(var state in tweakStates){
    var t=state.Tweak;
    string status=state.Known?Core.L.T(state.Enabled?"Đang bật":"Đã tắt"):state.Missing?Core.L.T(state.Enabled?"Mặc định (bật)":"Mặc định (tắt)"):Core.L.T("Giá trị lạ");
    var row=new ListViewItem(new[]{Core.L.T(t.Name),status,(t.Caution?"⚠ ":"")+Core.L.T(t.Description)}){Tag=state,Checked=state.Enabled,ToolTipText="HKCU\\"+t.KeyPath+"\\"+t.ValueName+" = "+(state.Raw==null?Core.L.T("chưa có"):Convert.ToString(state.Raw))+"  •  "+Core.L.F("bật = {0}, tắt = {1}",t.OnValue,t.OffValue)};
    if(t.Caution)row.ForeColor=Theme.Warning;
    Theme.AssignGroup(tweakList,row,((int)t.Group).ToString(),ExplorerTweaks.GroupLabel(t.Group));
    Theme.StripeRow(row,index++);tweakList.Items.Add(row);
   }
   tweakList.EndUpdate();tweakLoading=false;
   Theme.SetOverlay(tweakOverlay,ExplorerTweaks.IsWindows?null:"Chỉ đọc được trên Windows; danh sách hiện giá trị mặc định.",NoteKind.Info);
   UpdateTweakSummary();
  }

  void SetTweakChecks(Func<TweakState,bool> desired){tweakLoading=true;tweakList.BeginUpdate();foreach(ListViewItem row in tweakList.Items)row.Checked=desired((TweakState)row.Tag);tweakList.EndUpdate();tweakLoading=false;UpdateTweakSummary();}

  List<TweakChange> PendingTweaks(){
   var pending=new List<TweakChange>();
   foreach(ListViewItem r in tweakList.Items){if(r==null)continue;var s=r.Tag as TweakState;if(s==null||s.Tweak==null)continue;if(r.Checked!=s.Enabled||!s.Known&&!s.Missing)pending.Add(new TweakChange{Tweak=s.Tweak,Enable=r.Checked});}
   return pending;
  }

  void UpdateTweakSummary(){
   if(tweakLoading||tweakSummary==null||tweakStates==null)return;
   var pending=PendingTweaks();int on=tweakStates.Count(s=>s.Enabled);
   tweakSummary.Text=Core.L.F("{0} tùy chỉnh  •  {1} đang bật  •  {2} thay đổi chờ áp dụng",tweakStates.Count,on,pending.Count);
   tweakSummary.ForeColor=pending.Count>0?Theme.Primary:Theme.Muted;
   if(tweakApplyButton!=null)tweakApplyButton.Text=pending.Count>0?Core.L.F("Áp dụng {0} thay đổi",pending.Count):Core.L.T("Áp dụng thay đổi");
  }

  /// <summary>Confirms the pending switches, writes each one through the vault and asks Explorer to refresh.</summary>
  async Task ApplyTweaks(){
   var pending=PendingTweaks();if(pending.Count==0)throw new IOException(Core.L.T("Chưa có thay đổi nào: tích hoặc bỏ tích một tùy chỉnh trước."));
   string list=String.Join("\r\n",pending.Select(c=>"• "+Core.L.T(c.Tweak.Name)+" → "+Core.L.T(c.Enable?"BẬT":"TẮT")));
   bool caution=pending.Any(c=>c.Enable&&c.Tweak.Caution);
   if(!Confirm(Core.L.F("Áp dụng {0} thay đổi cho File Explorer của tài khoản hiện tại?\r\n\r\n{1}\r\n\r\nGiá trị cũ được lưu vào Kho khôi phục.",pending.Count,list)+(caution?Core.L.T("\r\n\r\nLƯU Ý: hiện tệp hệ thống được bảo vệ làm các tệp Windows quan trọng lộ ra trong Explorer; đừng xóa hay đổi tên chúng."):"")))return;
   int ok=0;var errors=new List<string>();
   foreach(var c in pending){try{await Task.Run(()=>ExplorerTweaks.Apply(c.Tweak,c.Enable));ok++;Log(Core.L.F("Explorer: {0} → {1}.",Core.L.T(c.Tweak.Name),Core.L.T(c.Enable?"bật":"tắt")));}catch(Exception e){errors.Add(Core.L.T(c.Tweak.Name)+": "+e.Message);Log(Core.L.T("LỖI: ")+e.Message);}}
   ExplorerTweaks.RefreshExplorer();LoadBackups();ReloadTweaks();
   bool taskbar=pending.Any(c=>c.Tweak.Group==TweakGroup.Taskbar);
   string summary=Core.L.F("Đã áp dụng {0}/{1} thay đổi.",ok,pending.Count)+(errors.Count>0?"\r\n\r\n"+String.Join("\r\n",errors.Take(5)):"")+Core.L.T("\r\n\r\nCửa sổ Explorer đang mở sẽ tự cập nhật; nếu chưa, bấm F5 trong Explorer.")+(taskbar?Core.L.T("\r\nTùy chỉnh thanh tác vụ chỉ có hiệu lực sau khi Khởi động lại Explorer."):"");
   MessageBox.Show(this,summary,Core.L.T("Tùy chỉnh Explorer"),MessageBoxButtons.OK,errors.Count>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
  }

  async Task RestartExplorerPrompt(){
   if(!Confirm(Core.L.T("Khởi động lại Explorer?\r\n\r\nMọi cửa sổ Explorer đang mở sẽ đóng, thanh tác vụ và màn hình nền biến mất vài giây rồi hiện lại. Tệp đang sao chép trong Explorer sẽ bị ngắt.")))return;
   int killed=await Task.Run(()=>ExplorerTweaks.RestartExplorer());
   Log(Core.L.F("Đã khởi động lại Explorer ({0} tiến trình).",killed));
  }

  FolderEntry SelectedEntry(){return browserList.SelectedItems.Count>0?browserList.SelectedItems[0].Tag as FolderEntry:null;}

  /// <summary>Lists a folder (or the drives when path is empty) on a worker thread, showing hidden and system entries regardless of Explorer settings.</summary>
  async Task BrowseFolder(string path){
   path=(path??"").Trim().Trim('"');
   Theme.SetOverlay(browserOverlay,"Đang mở thư mục…",NoteKind.Info);
   var listing=await Task.Run(()=>FileInspector.ListFolder(path,CancellationToken.None));
   browserListing=listing;browserPath=listing.IsDrives?"":listing.Path;filePathBox.Text=browserPath;
   RenderBrowser();
   if(listing.Notes.Count>0)foreach(string n in listing.Notes)Log(Core.L.T("Explorer: ")+n);
  }

  /// <summary>Renders the current listing; hidden entries are muted, system entries amber, so nothing Explorer hides stays invisible here.</summary>
  void RenderBrowser(){
   var listing=browserListing;if(listing==null)return;
   browserList.BeginUpdate();browserList.Items.Clear();browserIcons.Images.Clear();int index=0;
   foreach(var e in listing.Entries){
    if(browserOnlyHidden.Checked&&!e.Hidden&&!e.System)continue;
    var flags=new List<string>();if(e.Hidden)flags.Add(Core.L.T("Ẩn"));if(e.System)flags.Add(Core.L.T("Hệ thống"));if((e.Attributes&FileAttributes.ReadOnly)!=0)flags.Add(Core.L.T("Chỉ đọc"));if(e.ReparsePoint)flags.Add(Core.L.T("Liên kết"));if((e.Attributes&FileAttributes.Encrypted)!=0)flags.Add(Core.L.T("Mã hóa EFS"));if((e.Attributes&FileAttributes.Compressed)!=0)flags.Add(Core.L.T("Nén NTFS"));
    string size=e.IsDrive?(e.Bytes<0?"":Presentation.BytesLabel(e.Bytes)+" "+Core.L.T("đã dùng")):e.IsDirectory?"":Presentation.BytesLabel(e.Bytes);
    string ext=e.IsDrive?e.Extension:e.IsDirectory?"":(e.Extension==""?"—":e.Extension.TrimStart('.').ToLowerInvariant());
    var row=new ListViewItem(new[]{e.Name,ext,e.TypeName,size,e.Modified==DateTime.MinValue?"":e.Modified.ToString("dd/MM/yyyy HH:mm"),String.Join(", ",flags)}){Tag=e,ToolTipText=e.Path};
    if(e.System)row.ForeColor=Theme.Warning;else if(e.Hidden)row.ForeColor=Theme.Muted;
    if(e.Hidden){if(browserHiddenFont==null)browserHiddenFont=new Font(Theme.Body,FontStyle.Italic);row.Font=browserHiddenFont;}
    if(!e.IsDirectory&&!String.IsNullOrEmpty(FileInspector.DisguisedAs(e.Name))){row.ForeColor=Theme.Danger;row.Font=Theme.Strong;}
    string iconKey=e.IsDrive?e.Path:e.IsDirectory?"folder":(e.Extension==""||FileInspector.PortableExecutable.Contains(e.Extension)||e.Extension.Equals(".ico",StringComparison.OrdinalIgnoreCase)||e.Extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase)?e.Path:e.Extension.ToLowerInvariant());
    if(!browserIcons.Images.ContainsKey(iconKey)){var icon=FileInspector.ShellIcon(e,16)??Branding.WindowsAppGlyph(16,Theme.Muted);browserIcons.Images.Add(iconKey,icon);icon.Dispose();}
    row.ImageKey=iconKey;
    Theme.StripeRow(row,index++);browserList.Items.Add(row);
   }
   browserList.EndUpdate();
   Theme.SetOverlay(browserOverlay,browserList.Items.Count==0?(browserOnlyHidden.Checked?"Thư mục này không có mục ẩn hay hệ thống.":"Thư mục trống."):null,NoteKind.Info);
   browserSummary.Text=listing.IsDrives?Core.L.F("{0} ổ đĩa. Bấm đúp để mở.",listing.Entries.Count):Core.L.F("{0} thư mục, {1} tệp  •  {2} ẩn, {3} hệ thống{4}  •  Enter/bấm đúp để mở, Backspace để lên một cấp",listing.Folders,listing.Files,listing.Hidden,listing.System,listing.Truncated?Core.L.F(" (chỉ hiện {0} mục đầu)",FileInspector.MaxEntries):"");
  }

  async Task PickFile(){
   using(var dialog=new OpenFileDialog{Title=Core.L.T("Chọn tệp để xem chi tiết"),Filter=Core.L.T("Mọi tệp")+" (*.*)|*.*",CheckFileExists=true,Multiselect=false}){
    if(!String.IsNullOrWhiteSpace(browserPath)&&Directory.Exists(browserPath))dialog.InitialDirectory=browserPath;
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    filePathBox.Text=dialog.FileName;
   }
   await InspectPath(filePathBox.Text,true);
  }

  /// <summary>Inspects the path in the box with hashing (button/Enter); a folder path browses instead.</summary>
  async Task InspectCurrentFile(){
   var entry=SelectedEntry();string path=filePathBox.Text.Trim().Trim('"');
   if(entry!=null&&!entry.IsDirectory&&(path==""||String.Equals(path,browserPath,StringComparison.OrdinalIgnoreCase)))path=entry.Path;
   if(path=="")throw new IOException(Core.L.T("Dán hoặc chọn một đường dẫn tệp trước."));
   await InspectPath(path,true);
  }

  /// <summary>Browses folders, inspects files on a worker thread and renders the grouped rows; hashing only when asked and enabled.</summary>
  async Task InspectPath(string path,bool withHash){
   path=(path??"").Trim().Trim('"');if(path=="")throw new IOException(Core.L.T("Dán hoặc chọn một đường dẫn tệp trước."));
   if(Directory.Exists(path)){await BrowseFolder(path);return;}
   if(fileCancellation!=null){fileCancellation.Cancel();}
   fileCancellation=new CancellationTokenSource();var token=fileCancellation.Token;
   fileStage.Visible=true;fileStage.Text=Core.L.T("Đang đọc…");fileStage.ForeColor=Theme.Muted;Theme.SetOverlay(fileOverlay,null,NoteKind.Info);
   try{
    var details=await Task.Run(()=>FileInspector.Inspect(path,withHash&&fileHashOption.Checked,token,stage=>{if(!IsDisposed&&IsHandleCreated)try{BeginInvoke((Action)(()=>{if(!IsDisposed)fileStage.Text=stage;}));}catch(InvalidOperationException){}}),token);
    if(token.IsCancellationRequested)return;
    RenderFile(details);
    string parent=FileInspector.ParentOf(details.Path);
    if(details.Exists&&!String.Equals(parent,browserPath,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(parent)){await BrowseFolder(parent);SelectEntry(details.Path);}
    filePathBox.Text=details.Path;
    fileStage.Text=Core.L.F("{0}  •  {1}  •  {2} cảnh báo",details.Name,details.IsDirectory?Core.L.T("Thư mục"):Presentation.BytesLabel(details.Bytes),details.Warnings.Count)+(details.HashSkipped&&!withHash?"  •  "+Core.L.T("bấm Xem chi tiết + băm để tính SHA-256 / MD5"):"");
    fileStage.ForeColor=details.Warnings.Count>0?Theme.Danger:Theme.Muted;
    if(withHash)Log(Core.L.F("Chi tiết tệp: {0} ({1}, {2} cảnh báo).",details.Path,details.TypeName,details.Warnings.Count));
   }catch(OperationCanceledException){fileStage.Text=Core.L.T("Đã dừng.");}
  }

  void SelectEntry(string path){
   foreach(ListViewItem row in browserList.Items){var e=row.Tag as FolderEntry;if(e!=null&&String.Equals(e.Path,path,StringComparison.OrdinalIgnoreCase)){row.Selected=true;row.EnsureVisible();return;}}
  }


  void RenderFile(FileDetails d){
   currentFile=d;fileList.BeginUpdate();fileList.Items.Clear();fileList.Groups.Clear();int index=0;
   string[] order={Core.L.T("Cảnh báo an toàn"),Core.L.T("Chung"),Core.L.T("Thời gian"),Core.L.T("Bảo mật"),Core.L.T("Phiên bản tệp"),Core.L.T("Mã băm")};
   foreach(var r in FileInspector.Rows(d)){
    var row=new ListViewItem(new[]{r.Label,r.Value}){Tag=r,ToolTipText=r.Value};
    if(r.Kind==RowKind.Warning){row.ForeColor=Theme.Danger;row.Font=Theme.Strong;}else if(r.Kind==RowKind.Note)row.ForeColor=Theme.Warning;
    int g=Array.IndexOf(order,r.Group);Theme.AssignGroup(fileList,row,(g<0?9:g).ToString(),r.Group);
    Theme.StripeRow(row,index++);fileList.Items.Add(row);
   }
   fileList.EndUpdate();
   try{var old=fileIconBox.Image;fileIconBox.Image=ExplorerTweaks.IsWindows&&d.Exists&&!d.IsDirectory?Presentation.ShellIcon(d.Path,32):null;if(old!=null)old.Dispose();}catch(Exception){fileIconBox.Image=null;}
  }

  /// <summary>Fills the tab with a disguised-installer sample for --preview explorer; no registry writes, no hashing of real files.</summary>
  public void PreviewExplorer(bool tweaks=false){
   ShowExplorerView(!tweaks);
   var d=new FileDetails{Path=@"C:\Users\ADMIN\Downloads\HoaDon_Thang9.pdf.exe",Name="HoaDon_Thang9.pdf.exe",Extension=".exe",Exists=true,Bytes=1_482_752,TypeName="Application",Architecture="x86",Created=DateTime.Now.AddDays(-2),Modified=DateTime.Now.AddDays(-2),Accessed=DateTime.Now,Attributes=FileAttributes.Archive|FileAttributes.Hidden,FromInternet=true,ZoneId=3,HostUrl="https://files.example-download.net/HoaDon_Thang9.pdf.exe",Owner=@"DESKTOP\ADMIN",Sha256="9f2c4b1e0a7d6c5b4a3928170f6e5d4c3b2a1908f7e6d5c4b3a291807f6e5d4c",Md5="0e7c3d2b1a9f8e7d6c5b4a3928170f6e"};
   d.DisguisedAs=FileInspector.DisguisedAs(d.Name);FileInspector.Warn(d);
   var listing=new FolderListing{Path=@"C:\Users\ADMIN\Downloads"};var now=DateTime.Now;
   Action<string,string,long,FileAttributes,bool> add=(name,type,bytes,attrs,dir)=>{listing.Entries.Add(new FolderEntry{Path=Path.Combine(listing.Path,name),Name=name,Extension=dir?"":Path.GetExtension(name),TypeName=type,Bytes=dir?-1:bytes,Modified=now.AddHours(-listing.Entries.Count*7),Attributes=attrs|(dir?FileAttributes.Directory:0),IsDirectory=dir});if(dir)listing.Folders++;else listing.Files++;if((attrs&FileAttributes.Hidden)!=0)listing.Hidden++;if((attrs&FileAttributes.System)!=0)listing.System++;};
   add("Tài liệu cũ","Thư mục",0,FileAttributes.Normal,true);add(".cache","Thư mục",0,FileAttributes.Hidden,true);
   add("desktop.ini","Configuration settings",282,FileAttributes.Hidden|FileAttributes.System,false);add("Thumbs.db","Data Base File",25_600,FileAttributes.Hidden|FileAttributes.System,false);
   add("Bao-cao-Q3.xlsx","Microsoft Excel Worksheet",148_992,FileAttributes.Archive,false);add("HoaDon_Thang9.pdf.exe","Application",1_482_752,FileAttributes.Archive|FileAttributes.Hidden,false);
   add("hop-dong.pdf","PDF Document",512_000,FileAttributes.Archive,false);add("setup-notes","Tệp không có phần mở rộng",1_024,FileAttributes.Archive,false);add("~$Bao-cao-Q3.xlsx","Microsoft Excel Worksheet",165,FileAttributes.Hidden,false);
   browserListing=listing;browserPath=listing.Path;RenderBrowser();
   filePathBox.Text=d.Path;RenderFile(d);Theme.SetOverlay(fileOverlay,null,NoteKind.Info);
   fileStage.Visible=true;fileStage.Text=Core.L.F("{0}  •  {1}  •  {2} cảnh báo",d.Name,Presentation.BytesLabel(d.Bytes),d.Warnings.Count);fileStage.ForeColor=Theme.Danger;
   tabs.SelectedTab=explorerTab;
  }
 }
}
