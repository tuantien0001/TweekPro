using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Network;

namespace TweekPro {
 public partial class MainForm {
  ListView netList=new SmoothListView(),netApps=new SmoothListView();Label netOverlay,netSummary,netNote,netAppsOverlay,netFilterLabel;TabPage netTab;
  TextBox netSearch=new TextBox();NumericUpDown netInterval=new NumericUpDown();CheckBox netResolve=new CheckBox(),netHideLoopback=new CheckBox();
  Button netPause,netBandwidth,netShowAll;Timer netTimer=new Timer();EtwNetworkSession etw;ImageList netIcons=new ImageList(),netAppIcons=new ImageList();PacketFlowStrip netFlow;SplitContainer netSplit;
  bool netPaused,netRefreshing,netAutoBandwidthTried;List<ConnectionInfo> netRows=new List<ConnectionInfo>();Dictionary<int,TrafficSample> netTraffic=new Dictionary<int,TrafficSample>();
  HashSet<int> netSelectedPids=new HashSet<int>();double netMaxRate=1;ContextMenuStrip netMenu=new ContextMenuStrip();
  const int NetColApp=0,NetColDirection=1,NetColUp=2,NetColDown=3;

  /// <summary>Builds the read-only Network tab: applications with live send/receive activity on top, their connections below, ETW bandwidth when elevated.</summary>
  void BuildNetworkTab(){
   netTab=new TabPage(Core.L.T("Mạng"));tabs.TabPages.Add(netTab);
   SetupList(netApps,new[]{"Ứng dụng","Hoạt động","Gửi/s","Nhận/s","Đã gửi","Đã nhận","Kết nối","Đã kết nối","Lắng nghe","Máy từ xa","PID","Nhà phát hành"},new[]{230,175,130,130,90,90,75,95,85,90,65,180},false,false);
   netApps.MultiSelect=true;netApps.OwnerDraw=true;
   netApps.DrawColumnHeader+=(s,e)=>e.DrawDefault=true;
   netApps.DrawItem+=(s,e)=>{};
   netApps.DrawSubItem+=DrawNetworkAppCell;
   netApps.SelectedIndexChanged+=(s,e)=>{netSelectedPids=new HashSet<int>(netApps.SelectedItems.Cast<ListViewItem>().Select(i=>((ProcessNetworkSummary)i.Tag).Pid));RenderNetworkConnections();};
   netApps.DoubleClick+=(s,e)=>ShowApplicationDetails();
   netMenu.Opening+=(s,e)=>{e.Cancel=!BuildNetworkMenu();};
   netApps.ContextMenuStrip=netMenu;netList.ContextMenuStrip=netMenu;
   // ImageList copies bitmaps only once its handle exists; create the handles first so the source bitmaps can be disposed safely.
   netIcons.ColorDepth=ColorDepth.Depth32Bit;netIcons.ImageSize=new Size(16,16);var iconsHandle=netIcons.Handle;
   foreach(string k in new[]{"out","in","both","listen","idle"})using(var bmp=NetworkGlyphs.Icon(k,16))netIcons.Images.Add(k,bmp);
   netAppIcons.ColorDepth=ColorDepth.Depth32Bit;netAppIcons.ImageSize=new Size(24,24);var appIconsHandle=netAppIcons.Handle;
   using(var fallback=Branding.WindowsAppGlyph(24,Theme.Muted))netAppIcons.Images.Add("app",fallback);
   netApps.SmallImageList=netAppIcons;netList.SmallImageList=netIcons;

   SetupList(netList,new[]{"Tiến trình","Hướng","PID","Nhà phát hành","Giao thức","Cục bộ","Từ xa","Trạng thái","Máy từ xa","Đã gửi","Đã nhận","Gửi/s","Nhận/s"},new[]{190,60,70,170,70,190,210,120,200,90,90,90,90},false,true);
   netList.DoubleClick+=(s,e)=>ShowConnectionDetails();
   var appsHost=Theme.ListHost(netApps,out netAppsOverlay);
   var host=Theme.ListHost(netList,out netOverlay);

   var bar=Bar();
   Add(bar,"Làm mới",async()=>await RefreshNetwork(true));
   netPause=Theme.Button("Tạm dừng",ButtonStyle.Secondary);netPause.Margin=new Padding(0,0,8,8);netPause.Click+=(s,e)=>{netPaused=!netPaused;netPause.Text=netPaused?Core.L.T("Tiếp tục"):Core.L.T("Tạm dừng");UpdateNetworkSummary();};bar.Controls.Add(netPause);
   var intervalLabel=new Label{Text=Core.L.T("Chu kỳ (giây)"),AutoSize=true,Margin=new Padding(8,9,4,0),ForeColor=Theme.Muted};
   netInterval.Minimum=1;netInterval.Maximum=30;netInterval.Value=Math.Min(30,Math.Max(1,settings.NetworkRefreshSeconds));netInterval.Width=56;netInterval.Margin=new Padding(0,5,12,0);netInterval.Font=Theme.Body;
   netInterval.ValueChanged+=(s,e)=>{netTimer.Interval=(int)netInterval.Value*1000;settings.NetworkRefreshSeconds=(int)netInterval.Value;};
   var searchLabel=new Label{Text=Core.L.T("Tìm kiếm"),AutoSize=true,Margin=new Padding(0,9,4,0),ForeColor=Theme.Muted};
   netSearch.Width=200;netSearch.Margin=new Padding(0,4,12,0);netSearch.Font=Theme.Body;netSearch.BorderStyle=BorderStyle.FixedSingle;netSearch.TextChanged+=(s,e)=>RenderNetwork();
   netResolve.Text=Core.L.T("Phân giải tên máy");netResolve.AutoSize=true;netResolve.Margin=new Padding(0,8,12,0);netResolve.Checked=settings.NetworkResolveHosts;netResolve.ForeColor=Theme.Text;
   netResolve.CheckedChanged+=(s,e)=>{settings.NetworkResolveHosts=netResolve.Checked;if(!netResolve.Checked)HostResolver.Clear();RenderNetwork();};
   netHideLoopback.Text=Core.L.T("Ẩn loopback");netHideLoopback.AutoSize=true;netHideLoopback.Margin=new Padding(0,8,12,0);netHideLoopback.ForeColor=Theme.Text;netHideLoopback.CheckedChanged+=(s,e)=>RenderNetwork();
   netBandwidth=Theme.Button(EtwNetworkSession.CanStart?Core.L.T("Bật băng thông (ETW)"):Core.L.T("Băng thông: cần quyền quản trị"),EtwNetworkSession.CanStart?ButtonStyle.Primary:ButtonStyle.Secondary);netBandwidth.Margin=new Padding(0,0,8,8);netBandwidth.Enabled=EtwNetworkSession.CanStart;
   netBandwidth.Click+=async(s,e)=>await Guard(()=>{ToggleBandwidth();return Task.FromResult(0);});
   bar.Controls.Add(intervalLabel);bar.Controls.Add(netInterval);bar.Controls.Add(searchLabel);bar.Controls.Add(netSearch);bar.Controls.Add(netResolve);bar.Controls.Add(netHideLoopback);bar.Controls.Add(netBandwidth);
   Add(bar,"Xuất CSV",()=>{ExportNetwork();return Task.FromResult(0);});

   netNote=Theme.Note("Chỉ xem, không chặn: bảng trên là từng ứng dụng đang gửi (↑ xanh dương) hoặc nhận (↓ xanh lá) dữ liệu theo thời gian thực, thanh màu là tốc độ; bấm một ứng dụng để xem các kết nối của nó ở bảng dưới. Tốc độ và byte đo bằng ETW của Windows (tự bật khi có quyền quản trị).",NoteKind.Info);
   netSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true};Theme.BorderTop(netSummary);
   netFlow=new PacketFlowStrip{Dock=DockStyle.Top,Height=70,BackColor=Theme.Surface};Theme.BorderBottom(netFlow);

