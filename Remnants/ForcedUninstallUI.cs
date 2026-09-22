using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.Remnants;

namespace TweekPro {
 /// <summary>Dialog that collects a name plus optional folder / executable for a program without an Uninstall entry.</summary>
 public sealed class ForcedUninstallForm:Form {
  readonly TextBox name=new TextBox(),publisher=new TextBox(),folder=new TextBox(),exe=new TextBox();
  readonly IEnumerable<AppEntry> inventory;
  public AppEntry Result;
  public string Folder { get { return folder.Text.Trim().Trim('"'); } }

  public ForcedUninstallForm(IEnumerable<AppEntry> installed){
   inventory=installed;
   Text=Core.L.T("Gỡ cưỡng bức — ứng dụng không còn đăng ký");Size=new Size(640,400);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
   Font=Theme.Body;BackColor=Theme.Surface;ForeColor=Theme.Text;
   var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=5,Padding=new Padding(16,12,16,8),AutoSize=true};
   grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,96));
   Row(grid,0,"Tên ứng dụng *",name,null);
   Row(grid,1,"Nhà phát hành",publisher,null);
   Row(grid,2,"Thư mục cài",folder,()=>{using(var d=new FolderBrowserDialog{Description=Core.L.T("Chọn thư mục cài của ứng dụng"),ShowNewFolderButton=false}){if(d.ShowDialog(this)==DialogResult.OK)folder.Text=d.SelectedPath;}});
   Row(grid,3,"Tệp .exe chính",exe,()=>{using(var d=new OpenFileDialog{Filter="Executable (*.exe)|*.exe",Title=Core.L.T("Chọn tệp .exe của ứng dụng")}){if(d.ShowDialog(this)==DialogResult.OK)exe.Text=d.FileName;}});
   var note=Theme.Note("Dùng khi bộ cài đã hỏng, ứng dụng portable hoặc đã gỡ dở nên không còn trong danh sách. Tweek Pro quét sâu theo tên, thư mục và tệp .exe rồi liệt kê thư mục, khóa Registry, shortcut, mục khởi động liên quan để bạn duyệt; mọi thao tác xóa vẫn đi qua Kho khôi phục. Tên ngắn hoặc chung cần kèm thư mục hoặc .exe.",NoteKind.Info);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(16,8,16,12),AutoSize=true,BackColor=Theme.Canvas};
   var cancel=Theme.Button("Hủy",ButtonStyle.Secondary);cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};
   var ok=Theme.Button("Quét phần còn sót",ButtonStyle.Primary);ok.Click+=(s,e)=>Accept();
   buttons.Controls.Add(cancel);buttons.Controls.Add(ok);
   Controls.Add(grid);Controls.Add(note);Controls.Add(buttons);
   AcceptButton=ok;CancelButton=cancel;
  }

  void Row(TableLayoutPanel grid,int row,string label,TextBox box,Action browse){
   var l=new Label{Text=Core.L.T(label),AutoSize=true,Margin=new Padding(0,10,8,0),ForeColor=Theme.Muted,Font=Theme.Small};
   box.Dock=DockStyle.Top;box.Margin=new Padding(0,6,8,6);box.Font=Theme.Body;box.BorderStyle=BorderStyle.FixedSingle;
   grid.Controls.Add(l,0,row);grid.Controls.Add(box,1,row);
   if(browse!=null){var b=Theme.Button("Chọn…",ButtonStyle.Secondary);b.Margin=new Padding(0,4,0,4);b.Click+=(s,e)=>browse();grid.Controls.Add(b,2,row);}
  }

  void Accept(){
   try{Result=ForcedUninstall.Build(name.Text,folder.Text,exe.Text,publisher.Text,inventory);DialogResult=DialogResult.OK;Close();}
   catch(Exception e){MessageBox.Show(this,e.Message,Core.L.T("Gỡ cưỡng bức"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
 }

 public partial class MainForm {
  /// <summary>Asks for the program's identity, then runs the deep-scan review window exactly like a post-uninstall scan.</summary>
  Task ForceUninstall(){
   AppEntry app;string folder;
   using(var dialog=new ForcedUninstallForm(inventory)){if(dialog.ShowDialog(this)!=DialogResult.OK||dialog.Result==null)return Task.FromResult(0);app=dialog.Result;folder=dialog.Folder;}
   Remember(new[]{app});
   Log(Core.L.F("Gỡ cưỡng bức: quét sâu «{0}»{1}{2}.",app.Name,app.Location==""?"":Core.L.T(", thư mục ")+app.Location,app.KnownExecutables.Count==0?"":Core.L.F(", {0} tệp .exe đối chiếu",app.KnownExecutables.Count)));
   ReviewLeftovers(new[]{app},true);
   if(folder!=""&&app.Location=="")PresentCandidates(candidates.Concat(new[]{ForcedUninstall.ReviewFolder(app,folder)}));
   if(candidates.Count>0)tabs.SelectedTab=remnantsTab;
   return Task.FromResult(0);
  }
 }
}
