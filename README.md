<p align="center">
  <img src="Assets/TweekPro.png" width="96" alt="Tweek Pro">
</p>

<h1 align="center">Tweek Pro</h1>

<p align="center">
  Trình quản lý Windows chuyên sâu: gỡ sạch ứng dụng, dọn rác, kiểm tra sức khỏe máy, theo dõi mạng và dịch vụ, trợ lý AI.<br>
  Mọi thao tác phá hủy đều được xác nhận và có thể khôi phục từ Kho.
</p>

<p align="center">
  <a href="https://github.com/tuantien0001/TweekPro/releases/latest"><img src="https://img.shields.io/github/v/release/tuantien0001/TweekPro?label=Release&color=2ea043" alt="Release"></a>
  <a href="https://github.com/tuantien0001/TweekPro/actions/workflows/release.yml"><img src="https://img.shields.io/github/actions/workflow/status/tuantien0001/TweekPro/release.yml?branch=main&label=Build" alt="Build"></a>
  <a href="https://github.com/tuantien0001/TweekPro/releases"><img src="https://img.shields.io/github/downloads/tuantien0001/TweekPro/total?label=T%E1%BA%A3i%20v%E1%BB%81" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011%20x64-0078D6?logo=windows" alt="Windows 10/11 x64">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4" alt=".NET Framework 4.8">
  <img src="https://img.shields.io/badge/C%23-7.3%20%C2%B7%20WinForms-239120" alt="C# 7.3 WinForms">
</p>

<p align="center">
  <a href="docs/screenshots/health.png"><img src="docs/screenshots/health.png" width="860" alt="Tab Tổng quan của Tweek Pro"></a><br>
  <sub>Tab Tổng quan — <a href="#ảnh-các-tab">xem ảnh tất cả các tab</a></sub>
</p>

---

## Giới thiệu

Tweek Pro là ứng dụng Windows 64-bit, mã nguồn mở, kế thừa AppCare 0.6. Một cửa sổ, 16 tab, giao diện tiếng Việt (có tiếng Anh), không kernel driver, không telemetry, dữ liệu chỉ nằm trên máy.

Nguyên tắc thiết kế không đổi qua mọi phiên bản:

- **Xác nhận rồi mới làm.** Không có thao tác nào tự chạy; xóa thẳng luôn hỏi hai lần.
- **Sao lưu trước khi xóa.** Tệp, khóa Registry, mục khởi động, dịch vụ, gói Store… đều vào **Kho khôi phục** và bật lại được bằng một nút.
- **Hàng rào nằm trong mã, không trong cấu hình.** `System32`, `SysWOW64`, `WinSxS`, `Program Files`, Documents/Desktop/OneDrive, dịch vụ và tiến trình cốt lõi bị khóa cứng; không quy tắc JSON hay chế độ AI nào mở được.
- **Chỉ xem những gì không nên đụng.** Driver, tác vụ lịch, kết nối mạng: hiển thị đầy đủ, không sửa.

## Tải và cài đặt

| Tệp | Dùng khi | Ghi chú |
|---|---|---|
| **`TweekPro-<phiên bản>-Setup.exe`** | Dùng lâu dài | Cài vào `Program Files\Tweek Pro`, lối tắt Start Menu/Desktop, hiện trong *Ứng dụng đã cài*; gỡ cài đặt **không** xóa Kho khôi phục |
| `TweekPro-<phiên bản>-portable.zip` | Chạy thử, USB | Giải nén cả thư mục (exe + DLL) rồi chạy |

