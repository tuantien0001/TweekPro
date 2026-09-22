using System;
using System.Collections.Generic;

namespace TweekPro.RegClean {
 /// <summary>English strings for the Registry tab; merged by Core.L (keys must not repeat in other tables).</summary>
 public static class RegCleanLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Registry","Registry"},{"Registry cleaner","Registry cleaner"},{"Mục Registry","Registry entry"},{"Tệp đích không còn","Missing target file"},{"Tên hiển thị / dữ liệu","Display name / data"},{"Nhánh","Hive"},
   {"Quét Registry","Scan registry"},{"Dọn mục đã chọn (lưu kho)","Clean selected (save to vault)"},{"Mở trong Regedit","Open in Regedit"},
   {"Mục Uninstall mồ côi","Orphaned Uninstall entries"},{"Khóa trong danh sách Gỡ cài đặt mà trình gỡ, thư mục cài và biểu tượng đều không còn trên đĩa (bỏ qua MSI, thành phần hệ thống, bản cập nhật).","Uninstall-list keys whose uninstaller, install folder and icon are all gone from disk (MSI, system components and updates are skipped)."},
   {"App Paths trỏ tệp không tồn tại","App Paths pointing to missing files"},{"Đăng ký tên lệnh ngắn (Win+R) trỏ tới .exe đã bị xóa.","Short command names (Win+R) registered for a deleted .exe."},
   {"Đăng ký «Mở bằng» của ứng dụng đã mất","\"Open with\" registrations of missing programs"},{"Classes\\Applications\\<exe> có lệnh shell\\open trỏ tới tệp không còn; menu «Mở bằng» hết hiện mục hỏng.","Classes\\Applications\\<exe> whose shell\\open command points to a missing file; the \"Open with\" menu stops showing the broken entry."},
   {"MUICache của chương trình đã xóa","MUICache of deleted programs"},{"Tên hiển thị/nhà phát hành lưu đệm cho .exe không còn tồn tại.","Cached display name/publisher for an .exe that no longer exists."},
   {"SharedDLLs trỏ tệp không tồn tại","SharedDLLs pointing to missing files"},{"Bộ đếm tham chiếu DLL dùng chung cho tệp đã bị xóa.","Shared DLL reference counters for deleted files."},
   {"Shell extension đã duyệt nhưng DLL mất","Approved shell extensions with a missing DLL"},{"CLSID trong Shell Extensions\\Approved không còn đăng ký hoặc InprocServer32 trỏ DLL không tồn tại.","CLSIDs in Shell Extensions\\Approved that are no longer registered or whose InprocServer32 points to a missing DLL."},
   {"Registry cleaner bảo thủ: chỉ đề xuất mục mà tệp .exe/.dll đích chắc chắn không còn trên ổ cố định — khóa Uninstall mồ côi, App Paths, đăng ký «Mở bằng», MUICache, SharedDLLs, shell extension mất DLL. Không «tối ưu» Registry, không đụng CLSID, Run, SYSTEM, Policies. Mỗi lần dọn tạo một bản sao lưu trong Kho; khôi phục ghi lại đúng khóa/giá trị đã xóa nếu chưa tồn tại.","Conservative registry cleaner: only proposes entries whose target .exe/.dll is verifiably gone from a fixed drive — orphaned Uninstall keys, App Paths, \"Open with\" registrations, MUICache, SharedDLLs, shell extensions with a missing DLL. No registry \"optimization\"; CLSID, Run, SYSTEM and Policies are never touched. Each clean creates one vault backup; restore writes back exactly the removed keys/values if they do not exist."},
   {"Chưa quét.\r\nBấm Quét Registry để tìm mục mồ côi theo sáu nhóm. Chỉ đọc cho đến khi bạn đánh dấu, bấm Dọn và xác nhận.","Not scanned yet.\r\nClick Scan registry to find orphaned entries in six groups. Read-only until you check items, click Clean and confirm."},
   {"Tệp đích: ","Target file: "},{"{0} mục mồ côi trong {1} nhóm  •  Đã đánh dấu {2}  •  Dọn lưu bản sao vào Kho trước khi xóa","{0} orphaned entries in {1} groups  •  Checked {2}  •  Clean saves a copy to the vault before deleting"},
   {"Đang quét Registry…","Scanning registry…"},{"Registry: đang quét mục mồ côi (chỉ đọc).","Registry: scanning for orphaned entries (read-only)."},{"Quét xong: {0} mục mồ côi","Scan finished: {0} orphaned entries"},{"Registry: {0} mục mồ côi.","Registry: {0} orphaned entries."},
   {"Không tìm thấy mục Registry mồ côi trong sáu nhóm được kiểm tra.","No orphaned registry entries found in the six groups checked."},
   {"Quét rồi đánh dấu mục muốn dọn.","Scan first, then check the entries to clean."},
   {"Xóa {0} mục Registry mồ côi?\r\n\r\n{1}\r\n\r\nMỗi nhóm được lưu vào Kho khôi phục trước; tệp đích được kiểm tra lại ngay trước khi xóa.","Delete {0} orphaned registry entries?\r\n\r\n{1}\r\n\r\nEach group is saved to the Recovery Vault first; the target file is re-checked right before deletion."},
   {"Đang dọn Registry…","Cleaning registry…"},{"Đã xóa {0} mục Registry. Lỗi: {1}. Bản sao lưu: {2}.","Deleted {0} registry entries. Errors: {1}. Backups: {2}."},
   {"Registry: ","Registry: "},{"Registry lỗi: ","Registry error: "},{"Kết quả dọn Registry","Registry cleanup result"},
   {"Chọn một mục để mở trong Regedit.","Select an entry to open in Regedit."},
   {"Đã chạm giới hạn; kết quả chưa đủ.","Limit reached; results incomplete."},{"Tệp đích đã xuất hiện lại; giữ nguyên.","The target file reappeared; kept."}
  };
 }
}
