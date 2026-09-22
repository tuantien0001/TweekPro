using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Store;

namespace TweekPro {
 public partial class MainForm {
  /// <summary>Attaches a right-click menu to a list; the builder runs on every opening so items reflect the row under the cursor.</summary>
  void AttachMenu(ListView list,Action<ContextMenuStrip> build){
   var menu=new ContextMenuStrip{ShowImageMargin=false};
   menu.Opening+=(s,e)=>{menu.Items.Clear();build(menu);e.Cancel=menu.Items.Count==0;};
   list.ContextMenuStrip=menu;
  }

  /// <summary>Adds a localized menu item whose action runs through Guard (busy state, error dialog); disabled items show the reason in their text.</summary>
  ToolStripMenuItem MenuItem(ContextMenuStrip menu,string text,Func<Task> action,bool enabled=true,string disabledReason=null,bool danger=false){
   var item=new ToolStripMenuItem(Core.L.T(text)+(enabled||String.IsNullOrEmpty(disabledReason)?"":"  — "+Core.L.T(disabledReason))){Enabled=enabled};
   if(danger)item.ForeColor=Theme.Danger;
   item.Click+=async(s,e)=>await Guard(action);
   menu.Items.Add(item);
   return item;
  }

  /// <summary>Clipboard copy that ignores empty text and a busy clipboard instead of throwing.</summary>
  static void CopyText(string text){
   if(String.IsNullOrEmpty(text))return;
   try{Clipboard.SetText(text);}catch(System.Runtime.InteropServices.ExternalException){}
  }

