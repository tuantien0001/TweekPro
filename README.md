# Tweek Pro – Trình quản lý Windows (bản 0.7)

Ứng dụng Windows 64-bit, mã nguồn mở, kế thừa AppCare 0.6: gỡ ứng dụng theo hàng đợi, quét và dọn phần còn sót, **dọn tệp rác theo quy tắc**, **theo dõi mạng theo tiến trình (chỉ xem)** và kho khôi phục có **xóa vĩnh viễn**. Nguyên tắc không đổi: mọi thao tác phá hủy đều được xác nhận và sao lưu (trừ khi bạn chủ động chọn xóa thẳng), thư mục hệ thống/dùng chung bị chặn, service/driver/tác vụ lịch chỉ xem, không kernel driver, không telemetry.

Tên gọi cố ý viết "Tweek Pro". Mã nguồn dùng namespace `TweekPro.*`.

## Mở ứng dụng

Chạy `TweekPro-0.7.exe` trong thư mục phân phối (cạnh các DLL `Microsoft.Diagnostics.Tracing.TraceEvent`…; phải copy cả thư mục). Chỉ cần .NET Framework 4.8 có sẵn trong Windows 10/11.
**Luôn chạy với quyền quản trị** (`app.manifest` `requireAdministrator`, UAC hỏi một lần khi mở). Quyền này cần cho nhóm **Rác hệ thống** (`C:\Windows\Temp`, cache Windows Update, nhật ký CBS, dump…), khóa HKLM, băng thông mạng (ETW), gỡ ứng dụng Windows cho mọi tài khoản và **chiếm quyền sở hữu tệp cứng đầu**. Có quyền cao không đồng nghĩa xóa được mọi thứ: `System32`, `SysWOW64`, `WinSxS`, `Program Files` và các thư mục lõi khác bị khóa cứng trong mã, không quy tắc nào mở được.

**Tệp cứng đầu (`Core/StubbornFiles.cs`)**: khi dọn phần còn sót mà Windows từ chối, Tweek Pro leo thang theo bậc: bỏ thuộc tính chỉ đọc/ẩn/hệ thống → chiếm quyền sở hữu (Administrators) và cấp toàn quyền → thử lại. Tệp đang bị tiến trình khác giữ không thử lại (khóa không biến mất khi đổi ACL); tab Phần còn sót hỏi một lần để **hẹn xóa khi khởi động lại** (`MoveFileEx` delay-until-reboot, tệp trước rồi thư mục sâu nhất trước); mục hẹn xóa hiện trong Kho khôi phục với trạng thái riêng và không có bản sao lưu.

**Ngôn ngữ**: nút icon quả cầu **EN / VI** ở góc phải header (tooltip nêu ngôn ngữ sẽ chuyển) đổi ngôn ngữ giao diện (lưu vào `settings.json`, khóa `language`: `vi` hoặc `en`) và khởi động lại ứng dụng. Chuỗi nguồn là tiếng Việt; bản dịch nằm trong `Core/LangEn.cs` (khóa = chuỗi tiếng Việt), chuỗi thiếu bản dịch hiện tiếng Việt thay vì trống. Đã dịch: header, 13 tab, mọi nút/cột/ghi chú tạo qua `Theme`, toàn bộ tab Dọn rác (kể cả tên/mô tả quy tắc và hộp thoại), toàn bộ Tổng quan (điểm, đánh giá, báo cáo). Thông báo nhật ký và một số hộp thoại ở các tab khác còn tiếng Việt.

## Các tab

