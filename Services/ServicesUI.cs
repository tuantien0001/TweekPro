using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Network;
using TweekPro.Services;

namespace TweekPro {
 public partial class MainForm {
  ListView svcList=new SmoothListView();Label svcOverlay,svcSummary;TabPage servicesTab;ComboBox svcFilter;TextBox svcSearch,svcDetails;
  ImageList svcIcons=new ImageList();List<ServiceEntry> services=new List<ServiceEntry>();bool svcLoaded;
  static readonly string[] SvcFilters={"Đang chạy","Tất cả","Bên thứ ba","Có thể tắt","Đã dừng"};

  /// <summary>Builds the Services tab: every Windows service with a plain-language explanation, a safety verdict and right-click control.</summary>
  void BuildServicesTab(){
   var tab=servicesTab=new TabPage(Core.L.T("Dịch vụ hệ thống"));
   svcIcons.ColorDepth=ColorDepth.Depth32Bit;svcIcons.ImageSize=new Size(20,20);
   IntPtr handle=svcIcons.Handle;
   foreach(ServiceSafety s in Enum.GetValues(typeof(ServiceSafety)))svcIcons.Images.Add(s.ToString(),SafetyGlyph(s,20));
   svcIcons.Images.Add("stopped",SafetyGlyph(null,20));
   svcList.SmallImageList=svcIcons;
   SetupList(svcList,new[]{"Dịch vụ","Trạng thái","Kiểu khởi động","PID","RAM","Có dừng được?","Nhóm","Nhà phát hành","Giải thích"},new[]{260,90,100,70,90,230,120,170,620},false);
   svcList.SelectedIndexChanged+=(s,e)=>ShowServiceDetails();
   svcList.DoubleClick+=(s,e)=>{var svc=SelectedService();if(svc!=null)ShowServiceDialog(svc);};
   var host=Theme.ListHost(svcList,out svcOverlay);

   var bar=Bar();
   Add(bar,"Tải danh sách",async()=>await LoadServices(),ButtonStyle.Primary);
   Add(bar,"Dừng",async()=>await StopSelectedService(false));
   Add(bar,"Khởi động",async()=>await StartSelectedService());
   Add(bar,"Dừng và vô hiệu hóa",async()=>await StopSelectedService(true),ButtonStyle.Danger);
   Add(bar,"Services của Windows",()=>{Process.Start(new ProcessStartInfo("services.msc"){UseShellExecute=true});return Task.FromResult(0);});
   var filterLabel=new Label{Text=Core.L.T("Hiện"),AutoSize=true,Margin=new Padding(12,9,4,0),ForeColor=Theme.Muted};
   svcFilter=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=150,Margin=new Padding(0,5,0,0),Font=Theme.Body,FlatStyle=FlatStyle.Flat};
   foreach(var f in SvcFilters)svcFilter.Items.Add(Core.L.T(f));
   svcFilter.SelectedIndex=0;svcFilter.SelectedIndexChanged+=(s,e)=>{if(svcLoaded)RenderServices();};
   var searchLabel=new Label{Text=Core.L.T("Tìm kiếm"),AutoSize=true,Margin=new Padding(12,9,4,0),ForeColor=Theme.Muted};
   svcSearch=new TextBox{Width=200,Height=28,Margin=new Padding(0,4,0,0),Font=Theme.Body,BorderStyle=BorderStyle.FixedSingle,ForeColor=Theme.Text};
   svcSearch.TextChanged+=(s,e)=>{if(svcLoaded)RenderServices();};
   bar.Controls.Add(filterLabel);bar.Controls.Add(svcFilter);bar.Controls.Add(searchLabel);bar.Controls.Add(svcSearch);

