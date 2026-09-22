using System;
using System.Collections.Generic;

namespace TweekPro.Extensions {
 /// <summary>English strings for the Browser Extensions tab; merged by Core.L (keys must not repeat in other tables).</summary>
 public static class ExtensionsLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Tiện ích trình duyệt","Browser Extensions"},{"Tiện ích","Extension"},{"Hồ sơ","Profile"},{"Nguồn cài","Installed from"},{"Cài lúc","Installed"},
   {"Mở thư mục tiện ích","Open extension folder"},{"Mở trang quản lý của trình duyệt","Open the browser's extension manager"},
   {"Liệt kê tiện ích của Chrome, Edge, Brave, Vivaldi, Opera và Firefox trong mọi hồ sơ, kèm nguồn cài (cửa hàng, ép cài bằng chính sách, cài ngoài) và quyền rộng. «Gỡ» chỉ chạy khi trình duyệt đã đóng: thư mục tiện ích, mục cấu hình trong Preferences, nguồn cài trong Registry và giá trị ExtensionInstallForcelist đều được lưu vào Kho rồi mới xóa. «Dọn Forcelist» gỡ toàn bộ chính sách ép cài (HKLM + HKCU) — trên máy cá nhân, đó gần như luôn là phần mềm không mong muốn.","Lists extensions of Chrome, Edge, Brave, Vivaldi, Opera and Firefox across every profile, with provenance (store, policy-forced, sideloaded) and broad permissions. \"Remove\" only runs while the browser is closed: the extension folder, its Preferences entry, registry install sources and ExtensionInstallForcelist values are saved to the vault before deletion. \"Clean Forcelist\" removes every forced-install policy (HKLM + HKCU) — on a personal PC that is almost always unwanted software."},
   {"Gỡ tiện ích đã chọn (vào Kho)","Remove selected extensions (to vault)"},{"Dọn Forcelist (ép cài)…","Clean Forcelist (forced installs)…"},
   {"Chọn một hoặc nhiều tiện ích để gỡ.","Select one or more extensions to remove."},{"  (ép cài — sẽ gỡ cả Forcelist)","  (forced — the Forcelist value goes too)"},
   {"Gỡ {0} tiện ích vào Kho khôi phục?\r\n\r\n{1}\r\n\r\nTrình duyệt phải đóng. Thư mục tiện ích, mục cấu hình và chính sách liên quan được lưu lại; khôi phục từ tab Kho khôi phục (nếu trình duyệt từ chối mục cấu hình cũ, cài lại từ cửa hàng).","Remove {0} extensions into the Recovery Vault?\r\n\r\n{1}\r\n\r\nThe browser must be closed. The extension folder, its settings entry and related policies are saved; restore from the Recovery Vault tab (if the browser rejects the old settings entry, reinstall from the store)."},
   {"Đã gỡ tiện ích {0} ({1} / {2}) vào Kho.","Removed extension {0} ({1} / {2}) into the vault."},{"Gỡ tiện ích lỗi: ","Extension removal error: "},{"Đã gỡ {0} tiện ích, {1} lỗi.","Removed {0} extensions, {1} errors."},
   {"\r\n\r\nMở lại trình duyệt để kiểm tra; khôi phục trong tab Kho khôi phục.","\r\n\r\nReopen the browser to check; restore from the Recovery Vault tab."},{"Gỡ tiện ích","Remove extension"},
   {"{0} đang chạy. Đóng {0} ngay để gỡ tiện ích? (Tab đang mở được trình duyệt lưu vào phiên; tệp chưa lưu trên web có thể mất.)","{0} is running. Close {0} now to remove extensions? (Open tabs are saved in the session; unsaved web forms may be lost.)"},
   {"Đóng {0} rồi thử lại.","Close {0} and try again."},{"{0} vẫn còn chạy (có thể đang hỏi lưu). Đóng bằng tay rồi thử lại.","{0} is still running (it may be asking to save). Close it manually and try again."},
   {"Không có chính sách ExtensionInstallForcelist nào trong HKLM/HKCU. Máy này không bị ép cài tiện ích.","There is no ExtensionInstallForcelist policy in HKLM/HKCU. No extension is forced on this PC."},{"Dọn Forcelist","Clean Forcelist"},
   {"  (không có trong hồ sơ nào)","  (present in no profile)"},
   {"Tìm thấy {0} giá trị ép cài tiện ích:\r\n\r\n{1}\r\n\r\nXóa toàn bộ (lưu vào Kho trước)? Trình duyệt sẽ tự gỡ các tiện ích này ở lần mở sau; nếu máy thuộc tổ chức có quản trị, hãy hỏi IT trước.","Found {0} forced-install values:\r\n\r\n{1}\r\n\r\nDelete them all (saved to the vault first)? The browser uninstalls these extensions on its next start; on a company-managed PC ask IT first."},
   {"Đã xóa {0} giá trị ExtensionInstallForcelist (bản sao lưu {1}).","Deleted {0} ExtensionInstallForcelist values (backup {1})."},
   {"Đã xóa {0} giá trị Forcelist. Mở lại trình duyệt để nó gỡ tiện ích bị ép cài; nếu chính sách xuất hiện lại, một tác vụ nền đang ghi lại nó — kiểm tra tab Khởi động và Dịch vụ.","Deleted {0} Forcelist values. Reopen the browser so it uninstalls the forced extensions; if the policy comes back, a background task is rewriting it — check the Startup and Services tabs."},
   {"Chưa chọn tiện ích.","No extension selected."},{"Tiện ích Firefox này không có tệp trong hồ sơ (nhúng sẵn).","This Firefox add-on has no file in the profile (built in)."},
   {"Cài từ registry/thư mục hệ thống — gỡ nguồn cài ở HKLM\\Software\\Mozilla\\Firefox\\Extensions hoặc thư mục cài của Firefox.","Installed from the registry/system folder — remove the source at HKLM\\Software\\Mozilla\\Firefox\\Extensions or in the Firefox install folder."},
   {"Tiện ích thành phần của trình duyệt, không gỡ được.","Built-in component extension; cannot be removed."},{"Mã tiện ích không hợp lệ.","Invalid extension id."},
   {"Chưa tải.\r\nBấm Làm mới để liệt kê tiện ích của các trình duyệt đã cài.","Not loaded yet.\r\nClick Refresh to list the extensions of the installed browsers."},{"Chưa tải.","Not loaded."},
   {"Quyền: ","Permissions: "},
   {"Cửa hàng","Store"},{"Chính sách (ép cài)","Policy (forced)"},{"Giải nén (developer)","Unpacked (developer)"},{"Cài ngoài (registry/pref)","Sideloaded (registry/pref)"},
   {"Hồ sơ người dùng","User profile"},{"Cài ngoài (hệ thống/registry)","Sideloaded (system/registry)"},{"Giao diện","Theme"},{"Từ điển","Dictionary"},{"Gói ngôn ngữ","Language pack"},
   {"Ép cài bằng chính sách — trên máy cá nhân thường là phần mềm không mong muốn","Forced by policy — on a personal PC usually unwanted software"},{"Không cài từ cửa hàng chính thức","Not installed from the official store"},{"Cài từ ngoài Firefox (registry hoặc thư mục hệ thống)","Installed from outside Firefox (registry or system folder)"},
   {"{0} tiện ích trong {1} hồ sơ  •  {2} ép cài bằng chính sách  •  {3} cảnh báo  •  {4} đã tắt","{0} extensions in {1} profiles  •  {2} forced by policy  •  {3} warnings  •  {4} disabled"},
   {"Đang đọc hồ sơ trình duyệt…","Reading browser profiles…"},{"Tiện ích: ","Extensions: "},{"Tiện ích trình duyệt: {0} mục, {1} ép cài bằng chính sách.","Browser extensions: {0} items, {1} forced by policy."},
   {"Không tìm thấy tiện ích nào trong các hồ sơ trình duyệt của tài khoản này.","No extensions found in this account's browser profiles."},
   {"Chọn một tiện ích.","Select an extension."},{"Tiện ích này không có thư mục trên đĩa (được nhúng trong hồ sơ).","This extension has no folder on disk (embedded in the profile)."},
   {"Không tìm thấy tệp chạy của {0}; mở trang tiện ích bằng tay trong trình duyệt.","Could not locate the {0} executable; open the extensions page manually in the browser."},
   {"Chưa có dữ liệu để xuất.","Nothing to export yet."},{"Đã xuất ","Exported "},{"(không tên)","(unnamed)"}
  };
 }
}
