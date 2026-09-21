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
  TabPage explorerTab;ListView tweakList=new SmoothListView(),fileList=new SmoothListView();Label tweakOverlay,tweakSummary,fileOverlay,fileStage;
  TextBox filePathBox=new TextBox();PictureBox fileIconBox;CheckBox fileHashOption=new CheckBox();Button tweakApplyButton;
  List<TweakState> tweakStates=new List<TweakState>();FileDetails currentFile;bool tweakLoading;CancellationTokenSource fileCancellation;

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
   top.Controls.Add(tweakHost);top.Controls.Add(tweakSummary);top.Controls.Add(tweakNote);top.Controls.Add(tweakBar);top.Controls.Add(SectionTitle("Tùy chỉnh File Explorer"));

   SetupList(fileList,new[]{"Thuộc tính","Giá trị"},new[]{200,1000},false,true);
   fileList.DoubleClick+=(s,e)=>{if(fileList.SelectedItems.Count>0)try{Clipboard.SetText(fileList.SelectedItems[0].SubItems[1].Text);Log(Core.L.T("Đã sao chép giá trị."));}catch(Exception){}};
   var fileHost=Theme.ListHost(fileList,out fileOverlay);
   var fileBar=Bar();
   Add(fileBar,"Chọn tệp…",async()=>await PickFile(),ButtonStyle.Primary);
   Add(fileBar,"Xem chi tiết",async()=>await InspectCurrentFile());
   Add(fileBar,"Sao chép báo cáo",()=>{if(currentFile==null)throw new IOException(Core.L.T("Chưa có tệp nào được xem."));Clipboard.SetText(FileInspector.Report(currentFile));Log(Core.L.T("Đã sao chép báo cáo chi tiết tệp."));return Task.FromResult(0);});
   Add(fileBar,"Mở vị trí",()=>{if(currentFile==null)throw new IOException(Core.L.T("Chưa có tệp nào được xem."));ExplorerShell.Reveal(currentFile.Path);return Task.FromResult(0);});
   Add(fileBar,"Thuộc tính Windows",()=>{if(currentFile==null)throw new IOException(Core.L.T("Chưa có tệp nào được xem."));ExplorerShell.ShowProperties(Handle,currentFile.Path);return Task.FromResult(0);});
   fileHashOption.Text=Core.L.T("Tính SHA-256 / MD5");fileHashOption.Checked=true;fileHashOption.AutoSize=true;fileHashOption.Margin=new Padding(8,6,0,0);fileHashOption.ForeColor=Theme.Text;fileHashOption.Font=Theme.Body;fileBar.Controls.Add(fileHashOption);
   var pathPanel=new Panel{Dock=DockStyle.Top,Height=48,Padding=new Padding(16,8,16,8),BackColor=Theme.Surface};Theme.BorderBottom(pathPanel);
   fileIconBox=new PictureBox{Dock=DockStyle.Left,Width=40,SizeMode=PictureBoxSizeMode.CenterImage};
   var pathLabel=new Label{Text=Core.L.T("Đường dẫn:"),Dock=DockStyle.Left,Width=90,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Font=Theme.Small};
   filePathBox.Dock=DockStyle.Fill;filePathBox.Font=Theme.Body;filePathBox.BorderStyle=BorderStyle.FixedSingle;
   filePathBox.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Guard(async()=>await InspectCurrentFile());}};
   pathPanel.Controls.Add(filePathBox);pathPanel.Controls.Add(fileIconBox);pathPanel.Controls.Add(pathLabel);
   fileStage=new Label{Dock=DockStyle.Top,Height=28,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   var bottom=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Surface};
   bottom.Controls.Add(fileHost);bottom.Controls.Add(fileStage);bottom.Controls.Add(pathPanel);bottom.Controls.Add(fileBar);bottom.Controls.Add(SectionTitle("Xem chi tiết tệp — kéo thả tệp vào đây hoặc dán đường dẫn"));
   foreach(Control c in new Control[]{bottom,fileList,fileHost,fileOverlay,filePathBox}){c.AllowDrop=true;c.DragEnter+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};c.DragDrop+=async(s,e)=>{var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length>0){filePathBox.Text=files[0];await Guard(async()=>await InspectCurrentFile());}};}

   var split=new SplitContainer{Size=new Size(1200,720),Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterWidth=6,BackColor=Theme.Border,Panel1MinSize=160,Panel2MinSize=160};
   split.Panel1.Controls.Add(top);split.Panel2.Controls.Add(bottom);
   tab.Controls.Add(split);
   bool splitPlaced=false;
   split.SizeChanged+=(s,e)=>{if(splitPlaced||split.Height<400)return;splitPlaced=true;try{split.SplitterDistance=split.Height*46/100;}catch(InvalidOperationException){}};
   Theme.SetOverlay(fileOverlay,"Chưa chọn tệp.\r\nBấm Chọn tệp…, dán đường dẫn rồi Enter, hoặc kéo một tệp từ Explorer vào đây để xem loại thật, thuộc tính ẩn/hệ thống, chữ ký số, mã băm và cảnh báo giả dạng (hoadon.pdf.exe).",NoteKind.Info);
   ReloadTweaks();
  }

  static Label SectionTitle(string text){var l=new Label{Text=Core.L.T(text),Dock=DockStyle.Top,Height=34,Padding=new Padding(16,8,16,0),Font=Theme.Section,ForeColor=Theme.Text,BackColor=Theme.Surface,AutoEllipsis=true};return l;}

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

  async Task PickFile(){
   using(var dialog=new OpenFileDialog{Title=Core.L.T("Chọn tệp để xem chi tiết"),Filter=Core.L.T("Mọi tệp")+" (*.*)|*.*",CheckFileExists=true,Multiselect=false}){
    if(!String.IsNullOrWhiteSpace(filePathBox.Text)){try{string dir=Path.GetDirectoryName(filePathBox.Text.Trim().Trim('"'));if(Directory.Exists(dir))dialog.InitialDirectory=dir;}catch(ArgumentException){}}
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    filePathBox.Text=dialog.FileName;
   }
   await InspectCurrentFile();
  }

  /// <summary>Runs the inspector on a worker thread for the path in the box and renders the grouped rows.</summary>
  async Task InspectCurrentFile(){
   string path=filePathBox.Text.Trim().Trim('"');if(path=="")throw new IOException(Core.L.T("Dán hoặc chọn một đường dẫn tệp trước."));
   if(fileCancellation!=null){fileCancellation.Cancel();}
   fileCancellation=new CancellationTokenSource();var token=fileCancellation.Token;
   fileStage.Visible=true;fileStage.Text=Core.L.T("Đang đọc…");Theme.SetOverlay(fileOverlay,null,NoteKind.Info);
   try{
    var details=await Task.Run(()=>FileInspector.Inspect(path,fileHashOption.Checked,token,stage=>{if(!IsDisposed&&IsHandleCreated)try{BeginInvoke((Action)(()=>{if(!IsDisposed)fileStage.Text=stage;}));}catch(InvalidOperationException){}}),token);
    RenderFile(details);
    fileStage.Text=Core.L.F("{0}  •  {1}  •  {2} cảnh báo",details.Name,details.IsDirectory?Core.L.T("Thư mục"):Presentation.BytesLabel(details.Bytes),details.Warnings.Count);
    fileStage.ForeColor=details.Warnings.Count>0?Theme.Danger:Theme.Muted;
    Log(Core.L.F("Chi tiết tệp: {0} ({1}, {2} cảnh báo).",details.Path,details.TypeName,details.Warnings.Count));
   }catch(OperationCanceledException){fileStage.Text=Core.L.T("Đã dừng.");}
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
  public void PreviewExplorer(){
   var d=new FileDetails{Path=@"C:\Users\ADMIN\Downloads\HoaDon_Thang9.pdf.exe",Name="HoaDon_Thang9.pdf.exe",Extension=".exe",Exists=true,Bytes=1_482_752,TypeName="Application",Architecture="x86",Created=DateTime.Now.AddDays(-2),Modified=DateTime.Now.AddDays(-2),Accessed=DateTime.Now,Attributes=FileAttributes.Archive|FileAttributes.Hidden,FromInternet=true,ZoneId=3,HostUrl="https://files.example-download.net/HoaDon_Thang9.pdf.exe",Owner=@"DESKTOP\ADMIN",Sha256="9f2c4b1e0a7d6c5b4a3928170f6e5d4c3b2a1908f7e6d5c4b3a291807f6e5d4c",Md5="0e7c3d2b1a9f8e7d6c5b4a3928170f6e"};
   d.DisguisedAs=FileInspector.DisguisedAs(d.Name);FileInspector.Warn(d);
   filePathBox.Text=d.Path;RenderFile(d);Theme.SetOverlay(fileOverlay,null,NoteKind.Info);
   fileStage.Visible=true;fileStage.Text=Core.L.F("{0}  •  {1}  •  {2} cảnh báo",d.Name,Presentation.BytesLabel(d.Bytes),d.Warnings.Count);fileStage.ForeColor=Theme.Danger;
   tabs.SelectedTab=explorerTab;
  }
 }
}
