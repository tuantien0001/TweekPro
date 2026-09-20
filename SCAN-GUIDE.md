# AppCare 0.6 — Quét sâu và truy vết phần còn sót

## Sử dụng trong AppCare

1. Mở AppCare-0.6.exe. Tùy chọn **Quét sâu sau khi gỡ** đang bật mặc định.
2. Trước khi gỡ, AppCare ghi nhận tên, thư mục cài và một số đường dẫn executable. Sau khi đăng ký cài đặt biến mất, AppCare mở cửa sổ **Quét phần còn sót** và quét sâu tự động; cửa sổ hiển thị giai đoạn quét, số thư mục đã duyệt, dung lượng từng mục và có các nút Chọn tất cả / Bỏ chọn / Xóa đã chọn.
3. Với ứng dụng đã được ghi nhận ở các lần trước, vào **Phần còn sót → Quét siêu sâu**. Nút này quét các ứng dụng trong lịch sử. Với ứng dụng chưa ghi nhận, chọn nó ở tab Ứng dụng rồi bấm Quét mục đang xem trước.
4. Xem cột cơ sở đề xuất, dùng **Chọn tất cả** hoặc đánh dấu riêng, rồi **Xóa đã chọn (có sao lưu)**. Mục **Chỉ xem** không thể được đánh dấu xóa.
5. Xem Nhật ký để biết giới hạn đã chạm hoặc đường dẫn bị bỏ qua. **Dừng quét sâu** giữ kết quả của các ứng dụng đã hoàn tất, không giữ kết quả dở dang của ứng dụng đang quét.

## Các nguồn được kiểm tra trong phiên bản này

- InstallLocation và các thư mục trùng tên chính xác trong Program Files, Program Files (x86), LocalAppData, **LocalAppData\Programs** (cài theo từng tài khoản, ví dụ Devin/Electron), Roaming AppData, LocalLow và ProgramData, kể cả các thư mục lồng bên dưới trong giới hạn độ sâu.
- `%LOCALAPPDATA%\Temp` (và thư mục Temp của tài khoản) **chỉ** khi trùng đúng tên ứng dụng, nhà phát hành\ứng dụng, thư mục cuối của InstallLocation hoặc thư mục chứa executable đã ghi nhận. Không duyệt mù toàn bộ Temp.
- Các khóa riêng trùng tên ứng dụng trong SOFTWARE và nhánh nhà phát hành của HKCU/HKLM, ở chế độ Registry 32/64-bit. Khóa lồng quá sâu được đưa vào nhóm Chỉ xem.
- Từng giá trị Run/RunOnce có lệnh thực thi trỏ tới đường dẫn ứng dụng đã ghi nhận.
- Từng giá trị AppCompatFlags ở Compatibility Assistant\Store và Layers nếu tên giá trị là đường dẫn executable phù hợp. Đây là quy tắc đối chiếu của AppCare, không phải API cam kết ổn định của Microsoft; không quét/xóa toàn bộ nhánh này.
- Shortcut .lnk trong Start Menu, Desktop và Startup của người dùng hiện tại/toàn máy, đối chiếu đích shortcut.
- App Paths, dịch vụ qua ImagePath và tác vụ theo lịch có hành động executable tương ứng: **chỉ xem**, kiểm tra tiếp bằng Windows Tools.

