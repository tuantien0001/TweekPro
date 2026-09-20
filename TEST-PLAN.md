# Tweek Pro 0.7 — Kế hoạch kiểm thử trong máy ảo

Chạy đủ checklist này trước khi giao bản. Chỉ thử tính năng phá hủy trong máy ảo có snapshot (Hyper-V/VirtualBox), không thử trên máy thật.

## Ma trận

| Hệ điều hành | Tài khoản | Bộ cài mẫu |
|---|---|---|
| Windows 10 22H2 x64 | Người dùng thường; quản trị (Run as administrator / nút Khởi động lại với quyền quản trị) | MSI (7-Zip msi), Inno Setup (Notepad++), NSIS (VLC), Squirrel/Electron per-user (Discord hoặc VS Code user setup), ứng dụng Store (chỉ để kiểm tra không hiển thị sai) |
| Windows 11 24H2 x64 | Như trên | Như trên |

Với mỗi ô: chụp snapshot "sạch" trước, chạy checklist, khôi phục snapshot sau.

## 0. Biên dịch và kiểm thử tự động

- [ ] `build.ps1` trên máy **không có** .NET SDK: in hướng dẫn cài SDK, mã thoát 2, không throw.
- [ ] `build.ps1 -SelfTest` trên máy có SDK 8: tạo `bin\Release\net48\TweekPro-0.7.exe`, `--self-test` PASS, `test-results.txt` không chứa stack trace, không còn thư mục/khóa `TweekProTest*`/`TweekProFixture*` trong `%LOCALAPPDATA%`, `%LOCALAPPDATA%\Temp`, HKCU\SOFTWARE và Run.
- [ ] `--self-test core` và `--self-test junk` PASS.
- [ ] `--preview`, `--preview scan`, `--preview junk`, `--preview network` tạo PNG, không `preview-error.txt`.

## 1. Rebrand và di trú dữ liệu (1.1)

- [ ] Tiêu đề cửa sổ "Tweek Pro 0.7 – Trình quản lý Windows"; header, ghi chú, hộp thoại không còn chữ "AppCare" ngoài chỗ nói về kho cũ.
- [ ] Có `%LOCALAPPDATA%\AppCare` (từ bản 0.6, gồm `Backups` với ≥ 1 bản), chưa có `TweekPro`: mở 0.7 → thư mục được đổi tên thành `TweekPro`, nhật ký ghi "Đã chuyển dữ liệu", tab Kho hiển thị bản cũ với cột Kho = "Tweek Pro", khôi phục được.
- [ ] Cả hai thư mục cùng tồn tại: không gộp, không ghi đè; bản trong `AppCare\Backups` hiển thị với Kho = "AppCare (cũ)"; khôi phục và xóa vĩnh viễn hoạt động trên bản đó; bản mới được ghi vào `TweekPro\Backups`.
- [ ] Khóa thư mục `AppCare` bằng một tệp đang mở bên trong → di trú báo Failed trong nhật ký, ứng dụng vẫn chạy và vẫn đọc kho cũ.

## 2. Build hai đường (1.2)

- [ ] `dotnet build -c Release` trên Windows và exe chạy được từ `bin\Release\net48` (copy cả thư mục sang máy khác không có SDK → chạy được).
- [ ] Defender/SmartScreen: ghi lại cảnh báo (nếu có) và hash SHA-256 của exe vào changelog.

## 3. Xóa vĩnh viễn kho (1.3)

- [ ] Tạo ≥ 3 bản sao lưu (dọn phần còn sót + dọn rác vào kho); khôi phục 1 bản.
- [ ] Đánh dấu 1 bản đã khôi phục → **Xóa vĩnh viễn mục đã đánh dấu** → hộp xác nhận nêu số mục và dung lượng; sau OK thư mục `Backups\<id>` biến mất, dung lượng ổ tăng tương ứng, danh sách cập nhật.
- [ ] Đánh dấu bản CHƯA khôi phục → hộp xác nhận có cảnh báo mất khả năng hoàn tác.
- [ ] **Dọn kho theo tuổi…**: sửa `Created` trong một manifest về 200 ngày trước; với 90 ngày và không tick "bao gồm chưa khôi phục" chỉ bản đã khôi phục được chọn; tick → bản chưa khôi phục cũng được chọn; số liệu dung lượng khớp Explorer.
- [ ] Hủy ở mọi hộp thoại → không có gì bị xóa.

## 4. Dọn rác (1.4)