  /// <summary>Opens an http(s) URL from an Uninstall key in the default browser; anything else is refused so a registry value cannot launch a program.</summary>
  static void OpenWebsite(string url){
   Uri uri;if(!Uri.TryCreate((url??"").Trim(),UriKind.Absolute,out uri)||(uri.Scheme!=Uri.UriSchemeHttp&&uri.Scheme!=Uri.UriSchemeHttps))throw new IOException(Core.L.T("Địa chỉ web không hợp lệ: ")+url);
   Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});
  }

  /// <summary>Points Regedit's LastKey at the entry (WOW6432Node for 32-bit views on 64-bit Windows) and launches it.</summary>
  static void OpenRegedit(string hive,string view,string key){
   string sub=view=="32"&&Environment.Is64BitOperatingSystem?System.Text.RegularExpressions.Regex.Replace(key,"^SOFTWARE\\\\","SOFTWARE\\WOW6432Node\\",System.Text.RegularExpressions.RegexOptions.IgnoreCase):key;
   string full=(hive=="HKLM"?"HKEY_LOCAL_MACHINE\\":"HKEY_CURRENT_USER\\")+sub;
   using(var k=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"))k.SetValue("LastKey",full);
   Process.Start(new ProcessStartInfo("regedit.exe"){UseShellExecute=true});
  }

  static void OpenInExplorer(string path){
   if(String.IsNullOrWhiteSpace(path))return;
   if(File.Exists(path))Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+path+"\""){UseShellExecute=true});
   else if(Directory.Exists(path))Process.Start(new ProcessStartInfo("explorer.exe","\""+path+"\""){UseShellExecute=true});
   else throw new IOException(Core.L.T("Đường dẫn không còn tồn tại: ")+path);
  }

  /// <summary>Right-click menus for the Applications, Windows Apps, Startup and Recovery Vault lists.</summary>
  void BuildListMenus(){
   AttachMenu(apps,menu=>{
    var a=Selected();if(a==null)return;
    MenuItem(menu,"Gỡ ứng dụng này",async()=>{foreach(ListViewItem i in apps.Items)i.Checked=i.Tag==a;await Uninstall();},danger:true);
    MenuItem(menu,"Quét phần còn sót của ứng dụng này",async()=>await ScanSelected());
    menu.Items.Add(new ToolStripSeparator());
    bool hasFolder=!String.IsNullOrWhiteSpace(a.Location)&&Directory.Exists(a.Location);
    MenuItem(menu,"Mở thư mục cài",()=>{OpenInExplorer(a.Location);return Task.FromResult(0);},hasFolder,"chưa khai báo thư mục cài");
    MenuItem(menu,"Sao chép thư mục cài",()=>{CopyText(a.Location);return Task.FromResult(0);},!String.IsNullOrWhiteSpace(a.Location));
    MenuItem(menu,"Sao chép lệnh gỡ",()=>{CopyText(a.Command??"");return Task.FromResult(0);},!String.IsNullOrWhiteSpace(a.Command));
    MenuItem(menu,"Sao chép lệnh gỡ im lặng",()=>{CopyText(a.QuietCommand??"");return Task.FromResult(0);},!String.IsNullOrWhiteSpace(a.QuietCommand),"bộ cài không khai báo");
    MenuItem(menu,"Mở trang web nhà phát hành",()=>{OpenWebsite(a.Website);return Task.FromResult(0);},!String.IsNullOrWhiteSpace(a.Website),"bộ cài không khai báo");
    MenuItem(menu,"Mở khóa Registry trong Regedit",()=>{OpenRegedit(a.Hive,a.View,a.Key);return Task.FromResult(0);});
    menu.Items.Add(new ToolStripSeparator());
    int checkedCount=apps.CheckedItems.Count;
    MenuItem(menu,"Bỏ đánh dấu tất cả",()=>{foreach(ListViewItem i in apps.Items)i.Checked=false;return Task.FromResult(0);},checkedCount>0);
   });

   AttachMenu(storeList,menu=>{
    var a=SelectedStoreApp();if(a==null)return;
    bool protectedApp=a.Status==AppxStatus.Protected;
    MenuItem(menu,"Gỡ gói này",async()=>{foreach(ListViewItem i in storeList.Items)i.Checked=i.Tag==a;await RemoveStoreApps();},!protectedApp,"thành phần được bảo vệ",danger:true);
    menu.Items.Add(new ToolStripSeparator());
    MenuItem(menu,"Mở trong Microsoft Store",()=>{OpenStorePage();return Task.FromResult(0);});
    MenuItem(menu,"Mở thư mục gói",()=>{OpenStoreLocation();return Task.FromResult(0);},!String.IsNullOrWhiteSpace(a.InstallLocation));
    MenuItem(menu,"Sao chép tên gói",()=>{CopyText(a.FullName??"");return Task.FromResult(0);});
   });

   AttachMenu(autorunList,menu=>{
    if(autorunList.SelectedItems.Count==0)return;
    var a=(AutorunEntry)autorunList.SelectedItems[0].Tag;
    MenuItem(menu,"Tắt mục khởi động (sao lưu)",async()=>await DisableAutorun(),a.Item!=null,"đã nằm trong kho",danger:true);
    MenuItem(menu,"Bật lại từ kho",async()=>await EnableAutorun(),a.Saved!=null,"vẫn đang có đăng ký");
    menu.Items.Add(new ToolStripSeparator());
    string target=Advanced.CommandExe(a.Command);if(target==""&&Advanced.LocalPath(a.Command))target=a.Command;
    MenuItem(menu,"Mở thư mục chứa tệp",()=>{OpenInExplorer(target);return Task.FromResult(0);},target!=""&&File.Exists(target));
    MenuItem(menu,"Sao chép vị trí",()=>{CopyText(a.Item!=null?Presentation.CandidatePath(a.Item):a.Saved.Original);return Task.FromResult(0);});
    MenuItem(menu,"Sao chép lệnh",()=>{CopyText(a.Command??"");return Task.FromResult(0);},!String.IsNullOrWhiteSpace(a.Command));
   });

   AttachMenu(backups,menu=>{
    if(backups.SelectedItems.Count==0)return;
    var b=(Backup)backups.SelectedItems[0].Tag;
    MenuItem(menu,"Khôi phục mục này",async()=>await Restore(),b.State!="Restored","đã khôi phục");
    MenuItem(menu,"Xóa vĩnh viễn mục này",async()=>await PurgeBackups(new List<Backup>{b},b.AppName??b.Original),danger:true);
    menu.Items.Add(new ToolStripSeparator());
    MenuItem(menu,"Mở thư mục sao lưu",()=>{OpenInExplorer(Path.Combine(Engine.VaultOf(b),b.Id));return Task.FromResult(0);},Directory.Exists(Path.Combine(Engine.VaultOf(b),b.Id)));
    bool localOriginal=!String.IsNullOrWhiteSpace(b.Original)&&Advanced.LocalPath(b.Original);
    MenuItem(menu,"Mở vị trí gốc",()=>{OpenInExplorer(Directory.Exists(b.Original)?b.Original:Path.GetDirectoryName(b.Original));return Task.FromResult(0);},localOriginal,"không phải đường dẫn tệp");
    MenuItem(menu,"Sao chép đường dẫn gốc",()=>{CopyText(b.Original??"");return Task.FromResult(0);},!String.IsNullOrWhiteSpace(b.Original));
   });
  }
 }
}
