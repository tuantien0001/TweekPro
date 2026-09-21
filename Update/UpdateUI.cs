using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using UpdateCheck=TweekPro.Update.UpdateCheck;

namespace TweekPro {
 public partial class MainForm {
  /// <summary>Opt-in GitHub update check: notifies and opens the release/download page; never overwrites Program Files.</summary>
  async Task CheckForUpdates(){
   Log(Core.L.T("Đang kiểm tra cập nhật trên GitHub…"));
   var info=await Task.Run(()=>UpdateCheck.Check(Version));
   if(!String.IsNullOrEmpty(info.Error)){
    Log(Core.L.T("Không kiểm tra được cập nhật: ")+Core.L.T(info.Error));
    MessageBox.Show(this,Core.L.T("Không kết nối được GitHub Releases.\r\n\r\n")+Core.L.T(info.Error)+"\r\n\r\n"+Core.L.T("Cập nhật cần tải bản mới và khởi động lại; ứng dụng không tự ghi đè Program Files."),Core.L.T("Kiểm tra cập nhật"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
    return;
   }
   if(!info.UpdateAvailable){
    Log(Core.L.F("Đang dùng bản mới nhất ({0}).",info.Current));
    MessageBox.Show(this,Core.L.F("Bạn đang dùng Tweek Pro {0}.\r\nKhông có bản phát hành mới hơn trên GitHub.",info.Current)+"\r\n\r\n"+Core.L.T("Cập nhật cần tải bản mới và khởi động lại; ứng dụng không tự ghi đè Program Files."),Core.L.T("Kiểm tra cập nhật"),MessageBoxButtons.OK,MessageBoxIcon.Information);
    return;
   }
   Log(Core.L.F("Có bản mới: {0} (đang dùng {1}).",info.Latest,info.Current));
   string body=String.IsNullOrWhiteSpace(info.Notes)?"":"\r\n\r\n"+Core.L.T(info.Notes.Trim());
   if(body.Length>600)body=body.Substring(0,600)+"…";
   var choice=MessageBox.Show(this,Core.L.F("Có bản phát hành mới: {0}\r\nBạn đang dùng {1}.{2}\r\n\r\nMở trang tải trên GitHub?",info.Latest,info.Current,body),Core.L.T("Có cập nhật mới"),MessageBoxButtons.YesNo,MessageBoxIcon.Information);
   if(choice!=DialogResult.Yes)return;
   string url=!String.IsNullOrEmpty(info.InstallerUrl)?info.InstallerUrl:info.PageUrl;
   if(!String.IsNullOrEmpty(url))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});
  }
 }
}
