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
   {"Run, RunOnce và shortcut Startup của tài khoản hiện tại/toàn máy, kèm trạng thái Windows cho phép chạy (Task Manager › Startup), chữ ký số, kích cỡ và tác động ước tính theo kích cỡ tệp. Mục đích không tồn tại được tô cam. Bật lại áp dụng cho mục đã tắt bằng Tweek Pro.","Run, RunOnce and Startup shortcuts for the current user and the whole machine, with Windows' own enabled/disabled state (Task Manager › Startup), digital signature, size and an impact estimate based on file size. Entries whose target no longer exists are highlighted orange. Re-enable applies to entries disabled by Tweek Pro."},
   {"Đã đọc {0} mục khởi động: {1} đang chạy khi đăng nhập, {2} Windows đã tắt, {3} đích không tồn tại. Chưa gồm service/driver/tác vụ lịch.","Read {0} startup entries: {1} run at sign-in, {2} disabled by Windows, {3} with a missing target. Services, drivers and scheduled tasks are not included."},
   {"{0} mục đang bật (Run/RunOnce/Startup); {1} mục trỏ tới tệp không còn tồn tại. Mỗi mục kéo dài thời gian mở máy.","{0} entries are enabled (Run/RunOnce/Startup); {1} point to a file that no longer exists. Each entry lengthens boot time."},
   {"Có mục khởi động hỏng","Broken startup entries"},
  };
 }
}
