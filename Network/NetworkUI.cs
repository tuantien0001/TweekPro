using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Network;

namespace TweekPro {
 public partial class MainForm {
  ListView netList=new SmoothListView();Label netOverlay,netSummary,netNote;TabPage netTab;
  TextBox netSearch=new TextBox();NumericUpDown netInterval=new NumericUpDown();CheckBox netResolve=new CheckBox(),netHideLoopback=new CheckBox();
  Button netPause,netBandwidth;Timer netTimer=new Timer();EtwNetworkSession etw;ImageList netIcons=new ImageList();PacketFlowStrip netFlow;
  bool netPaused,netRefreshing;List<ConnectionInfo> netRows=new List<ConnectionInfo>();Dictionary<int,TrafficSample> netTraffic=new Dictionary<int,TrafficSample>();

  /// <summary>Builds the read-only Network tab: live connection table per process, optional ETW bandwidth when elevated.</summary>
  void BuildNetworkTab(){
   netTab=new TabPage("Mạng");tabs.TabPages.Add(netTab);
   SetupList(netList,new[]{"Tiến trình","Hướng","PID","Nhà phát hành","Giao thức","Cục bộ","Từ xa","Trạng thái","Máy từ xa","Đã gửi","Đã nhận","Gửi/s","Nhận/s"},new[]{190,60,70,170,70,190,210,120,200,90,90,90,90},false,true);
   netIcons.ColorDepth=ColorDepth.Depth32Bit;netIcons.ImageSize=new Size(16,16);
   foreach(string k in new[]{"out","in","both","listen","idle"})netIcons.Images.Add(k,NetworkGlyphs.Icon(k,16));
   netList.SmallImageList=netIcons;
   netList.DoubleClick+=(s,e)=>ShowConnectionDetails();
   var host=Theme.ListHost(netList,out netOverlay);

   var bar=Bar();
   Add(bar,"Làm mới",async()=>await RefreshNetwork(true));
   netPause=Theme.Button("Tạm dừng",ButtonStyle.Secondary);netPause.Margin=new Padding(0,0,8,8);netPause.Click+=(s,e)=>{netPaused=!netPaused;netPause.Text=netPaused?"Tiếp tục":"Tạm dừng";UpdateNetworkSummary();};bar.Controls.Add(netPause);
   var intervalLabel=new Label{Text="Chu kỳ (giây)",AutoSize=true,Margin=new Padding(8,9,4,0),ForeColor=Theme.Muted};
   netInterval.Minimum=1;netInterval.Maximum=30;netInterval.Value=Math.Min(30,Math.Max(1,settings.NetworkRefreshSeconds));netInterval.Width=56;netInterval.Margin=new Padding(0,5,12,0);netInterval.Font=Theme.Body;
   netInterval.ValueChanged+=(s,e)=>{netTimer.Interval=(int)netInterval.Value*1000;settings.NetworkRefreshSeconds=(int)netInterval.Value;};
   var searchLabel=new Label{Text="Tìm kiếm",AutoSize=true,Margin=new Padding(0,9,4,0),ForeColor=Theme.Muted};
   netSearch.Width=200;netSearch.Margin=new Padding(0,4,12,0);netSearch.Font=Theme.Body;netSearch.BorderStyle=BorderStyle.FixedSingle;netSearch.TextChanged+=(s,e)=>RenderNetwork();
   netResolve.Text="Phân giải tên máy";netResolve.AutoSize=true;netResolve.Margin=new Padding(0,8,12,0);netResolve.Checked=settings.NetworkResolveHosts;netResolve.ForeColor=Theme.Text;
   netResolve.CheckedChanged+=(s,e)=>{settings.NetworkResolveHosts=netResolve.Checked;if(!netResolve.Checked)HostResolver.Clear();RenderNetwork();};
   netHideLoopback.Text="Ẩn loopback";netHideLoopback.AutoSize=true;netHideLoopback.Margin=new Padding(0,8,12,0);netHideLoopback.ForeColor=Theme.Text;netHideLoopback.CheckedChanged+=(s,e)=>RenderNetwork();
   netBandwidth=Theme.Button(EtwNetworkSession.CanStart?"Bật băng thông (ETW)":"Băng thông: cần quyền quản trị",EtwNetworkSession.CanStart?ButtonStyle.Primary:ButtonStyle.Secondary);netBandwidth.Margin=new Padding(0,0,8,8);netBandwidth.Enabled=EtwNetworkSession.CanStart;
   netBandwidth.Click+=async(s,e)=>await Guard(()=>{ToggleBandwidth();return Task.FromResult(0);});
   bar.Controls.Add(intervalLabel);bar.Controls.Add(netInterval);bar.Controls.Add(searchLabel);bar.Controls.Add(netSearch);bar.Controls.Add(netResolve);bar.Controls.Add(netHideLoopback);bar.Controls.Add(netBandwidth);
   Add(bar,"Xuất CSV",()=>{ExportNetwork();return Task.FromResult(0);});

   netNote=Theme.Note(EtwNetworkSession.CanStart?"Chỉ xem: bảng kết nối TCP/UDP theo tiến trình từ Windows (không chặn, không driver). Bật băng thông (ETW) để xem byte gửi/nhận và tốc độ theo tiến trình.":"Chỉ xem: bảng kết nối TCP/UDP theo tiến trình từ Windows (không chặn, không driver). Byte gửi/nhận và tốc độ cần quyền quản trị — dùng nút Khởi động lại với quyền quản trị ở đầu cửa sổ.",NoteKind.Info);
   netSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true};Theme.BorderTop(netSummary);
   netFlow=new PacketFlowStrip{Dock=DockStyle.Top,Height=70,BackColor=Theme.Surface};Theme.BorderBottom(netFlow);
   netTab.Controls.Add(host);netTab.Controls.Add(netSummary);netTab.Controls.Add(netFlow);netTab.Controls.Add(netNote);netTab.Controls.Add(bar);
   Theme.SetOverlay(netOverlay,"Chưa đọc bảng kết nối.\r\nMở tab này để bắt đầu làm mới tự động theo chu kỳ đã chọn, hoặc bấm Làm mới.",NoteKind.Info);

