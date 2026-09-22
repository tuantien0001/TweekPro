using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Extensions;

namespace TweekPro {
 public partial class MainForm {
  ListView extList=new SmoothListView();Label extOverlay,extSummary;TabPage extTab;List<BrowserExtension> extItems=new List<BrowserExtension>();

  /// <summary>Builds the Browser Extensions tab: a read-only inventory grouped by browser with provenance and permission warnings.</summary>
  void BuildExtensionsTab(){
   var tab=extTab=new TabPage(Core.L.T("Tiện ích trình duyệt"));tabs.TabPages.Add(tab);
   SetupList(extList,new[]{"Tiện ích","Phiên bản","Hồ sơ","Trạng thái","Nguồn cài","Cảnh báo","Cài lúc","Thư mục"},new[]{260,90,150,90,170,330,110,300},false,true);
   extList.DoubleClick+=(s,e)=>OpenExtensionFolder();
   var host=Theme.ListHost(extList,out extOverlay);
   var bar=Bar();
   Add(bar,"Làm mới",async()=>await LoadExtensions(),ButtonStyle.Primary);
   Add(bar,"Mở thư mục tiện ích",()=>{OpenExtensionFolder();return Task.FromResult(0);});
   Add(bar,"Mở trang quản lý của trình duyệt",()=>{OpenExtensionManager();return Task.FromResult(0);});
   Add(bar,"Xuất CSV",()=>{ExportExtensions();return Task.FromResult(0);});
   var note=Theme.Note("Chỉ đọc: liệt kê tiện ích của Chrome, Edge, Brave, Vivaldi, Opera và Firefox trong mọi hồ sơ, kèm nguồn cài (cửa hàng, ép cài bằng chính sách, cài ngoài) và quyền rộng. Gỡ tiện ích bằng trang quản lý của chính trình duyệt để trạng thái đồng bộ đúng; tiện ích «ép cài bằng chính sách» trên máy cá nhân thường là phần mềm không mong muốn — kiểm tra khóa ExtensionInstallForcelist.",NoteKind.Info);
   extSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(extSummary);
   tab.Controls.Add(host);tab.Controls.Add(extSummary);tab.Controls.Add(note);tab.Controls.Add(bar);
   Theme.SetOverlay(extOverlay,"Chưa tải.\r\nBấm Làm mới để liệt kê tiện ích của các trình duyệt đã cài.",NoteKind.Info);
   extSummary.Text=Core.L.T("Chưa tải.");
  }

  void RenderExtensions(){
   extList.BeginUpdate();extList.Items.Clear();extList.Groups.Clear();
   foreach(var e in extItems){
    var row=new ListViewItem(new[]{e.Name,e.Version,e.Profile,Core.L.T(e.Enabled?"Đang bật":"Đã tắt"),Core.L.T(e.Source),Core.L.T(e.Warning),e.Installed==DateTime.MinValue?"—":e.Installed.ToString("dd/MM/yyyy"),Presentation.ShortPath(e.Folder??"",60)}){Tag=e,ToolTipText=e.Name+"  •  "+e.Id+"\r\n"+e.Description+"\r\n"+Core.L.T("Quyền: ")+(e.Permissions.Count==0?"—":String.Join(", ",e.Permissions.Take(12)))+"\r\n"+e.Folder};
    if(e.Policy)row.ForeColor=Theme.Danger;else if(e.Warning!="")row.ForeColor=Theme.Warning;else if(!e.Enabled)row.ForeColor=Theme.Muted;
    Theme.AssignGroup(extList,row,e.Browser,e.Browser);
    Theme.StripeRow(row,extList.Items.Count);extList.Items.Add(row);
   }
   extList.EndUpdate();
   extSummary.Text=Core.L.F("{0} tiện ích trong {1} hồ sơ  •  {2} ép cài bằng chính sách  •  {3} cảnh báo  •  {4} đã tắt",extItems.Count,extItems.Select(e=>e.Browser+"|"+e.Profile).Distinct().Count(),extItems.Count(e=>e.Policy),extItems.Count(e=>e.Warning!=""&&!e.Policy),extItems.Count(e=>!e.Enabled));
  }

  async Task LoadExtensions(){
   Theme.SetOverlay(extOverlay,"Đang đọc hồ sơ trình duyệt…",NoteKind.Info);
   var notes=new List<string>();
   extItems=await Task.Run(()=>BrowserExtensions.Scan(BrowserExtensions.Profiles(),notes));
   RenderExtensions();foreach(string n in notes.Take(5))Log(Core.L.T("Tiện ích: ")+n);
   Log(Core.L.F("Tiện ích trình duyệt: {0} mục, {1} ép cài bằng chính sách.",extItems.Count,extItems.Count(e=>e.Policy)));
   Theme.SetOverlay(extOverlay,extItems.Count==0?"Không tìm thấy tiện ích nào trong các hồ sơ trình duyệt của tài khoản này.":null,NoteKind.Info);
  }