| Tab | Chức năng | Ghi chú an toàn |
|---|---|---|
| **Tổng quan** (mới) | Kiểm tra sức khỏe máy một nút: điểm 0–100 với vòng đo màu và hạng A–E, danh sách khu vực cần chú ý (tệp rác, phần còn sót, thư mục rỗng, kho khôi phục, mục khởi động, dung lượng trống, phần mềm không mong muốn) kèm dung lượng có thể giải phóng; bấm đúp để mở tab xử lý; sao chép báo cáo | Hoàn toàn chỉ đọc; chỉ tính là "giải phóng được" phần Tweek Pro có thể dọn an toàn (tệp rác theo quy tắc, bản sao lưu cũ trong Kho); có nút Dừng kiểm tra |
| Ứng dụng | Danh sách ứng dụng desktop (Uninstall HKLM/HKCU), gỡ theo hàng đợi, quét mục đang xem, CSV; **cột Cảnh báo** (mới) đánh dấu PUP/bloatware theo quy tắc nhúng (thanh công cụ, quảng cáo/dọa lỗi, tối ưu tiếp thị, bảo mật cài sẵn, đi kèm/runtime cũ, cài sẵn theo máy) với lý do bằng lời thường; lọc «Chỉ hiện mục cảnh báo» | Không dùng Win32_Product; chặn lệnh gỡ qua cmd/PowerShell/script. Cảnh báo chỉ đọc, không tự gỡ; nhà phát hành cốt lõi (Microsoft, Google, Mozilla, NVIDIA, Intel, AMD…) và tên sản phẩm tin cậy không bao giờ bị đánh dấu dù quy tắc lỏng |
| **Ứng dụng Windows** (mới) | Ứng dụng cài sẵn và Microsoft Store (gói Appx/MSIX) qua `Get-AppxPackage`, có logo lấy từ gói; nhóm Gỡ được / Cần cân nhắc / Được bảo vệ; gỡ bằng `Remove-AppxPackage` (tùy chọn mọi tài khoản, bỏ provisioning); mở trang Store, mở thư mục gói | Framework, gói `NonRemovable`, Start/Search/Settings/Windows Security/Edge và mọi gói `Microsoft.Windows.*`, `MicrosoftWindows.*`, `Windows.*` (trừ danh sách trắng Photos, Widgets, Cortana, Dev Home) bị khóa trong mã: không tích được, không tạo được script. Store/App Installer/Photos… hỏi xác nhận lần hai. Bản ghi `Kind=Store` trong Kho; khôi phục = đăng ký lại từ thư mục gói hoặc mở trang Store |
| Phần còn sót | Kết quả quét nhanh/sâu chờ duyệt; Chọn/Bỏ chọn/Xem/Xóa (có sao lưu) | Từ chối dọn khi ứng dụng còn đăng ký; mục Chỉ xem không thể tích |
| **Dọn rác** (mới) | Xem trước theo quy tắc với số tệp và dung lượng; chọn từng nhóm; hai chế độ: vào Kho (mặc định) hoặc **Xóa thẳng** (nhãn đỏ, xác nhận hai lần) | Bỏ qua tệp mới hơn 24 giờ (cache trình duyệt: 0 giờ), tệp đang mở, liên kết; nhóm cache trình duyệt bị khóa khi trình duyệt còn chạy; không bao giờ chạm Documents/Desktop/Downloads/OneDrive |
| **Thư mục rỗng** (mới) | Chọn một thư mục, quét chỉ đọc để tìm các nhánh thư mục hoàn toàn rỗng; xóa nhánh đã chọn (thư mục rỗng không có dữ liệu) | Chỉ liệt kê thư mục không chứa tệp ở mọi cấp; bỏ qua liên kết; không đụng thư mục hệ thống/Program Files/dữ liệu Tweek Pro; kiểm tra lại rỗng ngay trước khi xóa |
| **Tệp trùng lặp** (mới) | Chọn một thư mục, quét chỉ đọc để tìm tệp giống hệt nhau (gom nhóm theo kích thước rồi SHA-256); chọn giữ bản cũ nhất/mới nhất; hai chế độ: vào Kho (mặc định) hoặc **Xóa thẳng** (xác nhận hai lần) | Bản giữ lại không bao giờ bị xóa; bỏ qua liên kết và tệp đang mở; không quét thư mục hệ thống, Program Files hay dữ liệu Tweek Pro; bản dư chuyển vào Kho có thể khôi phục |
| **Tệp tải về cũ** (mới) | Quét Downloads (và Desktop nếu chọn) tìm bộ cài (.exe/.msi/.msix/.apk…), tệp nén, ảnh đĩa, tệp tải dở (.crdownload/.part/.tmp) và tệp lớn (ngưỡng MB tùy chọn) cũ hơn N ngày (mặc định 30); bộ cài và tệp tải dở được tích sẵn; hai chế độ: vào Kho (mặc định, `Kind=Stale`) hoặc **Xóa thẳng** (xác nhận hai lần); mở vị trí | Chỉ quét Downloads/Desktop của tài khoản hiện tại (`StaleSafety` trong mã), không đụng Documents/Ảnh/Nhạc/Video, OneDrive/Dropbox/Google Drive, thư mục hệ thống, dữ liệu Tweek Pro; bỏ qua shortcut, tệp ẩn/hệ thống, liên kết, tệp đang mở; kiểm tra tệp chưa đổi kể từ khi quét; khôi phục không ghi đè đích đã tồn tại |
| **Phân tích ổ đĩa** (mới) | Chọn một thư mục, quét chỉ đọc để xem thư mục con nặng nhất, dung lượng theo phần mở rộng và các tệp lớn nhất; mở vị trí, xuất CSV | Chỉ đọc, không thay đổi gì trên đĩa; bỏ qua liên kết (junction/symlink); giới hạn 1.000.000 tệp / 120 giây |
| Kho khôi phục | Khôi phục mục đang chọn; **Xóa vĩnh viễn mục đã đánh dấu**; **Dọn kho theo tuổi…** (xem trước số mục và dung lượng) | Xóa vĩnh viễn không hoàn tác được; mặc định chỉ xóa bản đã khôi phục; kho AppCare cũ vẫn hiển thị và khôi phục được |
| Khởi động (Autorun) | Run/RunOnce/shortcut Startup: tắt có sao lưu, bật lại, CSV | Không dừng tiến trình đang chạy |
| **Mạng** (mới) | Kết nối TCP/UDP theo tiến trình (tên, PID, nhà phát hành chữ ký, endpoint, trạng thái), làm mới 1–30 giây, tạm dừng, tìm kiếm, ẩn loopback, phân giải tên máy (tắt mặc định), CSV; khi có quyền quản trị: **Bật băng thông (ETW)** hiển thị byte gửi/nhận và tốc độ theo tiến trình | Chỉ xem, không chặn, không driver; phiên ETW tên `TweekPro-Network` luôn được dừng khi đóng |
| **Dịch vụ hệ thống** (mới) | Mọi dịch vụ Windows (WMI) với **giải thích bằng lời thường** từ cơ sở kiến thức ~180 dịch vụ + mẫu tên (trình cập nhật, nâng quyền, anti-cheat, máy chủ CSDL…), huy hiệu màu theo mức an toàn (đỏ = cốt lõi, xanh dương = Windows, vàng = tùy chọn, xanh lá = bên thứ ba), PID/RAM, nhà phát hành theo chữ ký; lọc Đang chạy / Bên thứ ba / Có thể tắt; chuột phải: Dừng, Khởi động, Khởi động lại, **Dừng và vô hiệu hóa (lưu Kho)**, Kết thúc tiến trình, mở thư mục, chi tiết | Dịch vụ cốt lõi bị khóa mọi thao tác dừng; svchost không cho kết thúc tiến trình; vô hiệu hóa lưu kiểu khởi động cũ vào Kho (`Kind=Service`) |
| Công cụ (Windows Tools) | Danh sách lối mở công cụ Windows kèm icon hệ thống từ .exe/.cpl/.msc, cột dòng lệnh | SFC/chkdsk hỏi xác nhận quyền quản trị |
| Nhật ký | Nhật ký phiên, nút mở thư mục nhật ký/dữ liệu, lưu cài đặt | Nhật ký xoay vòng theo ngày |
| **Trợ lý AI** (tab cuối) | Claude/OpenAI và Local AI (LM Studio/Ollama); tải danh sách model, chat, quét/dọn phần còn sót qua công cụ | Mặc định hỏi xác nhận; có Chỉ đọc và Tự động do người dùng chọn. Giới hạn bảo vệ luôn giữ nguyên; model không hỗ trợ công cụ chỉ trả lời hướng dẫn |

## Dọn rác: quy tắc và giới hạn

Quy tắc là dữ liệu JSON đóng gói trong exe (`Cleaner/junk-rules.json`). Đặt một bản sửa đổi tại `%LOCALAPPDATA%\TweekPro\junk-rules.json` để ghi đè (nút **Nạp lại quy tắc**). Quy tắc mặc định: `%TEMP%`, `C:\Windows\Temp` (cần quản trị), `CrashDumps` (*.dmp), WER ReportQueue/ReportArchive (tài khoản và toàn máy), thumbcache/iconcache của Explorer, INetCache, cache shader GPU (D3DSCache, NVIDIA DXCache/GLCache, AMD DxCache), cache của Chrome/Edge/Brave (Cache, Code Cache, GPUCache, ShaderCache) và Firefox (cache2, startupCache, shader-cache).

Nhóm **Rác hệ thống (cần quyền quản trị)** dọn các vị trí rác do chính Windows sở hữu và tự tạo lại: gói Windows Update đã tải (`SoftwareDistribution\Download`, chỉ tệp cũ hơn 7 ngày, khóa khi `TiWorker`/`TrustedInstaller` đang chạy), nhật ký CBS (`Logs\CBS`, cũ hơn 7 ngày), dump lỗi kernel (`Minidump`, `LiveKernelReports`), nhật ký cài đặt (`Panther`) và `Downloaded Program Files`. Đây là phạm vi tối đa mà Tweek Pro cho phép ngay cả khi chạy quản trị: **không có chế độ nào xóa được tệp trong `System32`, `SysWOW64`, `WinSxS`, `servicing`, `Boot`, `Fonts`, `Installer` hay Program Files** — các thư mục này nằm trong `JunkSafety.WindowsCoreFolders` và bị từ chối trước khi bất kỳ tệp nào được đếm, kể cả khi người dùng tự viết quy tắc JSON trỏ vào đó. Mọi tệp trong System32 đều là thành phần Windows, không phải rác của ứng dụng nào; xóa ở đó là cách chắc chắn nhất để máy không khởi động được, nên đây là giới hạn thiết kế, không phải thiếu tính năng.

Hàng rào nằm trong mã, không trong JSON: một quy tắc chỉ được trỏ vào các vùng rác cho phép (Temp, CrashDumps, WER, Explorer cache, INetCache, cache GPU) hoặc vào thư mục hồ sơ trình duyệt **với điều kiện** đường dẫn có một thư mục cache (`Cache`, `Code Cache`, `GPUCache`, `cache2`…). Đường dẫn chạm Documents, Desktop, Pictures, Music, Videos, Downloads, OneDrive, Program Files, System32/SysWOW64/WinSxS và các thư mục Windows cốt lõi khác, hay thư mục dữ liệu của Tweek Pro bị từ chối; gốc `C:\Windows` cũng bị từ chối vì chứa các thư mục đó. Mỗi quy tắc dừng ở 100.000 tệp / 30 giây và báo "chưa đủ".

Chế độ vào Kho tạo một bản sao lưu loại **Rác** cho mỗi nhóm, gồm `files.xml` ánh xạ từng tệp về đường dẫn gốc; khôi phục bỏ qua tệp đã tồn tại ở đích. Dung lượng chỉ được giải phóng khi xóa vĩnh viễn trong Kho. Thư mục con rỗng sau khi dọn được xóa (gốc quy tắc giữ nguyên).

