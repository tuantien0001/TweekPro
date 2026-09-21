using System;
using System.Collections.Generic;

namespace TweekPro.Pup {
 /// <summary>English strings for the PUP/bloatware detector; registered with Core.L beside the main table (keys must not repeat there).</summary>
 public static class PupLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   // Labels
   {"Thanh công cụ","Toolbar"},{"Quảng cáo / dọa lỗi","Adware / scareware"},{"Tối ưu tiếp thị","Marketed optimizer"},{"Bảo mật cài sẵn","Bundled security"},
   {"Đi kèm / runtime cũ","Bundled / legacy runtime"},{"Cài sẵn theo máy","OEM preload"},{"Store cài sẵn","Store preload"},
   {"Nên gỡ","Remove"},{"Cân nhắc gỡ","Consider removing"},{"Không cần thiết","Not needed"},
   {"Không phát hiện phần mềm không mong muốn.","No unwanted software detected."},
   {"{0} mục nghi không mong muốn: {1} nên gỡ, {2} cân nhắc, {3} không cần thiết.","{0} suspected unwanted items: {1} to remove, {2} to consider, {3} not needed."},
   {"Cảnh báo","Flag"},{"Chỉ hiện mục cảnh báo","Flagged only"},{"{0} mục nghi không mong muốn","{0} suspected unwanted"},
   {"Đánh giá: ","Verdict: "},{"Ứng dụng không mong muốn","Unwanted software"},
   {"Không có phần mềm không mong muốn","No unwanted software"},{"Có phần mềm không mong muốn","Unwanted software present"},{"Có phần mềm nên gỡ","Software that should be removed"},
   {"Không mục nào khớp quy tắc PUP/bloatware.","Nothing matched the PUP/bloatware rules."},
   {"{0} mục ({1} nên gỡ). Xem cột Cảnh báo ở tab Ứng dụng / Ứng dụng Windows.","{0} items ({1} to remove). See the Flag column in Applications / Windows Apps."},
   {"Đang phân loại phần mềm không mong muốn…","Classifying unwanted software…"},
   {"Phát hiện PUP/bloatware: {0}","PUP/bloatware detection: {0}"},
   {"Kiểm tra sức khỏe chỉ đọc: đo tệp rác theo quy tắc, mục còn sót chờ duyệt, thư mục rỗng trong Downloads, bản sao lưu cũ hơn tuổi dọn kho, số mục khởi động, dung lượng trống ổ hệ thống và phần mềm không mong muốn (PUP/bloatware). Không xóa gì; bấm đúp một dòng để mở tab xử lý tương ứng.","Read-only health check: rule-matched junk, pending leftovers, empty folders in Downloads, backups older than the purge age, startup entries, free space on the system drive and unwanted software (PUP/bloatware). Nothing is deleted; double-click a row to open the tab that handles it."},
   {"Cột Cảnh báo đánh dấu phần mềm khớp quy tắc PUP/bloatware (thanh công cụ, quảng cáo, tối ưu tiếp thị, bảo mật cài sẵn, đi kèm, cài sẵn theo máy). Chỉ là gợi ý chỉ đọc: Tweek Pro không tự gỡ; bạn gỡ bằng nút Gỡ mục đã chọn như mọi ứng dụng khác.","The Flag column marks software matching the PUP/bloatware rules (toolbars, adware, marketed optimizers, bundled security, bundled components, OEM preloads). Read-only hints only: Tweek Pro never removes them by itself; uninstall with Uninstall selected like any other application."},
   {"Không có ứng dụng nào bị đánh dấu không mong muốn.\r\nBỏ chọn «Chỉ hiện mục cảnh báo» để xem toàn bộ.","No application is flagged as unwanted.\r\nUntick “Flagged only” to see everything."},
   // Rule reasons (keys must match pup-rules.json byte for byte)
   {"Thanh công cụ trình duyệt thường đi kèm bộ cài khác, đổi trang chủ/máy tìm kiếm và theo dõi lượt tìm.","Browser toolbars usually ride along with other installers, hijack the home page/search engine and track searches."},
   {"Thành phần chiếm quyền tìm kiếm/trang chủ đã bị các hãng bảo mật xếp vào nhóm không mong muốn.","Search/home-page hijacker that security vendors classify as unwanted software."},
   {"Phần mềm quảng cáo/chèn nội dung hoặc chương trình dọa lỗi giả; nên gỡ và quét lại bằng phần mềm bảo mật.","Adware/content injector or fake-error scareware; remove it and rescan with your security software."},
   {"Trình \"tăng tốc\"/cập nhật driver có tiếp thị mạnh: quét dọa lỗi, nhắc mua bản trả phí, hay chạy nền và đổi cài đặt. Windows Update đã lo driver.","Aggressively marketed \"speed-up\"/driver-updater tool: scare scans, upsell prompts, background services and settings changes. Windows Update already handles drivers."},
   {"Bảo mật cài sẵn/thử nghiệm hoặc thành phần đi kèm bộ cài khác. Nếu không chủ động mua, Windows Security đã đủ; chạy hai trình diệt virus làm máy chậm.","Preloaded/trial security or a component bundled with another installer. Unless you bought it on purpose, Windows Security is enough; two antivirus engines slow the PC down."},
   {"Bộ bảo mật thường được cài sẵn dạng thử nghiệm trên máy mới. Nếu bạn không mua gói này, hết hạn sẽ báo liên tục; Windows Security là đủ.","Security suite usually preloaded as a trial on new PCs. If you did not buy it, it nags after expiry; Windows Security is enough."},
   {"Thành phần đi kèm bộ cài khác hoặc runtime cũ không còn được hỗ trợ (Flash, Silverlight, Java cũ, QuickTime); chỉ chạy nền và tốn tài nguyên hoặc là lỗ hổng bảo mật.","Component bundled with another installer or an unsupported legacy runtime (Flash, Silverlight, old Java, QuickTime); it only runs in the background, wastes resources or is a security hole."},
   {"Ứng dụng nhà sản xuất máy hoặc đối tác cài sẵn (quảng cáo dịch vụ, đăng ký, game thử nghiệm). Thường không cần; gỡ được bằng tab Ứng dụng.","OEM or partner preload (service promos, registration, trial games). Rarely needed; removable from the Applications tab."},
   {"Trình đào tiền ảo hoặc điều khiển từ xa hay bị lợi dụng cài lén; nếu bạn không tự cài, hãy gỡ và quét toàn máy.","Crypto miner or remote-control tool often installed silently; if you did not install it yourself, remove it and run a full scan."},
   {"Gói Store cài sẵn theo hợp đồng quảng cáo (game, mạng xã hội, dịch vụ giải trí). Không phải thành phần Windows; gỡ được bằng tab Ứng dụng Windows nếu không dùng.","Store package preloaded under a promotion deal (games, social, streaming). Not a Windows component; removable from the Windows Apps tab if unused."},
   {"Ứng dụng Windows cài sẵn dạng tùy chọn (Xbox, Bing, Solitaire, Teams cá nhân…). Không ảnh hưởng hệ thống khi gỡ và có thể cài lại từ Store; chỉ gỡ khi chắc không dùng.","Optional inbox Windows app (Xbox, Bing, Solitaire, personal Teams…). Safe to remove and reinstallable from the Store; remove only if you are sure you do not use it."}
  };
 }
}
