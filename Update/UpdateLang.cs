using System;
using System.Collections.Generic;

namespace TweekPro.Update {
 /// <summary>English strings for the in-app update checker; merged by Core.L.</summary>
 public static class UpdateLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Kiểm tra cập nhật","Check for updates"},
   {"Tải bản mới","Download update"},{"Bỏ qua bản này","Skip this version"},
   {"Có bản Tweek Pro {0} mới (đang dùng {1}). Tải bộ cài để cập nhật — ứng dụng không tự ghi đè.","Tweek Pro {0} is available (you are running {1}). Download the installer to update — the app never overwrites itself."},
   {"Đã bỏ qua thông báo cho bản {0}.","Update notice for version {0} dismissed."},
   {"Có cập nhật mới","Update available"},
   {"Đang kiểm tra cập nhật trên GitHub…","Checking GitHub for updates…"},
   {"Không kiểm tra được cập nhật: ","Could not check for updates: "},
   {"Không kết nối được GitHub Releases.\r\n\r\n","Could not reach GitHub Releases.\r\n\r\n"},
   {"Đang dùng bản mới nhất ({0}).","Already on the latest release ({0})."},
   {"Bạn đang dùng Tweek Pro {0}.\r\nKhông có bản phát hành mới hơn trên GitHub.","You are running Tweek Pro {0}.\r\nNo newer GitHub Release is available."},
   {"Có bản mới: {0} (đang dùng {1}).","Update available: {0} (running {1})."},
   {"Có bản phát hành mới: {0}\r\nBạn đang dùng {1}.{2}\r\n\r\nMở trang tải trên GitHub?","A new release is available: {0}\r\nYou are running {1}.{2}\r\n\r\nOpen the download page on GitHub?"},
   {"Cập nhật cần tải bản mới và khởi động lại; ứng dụng không tự ghi đè Program Files.","An update requires downloading a new build and restarting; the app never overwrites Program Files by itself."},
   {"Phản hồi GitHub trống.","Empty response from GitHub."},
   {"Không đọc được bản phát hành.","Could not parse the release."},
   {"Chưa có GitHub Release (cần gắn tag v*). Push nhánh chỉ tạo artifact Actions, không phải bản phát hành.","No GitHub Release yet (create a v* tag). Branch pushes only produce Actions artifacts, not a published release."},
   {"Chỉ có tag phiên bản trên GitHub, chưa có Release kèm bộ cài. Mở trang Releases để tải artifact hoặc đợi bản phát hành chính thức.","Only a version tag exists on GitHub; there is no Release with an installer yet. Open the Releases page for Actions artifacts or wait for an official release."},
  };
 }
}
