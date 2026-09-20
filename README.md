# Tweek Pro – Trình quản lý Windows (bản 0.7)

Ứng dụng Windows 64-bit, mã nguồn mở, kế thừa AppCare 0.6: gỡ ứng dụng theo hàng đợi, quét và dọn phần còn sót, **dọn tệp rác theo quy tắc**, **theo dõi mạng theo tiến trình (chỉ xem)** và kho khôi phục có **xóa vĩnh viễn**. Nguyên tắc không đổi: mọi thao tác phá hủy đều được xác nhận và sao lưu (trừ khi bạn chủ động chọn xóa thẳng), thư mục hệ thống/dùng chung bị chặn, service/driver/tác vụ lịch chỉ xem, không kernel driver, không telemetry.

Tên gọi cố ý viết "Tweek Pro". Mã nguồn dùng namespace `TweekPro.*`.

## Mở ứng dụng

Chạy `TweekPro-0.7.exe` trong thư mục phân phối (cạnh các DLL `Microsoft.Diagnostics.Tracing.TraceEvent`…; phải copy cả thư mục). Chỉ cần .NET Framework 4.8 có sẵn trong Windows 10/11.
Mặc định chạy với quyền người dùng; đầu cửa sổ có huy hiệu quyền và nút **Khởi động lại với quyền quản trị** (một lần UAC). Quyền quản trị chỉ cần cho: băng thông mạng (ETW), `C:\Windows\Temp`, báo cáo lỗi toàn máy và khóa HKLM.

## Các tab

| Tab | Chức năng | Ghi chú an toàn |
|---|---|---|
| Ứng dụng | Danh sách ứng dụng desktop (Uninstall HKLM/HKCU), gỡ theo hàng đợi, quét mục đang xem, CSV | Không dùng Win32_Product; chặn lệnh gỡ qua cmd/PowerShell/script |
| Phần còn sót | Kết quả quét nhanh/sâu chờ duyệt; Chọn/Bỏ chọn/Xem/Xóa (có sao lưu) | Từ chối dọn khi ứng dụng còn đăng ký; mục Chỉ xem không thể tích |
| **Dọn rác** (mới) | Xem trước theo quy tắc với số tệp và dung lượng; chọn từng nhóm; hai chế độ: vào Kho (mặc định) hoặc **Xóa thẳng** (nhãn đỏ, xác nhận hai lần) | Bỏ qua tệp mới hơn 24 giờ (cache trình duyệt: 0 giờ), tệp đang mở, liên kết; nhóm cache trình duyệt bị khóa khi trình duyệt còn chạy; không bao giờ chạm Documents/Desktop/Downloads/OneDrive |
| Kho khôi phục | Khôi phục mục đang chọn; **Xóa vĩnh viễn mục đã đánh dấu**; **Dọn kho theo tuổi…** (xem trước số mục và dung lượng) | Xóa vĩnh viễn không hoàn tác được; mặc định chỉ xóa bản đã khôi phục; kho AppCare cũ vẫn hiển thị và khôi phục được |
| Autorun Manager | Run/RunOnce/shortcut Startup: tắt có sao lưu, bật lại, CSV | Không dừng tiến trình đang chạy |
| **Mạng** (mới) | Kết nối TCP/UDP theo tiến trình (tên, PID, nhà phát hành chữ ký, endpoint, trạng thái), làm mới 1–30 giây, tạm dừng, tìm kiếm, ẩn loopback, phân giải tên máy (tắt mặc định), CSV; khi có quyền quản trị: **Bật băng thông (ETW)** hiển thị byte gửi/nhận và tốc độ theo tiến trình | Chỉ xem, không chặn, không driver; phiên ETW tên `TweekPro-Network` luôn được dừng khi đóng |
| Windows Tools | 19 lối mở công cụ có sẵn | SFC hỏi xác nhận quyền quản trị |
| Nhật ký | Nhật ký phiên, nút mở thư mục nhật ký/dữ liệu, lưu cài đặt | Nhật ký xoay vòng theo ngày |

