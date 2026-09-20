# AppCare — bản thử nghiệm 0.6

Ứng dụng Windows 64-bit để quản lý phần mềm desktop, chạy trình gỡ chính thức theo hàng đợi, duyệt các mục nghi còn sót và sao lưu/khôi phục chúng.

## Mở ứng dụng

Nhấp đúp AppCare-0.6.exe. Không cần cài thêm gói hay tải SDK trên máy này.
Mặc định chạy với quyền người dùng. Nếu thao tác trong Program Files/HKLM báo thiếu quyền, đóng ứng dụng rồi chọn Run as administrator bằng cùng tài khoản Windows. Không cần quyền quản trị để xem danh sách thông thường.

## Quy trình sử dụng

1. Ở tab Ứng dụng, tìm theo tên/nhà phát hành và đánh dấu các ứng dụng muốn gỡ.
2. Chọn Gỡ mục đã chọn. AppCare lưu thông tin các ứng dụng trước khi mở trình gỡ chính thức lần lượt. Xác nhận trong từng trình gỡ. Bạn có thể dừng hàng đợi trước ứng dụng tiếp theo.
3. Sau mỗi lần gỡ, khi đăng ký cài đặt biến mất, AppCare tự mở cửa sổ **Quét phần còn sót** (xem mục Cập nhật 0.5 và 0.6). Nếu ứng dụng vẫn còn đăng ký, AppCare không tự quét lần đó và yêu cầu kiểm tra cửa sổ gỡ phụ. Nếu trình gỡ chuyển sang cửa sổ khác, phải chờ cửa sổ đó hoàn tất trước khi xác nhận tiếp tục trong AppCare.
4. Kiểm tra từng đường dẫn. Tên trùng hoặc InstallLocation không chứng minh mọi nội dung thuộc riêng ứng dụng. Thư mục có thể chứa dự án, hồ sơ và dữ liệu cá nhân.
5. Trong cửa sổ quét, đánh dấu các mục đã kiểm tra hoặc bấm Chọn tất cả; dùng Bỏ chọn để bỏ đánh dấu; rồi bấm Xóa đã chọn và xác nhận. Kết quả (số mục đã xóa, số mục lỗi và lý do) hiển thị ngay trong cửa sổ. Mục chưa xử lý được chuyển sang tab Phần còn sót để duyệt sau với cùng bộ nút. Nếu đăng ký cài đặt vẫn tồn tại, AppCare từ chối dọn. Chạy Quét lại lịch sử gỡ để cập nhật sau khi gỡ hoặc khởi động lại.
6. Ở Kho khôi phục, chọn một mục rồi Khôi phục mục đang chọn. Không ghi đè nếu vị trí gốc đã tồn tại.

## Cơ chế và phạm vi

- Đọc các khóa Uninstall của HKLM/HKCU ở chế độ 32/64-bit. Không dùng Win32_Product và không kích hoạt sửa chữa MSI khi liệt kê.
- Dung lượng, phiên bản và ngày cài do ứng dụng tự khai báo; có thể thiếu/sai. Cột Nguồn là chế độ Registry, không phải kiến trúc thực của phần mềm.
- Gỡ MSI bằng /x với mã sản phẩm đã xác thực; các ứng dụng khác chạy UninstallString dạng .exe có đường dẫn tuyệt đối. Không chạy lệnh qua cmd/PowerShell/script. Lệnh không hỗ trợ được báo để dùng Windows Settings.
- Quét InstallLocation, `%LOCALAPPDATA%\Programs\<tên>`, thư mục trùng DisplayName trong AppData/LocalLow/ProgramData, và khóa SOFTWARE theo tên ứng dụng hoặc nhà phát hành/tên ứng dụng. Không quét gần đúng toàn ổ hoặc toàn Registry. Temp chỉ khi trùng đúng dấu vân tay.
- Chặn thư mục hệ thống/dùng chung trong vùng được bảo vệ và đường dẫn chứa liên kết. Chặn thư mục giao với InstallLocation của ứng dụng còn cài; Registry cùng tên ứng dụng còn cài cũng được giữ lại.
- File được chuyển vào kho trên cùng ổ đĩa. Đây là thao tác có thể khôi phục, CHƯA giải phóng dung lượng ổ đĩa. Không có chức năng xóa vĩnh viễn kho trong phiên bản này.
- Registry được sao lưu khóa con và giá trị, gồm kiểu dữ liệu. Không sao lưu quyền ACL. Không hoàn tác thao tác do trình gỡ chính thức thực hiện và không cài lại phần mềm.
- Mỗi mục có manifest trước khi dọn. Pending/NeedsReview nghĩa là thao tác có thể đã gián đoạn: kiểm tra cả nguồn và kho. Nếu Registry còn tồn tại một phần, chức năng khôi phục từ chối ghi đè; cần kiểm tra thủ công.
- Không đóng/cài lại ứng dụng hoặc chỉnh sửa các thư mục đang được dọn từ chương trình khác trong lúc xử lý.