   var note=Theme.Note("Mỗi dịch vụ được giải thích bằng lời thường: nó làm gì, thuộc Windows hay ứng dụng nào cài, và có dừng được không. Dịch vụ cốt lõi bị khóa. \"Dừng và vô hiệu hóa\" lưu kiểu khởi động cũ vào Kho khôi phục để bật lại. Chuột phải lên một dòng để thao tác.",NoteKind.Info);
   svcDetails=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Theme.Stripe,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,Font=Theme.Small,Dock=DockStyle.Fill};
   var detailsWrap=new Panel{Dock=DockStyle.Bottom,Height=118,Padding=new Padding(16,10,16,10),BackColor=Theme.Stripe};Theme.BorderTop(detailsWrap);detailsWrap.Controls.Add(svcDetails);
   svcDetails.Text=Core.L.T("Chọn một dịch vụ để xem giải thích đầy đủ và lời khuyên có nên dừng hay không.");
   svcSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(svcSummary);
   tab.Controls.Add(host);tab.Controls.Add(detailsWrap);tab.Controls.Add(svcSummary);tab.Controls.Add(note);tab.Controls.Add(bar);
   Theme.SetOverlay(svcOverlay,"Chưa tải danh sách.\r\nBấm Tải danh sách để đọc các dịch vụ Windows (WMI). Chỉ đọc; chưa có gì bị thay đổi.",NoteKind.Info);
   BuildServicesMenu();
  }

  /// <summary>Colored badge per safety level: red lock (core), blue (Windows), amber (optional), green (third-party); grey when stopped.</summary>
  static Bitmap SafetyGlyph(ServiceSafety? safety,int size){
   var bmp=new Bitmap(size,size);
   using(var g=Graphics.FromImage(bmp)){
    g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);
    Color c=safety==null?Color.FromArgb(203,213,225):SafetyColor(safety.Value);
    var r=new Rectangle(1,1,size-3,size-3);
    using(var b=new SolidBrush(c))g.FillEllipse(b,r);
    using(var pen=new Pen(Color.White,2f){StartCap=LineCap.Round,EndCap=LineCap.Round}){
     float cx=size/2f,cy=size/2f;
     switch(safety){
      case ServiceSafety.Core:g.DrawRectangle(pen,cx-3.5f,cy-1,7,5);g.DrawArc(pen,cx-2.5f,cy-5,5,6,180,180);break;
      case ServiceSafety.Windows:g.DrawLine(pen,cx-3,cy,cx+3,cy);g.DrawLine(pen,cx,cy-3,cx,cy+3);break;
      case ServiceSafety.Optional:g.DrawLine(pen,cx-3,cy,cx+3,cy);break;
      case ServiceSafety.ThirdParty:g.DrawLines(pen,new[]{new PointF(cx-3.5f,cy),new PointF(cx-1,cy+2.5f),new PointF(cx+3.5f,cy-2.5f)});break;
      default:g.DrawEllipse(pen,cx-2,cy-2,4,4);break;
     }
    }
   }
   return bmp;
  }

  static Color SafetyColor(ServiceSafety s){
   switch(s){case ServiceSafety.Core:return Theme.Danger;case ServiceSafety.Windows:return Theme.Primary;case ServiceSafety.Optional:return Theme.Warning;default:return Theme.Success;}
  }

  async Task LoadServices(){
   Theme.SetOverlay(svcOverlay,"Đang đọc dịch vụ qua WMI…",NoteKind.Info);
   List<ServiceEntry> list;
   try{list=await Task.Run(()=>ServiceCatalog.List());}
   catch(Exception e){Theme.SetOverlay(svcOverlay,Core.L.T("Không đọc được danh sách dịch vụ: ")+e.Message,NoteKind.Error);throw;}
   services=list;svcLoaded=true;RenderServices();
   Log(Core.L.F("Dịch vụ: {0} dịch vụ, {1} đang chạy, {2} của bên thứ ba.",services.Count,services.Count(s=>s.Running),services.Count(s=>s.Safety==ServiceSafety.ThirdParty)));
  }

  static string StartModeLabel(string mode){
   switch((mode??"").ToLowerInvariant()){case "auto":return Core.L.T("Tự động");case "manual":return Core.L.T("Thủ công");case "disabled":return Core.L.T("Đã vô hiệu");case "boot":case "system":return Core.L.T("Khi khởi động");default:return mode??"";}
  }
  static string StateLabel(string state){
   switch((state??"").ToLowerInvariant()){case "running":return Core.L.T("Đang chạy");case "stopped":return Core.L.T("Đã dừng");case "start pending":return Core.L.T("Đang khởi động");case "stop pending":return Core.L.T("Đang dừng");case "paused":return Core.L.T("Tạm dừng");default:return state??"";}
  }

  IEnumerable<ServiceEntry> FilteredServices(){
   int f=svcFilter.SelectedIndex;string q=(svcSearch.Text??"").Trim();
   IEnumerable<ServiceEntry> list=services;
   if(f==0)list=list.Where(s=>s.Running);
   else if(f==2)list=list.Where(s=>s.Safety==ServiceSafety.ThirdParty);
   else if(f==3)list=list.Where(s=>s.Safety==ServiceSafety.Optional||s.Safety==ServiceSafety.ThirdParty);
   else if(f==4)list=list.Where(s=>!s.Running);
   if(q!="")list=list.Where(s=>(s.Friendly+" "+s.Name+" "+s.DisplayName+" "+s.Explanation+" "+s.Publisher).IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0);
   return list;
  }

  void RenderServices(){
   var shown=FilteredServices().ToList();
   svcList.BeginUpdate();svcList.Items.Clear();
   foreach(var s in shown){
    var item=new ListViewItem(s.Friendly){Tag=s,ImageKey=s.Running?s.Safety.ToString():"stopped",UseItemStyleForSubItems=false,ToolTipText=s.Explanation};
    item.SubItems.Add(StateLabel(s.State)).ForeColor=s.Running?Theme.Success:Theme.Muted;
    item.SubItems.Add(StartModeLabel(s.StartMode));
    item.SubItems.Add(s.Pid>0?s.Pid.ToString():"");
    item.SubItems.Add(s.Memory>=0?Presentation.BytesLabel(s.Memory):"");
    item.SubItems.Add(ServiceCatalog.SafetyLabel(s.Safety)).ForeColor=SafetyColor(s.Safety);
    item.SubItems.Add(ServiceCatalog.CategoryLabel(s.Category));
    item.SubItems.Add(s.Publisher==""?(s.Safety==ServiceSafety.ThirdParty?"":"Microsoft Windows"):s.Publisher);
    item.SubItems.Add(s.Explanation.Replace("\r\n"," ")).ForeColor=Theme.Muted;
    if(!s.Running)item.ForeColor=Theme.Muted;
    svcList.Items.Add(item);
   }
   svcList.EndUpdate();
   svcOverlay.Visible=shown.Count==0;
   if(shown.Count==0)Theme.SetOverlay(svcOverlay,services.Count==0?Core.L.T("Không đọc được dịch vụ nào."):Core.L.T("Không có dịch vụ khớp bộ lọc."),NoteKind.Info);
   svcSummary.Text=Core.L.F("{0} dịch vụ đang hiện / {1} dịch vụ, {2} đang chạy  •  {3} cốt lõi (khóa), {4} Windows, {5} tùy chọn, {6} bên thứ ba",shown.Count,services.Count,services.Count(s=>s.Running),services.Count(s=>s.Safety==ServiceSafety.Core),services.Count(s=>s.Safety==ServiceSafety.Windows),services.Count(s=>s.Safety==ServiceSafety.Optional),services.Count(s=>s.Safety==ServiceSafety.ThirdParty));
   ShowServiceDetails();
  }

  ServiceEntry SelectedService(){return svcList.SelectedItems.Count==0?null:(ServiceEntry)svcList.SelectedItems[0].Tag;}

  /// <summary>Plain-language summary of one service for the details pane and the details dialog.</summary>
  static string DescribeService(ServiceEntry s){
   var lines=new List<string>();
   lines.Add(s.Friendly+"  ("+s.Name+")"+(s.Running?"  •  "+Core.L.T("Đang chạy")+(s.Pid>0?"  PID "+s.Pid:""):"  •  "+StateLabel(s.State))+"  •  "+Core.L.T("Khởi động: ")+StartModeLabel(s.StartMode));
   lines.Add(Core.L.T("Nó làm gì: ")+s.Explanation);
   lines.Add(Core.L.T("Có dừng được không? ")+ServiceCatalog.SafetyLabel(s.Safety)+". "+ServiceCatalog.SafetyAdvice(s.Safety));
   string owner=s.Publisher!=""?s.Publisher:(s.Safety==ServiceSafety.ThirdParty?Core.L.T("không rõ"):"Microsoft Windows");
   lines.Add(Core.L.T("Nhà phát hành: ")+owner+(s.Executable!=""?"  |  "+Core.L.T("Tệp: ")+s.Executable:"")+(s.Account!=""?"  |  "+Core.L.T("Tài khoản: ")+s.Account:"")+(s.Memory>=0?"  |  RAM "+Presentation.BytesLabel(s.Memory):""));
   if(!String.IsNullOrWhiteSpace(s.Description)&&s.Description!=s.Explanation&&!s.Explanation.Contains(s.Description))lines.Add(Core.L.T("Windows mô tả: ")+s.Description);
   return String.Join("\r\n",lines);
  }

  void ShowServiceDetails(){var s=SelectedService();svcDetails.Text=s==null?Core.L.T("Chọn một dịch vụ để xem giải thích đầy đủ và lời khuyên có nên dừng hay không."):DescribeService(s);}

  void ShowServiceDialog(ServiceEntry s){MessageBox.Show(this,DescribeService(s),Core.L.T("Chi tiết dịch vụ"),MessageBoxButtons.OK,MessageBoxIcon.Information);}

  /// <summary>File name of a Windows path regardless of the host platform's separator.</summary>
  static string FileNameOf(string path){return String.IsNullOrEmpty(path)?"":path.Substring(path.LastIndexOfAny(new[]{'\\','/'})+1);}

  string ServiceLabel(ServiceEntry s){return s.Friendly==s.Name?s.Name:s.Friendly+" ("+s.Name+")";}

  async Task StopSelectedService(bool disable){
   var s=SelectedService();if(s==null)throw new IOException(Core.L.T("Chọn một dịch vụ."));
   await StopService(s,disable);
  }

  async Task StopService(ServiceEntry s,bool disable){
   if(s.Safety==ServiceSafety.Core)throw new IOException(Core.L.T("Dịch vụ cốt lõi của Windows — Tweek Pro không dừng dịch vụ này."));
   string label=ServiceLabel(s);
   string question=disable
    ?Core.L.F("Dừng và vô hiệu hóa dịch vụ {0}?\r\n\r\n{1}\r\n\r\nDịch vụ không tự chạy lại nữa. Kiểu khởi động cũ ({2}) được lưu vào Kho khôi phục để bật lại bất kỳ lúc nào.",label,s.Explanation,StartModeLabel(s.StartMode))
    :Core.L.F("Dừng dịch vụ {0}?\r\n\r\n{1}\r\n\r\nDịch vụ sẽ chạy lại theo kiểu khởi động hiện tại ({2}) ở lần khởi động máy sau.",label,s.Explanation,StartModeLabel(s.StartMode));
   if(!Confirm(question))return;
   await Task.Run(()=>ProcessControl.StopService(s.ToHosted(),disable));
   Log(disable?Core.L.F("Dịch vụ: đã dừng và vô hiệu hóa {0}; bật lại trong Kho khôi phục.",label):Core.L.F("Dịch vụ: đã dừng {0}.",label));
   if(disable)LoadBackups();
   await LoadServices();
  }

  async Task StartSelectedService(){
   var s=SelectedService();if(s==null)throw new IOException(Core.L.T("Chọn một dịch vụ."));
   if(s.Running)throw new IOException(Core.L.T("Dịch vụ này đang chạy."));
   await Task.Run(()=>ServiceCatalog.Start(s.Name));
   Log(Core.L.F("Dịch vụ: đã khởi động {0}.",ServiceLabel(s)));
   await LoadServices();
  }

  async Task RestartService(ServiceEntry s){
   if(s.Safety==ServiceSafety.Core)throw new IOException(Core.L.T("Dịch vụ cốt lõi của Windows — Tweek Pro không dừng dịch vụ này."));
   if(!Confirm(Core.L.F("Khởi động lại dịch vụ {0}?\r\nDịch vụ dừng rồi chạy lại ngay; kết nối đang mở của nó sẽ bị ngắt trong giây lát.",ServiceLabel(s))))return;
   await Task.Run(()=>ServiceCatalog.Restart(s));
   Log(Core.L.F("Dịch vụ: đã khởi động lại {0}.",ServiceLabel(s)));
   await LoadServices();
  }

  /// <summary>Right-click menu: stop / start / restart / stop+disable / end process / open folder / copy name / details; core services show why they are locked.</summary>
  void BuildServicesMenu(){
   AttachMenu(svcList,menu=>{
    var s=SelectedService();if(s==null)return;
    bool core=s.Safety==ServiceSafety.Core;
    MenuItem(menu,"Dừng dịch vụ",async()=>await StopService(s,false),s.Running&&!core,core?"dịch vụ cốt lõi":"đã dừng");
    MenuItem(menu,"Khởi động dịch vụ",async()=>await StartSelectedService(),!s.Running&&!String.Equals(s.StartMode,"Disabled",StringComparison.OrdinalIgnoreCase),s.Running?"đang chạy":"đã vô hiệu — bật lại trong Kho khôi phục");
    MenuItem(menu,"Khởi động lại dịch vụ",async()=>await RestartService(s),s.Running&&!core,core?"dịch vụ cốt lõi":"đã dừng");
    MenuItem(menu,"Dừng và vô hiệu hóa (lưu vào Kho)",async()=>await StopService(s,true),!core&&!String.Equals(s.StartMode,"Disabled",StringComparison.OrdinalIgnoreCase),core?"dịch vụ cốt lõi":"đã vô hiệu",danger:true);
    menu.Items.Add(new ToolStripSeparator());
    string blocked=s.Pid>0?ProcessControl.TerminateBlockReason(s.Pid,FileNameOf(s.Executable)):Core.L.T("không có tiến trình");
    MenuItem(menu,Core.L.F("Kết thúc tiến trình (PID {0})",s.Pid>0?s.Pid.ToString():"—"),async()=>{
     if(!Confirm(Core.L.F("Kết thúc tiến trình {0} (PID {1})?\r\nỨng dụng sẽ đóng ngay và dữ liệu chưa lưu có thể mất. Nếu đây là dịch vụ, Windows có thể tự chạy lại nó — dùng \"Dừng và vô hiệu hóa\" để ngăn.",FileNameOf(s.Executable),s.Pid)))return;
     await Task.Run(()=>ProcessControl.Terminate(s.Pid,FileNameOf(s.Executable)));
     Log(Core.L.F("Dịch vụ: đã kết thúc tiến trình PID {0} của {1}.",s.Pid,ServiceLabel(s)));await LoadServices();
    },blocked==null,blocked,danger:true);
    menu.Items.Add(new ToolStripSeparator());
    MenuItem(menu,"Mở thư mục chứa tệp",()=>{OpenInExplorer(s.Executable);return Task.FromResult(0);},s.Executable!=""&&File.Exists(s.Executable));
    MenuItem(menu,"Sao chép tên dịch vụ",()=>{Clipboard.SetText(s.Name);return Task.FromResult(0);});
    MenuItem(menu,"Sao chép đường dẫn",()=>{Clipboard.SetText(s.PathName??"");return Task.FromResult(0);},!String.IsNullOrWhiteSpace(s.PathName));
    MenuItem(menu,"Chi tiết dịch vụ",()=>{ShowServiceDialog(s);return Task.FromResult(0);});
   });
  }

  /// <summary>Seeds representative services so the tab can be previewed off Windows.</summary>
  public void PreviewServices(){
   string win=@"C:\Windows\System32\";
   var seed=new[]{
    new ServiceEntry{Name="RpcSs",DisplayName="Remote Procedure Call (RPC)",State="Running",StartMode="Auto",Pid=1032,Memory=14L<<20,PathName=win+"svchost.exe -k rpcss -p",Account="NT AUTHORITY\\NetworkService"},
    new ServiceEntry{Name="Spooler",DisplayName="Print Spooler",State="Running",StartMode="Auto",Pid=3120,Memory=9L<<20,PathName=win+"spoolsv.exe",Account="LocalSystem"},
    new ServiceEntry{Name="WSearch",DisplayName="Windows Search",State="Running",StartMode="Auto",Pid=6004,Memory=88L<<20,PathName=win+"SearchIndexer.exe /Embedding",Account="LocalSystem"},
    new ServiceEntry{Name="wuauserv",DisplayName="Windows Update",State="Running",StartMode="Manual",Pid=1888,Memory=41L<<20,PathName=win+"svchost.exe -k netsvcs -p",Account="LocalSystem"},
    new ServiceEntry{Name="cbdhsvc_4a2b1",DisplayName="Clipboard User Service_4a2b1",State="Running",StartMode="Manual",Pid=7220,Memory=6L<<20,PathName=win+"svchost.exe -k ClipboardSvcGroup -p",Account="LocalSystem"},
    new ServiceEntry{Name="AdobeARMservice",DisplayName="Adobe Acrobat Update Service",State="Running",StartMode="Auto",Pid=5364,Memory=3L<<20,PathName="\"C:\\Program Files (x86)\\Common Files\\Adobe\\ARM\\1.0\\armsvc.exe\"",Account="LocalSystem",Publisher="Adobe Inc."},
    new ServiceEntry{Name="BraveElevationService",DisplayName="Brave Elevation Service (BraveElevationService)",State="Stopped",StartMode="Manual",PathName="\"C:\\Program Files\\BraveSoftware\\Brave-Browser\\Application\\1.70.1\\elevation_service.exe\"",Account="LocalSystem",Publisher="Brave Software, Inc."},
    new ServiceEntry{Name="Steam Client Service",DisplayName="Steam Client Service",State="Stopped",StartMode="Manual",PathName="\"C:\\Program Files (x86)\\Common Files\\Steam\\steamservice.exe\" /RunAsService",Account="LocalSystem",Publisher="Valve Corporation"},
    new ServiceEntry{Name="MySQL80",DisplayName="MySQL80",State="Running",StartMode="Auto",Pid=4488,Memory=412L<<20,PathName="\"C:\\Program Files\\MySQL\\MySQL Server 8.0\\bin\\mysqld.exe\" --defaults-file=my.ini MySQL80",Account="NT AUTHORITY\\NetworkService",Publisher="Oracle Corporation"},
    new ServiceEntry{Name="ZoomCptService",DisplayName="Zoom Sharing Service",State="Running",StartMode="Auto",Pid=8102,Memory=12L<<20,PathName="\"C:\\Program Files\\Zoom\\bin\\CptService.exe\"",Account="LocalSystem",Publisher="Zoom Video Communications, Inc.",Description="Zoom screen sharing helper."},
    new ServiceEntry{Name="DiagTrack",DisplayName="Connected User Experiences and Telemetry",State="Running",StartMode="Auto",Pid=2716,Memory=22L<<20,PathName=win+"svchost.exe -k utcsvc -p",Account="LocalSystem"},
    new ServiceEntry{Name="Fax",DisplayName="Fax",State="Stopped",StartMode="Manual",PathName=win+"fxssvc.exe",Account="NT AUTHORITY\\NetworkService"},
   };
   foreach(var s in seed)ServiceCatalog.Explain(s);
   services=seed.OrderBy(s=>s.Running?0:1).ThenBy(s=>s.Safety==ServiceSafety.ThirdParty?0:1).ThenBy(s=>s.Friendly).ToList();
   svcLoaded=true;svcFilter.SelectedIndex=1;RenderServices();
   if(svcList.Items.Count>0){var first=svcList.Items.Cast<ListViewItem>().FirstOrDefault(i=>((ServiceEntry)i.Tag).Name=="AdobeARMservice")??svcList.Items[0];first.Selected=true;first.Focused=true;}
   tabs.SelectedTab=servicesTab;
  }
 }
}
