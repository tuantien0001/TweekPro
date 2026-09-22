using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.SysSpace;

namespace TweekPro {
 public partial class MainForm {
  ListView spaceList=new SmoothListView();Label spaceOverlay,spaceSummary,spaceStage;TabPage spaceTab;Button spaceStop;
  List<SpaceItem> spaceItems=new List<SpaceItem>();List<DriverPackage> spaceDrivers;CancellationTokenSource spaceCancellation;

  /// <summary>Builds the System Space tab: measured system consumers, the estimated reclaim and the Windows tool (or vault move) that reclaims each one.</summary>
  void BuildSystemSpaceTab(){
   var tab=spaceTab=new TabPage(Core.L.T("Dung lượng hệ thống"));tabs.TabPages.Add(tab);
   SetupList(spaceList,new[]{"Mục","Dung lượng","Ước tính thu hồi","Trạng thái","Công cụ Windows","Vị trí","Mô tả"},new[]{290,110,130,220,250,280,340},false,true);
   spaceList.DoubleClick+=(s,e)=>ShowSpaceDetails();
   var host=Theme.ListHost(spaceList,out spaceOverlay);
   var bar=Bar();
   Add(bar,"Đo dung lượng",async()=>await MeasureSystemSpace(),ButtonStyle.Primary);
   Add(bar,"Chạy công cụ cho mục đang chọn",async()=>await RunSystemSpaceTool(),ButtonStyle.Danger);
   Add(bar,"Xem chi tiết",()=>{ShowSpaceDetails();return Task.FromResult(0);});
   Add(bar,"Mở Storage Sense",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:storagesense"){UseShellExecute=true});return Task.FromResult(0);});
   spaceStop=Theme.Button("Dừng",ButtonStyle.Secondary);spaceStop.Margin=new Padding(0,0,8,8);spaceStop.Visible=false;
   spaceStop.Click+=(s,e)=>{if(spaceCancellation!=null)spaceCancellation.Cancel();};bar.Controls.Add(spaceStop);
   var note=Theme.Note("Những vùng này thuộc Windows nên Tweek Pro không tự xóa tệp: mỗi mục có đúng một công cụ chính chủ (Disk Cleanup cho Windows.old, DISM cho WinSxS, pnputil cho driver cũ, powercfg cho hiberfil, cmdlet cho Delivery Optimization và Thùng rác). Riêng gói MSI/MSP mồ côi trong Windows\\Installer được chuyển vào Kho khôi phục. Cột «Ước tính thu hồi» cho biết công cụ sẽ giải phóng bao nhiêu (WinSxS lấy từ DISM /AnalyzeComponentStore). Công cụ chạy nền, có thể theo dõi tiến độ ở dòng trạng thái. Cần quyền quản trị.",NoteKind.Warning);
   spaceStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   spaceSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(spaceSummary);
   tab.Controls.Add(host);tab.Controls.Add(spaceSummary);tab.Controls.Add(spaceStage);tab.Controls.Add(note);tab.Controls.Add(bar);
   spaceItems=SystemSpace.Catalog();RenderSystemSpace(false);
   Theme.SetOverlay(spaceOverlay,"Chưa đo.\r\nBấm Đo dung lượng để xem Windows.old, WinSxS, Delivery Optimization, driver cũ, gói Installer mồ côi, hiberfil, pagefile, điểm khôi phục và thùng rác chiếm bao nhiêu và có thể thu hồi bao nhiêu. Chỉ đọc.",NoteKind.Info);
  }

  /// <summary>Renders every item; items not yet measured in this run show "…" so progress is visible while the measurement continues.</summary>
  void RenderSystemSpace(bool measured,HashSet<string> done=null){
   spaceList.BeginUpdate();spaceList.Items.Clear();spaceList.Groups.Clear();int order=0;
   foreach(var it in spaceItems){
    order++;bool ready=measured&&(done==null||done.Contains(it.Id));
    string state=!ready?Core.L.T(measured?"Đang đo…":"Chưa đo"):it.Info?Core.L.T("Chỉ hiển thị"):!it.Available?Core.L.T("Không có / không áp dụng"):it.Vaulted?Core.L.T("Có thể chuyển vào Kho (khôi phục được)"):it.Reversible?Core.L.T("Có thể tắt (đảo ngược được)"):Core.L.T("Có thể dọn bằng công cụ Windows");
    string reclaim=!ready?"…":it.Reclaim<0?(it.Info?Core.L.T("tùy chọn"):"—"):Presentation.BytesLabel(it.Reclaim);
    var row=new ListViewItem(new[]{Core.L.T(it.Name),!ready?"…":it.Bytes<0?"—":Presentation.BytesLabel(it.Bytes)+(it.Partial?"+":""),reclaim,state+(it.Note==""||!ready?"":"  •  "+Core.L.T(it.Note)),Core.L.T(it.ToolLabel),Presentation.ShortPath(it.Location,60),Core.L.T(it.Description)}){Tag=it,ToolTipText=Core.L.T(it.Description)+"\r\n"+it.ToolCommand+(String.IsNullOrEmpty(it.ReclaimNote)?"":"\r\n"+Core.L.T("Ước tính: ")+Core.L.T(it.ReclaimNote))+(String.IsNullOrEmpty(it.Warning)?"":"\r\n\r\n"+Core.L.T(it.Warning))};
    if(ready&&!it.Available&&!it.Info)row.ForeColor=Theme.Muted;else if(ready&&it.Available&&!it.Info&&!it.Reversible)row.ForeColor=Theme.Text;
    Theme.AssignGroup(spaceList,row,(Array.IndexOf(new[]{"Windows","Nguồn điện","Sao lưu"},it.Group)+1)+"-"+it.Group,Core.L.T(it.Group));
    Theme.StripeRow(row,spaceList.Items.Count);spaceList.Items.Add(row);
   }
   spaceList.EndUpdate();
   long total=spaceItems.Where(it=>it.Available&&!it.Info&&it.Bytes>0).Sum(it=>it.Bytes);
   long reclaimable=spaceItems.Where(it=>it.Available&&!it.Info&&it.Reclaim>0).Sum(it=>it.Reclaim);
   spaceSummary.Text=measured?Core.L.F("Đang chiếm {0}  •  Ước tính thu hồi được: khoảng {1}  •  Công cụ Windows không qua Kho; gói Installer mồ côi có qua Kho",Presentation.BytesLabel(total),Presentation.BytesLabel(reclaimable)):Core.L.T("Chưa đo.");
  }

  /// <summary>Measures item by item on a worker thread, re-rendering after each so sizes appear progressively; Dừng cancels between items.</summary>
  async Task MeasureSystemSpace(){
   spaceCancellation=new CancellationTokenSource();var token=spaceCancellation.Token;spaceStop.Visible=true;
   spaceStage.Visible=true;Theme.SetOverlay(spaceOverlay,null,NoteKind.Info);Log(Core.L.T("Dung lượng hệ thống: đang đo (chỉ đọc)."));
   var items=SystemSpace.Catalog();spaceItems=items;var done=new HashSet<string>();RenderSystemSpace(true,done);
   try{
    foreach(var it in items){
     token.ThrowIfCancellationRequested();spaceStage.Text=Core.L.T("Đang đo: ")+Core.L.T(it.Name);
     var current=it;
     await Task.Run(()=>{SystemSpace.Measure(current,TimeSpan.FromSeconds(current.Id=="winsxs"?20:8),null,s=>ReportSpace(Core.L.T("Đang đo: ")+Core.L.T(current.Name)+"  |  "+Core.L.T(s)));if(current.Id=="driver-store")spaceDrivers=current.Available||current.Note!=""?SafeDrivers():null;},token);
     done.Add(it.Id);RenderSystemSpace(true,done);
    }
    spaceStage.Text=Core.L.T("Đo xong.")+"  "+spaceSummary.Text;Log(Core.L.T("Dung lượng hệ thống: ")+spaceSummary.Text);
   }catch(OperationCanceledException){spaceStage.Text=Core.L.T("Đã dừng đo.");RenderSystemSpace(true,done);}
   finally{spaceCancellation.Dispose();spaceCancellation=null;spaceStop.Visible=false;}
  }

  List<DriverPackage> SafeDrivers(){try{return SystemSpace.ListDrivers();}catch(Exception e){Core.Log.Warn("Get-WindowsDriver: "+e.Message);return null;}}

  /// <summary>Confirms with the item's own warning, then runs its Windows tool (or the vault move) on a worker thread with live progress in the stage line.</summary>
  async Task RunSystemSpaceTool(){
   if(spaceList.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một mục để chạy công cụ."));
   var it=(SpaceItem)spaceList.SelectedItems[0].Tag;
   if(it.Info){SystemSpace.Run(it,null,CancellationToken.None);return;}
   if(!it.Available)throw new IOException(Core.L.T("Mục này hiện không có gì để dọn hoặc chưa được đo."));
   string estimate=it.Reclaim<0?"—":Presentation.BytesLabel(it.Reclaim);
   if(it.Vaulted){await MoveInstallerOrphans(it,estimate);return;}
   var older=it.Id=="driver-store"?SystemSpace.Duplicates(spaceDrivers??new List<DriverPackage>()):null;
   var commands=SystemSpace.Commands(it,older);if(commands.Count==0)throw new IOException(Core.L.T("Không có lệnh nào để chạy cho mục này."));
   string preview=String.Join("\r\n",commands.Take(8).Select(c=>"  "+Path.GetFileName(c.Key)+" "+c.Value))+(commands.Count>8?"\r\n  … +"+(commands.Count-8):"");
   var answer=MessageBox.Show(this,Core.L.T(it.Name)+"  •  "+(it.Bytes<0?"—":Presentation.BytesLabel(it.Bytes))+"\r\n"+Core.L.T("Ước tính thu hồi: ")+estimate+(String.IsNullOrEmpty(it.ReclaimNote)?"":"  ("+Core.L.T(it.ReclaimNote)+")")+"\r\n\r\n"+Core.L.T(it.Warning??"")+"\r\n\r\n"+Core.L.T("Lệnh sẽ chạy:")+"\r\n"+preview+"\r\n\r\n"+Core.L.T("Kết quả KHÔNG đi qua Kho khôi phục. Tiếp tục?"),Core.L.T("Chạy công cụ Windows"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
   if(answer!=DialogResult.Yes)return;
   spaceCancellation=new CancellationTokenSource();var token=spaceCancellation.Token;spaceStage.Visible=true;spaceStage.Text=Core.L.T("Đang chạy: ")+it.ToolCommand;
   Log(Core.L.F("Dung lượng hệ thống: chạy {0} lệnh cho «{1}».",commands.Count,Core.L.T(it.Name)));
   long before=FreeBytes(it.Location);
   List<ToolResult> results;
   try{results=await Task.Run(()=>SystemSpace.Run(it,older,token,s=>ReportSpace(Core.L.T("Đang chạy: ")+s)),token);}
   catch(OperationCanceledException){spaceStage.Text=Core.L.T("Đã dừng.");return;}
   finally{spaceCancellation.Dispose();spaceCancellation=null;}
   int ok=results.Count(r=>r.Ok),failed=results.Count-ok;long after=FreeBytes(it.Location);
   foreach(var r in results)Log(Path.GetFileName(r.Exe)+" "+r.Args+" → "+Core.L.F("mã {0}, {1}s",r.ExitCode,(int)r.Elapsed.TotalSeconds)+(SystemSpace.Tail(r,2)==""?"":"  |  "+SystemSpace.Tail(r,2).Replace("\r\n"," / ")));
   string summary=Core.L.F("Đã chạy {0} lệnh: {1} thành công, {2} bị từ chối hoặc lỗi.",results.Count,ok,failed)+(before>=0&&after>=0?"  "+Core.L.F("Ổ đĩa trống thêm {0}.",Presentation.BytesLabel(Math.Max(0,after-before))):"");
   spaceStage.Text=summary;
   MessageBox.Show(this,summary+(failed>0?"\r\n\r\n"+String.Join("\r\n\r\n",results.Where(r=>!r.Ok).Take(3).Select(r=>Path.GetFileName(r.Exe)+" "+r.Args+"\r\n"+SystemSpace.Tail(r,4))):""),Core.L.T("Kết quả công cụ Windows"),MessageBoxButtons.OK,failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
   await MeasureSystemSpace();
  }

  /// <summary>Confirms and moves orphaned MSI/MSP caches into the vault; the list of files is shown first.</summary>
  async Task MoveInstallerOrphans(SpaceItem it,string estimate){
   string preview=String.Join("\r\n",it.Details.Take(10).Select(d=>"  "+Presentation.ShortPath(d,100)))+(it.Details.Count>10?"\r\n  … +"+(it.Details.Count-10):"");
   if(!Confirm(Core.L.T(it.Name)+"  •  "+it.Paths.Count+Core.L.T(" tệp, ")+estimate+"\r\n\r\n"+Core.L.T(it.Warning??"")+"\r\n\r\n"+preview+"\r\n\r\n"+Core.L.T("Chuyển các tệp này vào Kho khôi phục?")))return;
   spaceCancellation=new CancellationTokenSource();var token=spaceCancellation.Token;spaceStage.Visible=true;spaceStop.Visible=true;spaceStage.Text=Core.L.T("Đang chuyển vào kho…");
   Backup backup;
   try{backup=await Task.Run(()=>SystemSpace.MoveOrphans(it,token,s=>ReportSpace(Core.L.T("Đang chuyển vào kho: ")+s)),token);}
   catch(OperationCanceledException){spaceStage.Text=Core.L.T("Đã dừng.");return;}
   finally{spaceCancellation.Dispose();spaceCancellation=null;spaceStop.Visible=false;}
   LoadBackups();
   string summary=Core.L.F("Đã chuyển {0} vào Kho khôi phục (bản sao lưu {1}).",Presentation.BytesLabel(backup.Bytes),backup.Id.Substring(0,8))+(String.IsNullOrEmpty(backup.Error)?"":"  "+Core.L.T("Bỏ qua: ")+backup.Error);
   spaceStage.Text=summary;Log(Core.L.T("Dung lượng hệ thống: ")+summary);
   MessageBox.Show(this,summary+Core.L.T("\r\n\r\nNếu một ứng dụng báo thiếu gói khi sửa/gỡ, khôi phục bản sao lưu này trong tab Kho khôi phục."),Core.L.T("Windows\\Installer"),MessageBoxButtons.OK,backup.State=="BackedUp"?MessageBoxIcon.Information:MessageBoxIcon.Warning);
   await MeasureSystemSpace();
  }

  static long FreeBytes(string location){try{string root=Path.GetPathRoot(location);if(String.IsNullOrEmpty(root))return -1;return new DriveInfo(root).AvailableFreeSpace;}catch(Exception){return -1;}}

  void ReportSpace(string text){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(!IsDisposed)spaceStage.Text=text;}));}catch(InvalidOperationException){}}

  void ShowSpaceDetails(){
   if(spaceList.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một mục để xem chi tiết."));
   var it=(SpaceItem)spaceList.SelectedItems[0].Tag;
   string body=Core.L.T(it.Description)+"\r\n\r\n"+Core.L.T("Vị trí: ")+it.Location+"\r\n"+Core.L.T("Dung lượng: ")+(it.Bytes<0?"—":Presentation.BytesLabel(it.Bytes)+(it.Partial?" (+)":""))+"\r\n"+Core.L.T("Ước tính thu hồi: ")+(it.Reclaim<0?"—":Presentation.BytesLabel(it.Reclaim))+(String.IsNullOrEmpty(it.ReclaimNote)?"":"  ("+Core.L.T(it.ReclaimNote)+")")+"\r\n"+Core.L.T("Công cụ: ")+it.ToolCommand+(String.IsNullOrEmpty(it.Warning)?"":"\r\n\r\n"+Core.L.T(it.Warning))+(it.Details.Count==0?"":"\r\n\r\n"+String.Join("\r\n",it.Details.Take(40))+(it.Details.Count>40?"\r\n… +"+(it.Details.Count-40):""));
   MessageBox.Show(this,body,Core.L.T(it.Name),MessageBoxButtons.OK,MessageBoxIcon.Information);
  }

  /// <summary>Fills the tab with illustrative sizes for --preview space; nothing is measured.</summary>
  public void PreviewSystemSpace(){
   spaceItems=SystemSpace.Catalog();var sizes=new Dictionary<string,long>{{"windows-old",21L*1024*1024*1024},{"winsxs",8L*1024*1024*1024+300L*1024*1024},{"delivery-optimization",1400L*1024*1024},{"driver-store",860L*1024*1024},{"installer-orphans",3L*1024*1024*1024+200L*1024*1024},{"hiberfil",6L*1024*1024*1024+400L*1024*1024},{"pagefile",4L*1024*1024*1024},{"restore-points",9L*1024*1024*1024},{"recycle-bin",512L*1024*1024}};
   foreach(var it in spaceItems){it.Bytes=sizes[it.Id];it.Available=true;it.Reclaim=it.Info?-1:it.Id=="winsxs"?1310L*1024*1024:it.Bytes;if(it.Id=="driver-store")it.Note="3 bản cũ trong 112 gói.";if(it.Id=="installer-orphans")it.Note="41 gói mồ côi; 236 sản phẩm/bản vá còn tham chiếu 310 gói.";if(it.Id=="winsxs")it.ReclaimNote="DISM: sao lưu và tính năng đã tắt 1,3 GB + bộ đệm 0 B.";}
   RenderSystemSpace(true);Theme.SetOverlay(spaceOverlay,null,NoteKind.Info);tabs.SelectedTab=spaceTab;
  }
 }
}