  BrowserExtension SelectedExtension(){if(extList.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một tiện ích."));return (BrowserExtension)extList.SelectedItems[0].Tag;}
  void OpenExtensionFolder(){var e=SelectedExtension();if(String.IsNullOrEmpty(e.Folder)||!Directory.Exists(e.Folder))throw new IOException(Core.L.T("Tiện ích này không có thư mục trên đĩa (được nhúng trong hồ sơ)."));System.Diagnostics.Process.Start("explorer.exe","\""+e.Folder+"\"");}
  void OpenExtensionManager(){var e=SelectedExtension();var info=BrowserExtensions.ManagerPage(e.Browser);if(info==null)throw new IOException(Core.L.F("Không tìm thấy tệp chạy của {0}; mở trang tiện ích bằng tay trong trình duyệt.",e.Browser));System.Diagnostics.Process.Start(info);}

  void ExportExtensions(){
   if(extItems.Count==0)throw new IOException(Core.L.T("Chưa có dữ liệu để xuất."));
   using(var d=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName="tweekpro-extensions.csv"}){
    if(d.ShowDialog(this)!=DialogResult.OK)return;
    var sb=new StringBuilder();sb.AppendLine("Browser,Profile,Name,Version,Id,Enabled,Source,Warning,Installed,Permissions,Folder");
    foreach(var e in extItems)sb.AppendLine(String.Join(",",new[]{e.Browser,e.Profile,e.Name,e.Version,e.Id,e.Enabled?"yes":"no",e.Source,e.Warning,e.Installed==DateTime.MinValue?"":e.Installed.ToString("yyyy-MM-dd"),String.Join(" ",e.Permissions),e.Folder}.Select(Engine.Csv)));
    File.WriteAllText(d.FileName,sb.ToString(),new UTF8Encoding(true));Log(Core.L.T("Đã xuất ")+d.FileName);
   }
  }

  /// <summary>Fills the tab with illustrative rows for --preview extensions.</summary>
  public void PreviewExtensions(){
   extItems=new List<BrowserExtension>{
    new BrowserExtension{Browser="Chrome",Profile="Default",Id="cjpalhdlnbpafiamejdnhcphjbkeiagm",Name="uBlock Origin",Version="1.62.0",Enabled=true,Source="Cửa hàng",FromStore=true,Installed=DateTime.Now.AddDays(-300),Permissions={"webRequest","<all_urls>"},Warning="Quyền rộng: webRequest, <all_urls>",Folder=@"C:\Users\ADMIN\AppData\Local\Google\Chrome\User Data\Default\Extensions\cjpalhdlnbpafiamejdnhcphjbkeiagm\1.62.0_0"},
    new BrowserExtension{Browser="Chrome",Profile="Default",Id="aapbdbdomjkkjkaonfhkkikfgjllcleb",Name="Google Translate",Version="2.0.15",Enabled=true,Source="Cửa hàng",FromStore=true,Installed=DateTime.Now.AddDays(-120),Folder=@"C:\Users\ADMIN\AppData\Local\Google\Chrome\User Data\Default\Extensions\aapbdbdomjkkjkaonfhkkikfgjllcleb\2.0.15_0"},
    new BrowserExtension{Browser="Chrome",Profile="Default",Id="pkedcjkdefgpdelpbcmbmeomcjbeemfm",Name="Search Manager Pro",Version="4.1",Enabled=true,Policy=true,Source="Chính sách (ép cài)",FromStore=false,Installed=DateTime.Now.AddDays(-9),Permissions={"tabs","<all_urls>","webRequest"},Warning="Ép cài bằng chính sách — trên máy cá nhân thường là phần mềm không mong muốn; Quyền rộng: tabs, <all_urls>, webRequest",Folder=@"C:\Users\ADMIN\AppData\Local\Google\Chrome\User Data\Default\Extensions\pkedcjkdefgpdelpbcmbmeomcjbeemfm\4.1_0"},
    new BrowserExtension{Browser="Edge",Profile="Công việc (Profile 1)",Id="odfafepnkmbhccpbejgmiehpchacaeak",Name="Microsoft Editor",Version="1.0.32",Enabled=false,Source="Cửa hàng",FromStore=true,Installed=DateTime.Now.AddDays(-60),Folder=@"C:\Users\ADMIN\AppData\Local\Microsoft\Edge\User Data\Profile 1\Extensions\odfafepnkmbhccpbejgmiehpchacaeak\1.0.32_0"},
    new BrowserExtension{Browser="Firefox",Profile="abc123.default-release",Id="uBlock0@raymondhill.net",Name="uBlock Origin",Version="1.62.0",Enabled=true,Source="Hồ sơ người dùng",FromStore=true,Installed=DateTime.Now.AddDays(-400),Permissions={"<all_urls>","webRequest"},Warning="Quyền rộng: <all_urls>, webRequest",Folder=@"C:\Users\ADMIN\AppData\Roaming\Mozilla\Firefox\Profiles\abc123.default-release\extensions\uBlock0@raymondhill.net.xpi"}
   };
   RenderExtensions();Theme.SetOverlay(extOverlay,null,NoteKind.Info);tabs.SelectedTab=extTab;
  }
 }
}
