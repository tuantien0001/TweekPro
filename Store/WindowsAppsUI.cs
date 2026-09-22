using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Store;

namespace TweekPro {
 public partial class MainForm {
  ListView storeList=new SmoothListView();Label storeOverlay,storeSummary;TabPage storeTab;CheckBox storeAllUsers,storeDeprovision,storeShowProtected;
  ImageList storeIcons=new ImageList();List<WindowsApp> storeApps=new List<WindowsApp>();

  /// <summary>Builds the Windows Apps tab: inbox and Store packages with their logos; removable ones can be uninstalled, core ones are locked.</summary>
  void BuildStoreTab(){
   var tab=storeTab=new TabPage(Core.L.T("Ứng dụng Windows"));
   storeIcons.ColorDepth=ColorDepth.Depth32Bit;storeIcons.ImageSize=new Size(28,28);storeList.SmallImageList=storeIcons;
   SetupList(storeList,new[]{"Ứng dụng","Gói","Nhà phát hành","Phiên bản","Dung lượng","Nguồn","Tình trạng","Cảnh báo","Vị trí"},new[]{230,260,160,100,95,80,120,180,300},true,true);
   storeList.ItemChecked+=(s,e)=>{var a=e.Item.Tag as WindowsApp;if(a!=null&&a.Status==AppxStatus.Protected&&e.Item.Checked)e.Item.Checked=false;UpdateStoreSummary();};
   storeList.DoubleClick+=async(s,e)=>await Guard(()=>{OpenStoreLocation();return Task.FromResult(0);});
   var host=Theme.ListHost(storeList,out storeOverlay);

   var bar=Bar();
   Add(bar,"Tải danh sách",async()=>await LoadStoreApps(),ButtonStyle.Primary);
   Add(bar,"Gỡ mục đã chọn",async()=>await RemoveStoreApps(),ButtonStyle.Danger);
   Add(bar,"Bỏ chọn",()=>{foreach(ListViewItem i in storeList.Items)i.Checked=false;return Task.FromResult(0);});
   Add(bar,"Mở trong Microsoft Store",()=>{OpenStorePage();return Task.FromResult(0);});
   Add(bar,"Mở thư mục gói",()=>{OpenStoreLocation();return Task.FromResult(0);});
   storeAllUsers=new CheckBox{Text=Core.L.T("Gỡ cho mọi tài khoản"),AutoSize=true,Margin=new Padding(8,8,12,0),Checked=Core.Elevation.IsElevated,Enabled=Core.Elevation.IsElevated,ForeColor=Theme.Text};
   storeDeprovision=new CheckBox{Text=Core.L.T("Không cài lại cho tài khoản mới"),AutoSize=true,Margin=new Padding(0,8,12,0),Checked=false,Enabled=Core.Elevation.IsElevated,ForeColor=Theme.Text};
   storeShowProtected=new CheckBox{Text=Core.L.T("Hiện thành phần được bảo vệ"),AutoSize=true,Margin=new Padding(0,8,12,0),Checked=false,ForeColor=Theme.Text};
   storeShowProtected.CheckedChanged+=(s,e)=>{if(storeLoaded)RenderStoreApps();};
   bar.Controls.Add(storeAllUsers);bar.Controls.Add(storeDeprovision);bar.Controls.Add(storeShowProtected);

   var note=Theme.Note("Ứng dụng cài sẵn của Windows và ứng dụng Microsoft Store (gói Appx/MSIX). Gỡ qua Remove-AppxPackage của Windows; các thành phần cốt lõi (Start, Search, Settings, Windows Security, thư viện nền…) bị khóa trong mã và không thể chọn. Mục “Cần cân nhắc” gỡ được nhưng hữu ích (Store, App Installer, Photos…). Khôi phục: đăng ký lại từ thư mục gói nếu còn, hoặc cài lại từ Microsoft Store.",NoteKind.Info);
   storeSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(storeSummary);
   tab.Controls.Add(host);tab.Controls.Add(storeSummary);tab.Controls.Add(note);tab.Controls.Add(bar);
   Theme.SetOverlay(storeOverlay,"Chưa tải danh sách.\r\nBấm Tải danh sách để đọc các gói Appx/MSIX đã cài (Get-AppxPackage). Chỉ đọc; chưa có gì bị thay đổi.",NoteKind.Info);
   UpdateStoreSummary();
  }

