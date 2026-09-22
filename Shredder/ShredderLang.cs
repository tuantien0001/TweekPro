using System;
using System.Collections.Generic;

namespace TweekPro.Shredder {
 /// <summary>English strings for the shredder (Explorer file pane); merged by Core.L, keys must not repeat elsewhere.</summary>
 public static class ShredderLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Xóa không phục hồi…","Shred…"},{"Xóa không phục hồi","Shred (unrecoverable delete)"},{"Xóa vĩnh viễn","Shred now"},
   {"Thư mục: {0} tệp trong {1} thư mục, {2}.","Folder: {0} files in {1} folders, {2}."},{"Tệp: {0}.","File: {0}."},{"{0} liên kết (symlink/junction) sẽ được bỏ qua.","{0} links (symlink/junction) will be skipped."},
   {"… và {0} tệp khác","… and {0} more files"},
   {"1 lần ghi ngẫu nhiên (khuyến nghị, đủ cho ổ hiện đại)","1 random pass (recommended, enough for modern drives)"},{"3 lần (0x00, 0xFF, ngẫu nhiên — chậm gấp ba)","3 passes (0x00, 0xFF, random — three times slower)"},
   {"Tôi hiểu: dữ liệu bị ghi đè, KHÔNG vào Kho khôi phục và KHÔNG thể lấy lại bằng bất kỳ công cụ nào.","I understand: the data is overwritten, does NOT go to the Recovery Vault and CANNOT be recovered by any tool."},
   {"Trên SSD/NVMe, bộ điều khiển có thể ghi vào ô nhớ khác nên ghi đè không đảm bảo 100%; hãy bật BitLocker hoặc TRIM để bảo vệ trọn vẹn. Trên HDD, ghi đè một lần đã đủ với phần cứng hiện đại.","On SSD/NVMe the controller may write to different cells, so overwriting is not a 100% guarantee; enable BitLocker or TRIM for full protection. On HDDs a single pass is enough for modern hardware."},
   {"Ổ tháo lắp (USB/thẻ nhớ): bộ điều khiển flash có thể giữ bản sao cũ; ghi đè giảm rủi ro nhưng không tuyệt đối.","Removable drive (USB/memory card): the flash controller may keep stale copies; overwriting lowers the risk but is not absolute."},
   {"Ổ mạng: máy chủ quyết định dữ liệu có thật sự bị ghi đè hay không.","Network drive: the server decides whether the data is really overwritten."},
   {"Chọn một tệp hoặc thư mục trong danh sách bên trái trước.","Select a file or folder in the list on the left first."},{"Không xóa gốc ổ đĩa.","Drive roots cannot be shredded."},
   {"Đang liệt kê nội dung sẽ xóa…","Listing what will be shredded…"},{"Không có gì để xóa.","Nothing to shred."},{"Đã hủy.","Cancelled."},
   {"Xóa không phục hồi: {0} ({1} tệp, {2}, {3} lần ghi)…","Shredding: {0} ({1} files, {2}, {3} passes)…"},{"Đang ghi đè: ","Overwriting: "},
   {"Tệp đã bị xóa không phục hồi.","The file has been shredded."},
   {"Đã ghi đè và xóa {0} tệp ({1}) + {2} thư mục trong {3:0.0} giây.","Overwrote and deleted {0} files ({1}) + {2} folders in {3:0.0} s."},{"{0} mục thất bại (xem nhật ký).","{0} items failed (see the log)."},{"Không xóa được: ","Could not shred: "},
  };
 }
}
