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
    MenuItem(menu,"Sao chép thư mục cài",()=>{Clipboard.SetText(a.Location);return Task.FromResult(0);},!String.IsNullOrWhiteSpace(a.Location));
    MenuItem(menu,"Sao chép lệnh gỡ",()=>{Clipboard.SetText(a.Command??"");return Task.FromResult(0);},!String.IsNullOrWhiteSpace(a.Command));
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
    MenuItem(menu,"Sao chép tên gói",()=>{Clipboard.SetText(a.FullName??"");return Task.FromResult(0);});
   });

   AttachMenu(autorunList,menu=>{
    if(autorunList.SelectedItems.Count==0)return;
    var a=(AutorunEntry)autorunList.SelectedItems[0].Tag;
    MenuItem(menu,"Tắt mục khởi động (sao lưu)",async()=>await DisableAutorun(),a.Item!=null,"đã nằm trong kho",danger:true);
    MenuItem(menu,"Bật lại từ kho",async()=>await EnableAutorun(),a.Saved!=null,"vẫn đang có đăng ký");
    menu.Items.Add(new ToolStripSeparator());
    string target=Advanced.CommandExe(a.Command);if(target==""&&Advanced.LocalPath(a.Command))target=a.Command;
    MenuItem(menu,"Mở thư mục chứa tệp",()=>{OpenInExplorer(target);return Task.FromResult(0);},target!=""&&File.Exists(target));
    MenuItem(menu,"Sao chép vị trí",()=>{Clipboard.SetText(a.Item!=null?Presentation.CandidatePath(a.Item):a.Saved.Original);return Task.FromResult(0);});
    MenuItem(menu,"Sao chép lệnh",()=>{Clipboard.SetText(a.Command??"");return Task.FromResult(0);},!String.IsNullOrWhiteSpace(a.Command));
   });

   AttachMenu(backups,menu=>{
    if(backups.SelectedItems.Count==0)return;
    var b=(Backup)backups.SelectedItems[0].Tag;
    MenuItem(menu,"Khôi phục mục này",async()=>await Restore(),b.State!="Restored","đã khôi phục");
    MenuItem(menu,"Xóa vĩnh viễn mục này",async()=>await PurgeBackups(new List<Backup>{b},b.AppName??b.Original),danger:true);
    menu.Items.Add(new ToolStripSeparator());
    MenuItem(menu,"Mở thư mục sao lưu",()=>{OpenInExplorer(Path.Combine(Engine.VaultOf(b),b.Id));return Task.FromResult(0);},Directory.Exists(Path.Combine(Engine.VaultOf(b),b.Id)));
    MenuItem(menu,"Mở vị trí gốc",()=>{OpenInExplorer(Directory.Exists(b.Original)?b.Original:Path.GetDirectoryName(b.Original));return Task.FromResult(0);},!String.IsNullOrWhiteSpace(b.Original)&&b.Original.Contains("\\"));
    MenuItem(menu,"Sao chép đường dẫn gốc",()=>{Clipboard.SetText(b.Original??"");return Task.FromResult(0);},!String.IsNullOrWhiteSpace(b.Original));
   });
  }
 }
}