   netTimer.Interval=(int)netInterval.Value*1000;
   netTimer.Tick+=async(s,e)=>{if(!netPaused&&tabs.SelectedTab==netTab&&!IsDisposed)await RefreshNetwork(false);};
   tabs.SelectedIndexChanged+=async(s,e)=>{if(tabs.SelectedTab==netTab){netTimer.Start();netFlow.Begin();if(netRows.Count==0)await RefreshNetwork(false);}else{netTimer.Stop();netFlow.End();}};
   FormClosed+=(s,e)=>{netTimer.Stop();netFlow.End();if(etw!=null){etw.Dispose();etw=null;}netIcons.Dispose();};
   UpdateNetworkSummary();
  }

  /// <summary>Starts or stops the ETW bandwidth session; the fixed session name is always stopped on exit.</summary>
  void ToggleBandwidth(){
   if(etw!=null&&etw.IsRunning){etw.Stop();etw.Dispose();etw=null;netTraffic.Clear();netBandwidth.Text="Bật băng thông (ETW)";Log("Mạng: đã dừng phiên ETW "+EtwNetworkSession.SessionName+".");RenderNetwork();return;}
   etw=new EtwNetworkSession();
   try{etw.Start();}catch(Exception e){etw.Dispose();etw=null;throw new IOException("Không bật được ETW: "+e.Message);}
   netBandwidth.Text="Dừng băng thông (ETW)";Log("Mạng: đã bật phiên ETW "+EtwNetworkSession.SessionName+" (kernel TCP/IP, chỉ đọc).");
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
    else if(etw!=null&&!etw.IsRunning&&etw.Failure!=""){Log("Mạng: phiên ETW dừng — "+etw.Failure);etw.Dispose();etw=null;netBandwidth.Text="Bật băng thông (ETW)";}
    RenderNetwork();
    if(manual)Log("Mạng: "+rows.Count+" kết nối, "+rows.Select(c=>c.Pid).Distinct().Count()+" tiến trình.");
   }catch(Exception e){Theme.SetOverlay(netOverlay,"Không đọc được bảng kết nối.\r\n"+e.Message,NoteKind.Error);if(manual)throw;}
   finally{netRefreshing=false;}
  }

  static string Rate(double bytesPerSecond){return bytesPerSecond<1?"":Presentation.BytesLabel((long)bytesPerSecond)+"/s";}

  /// <summary>Renders the current snapshot grouped by process, applying the search and loopback filters.</summary>
  void RenderNetwork(){
   string q=netSearch.Text.Trim();bool hideLoop=netHideLoopback.Checked;bool resolve=netResolve.Checked;
   string selectedKey=netList.SelectedItems.Count>0?((ConnectionInfo)netList.SelectedItems[0].Tag).Key:null;
   int top=netList.Items.Count>0?netList.TopItem!=null?netList.TopItem.Index:0:0;
   netList.BeginUpdate();netList.Items.Clear();netList.Groups.Clear();
   int shown=0;var processes=new HashSet<int>();
   foreach(var c in netRows.OrderBy(c=>ProcessResolver.Resolve(c.Pid).Display,StringComparer.CurrentCultureIgnoreCase).ThenBy(c=>c.Pid).ThenBy(c=>c.Protocol).ThenBy(c=>c.LocalPort)){
    if(hideLoop&&c.IsLoopback)continue;
    var id=ProcessResolver.Resolve(c.Pid);
    string hostName=resolve&&c.RemoteAddress!=null?HostResolver.Lookup(c.RemoteAddress,true):"";
    if(q!=""){string hay=id.Display+" "+id.Path+" "+c.Pid+" "+c.Protocol+" "+c.Local+" "+c.Remote+" "+c.State+" "+hostName+" "+id.Publisher;if(hay.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)<0)continue;}
    TrafficSample t;netTraffic.TryGetValue(c.Pid,out t);
    string dirKey=NetworkStats.DirectionKey(c,netTraffic,NetworkStats.MinRate);
    var row=new ListViewItem(new[]{id.Display,NetworkStats.DirectionArrows(dirKey),c.Pid.ToString(),id.Publisher,c.Protocol,c.Local,c.Remote,c.State,hostName,t==null?"":Presentation.BytesLabel(t.Sent),t==null?"":Presentation.BytesLabel(t.Received),t==null?"":Rate(t.SentPerSecond),t==null?"":Rate(t.ReceivedPerSecond)}){Tag=c,ImageKey=dirKey,ToolTipText=(id.Path==""?id.Display:id.Path)+"\r\n"+c.Protocol+" "+c.Local+(c.Remote==""?"":" → "+c.Remote)+(c.State==""?"":"  ["+c.State+"]")};
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
   else if(shown==0)Theme.SetOverlay(netOverlay,"Không có kết nối khớp với bộ lọc hiện tại.",NoteKind.Info);
   else Theme.SetOverlay(netOverlay,null,NoteKind.Info);
   if(netFlow!=null){double up=0,down=0;foreach(var s in netTraffic.Values){up+=s.SentPerSecond;down+=s.ReceivedPerSecond;}netFlow.SetRates(up,down);}
   UpdateNetworkSummary(shown,processes.Count);
  }

  void UpdateNetworkSummary(int shown=-1,int processes=-1){
   if(netSummary==null)return;
   var sb=new StringBuilder();
   sb.Append(shown<0?netRows.Count.ToString():shown.ToString()).Append(" kết nối hiển thị / ").Append(netRows.Count).Append(" tổng  •  ").Append(processes<0?netRows.Select(c=>c.Pid).Distinct().Count():processes).Append(" tiến trình");
   sb.Append(netPaused?"  •  Đã tạm dừng làm mới":"  •  Làm mới mỗi "+netInterval.Value+" giây");
   if(etw!=null&&etw.IsRunning){
    double up=netTraffic.Values.Sum(t=>t.SentPerSecond),down=netTraffic.Values.Sum(t=>t.ReceivedPerSecond);
    sb.Append("  •  ETW: ").Append(etw.Events.ToString("N0")).Append(" sự kiện");
    if(etw.EventsLost>0)sb.Append(", mất ").Append(etw.EventsLost.ToString("N0"));
    sb.Append("  •  Tổng ↑ ").Append(Rate(up)==""?"0 B/s":Rate(up)).Append("  ↓ ").Append(Rate(down)==""?"0 B/s":Rate(down));
   }else sb.Append(EtwNetworkSession.CanStart?"  •  Băng thông: chưa bật":"  •  Băng thông: cần quyền quản trị");
   netSummary.Text=sb.ToString();
  }

  /// <summary>Shows the full process path and endpoint details for the selected row.</summary>
  void ShowConnectionDetails(){
   if(netList.SelectedItems.Count==0)return;var c=(ConnectionInfo)netList.SelectedItems[0].Tag;var id=ProcessResolver.Resolve(c.Pid);
   TrafficSample t;netTraffic.TryGetValue(c.Pid,out t);
   MessageBox.Show(this,id.Display+"  (PID "+c.Pid+")\r\nĐường dẫn: "+(id.Path==""?"không đọc được (tiến trình được bảo vệ hoặc đã kết thúc)":id.Path)+"\r\nNhà phát hành (chữ ký): "+(id.Publisher==""?"—":id.Publisher)+"\r\n\r\n"+c.Protocol+"  "+c.Local+(c.Remote==""?"":"  →  "+c.Remote)+(c.State==""?"":"\r\nTrạng thái: "+c.State)+(t==null?"":"\r\n\r\nĐã gửi: "+Presentation.BytesLabel(t.Sent)+"  •  Đã nhận: "+Presentation.BytesLabel(t.Received)+" (từ khi bật ETW, toàn tiến trình)")+"\r\n\r\nTweek Pro chỉ hiển thị; không chặn hay thay đổi kết nối.","Chi tiết kết nối",MessageBoxButtons.OK,MessageBoxIcon.Information);
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