  async Task LoadStoreApps(){
   Theme.SetOverlay(storeOverlay,"Đang đọc danh sách gói qua PowerShell…",NoteKind.Info);
   bool allUsers=Core.Elevation.IsElevated;
   List<WindowsApp> apps;Dictionary<string,Bitmap> logos;
   try{apps=await Task.Run(()=>WindowsApps.List(allUsers));logos=await Task.Run(()=>LoadStoreIcons(apps));}
   catch(Exception e){Theme.SetOverlay(storeOverlay,Core.L.T("Không đọc được danh sách ứng dụng Windows: ")+e.Message,NoteKind.Error);throw;}
   SwapStoreData(apps,logos);
   RenderStoreApps();
   Log(Core.L.F("Ứng dụng Windows: {0} gói, {1} gỡ được, {2} được bảo vệ.",storeApps.Count,storeApps.Count(a=>a.Status!=AppxStatus.Protected),storeApps.Count(a=>a.Status==AppxStatus.Protected)));
   Log(Core.L.T("Đang đo dung lượng gói…"));
   await Task.Run(()=>MeasureStoreSizes(apps,TimeSpan.FromSeconds(25)));
   if(ReferenceEquals(apps,storeApps))RenderStoreApps();
  }

  /// <summary>Measures each package folder within the overall budget (cached for a day in SizeCache); packages moved to another drive are junctions under WindowsApps and are followed at the root only.</summary>
  static void MeasureStoreSizes(List<WindowsApp> apps,TimeSpan budget){
   var watch=System.Diagnostics.Stopwatch.StartNew();var now=DateTime.Now;
   foreach(var a in apps){
    if(String.IsNullOrWhiteSpace(a.InstallLocation))continue;
    bool partial;long bytes;
    if(!Sizing.SizeCache.TryGet(a.InstallLocation,now,out bytes,out partial)){
     if(watch.Elapsed>budget)break;
     bytes=Sizing.FolderSize.Measure(a.InstallLocation,Sizing.InstallSize.PerFolder,out partial);
     if(bytes>=0)Sizing.SizeCache.Put(a.InstallLocation,bytes,partial,now);
    }
    a.Bytes=bytes;a.BytesPartial=partial;
   }
   Sizing.SizeCache.Flush(now);
  }

  Dictionary<string,Bitmap> storeLogoCache=new Dictionary<string,Bitmap>(StringComparer.OrdinalIgnoreCase);bool storeLoaded;
  /// <summary>Builds package logos into a fresh dictionary (safe on a worker thread); missing or unreadable logos fall back to the drawn Windows glyph.</summary>
  static Dictionary<string,Bitmap> LoadStoreIcons(List<WindowsApp> apps){
   var logos=new Dictionary<string,Bitmap>(StringComparer.OrdinalIgnoreCase);
   foreach(var a in apps){
    if(logos.ContainsKey(a.FullName))continue;
    string logo=WindowsApps.ResolveLogo(a.InstallLocation);
    Bitmap bitmap=null;
    if(logo!=null){try{using(var raw=new Bitmap(logo))bitmap=Presentation.FitIcon(raw,28);}catch(Exception){bitmap=null;}}
    if(bitmap==null)bitmap=Branding.WindowsAppGlyph(28,a.Origin=="System"?Theme.Primary:Theme.Muted);
    logos[a.FullName]=bitmap;
   }
   return logos;
  }

