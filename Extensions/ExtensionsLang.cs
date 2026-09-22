using System;
using System.Collections.Generic;

namespace TweekPro.Extensions {
 /// <summary>English strings for the Browser Extensions tab; merged by Core.L (keys must not repeat in other tables).</summary>
 public static class ExtensionsLang {
  public static readonly Dictionary<string,string> Table=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Tiện ích trình duyệt","Browser Extensions"},{"Tiện ích","Extension"},{"Hồ sơ","Profile"},{"Nguồn cài","Installed from"},{"Cài lúc","Installed"},
   {"Mở thư mục tiện ích","Open extension folder"},{"Mở trang quản lý của trình duyệt","Open the browser's extension manager"},
   {"Chỉ đọc: liệt kê tiện ích của Chrome, Edge, Brave, Vivaldi, Opera và Firefox trong mọi hồ sơ, kèm nguồn cài (cửa hàng, ép cài bằng chính sách, cài ngoài) và quyền rộng. Gỡ tiện ích bằng trang quản lý của chính trình duyệt để trạng thái đồng bộ đúng; tiện ích «ép cài bằng chính sách» trên máy cá nhân thường là phần mềm không mong muốn — kiểm tra khóa ExtensionInstallForcelist.","Read-only: lists extensions of Chrome, Edge, Brave, Vivaldi, Opera and Firefox across every profile, with provenance (store, policy-forced, sideloaded) and broad permissions. Remove extensions through the browser's own manager so its state stays consistent; \"policy-forced\" extensions on a personal PC are usually unwanted software — check the ExtensionInstallForcelist key."},
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