## Tổng quan (kiểm tra sức khỏe): cơ chế

Tab **Tổng quan** là điểm đến đầu tiên cho người dùng không chuyên: một nút **Kiểm tra ngay** chạy sáu phép đo chỉ đọc trên luồng nền — xem trước tệp rác theo quy tắc (`JunkCleaner.Preview`, bỏ qua nhóm đang khóa), số mục còn sót đang chờ duyệt, thư mục rỗng trong `Downloads`, dung lượng các bản sao lưu **cũ hơn tuổi dọn kho** (mặc định 90 ngày, đúng tập mà "Dọn kho theo tuổi…" sẽ chọn), số mục khởi động đang bật và tỉ lệ trống của ổ hệ thống. Kết quả đi qua `Health/HealthCheck.Evaluate`, một hàm thuần không phụ thuộc Windows: mỗi khu vực nhận mức **Tốt / Nên xem / Cần dọn / Khẩn** theo ngưỡng cố định (ví dụ rác ≥100 MB nên xem, ≥500 MB cần dọn, ≥2 GB khẩn; ổ trống <20 % nên xem, <10 % cần dọn, <5 % khẩn), điểm bắt đầu từ 100 và trừ 5/12/25 cho mỗi khu vực theo mức, hạng A ≥90, B ≥75, C ≥60, D ≥40, còn lại E. Khu vực không đo được (thiếu quyền, lỗi đọc) hiển thị "Chưa đo" và **không bị trừ điểm**.

Thẻ điểm được vẽ bằng GDI+ (`HealthRenderer`, vòng cung 270° đổi màu xanh → cam → đỏ theo điểm, huy hiệu hạng, câu tóm tắt, dung lượng có thể giải phóng). Mỗi dòng kết quả chỉ ra tab xử lý; bấm đúp hoặc **Mở tab xử lý** để chuyển ngay. **Sao chép báo cáo** đưa bản văn bản vào clipboard. Tab này không xóa gì.

## Tệp trùng lặp: cơ chế

Tab **Tệp trùng lặp** tìm các tệp có nội dung giống hệt nhau trong một thư mục do người dùng chọn (mặc định gợi ý `Downloads`). Quét theo hai bước để nhanh và chính xác: trước tiên gom theo kích thước, chỉ những kích thước có từ hai tệp trở lên mới được băm nội dung bằng **SHA-256**; các tệp cùng mã băm là bản trùng thật sự. Mỗi nhóm chọn một **bản giữ lại** (mặc định là bản cũ nhất, có thể đổi sang mới nhất); bản giữ lại không bao giờ bị đụng tới.

Hàng rào an toàn nằm trong mã (`Dupes/DupeSafety`): không quét gốc ổ đĩa, thư mục Windows/System32, Program Files hay thư mục dữ liệu của Tweek Pro; bỏ qua liên kết (reparse point) và tệp đang mở. Giới hạn 500.000 tệp / 90 giây rồi báo "chưa đủ". Chế độ vào Kho tạo một bản sao lưu loại **Bản trùng** với `files.xml` ánh xạ từng bản dư về đường dẫn gốc; khôi phục bỏ qua tệp đã tồn tại ở đích. Chế độ **Xóa thẳng** xóa vĩnh viễn sau khi xác nhận hai lần. Ngay trước khi dọn, mỗi nhóm được kiểm tra lại: bản giữ lại phải còn nguyên (kích thước và thời gian sửa như lúc quét, nếu không cả nhóm bị bỏ qua), mỗi bản dư phải chưa thay đổi kể từ lúc quét, và chế độ xóa thẳng còn băm lại nội dung trước khi xóa. Dung lượng chỉ được giải phóng khi xóa vĩnh viễn trong Kho. Ngưỡng kích thước tối thiểu (`duplicateMinKB`) và lựa chọn giữ bản mới nhất (`duplicateKeepNewest`) được lưu trong `settings.json`.

## Phân tích ổ đĩa: cơ chế

Tab **Phân tích ổ đĩa** quét chỉ đọc một thư mục do người dùng chọn (mặc định gợi ý thư mục hồ sơ người dùng) và trả về ba nhóm kết quả: dung lượng đệ quy của từng thư mục con trực tiếp (kèm bucket cho tệp nằm ngay ở gốc), tổng dung lượng theo phần mở rộng tệp, và danh sách các tệp lớn nhất. Tất cả sắp xếp giảm dần theo dung lượng để thấy ngay thứ chiếm chỗ.

Quét bằng duyệt lặp (stack) một lần, bỏ qua liên kết (reparse point) để tránh vòng lặp, cộng dồn tổng số tệp/dung lượng, gom theo phần mở rộng và giữ danh sách top tệp lớn nhất bằng ngưỡng động (bounded top-N) để không tốn bộ nhớ trên cây thư mục lớn. Có giới hạn 1.000.000 tệp / 120 giây rồi báo "chưa đủ". Tab này **hoàn toàn chỉ đọc** — không xóa, không di chuyển, không sao lưu; hành động duy nhất là mở vị trí trong Explorer và xuất CSV, để người dùng tự quyết định.

## Thư mục rỗng: cơ chế

Tab **Thư mục rỗng** quét một thư mục do người dùng chọn và chỉ liệt kê những nhánh **hoàn toàn rỗng** (không có tệp ở bất kỳ cấp nào), gom về nhánh cao nhất để xóa một lần. Bỏ qua liên kết; từ chối gốc ổ đĩa, Windows, Program Files và dữ liệu Tweek Pro (`EmptyFolders.ForbiddenRoots`). Ngay trước khi xóa, mỗi nhánh được kiểm tra rỗng lại lần nữa; nếu đã có tệp xuất hiện thì bỏ qua và báo lại. Vì thư mục rỗng không có dữ liệu nên không cần đưa vào Kho.

## Mạng: cơ chế