- [ ] **Xem trước** không thay đổi đĩa (so sánh số tệp trong `%TEMP%` trước/sau).
- [ ] Tạo trong `%TEMP%` một tệp mới và một tệp với ngày tạo/sửa lùi 3 ngày (`(Get-Item x).CreationTime=...`): chỉ tệp cũ xuất hiện.
- [ ] Mở một tệp cũ bằng `notepad`/khóa bằng PowerShell `[IO.File]::Open(...,'None')` → dọn báo "Bỏ qua vì đang dùng: 1", tệp còn nguyên.
- [ ] Chrome đang chạy (kể cả chỉ tiến trình nền) → nhóm Chrome đỏ "Bị khóa", không tick được; đóng Chrome hẳn → xem trước lại → dọn được; mở Chrome lại: lịch sử, mật khẩu, bookmark, extension còn nguyên; cache được tạo lại.
- [ ] Firefox tương tự với `cache2`.
- [ ] Chế độ vào Kho: dọn nhóm Temp → tab Kho có bản loại "Rác" với dung lượng; **Khôi phục** → toàn bộ tệp trở về đúng đường dẫn (đối chiếu danh sách trước đó), thư mục con được tạo lại; nếu đích đã tồn tại thì bỏ qua và ghi lỗi mô tả.
- [ ] Chế độ **Xóa thẳng**: nút đổi tên đỏ, hai hộp xác nhận; sau dọn không có bản sao lưu mới; dung lượng ổ tăng.
- [ ] Người dùng thường: nhóm `C:\Windows\Temp` và WER toàn máy hiển thị "Bị khóa • cần quản trị"; quản trị: dọn được.
- [ ] Sửa `%LOCALAPPDATA%\TweekPro\junk-rules.json` trỏ một quy tắc vào `%USERPROFILE%\Documents` hoặc `%LOCALAPPDATA%` gốc hoặc `...\Chrome\User Data\Default` → Xem trước ghi chú "nằm ngoài vùng rác được phép"/"thư mục được bảo vệ", 0 tệp; không có gì bị dọn.
- [ ] Đặt `minAgeHours` lớn → không có tệp; đặt `patterns` có `\` → nạp quy tắc báo lỗi và dùng mặc định.

## 5. Mạng (1.5)

- [ ] Người dùng thường: tab Mạng hiển thị kết nối (chrome/edge/svchost…), PID, nhà phát hành; nút băng thông ghi "cần quyền quản trị"; không có cột byte.
- [ ] Chu kỳ 1 s trong 10 phút: CPU trung bình của TweekPro < 3 %, bộ nhớ ổn định (Task Manager). **Tạm dừng** giữ bảng, **Tiếp tục** cập nhật lại.
- [ ] Tìm kiếm "chrome" lọc đúng; **Ẩn loopback** bỏ 127.0.0.1/::1.
- [ ] **Phân giải tên máy** tắt: Wireshark/`Get-DnsClientCache` không thấy truy vấn PTR từ Tweek Pro; bật: cột Máy từ xa dần được điền.
- [ ] Quản trị: **Bật băng thông (ETW)** → `logman query -ets` thấy `TweekPro-Network`; tải một tệp lớn 60 s → tổng byte nhận của tiến trình tải sai lệch ≤ 10 % so với Task Manager (cột Network của tiến trình) hoặc Resource Monitor.
- [ ] Đóng ứng dụng, mở lại và kill bằng Task Manager: `logman query -ets` không còn `TweekPro-Network` sau khi đóng bình thường; sau kill, lần mở tiếp bật ETW thành công (phiên cũ được dừng trước).
- [ ] Chạy 8 giờ với ETW bật: bộ nhớ < 150 MB, không rò rỉ tăng dần; `EventsLost` trên dòng tóm tắt bằng 0 hoặc rất nhỏ.
- [ ] Xuất CSV mở được trong Excel, cột không lệch với đường dẫn có dấu phẩy.

## 6. Quyền, cài đặt, nhật ký (1.6)

- [ ] Nút **Khởi động lại với quyền quản trị**: đúng một hộp UAC; cửa sổ cũ đóng, cửa sổ mới có huy hiệu xanh "Quyền quản trị viên"; từ chối UAC → cửa sổ cũ vẫn mở và nhật ký ghi "Đã hủy".
- [ ] Đổi tick "Quét sâu sau khi gỡ", chu kỳ mạng, phân giải tên máy, kích cỡ cửa sổ → đóng mở lại vẫn giữ; `settings.json` đọc được, sửa tay giá trị vô lý (chu kỳ 999) → bị kẹp về 30.
- [ ] `Logs\tweekpro-yyyyMMdd.log` có dòng khởi động/đóng, mức INFO/WARN/ERROR; tạo 20 tệp log giả ngày cũ → lần ghi tiếp chỉ còn 14 ngày.
- [ ] Gây lỗi có chủ đích (ví dụ xóa thư mục dữ liệu trong khi chạy) → hộp lỗi thân thiện, không crash, dòng ERROR trong nhật ký.

## 7. Bất biến an toàn (kế thừa 0.6, kiểm tra lại)

- [ ] Gỡ 5 bộ cài mẫu theo hàng đợi; cửa sổ quét mở khi đăng ký biến mất; mục Chỉ xem không tick được; xóa có sao lưu; khôi phục 100 %.
- [ ] Ứng dụng còn đăng ký → từ chối dọn; thư mục giao với ứng dụng còn cài → từ chối.
- [ ] Không có thao tác nào chạm `Program Files\Common Files`, `Windows`, `Packages`, junction/symlink.
- [ ] Service/driver/tác vụ lịch chỉ xem ở mọi nơi.

## Ghi kết quả

Với mỗi ô ma trận: ngày, build hash, người thử, kết quả từng mục (Đạt/Không đạt/Không áp dụng), ảnh chụp lỗi. Không giao bản khi có mục "Không đạt" ở phần 3, 4, 5 hoặc 7.