  /// <summary>Publishes a freshly loaded list and logo set on the UI thread, disposing the previous logos only after the swap.</summary>
  void SwapStoreData(List<WindowsApp> apps,Dictionary<string,Bitmap> logos){
   var old=storeLogoCache;storeApps=apps;storeLogoCache=logos;storeLoaded=true;
   foreach(var bmp in old.Values)bmp.Dispose();
  }

  void RenderStoreApps(){
   storeList.BeginUpdate();storeList.Items.Clear();storeList.Groups.Clear();storeIcons.Images.Clear();
   var handle=storeIcons.Handle;
   foreach(var pair in storeLogoCache)storeIcons.Images.Add(pair.Key,pair.Value);
   bool showProtected=storeShowProtected.Checked;
   foreach(var a in storeApps){
    if(a.Status==AppxStatus.Protected&&!showProtected)continue;
    string status=Core.L.T(a.Status==AppxStatus.Removable?"Gỡ được":a.Status==AppxStatus.Caution?"Cần cân nhắc":"Được bảo vệ");
    var verdict=Pup.PupDetector.Classify(a);string flag=verdict==null?"":verdict.Badge+" • "+Pup.PupDetector.SeverityLabel(verdict.Severity);
    string size=a.Bytes<0?"":Presentation.SizeLabel(Sizing.InstallSize.ToKB(a.Bytes))+(a.BytesPartial?"+":"");
    var row=new ListViewItem(new[]{a.DisplayName,a.Name,a.PublisherName,a.Version,size,Core.L.T(a.Origin=="System"?"Hệ thống":a.Origin=="Store"?"Store":"Khác"),status,flag,Presentation.ShortPath(a.InstallLocation,60)}){Tag=a,ImageKey=a.FullName,ToolTipText=a.FullName+"\r\n"+a.InstallLocation+(a.Bytes<0?"":"\r\n"+Core.L.T("Dung lượng gói đo từ thư mục cài; gói ở ổ khác được theo dõi qua liên kết của Windows."))+"\r\n\r\n"+a.StatusReason+(verdict==null?"":"\r\n\r\n"+verdict.Badge+": "+verdict.Reason)};
    if(a.Status==AppxStatus.Protected)row.ForeColor=Theme.Muted;else if(a.Status==AppxStatus.Caution||verdict!=null)row.ForeColor=Theme.Warning;
    Theme.AssignGroup(storeList,row,((int)a.Status).ToString(),Core.L.T(a.Status==AppxStatus.Removable?"Gỡ được":a.Status==AppxStatus.Caution?"Cần cân nhắc":"Được bảo vệ (chỉ xem)"));
    Theme.StripeRow(row,storeList.Items.Count);storeList.Items.Add(row);
   }
   storeList.EndUpdate();
   Theme.SetOverlay(storeOverlay,storeApps.Count==0?"Không đọc được gói nào.":storeList.Items.Count==0?"Mọi gói đều được bảo vệ; bật “Hiện thành phần được bảo vệ” để xem.":null,NoteKind.Info);
   UpdateStoreSummary();
  }

  void UpdateStoreSummary(){
   var selected=storeList.CheckedItems.Cast<ListViewItem>().Select(i=>(WindowsApp)i.Tag).ToList();
   storeSummary.Text=Core.L.F("{0} gói  •  {1} gỡ được  •  {2} được bảo vệ  •  Đã chọn {3}",storeApps.Count,storeApps.Count(a=>a.Status!=AppxStatus.Protected),storeApps.Count(a=>a.Status==AppxStatus.Protected),selected.Count)+(selected.Any(a=>a.Status==AppxStatus.Caution)?Core.L.T("  •  có mục cần cân nhắc"):"");
  }