- Bố cục hai tầng: **bảng Ứng dụng** ở trên (icon lấy từ tệp exe, cột Hoạt động với glyph màu và nhãn "Đang gửi / Đang nhận / Gửi và nhận / Lắng nghe / Không hoạt động", cột Gửi/s và Nhận/s vẽ **thanh tốc độ** tỉ lệ căn bậc hai so với ứng dụng bận nhất, cùng tổng byte, số kết nối, đã kết nối, lắng nghe, máy từ xa, PID, nhà phát hành; sắp theo lưu lượng hiện tại, dòng đang truyền in đậm) và **bảng Kết nối** ở dưới. Bấm một (hoặc Ctrl+bấm nhiều) ứng dụng để bảng dưới chỉ hiện kết nối của nó; "Hiện tất cả kết nối" bỏ lọc; nhấp đúp mở chi tiết ứng dụng. Lựa chọn giữ theo PID qua mỗi lần làm mới.
- **Chuột phải** trên một ứng dụng (hoặc một dòng kết nối): *Kết thúc tiến trình*; nếu tiến trình chứa dịch vụ Windows (đọc qua WMI `Win32_Service`), liệt kê từng dịch vụ với *Dừng dịch vụ* và *Dừng và vô hiệu hóa* — mục sau lưu kiểu khởi động cũ vào Kho khôi phục (`Kind=Service`) để bật lại bằng nút Khôi phục; thêm *Mở thư mục chứa tệp*, *Sao chép đường dẫn*, *Chi tiết*. Hàng rào trong mã (`ProcessControl`): không kết thúc System/smss/csrss/wininit/winlogon/services/lsass/svchost/dwm/MsMpEng… và chính Tweek Pro; không dừng các dịch vụ cốt lõi (RpcSs, DcomLaunch, Winmgmt, EventLog, Dnscache, WinDefend, TrustedInstaller…) — các mục này hiện mờ kèm lý do. Với svchost.exe chỉ cho dừng từng dịch vụ bên trong.
- Băng thông (ETW) **tự bật** khi tab mở lần đầu nếu ứng dụng chạy quyền quản trị (tùy chọn `networkStartBandwidthWhenElevated`, mặc định bật); nút Bật/Dừng băng thông vẫn có để tắt tay.
- Đầu tab có **dải luồng packet realtime** (`PacketFlowStrip`): packet bay lên ở làn "Ra ↑" (xanh dương) và bay xuống ở làn "Vào ↓" (xanh lá), mật độ tỉ lệ với tốc độ gửi/nhận hiện tại (một packet ≈ 32 KB/s), cập nhật ~20 khung/giây. Tốc độ lấy từ phiên ETW nên dải chỉ có packet khi chạy với quyền quản trị và đã **Bật băng thông (ETW)**; không có quyền quản trị dải hiển thị trạng thái nhàn rỗi. Mô hình animation `PacketAnimator` không dùng ngẫu nhiên nên tất định và có self-test; phần vẽ `PacketFlowRenderer` là GDI+ thuần.
- Mỗi dòng có **icon packet theo hướng** vẽ bằng GDI+: mũi tên lên xanh dương = gửi ra (outbound), mũi tên xuống xanh lá = nhận vào (inbound), hai mũi tên = hai chiều, vòng tròn = đang lắng nghe, chấm xám = nhàn rỗi. Hướng suy ra từ tốc độ gửi/nhận theo tiến trình khi bật ETW (ngưỡng nhiễu 16 B/s), hoặc từ trạng thái kết nối khi chưa bật. Cột "Hướng" hiển thị mũi tên tương ứng. Lớp `Network/NetworkStats` (gộp theo tiến trình, phân loại hướng) chạy được ngoài Windows và có self-test.
- Bảng kết nối: `GetExtendedTcpTable`/`GetExtendedUdpTable` (iphlpapi, `TCP_TABLE_OWNER_PID_ALL`, IPv4 và IPv6), không cần quyền. Tên/đường dẫn tiến trình qua `QueryFullProcessImageName` với quyền truy vấn hạn chế; nhà phát hành lấy từ chữ ký Authenticode của tệp exe (cache theo đường dẫn).
- Băng thông: phiên ETW thời gian thực trên nhà cung cấp TCP/IP của kernel (cùng nguồn Task Manager dùng) qua thư viện `Microsoft.Diagnostics.Tracing.TraceEvent`; sự kiện send/recv TCP/UDP v4/v6 được cộng theo PID trên luồng riêng, giao diện chỉ đọc ảnh chụp mỗi chu kỳ. Cần quyền quản trị. Số liệu tính từ lúc bật ETW, gồm cả loopback (có thể ẩn). Bộ đếm `EventsLost` hiển thị ở dòng tóm tắt khi có mất sự kiện.
- Phân giải tên máy ngược là tùy chọn, bất đồng bộ, có cache; tắt thì không gửi truy vấn DNS nào.

## Dịch vụ hệ thống: cơ chế

- Đọc một lần qua WMI `Win32_Service` (tên, tên hiển thị, mô tả, trạng thái, kiểu khởi động, PID, đường dẫn, tài khoản); RAM lấy từ `Process.WorkingSet64`; nhà phát hành từ chữ ký số của tệp exe (không áp dụng cho `svchost.exe`).
- Giải thích: `Services/service-knowledge.json` (nhúng) khớp theo tên dịch vụ (bỏ hậu tố `_xxxxx` của dịch vụ theo tài khoản như `cbdhsvc_4a2b1`); không có trong cơ sở kiến thức thì khớp mẫu tên (`update`, `elevation`, `anticheat`, `license`, `telemetry`, `helper`, `sql`, `vpn`) với tên ứng dụng suy từ tên hiển thị; còn lại: chạy từ thư mục Windows → "thành phần Windows" + mô tả gốc, ngược lại → "dịch vụ nền của <nhà phát hành>".
- Mức an toàn: `Core` (danh sách `ProcessControl.CoreServices`, luôn khóa), `Windows`, `Optional`, `ThirdParty`; mỗi mức có lời khuyên riêng trong khung chi tiết. Dừng/vô hiệu hóa dùng `ProcessControl.StopService` (dùng chung với tab Mạng); khởi động qua `ServiceController`.

## Trợ lý AI: cơ chế

- `AI/AiClient`: Anthropic Messages API (`x-api-key`, `anthropic-version 2023-06-01`, khối `tool_use`/`tool_result`) và OpenAI Chat Completions (`Authorization: Bearer`, `tools[type=function]`, thông điệp `tool`); model mặc định `claude-sonnet-4-5` / `gpt-4.1` (sửa được), `max_completion_tokens` cho họ gpt-5/o*. Transport HTTP tách rời nên toàn bộ dựng/đọc yêu cầu được kiểm thử ngoài mạng.
- `AI/AiAgent`: vòng lặp hỏi → gọi công cụ → trả kết quả → hỏi tiếp, tối đa 8 vòng; công cụ thay đổi mặc định qua `IAiHost.ConfirmAction`; chế độ Chỉ đọc chặn chúng, chế độ Tự động thực thi không hỏi lại sau khi người dùng chọn; lỗi công cụ trả về cho model dưới dạng văn bản thay vì làm hỏng lượt.
- Khóa lưu trong `settings.json` dưới dạng `dpapi:<base64>` (`ProtectedData`, phạm vi người dùng hiện tại, có entropy riêng); ngoài Windows chỉ là `plain:<base64>` và giao diện nói rõ điều đó.

## Local AI: LM Studio / Ollama

1. Mở máy chủ LM Studio hoặc Ollama và nạp/tải một model chat. Tweek Pro kết nối tới máy chủ, không tự cài model.
2. Trong **Trợ lý AI**, chọn **LM Studio (local)** hoặc **Ollama (local)**. Địa chỉ mặc định lần lượt là `http://localhost:1234/v1/chat/completions` và `http://localhost:11434/v1/chat/completions`.
3. Bấm **Tải danh sách model**, chọn model rồi **Lưu cấu hình** và **Kiểm tra kết nối**. Không cần API key khi máy chủ không yêu cầu; máy chủ có xác thực vẫn có thể dùng key.
4. Chọn quyền thao tác: **Chỉ đọc**, **Hỏi xác nhận** (mặc định), hoặc **Tự động**. Chế độ Tự động vẫn từ chối đường dẫn hệ thống, mục chưa rõ quyền sở hữu và thao tác bị engine chặn.
5. Nếu model từ chối gọi công cụ, Tweek Pro chuyển sang trả lời hướng dẫn và thông báo rõ. Cần model có hỗ trợ tool calling để quản lý ứng dụng.

Dữ liệu gửi tới địa chỉ máy chủ được chọn; chỉ kết nối localhost mới ở trên cùng máy. Có thể dùng địa chỉ LAN cho máy chủ local. Không gửi key của nhà cung cấp trước khi đổi sang nhà cung cấp khác. Mô hình local được chờ tối đa 10 phút mỗi yêu cầu. Kho chỉ khôi phục phần dọn còn sót, **không hoàn tác trình gỡ chính thức**.

Icon T + PC mẫu 2 được nhúng trong exe và dùng cho header/cửa sổ/bộ cài. `--export-icon` xuất đúng file ICO đã duyệt (11 kích thước), không vẽ lại logo cũ. File ICO gốc là `TweekPro.ico` ở thư mục gốc dự án.

