using System;
using System.Collections.Generic;

namespace TweekPro.Network {
 /// <summary>English strings for the per-application network block; merged by Core.L, keys must not repeat elsewhere.</summary>
 public static class FirewallLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Chặn mạng","Network block"},{"Bỏ chặn mạng","Unblock network"},{"Đã chặn","Blocked"},{"Đang chặn mạng…","Blocked programs…"},
   {"Chặn mạng: {0}","Block network: {0}"},{"Bỏ chặn mạng: {0}","Unblock network: {0}"},
   {"Đã chặn mạng bằng Windows Firewall — bỏ chặn qua menu chuột phải hoặc Kho khôi phục.","Network blocked by Windows Firewall — unblock from the right-click menu or the Recovery Vault."},
   {"Chặn toàn bộ mạng của {0}?\r\n{1}\r\n\r\nTweek Pro tạo hai quy tắc Windows Firewall (vào + ra) chỉ cho tệp này; các tiến trình đang chạy mất kết nối ngay. Bỏ chặn bằng menu này hoặc trong Kho khôi phục (loại Chặn mạng).","Block all network traffic of {0}?\r\n{1}\r\n\r\nTweek Pro creates two Windows Firewall rules (in + out) for this file only; running processes lose connectivity immediately. Unblock from this menu or in the Recovery Vault (kind Network block)."},
   {"Mạng: đã chặn {0} ({1}).","Network: blocked {0} ({1})."},{"Mạng: đã bỏ chặn {0}.","Network: unblocked {0}."},
   {"Chương trình đang bị chặn mạng","Programs blocked from the network"},{"Bỏ chặn mục đã chọn","Unblock selected"},{"Bỏ chặn tất cả","Unblock all"},
   {"Không có chương trình nào đang bị Tweek Pro chặn mạng.\r\nChuột phải một ứng dụng trong bảng Mạng → Chặn mạng.","No program is currently blocked by Tweek Pro.\r\nRight-click an application in the Network table → Block network."},
   {"Bỏ chặn mạng cho tất cả {0} chương trình?","Unblock the network for all {0} programs?"},
   {"Danh sách đọc từ chính sách Windows Firewall (chỉ quy tắc do Tweek Pro tạo). Bỏ chặn xóa quy tắc và đánh dấu mục tương ứng trong Kho khôi phục là Đã khôi phục.","Read from the Windows Firewall policy (Tweek Pro rules only). Unblocking deletes the rules and marks the matching vault entry as Restored."},
   {"Bảng trên là từng ứng dụng đang gửi (↑ xanh dương) hoặc nhận (↓ xanh lá) dữ liệu theo thời gian thực, thanh màu là tốc độ; bấm một ứng dụng để xem các kết nối của nó ở bảng dưới. Chuột phải để kết thúc tiến trình, dừng dịch vụ hoặc Chặn mạng (quy tắc Windows Firewall vào + ra, gỡ được trong Kho khôi phục). Tốc độ và byte đo bằng ETW của Windows (tự bật khi có quyền quản trị).","The upper table shows each application sending (↑ blue) or receiving (↓ green) data in real time, the colored bar is the rate; click an application to see its connections below. Right-click to end the process, stop services or Block network (Windows Firewall rules in + out, removable in the Recovery Vault). Rates and bytes come from Windows ETW (enabled automatically when elevated)."},
   {"Không xác định được tệp thực thi của tiến trình này.","The executable of this process could not be determined."},{"Chỉ chặn được chương trình nằm trên ổ đĩa cục bộ.","Only programs on a local drive can be blocked."},{"Đường dẫn không hợp lệ.","Invalid path."},{"Chỉ chặn được tệp .exe.","Only .exe files can be blocked."},
   {"Tiến trình cốt lõi của Windows — chặn mạng sẽ làm hỏng Windows Update, đăng nhập hoặc Defender.","Windows core process — blocking it would break Windows Update, sign-in or Defender."},
   {"Tệp hệ thống trong thư mục Windows — không chặn để tránh hỏng cập nhật và bảo mật.","System file in the Windows folder — not blocked, to keep updates and security working."},
   {"{0} đã bị chặn mạng.","{0} is already blocked."},{"Đã chặn mạng (vào + ra) bằng Windows Firewall; khôi phục = gỡ quy tắc chặn.","Network blocked (in + out) through Windows Firewall; restore = remove the block rules."}
  };
 }
}
