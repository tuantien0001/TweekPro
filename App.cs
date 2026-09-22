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

namespace TweekPro {
 public partial class MainForm:Form {
  ListView apps=new SmoothListView(),remnants=new SmoothListView(),backups=new SmoothListView();
  TextBox search=new TextBox(),details=new TextBox(),log=new TextBox();
  Label selectionSummary=new Label(),backupSummary=new Label();Label status=Theme.StatusBar();TabControl tabs=new TabControl();TabStrip tabStrip;
  Label appsOverlay,remnantsOverlay,backupsOverlay;TabPage remnantsTab;
  List<AppEntry> inventory=new List<AppEntry>(),history=new List<AppEntry>();
  List<Candidate> candidates=new List<Candidate>();List<Button> actions=new List<Button>();
  ImageList appIcons=new ImageList(); Label appCount=new Label();
  bool busy;string inventoryError;string sessions=Core.Paths.Sessions;Icon brandIcon,brandIconSmall;CheckBox pupOnly=new CheckBox();
  Core.Settings settings=Core.Settings.Load(Core.Paths.SettingsFile);
  public const string Version="0.7.4";
  public const string AppTitle="Tweek Pro";
  public const string Tagline="Trình quản lý Windows";

  /// <summary>Builds the main window; when preview is true the inventory is not loaded automatically.</summary>
  public MainForm(bool preview=false){
   SuspendLayout();
   Text=AppTitle+" "+Version+" – "+Core.L.T(Tagline);Size=new Size(1240,820);MinimumSize=new Size(1120,700);StartPosition=FormStartPosition.CenterScreen;
   Font=Theme.Body;BackColor=Theme.Canvas;ForeColor=Theme.Text;AutoScaleDimensions=new SizeF(96F,96F);AutoScaleMode=AutoScaleMode.Dpi;
   try{Branding.ApplyWindowIcons(this,out brandIconSmall,out brandIcon);}catch(Exception){}
   FormClosed+=(s,e)=>{if(brandIcon!=null)brandIcon.Dispose();if(brandIconSmall!=null)brandIconSmall.Dispose();};
   var header=Theme.HeaderBand(AppTitle+" – "+Core.L.T(Tagline),"Gỡ ứng dụng, dọn rác, theo dõi mạng  •  Mọi thao tác xóa đều được sao lưu và hoàn tác",96);
   BuildHeaderActions(header);
   status.Text=Core.L.T("Sẵn sàng. Tweek Pro chỉ thay đổi dữ liệu khi bạn xác nhận.");
   BuildTabs();
   appIcons.ColorDepth=ColorDepth.Depth32Bit;appIcons.ImageSize=new Size(32,32);apps.SmallImageList=appIcons;
   FormClosed+=(s,e)=>appIcons.Dispose();

   var installed=new TabPage(Core.L.T("Ứng dụng"));var clean=new TabPage(Core.L.T("Phần còn sót"));var vault=new TabPage(Core.L.T("Kho khôi phục"));var logs=new TabPage(Core.L.T("Nhật ký"));
   tabs.TabPages.AddRange(new[]{installed,clean,vault,logs});

   SetupList(apps,new[]{"Ứng dụng","Phiên bản","Nhà phát hành","Dung lượng *","Phạm vi","Ngày cài / cập nhật","Cảnh báo"},new[]{320,120,220,110,100,150,230},true);
   var appsHost=Theme.ListHost(apps,out appsOverlay);
   var bar=Bar();
   Add(bar,"Làm mới",async()=>await Reload());
   Add(bar,"Gỡ mục đã chọn",async()=>await Uninstall(),ButtonStyle.Primary);
   Add(bar,"Quét mục đang xem",async()=>await ScanSelected());
   Add(bar,"Gỡ cưỡng bức…",async()=>await ForceUninstall());
   Add(bar,"Xuất CSV",()=>{ExportApps();return Task.FromResult(0);});
   deepMode.Text=Core.L.T("Quét sâu sau khi gỡ");deepMode.Checked=settings.DeepScanAfterUninstall;deepMode.CheckedChanged+=(s,e)=>settings.DeepScanAfterUninstall=deepMode.Checked;deepMode.AutoSize=true;deepMode.Margin=new Padding(12,8,16,0);deepMode.ForeColor=Theme.Text;bar.Controls.Add(deepMode);
   pupOnly.Text=Core.L.T("Chỉ hiện mục cảnh báo");pupOnly.AutoSize=true;pupOnly.Margin=new Padding(0,8,16,0);pupOnly.ForeColor=Theme.Text;pupOnly.CheckedChanged+=(s,e)=>Filter();bar.Controls.Add(pupOnly);
   var searchLabel=new Label{Text=Core.L.T("Tìm kiếm"),AutoSize=true,Margin=new Padding(8,9,4,0),ForeColor=Theme.Muted};
   search.Width=240;search.Height=28;search.Margin=new Padding(0,4,0,0);search.Font=Theme.Body;search.BorderStyle=BorderStyle.FixedSingle;search.ForeColor=Theme.Text;search.TextChanged+=(s,e)=>Filter();
   bar.Controls.Add(searchLabel);bar.Controls.Add(search);
   details.Dock=DockStyle.Bottom;details.Height=92;details.Multiline=true;details.ReadOnly=true;details.ScrollBars=ScrollBars.Vertical;details.BackColor=Theme.Stripe;details.ForeColor=Theme.Muted;details.BorderStyle=BorderStyle.None;details.Font=Theme.Small;
   var detailsWrap=new Panel{Dock=DockStyle.Bottom,Height=104,Padding=new Padding(16,10,16,10),BackColor=Theme.Stripe};Theme.BorderTop(detailsWrap);details.Dock=DockStyle.Fill;detailsWrap.Controls.Add(details);
   details.Text=Core.L.T("Chọn một ứng dụng để xem thông tin. Dùng ô tìm kiếm để lọc theo tên hoặc nhà phát hành.");
   apps.SelectedIndexChanged+=(s,e)=>{var a=Selected();var verdict=a==null?null:Pup.PupDetector.Classify(a);details.Text=a==null?Core.L.T("Chọn một ứng dụng để xem thông tin."):a.Name+"  •  "+a.Version+"\r\n"+a.Publisher+"  |  "+Presentation.SizeLabel(a.Size)+"  |  "+Presentation.DateLabel(a.InstallDate)+"\r\n"+Core.L.T("Thư mục: ")+(String.IsNullOrWhiteSpace(a.Location)?Core.L.T("Chưa được ứng dụng khai báo"):a.Location)+"\r\n"+Core.L.T("Ngày do bộ cài cung cấp, có thể là ngày cập nhật. Giá trị gốc: ")+(String.IsNullOrWhiteSpace(a.InstallDate)?Core.L.T("không có"):a.InstallDate)+(verdict==null?"":"\r\n"+Core.L.T("Đánh giá: ")+verdict.Badge+" — "+Pup.PupDetector.SeverityLabel(verdict.Severity)+". "+verdict.Reason);};
   appCount.Dock=DockStyle.Top;appCount.Height=34;appCount.Padding=new Padding(16,0,16,0);appCount.TextAlign=ContentAlignment.MiddleLeft;appCount.BackColor=Theme.Surface;appCount.ForeColor=Theme.Muted;appCount.Font=Theme.Small;Theme.BorderBottom(appCount);
   var pupNote=Theme.Note("Cột Cảnh báo đánh dấu phần mềm khớp quy tắc PUP/bloatware (thanh công cụ, quảng cáo, tối ưu tiếp thị, bảo mật cài sẵn, đi kèm, cài sẵn theo máy). Chỉ là gợi ý chỉ đọc: Tweek Pro không tự gỡ; bạn gỡ bằng nút Gỡ mục đã chọn như mọi ứng dụng khác.",NoteKind.Info);
   installed.Controls.Add(appsHost);installed.Controls.Add(detailsWrap);installed.Controls.Add(appCount);installed.Controls.Add(pupNote);installed.Controls.Add(bar);

   SetupList(remnants,new[]{"Loại","Ứng dụng","Đường dẫn","Cơ sở đề xuất"},new[]{110,200,480,430},true,true);
   var remnantsHost=Theme.ListHost(remnants,out remnantsOverlay);
   var cleanbar=Bar();
   Add(cleanbar,"Quét lại lịch sử gỡ",async()=>await ScanHistory());
   Add(cleanbar,"Quét siêu sâu",async()=>await DeepHistory());
   Add(cleanbar,"Chọn tất cả",()=>{SetRemnantChecks(true);return Task.FromResult(0);});
   Add(cleanbar,"Bỏ chọn tất cả",()=>{SetRemnantChecks(false);return Task.FromResult(0);});
   Add(cleanbar,"Xem mục",()=>{InspectCandidate();return Task.FromResult(0);});
   Add(cleanbar,"Xóa đã chọn (có sao lưu)",async()=>await Cleanup(),ButtonStyle.Danger);
   Add(cleanbar,"Xuất báo cáo",()=>{ExportCandidates();return Task.FromResult(0);});
   var note=Theme.Note("Mục nghi còn sót, chưa chắc thuộc riêng ứng dụng và có thể chứa dữ liệu cá nhân. Chỉ dọn sau khi đăng ký cài đặt đã biến mất; mọi thao tác xóa đều được sao lưu vào kho.",NoteKind.Warning);
   selectionSummary.Dock=DockStyle.Bottom;selectionSummary.Height=34;selectionSummary.Padding=new Padding(16,0,16,0);selectionSummary.TextAlign=ContentAlignment.MiddleLeft;selectionSummary.BackColor=Theme.Surface;selectionSummary.ForeColor=Theme.Muted;selectionSummary.Font=Theme.Small;Theme.BorderTop(selectionSummary);
   remnants.ItemCheck+=(s,e)=>{if(((Candidate)remnants.Items[e.Index].Tag).ReviewOnly)e.NewValue=CheckState.Unchecked;};remnants.ItemChecked+=(s,e)=>UpdateSelectionSummary();
   clean.Controls.Add(remnantsHost);clean.Controls.Add(selectionSummary);clean.Controls.Add(note);clean.Controls.Add(cleanbar);

   SetupList(backups,new[]{"Ngày","Ứng dụng / nhóm","Loại","Trạng thái","Dung lượng","Kho","Đường dẫn gốc"},new[]{150,210,100,140,100,90,420},true);
   backups.MultiSelect=true;backups.ItemChecked+=(s,e)=>UpdateBackupSummary();
   var backupsHost=Theme.ListHost(backups,out backupsOverlay);
   var backupbar=Bar();
   Add(backupbar,"Làm mới kho",()=>{LoadBackups();return Task.FromResult(0);});
   Add(backupbar,"Khôi phục mục đang chọn",async()=>await Restore(),ButtonStyle.Primary);
   Add(backupbar,"Xóa vĩnh viễn mục đã đánh dấu",async()=>await PurgeChecked(),ButtonStyle.Danger);
   Add(backupbar,"Dọn kho theo tuổi…",async()=>await PurgeByAge());
   Add(backupbar,"Mở kho",()=>{Directory.CreateDirectory(Engine.Vault);Process.Start("explorer.exe","\""+Engine.Vault+"\"");return Task.FromResult(0);});
   var backupnote=Theme.Note("Khôi phục đưa file, giá trị Registry hoặc tệp rác về vị trí gốc; không ghi đè đích đã tồn tại. Xóa vĩnh viễn mới giải phóng dung lượng và không thể hoàn tác. Kho cũ của AppCare (nếu còn) vẫn hiển thị và khôi phục được.",NoteKind.Info);
   backupSummary.Dock=DockStyle.Bottom;backupSummary.Height=34;backupSummary.Padding=new Padding(16,0,16,0);backupSummary.TextAlign=ContentAlignment.MiddleLeft;backupSummary.BackColor=Theme.Surface;backupSummary.ForeColor=Theme.Muted;backupSummary.Font=Theme.Small;Theme.BorderTop(backupSummary);
   vault.Controls.Add(backupsHost);vault.Controls.Add(backupSummary);vault.Controls.Add(backupnote);vault.Controls.Add(backupbar);

   log.Dock=DockStyle.Fill;log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Both;log.BorderStyle=BorderStyle.None;log.Font=Theme.Mono;log.BackColor=Theme.Surface;log.ForeColor=Theme.Text;
   var logbar=Bar();
   Add(logbar,"Mở thư mục nhật ký",()=>{Directory.CreateDirectory(Core.Paths.Logs);Process.Start("explorer.exe","\""+Core.Paths.Logs+"\"");return Task.FromResult(0);});
   Add(logbar,"Mở thư mục dữ liệu",()=>{Directory.CreateDirectory(Core.Paths.Root);Process.Start("explorer.exe","\""+Core.Paths.Root+"\"");return Task.FromResult(0);});
   Add(logbar,"Lưu cài đặt ngay",()=>{SaveSettings();Log(Core.L.T("Đã lưu settings.json."));return Task.FromResult(0);});
   var logNote=Theme.Note(Core.L.F("Nhật ký phiên hiện tại. Tệp nhật ký xoay vòng theo ngày trong {0} (giữ {1} ngày). Cài đặt: {2}.",Core.Paths.Logs,settings.LogRetentionDays,Core.Paths.SettingsFile),NoteKind.Info);
   var logWrap=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16,12,16,12),BackColor=Theme.Surface};logWrap.Controls.Add(log);logs.Controls.Add(logWrap);logs.Controls.Add(logNote);logs.Controls.Add(logbar);
   BuildHealthTab();
   BuildAdvancedTabs();
   BuildJunkTab();
   BuildEmptyTab();
   BuildDuplicateTab();
   BuildStaleTab();
   BuildTracksTab();
   BuildRegCleanTab();
   BuildAnalyzerTab();
   BuildSystemSpaceTab();
   BuildNetworkTab();
   BuildStoreTab();
   BuildServicesTab();
   BuildAiTab();
   BuildExtensionsTab();
   BuildExplorerTab();
   BuildListMenus();
   // Canonical tab order: overview → inventory → cleanup family → recovery → system → diagnostics → AI assistant last.
   remnantsTab=clean;
   var ordered=new TabPage[]{healthTab,installed,storeTab,clean,junkTab,emptyTab,dupeTab,staleTab,tracksTab,regTab,analyzerTab,spaceTab,vault,autorunTab,extTab,explorerTab,netTab,servicesTab,toolsTab,logs,aiTab};
   tabs.TabPages.Clear();tabs.TabPages.AddRange(ordered);
   foreach(TabPage page in tabs.TabPages)page.BackColor=Theme.Canvas;
   Controls.Add(tabs);Controls.Add(tabStrip);Controls.Add(header);Controls.Add(status);
   FormClosed+=(s,e)=>SaveSettings();
   int savedWidth=settings.WindowWidth,savedHeight=settings.WindowHeight;bool sizeRestored=false;
   Load+=(s,e)=>{int dpi=ScreenDpi();int w=Logical(savedWidth)*dpi/96,h=Logical(savedHeight)*dpi/96;if(w>=MinimumSize.Width&&h>=MinimumSize.Height)Size=new Size(w,h);sizeRestored=true;};
   Resize+=(s,e)=>{if(sizeRestored&&WindowState==FormWindowState.Normal){int dpi=ScreenDpi();settings.WindowWidth=Width*96/dpi;settings.WindowHeight=Height*96/dpi;}};
   Theme.SetOverlay(appsOverlay,"Đang đọc danh sách ứng dụng…\r\nTweek Pro đọc khóa Uninstall của HKLM/HKCU, không kích hoạt sửa chữa MSI.",NoteKind.Info);
   Theme.SetOverlay(remnantsOverlay,"Chưa có mục còn sót.\r\nSau khi gỡ, cửa sổ quét sẽ chuyển các mục chưa xử lý vào đây. Có thể dùng Quét lại lịch sử gỡ hoặc Quét siêu sâu.",NoteKind.Info);
   Theme.SetOverlay(backupsOverlay,"Chưa có bản sao lưu.\r\nCác mục xóa từ cửa sổ quét hoặc tab Phần còn sót sẽ xuất hiện ở đây để khôi phục.",NoteKind.Info);
   if(!preview) Shown+=async(s,e)=>{await Guard(async()=>await Reload());await AutoCheckForUpdates();};
   FormClosing+=(s,e)=>{if(busy){e.Cancel=true;MessageBox.Show(this,Core.L.T("Đang xử lý. Hãy chờ thao tác hiện tại hoàn tất."));}};
   ResumeLayout(true);
   try{if(File.Exists(sessions))history=Engine.Load<List<AppEntry>>(sessions);}catch(Exception e){Log(Core.L.T("Không đọc được lịch sử: ")+e.Message);}
  }
  /// <summary>
  /// Window size is persisted in logical (96-DPI) pixels and restored in the Load event, after auto-scaling, so it never compounds
  /// across launches on high-DPI screens. Values wider than any plausible logical width (old physical-pixel settings) are treated as unset.
  /// </summary>
  static int Logical(int saved){return saved>0&&saved<=4096?saved:0;}
  /// <summary>Current DPI of the window's screen; works on .NET Framework and Mono alike.</summary>
  int ScreenDpi(){try{using(var g=CreateGraphics())return Math.Max(96,(int)Math.Round(g.DpiX));}catch(Exception){return 96;}}
  /// <summary>Hides the native tab headers and mounts a fixed-order TabStrip above the pages (multiline TabControl reorders its rows on selection).</summary>
  void BuildTabs(){
   tabs.Dock=DockStyle.Fill;tabs.Font=Theme.Body;TabStrip.HideNativeHeaders(tabs);
   tabStrip=new TabStrip(tabs);
  }
  FlowLayoutPanel Bar(){return Theme.Toolbar();}
  /// <summary>Adds a guarded action button to a toolbar; the style marks the primary or destructive action.</summary>
  void Add(FlowLayoutPanel bar,string text,Func<Task> action,ButtonStyle style=ButtonStyle.Secondary){
   var b=Theme.Button(text,style);b.Margin=new Padding(0,0,8,8);
   b.Click+=async(s,e)=>await Guard(action);bar.Controls.Add(b);actions.Add(b);
  }
  void SetupList(ListView list,string[] names,int[] widths,bool check,bool groups=false){Theme.StyleList(list);list.CheckBoxes=check;list.ShowGroups=groups;for(int i=0;i<names.Length;i++)list.Columns.Add(Core.L.T(names[i]),widths[i]);}
  async Task Guard(Func<Task> action){if(busy)return;busy=true;foreach(var b in actions)b.Enabled=false;deepMode.Enabled=false;search.Enabled=false;apps.Enabled=false;remnants.Enabled=false;backups.Enabled=false;try{await action();}catch(Exception e){Log(Core.L.T("LỖI: ")+e.Message);MessageBox.Show(this,e.Message,"Tweek Pro",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{busy=false;foreach(var b in actions)b.Enabled=true;deepMode.Enabled=true;search.Enabled=true;apps.Enabled=true;remnants.Enabled=true;backups.Enabled=true;}}
  void Log(string value){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+value+"\r\n");status.Text=value;if(value.StartsWith("LỖI",StringComparison.OrdinalIgnoreCase)||value.StartsWith("ERROR",StringComparison.OrdinalIgnoreCase))Core.Log.Error(value);else Core.Log.Info(value);}
  /// <summary>Persists settings.json; failures are logged, never shown as blocking errors.</summary>
  void SaveSettings(){try{settings.Save(Core.Paths.SettingsFile);}catch(Exception e){Core.Log.Warn("Không lưu được settings.json: "+e.Message);}}
  /// <summary>Adds the privilege badge and the compact language toggle to the right side of the header band.</summary>
  void BuildHeaderActions(Panel header){
   var right=new FlowLayoutPanel{Dock=DockStyle.Right,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(16,22,0,0),BackColor=Theme.Header};
   bool elevated=Core.Elevation.IsElevated;
   var badge=new Label{Text=Core.Elevation.BadgeText,AutoSize=true,Padding=new Padding(10,6,10,6),Margin=new Padding(8,4,0,0),Font=Theme.Small,ForeColor=Color.White,BackColor=elevated?Theme.Success:Color.FromArgb(51,65,85)};
   right.Controls.Add(badge);
   var language=Branding.LanguageButton(Core.L.English?"EN":"VI",Core.L.T(Core.L.English?"Chuyển sang tiếng Việt":"Switch to English"));language.Margin=new Padding(8,2,0,0);
   language.Click+=async(s,e)=>await Guard(()=>{SwitchLanguage();return Task.FromResult(0);});
   right.Controls.Add(language);
   header.Controls.Add(right);right.Width=right.PreferredSize.Width;
  }
  /// <summary>Toggles between Vietnamese and English, saves the preference and relaunches so every tab is rebuilt in the new language.</summary>
  void SwitchLanguage(){
   bool toEnglish=!Core.L.English;
   if(!Confirm(Core.L.T(toEnglish?"Đổi ngôn ngữ sang tiếng Anh và khởi động lại Tweek Pro?\r\n\r\nCài đặt và kho khôi phục được giữ nguyên.":"Đổi ngôn ngữ sang tiếng Việt và khởi động lại Tweek Pro?\r\n\r\nCài đặt và kho khôi phục được giữ nguyên.")))return;
   settings.Language=toEnglish?Core.L.EnglishCode:Core.L.Vietnamese;SaveSettings();
   Log(toEnglish?"Language set to English; restarting.":"Đã đặt ngôn ngữ tiếng Việt; khởi động lại.");
   try{System.Diagnostics.Process.Start(Application.ExecutablePath);}catch(Exception ex){Log(Core.L.T("LỖI: ")+ex.Message);return;}
   BeginInvoke((Action)Close);
  }
  bool Confirm(string text){return MessageBox.Show(this,text,Core.L.T("Xác nhận thao tác"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;}
  AppEntry Selected(){return apps.SelectedItems.Count==0?null:(AppEntry)apps.SelectedItems[0].Tag;}
  List<AppEntry> CheckedApps(){var list=apps.CheckedItems.Cast<ListViewItem>().Select(i=>(AppEntry)i.Tag).ToList();if(list.Count==0&&Selected()!=null)list.Add(Selected());return list;}
  public void PopulateForPreview(){inventory=Engine.Inventory();if(inventory.Count==0)inventory=SampleInventory();else Sizing.InstallSize.Apply(inventory,TimeSpan.FromSeconds(10));LoadAppIcons();Filter();LoadBackups();}
  /// <summary>Selects a tab that has no sample data of its own so --preview all still captures its empty-state guidance.</summary>
  public void PreviewTab(string key){TabPage t=key=="empty"?emptyTab:key=="dupes"?dupeTab:key=="analyzer"?analyzerTab:tabs.TabPages.Cast<TabPage>().FirstOrDefault(p=>p.Text.StartsWith(Core.L.T(key=="vault"?"Kho khôi phục":"Nhật ký"),StringComparison.Ordinal));if(t!=null)tabs.SelectedTab=t;}
  /// <summary>Representative applications for layout previews on systems without a Windows registry.</summary>
  static List<AppEntry> SampleInventory(){
   return new List<AppEntry>{
    new AppEntry{Name="Adobe Acrobat (64-bit)",Version="26.002.21931",Publisher="Adobe",Size=2662400,Location=@"C:\Program Files\Adobe\Acrobat DC\",Command=@"C:\Program Files\Adobe\Acrobat DC\Acrobat\Setup.exe /uninstall",Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Acrobat",InstallDate="20260918"},
    new AppEntry{Name="Brave",Version="153.1.95.104",Publisher="Brave Software Inc",Size=551600,Location=@"C:\Program Files\BraveSoftware\Brave-Browser\Application",Command=@"C:\Program Files\BraveSoftware\Brave-Browser\Application\153.1.95.104\Installer\setup.exe --uninstall",Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BraveSoftware Brave-Browser",InstallDate="20260919"},
    new AppEntry{Name="CapCut",Version="9.4.0.4015",Publisher="Bytedance Pte. Ltd.",Size=0,Location="",Command=@"C:\Users\ADMIN\AppData\Local\CapCut\Apps\uninstall.exe",Hive="HKCU",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CapCut",InstallDate=""},
    new AppEntry{Name="MySQL Server 8.0",Version="8.0.39",Publisher="Oracle Corporation",Size=612300,Location=@"C:\Program Files\MySQL\MySQL Server 8.0\",Command="MsiExec.exe /X{1A2B3C4D-0000-0000-0000-000000000001}",Msi=true,Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{1A2B3C4D-0000-0000-0000-000000000001}",InstallDate="20260701"},
    new AppEntry{Name="McAfee LiveSafe",Version="16.0 R70",Publisher="McAfee, LLC",Size=1048576,Location=@"C:\Program Files\McAfee\",Command=@"C:\Program Files\McAfee\MSC\mcuihost.exe /body:misp://MSCJsRes.dll::uninstall.html",Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MSC",InstallDate="20260101"},
    new AppEntry{Name="Driver Booster 11",Version="11.2.0.46",Publisher="IObit",Size=204800,Location=@"C:\Program Files (x86)\IObit\Driver Booster\",Command=@"C:\Program Files (x86)\IObit\Driver Booster\unins000.exe",Hive="HKLM",View="32",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Driver Booster_is1",InstallDate="20260315"},
   };
  }
  /// <summary>Shows the Applications tab (sample inventory carries two PUP-flagged rows) for --preview apps.</summary>
  public void PreviewApps(){tabs.SelectedIndex=1;}
  public void PreviewRemnants(){PresentCandidates(new[]{new Candidate{Kind="Folder",AppName="Ứng dụng mẫu",Path=@"C:\Program Files\Example App",Reason="Dữ liệu minh họa giao diện, không phải kết quả quét."},new Candidate{Kind="Registry",AppName="Ứng dụng mẫu",Path=@"SOFTWARE\Example App",Hive="HKCU",View="64",Reason="Dữ liệu minh họa giao diện."}});tabs.SelectedTab=remnantsTab;SetRemnantChecks(true);}
  async Task Reload(){
   Theme.SetOverlay(appsOverlay,"Đang đọc danh sách ứng dụng…\r\nTweek Pro đọc khóa Uninstall của HKLM/HKCU, không kích hoạt sửa chữa MSI.",NoteKind.Info);
   Log(Core.L.T("Đang đọc danh sách ứng dụng…"));
   try{
    inventory=await Task.Run(()=>Engine.Inventory());inventoryError=null;LoadAppIcons();Filter();LoadBackups();
    Log(Core.L.F("Đã đọc {0} ứng dụng desktop. Chưa bao gồm toàn bộ ứng dụng Microsoft Store.",inventory.Count));
    var measured=inventory;int filled=await Task.Run(()=>Sizing.InstallSize.Apply(measured,TimeSpan.FromSeconds(20)));
    if(filled>0&&ReferenceEquals(measured,inventory)){Filter();Log(Core.L.F("Đã bổ sung dung lượng cho {0} ứng dụng từ Steam hoặc thư mục cài.",filled));}
   }catch(Exception e){
    inventoryError=e.Message;inventory=new List<AppEntry>();LoadAppIcons();Filter();
    throw;
   }
  }
  void LoadAppIcons(){appIcons.Images.Clear();var imageHandle=appIcons.Handle;foreach(var a in inventory){using(var bitmap=Presentation.AppIcon(a))appIcons.Images.Add(a.Id,bitmap);}}
  void Filter(){
   if(inventoryError!=null){
    apps.BeginUpdate();apps.Items.Clear();apps.EndUpdate();appCount.Text=Core.L.T("Không đọc được danh sách ứng dụng");
    Theme.SetOverlay(appsOverlay,Core.L.F("Không đọc được danh sách ứng dụng.\r\n{0}\r\n\r\nBấm Làm mới để thử lại. Nếu khóa HKLM bị chặn, chạy Tweek Pro với quyền quản trị cùng tài khoản Windows.",inventoryError),NoteKind.Error);
    return;
   }
   var checkedIds=new HashSet<string>(apps.CheckedItems.Cast<ListViewItem>().Select(i=>((AppEntry)i.Tag).Id));apps.BeginUpdate();apps.Items.Clear();details.Clear();string q=search.Text.Trim();bool onlyPup=pupOnly.Checked;int flagged=0;foreach(var a in inventory.Where(a=>(a.Name+" "+a.Publisher).IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0)){
   var verdict=Pup.PupDetector.Classify(a);if(verdict!=null)flagged++;if(onlyPup&&verdict==null)continue;
   string location=String.IsNullOrWhiteSpace(a.Location)?Core.L.T("Chưa được ứng dụng khai báo thư mục cài"):a.Location;
   string flag=verdict==null?"":verdict.Badge+" • "+Pup.PupDetector.SeverityLabel(verdict.Severity);
   string sizeNote=!a.SizeMeasured?"":Sizing.SteamLibrary.AppIdOf(a.Key,a.Command)>0?"\r\n"+Core.L.T("Dung lượng do Steam ghi nhận; thư mục thật: ")+a.Location:"\r\n"+Core.L.T(a.SizePartial?"Dung lượng đo chưa đủ — thư mục quá lớn hoặc đọc quá lâu.":"Dung lượng đo từ thư mục cài (bộ cài không khai báo).")+(String.IsNullOrWhiteSpace(a.Location)?" "+Sizing.InstallSize.FolderOf(a):"");
   var item=new ListViewItem(new[]{a.Name,a.Version,a.Publisher,Presentation.SizeLabel(a.Size)+(a.SizePartial?"+":""),a.Hive=="HKLM"?Core.L.T("Toàn máy"):Core.L.T("Tài khoản"),Presentation.DateLabel(a.InstallDate),flag}){Tag=a,ImageKey=a.Id,Checked=checkedIds.Contains(a.Id),ToolTipText=location+sizeNote+"\r\n"+Core.L.T("Ngày bộ cài khai báo: ")+(String.IsNullOrWhiteSpace(a.InstallDate)?Core.L.T("Không có"):a.InstallDate)+(verdict==null?"":"\r\n\r\n"+verdict.Badge+": "+verdict.Reason)};
   if(verdict!=null)item.ForeColor=verdict.Severity==Pup.PupSeverity.High?Theme.Danger:Theme.Warning;
   Theme.StripeRow(item,apps.Items.Count);apps.Items.Add(item);
  }apps.EndUpdate();appCount.Text=Core.L.F("{0} ứng dụng hiển thị  /  {1} ứng dụng trên máy",apps.Items.Count,inventory.Count)+(flagged>0?"  •  "+Core.L.F("{0} mục nghi không mong muốn",flagged):"");
   if(inventory.Count==0)Theme.SetOverlay(appsOverlay,"Không tìm thấy ứng dụng desktop nào.\r\nTweek Pro đọc các khóa Uninstall của HKLM/HKCU; chưa gồm toàn bộ ứng dụng Microsoft Store.",NoteKind.Info);
   else if(apps.Items.Count==0&&onlyPup&&q=="")Theme.SetOverlay(appsOverlay,"Không có ứng dụng nào bị đánh dấu không mong muốn.\r\nBỏ chọn «Chỉ hiện mục cảnh báo» để xem toàn bộ.",NoteKind.Info);
   else if(apps.Items.Count==0)Theme.SetOverlay(appsOverlay,Core.L.F("Không có ứng dụng khớp với «{0}».\r\nThử từ khóa khác hoặc xóa ô tìm kiếm.",q),NoteKind.Info);
   else Theme.SetOverlay(appsOverlay,null,NoteKind.Info);
  }
  void Remember(IEnumerable<AppEntry> entries){foreach(var a in entries){history.RemoveAll(x=>x.Id==a.Id);history.Add(a);}Directory.CreateDirectory(Path.GetDirectoryName(sessions));Engine.Save(sessions,history);}
  async Task Uninstall(){
   var queue=CheckedApps();if(queue.Count==0)throw new IOException(Core.L.T("Chọn ứng dụng muốn gỡ."));
   if(!Confirm(Core.L.F("Mở trình gỡ chính thức lần lượt cho {0} ứng dụng?\r\n\r\n{1}\r\n\r\nTrình gỡ có thể xóa dữ liệu và yêu cầu quyền quản trị. Kho Tweek Pro chỉ khôi phục phần dọn sau đó, không hoàn tác trình gỡ.",queue.Count,String.Join("\r\n",queue.Take(12).Select(a=>a.Name)))))return;
   foreach(var entry in queue)Advanced.Capture(entry);Remember(queue);candidates.Clear();RenderCandidates();
   for(int i=0;i<queue.Count;i++){
    var a=queue[i];if(i>0&&!Confirm(Core.L.F("Tiếp tục gỡ {0}?\r\nChọn No để dừng hàng đợi.",a.Name)))break;
    try{
     var info=Engine.UninstallInfo(a);Log(Core.L.T("Mở trình gỡ: ")+a.Name);
     using(var process=Process.Start(info)){if(process!=null)await Task.Run(()=>process.WaitForExit());}
     bool remains=Engine.Installed(a.Id);
     if(remains){
      await Task.Delay(1500);remains=Engine.Installed(a.Id);
      if(remains){MessageBox.Show(this,Core.L.F("Trình gỡ của {0} đã kết thúc tiến trình chính nhưng ứng dụng vẫn còn đăng ký. Nếu có cửa sổ gỡ khác, hãy hoàn tất hoặc hủy rồi nhấn OK. Nếu cần khởi động lại, có thể quét lại lịch sử sau đó.",a.Name),Core.L.T("Kiểm tra trình gỡ"),MessageBoxButtons.OK,MessageBoxIcon.Information);remains=Engine.Installed(a.Id);}
     }
     Log(a.Name+": "+Core.L.T(remains?"vẫn còn đăng ký cài đặt; không tự quét/dọn.":"đã bỏ đăng ký; mở cửa sổ quét phần còn sót."));
     if(!remains)ReviewLeftovers(new[]{a},deepMode.Checked);
    }catch(Exception e){Log(a.Name+": "+e.Message);MessageBox.Show(this,a.Name+"\r\n"+e.Message,Core.L.T("Không hoàn tất thao tác"));}
   }
   await Reload();
   if(candidates.Count>0)tabs.SelectedTab=remnantsTab;
   Log(Core.L.F("Đã kết thúc hàng đợi. Còn {0} mục chờ duyệt ở tab Phần còn sót.",candidates.Count));
  }
  /// <summary>Opens the dedicated leftover window for the given applications and merges what the user left behind into the review tab.</summary>
  void ReviewLeftovers(AppEntry[] entries,bool deep){
   using(var window=new LeftoverScanForm(entries,deep,Log)){
    window.ShowDialog(this);
    PresentCandidates(candidates.Concat(window.Remaining));
    LoadBackups();
    Log(String.Join(", ",entries.Select(a=>a.Name))+Core.L.F(": tìm thấy {0} mục, đã xóa {1}, lỗi {2}, còn {3} mục chờ duyệt.",window.Found,window.Deleted,window.Failed,window.Remaining.Count));
   }
  }
  async Task ScanSelected(){var a=Selected();if(a==null)throw new IOException(Core.L.T("Chọn một dòng ứng dụng trước."));await Task.Run(()=>Advanced.Capture(a));Remember(new[]{a});ReviewLeftovers(new[]{a},deepMode.Checked);if(candidates.Count>0)tabs.SelectedTab=remnantsTab;}
  async Task ScanHistory(){if(history.Count==0)throw new IOException(Core.L.T("Chưa có lịch sử. Hãy chọn ứng dụng và quét hoặc gỡ trước."));await ScanEntries(history.ToArray());}
  async Task ScanEntries(IEnumerable<AppEntry> entries,bool append=false){
   var input=entries.ToArray();Log(Core.L.F("Đang quét {0} ứng dụng…",input.Length));
   var found=await Task.Run(()=>input.SelectMany(a=>Engine.Scan(a)).ToList());
   PresentCandidates(append?candidates.Concat(found):found);tabs.SelectedTab=remnantsTab;
   Log(Core.L.F("Tìm thấy {0} mục cần duyệt. Không tìm thấy không có nghĩa đã sạch toàn bộ.",candidates.Count));
  }
  internal void PresentCandidates(IEnumerable<Candidate> items){
   candidates=items.GroupBy(Advanced.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();RenderCandidates();
  }
  internal int CheckedRemnantCount { get { return remnants.CheckedItems.Count; } }
  internal void SetRemnantChecks(bool check){remnants.BeginUpdate();foreach(ListViewItem item in remnants.Items)item.Checked=check&&!((Candidate)item.Tag).ReviewOnly;remnants.EndUpdate();UpdateSelectionSummary();}
  void UpdateSelectionSummary(){selectionSummary.Text=Core.L.F("Tìm thấy: {0} mục  •  Đã chọn: {1}  •  Xóa sẽ lưu bản khôi phục trước.",remnants.Items.Count,remnants.CheckedItems.Count);}
  void RenderCandidates(){
   remnants.BeginUpdate();remnants.Items.Clear();remnants.Groups.Clear();
   foreach(var c in candidates.OrderBy(Presentation.KindGroup).ThenBy(Presentation.CandidatePath,StringComparer.OrdinalIgnoreCase)){
    var row=new ListViewItem(new[]{Presentation.KindLabel(c),c.AppName,Presentation.CandidatePath(c),(c.ReviewOnly?Core.L.T("CHỈ XEM • "):"")+c.Reason}){Tag=c,ToolTipText=Presentation.CandidatePath(c)+"\r\n"+c.Reason};
    if(c.ReviewOnly)row.ForeColor=Theme.Muted;
    Theme.AssignGroup(remnants,row,Presentation.KindGroup(c),Presentation.KindGroupHeader(Presentation.KindGroup(c)));
    Theme.StripeRow(row,remnants.Items.Count);remnants.Items.Add(row);
   }
   remnants.EndUpdate();UpdateSelectionSummary();
   if(candidates.Count==0)Theme.SetOverlay(remnantsOverlay,"Chưa có mục còn sót.\r\nSau khi gỡ, cửa sổ quét sẽ chuyển các mục chưa xử lý vào đây. Có thể dùng Quét lại lịch sử gỡ hoặc Quét siêu sâu.",NoteKind.Info);
   else Theme.SetOverlay(remnantsOverlay,null,NoteKind.Info);
  }
  void InspectCandidate(){
   if(remnants.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một mục để xem."));var c=(Candidate)remnants.SelectedItems[0].Tag;
   if(c.Kind=="Folder"){Engine.ValidateFolder(c.Path,false);Process.Start("explorer.exe","\""+c.Path+"\"");}
   else if(c.Kind=="File"){Advanced.ValidateFile(c.Path);Process.Start("explorer.exe","/select,\""+c.Path+"\"");}
   else MessageBox.Show(this,Presentation.CandidatePath(c)+"\r\n\r\n"+c.Reason+Core.L.T(c.ReviewOnly?"\r\n\r\nChỉ xem: không tự xóa mục này.":"\r\n\r\nKiểm tra đúng khóa hoặc giá trị trước khi chọn dọn."),Core.L.T("Chi tiết mục còn sót"));
  }
  async Task Cleanup(){
   var selected=remnants.CheckedItems.Cast<ListViewItem>().Select(i=>(Candidate)i.Tag).Where(c=>!c.ReviewOnly).ToList();if(selected.Count==0)throw new IOException(Core.L.T("Đánh dấu những mục đã kiểm tra và muốn dọn."));
   if(!Confirm(Core.L.F("Sao lưu và dọn {0} mục đã đánh dấu?\r\n\r\n{1}\r\n\r\nCác mục có thể chứa cài đặt, dự án hoặc dữ liệu cá nhân. Chỉ tiếp tục khi đã kiểm tra. File được chuyển vào kho nên chưa giải phóng dung lượng ổ đĩa. Registry được lưu giá trị và khóa con, không lưu quyền ACL.",selected.Count,String.Join("\r\n",selected.Take(8).Select(c=>c.Path)))))return;
   int removedFolders=0,removedKeys=0,failed=0,scheduled=0;var stubborn=new List<Candidate>();
   foreach(var c in selected){try{Log(Core.L.T("Đang sao lưu: ")+c.Path);await Task.Run(()=>Engine.Quarantine(c));Log(Core.L.T("Đã sao lưu và dọn: ")+c.Path);candidates.Remove(c);if(c.Kind=="Folder"||c.Kind=="File")removedFolders++;else removedKeys++;}catch(Exception e){failed++;Log(Core.L.T("Giữ lại / cần kiểm tra ")+c.Path+": "+e.Message);if((c.Kind=="Folder"||c.Kind=="File")&&(Core.StubbornFiles.IsLocked(e)||Core.StubbornFiles.IsAccessDenied(e)))stubborn.Add(c);else MessageBox.Show(this,c.Path+"\r\n"+e.Message,Core.L.T("Mục chưa dọn xong"));}}
   if(stubborn.Count>0&&Core.StubbornFiles.IsWindows&&Confirm(Core.L.F("{0} mục vẫn bị Windows chặn dù đã bỏ thuộc tính và chiếm quyền sở hữu (thường do một tiến trình đang giữ tệp):\r\n\r\n{1}\r\n\r\nHẹn xóa các mục này khi khởi động lại Windows? Chúng sẽ bị xóa thẳng lúc khởi động, KHÔNG có bản sao lưu để khôi phục.",stubborn.Count,String.Join("\r\n",stubborn.Take(8).Select(c=>c.Path))))){
    foreach(var c in stubborn){try{await Task.Run(()=>Engine.ScheduleOnReboot(c));scheduled++;failed--;candidates.Remove(c);Log(Core.L.T("Đã hẹn xóa khi khởi động lại: ")+c.Path);}catch(Exception e){Log(Core.L.T("Không hẹn xóa được ")+c.Path+": "+e.Message);MessageBox.Show(this,c.Path+"\r\n"+e.Message,Core.L.T("Mục chưa dọn xong"));}}
   }else if(stubborn.Count>0)foreach(var c in stubborn)MessageBox.Show(this,c.Path+"\r\n"+Core.L.T("Vẫn bị chặn sau khi chiếm quyền sở hữu; có thể đang bị một tiến trình giữ. Đóng ứng dụng liên quan rồi thử lại."),Core.L.T("Mục chưa dọn xong"));
   RenderCandidates();LoadBackups();
   string summary=Core.L.F("Đã dọn và sao lưu: {0} mục file/thư mục, {1} mục Registry.",removedFolders,removedKeys)+(scheduled>0?Core.L.F(" Hẹn xóa khi khởi động lại: {0} mục.",scheduled):"")+Core.L.F(" Chưa dọn: {0} mục. Còn hiển thị: {1} mục.",failed,candidates.Count);
   Log(summary);MessageBox.Show(this,summary+Core.L.T("\r\n\r\nCó thể khôi phục trong Kho khôi phục. File trong kho vẫn chiếm dung lượng."),Core.L.T("Kết quả dọn"),MessageBoxButtons.OK,MessageBoxIcon.Information);
  }
  static string StateLabel(Backup b){return Core.L.T(b.State=="BackedUp"?"Đã sao lưu":b.State=="Restored"?"Đã khôi phục":b.State=="PendingReboot"?"Hẹn xóa khi khởi động lại":"Cần kiểm tra");}
  static string KindLabel(Backup b){return b.Kind==Explorer.ExplorerTweaks.BackupKind?"Explorer":b.Kind==Network.FirewallBlock.BackupKind?Core.L.T("Chặn mạng"):b.Kind=="Store"?Core.L.T("App Windows"):b.Kind=="Service"?Core.L.T("Dịch vụ"):b.Kind==Startup.StartupSources.BackupKind?Core.L.T("Khởi động ứng dụng"):b.Purpose==Startup.StartupInspector.ApprovalPurpose?Core.L.T("Cờ khởi động"):b.Kind=="Junk"?Core.L.T("Rác"):b.Kind==Stale.StaleFinder.BackupKind?Stale.StaleFinder.BackupKindLabel():b.Kind==Tracks.TracksCleaner.BackupKind?Tracks.TracksCleaner.BackupKindLabel():b.Kind==RegClean.RegistryCleaner.BackupKind?RegClean.RegistryCleaner.BackupKindLabel():b.Kind=="Duplicate"?Core.L.T("Bản trùng"):b.Kind=="Folder"?Core.L.T("Thư mục"):b.Kind=="File"?Core.L.T("Tệp"):b.Kind=="RegistryValue"?Core.L.T("Giá trị Registry"):b.Kind=="Registry"?Core.L.T("Khóa Registry"):b.Kind;}
  int backupsLoadToken;
  /// <summary>Lists both vaults, then measures sizes on a worker thread and fills the size column as results arrive.</summary>
  void LoadBackups(){
   var items=Engine.Backups();int token=++backupsLoadToken;
   backups.BeginUpdate();backups.Items.Clear();
   foreach(var b in items){var row=new ListViewItem(new[]{Presentation.StampLabel(b.Created),b.AppName,KindLabel(b),StateLabel(b),"…",b.VaultPath==null?"Tweek Pro":Core.L.T("AppCare (cũ)"),b.Original}){Tag=b,ToolTipText=b.Original+"\r\n"+Path.Combine(Engine.VaultOf(b),b.Id)+(String.IsNullOrEmpty(b.Error)?"":"\r\n"+b.Error)};if(b.State=="Restored")row.ForeColor=Theme.Muted;else if(b.State=="PendingReboot")row.ForeColor=Theme.Warning;else if(b.State!="BackedUp")row.ForeColor=Theme.Danger;Theme.StripeRow(row,backups.Items.Count);backups.Items.Add(row);}
   backups.EndUpdate();UpdateBackupSummary();
   if(items.Count==0)Theme.SetOverlay(backupsOverlay,"Chưa có bản sao lưu.\r\nCác mục xóa từ cửa sổ quét, tab Phần còn sót hoặc Dọn rác sẽ xuất hiện ở đây để khôi phục hoặc xóa vĩnh viễn.",NoteKind.Info);
   else Theme.SetOverlay(backupsOverlay,null,NoteKind.Info);
   if(items.Count>0)Task.Run(()=>{
    foreach(var b in items){if(token!=backupsLoadToken||IsDisposed)return;long bytes=Engine.BackupSize(b);var target=b;
     try{BeginInvoke((Action)(()=>{if(token!=backupsLoadToken||IsDisposed)return;target.Bytes=bytes;foreach(ListViewItem row in backups.Items)if(row.Tag==target){row.SubItems[4].Text=Presentation.BytesLabel(bytes);break;}UpdateBackupSummary();}));}catch(InvalidOperationException){return;}
    }
   });
  }
  void UpdateBackupSummary(){
   var all=backups.Items.Cast<ListViewItem>().Select(i=>(Backup)i.Tag).ToList();var marked=backups.CheckedItems.Cast<ListViewItem>().Select(i=>(Backup)i.Tag).ToList();
   long total=all.Where(b=>b.Bytes>0).Sum(b=>b.Bytes),markedBytes=marked.Where(b=>b.Bytes>0).Sum(b=>b.Bytes);
   backupSummary.Text=Core.L.F("{0} bản sao lưu ({1} trên đĩa)  •  Đã đánh dấu {2} ({3})  •  Khôi phục dùng dòng đang chọn; xóa vĩnh viễn dùng ô đánh dấu.",all.Count,Presentation.BytesLabel(total),marked.Count,Presentation.BytesLabel(markedBytes));
  }
  async Task Restore(){if(backups.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một bản sao lưu."));var b=(Backup)backups.SelectedItems[0].Tag;if(b.State=="Restored")throw new IOException(Core.L.T("Mục này đã khôi phục."));if(!Confirm(Core.L.F("Khôi phục về vị trí gốc?\r\n{0}\r\n\r\nKhông ghi đè nếu đích đã tồn tại. Nếu lần dọn trước bị gián đoạn, kiểm tra cả vị trí gốc và kho.",b.Original)))return;await Task.Run(()=>Engine.Restore(b));LoadBackups();Log(Core.L.T("Đã khôi phục: ")+b.Original);}
  /// <summary>Permanently deletes the checked backups after a size-aware confirmation and reports the outcome.</summary>
  async Task PurgeChecked(){
   var marked=backups.CheckedItems.Cast<ListViewItem>().Select(i=>(Backup)i.Tag).ToList();
   if(marked.Count==0)throw new IOException(Core.L.T("Đánh dấu (tick) các bản sao lưu muốn xóa vĩnh viễn."));
   await PurgeBackups(marked,Core.L.T("đã đánh dấu"));
  }
  /// <summary>Opens the age-based purge dialog, then permanently deletes what the user confirmed.</summary>
  async Task PurgeByAge(){
   var all=Engine.Backups();if(all.Count==0)throw new IOException(Core.L.T("Kho trống."));
   foreach(var b in all)if(b.Bytes<0)b.Bytes=await Task.Run(()=>Engine.BackupSize(b));
   List<Backup> selected;
   using(var dialog=new PurgeForm(all,settings.PurgeDefaultDays)){if(dialog.ShowDialog(this)!=DialogResult.OK)return;selected=dialog.Selected;settings.PurgeDefaultDays=dialog.Days;}
   if(selected.Count==0)throw new IOException(Core.L.T("Không có bản sao lưu nào đủ tuổi theo lựa chọn."));
   await PurgeBackups(selected,Core.L.F("cũ hơn {0} ngày",settings.PurgeDefaultDays));
  }
  async Task PurgeBackups(List<Backup> selected,string label){
   long bytes=selected.Where(b=>b.Bytes>0).Sum(b=>b.Bytes);int unrestored=selected.Count(b=>b.State!="Restored");
   var answer=MessageBox.Show(this,Core.L.F("XÓA VĨNH VIỄN {0} bản sao lưu {1} ({2})?\r\n\r\n{3}",selected.Count,label,Presentation.BytesLabel(bytes),String.Join("\r\n",selected.Take(8).Select(b=>Presentation.StampLabel(b.Created)+"  "+b.AppName+"  •  "+StateLabel(b))))+(selected.Count>8?Core.L.F("\r\n… và {0} mục khác",selected.Count-8):"")+(unrestored>0?Core.L.F("\r\n\r\nCẢNH BÁO: {0} bản chưa khôi phục sẽ mất vĩnh viễn — không thể hoàn tác các lần dọn tương ứng nữa.",unrestored):"")+Core.L.T("\r\n\r\nThao tác này không thể hoàn tác."),Core.L.T("Xác nhận xóa vĩnh viễn"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
   if(answer!=DialogResult.Yes)return;
   int ok=0,failed=0;long freed=0;
   foreach(var b in selected){try{freed+=await Task.Run(()=>Engine.Purge(b));ok++;Log(Core.L.F("Đã xóa vĩnh viễn bản sao lưu {0} ({1}).",b.Id,b.AppName));}catch(Exception e){failed++;Log(Core.L.F("Không xóa được {0}: {1}",b.Id,e.Message));}}
   LoadBackups();
   string summary=Core.L.F("Đã xóa vĩnh viễn {0} bản sao lưu, giải phóng {1}. Lỗi: {2}.",ok,Presentation.BytesLabel(freed),failed);
   Log(summary);MessageBox.Show(this,summary,Core.L.T("Kết quả dọn kho"),MessageBoxButtons.OK,failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
  }
  string SavePath(string name){using(var dialog=new SaveFileDialog{Filter="CSV UTF-8|*.csv",FileName=name})return dialog.ShowDialog(this)==DialogResult.OK?dialog.FileName:null;}
  void ExportApps(){string p=SavePath("TweekPro-applications.csv");if(p==null)return;var lines=new List<string>{"Name,Version,Publisher,SizeKB,RegistryView,InstallLocation,UninstallCommand"};lines.AddRange(inventory.Select(a=>String.Join(",",new[]{a.Name,a.Version,a.Publisher,a.Size.ToString(),a.Hive+"/"+a.View,a.Location,a.Command}.Select(Engine.Csv))));File.WriteAllLines(p,lines, new UTF8Encoding(true));Log(Core.L.T("Đã xuất danh sách: ")+p);}
  void ExportCandidates(){string p=SavePath("TweekPro-review.csv");if(p==null)return;var lines=new List<string>{"App,Kind,Path,Hive,View,Reason"};lines.AddRange(candidates.Select(c=>String.Join(",",new[]{c.AppName,c.Kind,Presentation.CandidatePath(c),c.Hive,c.View,(c.ReviewOnly?Core.L.T("CHỈ XEM: "):"")+c.Reason}.Select(Engine.Csv))));File.WriteAllLines(p,lines,new UTF8Encoding(true));Log(Core.L.T("Đã xuất báo cáo: ")+p);}
 }
 public static class Program {
  /// <summary>Migrates the AppCare data folder once, then points the rotating log at the Tweek Pro data directory.</summary>
  static void Bootstrap(){
   var migration=Core.Paths.Migrate();
   var settings=Core.Settings.Load(Core.Paths.SettingsFile);
   Core.L.Lang=settings.Language;
   Core.Log.Configure(Core.Paths.Logs,settings.LogRetentionDays);
   Core.Log.Info("Tweek Pro "+MainForm.Version+" khởi động. Quyền: "+Core.Elevation.BadgeText+".");
   if(migration.Outcome==Core.MigrationOutcome.Moved)Core.Log.Info("Đã chuyển dữ liệu từ "+migration.Source+" sang "+migration.Target+".");
   else if(migration.Outcome==Core.MigrationOutcome.Failed)Core.Log.Warn("Không chuyển được thư mục AppCare cũ ("+migration.Error+"); kho cũ vẫn được đọc tại "+Core.Paths.LegacyBackups+".");
  }
  [STAThread]public static void Main(string[] args){
   // Handle the headless self-test before any WinForms initialization so it needs no X display / desktop session.
   if(args.Length>1&&args[0]=="--export-icon"){try{Branding.WriteIconFile(args[1]);}catch(Exception error){Console.Error.WriteLine(error.Message);Environment.ExitCode=1;}return;}
   if(args.Length>0&&args[0]=="--self-test"){
    bool coreOnly=args.Length>1&&(args[1]=="core"||args[1]=="junk");string results=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt");
    try{
     CoreTests.Run();
     if(args.Length>1&&args[1]=="junk")Tests07.Run();
     if(!coreOnly){Tests.Run();AdvancedTests.Run();Tests07.Run();}
     bool junkMode=args.Length>1&&args[1]=="junk";
     File.WriteAllText(results,(junkMode?"PASS (core + junk cleaner/vault/purge, no registry/UI): ":coreOnly?"PASS (core only): ":"PASS: ")+"junk rule parsing/expansion/glob/safety, purge selection by age, data-dir migration, settings round-trip, log rotation, stored-name mapping"+(junkMode?", junk preview/clean/restore via vault, direct delete, in-use skip, locked rule, vault purge, legacy vault listing, duplicate finder scan/quarantine/restore, disk analyzer totals/folders/extensions/largest, network per-process aggregation/direction, packet-flow animation, empty folder finder, health score/grades/thresholds":"")+(coreOnly?"":", path boundaries, protected folders, command parsing, file backup/restore, registry value round-trip, destination collision, live inventory (read-only), exact install-folder alias scan, bulk selection, deduplication, install-date formatting, icon resource parsing, autorun value/shortcut backup and restore, stale-value conflict protection, deep scan, tool catalog, junk vault round-trip, vault purge, legacy vault listing, duplicate finder scan/quarantine/restore, disk analyzer totals/folders/extensions/largest, network per-process aggregation/direction, packet-flow animation, empty folder finder, health score/grades/thresholds, connection table snapshot")+".\r\n"+DateTime.Now.ToString("s"));
     Environment.ExitCode=0;
    }catch(Exception e){File.WriteAllText(results,e.ToString());Console.Error.WriteLine(e);Environment.ExitCode=1;}
    return;
   }
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   Branding.RegisterAppUserModelId();
   Bootstrap();
   if(args.Length>0&&args[0]=="--scan-smoke"){try{var app=Engine.Inventory().First(a=>a.Name=="Brave");Advanced.Capture(app);var result=Advanced.DeepScan(app,System.Threading.CancellationToken.None);File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"scan-smoke.txt"),new[]{"Read-only deep scan: "+app.Name,"Visited folders: "+result.Visited,"Candidates: "+result.Items.Count,"Notes: "+result.Notes.Count}.Concat(result.Notes));}catch(Exception error){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"scan-smoke.txt"),error.ToString());Environment.ExitCode=1;}return;}
   if(args.Length>0&&args[0]=="--preview"){try{
    string mode=args.Length>1?args[1]:"";
    if(mode=="scan"){using(var window=new LeftoverScanForm(new[]{new AppEntry{Name="Ứng dụng mẫu",Hive="HKCU",View="64",Key="SOFTWARE\\Missing"}},true,null)){window.PopulateForPreview();Snapshot(window,"TweekPro-scan-preview.png");}return;}
    bool show=args.Contains("--show");
    if(mode=="all"){string dir=args.Length>2&&!args[2].StartsWith("--")?args[2]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"previews");Directory.CreateDirectory(dir);Core.L.Lang=args.Contains("--en")?Core.L.EnglishCode:Core.L.Vietnamese;foreach(string m in PreviewModes)using(var form=new MainForm(true)){form.PopulateForPreview();ApplyPreview(form,m);Snapshot(form,Path.Combine(dir,m+".png"),new Size(1280,820));}return;}
    using(var form=new MainForm(true)){form.PopulateForPreview();ApplyPreview(form,mode);if(show)Application.Run(form);else Snapshot(form,"TweekPro-preview.png");}
   }catch(Exception error){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-error.txt"),error.ToString());Environment.ExitCode=1;}return;}
   Application.ThreadException+=(s,e)=>{Core.Log.Error("Lỗi chưa xử lý | "+e.Exception);MessageBox.Show(e.Exception.Message,"Tweek Pro",MessageBoxButtons.OK,MessageBoxIcon.Error);};
   Application.Run(new MainForm());
   Core.Log.Info("Tweek Pro đóng.");
  }
  /// <summary>Renders a form off-screen into a PNG next to the executable so the layout can be reviewed without interaction.</summary>
  /// <summary>Every tab in display order; --preview all renders one PNG per entry for the README gallery.</summary>
  public static readonly string[] PreviewModes={"health","apps","store","remnants","junk","empty","dupes","stale","tracks","registry","analyzer","space","vault","autorun","extensions","explorer","tweaks","network","services","tools","logs","ai"};
  static void ApplyPreview(MainForm form,string mode){
   if(mode=="autorun"||mode=="tools"||mode=="junk"||mode=="network"||mode=="health"||mode=="store"||mode=="services"||mode=="ai")form.PreviewAdvanced(mode);else if(mode=="apps")form.PreviewApps();else if(mode=="stale")form.PreviewStale();else if(mode=="tracks")form.PreviewTracks(true);else if(mode=="registry")form.PreviewRegistry();else if(mode=="space")form.PreviewSystemSpace();else if(mode=="extensions")form.PreviewExtensions();else if(mode=="explorer"||mode=="tweaks")form.PreviewExplorer(mode=="tweaks");else if(mode=="empty"||mode=="dupes"||mode=="analyzer"||mode=="vault"||mode=="logs")form.PreviewTab(mode);else if(mode!="")form.PreviewRemnants();
  }
  static void Snapshot(Form form,string fileName,Size? size=null){
   form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();if(size.HasValue){form.Size=size.Value;Application.DoEvents();}
   using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,fileName));}
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
   Assert(Presentation.KindGroup(new Candidate{Kind="Folder"})=="1-folder","Folder group");
   Assert(Presentation.KindGroup(new Candidate{Kind="File",Path=@"C:\Users\a\Desktop\app.lnk"})=="3-shortcut","Shortcut group");
   Assert(Presentation.KindGroup(new Candidate{Kind="Registry"})=="4-registry","Registry group");
   Assert(Presentation.KindGroup(new Candidate{Kind="Review",ReviewOnly=true})=="5-review","Review-only group");
   Assert(Presentation.PermissionNote("Không đủ quyền đọc: C:\\Program Files"),"Permission note");
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
   Assert(Engine.Csv("=1+1").Contains("'=1+1"),"CSV formula protection"); string name="TweekProTest"+Guid.NewGuid().ToString("N");string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);string folder=Path.Combine(local,name);string programsFolder=Path.Combine(local,"Programs",name);string tempHit=Path.Combine(Path.GetTempPath(),name);string tempNoise=Path.Combine(Path.GetTempPath(),name+"noise");string vault=Path.Combine(local,name+"Vault");string originalVault=Engine.Vault;string key="SOFTWARE\\"+name;string view=Environment.Is64BitOperatingSystem?"64":"32";
   try{
    Engine.Vault=vault;Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"sample.txt"),"sample data — kiểm thử");
    var sampleApp=new AppEntry{Name="Friendly "+name,Location=folder,Publisher="PublisherFixture",Hive="HKCU",View=view,Key="SOFTWARE\\"+name+"Missing"};
    Assert(Engine.Scan(sampleApp).Any(x=>x.Path==folder),"Install-folder alias scan");
    Directory.CreateDirectory(programsFolder);File.WriteAllText(Path.Combine(programsFolder,"leftover.txt"),"per-user leftover");
    var perUser=new AppEntry{Name=name,Publisher="PublisherFixture",Hive="HKCU",View=view,Key="SOFTWARE\\"+name+"Missing"};
    Assert(Engine.Scan(perUser).Any(x=>String.Equals(Engine.Canon(x.Path),Engine.Canon(programsFolder),StringComparison.OrdinalIgnoreCase)),"Per-user Local\\Programs leftover");
    MustFail(()=>Engine.ValidateFolder(Path.Combine(local,"Programs")));
    Directory.CreateDirectory(tempHit);
    Assert(Engine.Scan(perUser).Any(x=>String.Equals(Engine.Canon(x.Path),Engine.Canon(tempHit),StringComparison.OrdinalIgnoreCase)),"Temp leftover only with exact name fingerprint");
    Directory.CreateDirectory(tempNoise);
    Assert(!Engine.Scan(perUser).Any(x=>String.Equals(Engine.Canon(x.Path),Engine.Canon(tempNoise),StringComparison.OrdinalIgnoreCase)),"Do not fuzzy-scan Temp");
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
    if(Path.GetFileName(programsFolder)==name&&Engine.Under(Engine.Canon(programsFolder),Engine.Canon(Path.Combine(local,"Programs")))&&Directory.Exists(programsFolder))Directory.Delete(programsFolder,true);
    if(Path.GetFileName(tempHit)==name&&Directory.Exists(tempHit))Directory.Delete(tempHit,true);
    if(Path.GetFileName(tempNoise)==name+"noise"&&Directory.Exists(tempNoise))Directory.Delete(tempNoise,true);
    if(Path.GetFileName(vault)==name+"Vault"&&Engine.Under(Engine.Canon(vault),Engine.Canon(local))&&Directory.Exists(vault))Directory.Delete(vault,true);
    using(var root=Engine.Base("HKCU",view)){if(key=="SOFTWARE\\"+name)root.DeleteSubKeyTree(key,false);}
   }
  }
 }
}