## Dữ liệu Tweek Pro

Thư mục dữ liệu: `%LOCALAPPDATA%\TweekPro` (kho `Backups`, `sessions.xml`, `settings.json`, `Logs\tweekpro-yyyyMMdd.log` giữ 14 ngày, `junk-rules.json` tùy chọn).
Lần chạy đầu, nếu chưa có thư mục này mà có `%LOCALAPPDATA%\AppCare`, toàn bộ thư mục cũ được **đổi tên** sang `TweekPro` (không sao chép, không gộp). Nếu không đổi tên được, kho cũ vẫn được đọc tại chỗ và hiển thị với cột Kho = "AppCare (cũ)"; khôi phục và xóa vĩnh viễn hoạt động trên cả hai kho. Bản sao lưu mới luôn ghi vào kho TweekPro.
Dữ liệu chỉ lưu cục bộ, không gửi đi đâu. Đây không phải bản sao lưu chống hỏng ổ đĩa.

## Giới hạn của bản 0.7

Chưa có theo dõi cài đặt, forced uninstall, gỡ ứng dụng Store, quản lý tiện ích trình duyệt, chặn mạng, trợ lý AI, bộ cài, chữ ký số hay tự vá exe đang chạy (xem lộ trình 1.0/2.0). Nút **Kiểm tra cập nhật** (Tổng quan / Công cụ) chỉ hỏi GitHub Releases và mở trang tải — vẫn cần tải bản mới và khởi động lại. Không xác định được mọi dấu vết của mọi ứng dụng. Băng thông theo tiến trình cần quyền quản trị và chỉ tính từ khi bật. Bản phân phối là một thư mục (exe + DLL), chưa gộp thành một tệp. Chuỗi giao diện vẫn nằm trong mã (chưa `.resx`).

## Kiểm chứng đã thực hiện

- `dotnet build -c Release -p:EnableWindowsTargeting=true` trên Linux: 0 lỗi, 0 cảnh báo.
- `TweekPro-0.7.exe --self-test junk` chạy bằng Mono trên Linux: PASS (xem `test-results.txt`) — phân tích quy tắc JSON, mở rộng đường dẫn, glob, hàng rào an toàn, chọn bản cần xóa theo tuổi, di trú thư mục dữ liệu, `settings.json`, xoay vòng nhật ký, xem trước/dọn/khôi phục rác qua kho, xóa thẳng, bỏ qua tệp đang mở, khóa quy tắc khi tiến trình chạy, xóa vĩnh viễn, liệt kê kho cũ, **tìm tệp trùng lặp** (chọn bản giữ lại, gom theo kích thước + SHA-256, bỏ qua thư mục được bảo vệ, chuyển vào kho/khôi phục/xóa thẳng), **phân tích ổ đĩa** (tổng dung lượng, theo thư mục con và phần mở rộng, thứ tự tệp lớn nhất, bounded top-N), **mạng** (gộp theo tiến trình, phân loại hướng, mô hình animation packet), **thư mục rỗng** (phát hiện nhánh rỗng cao nhất, từ chối thư mục bảo vệ, xóa), **kiểm tra sức khỏe** (ngưỡng từng khu vực, cộng dồn và kẹp điểm, ranh giới hạng, khu vực chưa đo không trừ điểm, tổng dung lượng giải phóng). Chạy headless không cần màn hình.
- **Chưa chạy trên Windows** trong lần phát hành này: giao diện WinForms, `--self-test` đầy đủ (Registry, deep scan, autorun), bảng kết nối iphlpapi, phiên ETW, khởi động lại với quyền quản trị. Xem `TEST-PLAN.md` trước khi dùng thật; chỉ thử tính năng phá hủy trong máy ảo.

## Bàn giao cho agent khác / cập nhật máy Windows

- `get-latest.ps1`: kéo nhánh mới nhất về `C:\Users\ADMIN\TweekPro` (hoặc clone nếu thư mục trống), build, chạy `--self-test` (khi PowerShell chạy quản trị) và mở app với `-Run`.
- `HANDOFF.md`: hiện trạng, quy tắc sản phẩm bất biến, checklist kiểm chứng trên Windows thật, lộ trình tính năng tiếp theo.
- `AGENTS.md`: lệnh build/test và quy ước mã cho agent (Codex, Cursor…) đọc tự động.

## Cài đặt và phân phối

