using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.RegClean;

namespace TweekPro {
 public partial class MainForm {
  ListView regList=new SmoothListView();Label regOverlay,regSummary,regStage;TabPage regTab;
  List<RegCategoryResult> regResults=new List<RegCategoryResult>();CancellationTokenSource regCancellation;

  /// <summary>Builds the Registry tab: one row per orphaned entry grouped by category; cleaning saves everything to the vault first.</summary>
  void BuildRegCleanTab(){
   var tab=regTab=new TabPage(Core.L.T("Registry"));tabs.TabPages.Add(tab);
   SetupList(regList,new[]{"Mục Registry","Tệp đích không còn","Tên hiển thị / dữ liệu","Nhánh"},new[]{460,360,300,120},true,true);
   regList.ItemChecked+=(s,e)=>UpdateRegSummary();
   var host=Theme.ListHost(regList,out regOverlay);
   var bar=Bar();
   Add(bar,"Quét Registry",async()=>await ScanRegistry(),ButtonStyle.Primary);
   Add(bar,"Chọn mặc định",()=>{SetRegChecks(true);return Task.FromResult(0);});
   Add(bar,"Bỏ chọn",()=>{SetRegChecks(false);return Task.FromResult(0);});
   Add(bar,"Dọn mục đã chọn (lưu kho)",async()=>await CleanRegistry(),ButtonStyle.Danger);
   Add(bar,"Mở trong Regedit",()=>{OpenRegedit();return Task.FromResult(0);});
   var note=Theme.Note("Registry cleaner bảo thủ: chỉ đề xuất mục mà tệp .exe/.dll đích chắc chắn không còn trên ổ cố định — khóa Uninstall mồ côi, App Paths, đăng ký «Mở bằng», MUICache, SharedDLLs, shell extension mất DLL. Không «tối ưu» Registry, không đụng CLSID, Run, SYSTEM, Policies. Mỗi lần dọn tạo một bản sao lưu trong Kho; khôi phục ghi lại đúng khóa/giá trị đã xóa nếu chưa tồn tại.",NoteKind.Warning);
   regStage=new Label{Dock=DockStyle.Top,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small,AutoEllipsis=true,Visible=false};
   regSummary=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(regSummary);
   tab.Controls.Add(host);tab.Controls.Add(regSummary);tab.Controls.Add(regStage);tab.Controls.Add(note);tab.Controls.Add(bar);
   Theme.SetOverlay(regOverlay,"Chưa quét.\r\nBấm Quét Registry để tìm mục mồ côi theo sáu nhóm. Chỉ đọc cho đến khi bạn đánh dấu, bấm Dọn và xác nhận.",NoteKind.Info);
   UpdateRegSummary();
  }

  void RenderRegistry(){
   regList.BeginUpdate();regList.Items.Clear();regList.Groups.Clear();int order=0;
   foreach(var r in regResults){
    order++;string header=Core.L.T(r.Category.Name)+" ("+r.Count.ToString("N0")+")"+(r.Note==""?"":"  •  "+Core.L.T(r.Note));
    foreach(var f in r.Findings){
     var row=new ListViewItem(new[]{f.Path,f.Target,f.Detail??"",f.Hive+" "+f.View}){Tag=Tuple.Create(r,f),ToolTipText=f.Path+"\r\n"+Core.L.T("Tệp đích: ")+f.Target+"\r\n"+Core.L.T(r.Category.Description)};
     Theme.AssignGroup(regList,row,order.ToString("D2")+"-"+r.Category.Id,header);
     Theme.StripeRow(row,regList.Items.Count);regList.Items.Add(row);row.Checked=r.Category.DefaultChecked;
    }
   }
   regList.EndUpdate();UpdateRegSummary();
  }

  void SetRegChecks(bool value){regList.BeginUpdate();foreach(ListViewItem item in regList.Items){var t=(Tuple<RegCategoryResult,RegFinding>)item.Tag;item.Checked=value&&t.Item1.Category.DefaultChecked;}regList.EndUpdate();UpdateRegSummary();}

  List<RegCategoryResult> CheckedRegistry(){
   var picked=regList.CheckedItems.Cast<ListViewItem>().Select(i=>(Tuple<RegCategoryResult,RegFinding>)i.Tag).ToList();
   return picked.GroupBy(t=>t.Item1).Select(g=>new RegCategoryResult{Category=g.Key.Category,Findings=g.Select(t=>t.Item2).ToList()}).ToList();
  }

  void UpdateRegSummary(){
   int total=regResults.Sum(r=>r.Count);int checkedCount=regList.CheckedItems.Count;
   regSummary.Text=Core.L.F("{0} mục mồ côi trong {1} nhóm  •  Đã đánh dấu {2}  •  Dọn lưu bản sao vào Kho trước khi xóa",total.ToString("N0"),regResults.Count,checkedCount.ToString("N0"));
  }