## Dọn rác: quy tắc và giới hạn

Quy tắc là dữ liệu JSON đóng gói trong exe (`Cleaner/junk-rules.json`). Đặt một bản sửa đổi tại `%LOCALAPPDATA%\TweekPro\junk-rules.json` để ghi đè (nút **Nạp lại quy tắc**). Quy tắc mặc định: `%TEMP%`, `C:\Windows\Temp` (cần quản trị), `CrashDumps` (*.dmp), WER ReportQueue/ReportArchive (tài khoản và toàn máy), thumbcache/iconcache của Explorer, cache của Chrome/Edge/Brave (Cache, Code Cache, GPUCache, ShaderCache) và Firefox (cache2, startupCache, shader-cache).

Hàng rào nằm trong mã, không trong JSON: một quy tắc chỉ được trỏ vào các vùng rác cho phép (Temp, CrashDumps, WER, Explorer cache, INetCache, cache GPU) hoặc vào thư mục hồ sơ trình duyệt **với điều kiện** đường dẫn có một thư mục cache (`Cache`, `Code Cache`, `GPUCache`, `cache2`…). Đường dẫn chạm Documents, Desktop, Pictures, Music, Videos, Downloads, OneDrive, Program Files, System32 hay thư mục dữ liệu của Tweek Pro bị từ chối. Mỗi quy tắc dừng ở 100.000 tệp / 30 giây và báo "chưa đủ".

Chế độ vào Kho tạo một bản sao lưu loại **Rác** cho mỗi nhóm, gồm `files.xml` ánh xạ từng tệp về đường dẫn gốc; khôi phục bỏ qua tệp đã tồn tại ở đích. Dung lượng chỉ được giải phóng khi xóa vĩnh viễn trong Kho. Thư mục con rỗng sau khi dọn được xóa (gốc quy tắc giữ nguyên).

## Mạng: cơ chế

- Bảng kết nối: `GetExtendedTcpTable`/`GetExtendedUdpTable` (iphlpapi, `TCP_TABLE_OWNER_PID_ALL`, IPv4 và IPv6), không cần quyền. Tên/đường dẫn tiến trình qua `QueryFullProcessImageName` với quyền truy vấn hạn chế; nhà phát hành lấy từ chữ ký Authenticode của tệp exe (cache theo đường dẫn).
- Băng thông: phiên ETW thời gian thực trên nhà cung cấp TCP/IP của kernel (cùng nguồn Task Manager dùng) qua thư viện `Microsoft.Diagnostics.Tracing.TraceEvent`; sự kiện send/recv TCP/UDP v4/v6 được cộng theo PID trên luồng riêng, giao diện chỉ đọc ảnh chụp mỗi chu kỳ. Cần quyền quản trị. Số liệu tính từ lúc bật ETW, gồm cả loopback (có thể ẩn). Bộ đếm `EventsLost` hiển thị ở dòng tóm tắt khi có mất sự kiện.
- Phân giải tên máy ngược là tùy chọn, bất đồng bộ, có cache; tắt thì không gửi truy vấn DNS nào.
- Đầu tab có **dải luồng packet realtime** (`PacketFlowStrip`): packet bay lên ở làn "Ra ↑" (xanh dương) và bay xuống ở làn "Vào ↓" (xanh lá), mật độ tỉ lệ với tốc độ gửi/nhận hiện tại (một packet ≈ 32 KB/s), cập nhật ~20 khung/giây. Mô hình animation `PacketAnimator` không dùng ngẫu nhiên nên tất định và có self-test; phần vẽ `PacketFlowRenderer` là GDI+ thuần.
- Mỗi dòng có **icon packet theo hướng** vẽ bằng GDI+: mũi tên lên xanh dương = gửi ra (outbound), mũi tên xuống xanh lá = nhận vào (inbound), hai mũi tên = hai chiều, vòng tròn = đang lắng nghe, chấm xám = nhàn rỗi. Hướng suy ra từ tốc độ gửi/nhận theo tiến trình khi bật ETW (ngưỡng nhiễu 16 B/s), hoặc từ trạng thái kết nối khi chưa bật. Cột "Hướng" hiển thị mũi tên tương ứng. Lớp `Network/NetworkStats` (gộp theo tiến trình, phân loại hướng) chạy được ngoài Windows và có self-test.

