using System;
using System.Collections.Generic;

namespace TweekPro.Tracks {
 /// <summary>English strings for the Privacy Tracks tab; merged by Core.L (keys must not repeat in other tables).</summary>
 public static class TracksLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Dấu vết","Tracks"},{"Số mục","Items"},{"Chọn mặc định","Select defaults"},{"Dọn dấu vết đã chọn (vào kho)","Clean selected tracks (to vault)"},{"Đang xem trước…","Previewing…"},
   {"Tệp mở gần đây (Recent)","Recently opened files (Recent)"},{"Shortcut tới tệp và thư mục vừa mở; Explorer và hộp Mở/Lưu dùng để gợi ý.","Shortcuts to files and folders you just opened; Explorer and Open/Save dialogs use them for suggestions."},
   {"Jump List trên thanh tác vụ","Taskbar Jump Lists"},{"Danh sách «gần đây / thường dùng» khi chuột phải biểu tượng trên taskbar và Start.","The \"recent / frequent\" lists shown when right-clicking taskbar and Start icons."},
   {"Lịch sử hoạt động (Timeline)","Activity history (Timeline)"},{"Cơ sở dữ liệu ActivitiesCache.db của Timeline / Đồng bộ hoạt động; tệp đang được dịch vụ giữ sẽ bị bỏ qua.","The ActivitiesCache.db database behind Timeline / activity sync; files held by the service are skipped."},
   {"Lịch sử hộp Run (Win+R)","Run dialog history (Win+R)"},{"Các lệnh đã gõ vào hộp Run.","Commands typed into the Run box."},
   {"Đường dẫn đã gõ vào thanh địa chỉ","Paths typed into the address bar"},{"Đường dẫn gõ vào thanh địa chỉ Explorer.","Paths typed into the Explorer address bar."},
   {"RecentDocs (tài liệu theo đuôi tệp)","RecentDocs (documents by extension)"},{"Tên tệp vừa mở, xếp theo phần mở rộng; nguồn của menu «Recent» trong Start.","Names of files just opened, grouped by extension; feeds the Start \"Recent\" menu."},
   {"Hộp Mở / Lưu (ComDlg32)","Open / Save dialogs (ComDlg32)"},{"Tệp và thư mục dùng gần đây trong hộp thoại Mở/Lưu của mọi ứng dụng.","Files and folders recently used in every application's Open/Save dialog."},
   {"Từ khóa tìm trong Explorer","Explorer search terms"},{"Chuỗi đã gõ vào ô tìm kiếm của Explorer (WordWheelQuery).","Strings typed into the Explorer search box (WordWheelQuery)."},
   {"Thống kê chạy chương trình (UserAssist)","Program launch statistics (UserAssist)"},{"Số lần và lần cuối chạy từng chương trình từ Start; Start dùng để xếp «Most used».","Launch count and last run of each program from Start; Start uses it for \"Most used\"."},
   {"Tệp gần đây của Paint, WordPad, Regedit","Recent files of Paint, WordPad, Regedit"},{"Danh sách tệp gần đây của Paint/WordPad và khóa mở lần cuối của Regedit.","Recent file lists of Paint/WordPad and the last key opened in Regedit."},
   {"Chrome / Edge / Brave — lịch sử duyệt web","Chrome / Edge / Brave — browsing history"},{"History, Visited Links, Top Sites, Shortcuts của từng hồ sơ. Không đụng mật khẩu, dấu trang, tiện ích. Khóa khi trình duyệt còn chạy.","History, Visited Links, Top Sites, Shortcuts of every profile. Passwords, bookmarks and extensions are untouched. Locked while the browser runs."},
   {"Chrome / Edge / Brave — cookie (đăng xuất mọi web)","Chrome / Edge / Brave — cookies (signs you out everywhere)"},{"Xóa cookie đăng nhập của mọi trang web; bạn sẽ phải đăng nhập lại. Mặc định không chọn.","Removes login cookies of every website; you will have to sign in again. Unchecked by default."},
   {"Firefox — lịch sử biểu mẫu và phiên","Firefox — form history and sessions"},{"formhistory.sqlite (chuỗi đã gõ vào ô nhập) và sessionstore-backups. Không đụng places.sqlite (lịch sử + dấu trang), mật khẩu, cookie.","formhistory.sqlite (strings typed into fields) and sessionstore-backups. places.sqlite (history + bookmarks), passwords and cookies are untouched."},
   {"Dấu vết là dữ liệu Windows và trình duyệt ghi lại thói quen dùng máy: tệp mở gần đây, Jump List, lịch sử Run, hộp Mở/Lưu, từ khóa tìm, lịch sử duyệt web. Chỉ những khóa HKCU và thư mục trong danh sách cho phép mới được dọn; không đụng mật khẩu, dấu trang, tiện ích. Mọi thứ đi vào Kho khôi phục: tệp được chuyển đi, giá trị Registry được lưu rồi xóa và có thể ghi lại. Cookie đăng nhập mặc định không chọn.","Tracks are the data Windows and browsers keep about how you use the PC: recent files, Jump Lists, Run history, Open/Save dialogs, search terms, browsing history. Only the allow-listed HKCU keys and folders are cleaned; passwords, bookmarks and extensions are untouched. Everything goes to the Recovery Vault: files are moved, registry values are saved then deleted and can be written back. Login cookies start unchecked."},
   {"Chưa xem trước.\r\nBấm Xem trước để đếm tệp và giá trị Registry của từng loại dấu vết. Chưa có gì bị thay đổi cho đến khi bạn bấm Dọn và xác nhận.","Not previewed yet.\r\nClick Preview to count the files and registry values of each track type. Nothing changes until you click Clean and confirm."},
   {"  •  nhạy cảm","  •  sensitive"},
   {"{0} loại dấu vết  •  Đã chọn {1} loại, {2} mục, {3}  •  Chuyển vào Kho: khôi phục được, không ghi đè mục mới hơn","{0} track types  •  Selected {1} types, {2} items, {3}  •  To vault: restorable, never overwrites newer entries"},
   {"Dấu vết: đang xem trước {0} loại (chỉ đọc).","Tracks: previewing {0} types (read-only)."},{"Xem trước xong: {0} mục, {1} tệp","Preview finished: {0} items, {1} of files"},
   {"  •  {0} loại bị khóa (đóng trình duyệt rồi xem trước lại)","  •  {0} types locked (close the browser and preview again)"},{"Dấu vết: {0} mục, {1}, {2} loại bị khóa.","Tracks: {0} items, {1}, {2} types locked."},
   {"Không tìm thấy dấu vết nào trong các vị trí cho phép.","No tracks found in the allowed locations."},
   {"Xem trước rồi đánh dấu loại dấu vết muốn dọn.","Preview first, then check the track types to clean."},
   {"Bạn đã chọn cookie đăng nhập: mọi trang web sẽ yêu cầu đăng nhập lại. Tiếp tục?","You selected login cookies: every website will ask you to sign in again. Continue?"},
   {"Dọn {0} mục dấu vết vào Kho khôi phục?\r\n\r\n{1}\r\n\r\nMỗi loại tạo một bản sao lưu; giá trị Registry được ghi lại khi khôi phục nếu chưa có mục mới hơn.","Clean {0} track items into the Recovery Vault?\r\n\r\n{1}\r\n\r\nEach type creates one backup; registry values are written back on restore unless newer entries exist."},
   {"{0} mục","{0} items"},{" mục"," items"},
   {"Đã dọn {0} mục ({1}). Bỏ qua vì đang dùng: {2}. Lỗi: {3}. Bản sao lưu: {4}.","Cleaned {0} items ({1}). Skipped in use: {2}. Errors: {3}. Backups: {4}."},
   {"Dấu vết: ","Tracks: "},{"Dọn dấu vết lỗi: ","Track cleanup error: "},{"Kết quả dọn dấu vết","Track cleanup result"},
   {"Chọn một loại dấu vết để xem mục.","Select a track type to view its items."},{"Mục sẽ dọn — ","Items to clean — "},{"Đường dẫn / giá trị","Path / value"},
   {"Đang chạy: chrome — đóng trình duyệt rồi xem trước lại.","Running: chrome — close the browser and preview again."}
  };
 }
}