Tài liệu Microsoft xác định các thư mục chuẩn qua [KNOWNFOLDERID](https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid), các đăng ký khởi động qua [Run và RunOnce](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys), và đăng ký App Paths qua [Application Registration](https://learn.microsoft.com/en-us/windows/win32/shell/app-registration).

Đọc tác vụ theo lịch dựa trên [ITaskFolder.GetTasks](https://learn.microsoft.com/en-us/windows/win32/api/taskschd/nf-taskschd-itaskfolder-gettasks) và [IExecAction.Path](https://learn.microsoft.com/en-us/windows/win32/api/taskschd/nf-taskschd-iexecaction-get_path). AppCare không tự tắt/xóa tác vụ hoặc dịch vụ.

## Giới hạn quét và cách hiểu kết quả

Bản này không quét mù toàn bộ ổ đĩa. Phần thư mục được giới hạn tối đa 30.000 mục, khoảng 45 giây cho phần duyệt thư mục, sâu tối đa khoảng 6 cấp bên dưới mỗi gốc. Shortcut tối đa 5.000 mục mỗi gốc, Registry nhà phát hành tối đa 6.000 khóa mỗi nhánh/view, Task Scheduler tối đa 3.000 tác vụ hoặc khoảng 15 giây. Các giới hạn thời gian được kiểm tra giữa thao tác; một lời gọi Windows đang chờ có thể làm thời gian thực dài hơn. Mục vượt độ sâu không được khảo sát tiếp. Đây không phải bảo đảm đã tìm được mọi dấu vết.

Không đi theo junction/symlink và bỏ qua vùng hệ thống/dùng chung bị bảo vệ. Không xử lý dữ liệu các tài khoản Windows khác, ổ cài tùy chỉnh ngoài các gốc hỗ trợ, driver store, toàn bộ COM, WMI, MUICache, service-hosted DLL hay mọi kiểu tác vụ. Tác vụ chạy trình thông dịch với đường dẫn ứng dụng chỉ nằm trong arguments có thể không được nhận diện. File rời không thuộc thư mục được tìm thấy có thể bị bỏ sót.

Tên thư mục trùng là **gợi ý**, không chứng minh quyền sở hữu. Đường dẫn executable là bằng chứng cụ thể hơn nhưng vẫn có thể liên quan thành phần dùng chung. AppCare không dùng tên nhà phát hành một mình làm lý do xóa cả thư mục nhà phát hành. Thư mục cài trùng/giao với ứng dụng còn cài bị loại khỏi việc đối chiếu đường dẫn rộng và bị chặn khi dọn.

Các giá trị Registry và shortcut được so dấu vân tay với lúc quét; nếu đã thay đổi, phải quét lại. Giá trị được sao lưu trước khi xóa, giữ nguyên khóa cha. Các bản sao lưu này không bao gồm quyền Registry ACL. File được chuyển vào kho trên cùng ổ đĩa nên chưa giải phóng dung lượng. Không có xóa vĩnh viễn kho trong bản 0.6.

## Truy vết bằng Process Monitor khi cần chứng cứ chi tiết hơn

[Process Monitor của Microsoft Sysinternals](https://learn.microsoft.com/en-us/sysinternals/downloads/procmon) ghi lại hoạt động hệ thống file, Registry và tiến trình theo thời gian thực. Dùng công cụ này trước khi cài/chạy ứng dụng để có dữ liệu đối chiếu tốt hơn sau khi gỡ.

Quy trình thực hành đề xuất:

1. Ghi lại trạng thái trước khi cài trong môi trường thử nghiệm. Bật ghi log khi cài và khi chạy những chức năng chính của ứng dụng.
2. Xác định đúng tiến trình và tiến trình con của bộ cài/ứng dụng, lọc hoạt động liên quan và lưu log gốc.
3. Phân biệt thao tác đọc/mở với tạo/ghi thành công. Chỉ việc một đường dẫn xuất hiện trong log không chứng minh ứng dụng tạo hoặc sở hữu nó.
4. Sau khi gỡ, đối chiếu những đường dẫn thực sự được tạo/ghi và vẫn tồn tại; kiểm tra xem có ứng dụng khác sử dụng chúng không.

AppCare 0.6 **chưa có driver theo dõi cài đặt, chưa thu log Procmon trực tiếp và chưa nhập log Procmon tự động**. Hướng dẫn này để đối chiếu thủ công; không biến mọi dòng log thành mục được phép xóa.

## Autorun Manager

AppCare quản lý Run/RunOnce và shortcut Startup. **Tắt (sao lưu)** chuyển mục khỏi vị trí khởi động và lưu vào kho. **Bật lại từ kho** khôi phục chính mục đó, từ chối ghi đè nếu đích đã tồn tại. Không dừng tiến trình đang chạy. Mục toàn máy có thể cần Run as administrator với cùng tài khoản.

**Có đăng ký** không đồng nghĩa đang được Windows cho chạy: Windows có trạng thái Startup riêng. Dùng **Startup của Windows** để xem/chỉnh trạng thái đó. AppCare chưa quản lý mọi cơ chế tự khởi động hoặc mọi tài khoản. [Autoruns của Sysinternals](https://learn.microsoft.com/en-us/sysinternals/downloads/autoruns) là tài liệu/công cụ đối chiếu cho các nhóm rộng hơn như dịch vụ, driver, thành phần Explorer và nhiều cơ chế khác.

## Windows Tools

Tab Windows Tools có 19 mục mở công cụ sẵn có trên máy. Bản Windows thiếu công cụ nào sẽ hiển thị Không có. Mở công cụ không có nghĩa AppCare đã chạy tác vụ sửa chữa của công cụ đó. Riêng System File Checker sẽ hỏi xác nhận trước khi mở lệnh sửa chữa với quyền quản trị.

Tham khảo [System configuration tools in Windows](https://support.microsoft.com/en-us/windows/experience/system-configuration-tools-in-windows).