## Dữ liệu Tweek Pro

Thư mục dữ liệu: `%LOCALAPPDATA%\TweekPro` (kho `Backups`, `sessions.xml`, `settings.json`, `Logs\tweekpro-yyyyMMdd.log` giữ 14 ngày, `junk-rules.json` tùy chọn).
Lần chạy đầu, nếu chưa có thư mục này mà có `%LOCALAPPDATA%\AppCare`, toàn bộ thư mục cũ được **đổi tên** sang `TweekPro` (không sao chép, không gộp). Nếu không đổi tên được, kho cũ vẫn được đọc tại chỗ và hiển thị với cột Kho = "AppCare (cũ)"; khôi phục và xóa vĩnh viễn hoạt động trên cả hai kho. Bản sao lưu mới luôn ghi vào kho TweekPro.
Dữ liệu chỉ lưu cục bộ, không gửi đi đâu. Đây không phải bản sao lưu chống hỏng ổ đĩa.

## Giới hạn của bản 0.7

Chưa có theo dõi cài đặt, forced uninstall, gỡ ứng dụng Store, quản lý tiện ích trình duyệt, chặn mạng, trợ lý AI, bộ cài, chữ ký số hay tự cập nhật (xem lộ trình 1.0/2.0). Không xác định được mọi dấu vết của mọi ứng dụng. Băng thông theo tiến trình cần quyền quản trị và chỉ tính từ khi bật. Bản phân phối là một thư mục (exe + DLL), chưa gộp thành một tệp. Chuỗi giao diện vẫn nằm trong mã (chưa `.resx`).

## Kiểm chứng đã thực hiện

- `dotnet build -c Release -p:EnableWindowsTargeting=true` trên Linux: 0 lỗi, 0 cảnh báo.
- `TweekPro-0.7.exe --self-test junk` chạy bằng Mono trên Linux: PASS (xem `test-results.txt`) — phân tích quy tắc JSON, mở rộng đường dẫn, glob, hàng rào an toàn, chọn bản cần xóa theo tuổi, di trú thư mục dữ liệu, `settings.json`, xoay vòng nhật ký, xem trước/dọn/khôi phục rác qua kho, xóa thẳng, bỏ qua tệp đang mở, khóa quy tắc khi tiến trình chạy, xóa vĩnh viễn, liệt kê kho cũ.
- **Chưa chạy trên Windows** trong lần phát hành này: giao diện WinForms, `--self-test` đầy đủ (Registry, deep scan, autorun), bảng kết nối iphlpapi, phiên ETW, khởi động lại với quyền quản trị. Xem `TEST-PLAN.md` trước khi dùng thật; chỉ thử tính năng phá hủy trong máy ảo.

## Mã nguồn và biên dịch

