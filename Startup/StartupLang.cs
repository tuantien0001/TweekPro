using System;
using System.Collections.Generic;

namespace TweekPro.Startup {
 /// <summary>English strings for startup insight (Autorun tab columns, ratings, flags); merged by Core.L.</summary>
 public static class StartupLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Windows","Windows"},{"Tác động","Impact"},{"Chữ ký","Signature"},{"Kích cỡ","Size"},
   {"Windows đã tắt","Disabled by Windows"},{"Đích không tồn tại","Target missing"},{"Thấp","Low"},{"Trung bình","Medium"},{"Cao","High"},
   {"Đang bật","Enabled"},{"Đã tắt","Disabled"},{"Đã tắt {0}","Disabled {0}"},
   {"Chạy từ thư mục Temp","Runs from the Temp folder"},{"Chạy từ Downloads","Runs from Downloads"},{"Chưa ký, nằm trong AppData","Unsigned, located in AppData"},
   {"Đích: ","Target: "},{"Cảnh báo: ","Warnings: "},
   {"Run, shortcut Startup, ứng dụng Windows (Copilot, Terminal, Teams…) và dịch vụ tự chạy cùng máy. Bỏ dấu tích để tắt — trạng thái cũ vào Kho, bật lại được. Dịch vụ cốt lõi không tắt được và dịch vụ đang chạy không bị dừng. Mục đích không tồn tại được tô cam.","Run keys, Startup shortcuts, Windows apps (Copilot, Terminal, Teams…) and services that start with Windows. Clear the check to turn one off — the previous state goes to the Vault and can be turned back on. Core services cannot be turned off, and a running service is not stopped. Entries whose target is missing are highlighted orange."},
   {"Đã đọc {0} mục khởi động: {1} đang bật, {2} đã tắt, {3} đích không tồn tại ({4} ứng dụng Windows, {5} dịch vụ tự chạy).","Read {0} startup entries: {1} enabled, {2} disabled, {3} with a missing target ({4} Windows apps, {5} auto-start services)."},
   {"{0} mục đang bật khi đăng nhập (Run, Startup, ứng dụng Windows); {1} mục trỏ tới tệp không còn tồn tại. Mỗi mục kéo dài thời gian mở máy.","{0} entries run at sign-in (Run, Startup, Windows apps); {1} point to a file that no longer exists. Each entry lengthens boot time."},
   {"Run / Startup","Run / Startup"},{"Dịch vụ tự chạy","Auto-start services"},{"Tự chạy cùng máy","Starts with Windows"},{"Dịch vụ cốt lõi","Core service"},
   {"Người dùng đã tắt","Turned off by you"},{"Chính sách đã tắt","Turned off by policy"},{"Đang tắt","Off"},
   {"Khởi động ứng dụng","Startup app"},
   {"Không tìm thấy mục khởi động.\r\nMục toàn máy có thể cần quyền quản trị.","No startup entries found.\r\nMachine-wide entries may require administrator rights."},
   {"Bật lại {0} từ Kho khôi phục?","Turn {0} back on from the Recovery Vault?"},
   {"Bật khởi động của ứng dụng Windows {0}?\r\nÁp dụng từ lần đăng nhập sau. Trạng thái cũ được lưu vào Kho.","Turn on startup for the Windows app {0}?\r\nApplies from the next sign-in. The previous state is saved to the Vault."},
   {"Tắt khởi động của ứng dụng Windows {0}?\r\nÁp dụng từ lần đăng nhập sau. Trạng thái cũ được lưu vào Kho.","Turn off startup for the Windows app {0}?\r\nApplies from the next sign-in. The previous state is saved to the Vault."},
   {"Tắt khởi động cùng máy của dịch vụ {0}?\r\n\r\nDịch vụ đang chạy không bị dừng. Lần khởi động sau nó sẽ không tự chạy. Kiểu khởi động cũ được lưu vào Kho.","Stop service {0} from starting with Windows?\r\n\r\nThe running service is not stopped. It will not start at the next boot. The previous start mode is saved to the Vault."},
   {"Mục này không có công tắc bật/tắt của Windows. Hãy tắt để đưa vào Kho, rồi bật lại từ Kho.","This entry has no Windows on/off switch. Turn it off to move it into the Vault, then turn it back on from the Vault."},
   {"Bật lại {0}? Windows đang tắt mục này.\r\nTweek Pro chỉ bật cờ StartupApproved; lệnh Run hoặc shortcut giữ nguyên. Cờ cũ được lưu vào Kho.","Turn {0} back on? Windows has it turned off.\r\nTweek Pro only sets the StartupApproved flag; the Run command or shortcut stays. The previous flag is saved to the Vault."},
   {"Không phải bản sao lưu cờ khởi động.","Not a startup-flag backup."},
   {"Chỉ khôi phục được cờ StartupApproved.","Only a StartupApproved flag can be restored."},
   {"Tên giá trị không hợp lệ.","The value name is not valid."},
   {"Bản sao lưu không khớp tên giá trị.","The backup does not match the value name."},
   {"Cờ khởi động","Startup flag"},
   {"Không phải mục khởi động của ứng dụng Windows.","Not a Windows app startup entry."},
   {"Mục khởi động không còn tồn tại.","That startup entry no longer exists."},
   {"Mục khởi động không có trạng thái hợp lệ.","That startup entry has no valid state."},
   {"Chính sách Windows khóa mục khởi động này.","Windows policy locks this startup entry."},
   {"Mục khởi động này đang tắt.","This startup entry is already off."},
   {"Mục khởi động này đang bật.","This startup entry is already on."},
   {"Không phải bản sao lưu khởi động ứng dụng Windows.","Not a Windows app startup backup."},
   {"Đường dẫn khôi phục không hợp lệ.","The restore path is not valid."},
   {"Bản sao lưu trống.","The backup is empty."},
   {"Trạng thái đã lưu không hợp lệ.","The saved state is not valid."},
   {"Driver khởi động cùng nhân Windows — không tắt từ đây.","This driver starts with the Windows kernel — it cannot be turned off here."},
   {"Dịch vụ đã được tắt khởi động.","The service is already set not to start."},
   {"{0} là dịch vụ cốt lõi của Windows — không tắt khởi động cùng máy.","{0} is a core Windows service — its startup cannot be turned off."},
   {"Đã tắt khởi động cùng máy; dịch vụ đang chạy không bị dừng. Khôi phục = kiểu khởi động {0}.","Startup with Windows is off; the running service was not stopped. Restore sets the start mode back to {0}."},
   {"Không tắt được khởi động của dịch vụ {0}: {1}","Could not turn off startup for service {0}: {1}"},
   {"Có mục khởi động hỏng","Broken startup entries"},
  };
 }
}
