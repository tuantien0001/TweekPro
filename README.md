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

- **Yêu cầu:** Windows 10 (1903+) hoặc Windows 11, 64-bit. .NET Framework 4.8 đã có sẵn trong hệ thống; bộ cài tự kiểm tra.
- **Quyền quản trị:** ứng dụng luôn chạy quản trị (UAC hỏi một lần khi mở). Quyền này cần cho Rác hệ thống, HKLM, băng thông ETW, gỡ ứng dụng Windows cho mọi tài khoản và tệp cứng đầu. Có quyền cao không đồng nghĩa xóa được mọi thứ — các thư mục lõi vẫn bị khóa trong mã.
- **Cập nhật:** khi khởi động, ứng dụng hỏi GitHub Releases một lần (không gửi gì khác); có bản mới thì hiện dải thông báo trên tab **Tổng quan** với **Tải về** / **Bỏ qua bản này**. Tắt tự kiểm tra bằng `updateAutoCheck: false` trong `settings.json`; nút **Kiểm tra cập nhật** vẫn dùng được.
- **Dữ liệu:** `%LOCALAPPDATA%\TweekPro` — `Backups\` (Kho), `settings.json`, `sessions.xml`, `Logs\` (giữ 14 ngày), `junk-rules.json` tùy chọn. Lần chạy đầu tự đổi tên thư mục `AppCare` cũ sang `TweekPro`; kho AppCare vẫn đọc và khôi phục được.
- **Ngôn ngữ:** nút quả cầu **EN / VI** ở góc phải header đổi ngôn ngữ và khởi động lại (khóa `language` trong `settings.json`).

## Các tab

| Tab | Chức năng | An toàn |
|---|---|---|
| **Tổng quan** | Kiểm tra sức khỏe một nút: điểm 0–100, hạng A–E, 7 khu vực (rác, phần còn sót, thư mục rỗng, Kho, khởi động, ổ trống, phần mềm không mong muốn), dung lượng giải phóng được; bấm đúp mở tab xử lý; sao chép báo cáo; dải thông báo cập nhật | Chỉ đọc; khu vực không đo được không bị trừ điểm |
| **Ứng dụng** | Ứng dụng desktop (HKLM/HKCU), gỡ theo hàng đợi, quét phần còn sót sau khi gỡ, cột **Cảnh báo** PUP/bloatware với lý do bằng lời thường, lọc «Chỉ hiện mục cảnh báo», CSV, chuột phải | Không dùng Win32_Product; chặn lệnh gỡ qua cmd/PowerShell; nhà phát hành cốt lõi (Microsoft, Google, Mozilla, NVIDIA, Intel, AMD…) không bao giờ bị đánh dấu |
| **Ứng dụng Windows** | Gói Appx/MSIX cài sẵn và Microsoft Store với logo thật; nhóm Gỡ được / Cần cân nhắc / Được bảo vệ; gỡ cho tài khoản hiện tại hoặc mọi tài khoản; mở trang Store, thư mục gói | Framework, gói `NonRemovable`, Start/Search/Settings/Windows Security/Edge và `Microsoft.Windows.*` bị khóa trong mã; Store/App Installer/Photos hỏi hai lần; Kho `Kind=Store` đăng ký lại từ thư mục gói |
| **Phần còn sót** | Kết quả quét nhanh/sâu chờ duyệt; xem, chọn, xóa có sao lưu; hẹn xóa khi khởi động lại cho tệp bị giữ | Từ chối dọn khi ứng dụng còn đăng ký; mục Chỉ xem không tích được |
| **Dọn rác** | Xem trước theo quy tắc (Temp, CrashDumps, WER, cache Explorer/GPU/trình duyệt, Rác hệ thống) với số tệp và dung lượng; vào Kho (mặc định) hoặc **Xóa thẳng** | Bỏ qua tệp < 24 giờ và tệp đang mở; cache trình duyệt khóa khi trình duyệt còn chạy; `JunkSafety` chặn Documents/Desktop/Downloads/OneDrive và mọi thư mục Windows lõi |
| **Thư mục rỗng** | Quét một thư mục, liệt kê nhánh hoàn toàn rỗng, xóa một lần | Kiểm tra rỗng lại ngay trước khi xóa; từ chối gốc ổ đĩa, Windows, Program Files |
| **Tệp trùng lặp** | Gom theo kích thước rồi SHA-256; chọn giữ bản cũ nhất/mới nhất; bản dư vào Kho hoặc xóa thẳng | Bản giữ lại không bao giờ bị đụng; băm lại trước khi xóa thẳng; `DupeSafety` |
| **Tệp tải về cũ** | Downloads (và Desktop) — bộ cài, tệp nén, ảnh đĩa, tệp tải dở, tệp lớn cũ hơn N ngày (mặc định 30); vào Kho (`Kind=Stale`) hoặc xóa thẳng | `StaleSafety` chỉ cho Downloads/Desktop của tài khoản hiện tại; bỏ qua thư mục đám mây, shortcut, tệp ẩn/hệ thống, tệp đang mở |
| **Phân tích ổ đĩa** | Thư mục con nặng nhất, dung lượng theo phần mở rộng, tệp lớn nhất; mở vị trí, CSV | Hoàn toàn chỉ đọc; giới hạn 1.000.000 tệp / 120 giây |
| **Kho khôi phục** | Khôi phục mục đã chọn; xóa vĩnh viễn mục đánh dấu; **Dọn kho theo tuổi…** có xem trước | Xóa vĩnh viễn không hoàn tác; mặc định chỉ xóa bản đã khôi phục |
| **Khởi động** | Run/RunOnce/Startup với trạng thái Windows (StartupApproved), **tác động**, chữ ký, kích cỡ, mục hỏng; tắt có sao lưu, bật lại, CSV | Không dừng tiến trình đang chạy |
| **Mạng** | Kết nối TCP/UDP theo ứng dụng, icon và hướng gửi/nhận, thanh tốc độ, dải packet realtime; băng thông ETW tự bật khi quản trị; chuột phải kết thúc tiến trình / dừng dịch vụ | Chỉ xem, không chặn, không driver; `ProcessControl` khóa tiến trình và dịch vụ cốt lõi; phiên ETW luôn dừng khi đóng |
| **Dịch vụ hệ thống** | Mọi dịch vụ với **giải thích bằng lời thường** (~180 dịch vụ + mẫu tên), huy hiệu an toàn, PID/RAM, nhà phát hành; lọc; Dừng / Khởi động / **Dừng và vô hiệu hóa (lưu Kho)** | Dịch vụ cốt lõi bị khóa; svchost không cho kết thúc; kiểu khởi động cũ lưu `Kind=Service` |
| **Công cụ** | Lối mở công cụ Windows với icon hệ thống thật từ .exe/.cpl/.msc và cột dòng lệnh; Kiểm tra cập nhật | SFC/chkdsk hỏi xác nhận |
| **Nhật ký** | Nhật ký phiên, mở thư mục nhật ký/dữ liệu, lưu cài đặt | Xoay vòng theo ngày |
| **Trợ lý AI** | Claude / OpenAI / LM Studio / Ollama; chat có gọi công cụ (14 công cụ: 9 chỉ đọc, 5 thay đổi); khóa API mã hóa DPAPI | Mặc định **Hỏi xác nhận**; Chỉ đọc / Tự động do người dùng chọn; hàng rào engine luôn giữ nguyên |

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
| `--preview <remnants\|apps\|stale\|autorun\|tools\|junk\|network\|health\|store\|services\|ai> [--show]` | Kết xuất `TweekPro-preview.png` (hoặc mở cửa sổ) với dữ liệu mẫu |
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

Script ghi phiên bản mới vào `TweekPro.csproj`, `App.cs`, `build.ps1`, `installer\TweekPro.iss` (giữ UTF-8/BOM), chạy `build.ps1 -SelfTest`, commit «Release vX.Y.Z», gắn tag và push; nếu build/test lỗi thì hoàn nguyên. GitHub Actions (`.github/workflows/release.yml`) nhận tag, build trên `windows-latest`, đính kèm Setup + zip và xuất bản Release — vài phút sau ứng dụng báo bản mới. Đóng gói thủ công: `publish.ps1` (cần [Inno Setup 6.3+](https://jrsoftware.org/isinfo.php), `-InstallInno` để cài qua winget).

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

## Giới hạn hiện tại

Chưa có theo dõi cài đặt (install monitor), forced uninstall, quản lý tiện ích trình duyệt, chặn mạng, hay tự tải và cài bản mới im lặng (Kiểm tra cập nhật chỉ báo và mở trang tải). Không xác định được mọi dấu vết của mọi ứng dụng. Băng thông theo tiến trình chỉ tính từ khi bật ETW. Bản phân phối là một thư mục (exe + DLL). Chuỗi giao diện nằm trong mã (chưa `.resx`); một số thông báo nhật ký còn tiếng Việt khi chọn tiếng Anh.

## Lịch sử phiên bản

### 0.7.2

- Kiểm tra cập nhật tự động khi khởi động; dải thông báo trên Tổng quan với **Tải về** / **Bỏ qua bản này** (`updateAutoCheck`, `updateSkipVersion`).
- Bộ cài xóa exe phiên bản cũ trong `Program Files\Tweek Pro` trước khi chép bản mới.
- `release.ps1` tự tăng số bản vá khi không truyền `-Version`; chịu được thông báo tiến trình của git trên stderr.

### 0.7.1

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
