using System;
using System.Collections.Generic;

namespace TweekPro.SysSpace {
 /// <summary>English strings for the System Space tab; merged by Core.L (keys must not repeat in other tables).</summary>
 public static class SystemSpaceLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Dung lượng hệ thống","System Space"},{"Công cụ Windows","Windows tool"},{"Nguồn điện","Power"},{"Sao lưu","Backups"},
   {"Đo dung lượng","Measure"},{"Chạy công cụ cho mục đang chọn","Run tool for selected item"},{"Mở Storage Sense","Open Storage Sense"},
   {"Windows.old — bản Windows trước","Windows.old — previous Windows"},{"Bản cài trước còn giữ để quay lui sau khi nâng cấp lớn; Windows tự xóa sau 10 ngày nhưng thường sót lại.","The previous installation kept for rollback after a feature upgrade; Windows removes it after 10 days but it often lingers."},
   {"Dọn bằng Disk Cleanup (Previous Installations)","Clean with Disk Cleanup (Previous Installations)"},{"Sau khi dọn sẽ KHÔNG thể quay lui về bản Windows trước.","After cleaning you can NO LONGER roll back to the previous Windows."},
   {"WinSxS — kho thành phần","WinSxS — component store"},{"Kho thành phần Windows chứa mọi phiên bản tệp hệ thống đã cập nhật. Chỉ dọn bằng DISM; không bao giờ xóa tay.","The Windows component store holding every updated system file version. Cleaned only through DISM; never deleted by hand."},
   {"DISM /StartComponentCleanup","DISM /StartComponentCleanup"},{"Chạy vài phút; không dùng /ResetBase nên vẫn gỡ được bản cập nhật gần nhất.","Takes a few minutes; /ResetBase is not used so the latest updates stay uninstallable."},
   {"Bộ đệm Delivery Optimization","Delivery Optimization cache"},{"Tệp cập nhật Windows/Store tải về để chia sẻ ngang hàng với máy khác trong mạng.","Windows/Store update files downloaded for peer sharing with other PCs on the network."},
   {"Delete-DeliveryOptimizationCache","Delete-DeliveryOptimizationCache"},{"Windows sẽ tải lại khi cần; không ảnh hưởng dữ liệu.","Windows re-downloads when needed; no data is affected."},
   {"Driver cũ trùng lặp trong DriverStore","Older duplicate drivers in the DriverStore"},{"Gói driver bên thứ ba còn nhiều phiên bản cho cùng một INF; chỉ đề xuất bản cũ hơn, bỏ qua driver hộp và driver khởi động.","Third-party driver packages with several versions of the same INF; only older versions are proposed, inbox and boot-critical drivers are skipped."},
   {"pnputil /delete-driver (không cưỡng bức)","pnputil /delete-driver (no force)"},{"pnputil từ chối gói đang được thiết bị dùng — đó là lớp an toàn, Tweek Pro không dùng /force.","pnputil refuses packages a device is using — that is the safety net; Tweek Pro never uses /force."},
   {"hiberfil.sys — tệp ngủ đông","hiberfil.sys — hibernation file"},{"Ảnh RAM cho Hibernate và Fast Startup, thường 40–75% dung lượng RAM.","RAM image for Hibernate and Fast Startup, usually 40–75% of RAM size."},
   {"powercfg /hibernate off","powercfg /hibernate off"},{"Tắt luôn Hibernate và Fast Startup; bật lại bằng powercfg /hibernate on.","Also disables Hibernate and Fast Startup; re-enable with powercfg /hibernate on."},
   {"pagefile.sys + swapfile.sys","pagefile.sys + swapfile.sys"},{"Bộ nhớ ảo do Windows quản lý. Chỉ hiển thị; đổi kích thước trong System Properties → Performance.","Virtual memory managed by Windows. Display only; resize in System Properties → Performance."},
   {"Mở System Properties → Performance","Open System Properties → Performance"},
   {"Điểm khôi phục hệ thống (Shadow Copies)","System restore points (Shadow Copies)"},{"Dung lượng System Restore / Volume Shadow Copy đang dùng trên các ổ. Chỉ hiển thị; xóa điểm cũ trong System Protection.","Space used by System Restore / Volume Shadow Copy on each drive. Display only; delete old points in System Protection."},
   {"Mở System Protection","Open System Protection"},
   {"Thùng rác (mọi ổ đĩa)","Recycle Bin (all drives)"},{"Tệp đã xóa còn nằm trong $Recycle.Bin của tất cả ổ cố định.","Deleted files still sitting in $Recycle.Bin on every fixed drive."},
   {"Clear-RecycleBin","Clear-RecycleBin"},{"Tệp trong thùng rác bị xóa hẳn, không qua Kho khôi phục.","Recycle Bin contents are deleted for good, bypassing the Recovery Vault."},
   {"Những vùng này thuộc Windows nên Tweek Pro không tự xóa tệp: mỗi mục có đúng một công cụ chính chủ (Disk Cleanup cho Windows.old, DISM cho WinSxS, pnputil cho driver cũ, powercfg cho hiberfil, cmdlet cho Delivery Optimization và Thùng rác). Kết quả không đi qua Kho khôi phục — hãy đọc cảnh báo của từng mục trước khi chạy. Cần quyền quản trị.","These areas belong to Windows, so Tweek Pro never deletes their files itself: each item maps to exactly one first-party tool (Disk Cleanup for Windows.old, DISM for WinSxS, pnputil for old drivers, powercfg for hiberfil, cmdlets for Delivery Optimization and the Recycle Bin). Results bypass the Recovery Vault — read each item's warning before running. Administrator rights required."},
   {"Chưa đo.\r\nBấm Đo dung lượng để xem Windows.old, WinSxS, Delivery Optimization, driver cũ, hiberfil, pagefile, điểm khôi phục và thùng rác chiếm bao nhiêu. Chỉ đọc.","Not measured yet.\r\nClick Measure to see how much Windows.old, WinSxS, Delivery Optimization, old drivers, hiberfil, pagefile, restore points and the Recycle Bin occupy. Read-only."},
   {"Chưa đo","Not measured"},{"Chưa đo.","Not measured."},{"Chỉ hiển thị","Display only"},{"Không có / không áp dụng","Absent / not applicable"},{"Có thể tắt (đảo ngược được)","Can be turned off (reversible)"},{"Có thể dọn bằng công cụ Windows","Cleanable with a Windows tool"},
   {"Có thể thu hồi bằng công cụ Windows: khoảng {0}  •  Không qua Kho khôi phục","Reclaimable with Windows tools: about {0}  •  Bypasses the Recovery Vault"},
   {"Dung lượng hệ thống: đang đo (chỉ đọc).","System Space: measuring (read-only)."},{"Đang đo: ","Measuring: "},{"Đo xong.","Measured."},{"Dung lượng hệ thống: ","System Space: "},{"Đã dừng đo.","Measurement stopped."},
   {"Chọn một mục để chạy công cụ.","Select an item to run its tool."},{"Mục này hiện không có gì để dọn hoặc chưa được đo.","This item has nothing to clean right now or has not been measured."},{"Không có lệnh nào để chạy cho mục này.","There is no command to run for this item."},
   {"Lệnh sẽ chạy:","Commands to run:"},{"Kết quả KHÔNG đi qua Kho khôi phục. Tiếp tục?","Results do NOT go through the Recovery Vault. Continue?"},{"Chạy công cụ Windows","Run Windows tool"},
   {"Đang chạy: ","Running: "},{"Dung lượng hệ thống: chạy {0} lệnh cho «{1}».","System Space: running {0} commands for \"{1}\"."},
   {"mã {0}, {1}s","code {0}, {1}s"},{"Đã chạy {0} lệnh: {1} thành công, {2} bị từ chối hoặc lỗi.","Ran {0} commands: {1} succeeded, {2} refused or failed."},{"Kết quả công cụ Windows","Windows tool result"},
   {"Chọn một mục để xem chi tiết.","Select an item to view details."},{"Vị trí: ","Location: "},{"Dung lượng: ","Size: "},{"Công cụ: ","Tool: "},
   {"Hibernate đang tắt.","Hibernate is off."},{"Không có Windows.old.","No Windows.old."},{"Thư mục không tồn tại.","Folder does not exist."},{"Không đủ quyền đọc.","Not enough permission to read."},{"3 bản cũ trong 112 gói.","3 older versions among 112 packages."}
  };
 }
}