   var filterBar=new Panel{Dock=DockStyle.Top,Height=36,BackColor=Theme.Canvas};Theme.BorderBottom(filterBar);
   netFilterLabel=new Label{Dock=DockStyle.Fill,Padding=new Padding(16,0,8,0),TextAlign=ContentAlignment.MiddleLeft,Font=Theme.Strong,ForeColor=Theme.Text,AutoEllipsis=true};
   netShowAll=Theme.Button("Hiện tất cả kết nối",ButtonStyle.Secondary);netShowAll.Dock=DockStyle.Right;netShowAll.Width=190;netShowAll.Margin=Padding.Empty;netShowAll.Visible=false;
   netShowAll.Click+=(s,e)=>{netApps.SelectedItems.Clear();netSelectedPids.Clear();RenderNetworkConnections();};
   filterBar.Controls.Add(netFilterLabel);filterBar.Controls.Add(netShowAll);
   var lower=new Panel{Dock=DockStyle.Fill};lower.Controls.Add(host);lower.Controls.Add(filterBar);

   netSplit=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterWidth=6,BackColor=Theme.Border,Panel1MinSize=120,Panel2MinSize=120};
   netSplit.Panel1.Controls.Add(appsHost);netSplit.Panel2.Controls.Add(lower);
   netSplit.Panel1.BackColor=Theme.Surface;netSplit.Panel2.BackColor=Theme.Surface;
   netTab.Controls.Add(netSplit);netTab.Controls.Add(netSummary);netTab.Controls.Add(netFlow);netTab.Controls.Add(netNote);netTab.Controls.Add(bar);
   netSplit.SizeChanged+=(s,e)=>{if(netSplit.Height>300&&netSplit.Tag==null){netSplit.SplitterDistance=(int)(netSplit.Height*0.48);netSplit.Tag="placed";}};
   Theme.SetOverlay(netAppsOverlay,"Chưa đọc bảng kết nối.\r\nMở tab này để bắt đầu làm mới tự động theo chu kỳ đã chọn, hoặc bấm Làm mới.",NoteKind.Info);
   Theme.SetOverlay(netOverlay,"Chọn một ứng dụng ở bảng trên để xem các kết nối của nó.",NoteKind.Info);
   UpdateNetworkFilterLabel();