## Dữ liệu AppCare

Kho: %LOCALAPPDATA%\AppCare\Backups
Lịch sử ứng dụng đã quét/gỡ: %LOCALAPPDATA%\AppCare\sessions.xml
Nhật ký: %LOCALAPPDATA%\AppCare\activity.log
Dữ liệu chỉ lưu cục bộ. Không xóa thư mục này nếu vẫn cần khôi phục. Đây không phải bản sao lưu chống hỏng ổ đĩa.

## Giới hạn của bản thử nghiệm

Chưa tương đương Revo Uninstaller Pro. Chưa có theo dõi cài đặt, forced uninstall, xóa service/driver/tác vụ lịch, quản lý tiện ích trình duyệt hay hỗ trợ đầy đủ ứng dụng Microsoft Store. Không xác định được mọi dấu vết của mọi ứng dụng. Không tìm thấy mục không có nghĩa ứng dụng đã sạch toàn bộ. Không có cơ chế transaction toàn hệ thống và không có bộ cài/chữ ký số.

## Kiểm chứng đã thực hiện

Biên dịch thành công; thử đọc danh sách ứng dụng thật (chỉ đọc); thử sao lưu/khôi phục thư mục mẫu, round-trip các kiểu Registry mẫu, chống ghi đè, chặn vùng hệ thống và phân tích lệnh gỡ. Giao diện đã được kết xuất để kiểm tra bố cục.
Chưa chạy gỡ phần mềm thật trên máy người dùng. Cần kiểm thử thêm trong máy ảo với các bộ cài khác nhau trước khi dùng như công cụ quản trị chuyên nghiệp.
Kết quả kiểm thử ở test-results.txt. Kiểm thử chỉ tạo và dọn các mẫu có tên ngẫu nhiên AppCareTest trong AppData và HKCU\SOFTWARE.

## Mã nguồn và biên dịch

Engine.cs: phần lõi. App.cs: cửa sổ chính và bộ kiểm thử. ScanWindow.cs: cửa sổ quét phần còn sót. Presentation.cs: định dạng, biểu tượng và Theme dùng chung. Advanced.cs/AdvancedUI.cs: quét sâu, Autorun Manager, Windows Tools.

Cách 1 — không cần cài gì thêm: chạy `build.ps1` trong PowerShell để biên dịch bằng trình biên dịch .NET Framework có sẵn trong Windows; kết quả là `AppCare-0.6.exe` cạnh mã nguồn.

Cách 2 — có .NET SDK (6 trở lên): `dotnet build -c Release`; kết quả ở `bin\Release\net48\AppCare.exe`. Trên Linux/macOS thêm `-p:EnableWindowsTargeting=true` để chỉ kiểm tra biên dịch (ứng dụng chỉ chạy trên Windows).

`AppCare-0.6.exe --self-test`: kiểm thử mẫu. `--preview`: kết xuất ảnh cửa sổ chính; `--preview scan`: kết xuất ảnh cửa sổ quét với dữ liệu minh họa.

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