Có hai dạng phát hành, đều do `publish.ps1` tạo ra trong thư mục `dist\`:

- `TweekPro-0.7-Setup.exe` — trình cài đặt Windows chuẩn (Next → Install → Finish): cài vào `Program Files\Tweek Pro`, tạo lối tắt Start Menu và (tùy chọn) Desktop, hiện trong *Ứng dụng đã cài* để gỡ. Trình cài kiểm tra .NET Framework 4.8 (có sẵn trên Windows 10 1903+/11; máy cũ hơn được đưa tới trang tải của Microsoft). Gỡ cài đặt **không** xóa dữ liệu người dùng và Kho khôi phục trong `%LOCALAPPDATA%\TweekPro`.
- `TweekPro-0.7-portable.zip` — giải nén và chạy, không cần cài.

Tạo bản phát hành trên máy Windows: `powershell -ExecutionPolicy Bypass -File .\publish.ps1` (script tự xin quyền quản trị qua UAC vì self-test cần quyền này; `-SkipTests` để chạy không cần quyền) (build → self-test → icon → zip → installer; dạng `-ExecutionPolicy Bypass` tránh lỗi "running scripts is disabled on this system" của chính sách mặc định). Trình cài dùng [Inno Setup 6.3+](https://jrsoftware.org/isinfo.php); nếu chưa có, thêm `-InstallInno` (cài qua winget) hoặc `winget install JRSoftware.InnoSetup`. Kịch bản cài đặt nằm ở `installer\TweekPro.iss`; các tham số `-SkipTests`, `-NoInstaller` để rút gọn.

Không cần máy Windows: GitHub Actions (`.github/workflows/release.yml`) tự build trên `windows-latest` ở mỗi lần push — vào tab **Actions** → lần chạy mới nhất → artifact `TweekPro-dist` để tải cả hai tệp. Gắn tag `v*` sẽ tạo thêm **Release** trên GitHub với installer và zip đính kèm để chia sẻ bằng đường dẫn cố định. Ứng dụng chỉ báo «có bản mới» khi có **Release** (hoặc ít nhất tag `v*`); push nhánh không đủ để cập nhật trong app.

Phát hành từ terminal bằng một lệnh (PowerShell **Run as administrator** vì có self-test): `powershell -ExecutionPolicy Bypass -File .\release.ps1 -Version 0.7.1 -Notes "Mô tả ngắn"`. Script kiểm tra cây làm việc sạch và không tụt sau remote, ghi phiên bản mới vào `TweekPro.csproj`, `App.cs`, `build.ps1`, `installer\TweekPro.iss` (giữ nguyên UTF-8/BOM), chạy `build.ps1 -SelfTest`, rồi commit «Release v0.7.1», tạo tag `v0.7.1` và push cả nhánh lẫn tag; nếu build/test lỗi thì hoàn nguyên và không commit gì. GitHub Actions nhận tag, build lại trên `windows-latest`, đính kèm `TweekPro-0.7.1-Setup.exe` + zip và xuất bản Release — vài phút sau nút **Kiểm tra cập nhật** trong app báo bản mới. `-DryRun` chỉ kiểm tra và in các tệp sẽ đổi; `-SkipTests` bỏ self-test cục bộ (Actions vẫn chạy).

Icon exe/installer (`TweekPro.ico`, 16–256 px) được kết xuất từ chính logo vẽ bằng mã: `TweekPro-0.7.exe --export-icon TweekPro.ico`.

## Mã nguồn và biên dịch

| Tệp/thư mục | Nội dung |
|---|---|
| `Engine.cs` | Lõi: inventory, quét, hàng rào, kho (quarantine/restore/**purge**), đọc cả kho cũ |
| `App.cs` | Cửa sổ chính, header (huy hiệu quyền, khởi động lại quản trị), tab Kho, `Program.Main`, `Tests` |
| `ScanWindow.cs`, `Presentation.cs`, `Advanced.cs`, `AdvancedUI.cs`, `AdvancedTests.cs` | Cửa sổ quét, Theme, quét sâu, Autorun, Windows Tools (kế thừa 0.6) |
| `Core/` | `Paths` (thư mục dữ liệu + di trú), `Settings` (JSON), `Log` (xoay vòng), `Elevation` |
| `Cleaner/` | `JunkRules` (JSON, mở rộng đường dẫn, `JunkSafety`), `JunkCleaner` (xem trước/dọn/khôi phục), `JunkUI`, `junk-rules.json`; `EmptyFolders` (tìm/xóa thư mục rỗng, `EmptyFolderTests`), `EmptyFolderUI` |
| `Dupes/` | `DuplicateFinder` (quét theo kích thước + SHA-256, chọn bản giữ lại, chuyển vào kho/xóa/khôi phục), `DupeSafety`, `DuplicateUI`, `DupeTests` |
| `Analyzer/` | `DiskAnalyzer` (quét chỉ đọc: tổng dung lượng, thư mục con, theo phần mở rộng, tệp lớn nhất), `DiskAnalyzerUI`, `AnalyzerTests` |
| `Network/` | `ConnectionTable` (iphlpapi), `ProcessResolver`, `EtwNetworkSession` (TraceEvent), `NetworkStats` (gộp theo tiến trình + phân loại hướng), `NetworkGlyphs` (icon packet ra/vào), `PacketAnimator`/`PacketFlow` (animation luồng packet realtime), `NetworkUI`, `NetworkStatsTests`, `PacketAnimatorTests` |
| `Health/` | `HealthCheck` (ngưỡng, điểm, hạng — thuần logic), `HealthGauge` (`HealthRenderer` + panel vẽ thẻ điểm), `HealthUI` (tab Tổng quan, các phép đo), `HealthTests` |
| `Branding.cs`, `TweekPro.ico` | Logo ứng dụng và icon tab vẽ bằng mã (GDI+); `.ico` xuất từ logo, nhúng vào exe |
| `publish.ps1`, `installer/TweekPro.iss`, `.github/workflows/release.yml` | Đóng gói: zip portable + trình cài Inno Setup, build tự động trên GitHub Actions |
| `release.ps1` | Phát hành từ terminal: tăng phiên bản, build + self-test, commit, tag `v*`, push → Actions tạo Release |
| `Vault/PurgeForm.cs` | Hộp thoại dọn kho theo tuổi |
| `CoreTests.cs`, `Tests07.cs` | Kiểm thử không cần Windows / kiểm thử 0.7 |

Biên dịch (đường chính): cần .NET SDK 6+ (khuyến nghị 8): `dotnet build -c Release` → `bin\Release\net48\TweekPro-0.7.exe`. Trên Linux/macOS thêm `-p:EnableWindowsTargeting=true` để kiểm tra biên dịch. `build.ps1` trên Windows gọi lệnh trên, in hướng dẫn cài SDK nếu thiếu (`winget install Microsoft.DotNet.SDK.8`); thêm `-SelfTest` để chạy `--self-test` sau khi build. Ngôn ngữ C# 7.3 (mức tối đa của net48); gói NuGet duy nhất: `Microsoft.Diagnostics.Tracing.TraceEvent` 3.1.30.

Dòng lệnh: `--self-test` (đầy đủ, Windows; tạo và dọn fixture `TweekProTest*`/`TweekProFixture*` trong AppData và HKCU), `--self-test core` (không cần Windows, chạy headless không cần màn hình), `--self-test junk` (core + dọn rác/kho, không Registry/UI), `--preview [scan|autorun|tools|junk|network|health]` kết xuất PNG, `--export-icon <tệp.ico>` ghi icon đa kích cỡ, `--scan-smoke`.

Tham khảo định dạng đăng ký cài đặt của Microsoft:
https://learn.microsoft.com/en-us/windows/win32/msi/uninstall-registry-key

## Cập nhật 0.2

- Tự quét sau từng ứng dụng gỡ thành công theo dấu hiệu đăng ký cài đặt biến mất; kết quả được gộp và loại trùng trong hàng đợi. Ứng dụng chưa xử lý hoặc vẫn còn đăng ký không được thêm vào kết quả tự quét.
- Chọn tất cả / Bỏ chọn tất cả áp dụng cho danh sách đang hiển thị; không tự xóa. Nút Xóa đã chọn vẫn cần xác nhận và giữ mọi kiểm tra bảo vệ.
- Tìm thêm theo tên thư mục cuối của InstallLocation (ví dụ GPU-Z), và đường dẫn Nhà phát hành/Tên ứng dụng trong AppData, ProgramData. Chỉ xét tên chính xác, chưa quét gần đúng toàn ổ.
- Xóa thư mục ở đây là chuyển ra khỏi vị trí gốc vào kho có thể khôi phục, không xóa vĩnh viễn hoặc giải phóng dung lượng.
- Chưa có tùy chọn System Restore Point, sao lưu toàn bộ Registry, quét giá trị AppCompatFlags/MUICache trong khóa hệ thống, hay ba chế độ quét như Revo trong ảnh. Bộ quét Registry hiện xử lý khóa ứng dụng trong phạm vi đã nêu.

## Cập nhật 0.3

- Ngày dạng yyyyMMdd, ISO hoặc ngày tháng có dấu phân cách được trình bày thành dd/MM/yyyy; thiếu hoặc không hợp lệ hiển thị Không rõ. Với ngày có dấu / bị mơ hồ, ưu tiên quy ước M/d/yyyy thường gặp của bộ cài; giá trị gốc có trong tooltip và phần chi tiết. Không dùng ngày tạo thư mục để đoán ngày cài. Cột ghi Ngày cài / cập nhật vì MSI có thể đổi ngày khi bảo trì.
- Đọc DisplayIcon của ứng dụng, hỗ trợ đường dẫn có dấu ngoặc và chỉ số tài nguyên trong EXE/DLL. Icon thiếu hoặc không đọc được dùng biểu tượng mặc định. Không tải icon qua mạng hoặc file đám mây chưa có sẵn.
- Giao diện mới với tiêu đề xanh đậm, hàng có icon 32px, nền xen kẽ, nút thao tác chính, tổng ứng dụng và khung chi tiết gọn. Dung lượng lớn hiển thị GB.
- Presentation.cs chứa phần định dạng và tải biểu tượng. Bản 0.2 đang mở không bị đóng khi tạo bản này.

## Cập nhật 0.4

Autorun Manager: liệt kê Run/RunOnce và shortcut Startup, tắt có sao lưu, khôi phục, sao chép vị trí và xuất CSV. Trạng thái hiển thị là đăng ký hoặc đã tắt bởi AppCare, không suy đoán trạng thái riêng của Windows Startup.
Windows Tools: 19 lối mở công cụ có sẵn. Không chạy tự động các công cụ sửa chữa.
Quét sâu: duyệt thư mục lồng, nhánh Registry nhà phát hành, giá trị Run/RunOnce/AppCompatFlags, shortcut và hiển thị chỉ xem các App Paths/dịch vụ/tác vụ liên quan. Có hủy quét, đối chiếu chống thay đổi, giới hạn và ghi chú kết quả chưa đầy đủ.
Xem SCAN-GUIDE.md để biết phạm vi chính xác, tài liệu Microsoft và hướng dẫn truy vết với Process Monitor. Các mục mô tả giới hạn ở những phiên bản trước được cập nhật bởi phần này.
Các tệp mới: Advanced.cs, AdvancedUI.cs, AdvancedTests.cs. build.ps1 đóng gói AppCare-0.4.exe.

## Cập nhật 0.5

Cửa sổ Quét phần còn sót: ngay sau khi trình gỡ kết thúc và đăng ký cài đặt biến mất, AppCare mở một cửa sổ riêng (tương tự Revo Uninstaller) thay cho việc chuyển tab. Cửa sổ hiển thị giai đoạn quét đang chạy, số thư mục đã duyệt và số mục tìm thấy theo thời gian thực; danh sách gồm loại, đường dẫn, dung lượng (tính sau khi quét, có giới hạn 20.000 tệp/5 giây mỗi thư mục), trạng thái và cơ sở đề xuất. Các nút **Chọn tất cả**, **Bỏ chọn**, **Xóa đã chọn**, **Dừng quét** và **Đóng** nằm ở chân cửa sổ. Xóa chỉ diễn ra khi bấm Xóa đã chọn và xác nhận; từng mục được sao lưu vào kho trước, dòng đã xóa được gạch ngang, dòng lỗi tô đỏ với lý do trong tooltip, và dải kết quả cho biết số mục đã xóa/thất bại. Nhấp đúp một dòng để mở thư mục hoặc xem chi tiết. Nếu ứng dụng vẫn còn đăng ký cài đặt, cửa sổ chỉ cho xem. Mục chưa xử lý được gộp vào tab Phần còn sót khi đóng cửa sổ.
Nút Quét mục đang xem cũng mở cửa sổ này (quét sâu khi tùy chọn Quét sâu sau khi gỡ đang bật).
Giao diện: bảng màu, phông chữ và khoảng cách thống nhất qua lớp Theme; đầu trang gọn hơn, thanh công cụ tự xuống dòng, nút chính/nút xóa được phân biệt rõ, dải ghi chú theo loại (thông tin/cảnh báo/kết quả), dòng xen kẽ và tooltip đường dẫn trong mọi danh sách, kho khôi phục hiển thị ngày giờ dễ đọc và tô màu trạng thái.
Tệp mới: ScanWindow.cs, AppCare.csproj. build.ps1 đóng gói AppCare-0.5.exe.

## Cập nhật 0.6

Giao diện: Theme dùng chung trên mọi tab (Ứng dụng, Phần còn sót, Kho khôi phục, Autorun, Windows Tools, Nhật ký) — cùng khoảng cách, phông, màu, nút chính/nút xóa, dải ghi chú, dòng xen kẽ và tooltip đường dẫn. Cửa sổ quét nhóm kết quả theo loại (thư mục / tệp / shortcut / registry / chỉ xem), có trạng thái trống, lỗi, và dải “quét chưa đầy đủ” khi dừng giữa chừng hoặc bị từ chối quyền. Danh sách ứng dụng có trạng thái đang tải, không có ứng dụng, không khớp tìm kiếm, và lỗi đọc danh sách.
Phần còn sót theo từng tài khoản: quét `%LOCALAPPDATA%`, `%LOCALAPPDATA%\Programs` (cài kiểu Devin/Electron), `%APPDATA%`, LocalLow, ProgramData và InstallLocation. Không duyệt mù toàn bộ Temp; chỉ thêm `%LOCALAPPDATA%\Temp` khi trùng đúng tên ứng dụng, nhà phát hành\ứng dụng, thư mục cuối của InstallLocation hoặc thư mục executable đã ghi nhận. Thư mục `Programs` gốc vẫn được giữ; chỉ các thư mục con khớp dấu vân tay mới được đề xuất. Xóa vẫn cần Chọn / Xóa đã chọn, xác nhận và sao lưu vào kho. Dịch vụ/driver/tác vụ lịch vẫn chỉ xem.
build.ps1 đóng gói AppCare-0.6.exe.

## Giao diện và nhận diện

Ứng dụng có biểu tượng riêng vẽ bằng mã (không dùng icon mặc định của WinForms): một huy hiệu bo góc chuyển sắc xanh với chữ "T" và một chấm sáng, dùng làm icon cửa sổ/thanh tác vụ và logo trên dải tiêu đề đậm. Mỗi tab có một icon vector nhỏ vẽ bằng GDI+ (ứng dụng, kính lúp, thùng rác, chart, khiên, tia sét, quả cầu, bánh răng, tài liệu…) cùng dải gạch chân nhấn màu cho tab đang chọn, giúp người dùng không chuyên nhận diện nhanh. Các tab tiếng Anh được đổi sang nhãn tiếng Việt ngắn gọn ("Khởi động", "Công cụ"). Toàn bộ được vẽ bằng vector nên nét ở mọi mức phóng đại và DPI. Xem `Branding.cs`.

## Cập nhật 0.7

Khôi phục Local AI từ 7 file đã cứu: LM Studio/Ollama, chọn model, API key tùy chọn, ba chế độ thao tác và công cụ quét/dọn phần còn sót; sửa cấu hình lỗi dùng nhầm endpoint cũ, chặn đường dẫn mơ hồ, model thiếu công cụ chỉ chat. Tích hợp icon T + PC mẫu 2. Build và toàn bộ self-test Windows đã qua; đã thử HTTP/giao diện bằng máy chủ local giả lập, chưa thử suy luận bằng model thật.

Đổi tên thành **Tweek Pro – Trình quản lý Windows**; namespace `TweekPro`, exe `TweekPro-0.7.exe`; thư mục dữ liệu `%LOCALAPPDATA%\TweekPro` với di trú tự động từ `AppCare` và khả năng đọc kho cũ tại chỗ.
Build chính chuyển sang `TweekPro.csproj` SDK-style (net48, C# 7.3, NuGet); `build.ps1` bọc `dotnet build` và báo rõ khi thiếu SDK.
Kho khôi phục: xóa vĩnh viễn theo mục đánh dấu hoặc theo tuổi (hộp thoại xem trước số mục/dung lượng, mặc định chỉ bản đã khôi phục), cột dung lượng và cột kho.
Dọn rác v1: quy tắc JSON (Temp, Windows\Temp, CrashDumps, WER, thumbnail cache, cache Chrome/Edge/Brave/Firefox chỉ khi trình duyệt đã đóng), xem trước theo nhóm với dung lượng, hai chế độ (vào Kho / xóa thẳng có nhãn đỏ và xác nhận hai lần), bỏ qua tệp đang mở và tệp mới, báo cáo kết quả, khôi phục từng tệp từ kho.
Mạng (chỉ xem): bảng TCP/UDP theo tiến trình không cần quyền; băng thông và tốc độ theo tiến trình qua ETW khi chạy quản trị; chu kỳ làm mới, tạm dừng, tìm kiếm, ẩn loopback, phân giải tên máy tùy chọn, CSV.
Huy hiệu quyền và nút Khởi động lại với quyền quản trị; `settings.json`; nhật ký xoay vòng theo ngày; bắt lỗi chưa xử lý ghi vào nhật ký.
Tệp trùng lặp v1: quét chỉ đọc một thư mục để tìm tệp giống hệt nhau (gom theo kích thước rồi SHA-256), chọn giữ bản cũ nhất/mới nhất, chuyển bản dư vào Kho (khôi phục được) hoặc xóa thẳng; hàng rào an toàn `DupeSafety` chặn thư mục hệ thống và dữ liệu Tweek Pro, bỏ qua liên kết và tệp đang mở.
Phân tích ổ đĩa v1: quét chỉ đọc một thư mục, hiển thị thư mục con nặng nhất, dung lượng theo phần mở rộng và các tệp lớn nhất; mở vị trí trong Explorer và xuất CSV. Không xóa hay di chuyển gì.
Thư mục rỗng v1: tìm và xóa các nhánh thư mục hoàn toàn rỗng, kiểm tra lại ngay trước khi xóa.
Mạng: gộp theo ứng dụng với icon packet ra/vào theo hướng và dải animation luồng packet realtime.
Tổng quan v1: kiểm tra sức khỏe một nút với điểm 0–100, hạng A–E, sáu khu vực đo chỉ đọc, dung lượng có thể giải phóng, nhảy tới tab xử lý, sao chép báo cáo.
Giao diện: logo riêng, icon vector cho từng tab, nhãn tab tiếng Việt; thứ tự tab chuẩn: Tổng quan → Ứng dụng → Ứng dụng Windows → Phần còn sót → Dọn rác → Thư mục rỗng → Tệp trùng lặp → Tệp tải về cũ → Phân tích ổ đĩa → Kho khôi phục → Khởi động → Mạng → Dịch vụ hệ thống → Công cụ → Nhật ký → Trợ lý AI (tab cuối, theo yêu cầu chủ dự án).
Phát hiện PUP/bloatware v1 (`Pup/`): quy tắc nhúng `pup-rules.json` (11 nhóm: thanh công cụ, quảng cáo/dọa lỗi, tối ưu tiếp thị, bảo mật cài sẵn/thử nghiệm, thành phần đi kèm và runtime cũ, cài sẵn theo máy, gói Store quảng cáo và ứng dụng Windows tùy chọn) khớp theo tên/nhà phát hành/tên gói; `PupSafety` trong mã bảo vệ nhà phát hành và tên sản phẩm tin cậy, gói Appx được bảo vệ/cần cân nhắc không bao giờ bị đánh dấu; trình đọc quy tắc từ chối mẫu quá rộng. Cột Cảnh báo ở tab Ứng dụng và Ứng dụng Windows, lọc «Chỉ hiện mục cảnh báo», khu vực thứ bảy trong Tổng quan (Ứng dụng không mong muốn: 1–2 mục Nên xem, 3+ Cần dọn, có mục nên gỡ → Khẩn). Chỉ đọc: gỡ vẫn qua luồng gỡ ứng dụng hiện có. `--preview apps`. Từ điển tiếng Anh của tính năng nằm ở `Pup/PupLang.cs`, `Core.L` gộp các bảng và ném lỗi khi trùng khóa.
Tệp tải về cũ v1 (`Stale/`): quét chỉ đọc Downloads/Desktop tìm bộ cài, tệp nén, ảnh đĩa, tệp tải dở và tệp lớn cũ hơn N ngày (tuổi = mốc mới hơn giữa ngày tạo và ngày sửa); chuyển vào Kho (`Kind=Stale`, khôi phục qua `Engine.Restore`) hoặc xóa thẳng; `StaleSafety` giới hạn cứng trong Downloads/Desktop và từ chối thư mục đám mây, shortcut, tệp ẩn/hệ thống; cài đặt `staleMinAgeDays`/`staleLargeMB`/`staleIncludeDesktop` với mặc định phục hồi cho `settings.json` cũ; icon tab mũi tên tải xuống; `--preview stale`; `StaleTests` (phân loại, tuổi, biên, quét, kho, khôi phục không ghi đè, xóa thẳng bỏ qua tệp đang mở, cài đặt).
Icon thanh tác vụ Windows 11: gán icon 256 px qua `WM_SETICON` (ICON_SMALL/ICON_BIG) và đăng ký AppUserModelID `TweekPro.App.0.7.tpc` để taskbar không dùng cache icon cũ; `Branding.AppIcon` lấy đúng khung PNG trong ICO đa kích thước.
Rác hệ thống: nhóm quy tắc cần quản trị cho cache Windows Update, nhật ký CBS, dump kernel, nhật ký Panther, Downloaded Program Files; INetCache và cache shader GPU cho tài khoản; `JunkSafety.WindowsCoreFolders` chặn cứng System32/SysWOW64/WinSxS/servicing/Boot/Fonts/Installer… và Program Files, có test chứng minh System32, thư mục con của nó, gốc Windows và gốc ổ đĩa đều bị từ chối.
Quyền quản trị mặc định qua `app.manifest` (kèm PerMonitorV2, long paths); bỏ nút Khởi động lại với quyền quản trị; nút ngôn ngữ thành icon quả cầu EN/VI.
Tệp cứng đầu: `Core/StubbornFiles` (bỏ thuộc tính, chiếm quyền sở hữu với SeTakeOwnership/SeRestore, bậc thang leo thang, hẹn xóa khi khởi động lại) nối vào Quarantine thư mục/tệp; trạng thái Kho `PendingReboot`.
Ứng dụng Windows v1: `Store/WindowsApps` (PowerShell `-EncodedCommand`, phân loại trong mã, tên thân thiện, logo từ AppxManifest với biến thể scale/targetsize, script gỡ/đăng ký lại, chặn tên gói bất thường), tab riêng với icon, `--preview store [--show]`.
Dịch vụ hệ thống v1: tab riêng với cơ sở kiến thức nhúng, huy hiệu an toàn, khung chi tiết bằng lời thường và menu chuột phải dừng/khởi động/vô hiệu hóa; menu chuột phải cũng được thêm cho bảng Ứng dụng, Ứng dụng Windows, Khởi động và Kho khôi phục.
Trợ lý AI v1: tab nhập API key Claude/OpenAI (DPAPI), chat có gọi công cụ, 14 công cụ (9 chỉ đọc, 5 thay đổi có xác nhận), kiểm thử ngoài mạng bằng transport giả.
Kiểm tra cập nhật v1 (`Update/`): nút opt-in trên tab Tổng quan (và Công cụ nếu có) hỏi GitHub Releases/latest, so sánh với `MainForm.Version`; nếu chưa có Release thì fallback tag `v*` mới nhất (không có URL bộ cài). Chỉ thông báo và mở trang tải — không tải/ghi đè Program Files im lặng. `UpdateTests` không cần mạng.

Windows Tools: mỗi dòng lấy icon hệ thống qua `Presentation.ShellIcon` (`ExtractIconEx`, rồi `SHGetFileInfo` cho `.msc`); catalog mở rộng (cpl, backup, netstat, chkdsk…); cột Dòng lệnh kiểu Revo.
Song ngữ: `Core/L.cs` (`L.T`/`L.F`) + từ điển `Core/LangEn.cs`; cài đặt `language`; nút chuyển ngôn ngữ ở header khởi động lại ứng dụng; `LangTests` kiểm tra bảng dịch không rỗng, giữ nguyên placeholder, mọi tab/quy tắc rác có bản dịch và glyph tab khớp ở cả hai ngôn ngữ, engine Tổng quan nói tiếng Anh khi được chọn.
Kiểm thử: `CoreTests` (chạy được ngoài Windows), `Tests07` (dọn rác/kho/purge/kho cũ/bảng kết nối + gọi `DupeTests`, `AnalyzerTests`, `NetworkStatsTests`, `PacketAnimatorTests`, `EmptyFolderTests`, `HealthTests`, `LangTests`, `StubbornTests`, `WindowsAppsTests`); `--self-test` chạy trước khi khởi tạo WinForms nên không cần màn hình; `TEST-PLAN.md` cho máy ảo.