  async Task ScanRegistry(){
   regCancellation=new CancellationTokenSource();var token=regCancellation.Token;
   regStage.Visible=true;regStage.Text=Core.L.T("Đang quét Registry…");Theme.SetOverlay(regOverlay,null,NoteKind.Info);
   Log(Core.L.T("Registry: đang quét mục mồ côi (chỉ đọc)."));
   try{
    regResults=await Task.Run(()=>RegistryCleaner.Scan(RegistryCleaner.Categories(),token),token);
    RenderRegistry();int total=regResults.Sum(r=>r.Count);
    regStage.Text=Core.L.F("Quét xong: {0} mục mồ côi",total.ToString("N0"));Log(Core.L.F("Registry: {0} mục mồ côi.",total));
    if(total==0)Theme.SetOverlay(regOverlay,"Không tìm thấy mục Registry mồ côi trong sáu nhóm được kiểm tra.",NoteKind.Success);
   }catch(OperationCanceledException){regStage.Text=Core.L.T("Đã dừng quét.");}
   finally{regCancellation.Dispose();regCancellation=null;}
  }

  async Task CleanRegistry(){
   var selected=CheckedRegistry();int count=selected.Sum(r=>r.Count);if(count==0)throw new IOException(Core.L.T("Quét rồi đánh dấu mục muốn dọn."));
   string list=String.Join("\r\n",selected.Select(r=>"• "+Core.L.T(r.Category.Name)+" — "+Core.L.F("{0} mục",r.Count.ToString("N0"))));
   if(!Confirm(Core.L.F("Xóa {0} mục Registry mồ côi?\r\n\r\n{1}\r\n\r\nMỗi nhóm được lưu vào Kho khôi phục trước; tệp đích được kiểm tra lại ngay trước khi xóa.",count.ToString("N0"),list)))return;
   regCancellation=new CancellationTokenSource();var token=regCancellation.Token;regStage.Visible=true;regStage.Text=Core.L.T("Đang dọn Registry…");
   RegCleanReport report;
   try{report=await Task.Run(()=>RegistryCleaner.Clean(selected,token),token);}
   catch(OperationCanceledException){regStage.Text=Core.L.T("Đã dừng dọn.");return;}
   finally{regCancellation.Dispose();regCancellation=null;}
   LoadBackups();
   string summary=Core.L.F("Đã xóa {0} mục Registry. Lỗi: {1}. Bản sao lưu: {2}.",report.Cleaned.ToString("N0"),report.Failed.ToString("N0"),report.Backups.Count);
   regStage.Text=summary;Log(Core.L.T("Registry: ")+summary);
   foreach(string err in report.Errors.Take(20))Log(Core.L.T("Registry lỗi: ")+err);
   MessageBox.Show(this,summary+(report.Errors.Count>0?Core.L.T("\r\n\r\nLỗi đầu tiên:\r\n")+String.Join("\r\n",report.Errors.Take(5)):"")+Core.L.T("\r\n\r\nCó thể khôi phục hoặc xóa vĩnh viễn trong tab Kho khôi phục."),Core.L.T("Kết quả dọn Registry"),MessageBoxButtons.OK,report.Failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
   await ScanRegistry();
  }

  /// <summary>Points regedit at the selected key by setting its LastKey, then launches it.</summary>
  void OpenRegedit(){
   if(regList.SelectedItems.Count==0)throw new IOException(Core.L.T("Chọn một mục để mở trong Regedit."));
   var f=((Tuple<RegCategoryResult,RegFinding>)regList.SelectedItems[0].Tag).Item2;
   string full=(f.Hive=="HKLM"?"HKEY_LOCAL_MACHINE\\":"HKEY_CURRENT_USER\\")+f.Key;
   using(var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"))key.SetValue("LastKey",full);
   System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("regedit.exe"){UseShellExecute=true});
  }

  /// <summary>Fills the tab with illustrative findings for --preview registry; nothing is read from the machine.</summary>
  public void PreviewRegistry(){
   var cats=RegistryCleaner.Categories();
   regResults=cats.Select(c=>new RegCategoryResult{Category=c}).ToList();
   regResults[0].Findings.Add(new RegFinding{Category="uninstall",Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OldTool_is1",Target=@"C:\Program Files\OldTool\unins000.exe",Detail="Old Tool 2.1"});
   regResults[0].Findings.Add(new RegFinding{Category="uninstall",Hive="HKCU",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SampleEditor",Target=@"C:\Users\ADMIN\AppData\Local\SampleEditor\Update.exe",Detail="Sample Editor"});
   regResults[1].Findings.Add(new RegFinding{Category="app-paths",Hive="HKLM",View="64",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\oldtool.exe",Target=@"C:\Program Files\OldTool\oldtool.exe",Detail=@"C:\Program Files\OldTool\oldtool.exe"});
   regResults[3].Findings.Add(new RegFinding{Category="mui-cache",Hive="HKCU",View="64",Key=@"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache",ValueName=@"C:\Program Files\OldTool\oldtool.exe.FriendlyAppName",Target=@"C:\Program Files\OldTool\oldtool.exe",Detail="Old Tool"});
   regResults[4].Findings.Add(new RegFinding{Category="shared-dlls",Hive="HKLM",View="32",Key=@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs",ValueName=@"C:\Program Files (x86)\Common Files\OldVendor\helper.dll",Target=@"C:\Program Files (x86)\Common Files\OldVendor\helper.dll",Detail="DWord"});
   RenderRegistry();Theme.SetOverlay(regOverlay,null,NoteKind.Info);tabs.SelectedTab=regTab;
  }
 }
}
