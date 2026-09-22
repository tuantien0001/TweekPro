using System;
using System.Collections.Generic;

namespace TweekPro.Remnants {
 /// <summary>English strings for forced uninstall; merged by Core.L (keys must not repeat in other tables).</summary>
 public static class RemnantsLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Gỡ cưỡng bức…","Forced uninstall…"},{"Gỡ cưỡng bức","Forced uninstall"},
   {"Gỡ cưỡng bức — ứng dụng không còn đăng ký","Forced uninstall — program without an Uninstall entry"},
   {"Tên ứng dụng *","Program name *"},{"Thư mục cài","Install folder"},{"Tệp .exe chính","Main .exe file"},{"Chọn…","Browse…"},
   {"Chọn thư mục cài của ứng dụng","Select the program's install folder"},{"Chọn tệp .exe của ứng dụng","Select the program's .exe file"},
   {"Dùng khi bộ cài đã hỏng, ứng dụng portable hoặc đã gỡ dở nên không còn trong danh sách. Tweek Pro quét sâu theo tên, thư mục và tệp .exe rồi liệt kê thư mục, khóa Registry, shortcut, mục khởi động liên quan để bạn duyệt; mọi thao tác xóa vẫn đi qua Kho khôi phục. Tên ngắn hoặc chung cần kèm thư mục hoặc .exe.","For programs whose installer is broken, portable apps, or half-removed programs missing from the list. Tweek Pro deep-scans by name, folder and .exe, then lists related folders, registry keys, shortcuts and startup entries for review; every deletion still goes through the Recovery Vault. Short or generic names need a folder or .exe."},
   {"Gỡ cưỡng bức: quét sâu «{0}»{1}{2}.","Forced uninstall: deep scan of \"{0}\"{1}{2}."},{", thư mục ",", folder "},{", {0} tệp .exe đối chiếu",", {0} .exe files to match"},
   {"Chưa chọn thư mục.","No folder selected."},{"Đường dẫn thư mục không hợp lệ.","Invalid folder path."},
   {"Không nhận gốc ổ đĩa làm thư mục ứng dụng.","A drive root cannot be a program folder."},{"Thư mục nằm trong Windows; không phải ứng dụng.","The folder is inside Windows; not a program."},
   {"Nhập tên ứng dụng (ít nhất 3 ký tự, không chứa ký tự cấm).","Enter the program name (at least 3 characters, no forbidden characters)."},
   {"Tên quá ngắn hoặc quá chung để quét một mình; hãy chọn thêm thư mục cài hoặc tệp .exe của ứng dụng.","The name is too short or too generic to scan on its own; add the install folder or the program's .exe."},
   {"Thư mục bạn chỉ định nằm ngoài vùng Tweek Pro tự dọn (Program Files, AppData, ProgramData); xóa thủ công trong Explorer sau khi đã kiểm tra.","The folder you picked is outside the area Tweek Pro cleans itself (Program Files, AppData, ProgramData); delete it manually in Explorer after checking."}
  };
 }
}