| Tệp/thư mục | Nội dung |
|---|---|
| `Engine.cs` | Lõi: inventory, quét, hàng rào, kho (quarantine/restore/**purge**), đọc cả kho cũ |
| `App.cs` | Cửa sổ chính, header (huy hiệu quyền, khởi động lại quản trị), tab Kho, `Program.Main`, `Tests` |
| `ScanWindow.cs`, `Presentation.cs`, `Advanced.cs`, `AdvancedUI.cs`, `AdvancedTests.cs` | Cửa sổ quét, Theme, quét sâu, Autorun, Windows Tools (kế thừa 0.6) |
| `Core/` | `Paths` (thư mục dữ liệu + di trú), `Settings` (JSON), `Log` (xoay vòng), `Elevation` |
| `Cleaner/` | `JunkRules` (JSON, mở rộng đường dẫn, `JunkSafety`), `JunkCleaner` (xem trước/dọn/khôi phục), `JunkUI`, `junk-rules.json` |
| `Network/` | `ConnectionTable` (iphlpapi), `ProcessResolver`, `EtwNetworkSession` (TraceEvent), `NetworkStats` (gộp theo tiến trình + phân loại hướng), `NetworkGlyphs` (icon packet ra/vào), `PacketAnimator`/`PacketFlow` (animation luồng packet realtime), `NetworkUI`, `NetworkStatsTests`, `PacketAnimatorTests` |
| `Vault/PurgeForm.cs` | Hộp thoại dọn kho theo tuổi |
| `CoreTests.cs`, `Tests07.cs` | Kiểm thử không cần Windows / kiểm thử 0.7 |

Biên dịch (đường chính): cần .NET SDK 6+ (khuyến nghị 8): `dotnet build -c Release` → `bin\Release\net48\TweekPro-0.7.exe`. Trên Linux/macOS thêm `-p:EnableWindowsTargeting=true` để kiểm tra biên dịch. `build.ps1` trên Windows gọi lệnh trên, in hướng dẫn cài SDK nếu thiếu (`winget install Microsoft.DotNet.SDK.8`); thêm `-SelfTest` để chạy `--self-test` sau khi build. Ngôn ngữ C# 7.3 (mức tối đa của net48); gói NuGet duy nhất: `Microsoft.Diagnostics.Tracing.TraceEvent` 3.1.30.

Dòng lệnh: `--self-test` (đầy đủ, Windows; tạo và dọn fixture `TweekProTest*`/`TweekProFixture*` trong AppData và HKCU), `--self-test core` (không cần Windows), `--self-test junk` (core + dọn rác/kho, không Registry/UI), `--preview [scan|autorun|tools|junk|network]` kết xuất PNG, `--scan-smoke`.

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

## Cập nhật 0.7

Đổi tên thành **Tweek Pro – Trình quản lý Windows**; namespace `TweekPro`, exe `TweekPro-0.7.exe`; thư mục dữ liệu `%LOCALAPPDATA%\TweekPro` với di trú tự động từ `AppCare` và khả năng đọc kho cũ tại chỗ.
Build chính chuyển sang `TweekPro.csproj` SDK-style (net48, C# 7.3, NuGet); `build.ps1` bọc `dotnet build` và báo rõ khi thiếu SDK.
Kho khôi phục: xóa vĩnh viễn theo mục đánh dấu hoặc theo tuổi (hộp thoại xem trước số mục/dung lượng, mặc định chỉ bản đã khôi phục), cột dung lượng và cột kho.
Dọn rác v1: quy tắc JSON (Temp, Windows\Temp, CrashDumps, WER, thumbnail cache, cache Chrome/Edge/Brave/Firefox chỉ khi trình duyệt đã đóng), xem trước theo nhóm với dung lượng, hai chế độ (vào Kho / xóa thẳng có nhãn đỏ và xác nhận hai lần), bỏ qua tệp đang mở và tệp mới, báo cáo kết quả, khôi phục từng tệp từ kho.
Mạng (chỉ xem): bảng TCP/UDP theo tiến trình không cần quyền; băng thông và tốc độ theo tiến trình qua ETW khi chạy quản trị; chu kỳ làm mới, tạm dừng, tìm kiếm, ẩn loopback, phân giải tên máy tùy chọn, CSV.
Huy hiệu quyền và nút Khởi động lại với quyền quản trị; `settings.json`; nhật ký xoay vòng theo ngày; bắt lỗi chưa xử lý ghi vào nhật ký.
Kiểm thử: `CoreTests` (chạy được ngoài Windows), `Tests07` (dọn rác/kho/purge/kho cũ/bảng kết nối); `TEST-PLAN.md` cho máy ảo.
