using System;
using System.Collections.Generic;

namespace TweekPro.Stale {
 /// <summary>English strings for the Old Downloads tab; merged by Core.L (keys must not repeat in other tables).</summary>
 public static class StaleLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Tệp tải về cũ","Old Downloads"},{"Tệp cũ","Old file"},
   {"Tuổi (ngày)","Age (days)"},
   {"Quét tệp cũ","Scan old files"},{"Dọn tệp đã chọn","Clean selected files"},{"Dọn tệp đã chọn (vào kho)","Clean selected files (to vault)"},{"Xóa thẳng tệp đã chọn","Delete selected files permanently"},
   {"Quét trong:","Scan in:"},{"Cũ hơn (ngày):","Older than (days):"},{"Tệp lớn từ (MB):","Large file from (MB):"},
   {"Bộ cài","Installer"},{"Tệp nén","Archive"},{"Ảnh đĩa","Disk image"},{"Tải dở","Partial download"},{"Tệp lớn","Large file"},
   {"Chỉ quét Downloads và Desktop của tài khoản hiện tại: bộ cài (.exe/.msi/.msix), tệp nén, ảnh đĩa, tệp tải dở và tệp lớn cũ hơn số ngày đã chọn. Bỏ qua shortcut, tệp ẩn/hệ thống, thư mục OneDrive/Dropbox. Chưa có gì bị xóa cho đến khi bạn đánh dấu và xác nhận; mặc định chuyển vào Kho khôi phục.","Scans only the current user's Downloads and Desktop: installers (.exe/.msi/.msix), archives, disk images, partial downloads and large files older than the chosen number of days. Shortcuts, hidden/system files and OneDrive/Dropbox folders are skipped. Nothing is deleted until you check items and confirm; the default moves them to the Recovery Vault."},
   {"Chưa quét.\r\nBấm Quét tệp cũ để liệt kê bộ cài, tệp nén, ảnh đĩa, tệp tải dở và tệp lớn đã lâu không đụng tới trong Downloads (và Desktop nếu chọn).","Not scanned yet.\r\nClick Scan old files to list installers, archives, disk images, partial downloads and large files untouched for a long time in Downloads (and Desktop if selected)."},
   {"Tạo: {0}  •  Sửa: {1}  •  {2} ngày","Created: {0}  •  Modified: {1}  •  {2} days"},
   {"{0} tệp cũ ({1})  •  Đã chọn {2} tệp, {3}","{0} old files ({1})  •  Selected {2} files, {3}"},
   {"Chọn ít nhất một thư mục (Downloads hoặc Desktop) còn tồn tại.","Select at least one existing folder (Downloads or Desktop)."},
   {"Tệp tải về cũ: đang quét (chỉ đọc) {0}, cũ hơn {1} ngày, tệp lớn từ {2} MB.","Old Downloads: scanning (read-only) {0}, older than {1} days, large files from {2} MB."},
   {"Quét xong: {0} tệp cũ ({1}) trong {2} tệp đã duyệt","Scan finished: {0} old files ({1}) out of {2} files checked"},
   {"Tệp tải về cũ: {0} tệp, {1}.","Old Downloads: {0} files, {1}."},{"Tệp tải về cũ: ","Old Downloads: "},
   {"Không có tệp cũ hơn {0} ngày khớp tiêu chí trong thư mục đã chọn.","No files older than {0} days match the criteria in the selected folders."},
   {"Quét rồi đánh dấu tệp muốn dọn.","Scan first, then check the files to clean."},
   {"XÓA THẲNG {0} tệp ({1}) mà KHÔNG lưu bản khôi phục?\r\n\r\n{2}\r\n\r\nThao tác này không thể hoàn tác.","PERMANENTLY DELETE {0} files ({1}) WITHOUT a recovery copy?\r\n\r\n{2}\r\n\r\nThis cannot be undone."},
   {"Chuyển {0} tệp ({1}) vào Kho khôi phục?\r\n\r\n{2}\r\n\r\nCó thể khôi phục hoặc xóa vĩnh viễn sau trong tab Kho khôi phục. Dung lượng ổ đĩa chưa được giải phóng cho đến khi xóa vĩnh viễn.","Move {0} files ({1}) to the Recovery Vault?\r\n\r\n{2}\r\n\r\nYou can restore or permanently delete them later in the Recovery Vault tab. Disk space is not freed until they are purged."},
   {"Tệp tải về cũ: bắt đầu xóa thẳng {0} tệp.","Old Downloads: permanently deleting {0} files."},{"Tệp tải về cũ: bắt đầu chuyển vào kho {0} tệp.","Old Downloads: moving {0} files to the vault."},
   {"Dọn tệp cũ lỗi: ","Old file cleanup error: "},{"Kết quả dọn tệp cũ","Old file cleanup result"},
   {"Chọn một tệp để mở vị trí.","Select a file to open its location."},{"Tệp không còn tồn tại: ","File no longer exists: "},
   {"{0} ngày","{0} days"}
  };
 }
}