   netTimer.Interval=(int)netInterval.Value*1000;
   netTimer.Tick+=async(s,e)=>{if(!netPaused&&tabs.SelectedTab==netTab&&!IsDisposed)await RefreshNetwork(false);};
   tabs.SelectedIndexChanged+=async(s,e)=>{if(tabs.SelectedTab==netTab){netTimer.Start();netFlow.Begin();AutoStartBandwidth();if(netRows.Count==0)await RefreshNetwork(false);}else{netTimer.Stop();netFlow.End();}};
   FormClosed+=(s,e)=>{netTimer.Stop();netFlow.End();if(etw!=null){etw.Dispose();etw=null;}netIcons.Dispose();netAppIcons.Dispose();};
   UpdateNetworkSummary();
  }

  /// <summary>Starts the ETW bandwidth session once, the first time the tab opens, when the process is elevated and the setting allows it.</summary>
  void AutoStartBandwidth(){
   if(netAutoBandwidthTried||etw!=null||!EtwNetworkSession.CanStart||!settings.NetworkStartBandwidthWhenElevated)return;
   netAutoBandwidthTried=true;
   try{ToggleBandwidth();}catch(Exception e){Log(Core.L.F("Mạng: không tự bật được băng thông (ETW) — {0}",e.Message));}
  }

  /// <summary>Owner-draws the activity and rate cells of the application list: directional glyph plus label, and rate bars scaled to the busiest process.</summary>
  void DrawNetworkAppCell(object sender,DrawListViewSubItemEventArgs e){
   int col=e.ColumnIndex;
   if(col!=NetColDirection&&col!=NetColUp&&col!=NetColDown){e.DrawDefault=true;return;}
   var s=(ProcessNetworkSummary)e.Item.Tag;
   bool selected=e.Item.Selected;
   Color back=selected?SystemColors.Highlight:e.Item.BackColor,fore=selected?SystemColors.HighlightText:e.Item.ForeColor;
   using(var b=new SolidBrush(back))e.Graphics.FillRectangle(b,e.Bounds);
   var g=e.Graphics;var r=e.Bounds;
   if(col==NetColDirection){
    string key=NetworkStats.DirectionKey(s);
    var glyph=new Rectangle(r.X+6,r.Y+(r.Height-16)/2,16,16);
    NetworkGlyphs.Draw(g,glyph,key);
    TextRenderer.DrawText(g,NetworkStats.DirectionLabel(key),e.Item.Font,new Rectangle(glyph.Right+6,r.Y,Math.Max(0,r.Width-glyph.Width-12),r.Height),fore,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
    return;
   }
   bool up=col==NetColUp;double rate=up?s.SentPerSecond:s.ReceivedPerSecond;
   float fraction=NetworkStats.BarFraction(rate,netMaxRate);
   if(fraction>0){
    Color bar=up?Theme.Primary:Theme.Success;
    int w=Math.Max(3,(int)((r.Width-12)*fraction));
    using(var b=new SolidBrush(Color.FromArgb(selected?120:85,bar)))g.FillRectangle(b,r.X+6,r.Y+5,w,r.Height-10);
    using(var p=new Pen(bar,2f))g.DrawLine(p,r.X+6,r.Bottom-4,r.X+6+w,r.Bottom-4);
   }
   TextRenderer.DrawText(g,NetworkStats.Rate(rate),e.Item.Font,new Rectangle(r.X+8,r.Y,r.Width-10,r.Height),fore,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
  }

  /// <summary>Starts or stops the ETW bandwidth session; the fixed session name is always stopped on exit.</summary>
  void ToggleBandwidth(){
   if(etw!=null&&etw.IsRunning){etw.Stop();etw.Dispose();etw=null;netTraffic.Clear();netBandwidth.Text=Core.L.T("Bật băng thông (ETW)");Log("Mạng: đã dừng phiên ETW "+EtwNetworkSession.SessionName+".");RenderNetwork();return;}
   etw=new EtwNetworkSession();
   try{etw.Start();}catch(Exception e){etw.Dispose();etw=null;throw new IOException("Không bật được ETW: "+e.Message);}
   netBandwidth.Text=Core.L.T("Dừng băng thông (ETW)");Log("Mạng: đã bật phiên ETW "+EtwNetworkSession.SessionName+" (kernel TCP/IP, chỉ đọc).");
  }

  /// <summary>Reads the connection table on a worker thread, resolves processes and redraws the list.</summary>
  async Task RefreshNetwork(bool manual){
   if(netRefreshing)return;netRefreshing=true;
   try{
    bool resolve=netResolve.Checked;
    var rows=await Task.Run(()=>{
     var snapshot=ConnectionTable.Snapshot();
     var pids=new HashSet<int>(snapshot.Select(c=>c.Pid));
     foreach(int pid in pids)ProcessResolver.Resolve(pid);
     ProcessResolver.Trim(pids);
     if(resolve)foreach(var c in snapshot)if(c.RemoteAddress!=null)HostResolver.Lookup(c.RemoteAddress,true);
     return snapshot;
    });
    if(IsDisposed)return;
    netRows=rows;
    if(etw!=null&&etw.IsRunning){netTraffic=etw.Snapshot();etw.Trim(new HashSet<int>(rows.Select(c=>c.Pid)));}
    else if(etw!=null&&!etw.IsRunning&&etw.Failure!=""){Log("Mạng: phiên ETW dừng — "+etw.Failure);etw.Dispose();etw=null;netBandwidth.Text=Core.L.T("Bật băng thông (ETW)");}
    RenderNetwork();
    if(manual)Log("Mạng: "+rows.Count+" kết nối, "+rows.Select(c=>c.Pid).Distinct().Count()+" tiến trình.");
   }catch(Exception e){Theme.SetOverlay(netOverlay,"Không đọc được bảng kết nối.\r\n"+e.Message,NoteKind.Error);if(manual)throw;}
   finally{netRefreshing=false;}
  }

  static string Rate(double bytesPerSecond){return bytesPerSecond<1?"":Presentation.BytesLabel((long)bytesPerSecond)+"/s";}

  /// <summary>True when the row survives the loopback and search filters.</summary>
  bool NetworkRowVisible(ConnectionInfo c,string q,bool hideLoop,bool resolve){
   if(hideLoop&&c.IsLoopback)return false;
   if(q=="")return true;
   var id=ProcessResolver.Resolve(c.Pid);
   string hostName=resolve&&c.RemoteAddress!=null?HostResolver.Lookup(c.RemoteAddress,true):"";
   string hay=id.Display+" "+id.Path+" "+c.Pid+" "+c.Protocol+" "+c.Local+" "+c.Remote+" "+c.State+" "+hostName+" "+id.Publisher;
   return hay.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0;
  }

  /// <summary>Renders both lists from the current snapshot: applications rolled up by process, then the connections of the selected ones.</summary>
  void RenderNetwork(){RenderNetworkApps();RenderNetworkConnections();}

  /// <summary>Returns the image-list key for a process icon, extracting it from the executable on first sight.</summary>
  string NetworkAppIconKey(ProcessIdentity id){
   if(String.IsNullOrEmpty(id.Path))return "app";
   string key=id.Path.ToLowerInvariant();
   if(!netAppIcons.Images.ContainsKey(key)){
    using(var bitmap=Presentation.AppIcon(new AppEntry{DisplayIcon=id.Path}))netAppIcons.Images.Add(key,bitmap);
   }
   return key;
  }

  void RenderNetworkApps(){
   string q=netSearch.Text.Trim();bool hideLoop=netHideLoopback.Checked;bool resolve=netResolve.Checked;
   var visible=netRows.Where(c=>NetworkRowVisible(c,q,hideLoop,resolve)).ToList();
   var summaries=NetworkStats.Aggregate(visible,netTraffic,NetworkStats.MinRate);
   netMaxRate=Math.Max(1,summaries.Count==0?1:summaries.Max(s=>Math.Max(s.SentPerSecond,s.ReceivedPerSecond)));
   int top=netApps.Items.Count>0&&netApps.TopItem!=null?netApps.TopItem.Index:0;
   netApps.BeginUpdate();netApps.Items.Clear();
   int index=0;
   foreach(var s in summaries){
    var id=ProcessResolver.Resolve(s.Pid);
    string key=NetworkStats.DirectionKey(s);
    var row=new ListViewItem(new[]{id.Display,NetworkStats.DirectionLabel(key),NetworkStats.Rate(s.SentPerSecond),NetworkStats.Rate(s.ReceivedPerSecond),s.Sent==0?"":Presentation.BytesLabel(s.Sent),s.Received==0?"":Presentation.BytesLabel(s.Received),s.Connections.ToString(),s.Established==0?"":s.Established.ToString(),s.Listening==0?"":s.Listening.ToString(),s.RemoteEndpoints==0?"":s.RemoteEndpoints.ToString(),s.Pid.ToString(),id.Publisher}){Tag=s,ImageKey=NetworkAppIconKey(id),ToolTipText=(id.Path==""?id.Display:id.Path)+"\r\n"+NetworkStats.DirectionLabel(key)+"  •  "+s.Connections+" "+Core.L.T("kết nối")};
    if(s.Direction!=TrafficDirection.Idle)row.Font=Theme.Strong;
    else if(s.Established==0)row.ForeColor=Theme.Muted;
    Theme.StripeRow(row,index++);netApps.Items.Add(row);
    if(netSelectedPids.Contains(s.Pid))row.Selected=true;
   }
   netApps.EndUpdate();
   if(top>0&&top<netApps.Items.Count)try{netApps.TopItem=netApps.Items[top];}catch(Exception){}
   if(netRows.Count==0)Theme.SetOverlay(netAppsOverlay,"Không có kết nối nào được Windows báo cáo.",NoteKind.Info);
   else if(summaries.Count==0)Theme.SetOverlay(netAppsOverlay,"Không có kết nối khớp với bộ lọc hiện tại.",NoteKind.Info);
   else Theme.SetOverlay(netAppsOverlay,null,NoteKind.Info);
   if(netFlow!=null){double up=0,down=0;foreach(var s in netTraffic.Values){up+=s.SentPerSecond;down+=s.ReceivedPerSecond;}netFlow.SetRates(up,down);}
  }

  /// <summary>Renders the connection rows of the selected applications (or all of them), grouped by process.</summary>
  void RenderNetworkConnections(){
   string q=netSearch.Text.Trim();bool hideLoop=netHideLoopback.Checked;bool resolve=netResolve.Checked;
   string selectedKey=netList.SelectedItems.Count>0?((ConnectionInfo)netList.SelectedItems[0].Tag).Key:null;
   int top=netList.Items.Count>0?netList.TopItem!=null?netList.TopItem.Index:0:0;
   netList.BeginUpdate();netList.Items.Clear();netList.Groups.Clear();
   int shown=0;var processes=new HashSet<int>();bool filtered=netSelectedPids.Count>0;
   foreach(var c in netRows.OrderBy(c=>ProcessResolver.Resolve(c.Pid).Display,StringComparer.CurrentCultureIgnoreCase).ThenBy(c=>c.Pid).ThenBy(c=>c.Protocol).ThenBy(c=>c.LocalPort)){
    if(filtered&&!netSelectedPids.Contains(c.Pid))continue;
    if(!NetworkRowVisible(c,q,hideLoop,resolve))continue;
    var id=ProcessResolver.Resolve(c.Pid);
    string hostName=resolve&&c.RemoteAddress!=null?HostResolver.Lookup(c.RemoteAddress,true):"";
    TrafficSample t;netTraffic.TryGetValue(c.Pid,out t);
    string dirKey=NetworkStats.DirectionKey(c,netTraffic,NetworkStats.MinRate);
    var row=new ListViewItem(new[]{id.Display,NetworkStats.DirectionArrows(dirKey),c.Pid.ToString(),id.Publisher,c.Protocol,c.Local,c.Remote,Core.L.T(c.State),hostName,t==null?"":Presentation.BytesLabel(t.Sent),t==null?"":Presentation.BytesLabel(t.Received),t==null?"":Rate(t.SentPerSecond),t==null?"":Rate(t.ReceivedPerSecond)}){Tag=c,ImageKey=dirKey,ToolTipText=(id.Path==""?id.Display:id.Path)+"\r\n"+c.Protocol+" "+c.Local+(c.Remote==""?"":" → "+c.Remote)+(c.State==""?"":"  ["+Core.L.T(c.State)+"]")};
    if(c.State=="Đang lắng nghe")row.ForeColor=Theme.Muted;
    if(t!=null&&(t.SentPerSecond>1024||t.ReceivedPerSecond>1024))row.Font=Theme.Strong;
    string groupKey=id.Display.ToLowerInvariant()+"|"+c.Pid.ToString("D8");
    string header=id.Display+"  (PID "+c.Pid+")"+(t!=null?"   ↑ "+Presentation.BytesLabel(t.Sent)+"  ↓ "+Presentation.BytesLabel(t.Received)+(Rate(t.SentPerSecond)==""&&Rate(t.ReceivedPerSecond)==""?"":"   •   "+(Rate(t.SentPerSecond)==""?"0 B/s":Rate(t.SentPerSecond))+" / "+(Rate(t.ReceivedPerSecond)==""?"0 B/s":Rate(t.ReceivedPerSecond))):"");
    Theme.AssignGroup(netList,row,groupKey,header);
    Theme.StripeRow(row,shown++);netList.Items.Add(row);processes.Add(c.Pid);
    if(selectedKey!=null&&c.Key==selectedKey)row.Selected=true;
   }
   netList.EndUpdate();
   if(top>0&&top<netList.Items.Count)try{netList.TopItem=netList.Items[top];}catch(Exception){}
   if(netRows.Count==0)Theme.SetOverlay(netOverlay,"Không có kết nối nào được Windows báo cáo.",NoteKind.Info);
   else if(shown==0)Theme.SetOverlay(netOverlay,filtered?"Ứng dụng đã chọn không còn kết nối nào.":"Không có kết nối khớp với bộ lọc hiện tại.",NoteKind.Info);
   else Theme.SetOverlay(netOverlay,null,NoteKind.Info);
   UpdateNetworkFilterLabel();
   UpdateNetworkSummary(shown,processes.Count);
  }

  void UpdateNetworkFilterLabel(){
   if(netFilterLabel==null)return;
   if(netSelectedPids.Count==0){netFilterLabel.Text=Core.L.T("Kết nối của tất cả ứng dụng — bấm một ứng dụng ở bảng trên để lọc");netShowAll.Visible=false;return;}
   var names=netApps.SelectedItems.Cast<ListViewItem>().Select(i=>i.Text).Distinct().Take(3).ToList();
   string label=String.Join(", ",names)+(netSelectedPids.Count>names.Count?" +"+(netSelectedPids.Count-names.Count):"");
   netFilterLabel.Text=Core.L.F("Kết nối của: {0}",label);netShowAll.Visible=true;
  }

  /// <summary>PID under the context menu: the selected application row, or the process of the selected connection row.</summary>
  int NetworkMenuPid(){
   var source=netMenu.SourceControl;
   if(source==netList&&netList.SelectedItems.Count>0)return ((ConnectionInfo)netList.SelectedItems[0].Tag).Pid;
   if(netApps.SelectedItems.Count>0)return ((ProcessNetworkSummary)netApps.SelectedItems[0].Tag).Pid;
   return -1;
  }

  /// <summary>Fills the right-click menu for the process under the cursor: end process, stop/disable hosted services, open folder, details.</summary>
  bool BuildNetworkMenu(){
   netMenu.Items.Clear();
   int pid=NetworkMenuPid();if(pid<0)return false;
   var id=ProcessResolver.Resolve(pid);
   string blocked=ProcessControl.TerminateBlockReason(pid,id.Name);
   var kill=new ToolStripMenuItem(Core.L.F("Kết thúc tiến trình {0} (PID {1})",id.Display,pid)){Enabled=blocked==null,ToolTipText=blocked,Image=NetworkGlyphs.Icon("kill",16)};
   if(blocked!=null)kill.Text+="  — "+blocked;
   kill.Click+=async(s,e)=>await Guard(async()=>{
    if(!Confirm(Core.L.F("Kết thúc tiến trình {0} (PID {1})?\r\nỨng dụng sẽ đóng ngay và dữ liệu chưa lưu có thể mất. Nếu đây là dịch vụ, Windows có thể tự chạy lại nó — dùng \"Dừng và vô hiệu hóa\" để ngăn.",id.Display,pid)))return;
    await Task.Run(()=>ProcessControl.Terminate(pid,id.Name));
    Log(Core.L.F("Mạng: đã kết thúc {0} (PID {1}).",id.Display,pid));
    await RefreshNetwork(false);
   });
   netMenu.Items.Add(kill);
   var services=ProcessControl.ServicesOf(pid);
   if(services.Count>0){
    netMenu.Items.Add(new ToolStripSeparator());
    var header=new ToolStripMenuItem(Core.L.F("Dịch vụ Windows trong tiến trình này ({0})",services.Count)){Enabled=false};netMenu.Items.Add(header);
    foreach(var svc in services.Take(12)){
     var service=svc;bool core=ProcessControl.IsCoreService(service.Name);
     string label=service.DisplayName==""?service.Name:service.DisplayName+" ("+service.Name+")";
     var stop=new ToolStripMenuItem(Core.L.F("Dừng dịch vụ: {0}",label)+(core?"  — "+Core.L.T("dịch vụ cốt lõi"):"")){Enabled=!core};
     stop.Click+=async(s,e)=>await Guard(async()=>{
      if(!Confirm(Core.L.F("Dừng dịch vụ {0}?\r\nDịch vụ sẽ chạy lại theo kiểu khởi động hiện tại ({1}) ở lần khởi động máy sau.",label,service.StartMode)))return;
      await Task.Run(()=>ProcessControl.StopService(service,false));
      Log(Core.L.F("Mạng: đã dừng dịch vụ {0}.",label));await RefreshNetwork(false);
     });
     var disable=new ToolStripMenuItem(Core.L.F("Dừng và vô hiệu hóa: {0}",label)+(core?"  — "+Core.L.T("dịch vụ cốt lõi"):"")){Enabled=!core};
     disable.Click+=async(s,e)=>await Guard(async()=>{
      if(!Confirm(Core.L.F("Dừng và vô hiệu hóa dịch vụ {0}?\r\nDịch vụ không tự chạy lại nữa. Kiểu khởi động cũ ({1}) được lưu vào Kho khôi phục để bật lại bất kỳ lúc nào.",label,service.StartMode)))return;
      await Task.Run(()=>ProcessControl.StopService(service,true));
      Log(Core.L.F("Mạng: đã dừng và vô hiệu hóa dịch vụ {0}; bật lại trong Kho khôi phục.",label));LoadBackups();await RefreshNetwork(false);
     });
     netMenu.Items.Add(stop);netMenu.Items.Add(disable);
    }
   }
   netMenu.Items.Add(new ToolStripSeparator());
   var open=new ToolStripMenuItem(Core.L.T("Mở thư mục chứa tệp")){Enabled=id.Path!=""&&File.Exists(id.Path)};
   open.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+id.Path+"\""){UseShellExecute=true});}catch(Exception error){Log("LỖI: "+error.Message);}};
   var copy=new ToolStripMenuItem(Core.L.T("Sao chép đường dẫn")){Enabled=id.Path!=""};
   copy.Click+=(s,e)=>{try{Clipboard.SetText(id.Path);}catch(Exception){}};
   var details=new ToolStripMenuItem(Core.L.T("Chi tiết ứng dụng"));
   details.Click+=(s,e)=>{if(netMenu.SourceControl==netList)ShowConnectionDetails();else ShowApplicationDetails();};
   netMenu.Items.Add(open);netMenu.Items.Add(copy);netMenu.Items.Add(details);
   return true;
  }

  /// <summary>Shows the per-process rollup for the selected application.</summary>
  void ShowApplicationDetails(){
   if(netApps.SelectedItems.Count==0)return;var s=(ProcessNetworkSummary)netApps.SelectedItems[0].Tag;var id=ProcessResolver.Resolve(s.Pid);
   MessageBox.Show(this,id.Display+"  (PID "+s.Pid+")\r\n"+Core.L.T("Đường dẫn:")+" "+(id.Path==""?Core.L.T("không đọc được (tiến trình được bảo vệ hoặc đã kết thúc)"):id.Path)+"\r\n"+Core.L.T("Nhà phát hành (chữ ký):")+" "+(id.Publisher==""?"—":id.Publisher)+"\r\n\r\n"+NetworkStats.DirectionLabel(NetworkStats.DirectionKey(s))+"\r\n"+Core.L.F("{0} kết nối • {1} đã kết nối • {2} lắng nghe • {3} máy từ xa",s.Connections,s.Established,s.Listening,s.RemoteEndpoints)+(etw!=null&&etw.IsRunning?"\r\n"+Core.L.F("Gửi {0} ({1}) • Nhận {2} ({3})",Presentation.BytesLabel(s.Sent),Rate(s.SentPerSecond)==""?"0 B/s":Rate(s.SentPerSecond),Presentation.BytesLabel(s.Received),Rate(s.ReceivedPerSecond)==""?"0 B/s":Rate(s.ReceivedPerSecond)):"")+"\r\n\r\n"+Core.L.T("Tweek Pro chỉ hiển thị; không chặn hay thay đổi kết nối."),Core.L.T("Chi tiết ứng dụng"),MessageBoxButtons.OK,MessageBoxIcon.Information);
  }

  void UpdateNetworkSummary(int shown=-1,int processes=-1){
   if(netSummary==null)return;
   var sb=new StringBuilder();
   sb.Append(Core.L.F("{0} kết nối hiển thị / {1} tổng  •  {2} tiến trình",shown<0?netRows.Count:shown,netRows.Count,processes<0?netRows.Select(c=>c.Pid).Distinct().Count():processes));
   sb.Append("  •  ").Append(netPaused?Core.L.T("Đã tạm dừng làm mới"):Core.L.F("Làm mới mỗi {0} giây",netInterval.Value));
   if(etw!=null&&etw.IsRunning){
    double up=netTraffic.Values.Sum(t=>t.SentPerSecond),down=netTraffic.Values.Sum(t=>t.ReceivedPerSecond);
    sb.Append("  •  ").Append(Core.L.F("ETW: {0} sự kiện",etw.Events.ToString("N0")));
    if(etw.EventsLost>0)sb.Append(Core.L.F(", mất {0}",etw.EventsLost.ToString("N0")));
    sb.Append("  •  ").Append(Core.L.T("Tổng")).Append(" ↑ ").Append(Rate(up)==""?"0 B/s":Rate(up)).Append("  ↓ ").Append(Rate(down)==""?"0 B/s":Rate(down));
   }else sb.Append("  •  ").Append(EtwNetworkSession.CanStart?Core.L.T("Băng thông: chưa bật"):Core.L.T("Băng thông: cần quyền quản trị"));
   netSummary.Text=sb.ToString();
  }

  /// <summary>Shows the full process path and endpoint details for the selected row.</summary>
  void ShowConnectionDetails(){
   if(netList.SelectedItems.Count==0)return;var c=(ConnectionInfo)netList.SelectedItems[0].Tag;var id=ProcessResolver.Resolve(c.Pid);
   TrafficSample t;netTraffic.TryGetValue(c.Pid,out t);
   MessageBox.Show(this,id.Display+"  (PID "+c.Pid+")\r\nĐường dẫn: "+(id.Path==""?"không đọc được (tiến trình được bảo vệ hoặc đã kết thúc)":id.Path)+"\r\nNhà phát hành (chữ ký): "+(id.Publisher==""?"—":id.Publisher)+"\r\n\r\n"+c.Protocol+"  "+c.Local+(c.Remote==""?"":"  →  "+c.Remote)+(c.State==""?"":"\r\nTrạng thái: "+c.State)+(t==null?"":"\r\n\r\nĐã gửi: "+Presentation.BytesLabel(t.Sent)+"  •  Đã nhận: "+Presentation.BytesLabel(t.Received)+" (từ khi bật ETW, toàn tiến trình)")+"\r\n\r\nTweek Pro chỉ hiển thị; không chặn hay thay đổi kết nối.","Chi tiết kết nối",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }

  /// <summary>Fills the tab with synthetic applications, connections and moving rates so the layout can be reviewed without Windows.</summary>
  public void PreviewNetwork(){
   var apps=new[]{
    new ProcessIdentity{Pid=91001,Name="chrome.exe",Path=@"C:\Program Files\Google\Chrome\Application\chrome.exe",Publisher="Google LLC"},
    new ProcessIdentity{Pid=91002,Name="Spotify.exe",Path=@"C:\Users\ADMIN\AppData\Roaming\Spotify\Spotify.exe",Publisher="Spotify AB"},
    new ProcessIdentity{Pid=91003,Name="OneDrive.exe",Path=@"C:\Program Files\Microsoft OneDrive\OneDrive.exe",Publisher="Microsoft Corporation"},
    new ProcessIdentity{Pid=91004,Name="svchost.exe",Path=@"C:\Windows\System32\svchost.exe",Publisher="Microsoft Windows"},
    new ProcessIdentity{Pid=91005,Name="Discord.exe",Path=@"C:\Users\ADMIN\AppData\Local\Discord\app-1.0.9\Discord.exe",Publisher="Discord Inc."},
    new ProcessIdentity{Pid=91006,Name="steam.exe",Path=@"C:\Program Files (x86)\Steam\steam.exe",Publisher="Valve Corp."},
    new ProcessIdentity{Pid=91007,Name="mysqld.exe",Path=@"C:\Program Files\MySQL\bin\mysqld.exe",Publisher=""}};
   foreach(var a in apps)ProcessResolver.Seed(a);
   ProcessControl.ServiceProvider=pid=>pid==91004?new List<HostedService>{new HostedService{Pid=pid,Name="RpcSs",DisplayName="Remote Procedure Call (RPC)",State="Running",StartMode="Auto"},new HostedService{Pid=pid,Name="Spooler",DisplayName="Print Spooler",State="Running",StartMode="Auto"},new HostedService{Pid=pid,Name="WSearch",DisplayName="Windows Search",State="Running",StartMode="Auto"}}:pid==91007?new List<HostedService>{new HostedService{Pid=pid,Name="MySQL80",DisplayName="MySQL80",State="Running",StartMode="Auto",PathName=@"C:\Program Files\MySQL\bin\mysqld.exe"}}:new List<HostedService>();
   Func<int,string,string,int,int,string,ConnectionInfo> tcp=(pid,local,remote,lp,rp,state)=>new ConnectionInfo{Protocol="TCP",Pid=pid,LocalAddress=System.Net.IPAddress.Parse(local),LocalPort=lp,RemoteAddress=remote==null?null:System.Net.IPAddress.Parse(remote),RemotePort=rp,State=state};
   netRows=new List<ConnectionInfo>{
    tcp(91001,"192.168.1.20","142.250.66.100",52011,443,"Đã kết nối"),tcp(91001,"192.168.1.20","104.18.32.7",52012,443,"Đã kết nối"),tcp(91001,"192.168.1.20","151.101.1.69",52013,443,"Đã kết nối"),
    tcp(91002,"192.168.1.20","35.186.224.25",52020,443,"Đã kết nối"),tcp(91002,"192.168.1.20","35.186.224.47",52021,4070,"Đã kết nối"),
    tcp(91003,"192.168.1.20","13.107.42.12",52030,443,"Đã kết nối"),
    tcp(91004,"0.0.0.0",null,135,0,"Đang lắng nghe"),tcp(91004,"0.0.0.0",null,445,0,"Đang lắng nghe"),tcp(91004,"192.168.1.20","20.190.160.14",52040,443,"Đã kết nối"),
    tcp(91005,"192.168.1.20","162.159.135.232",52050,443,"Đã kết nối"),new ConnectionInfo{Protocol="UDP",Pid=91005,LocalAddress=System.Net.IPAddress.Parse("192.168.1.20"),LocalPort=50001,State=""},
    tcp(91006,"192.168.1.20","155.133.248.36",52060,27036,"Đã kết nối"),
    tcp(91007,"127.0.0.1",null,3306,0,"Đang lắng nghe")};
   var phase=0.0;
   Action tick=()=>{
    phase+=0.35;
    Func<double,double,double> wave=(baseRate,k)=>Math.Max(0,baseRate*(1+0.6*Math.Sin(phase*k)));
    netTraffic=new Dictionary<int,TrafficSample>{
     {91001,new TrafficSample{Pid=91001,Sent=48_300_000,Received=1_240_000_000,SentPerSecond=wave(180_000,1.1),ReceivedPerSecond=wave(2_900_000,0.7)}},
     {91002,new TrafficSample{Pid=91002,Sent=2_100_000,Received=380_000_000,SentPerSecond=wave(6_000,1.7),ReceivedPerSecond=wave(420_000,0.9)}},
     {91003,new TrafficSample{Pid=91003,Sent=910_000_000,Received=12_000_000,SentPerSecond=wave(1_600_000,0.8),ReceivedPerSecond=wave(9_000,1.3)}},
     {91005,new TrafficSample{Pid=91005,Sent=15_000_000,Received=19_000_000,SentPerSecond=wave(38_000,1.5),ReceivedPerSecond=wave(41_000,1.2)}},
     {91006,new TrafficSample{Pid=91006,Sent=3_000_000,Received=5_400_000_000,SentPerSecond=wave(12_000,1.9),ReceivedPerSecond=wave(11_500_000,0.5)}}};
    RenderNetwork();
   };
   netPaused=true;netPause.Text=Core.L.T("Tiếp tục");
   tick();
   var preview=new Timer{Interval=1000};preview.Tick+=(s,e)=>tick();preview.Start();
   FormClosed+=(s,e)=>preview.Dispose();
   tabs.SelectedTab=netTab;
  }

  void ExportNetwork(){
   string p=SavePath("TweekPro-network.csv");if(p==null)return;
   var lines=new List<string>{"Process,PID,Path,Publisher,Protocol,Local,Remote,State,RemoteHost,SentBytes,ReceivedBytes"};
   foreach(ListViewItem item in netList.Items){var c=(ConnectionInfo)item.Tag;var id=ProcessResolver.Resolve(c.Pid);TrafficSample t;netTraffic.TryGetValue(c.Pid,out t);
    lines.Add(String.Join(",",new[]{id.Display,c.Pid.ToString(),id.Path,id.Publisher,c.Protocol,c.Local,c.Remote,c.State,item.SubItems[8].Text,t==null?"":t.Sent.ToString(),t==null?"":t.Received.ToString()}.Select(Engine.Csv)));}
   File.WriteAllLines(p,lines,new UTF8Encoding(true));Log("Đã xuất bảng kết nối: "+p);
  }
 }
}
