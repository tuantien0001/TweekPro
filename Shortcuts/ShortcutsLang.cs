using System.Collections.Generic;

namespace TweekPro.Shortcuts {
 public static class ShortcutsLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>{
   {"Kết quả","Result"},
   {"Shortcut hỏng…","Broken shortcuts…"},
   {"Shortcut hỏng","Broken shortcuts"},
   {"Tên shortcut","Shortcut name"},
   {"Vị trí shortcut","Shortcut location"},
   {"Quét shortcut","Scan shortcuts"},
   {"Chuyển shortcut vào Kho","Move shortcuts to Vault"},
   {"Dừng quét / dọn","Stop scan / cleanup"},
   {"Chỉ tìm shortcut .lnk có đích bị mất trên ổ đĩa cố định trong Desktop, Start Menu và Startup. Bỏ qua liên kết mạng, ổ rời, shortcut cài theo yêu cầu và mục không xác định được đích. Chỉ chuyển shortcut vào Kho khôi phục; không xóa tệp đích.","Finds .lnk shortcuts in Desktop, Start Menu and Startup with missing targets on fixed drives. Skips network/removable drives, install-on-demand shortcuts and unknown targets. Moves only shortcuts to the Recovery Vault; target files are never deleted."},
   {"Bấm Quét shortcut để xem trước. Chưa có mục nào được chọn để dọn.","Click Scan shortcuts to preview. No items are selected for cleanup."},
   {"Đã dừng. Những shortcut đã chuyển vẫn còn trong Kho khôi phục.","Stopped. Shortcuts already moved remain in the Recovery Vault."},
   {"Đang kiểm tra shortcut…","Checking shortcuts…"},
   {"Đích bị mất","Target missing"},
   {"Đã kiểm tra {0} shortcut • Tìm thấy {1} shortcut hỏng • Bỏ qua {2} mục không đọc được/liên kết.","Checked {0} shortcuts • Found {1} broken shortcuts • Skipped {2} unreadable entries/links."},
   {"Đã chạm giới hạn quét; kết quả chưa đầy đủ.","Scan limit reached; results are incomplete."},
   {"Đánh dấu shortcut muốn chuyển vào Kho.","Select shortcuts to move to the Vault."},
   {"Chuyển {0} shortcut vào Kho khôi phục?\r\n\r\n{1}\r\n\r\nKhôi phục trong tab Kho khôi phục. Không xóa tệp đích.","Move {0} shortcuts to the Recovery Vault?\r\n\r\n{1}\r\n\r\nRestore from the Recovery Vault tab. Target files are not deleted."},
   {"Đã chuyển {0} shortcut vào Kho; {1} mục được giữ lại do thay đổi/lỗi.","Moved {0} shortcuts to the Vault; kept {1} changed/failed items."},
   {"Shortcut đã thay đổi hoặc đích không còn được xác nhận là mất. Hãy quét lại.","The shortcut changed or its target is no longer confirmed missing. Scan again."},
   {"Shortcut và Kho khôi phục phải cùng ổ đĩa; mục này được giữ nguyên.","The shortcut and Recovery Vault must be on the same drive; this item was kept."}
  };
 }
}
