using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Update;

namespace TweekPro {
 public partial class MainForm {
  /// <summary>Adds the "Báo lỗi / góp ý" button to a toolbar; it opens a small menu with prefilled GitHub Issues links instead of a single action.</summary>
  void AddFeedbackButton(FlowLayoutPanel bar){
   var b=Theme.Button("Báo lỗi / góp ý",ButtonStyle.Secondary);b.Margin=new Padding(0,0,8,8);
   var menu=new ContextMenuStrip{ShowImageMargin=false};
   // Show after the click finishes. Opening the menu inside Click makes the same mouse-up close it, so the list only appeared on the second press.
   b.Click+=(s,e)=>BeginInvoke((Action)(()=>{menu.Items.Clear();FillFeedbackMenu(menu);menu.Show(b,new System.Drawing.Point(0,b.Height));}));
   bar.Controls.Add(b);
  }

  void FillFeedbackMenu(ContextMenuStrip menu){
   string context=Core.L.F("Tab đang mở: {0}.",tabs.SelectedTab==null?"":tabs.SelectedTab.Text)+"\r\n\r\n";
   MenuItem(menu,"Báo lỗi trên GitHub…",()=>{OpenFeedback(Feedback.BugUrl(Version,Feedback.WindowsLabel(),context));return Task.FromResult(0);});
   MenuItem(menu,"Đề xuất tính năng…",()=>{OpenFeedback(Feedback.FeatureUrl(Version));return Task.FromResult(0);});
   MenuItem(menu,"Đánh giá / góp ý…",()=>{OpenFeedback(Feedback.FeedbackUrl(Version,Feedback.WindowsLabel()));return Task.FromResult(0);});
   menu.Items.Add(new ToolStripSeparator());
   MenuItem(menu,"Xem phản hồi đã gửi trên GitHub",()=>{OpenFeedback(Feedback.IssuesPage);return Task.FromResult(0);});
   MenuItem(menu,"Mở thư mục nhật ký để đính kèm",()=>{Directory.CreateDirectory(Core.Paths.Logs);Process.Start("explorer.exe","\""+Core.Paths.Logs+"\"");return Task.FromResult(0);});
  }

  /// <summary>Opens a GitHub Issues link in the default browser and notes it in the session log; GitHub requires a (free) account to submit.</summary>
  void OpenFeedback(string url){
   Log(Core.L.T("Mở GitHub Issues: ")+url.Split('?')[0]);
   Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
  }
 }
}