  async Task RemoveStoreApps(){
   var selected=storeList.CheckedItems.Cast<ListViewItem>().Select(i=>(WindowsApp)i.Tag).Where(a=>a.Status!=AppxStatus.Protected).ToList();
   if(selected.Count==0)throw new IOException(Core.L.T("Đánh dấu ứng dụng muốn gỡ (thành phần được bảo vệ không thể chọn)."));
   bool allUsers=storeAllUsers.Checked&&Core.Elevation.IsElevated,deprovision=storeDeprovision.Checked&&Core.Elevation.IsElevated;
   var caution=selected.Where(a=>a.Status==AppxStatus.Caution).ToList();
   string list=String.Join("\r\n",selected.Take(10).Select(a=>"• "+a.DisplayName+" ("+a.Name+")"))+(selected.Count>10?"\r\n… +"+(selected.Count-10):"");
   if(!Confirm(Core.L.F("Gỡ {0} ứng dụng Windows{1}?\r\n\r\n{2}\r\n\r\nGỡ bằng Remove-AppxPackage. Khôi phục: đăng ký lại từ thư mục gói nếu Windows còn giữ, hoặc cài lại từ Microsoft Store.",selected.Count,allUsers?Core.L.T(" cho mọi tài khoản"):"",list)+(deprovision?Core.L.T("\r\n\r\nBỏ provisioning: tài khoản Windows tạo mới cũng sẽ không có ứng dụng này."):"")))return;
   if(caution.Count>0&&!Confirm(Core.L.F("{0} mục thuộc nhóm “Cần cân nhắc”:\r\n{1}\r\n\r\nĐây là thành phần hữu ích của Windows (ví dụ Microsoft Store dùng để cài lại app khác). Vẫn gỡ?",caution.Count,String.Join("\r\n",caution.Select(a=>"• "+a.DisplayName)))))return;
   int ok=0,failed=0;
   foreach(var a in selected){
    try{Log(Core.L.T("Đang gỡ ứng dụng Windows: ")+a.DisplayName);await Task.Run(()=>WindowsApps.Remove(a,allUsers,deprovision));ok++;Log(Core.L.T("Đã gỡ: ")+a.DisplayName);}
    catch(Exception e){failed++;Log(Core.L.T("Không gỡ được ")+a.DisplayName+": "+e.Message);MessageBox.Show(this,a.DisplayName+"\r\n"+e.Message,Core.L.T("Ứng dụng chưa gỡ"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   }
   LoadBackups();
   await LoadStoreApps();
   string summary=Core.L.F("Đã gỡ {0} ứng dụng Windows; lỗi {1}.",ok,failed);
   Log(summary);MessageBox.Show(this,summary,Core.L.T("Kết quả gỡ ứng dụng Windows"),MessageBoxButtons.OK,failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
  }

  WindowsApp SelectedStoreApp(){return storeList.SelectedItems.Count==0?null:(WindowsApp)storeList.SelectedItems[0].Tag;}

  void OpenStoreLocation(){
   var a=SelectedStoreApp();if(a==null)throw new IOException(Core.L.T("Chọn một ứng dụng trước."));
   if(String.IsNullOrWhiteSpace(a.InstallLocation)||!Directory.Exists(a.InstallLocation))throw new IOException(Core.L.T("Thư mục gói không tồn tại hoặc không đọc được."));
   Process.Start(new ProcessStartInfo("explorer.exe","\""+a.InstallLocation+"\""){UseShellExecute=true});
  }

  void OpenStorePage(){
   var a=SelectedStoreApp();if(a==null)throw new IOException(Core.L.T("Chọn một ứng dụng trước."));
   Process.Start(new ProcessStartInfo(WindowsApps.StoreLink(a.FamilyName)){UseShellExecute=true});
  }

  /// <summary>Fills the tab with the sample package list for --preview store so the layout can be reviewed off Windows.</summary>
  public void PreviewStoreApps(){
   var apps=WindowsApps.Parse(WindowsAppsTests.SampleJsonForPreview);
   for(int i=0;i<apps.Count;i++)apps[i].Bytes=(i%7+1)*23L*1024*1024+(i*911L*1024);
   SwapStoreData(apps,LoadStoreIcons(apps));storeShowProtected.Checked=true;RenderStoreApps();tabs.SelectedTab=storeTab;
  }
 }
}