Tải bản mới nhất tại **[Releases](https://github.com/tuantien0001/TweekPro/releases/latest)**.

- **Yêu cầu:** Windows 10 (1903+) hoặc Windows 11, 64-bit — .NET Framework 4.8 đã có sẵn; bộ cài tự kiểm tra. Windows 7 SP1 / 8.1 64-bit chạy được sau khi cài [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) (bộ cài báo và mở trang tải), nhưng các phần dựa trên thành phần chỉ có ở Windows 10 tự lùi về mức thấp hơn: tab **Ứng dụng Windows** cần Appx (Windows 8+), **Dung lượng hệ thống** cần `DISM /AnalyzeComponentStore` và `Get-WindowsDriver` (Windows 8.1+), **Dấu vết** xóa cookie theo dòng cần `winsqlite3.dll` (Windows 10+; thiếu thì chuyển cả tệp vào Kho). Không có bản 32-bit. Chưa được kiểm thử thực tế trên Windows 7.
- **Máy yếu:** ứng dụng tiêu thụ khoảng 50 MB bộ nhớ riêng (≈90 MB working set) với đủ 22 tab; sau lần mở đầu, dung lượng thư mục cài được nhớ một ngày (`install-sizes.xml`) nên các lần mở sau chỉ tốn ≈3–4 giây CPU cho toàn bộ việc khởi động, đo và kiểm tra sức khỏe; các việc nền chạy ở mức ưu tiên thấp và mọi nút vẫn dùng được ngay khi danh sách ứng dụng hiện ra. Bộ cài chạy `ngen` để biên dịch sẵn mã máy, rút ngắn lần mở đầu trên CPU chậm.
- **Quyền quản trị:** ứng dụng luôn chạy quản trị (UAC hỏi một lần khi mở). Quyền này cần cho Rác hệ thống, HKLM, băng thông ETW, gỡ ứng dụng Windows cho mọi tài khoản và tệp cứng đầu. Có quyền cao không đồng nghĩa xóa được mọi thứ — các thư mục lõi vẫn bị khóa trong mã.
- **Cập nhật:** khi khởi động, ứng dụng hỏi GitHub Releases một lần (không gửi gì khác); có bản mới thì hiện dải thông báo trên tab **Tổng quan** với **Tải về** / **Bỏ qua bản này**. Tắt tự kiểm tra bằng `updateAutoCheck: false` trong `settings.json`; nút **Kiểm tra cập nhật** vẫn dùng được.
- **Dữ liệu:** `%LOCALAPPDATA%\TweekPro` — `Backups\` (Kho), `settings.json`, `sessions.xml`, `install-sizes.xml` (dung lượng thư mục cài đã đo, nhớ 24 giờ), `Logs\` (giữ 14 ngày), `junk-rules.json` tùy chọn. Lần chạy đầu tự đổi tên thư mục `AppCare` cũ sang `TweekPro`; kho AppCare vẫn đọc và khôi phục được.
- **Ngôn ngữ:** nút quả cầu **EN / VI** ở góc phải header đổi ngôn ngữ và khởi động lại (khóa `language` trong `settings.json`).

## Các tab

| Tab | Chức năng | An toàn |
|---|---|---|
| **Tổng quan** | Kiểm tra sức khỏe **tự chạy khi mở** (tắt được bằng «Tự kiểm tra khi mở»): điểm 0–100, hạng A–E, 7 khu vực (rác, phần còn sót, thư mục rỗng, Kho, khởi động, ổ trống, phần mềm không mong muốn), dung lượng giải phóng được; **vòng tròn dung lượng cho từng ổ đĩa cố định** (C:, D:, …) tô màu theo mức trống còn lại; bấm đúp mở tab xử lý; sao chép báo cáo; dải thông báo cập nhật | Chỉ đọc; khu vực không đo được không bị trừ điểm |
| **Ứng dụng** | Ứng dụng desktop (HKLM/HKCU), **dung lượng lớn nhất xếp trên** (bấm tiêu đề cột để xếp lại), gỡ theo hàng đợi, quét phần còn sót sau khi gỡ, cột **Cảnh báo** PUP/bloatware với lý do bằng lời thường, cột **Loại / Thư mục cài / Lệnh gỡ / Trang web / Ghi chú** như Revo, ô thông tin hiện lệnh gỡ im lặng và khóa Registry, lọc «Chỉ hiện mục cảnh báo», CSV, chuột phải (sao chép lệnh gỡ, mở trang web, mở Regedit); **Gỡ cưỡng bức…** cho chương trình không còn trong danh sách (nhập tên + thư mục/.exe → quét sâu → Phần còn sót). **Dung lượng** lấy từ bộ cài; thiếu thì đọc sổ Steam (`libraryfolders.vdf`, kể cả thư viện ở ổ khác) hoặc đo thư mục cài / thư mục trình gỡ | Không dùng Win32_Product; chặn lệnh gỡ qua cmd/PowerShell; nhà phát hành cốt lõi (Microsoft, Google, Mozilla, NVIDIA, Intel, AMD…) không bao giờ bị đánh dấu; không đo Program Files, Windows hay gốc ổ đĩa |
| **Ứng dụng Windows** | Gói Appx/MSIX cài sẵn và Microsoft Store với logo thật; cột **Dung lượng** đo từ thư mục gói (game Xbox chuyển sang ổ khác được theo dõi qua liên kết `WindowsApps`); nhóm Gỡ được / Cần cân nhắc / Được bảo vệ; gỡ cho tài khoản hiện tại hoặc mọi tài khoản; mở trang Store, thư mục gói | Framework, gói `NonRemovable`, Start/Search/Settings/Windows Security/Edge và `Microsoft.Windows.*` bị khóa trong mã; Store/App Installer/Photos hỏi hai lần; Kho `Kind=Store` đăng ký lại từ thư mục gói |
| **Phần còn sót** | Kết quả quét nhanh/sâu chờ duyệt; xem, chọn, xóa có sao lưu; hẹn xóa khi khởi động lại cho tệp bị giữ. Quét sâu dò thêm **dấu vết mở rộng** (`TraceHunter`): tên biến thể của ứng dụng, MUICache, AppCompat, FeatureUsage, quy tắc Firewall, SharedDLLs, StartupApproved (dọn được qua Kho) và đăng ký kiểu tệp/COM, RegisteredApplications, PATH, Event Log, Windows Installer, Tracing, CrashDumps, WER, Prefetch, Gần đây, Start Menu, thư mục tài liệu (Chỉ xem) | Từ chối dọn khi ứng dụng còn đăng ký; mục Chỉ xem không tích được; chỉ các vị trí giá trị trong danh sách cho phép mới xóa được; tên ngắn/chung chỉ vào Chỉ xem |
| **Dọn rác** | Xem trước theo quy tắc (Temp, CrashDumps, WER, cache Explorer/GPU/trình duyệt, Rác hệ thống) với số tệp và dung lượng; vào Kho (mặc định) hoặc **Xóa thẳng** | Bỏ qua tệp < 24 giờ và tệp đang mở; cache trình duyệt khóa khi trình duyệt còn chạy; `JunkSafety` chặn Documents/Desktop/Downloads/OneDrive và mọi thư mục Windows lõi |
| **Thư mục rỗng** | Quét một thư mục, liệt kê nhánh hoàn toàn rỗng, xóa một lần | Kiểm tra rỗng lại ngay trước khi xóa; từ chối gốc ổ đĩa, Windows, Program Files |
| **Tệp trùng lặp** | Gom theo kích thước rồi SHA-256; chọn giữ bản cũ nhất/mới nhất; bản dư vào Kho hoặc xóa thẳng | Bản giữ lại không bao giờ bị đụng; băm lại trước khi xóa thẳng; `DupeSafety` |
| **Tệp tải về cũ** | Downloads (và Desktop) — bộ cài, tệp nén, ảnh đĩa, tệp tải dở, tệp lớn cũ hơn N ngày (mặc định 30); vào Kho (`Kind=Stale`) hoặc xóa thẳng | `StaleSafety` chỉ cho Downloads/Desktop của tài khoản hiện tại; bỏ qua thư mục đám mây, shortcut, tệp ẩn/hệ thống, tệp đang mở |
| **Dấu vết** | 26 loại dấu vết riêng tư: Windows (Gần đây, Jump Lists, Activity History, lịch sử PowerShell, Remote Desktop), Explorer (Run, TypedPaths, RecentDocs, Mở/Lưu, tìm kiếm, ổ mạng ánh xạ, RecentApps, FeatureUsage, UserAssist, ShellBags), ứng dụng (Paint/WordPad, Regedit LastKey, Office File/Place MRU + shortcut Recent, Acrobat), trình duyệt (lịch sử, phiên/tab, cookie của Chrome/Edge/Brave; lịch sử giữ dấu trang, biểu mẫu, cookie của Firefox). **Cookie giữ lại** (danh sách tên miền — chỉ xóa cookie ngoài danh sách bằng SQL qua `winsqlite3.dll`), **Dọn khi thoát Tweek Pro** | Tệp vào Kho (`Kind=Tracks`); cơ sở dữ liệu SQLite được sao chép vào Kho trước khi xóa theo dòng và chép lại nguyên vẹn khi khôi phục; giá trị Registry lưu nguyên cây (hoặc chỉ các giá trị được nêu tên, ví dụ `LastKey` để giữ Favorites) và khôi phục kiểu gộp; `TracksSafety` chỉ cho các khóa HKCU và thư mục trong danh sách (mẫu `*` khớp đúng một cấp); quy tắc trình duyệt khóa khi trình duyệt còn chạy |
| **Registry** | Mục Registry mồ côi trỏ tới tệp/DLL không còn tồn tại: khóa Uninstall, App Paths, Applications, MUICache, SharedDLLs, Shell Extensions Approved; nhóm theo loại, mở Regedit tại khóa | Không «tối ưu» Registry, không đụng CLSID/Services/lớp tệp; `RegCleanSafety` chỉ cho đúng các khóa cha trong danh mục; mỗi lần dọn vào Kho (`Kind=Registry`), khôi phục chỉ ghi lại mục chưa tồn tại |
| **Phân tích ổ đĩa** | Thư mục con nặng nhất, dung lượng theo phần mở rộng, tệp lớn nhất; mở vị trí, CSV | Hoàn toàn chỉ đọc; giới hạn 1.000.000 tệp / 120 giây |
| **Dung lượng hệ thống** | Đo Windows.old, WinSxS, bộ đệm Delivery Optimization, driver cũ trùng lặp trong DriverStore, **gói MSI/MSP mồ côi trong Windows\Installer**, hiberfil.sys, pagefile.sys, dung lượng điểm khôi phục, Thùng rác; cột **Ước tính thu hồi** cho từng mục (WinSxS lấy từ `DISM /AnalyzeComponentStore`); đo và chạy công cụ ở luồng nền với tiến độ trực tiếp (kể cả % của DISM), nút Dừng; mỗi mục giải phóng bằng đúng công cụ Windows (cleanmgr sageset, `DISM /StartComponentCleanup`, `pnputil /delete-driver` không `/force`, `powercfg -h off`, cmdlet) | Không tự xóa tệp trong các vùng Windows và **không qua Kho** (quá lớn / không hoàn tác được); riêng gói Installer mồ côi (không còn `LocalPackage` nào tham chiếu, cũ hơn 1 ngày, chỉ tệp cấp gốc) được **chuyển vào Kho** (`Kind=Installer`) và khôi phục được; mọi lệnh hỏi xác nhận và cần quyền quản trị; kết quả công cụ ghi vào nhật ký |
| **Kho khôi phục** | Khôi phục mục đã chọn; xóa vĩnh viễn mục đánh dấu; **Dọn kho theo tuổi…** có xem trước | Xóa vĩnh viễn không hoàn tác; mặc định chỉ xóa bản đã khôi phục |
| **Khởi động** | Ba nhóm, mỗi dòng một ô tích: **Run / Startup**, **Ứng dụng Windows** (Copilot, Terminal, Teams, Defender…), **Dịch vụ tự chạy** (không gồm driver). Bỏ tích = tắt qua Kho; tích lại = bật. Run mà Windows đã tắt thì chỉ bật cờ StartupApproved | Không dừng tiến trình/dịch vụ đang chạy; dịch vụ cốt lõi không tắt được |
| **Tiện ích trình duyệt** | Liệt kê tiện ích của Chrome, Edge, Brave, Vivaldi, Opera, Firefox trong mọi hồ sơ: trạng thái bật/tắt, nguồn cài (cửa hàng / ép cài bằng chính sách / giải nén / cài ngoài), quyền rộng (`<all_urls>`, cookies, webRequest, history…), ngày cài, phiên bản; **Gỡ tiện ích đã chọn (vào Kho)** khi trình duyệt đã đóng (có nút đóng giúp); **Dọn Forcelist** xóa mọi giá trị `ExtensionInstallForcelist` (HKLM + HKCU, cả hai view) để trình duyệt tự gỡ tiện ích bị ép cài; mở thư mục tiện ích, mở trang quản lý của trình duyệt, CSV | Gỡ chỉ chạy khi trình duyệt đã đóng (nếu không nó ghi lại cấu hình cũ khi thoát). Chromium: thư mục tiện ích, mục `extensions.settings` + MAC toàn vẹn + ghim thanh công cụ trong `Preferences`/`Secure Preferences`, khóa nguồn cài `Software\…\Extensions\<id>` và giá trị Forcelist đều vào Kho (`Kind=Extension`) rồi mới xóa; Firefox: `.xpi` chuyển ra Kho, Firefox tự cập nhật `extensions.json` ở lần mở sau. Từ chối tiện ích thành phần, add-on cài từ registry/hệ thống và mọi thứ ngoài thư mục hồ sơ; khôi phục ghép lại mục cấu hình nếu chưa tồn tại |
| **Explorer** | Hai khung chuyển bằng nút ở đầu tab. **Tùy chỉnh File Explorer** (17 công tắc HKCU: hiện đuôi tệp, tệp ẩn, tệp hệ thống được bảo vệ, hộp chọn, thanh trạng thái, chế độ gọn, đường dẫn đầy đủ, mở vào This PC, tệp gần đây / thường dùng, quảng cáo OneDrive, giây trên đồng hồ, căn trái thanh tác vụ, Task View, Widgets) với nút Mặc định Windows / Khuyến nghị an toàn / Khởi động lại Explorer; **Duyệt tệp** hiện mọi tệp ẩn và hệ thống bất kể thiết lập Explorer với cột Đuôi, Mô tả (loại), Dung lượng, Sửa lần cuối, Thuộc tính (ẩn nghiêng, hệ thống vàng, giả dạng đỏ), lọc «Chỉ hiện mục ẩn / hệ thống»; **Xem chi tiết tệp** (bấm một tệp, chọn, kéo thả hoặc dán đường dẫn): loại thật, thuộc tính ẩn/hệ thống, thời gian, chủ sở hữu, chữ ký số, thông tin phiên bản, kiến trúc PE, Mark of the Web, SHA-256/MD5, cảnh báo đuôi kép (`hoadon.pdf.exe`), ký tự đảo chiều Unicode, PE giả tài liệu; mở hộp Thuộc tính Windows; **Xóa không phục hồi…** (shredder) cho tệp/thư mục đã chọn: liệt kê trước, hộp xác nhận phải tích «Tôi hiểu», ghi đè 1 lần ngẫu nhiên hoặc 3 lần (0x00/0xFF/ngẫu nhiên), đổi tên ngẫu nhiên rồi xóa | `ExplorerSafety` chỉ cho ghi đúng các giá trị trong danh mục dưới `HKCU\…\Explorer`; giá trị cũ vào Kho (`Kind=Explorer`) và khôi phục bằng một nút; xem chi tiết tệp hoàn toàn chỉ đọc. Shredder **không qua Kho**; `ShredSafety` từ chối gốc ổ đĩa, Windows, Program Files, ProgramData\Microsoft, thư mục hồ sơ, Kho khôi phục, chính Tweek Pro, đường dẫn UNC và mọi liên kết symlink/junction (không bao giờ đi xuyên liên kết, chỉ gỡ liên kết) |
| **Mạng** | Kết nối TCP/UDP theo ứng dụng, icon và hướng gửi/nhận, thanh tốc độ, dải packet realtime; băng thông ETW tự bật khi quản trị; chuột phải kết thúc tiến trình / dừng dịch vụ / **Chặn mạng** (cặp quy tắc Windows Firewall vào + ra cho đúng tệp .exe, cột Chặn mạng đỏ, Bỏ chặn, nút «Đang chặn mạng…» liệt kê và bỏ chặn hàng loạt) | Không driver, không lọc gói; `ProcessControl` khóa tiến trình và dịch vụ cốt lõi; `FirewallBlock` từ chối tiến trình cốt lõi, `System32`, chính Tweek Pro, tệp không phải .exe; mỗi lần chặn vào Kho (`Kind=Firewall`), khôi phục gỡ đúng quy tắc theo tên; phiên ETW luôn dừng khi đóng |
| **Dịch vụ hệ thống** | Mọi dịch vụ với **giải thích bằng lời thường** (~180 dịch vụ + mẫu tên), huy hiệu an toàn, PID/RAM, nhà phát hành; lọc; Dừng / Khởi động / **Dừng và vô hiệu hóa (lưu Kho)** | Dịch vụ cốt lõi bị khóa; svchost không cho kết thúc; kiểu khởi động cũ lưu `Kind=Service` |
| **Công cụ** | Lối mở công cụ Windows với icon hệ thống thật từ .exe/.cpl/.msc và cột dòng lệnh; Kiểm tra cập nhật | SFC/chkdsk hỏi xác nhận |
| **Nhật ký** | Nhật ký phiên, mở thư mục nhật ký/dữ liệu, lưu cài đặt | Xoay vòng theo ngày |
| **Trợ lý AI** | Claude / OpenAI / LM Studio / Ollama; chat có gọi công cụ (14 công cụ: 9 chỉ đọc, 5 thay đổi); khóa API mã hóa DPAPI | Mặc định **Hỏi xác nhận**; Chỉ đọc / Tự động do người dùng chọn; hàng rào engine luôn giữ nguyên |

### Ảnh các tab

Ảnh kết xuất từ chính ứng dụng bằng `--preview all` (dữ liệu mẫu, 1280×820); bấm vào ảnh để xem cỡ lớn.

<table>
<tr>
<td width="50%"><b>Tổng quan</b> — điểm sức khỏe, 7 khu vực, banner bản mới<br><a href="docs/screenshots/health.png"><img src="docs/screenshots/health.png" alt="Tổng quan"></a></td>
<td width="50%"><b>Ứng dụng</b> — kho ứng dụng, cột Cảnh báo PUP, hàng đợi gỡ<br><a href="docs/screenshots/apps.png"><img src="docs/screenshots/apps.png" alt="Ứng dụng"></a></td>
</tr>
<tr>
<td><b>Ứng dụng Windows</b> — Appx theo phân loại an toàn<br><a href="docs/screenshots/store.png"><img src="docs/screenshots/store.png" alt="Ứng dụng Windows"></a></td>
<td><b>Phần còn sót</b> — kết quả quét chờ duyệt<br><a href="docs/screenshots/remnants.png"><img src="docs/screenshots/remnants.png" alt="Phần còn sót"></a></td>
</tr>
<tr>
<td><b>Dọn rác</b> — quy tắc theo nhóm, xem trước rồi dọn<br><a href="docs/screenshots/junk.png"><img src="docs/screenshots/junk.png" alt="Dọn rác"></a></td>
<td><b>Thư mục rỗng</b><br><a href="docs/screenshots/empty.png"><img src="docs/screenshots/empty.png" alt="Thư mục rỗng"></a></td>
</tr>
<tr>
<td><b>Tệp trùng lặp</b><br><a href="docs/screenshots/dupes.png"><img src="docs/screenshots/dupes.png" alt="Tệp trùng lặp"></a></td>
<td><b>Tệp tải về cũ</b> — bộ cài, ảnh, tệp dở trong Downloads<br><a href="docs/screenshots/stale.png"><img src="docs/screenshots/stale.png" alt="Tệp tải về cũ"></a></td>
</tr>
<tr>
<td><b>Dấu vết</b> — dấu vết riêng tư Windows / trình duyệt, khôi phục kiểu gộp<br><a href="docs/screenshots/tracks.png"><img src="docs/screenshots/tracks.png" alt="Dấu vết"></a></td>
<td><b>Registry</b> — mục mồ côi trỏ tới tệp không còn, vào Kho<br><a href="docs/screenshots/registry.png"><img src="docs/screenshots/registry.png" alt="Registry"></a></td>
</tr>
<tr>
<td><b>Phân tích ổ đĩa</b><br><a href="docs/screenshots/analyzer.png"><img src="docs/screenshots/analyzer.png" alt="Phân tích ổ đĩa"></a></td>
<td><b>Dung lượng hệ thống</b> — Windows.old, WinSxS, driver cũ, hiberfil… bằng công cụ Windows<br><a href="docs/screenshots/space.png"><img src="docs/screenshots/space.png" alt="Dung lượng hệ thống"></a></td>
</tr>
<tr>
<td><b>Kho khôi phục</b> — mọi thao tác xóa đều quay lại được<br><a href="docs/screenshots/vault.png"><img src="docs/screenshots/vault.png" alt="Kho khôi phục"></a></td>
<td><b>Khởi động</b> — ô tích, ứng dụng Windows, dịch vụ tự chạy<br><a href="docs/screenshots/autorun.png"><img src="docs/screenshots/autorun.png" alt="Khởi động"></a></td>
</tr>
<tr>
<td><b>Tiện ích trình duyệt</b> — nguồn cài, quyền rộng, ép cài bằng chính sách<br><a href="docs/screenshots/extensions.png"><img src="docs/screenshots/extensions.png" alt="Tiện ích trình duyệt"></a></td>
<td><b>Explorer — Duyệt tệp và chi tiết tệp</b> — mọi tệp ẩn / hệ thống, đuôi, mô tả, cảnh báo giả dạng, Xóa không phục hồi<br><a href="docs/screenshots/explorer.png"><img src="docs/screenshots/explorer.png" alt="Explorer – duyệt tệp"></a></td>
</tr>
<tr>
<td><b>Explorer — Tùy chỉnh File Explorer</b> — 17 công tắc HKCU, khôi phục được<br><a href="docs/screenshots/tweaks.png"><img src="docs/screenshots/tweaks.png" alt="Explorer – tùy chỉnh"></a></td>
<td><b>Mạng</b> — ứng dụng đang gửi/nhận, chặn mạng<br><a href="docs/screenshots/network.png"><img src="docs/screenshots/network.png" alt="Mạng"></a></td>
</tr>
<tr>
<td><b>Dịch vụ hệ thống</b> — danh mục dịch vụ có giải thích<br><a href="docs/screenshots/services.png"><img src="docs/screenshots/services.png" alt="Dịch vụ hệ thống"></a></td>
<td><b>Công cụ</b> — công cụ Windows với icon thật<br><a href="docs/screenshots/tools.png"><img src="docs/screenshots/tools.png" alt="Công cụ"></a></td>
</tr>
<tr>
<td><b>Nhật ký</b><br><a href="docs/screenshots/logs.png"><img src="docs/screenshots/logs.png" alt="Nhật ký"></a></td>
<td><b>Trợ lý AI</b> — chat có gọi công cụ, chế độ Hỏi xác nhận<br><a href="docs/screenshots/ai.png"><img src="docs/screenshots/ai.png" alt="Trợ lý AI"></a></td>
</tr>
</table>

## Kho khôi phục

Mỗi thao tác phá hủy tạo một bản ghi `Backup` với `Kind` (Folder, File, Registry, RegistryValue, Junk, Duplicate, Stale, Store, Service) và `State`: `Pending → BackedUp / NeedsReview / Restored / PendingReboot`. Khôi phục không ghi đè đích đã tồn tại. Dung lượng chỉ thật sự được giải phóng khi **xóa vĩnh viễn** trong Kho hoặc khi bạn chọn **Xóa thẳng**.

**Tệp cứng đầu** (`Core/StubbornFiles`): khi Windows từ chối, Tweek Pro leo thang theo bậc — bỏ thuộc tính chỉ đọc/ẩn/hệ thống → chiếm quyền sở hữu (Administrators) và cấp toàn quyền → thử lại. Tệp đang bị tiến trình khác giữ thì hỏi một lần để **hẹn xóa khi khởi động lại** (`MoveFileEx`), mục này hiện trong Kho với trạng thái `PendingReboot`.

## Chi tiết kỹ thuật

<details>
<summary><b>Dọn rác: quy tắc và giới hạn</b></summary>

Quy tắc là JSON nhúng trong exe (`Cleaner/junk-rules.json`); đặt bản sửa tại `%LOCALAPPDATA%\TweekPro\junk-rules.json` để ghi đè (nút **Nạp lại quy tắc**). Mặc định: `%TEMP%`, `C:\Windows\Temp`, CrashDumps, WER ReportQueue/ReportArchive, thumbcache/iconcache, INetCache, cache shader GPU (D3DSCache, NVIDIA, AMD), cache Chrome/Edge/Brave/Firefox.

Nhóm **Rác hệ thống (cần quản trị)**: gói Windows Update đã tải (`SoftwareDistribution\Download`, cũ hơn 7 ngày, khóa khi TiWorker/TrustedInstaller chạy), nhật ký CBS, dump kernel (`Minidump`, `LiveKernelReports`), nhật ký Panther, Downloaded Program Files.

`JunkSafety` từ chối trước khi đếm bất kỳ tệp nào: một quy tắc chỉ được trỏ vào vùng rác cho phép hoặc thư mục hồ sơ trình duyệt có thư mục cache. `System32`, `SysWOW64`, `WinSxS`, `servicing`, `Boot`, `Fonts`, `Installer`, Program Files, gốc `C:\Windows`, Documents/Desktop/Pictures/Music/Videos/Downloads/OneDrive và dữ liệu Tweek Pro luôn bị chặn — có test chứng minh. Mỗi quy tắc dừng ở 100.000 tệp / 30 giây và báo "chưa đủ".
</details>

<details>
<summary><b>Tổng quan: cách tính điểm</b></summary>

Bảy phép đo chỉ đọc chạy trên luồng nền → `Health/HealthCheck.Evaluate` (hàm thuần, không phụ thuộc Windows). Mỗi khu vực nhận mức **Tốt / Nên xem / Cần dọn / Khẩn** theo ngưỡng cố định (rác ≥100 MB / ≥500 MB / ≥2 GB; ổ trống <20 % / <10 % / <5 %; PUP 1–2 / 3+ / có mục nên gỡ). Điểm bắt đầu 100, trừ 5/12/25 theo mức; hạng A ≥90, B ≥75, C ≥60, D ≥40, còn lại E. Khu vực không đo được hiện "Chưa đo" và không bị trừ. Thẻ điểm vẽ bằng GDI+ (`HealthRenderer`).
</details>

<details>
<summary><b>Mạng: nguồn dữ liệu</b></summary>

- Bảng kết nối: `GetExtendedTcpTable` / `GetExtendedUdpTable` (iphlpapi, IPv4 + IPv6), không cần quyền. Tên tiến trình qua `QueryFullProcessImageName`; nhà phát hành từ chữ ký Authenticode (cache).
- Băng thông: phiên ETW realtime trên nhà cung cấp TCP/IP kernel (cùng nguồn với Task Manager) qua `Microsoft.Diagnostics.Tracing.TraceEvent`; cộng theo PID trên luồng riêng; tự bật khi quản trị (`networkStartBandwidthWhenElevated`). Phiên `TweekPro-Network` luôn được dừng khi đóng.
- Hướng gửi/nhận suy từ tốc độ theo tiến trình (ngưỡng 16 B/s) hoặc trạng thái kết nối; dải packet (`PacketAnimator`) tất định, có self-test. Phân giải tên máy ngược là tùy chọn, bất đồng bộ, có cache.
- Chuột phải: kết thúc tiến trình; với tiến trình chứa dịch vụ (WMI `Win32_Service`) — dừng hoặc dừng và vô hiệu hóa từng dịch vụ. `ProcessControl` chặn System/smss/csrss/wininit/winlogon/services/lsass/svchost/dwm/MsMpEng… và các dịch vụ RpcSs, DcomLaunch, Winmgmt, EventLog, Dnscache, WinDefend, TrustedInstaller…
</details>

<details>
<summary><b>Dịch vụ hệ thống: cơ sở kiến thức</b></summary>

Đọc qua WMI `Win32_Service`; RAM từ `Process.WorkingSet64`; nhà phát hành từ chữ ký số. Giải thích lấy từ `Services/service-knowledge.json` (nhúng, khớp tên bỏ hậu tố `_xxxxx`), rồi mẫu tên (`update`, `elevation`, `anticheat`, `license`, `telemetry`, `helper`, `sql`, `vpn`), cuối cùng suy từ đường dẫn. Mức an toàn: `Core` (luôn khóa), `Windows`, `Optional`, `ThirdParty`. Dịch vụ ký bởi Microsoft xếp vào Windows.
</details>

<details>
<summary><b>Trợ lý AI và Local AI (LM Studio / Ollama)</b></summary>

- `AI/AiClient`: Anthropic Messages API và OpenAI Chat Completions (tool calling), chỉ endpoint HTTPS trừ localhost/LAN; model mặc định `claude-sonnet-4-5` / `gpt-4.1` (sửa được). Transport tách rời nên toàn bộ dựng/đọc yêu cầu được kiểm thử ngoài mạng.
- `AI/AiAgent`: vòng hỏi → gọi công cụ → trả kết quả, tối đa 8 vòng. Công cụ thay đổi đi qua `IAiHost.ConfirmAction`; **Chỉ đọc** chặn chúng, **Tự động** chạy không hỏi lại nhưng vẫn qua Kho và hàng rào engine.
- Khóa API lưu `dpapi:<base64>` trong `settings.json` (phạm vi người dùng hiện tại).
- **Local AI:** mở LM Studio hoặc Ollama và nạp một model chat hỗ trợ tool calling → trong tab chọn **LM Studio (local)** (`http://localhost:1234/v1/chat/completions`) hoặc **Ollama (local)** (`http://localhost:11434/v1/chat/completions`) → **Tải danh sách model** → **Lưu cấu hình** → **Kiểm tra kết nối**. Không cần API key nếu máy chủ không yêu cầu. Model không hỗ trợ công cụ chỉ trả lời hướng dẫn. Chờ tối đa 10 phút mỗi yêu cầu.
</details>

## Dòng lệnh

| Lệnh | Tác dụng |
|---|---|
| `TweekPro-<phiên bản>.exe` | Mở ứng dụng (yêu cầu UAC) |
| `--self-test` | Toàn bộ kiểm thử trên Windows (tạo và dọn fixture `TweekProTest*` trong AppData/HKCU) |
| `--self-test junk` | Mọi bộ kiểm thử không phụ thuộc Registry/UI — chạy được headless và trên Mono |
| `--self-test core` | Chỉ `CoreTests` |
| `--preview <remnants\|apps\|stale\|explorer\|tweaks\|autorun\|tools\|junk\|network\|health\|store\|services\|ai> [--show]` | Kết xuất `TweekPro-preview.png` (hoặc mở cửa sổ) với dữ liệu mẫu |
| `--preview all [thư_mục] [--en]` | Kết xuất ảnh mọi tab (1280×820, tiếng Việt; `--en` để lấy bản tiếng Anh) vào thư mục — dùng để làm mới `docs/screenshots/` |
| `--export-icon <tệp.ico>` | Xuất icon đa kích cỡ đã duyệt (11 khung) |

## Dành cho nhà phát triển

**Build:** cần .NET SDK 6+ (khuyến nghị 8; `winget install Microsoft.DotNet.SDK.8`).

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -SelfTest   # build Release + self-test (PowerShell quản trị)
dotnet build -c Release -p:EnableWindowsTargeting=true            # Linux/macOS: chỉ kiểm tra biên dịch
mono bin/Release/net48/TweekPro-<phiên bản>.exe --self-test junk # Linux/macOS: self-test headless
```

Build phải giữ **0 lỗi / 0 cảnh báo**. Mỗi thay đổi engine cần test trong `*Tests.cs` tương ứng và nối vào `Tests07.Run()`.

**Phát hành** (PowerShell quản trị, cây làm việc sạch):

```powershell
powershell -ExecutionPolicy Bypass -File .\release.ps1 -Notes "Mô tả ngắn"   # tự tăng 0.7.n → 0.7.n+1
powershell -ExecutionPolicy Bypass -File .\release.ps1 -Version 0.8 -DryRun  # nhảy minor, chỉ kiểm tra
```

Script ghi phiên bản mới vào `TweekPro.csproj`, `App.cs`, `build.ps1`, `installer\TweekPro.iss` (giữ UTF-8/BOM), **cập nhật mục Lịch sử phiên bản trong README** (đổi tiêu đề `### 0.7.n (chưa phát hành)` thành số bản + ngày, thêm `-Notes` làm dòng đầu; chưa có mục chờ thì tạo mới), chạy `build.ps1 -SelfTest`, commit «Release vX.Y.Z», gắn tag với nội dung changelog và push; nếu build/test lỗi thì hoàn nguyên. GitHub Actions (`.github/workflows/release.yml`) nhận tag, build trên `windows-latest`, đính kèm Setup + zip và xuất bản Release với phần mô tả lấy từ chính mục changelog đó — vài phút sau ứng dụng báo bản mới. Đóng gói thủ công: `publish.ps1` (cần [Inno Setup 6.3+](https://jrsoftware.org/isinfo.php), `-InstallInno` để cài qua winget).

**Cấu trúc mã** — mỗi tính năng một thư mục: `XyzEngine.cs` (logic thuần, không WinForms) · `XyzSafety` (hàng rào) · `XyzUI.cs` (`partial class MainForm`) · `XyzTests.cs`.

| Thư mục / tệp | Nội dung |
|---|---|
| `App.cs`, `Engine.cs`, `ScanWindow.cs` | Cửa sổ chính, `Program.Main`, lõi inventory/quét/Kho, cửa sổ quét phần còn sót |
| `Core/` | `Paths`, `Settings`, `Log`, `L` + `LangEn` (song ngữ), `StubbornFiles`, `Elevation` |
| `Cleaner/`, `Dupes/`, `Stale/`, `Analyzer/` | Dọn rác + thư mục rỗng, tệp trùng lặp, tệp tải về cũ, phân tích ổ đĩa |
| `Health/`, `Pup/`, `Startup/` | Tổng quan (điểm/hạng), phát hiện PUP (`pup-rules.json`), Startup Insight |
| `Store/`, `Services/`, `Network/` | Ứng dụng Windows (Appx), Dịch vụ (`service-knowledge.json`), Mạng (iphlpapi, ETW, packet flow) |
| `AI/`, `Update/`, `Vault/` | Trợ lý AI, kiểm tra cập nhật GitHub, hộp thoại dọn kho |
| `Advanced*.cs`, `Presentation.cs`, `Branding.cs`, `TweekPro.ico` | Autorun + Windows Tools, Theme, icon vector từng tab, logo T + PC |
| `CoreTests.cs`, `Tests07.cs`, `*Tests.cs` | Kiểm thử; `--self-test` chạy trước khi khởi tạo WinForms |
| `build.ps1`, `release.ps1`, `publish.ps1`, `installer/`, `.github/workflows/` | Build, phát hành, đóng gói, CI |

Tài liệu kèm theo: `AGENTS.md` (quy ước cho agent), `HANDOFF.md` (kiến trúc, lịch sử), `WORKSTATE.md` (checkpoint hiện tại), `TEST-PLAN.md` (kiểm thử trên máy thật), `SCAN-GUIDE.md` (phạm vi quét phần còn sót).

## Báo lỗi và góp ý

Mọi phản hồi theo dõi công khai tại [GitHub Issues](https://github.com/tuantien0001/TweekPro/issues) (cần tài khoản GitHub miễn phí). Có ba mẫu:

| Mẫu | Dùng khi | Nhãn |
| --- | --- | --- |
| [🐞 Báo lỗi](https://github.com/tuantien0001/TweekPro/issues/new?template=bug_report.yml) | Ứng dụng chạy sai, treo, báo lỗi, xóa/khôi phục không đúng | `bug` |
| [💡 Đề xuất tính năng](https://github.com/tuantien0001/TweekPro/issues/new?template=feature_request.yml) | Muốn Tweek Pro làm thêm hoặc làm khác đi | `enhancement` |
| [⭐ Đánh giá / góp ý](https://github.com/tuantien0001/TweekPro/issues/new?template=feedback.yml) | Chấm sao, điểm thích, điểm cần cải thiện | `feedback` |

Trong ứng dụng, nút **Báo lỗi / góp ý** (tab Tổng quan và Công cụ) mở đúng mẫu với phiên bản Tweek Pro, phiên bản Windows và tab đang mở đã điền sẵn; mục «Mở thư mục nhật ký để đính kèm» dẫn tới `%LOCALAPPDATA%\TweekPro\Logs` để kéo tệp `tweekpro-yyyyMMdd.log` vào issue. Không dán mật khẩu hay khóa bản quyền vào issue.

## Giới hạn hiện tại

Chưa có theo dõi cài đặt (install monitor), bật/tắt tiện ích trình duyệt tại chỗ (chỉ gỡ), hay tự tải và cài bản mới im lặng (Kiểm tra cập nhật chỉ báo và mở trang tải). Không xác định được mọi dấu vết của mọi ứng dụng. Băng thông theo tiến trình chỉ tính từ khi bật ETW. Bản phân phối là một thư mục (exe + DLL). Chuỗi giao diện nằm trong mã (chưa `.resx`); một số thông báo nhật ký còn tiếng Việt khi chọn tiếng Anh.

## Lịch sử phiên bản

### 0.7.9 (chưa phát hành)

- **Theo dõi cài đặt**: trên tab Ứng dụng, bấm một lần để ghi danh sách hiện có, cài phần mềm, bấm lần nữa để thấy ứng dụng mới và có thể quét phần còn sót.
- **Lịch sử gỡ**: mỗi lần gỡ xong được ghi vào thư mục dữ liệu. Nút **Lịch sử gỡ** trên tab Nhật ký xem 30 lần gần nhất.
- **Tài khoản khác**: quét phần còn sót cũng nhìn thư mục cùng tên trong AppData của tài khoản Windows khác (bỏ qua Public, Default và tài khoản đang dùng). Chỉ xem, không tự xóa, không mở file registry của tài khoản đó.
- **Ứng dụng Windows**: chuột phải thêm **Đặt lại dữ liệu ứng dụng** (mất dữ liệu, không vào Kho) và **Kết thúc ứng dụng**. Thành phần được bảo vệ bị từ chối.
- **Tiện ích**: danh sách có ô chọn để gỡ hàng loạt; nếu chưa đánh dấu thì dùng dòng đang chọn.
- **Giao diện tối**, **xuất / nhập cài đặt** (file xuất không chứa khóa AI) trên tab Nhật ký. Đổi giao diện cần khởi động lại.
- **Cài bản mới**: ô «Hỏi cài bản mới» (tắt mặc định). Khi bật, banner cập nhật có nút tải đúng bộ cài trên GitHub của Tweek Pro rồi chạy cài đặt im lặng. Ứng dụng không tự ghi đè file đang chạy.
- **Công cụ**: thêm DISM RestoreHealth, xóa bộ nhớ DNS, DirectX Diagnostic, và **Thêm công cụ…** (chỉ tệp .exe/.msc/.cpl trên máy; từ chối cmd, PowerShell và script).
- **Hunter**: kéo chữ thập thả lên một cửa sổ để chọn ứng dụng đã cài khớp tệp đó.
- **Báo lỗi / góp ý**: menu hiện ngay lần bấm đầu.

### 0.7.8 (25/09/2026)

- **Tổng quan dễ dùng hơn**: ngay dưới điểm số có tối đa 3 nút, việc nhiều GB nhất đứng trước. Nút **Dọn rác …** chuyển đúng các nhóm rác không bị khóa vào Kho (có hỏi trước, lấy lại được). Các nút còn lại mở đúng tab. Bảng xếp rác lớn lên đầu và tô nền theo mức. Thẻ điểm ghi một câu so với lần kiểm tra trước (điểm và dung lượng). Ô «Tự kiểm tra khi mở» nằm cùng hàng nút.
- **Ô tìm kiếm**: tab Ứng dụng và Dịch vụ hệ thống bỏ nhãn «Tìm kiếm» bên cạnh ô; chữ đó hiện mờ bên trong ô khi ô đang trống và biến mất khi gõ. Ô cao bằng nút trên thanh (36 px), mép trái/phải thẳng hàng với hàng nút, viền xanh khi đang gõ. Ô trống vẫn hiện mọi dòng.

### 0.7.7 (22/09/2026)

- **Báo lỗi và góp ý qua GitHub Issues**: ba mẫu issue (`.github/ISSUE_TEMPLATE`: Báo lỗi, Đề xuất tính năng, Đánh giá / góp ý — tiếng Việt, có nhãn `bug`/`enhancement`/`feedback`, chọn tab liên quan, chấm sao). Nút **Báo lỗi / góp ý** ở tab Tổng quan và Công cụ mở mẫu tương ứng với phiên bản Tweek Pro, phiên bản Windows (đọc `ProductName`/`DisplayVersion`/`CurrentBuild`, build ≥ 22000 ghi Windows 11) và tab đang mở đã điền sẵn; thêm lối «Xem phản hồi đã gửi» và «Mở thư mục nhật ký để đính kèm». Liên kết được cắt dưới 7000 ký tự để trình duyệt chấp nhận. README có mục «Báo lỗi và góp ý». Kiểm thử: mã hóa UTF-8 tham số, bỏ trường trống, cắt nội dung dài kèm dấu «…», ghép nhãn Windows.
- **Ứng dụng**: danh sách mặc định xếp **dung lượng lớn nhất lên đầu** (tự cập nhật khi đo xong thư mục); bấm tiêu đề cột để đổi cách xếp (bấm lại để đảo chiều). Thêm 5 cột giống Revo: **Loại** (32/64-bit, MSI), **Thư mục cài**, **Lệnh gỡ** (UninstallString), **Trang web** (URLInfoAbout/HelpLink), **Ghi chú** (Comments). Ô thông tin bên dưới hiện đủ lệnh gỡ, lệnh gỡ im lặng (QuietUninstallString), khóa Registry (kể cả WOW6432Node), trang web, ghi chú. Chuột phải thêm **Sao chép lệnh gỡ im lặng**, **Mở trang web nhà phát hành** (chỉ nhận http/https) và **Mở khóa Registry trong Regedit**. CSV xuất thêm Type, InstallDate, QuietUninstallCommand, Website, Comments, RegistryKey và theo đúng thứ tự đang xem; ô tìm kiếm lọc cả phần ghi chú.
- **Tổng quan tự kiểm tra khi mở**: sau khi đọc xong danh sách ứng dụng, Tweek Pro tự chạy kiểm tra sức khỏe (chỉ đọc) nên điểm số và các khu vực cần chú ý hiện ra ngay, không cần bấm Kiểm tra ngay; nút Dừng kiểm tra vẫn dùng được trong lúc đo. Ô «Tự kiểm tra khi mở» (`healthAutoCheck`, mặc định bật) tắt hành vi này cho máy chậm.
- **Khởi động nhẹ hơn, không khóa nút**: đo dung lượng thư mục cài (phần tốn CPU nhất khi mở — ≈5 giây CPU mỗi lần trên máy có 40 ứng dụng không khai báo dung lượng) được nhớ trong `%LOCALAPPDATA%\TweekPro\install-sizes.xml` tối đa 24 giờ cho từng thư mục (`Sizing/SizeCache`, dùng chung cho tab Ứng dụng và Ứng dụng Windows; mục cũ hơn 30 ngày tự bỏ). Đo dung lượng và kiểm tra sức khỏe chạy ở luồng ưu tiên thấp và **ngoài khóa thao tác**, nên mọi nút dùng được ngay sau khi danh sách ứng dụng hiện ra (trước đây bị mờ 8–10 giây). Thăm dò khởi động của kiểm tra sức khỏe không còn liệt kê 300+ dịch vụ qua WMI và xác minh chữ ký từng dịch vụ (`Advanced.Autoruns(includeServices:false)`). Đo trên máy phát triển: CPU cho toàn bộ khởi động giảm từ 7,8 s xuống 3,7 s ở lần mở thứ hai; bộ nhớ không đổi (≈50 MB riêng). Kiểm thử: cache trả kết quả với ngân sách 0, tồn tại qua lần đọc lại, hết hạn sau 24 giờ, khớp thư mục không phân biệt hoa thường; `Autoruns(false)` giữ mục Run và bỏ dịch vụ.
- **Tab tải chậm không còn bị bỏ qua**: bấm **Khởi động** hoặc **Explorer** trong lúc Tweek Pro còn bận (đọc danh sách, kiểm tra sức khỏe) giờ hiện «Đang đọc mục khởi động…» và tự tải ngay khi việc trước xong (`WhenIdle`), thay vì im lặng như trước.
- **Windows 7 SP1 / 8 / 8.1**: manifest khai báo thêm ba phiên bản này (64-bit, cần cài .NET Framework 4.8); bộ cài chạy `ngen install` sau khi chép tệp và `ngen uninstall` khi gỡ để lần mở đầu trên CPU chậm không phải biên dịch JIT toàn bộ. README ghi rõ phần nào lùi mức trên Windows cũ.
- **Vòng tròn dung lượng ổ đĩa** trên thẻ Tổng quan: mỗi ổ cố định (C:, D:, E:, …) một vòng, phần tô là dung lượng đã dùng, màu theo mức trống còn lại (≥20 % xanh, ≥10 % vàng, ≥5 % cam, dưới đó đỏ — cùng ngưỡng với mục «Dung lượng trống»), ghi «còn trống / tổng» bên dưới; đọc ở luồng nền khi mở và sau mỗi lần kiểm tra; số vòng tự co theo bề rộng cửa sổ để không che phần chữ. Kiểm thử: ngưỡng `DriveLevel`, phần trăm dùng kẹp trong [0, 1], nhãn, danh sách ổ sống có kích thước và sắp theo chữ cái, số vòng vừa khung.

### 0.7.6 (22/09/2026)

- **Tiện ích trình duyệt gỡ được** (`Extensions/ExtensionRemoval`): tab chuyển từ chỉ đọc sang xử lý. «Gỡ tiện ích đã chọn (vào Kho)» yêu cầu trình duyệt đã đóng (Tweek Pro đề nghị đóng giúp và chờ tối đa 15 giây); với Chromium, thư mục tiện ích được chuyển vào Kho, mục `extensions.settings.<id>`, MAC toàn vẹn `protection.macs…` và tham chiếu ghim/thanh công cụ được cắt khỏi `Preferences` và `Secure Preferences` (ghi nguyên tử, giữ nguyên các mục khác từng byte), khóa nguồn cài `Software\Google\Chrome\Extensions\<id>` (Edge/Chromium tương ứng) và giá trị `ExtensionInstallForcelist` của tiện ích đó cũng vào Kho rồi mới xóa; với Firefox, `.xpi` trong thư mục `extensions` của hồ sơ được chuyển vào Kho. Khôi phục (`Kind=Extension`) chép thư mục về, ghép lại mục cấu hình/MAC/ghim nếu chưa có, tạo lại khóa registry và giá trị chính sách. Từ chối tiện ích thành phần (location 5/10), add-on Firefox cài từ registry/hệ thống và mọi đường dẫn ngoài hồ sơ. «Dọn Forcelist (ép cài)…» liệt kê mọi giá trị Forcelist của Chrome/Edge/Brave/Chromium trong HKLM và HKCU (cả hai view, kèm tên tiện ích nếu còn trong hồ sơ) và xóa toàn bộ vào Kho, xóa luôn khóa rỗng để trình duyệt không còn báo «managed». Kiểm thử: hồ sơ Chromium giả với hai tiện ích, gỡ + khôi phục hai lần, tiện ích giải nén chỉ bị hủy đăng ký, `.xpi` Firefox đi/về, từ chối khi trình duyệt chạy, vòng Forcelist trên khóa HKCU giả.
- **Dấu vết ngang CCleaner**: thêm 13 nguồn — lịch sử PowerShell (PSReadLine), Remote Desktop (MRU + Servers), ổ mạng ánh xạ, RecentApps, FeatureUsage, ShellBags (mặc định không chọn), Regedit `LastKey` (giữ Favorites nhờ bộ lọc giá trị mới `TrackRule.Values`), Office File/Place MRU mọi phiên bản kể cả `User MRU\<tài khoản>` (mẫu nhiều dấu `*`, `TracksSafety` khớp đúng từng cấp), shortcut `Office\Recent`, Acrobat/Reader `cRecentFiles`, phiên và tab đã đóng của Chromium (mặc định không chọn), lịch sử Firefox **giữ dấu trang** (`places.sqlite` xóa `moz_historyvisits`/`moz_inputhistory`/`moz_places` chưa được bookmark bằng SQL), cookie Firefox. **Cookie giữ lại…**: danh sách tên miền (google.com giữ cả `.google.com` và `*.google.com`); khi có danh sách, cookie của Chromium (`Network\Cookies`) và Firefox (`cookies.sqlite`) được xóa **theo dòng** bằng `winsqlite3.dll` có sẵn trong Windows 10+ (`Core/WinSqlite`, không kèm thư viện ngoài), bản sao đầy đủ của cơ sở dữ liệu vào Kho và `VACUUM` để dữ liệu thật sự rời khỏi đĩa; khôi phục chép lại nguyên tệp khi trình duyệt đã đóng. **Dọn khi thoát Tweek Pro**: ghi nhớ các loại đang đánh dấu (`tracksExitRules`) và dọn lại vào Kho mỗi lần đóng ứng dụng, bỏ qua loại bị khóa vì trình duyệt còn chạy. Kiểm thử: cơ sở dữ liệu cookie giả 6 dòng → giữ 3 đúng tên miền, khôi phục về 6; mở rộng khóa nhiều `*`; bộ lọc giá trị chỉ xóa `LastKey`.
- **Dung lượng hệ thống**: cột **Ước tính thu hồi** cho từng mục (Windows.old, Delivery Optimization, hiberfil, thùng rác, driver cũ = toàn bộ; WinSxS = «Backups and Disabled Features» + «Cache and Temporary Data» đọc từ `DISM /Online /Cleanup-Image /AnalyzeComponentStore`, phân tích theo thứ tự dòng nên không phụ thuộc ngôn ngữ hiển thị); đo từng mục ở luồng nền và vẽ lại ngay sau mỗi mục, nút **Dừng**; công cụ chạy nền với tiến độ trực tiếp (đọc stdout theo cả `\r` nên % của DISM hiện trên dòng trạng thái) và báo ổ đĩa trống thêm bao nhiêu sau khi chạy. Mục mới **Windows\Installer — gói MSI/MSP mồ côi**: đối chiếu mọi `LocalPackage` trong `Installer\UserData\<SID>\Products\*\InstallProperties` và `…\Patches\*` với tệp `.msi/.msp` cấp gốc của `Windows\Installer`; tệp không còn ai tham chiếu và cũ hơn 1 ngày được **chuyển vào Kho** (`Kind=Installer`, khôi phục được); không đọc được danh sách sản phẩm thì bỏ qua để tránh xóa nhầm; kiểm tra lại tham chiếu ngay trước khi chuyển. Kiểm thử: phân tích DISM tiếng Anh và bản dịch dấu phẩy, tách dòng `\r`, thư mục Installer giả (tệp được tham chiếu / mới / `.txt` / thư mục con đều giữ), chuyển vào Kho và khôi phục.

### 0.7.5 (22/09/2026)

- **Gỡ cưỡng bức** (`Remnants/ForcedUninstall`): nút **Gỡ cưỡng bức…** ở tab Ứng dụng cho chương trình không còn trong danh sách (bộ cài hỏng, portable, gỡ dở) — nhập tên, tùy chọn nhà phát hành / thư mục cài / tệp .exe; Tweek Pro dựng một mục ảo và chạy quét sâu như bình thường, kết quả vào **Phần còn sót** để duyệt và xóa qua Kho. Tên ngắn/chung bắt buộc kèm thư mục hoặc .exe; thư mục Windows, Program Files gốc, hồ sơ người dùng, gốc ổ đĩa bị từ chối; thư mục ngoài vùng dọn được chỉ vào Chỉ xem.
- **Tab Dấu vết** (`Tracks/`): dọn dấu vết riêng tư của Windows (Gần đây, Jump Lists, RunMRU, TypedPaths, RecentDocs, ComDlg32, WordWheelQuery, UserAssist, ActivitiesCache.db) và trình duyệt (lịch sử, cookie, dữ liệu biểu mẫu của Chrome/Edge/Brave/Firefox). Xem trước theo quy tắc với số mục và dung lượng; tệp vào Kho (`Kind=Tracks`), khóa Registry lưu nguyên cây và khôi phục kiểu gộp — mục đã sinh mới sau khi dọn không bị ghi đè. `TracksSafety` chỉ cho các khóa HKCU và thư mục trong danh sách; quy tắc trình duyệt khóa khi trình duyệt còn chạy.
- **Tab Registry** (`RegClean/`): tìm mục Registry mồ côi — khóa Uninstall, App Paths, Applications, MUICache, SharedDLLs, Shell Extensions Approved — mà tệp/DLL đích không còn tồn tại. Bảo thủ có chủ đích: không «tối ưu», không đụng CLSID, Services, lớp tệp; chỉ dọn khi đường dẫn đích xác minh được là vắng. Mỗi lần dọn vào Kho (`Kind=Registry`), khôi phục ghi lại đúng mục chưa tồn tại. Mở Regedit tại khóa đã chọn.
- **Tab Dung lượng hệ thống** (`SysSpace/`): đo Windows.old, WinSxS, bộ đệm Delivery Optimization, driver cũ trùng lặp trong DriverStore (đọc bằng `Get-WindowsDriver`), hiberfil.sys, pagefile.sys, dung lượng điểm khôi phục, Thùng rác. Mỗi mục giải phóng bằng đúng công cụ Windows (`cleanmgr` sageset cho Windows.old, `DISM /StartComponentCleanup`, `pnputil /delete-driver` không `/force`, `powercfg -h off`, `vssadmin`, cmdlet Thùng rác) sau khi hỏi xác nhận; không tự xóa tệp và không qua Kho vì các vùng này quá lớn hoặc không hoàn tác được; kết quả công cụ vào nhật ký.
- **Tab Tiện ích trình duyệt** (`Extensions/`, chỉ đọc): liệt kê tiện ích của Chrome, Edge, Brave, Vivaldi, Opera và Firefox trong mọi hồ sơ với trạng thái bật/tắt, nguồn cài (cửa hàng, **ép cài bằng chính sách** qua `ExtensionInstallForcelist`, giải nén, cài ngoài), quyền rộng (`<all_urls>`, cookies, webRequest, history, nativeMessaging…), ngày cài, phiên bản; tô đỏ mục cần xem; mở thư mục tiện ích, mở trang quản lý của trình duyệt (`chrome://extensions`, `about:addons`), CSV. Đọc bằng bản sao khi trình duyệt đang khóa tệp; gỡ tiện ích vẫn qua chính trình duyệt để hồ sơ không hỏng.
- **Xóa không phục hồi (shredder)** trong khung Duyệt tệp của Explorer (`Shredder/`): liệt kê trước mọi tệp sẽ bị xóa, hộp xác nhận bắt tích «Tôi hiểu», chọn 1 lần ghi ngẫu nhiên (khuyến nghị) hoặc 3 lần (0x00/0xFF/ngẫu nhiên), ghi đè qua `WriteThrough`, cắt về 0 byte, đổi tên ngẫu nhiên rồi xóa; thư mục xóa từ dưới lên cũng dưới tên ngẫu nhiên. **Không qua Kho.** `ShredSafety` từ chối gốc ổ đĩa, tệp hệ thống ở gốc ổ, Windows, Program Files, ProgramData\Microsoft, thư mục hồ sơ người dùng, Kho khôi phục, thư mục Tweek Pro, đường dẫn UNC; symlink/junction không bao giờ bị đi xuyên (chỉ gỡ liên kết); tối đa 20.000 tệp một lần; ghi chú về SSD/USB/ổ mạng ngay trong hộp xác nhận.
- **Dung lượng đầy đủ như Revo** (`Sizing/`): app không khai báo `EstimatedSize` được đo từ thư mục cài, hoặc từ thư mục chứa trình gỡ / DisplayIcon khi không khai báo thư mục; game Steam đọc `libraryfolders.vdf` + `appmanifest_*.acf` nên game đã chuyển sang ổ khác (D:\SteamLibrary) hiện đúng kích cỡ và thư mục thật; tab **Ứng dụng Windows** có cột **Dung lượng** đo từ thư mục gói, đi theo liên kết `WindowsApps → D:\XboxGames` của game Xbox. Không đo Program Files, ProgramData, Windows, thư mục người dùng hay gốc ổ đĩa; mỗi thư mục tối đa 6 giây, dấu «+» nghĩa là đo chưa hết.
- **Sửa lỗi**: mở tab **Kho khôi phục** (hoặc bất kỳ danh sách có ô tích đã được nạp trước khi tab hiện ra) không còn báo «Object reference not set to an instance of an object» và kẹt ở tab cũ — Windows phát sự kiện ItemChecked cho từng dòng lúc tạo danh sách, nay được bỏ qua ở `SmoothListView`. Lỗi chưa xử lý ghi cả stack trace vào nhật ký.

### 0.7.4 (22/09/2026)

- **Khởi động** liệt kê đủ ba nguồn như Autorun Manager: Run/RunOnce/shortcut Startup, ứng dụng Windows (cờ State của gói Appx), và dịch vụ Win32 tự chạy (không gồm driver). Mỗi dòng có ô tích: bỏ tích thì tắt qua Kho khôi phục, tích lại thì bật. Dịch vụ cốt lõi bị từ chối và dịch vụ đang chạy không bị dừng. Mục Run/Startup mà Windows đã tắt thì tích lại chỉ bật cờ StartupApproved, lệnh gốc giữ nguyên.

### 0.7.3 (21/09/2026)

- Dòng mô tả trên thanh tiêu đề rút còn «Gỡ ứng dụng, dọn rác, theo dõi mạng • Mọi thao tác xóa đều được sao lưu và hoàn tác» và dừng trước nút ngôn ngữ / quyền, không còn bị che.
- Nút ngôn ngữ trên thanh tiêu đề là lá cờ (Việt Nam khi đang tiếng Việt, Union Jack khi tiếng Anh) thay cho quả địa cầu kèm chữ VI/EN.
- **Explorer → Duyệt tệp**: trình duyệt thư mục riêng của Tweek Pro hiện **mọi** tệp/thư mục ẩn và hệ thống bất kể thiết lập Explorer, có cột Đuôi, Mô tả (loại theo shell), Dung lượng, Sửa lần cuối, Thuộc tính; mục ẩn in nghiêng, hệ thống màu vàng, tệp giả dạng (`hoadon.pdf.exe`) màu đỏ; bấm một tệp là xem chi tiết ngay (mã băm tính khi bấm «Xem chi tiết + băm»); Lên một cấp / This PC / Thư mục người dùng / lọc «Chỉ hiện mục ẩn / hệ thống» / Mở trong Explorer / Thuộc tính Windows.
- **Mạng → Chặn mạng theo ứng dụng** (`Network/FirewallBlock.cs`): chuột phải một ứng dụng → **Chặn mạng** tạo cặp quy tắc Windows Firewall (vào + ra) chỉ cho tệp .exe đó, tên quy tắc `TweekPro Block - <exe> [hash]`; cột **Chặn mạng** đỏ, menu **Bỏ chặn**, nút **Đang chặn mạng…** liệt kê và bỏ chặn hàng loạt; mỗi lần chặn là một mục Kho khôi phục (`Kind=Firewall`) — khôi phục = gỡ đúng quy tắc theo tên. Từ chối tiến trình cốt lõi, tệp trong `Windows\System32`, chính Tweek Pro, đường dẫn mạng và tệp không phải .exe.
- **Quét sâu phần còn sót — dò dấu vết mở rộng** (`Remnants/TraceHunter.cs`): tên biến thể (bỏ phiên bản/năm/kiến trúc/ngoặc, bỏ tiền tố nhà phát hành, tên gộp, tên tệp .exe đã ghi nhận; tên ngắn/chung chỉ đưa vào mục Chỉ xem); giá trị dọn được qua Kho: MUICache, Trợ lý tương thích (Persisted), bộ đếm FeatureUsage, quy tắc Firewall trỏ tới exe đã gỡ, SharedDLLs, StartupApproved; mục Chỉ xem: đăng ký «Mở bằng», ProgID/CLSID trỏ tới tệp đã gỡ, RegisteredApplications, biến PATH, nguồn Event Log, đăng ký Windows Installer/Tracing, CrashDumps, báo cáo WER, Prefetch, shortcut Gần đây, thư mục Start Menu, Documents / Saved Games / thư mục «.tên». Toàn bộ giai đoạn có giới hạn thời gian (25 giây) và số khóa.
- **Thanh tab cố định thứ tự** (`TabStrip`): TabControl nhiều hàng của Windows luôn kéo hàng chứa tab đang chọn xuống sát nội dung nên 17 tab đảo vị trí sau mỗi lần bấm; nay tab được vẽ trong lưới trái→phải, trên→dưới không đổi, tiêu đề gốc của TabControl bị ẩn.
- README có **ảnh tất cả các tab** (`docs/screenshots/`, kết xuất bằng `--preview all [thư_mục] [--en]`, 1280×820, tiếng Việt). Tab Explorer chia thành hai khung chuyển bằng nút «Duyệt tệp và chi tiết tệp» / «Tùy chỉnh File Explorer» để mỗi danh sách dùng trọn chiều cao cửa sổ; nút Lên / This PC / Người dùng / Chọn tệp nằm cùng hàng Đường dẫn, nút thao tác tệp nằm trên khung chi tiết; sửa lỗi mọi tệp trong trình duyệt tệp bị tô đỏ như tệp giả dạng.
- **Tab Explorer** (`Explorer/`): 17 công tắc File Explorer / thanh tác vụ (hiện đuôi tệp, tệp ẩn, tệp hệ thống được bảo vệ, hộp chọn, đường dẫn đầy đủ, mở vào This PC, tệp gần đây, quảng cáo OneDrive, giây trên đồng hồ, căn trái, Task View, Widgets…) — ghi HKCU qua Kho (`Kind=Explorer`, khôi phục một nút), `ExplorerSafety` chỉ cho đúng khóa/giá trị trong danh mục, Explorer tự làm mới, nút Khởi động lại Explorer (không nâng quyền shell).
- **Xem chi tiết tệp**: chọn / kéo thả / dán đường dẫn → loại thật, thuộc tính, thời gian, chủ sở hữu, chữ ký số, phiên bản, kiến trúc PE, Mark of the Web (Zone.Identifier), SHA-256/MD5; cảnh báo đuôi kép, ký tự đảo chiều Unicode, PE giả tài liệu, tệp thực thi tải về chưa ký; sao chép báo cáo, mở hộp Thuộc tính Windows.
- `release.ps1` tự chốt mục changelog này (đổi tiêu đề thành số bản + ngày, thêm `-Notes`) và GitHub Release dùng nó làm mô tả; sửa cảnh báo `FileVersion` 5 phần khi build.

### 0.7.2 (21/09/2026)

- Kiểm tra cập nhật tự động khi khởi động; dải thông báo trên Tổng quan với **Tải về** / **Bỏ qua bản này** (`updateAutoCheck`, `updateSkipVersion`).
- Bộ cài xóa exe phiên bản cũ trong `Program Files\Tweek Pro` trước khi chép bản mới.
- `release.ps1` tự tăng số bản vá khi không truyền `-Version`; chịu được thông báo tiến trình của git trên stderr.

### 0.7.1 (21/09/2026)

- Phát hành đầu tiên qua `release.ps1` + GitHub Actions; Release có Setup và portable.
- **Startup Insight:** cột Windows (StartupApproved), Tác động, Chữ ký, Kích cỡ; mục hỏng; khu vực khởi động trong Tổng quan tính `AutorunBroken`.
- **Windows Tools** lấy icon hệ thống thật từ .exe/.cpl/.msc; catalog mở rộng; cột dòng lệnh.
- **Kiểm tra cập nhật v1** (`Update/`): hỏi GitHub Releases/latest, fallback tag `v*`, chỉ thông báo và mở trang.
- **Tệp tải về cũ** (`Stale/`): bộ cài, tệp nén, ảnh đĩa, tệp tải dở, tệp lớn dưới Downloads/Desktop; Kho `Kind=Stale`; `StaleSafety`.
- **Phát hiện PUP/bloatware** (`Pup/`): 11 nhóm quy tắc nhúng, `PupSafety` bảo vệ nhà phát hành tin cậy; cột Cảnh báo ở Ứng dụng và Ứng dụng Windows; khu vực thứ bảy trong Tổng quan.
- Icon thanh tác vụ Windows 11 qua `WM_SETICON` + AppUserModelID; tab Trợ lý AI chuyển ra cuối; bảng dịch tiếng Anh theo tính năng gộp bởi `Core.L`.

### 0.7.0

- Đổi tên **AppCare → Tweek Pro**, namespace `TweekPro`, dữ liệu `%LOCALAPPDATA%\TweekPro` với di trú tự động; `TweekPro.csproj` SDK-style (net48, C# 7.3); logo T + PC và icon vector từng tab.
- Quyền quản trị mặc định (`app.manifest`, PerMonitorV2, long paths); tệp cứng đầu với chiếm quyền sở hữu và hẹn xóa khi khởi động lại; trạng thái Kho `PendingReboot`.
- **Tổng quan** (điểm 0–100, hạng A–E); **Dọn rác** theo quy tắc JSON kèm Rác hệ thống và `JunkSafety.WindowsCoreFolders`; **Thư mục rỗng**; **Tệp trùng lặp** (SHA-256); **Phân tích ổ đĩa**.
- **Ứng dụng Windows** (Appx/MSIX qua PowerShell, phân loại trong mã, logo từ AppxManifest); **Dịch vụ hệ thống** với cơ sở kiến thức bằng lời thường; **Mạng** hai tầng với băng thông ETW, icon hướng, dải packet realtime, chuột phải dừng tiến trình/dịch vụ.
- **Trợ lý AI** Claude/OpenAI/LM Studio/Ollama với khóa DPAPI và 14 công cụ có xác nhận; menu chuột phải cho mọi bảng.
- Kho: xóa vĩnh viễn theo mục hoặc theo tuổi, đọc kho AppCare cũ. Song ngữ EN/VI; `settings.json`; nhật ký xoay vòng; bộ cài Inno Setup + zip portable; CI trên GitHub Actions.

<details>
<summary><b>Lịch sử AppCare 0.2 – 0.6</b></summary>

- **0.6** — Theme dùng chung mọi tab; cửa sổ quét nhóm kết quả theo loại với trạng thái trống/lỗi/chưa đầy đủ; quét phần còn sót theo tài khoản (`%LOCALAPPDATA%`, `Programs`, `%APPDATA%`, LocalLow, ProgramData, InstallLocation), chỉ thêm Temp khi trùng đúng tên.
- **0.5** — Cửa sổ Quét phần còn sót riêng sau khi gỡ (giai đoạn, số mục realtime, chọn/xóa/dừng); lớp `Theme`; `ScanWindow.cs`, `AppCare.csproj`.
- **0.4** — Autorun Manager (tắt có sao lưu, khôi phục, CSV); Windows Tools 19 lối mở; quét sâu (thư mục lồng, Registry nhà phát hành, Run/RunOnce/AppCompatFlags, shortcut; App Paths/dịch vụ/tác vụ chỉ xem); `SCAN-GUIDE.md`.
- **0.3** — Chuẩn hóa ngày cài về dd/MM/yyyy; đọc DisplayIcon (kể cả chỉ số tài nguyên); giao diện mới với icon 32 px; `Presentation.cs`.
- **0.2** — Tự quét sau mỗi ứng dụng gỡ thành công; Chọn/Bỏ chọn tất cả; tìm theo thư mục InstallLocation và Nhà phát hành\Ứng dụng trong AppData/ProgramData; xóa = chuyển vào kho khôi phục.
</details>

---

<p align="center">Tham khảo: <a href="https://learn.microsoft.com/en-us/windows/win32/msi/uninstall-registry-key">Uninstall Registry Key (Microsoft Learn)</a></p>
