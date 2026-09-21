using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using UpdateCheck=TweekPro.Update.UpdateCheck;
using UpdateInfo=TweekPro.Update.UpdateInfo;

namespace TweekPro {
 public partial class MainForm {
  Panel updateBanner;Label updateBannerText;UpdateInfo pendingUpdate;

  /// <summary>Opt-in GitHub update check: notifies and opens the release/download page; never overwrites Program Files.</summary>
  async Task CheckForUpdates(){
   Log(Core.L.T("Đang kiểm tra cập nhật trên GitHub…"));
   var info=await Task.Run(()=>UpdateCheck.Check(Version));
   if(!String.IsNullOrEmpty(info.Error)){
    Log(Core.L.T("Không kiểm tra được cập nhật: ")+Core.L.T(info.Error));
    MessageBox.Show(this,Core.L.T("Không kết nối được GitHub Releases.\r\n\r\n")+Core.L.T(info.Error)+"\r\n\r\n"+Core.L.T("Cập nhật cần tải bản mới và khởi động lại; ứng dụng không tự ghi đè Program Files."),Core.L.T("Kiểm tra cập nhật"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
    return;
   }
   if(!info.UpdateAvailable){
    Log(Core.L.F("Đang dùng bản mới nhất ({0}).",info.Current));
    MessageBox.Show(this,Core.L.F("Bạn đang dùng Tweek Pro {0}.\r\nKhông có bản phát hành mới hơn trên GitHub.",info.Current)+"\r\n\r\n"+Core.L.T("Cập nhật cần tải bản mới và khởi động lại; ứng dụng không tự ghi đè Program Files."),Core.L.T("Kiểm tra cập nhật"),MessageBoxButtons.OK,MessageBoxIcon.Information);
    return;
   }
   Log(Core.L.F("Có bản mới: {0} (đang dùng {1}).",info.Latest,info.Current));
   ShowUpdateBanner(info);
   string body=String.IsNullOrWhiteSpace(info.Notes)?"":"\r\n\r\n"+Core.L.T(info.Notes.Trim());
   if(body.Length>600)body=body.Substring(0,600)+"…";
   var choice=MessageBox.Show(this,Core.L.F("Có bản phát hành mới: {0}\r\nBạn đang dùng {1}.{2}\r\n\r\nMở trang tải trên GitHub?",info.Latest,info.Current,body),Core.L.T("Có cập nhật mới"),MessageBoxButtons.YesNo,MessageBoxIcon.Information);
   if(choice!=DialogResult.Yes)return;
   OpenUpdatePage(info);
  }

  /// <summary>Silent startup probe (one HTTPS request, off the UI thread): shows the Overview banner when a newer release exists; never blocks or pops a dialog.</summary>
  async Task AutoCheckForUpdates(){
   if(!settings.UpdateAutoCheck)return;
   try{
    var info=await Task.Run(()=>UpdateCheck.Check(Version));
    if(!String.IsNullOrEmpty(info.Error)){Log(Core.L.T("Không kiểm tra được cập nhật: ")+Core.L.T(info.Error));return;}
    if(!info.UpdateAvailable){Log(Core.L.F("Đang dùng bản mới nhất ({0}).",info.Current));return;}
    Log(Core.L.F("Có bản mới: {0} (đang dùng {1}).",info.Latest,info.Current));
    if(UpdateCheck.ShouldNotify(info,settings.UpdateSkipVersion))ShowUpdateBanner(info);
   }catch(Exception e){Core.Log.Warn("Update: auto check failed: "+e.Message);}
  }

  /// <summary>Builds the hidden banner once (top of the Overview tab); text and buttons are filled by ShowUpdateBanner.</summary>
  Panel BuildUpdateBanner(){
   updateBanner=new Panel{Dock=DockStyle.Top,Height=52,Padding=new Padding(16,8,12,8),Visible=false};
   Theme.Tint(updateBanner,NoteKind.Success);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Right,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,BackColor=Color.Transparent,Margin=new Padding(0)};
   var download=Theme.Button("Tải bản mới",ButtonStyle.Primary);download.Click+=(s,e)=>{if(pendingUpdate!=null)OpenUpdatePage(pendingUpdate);};
   var skip=Theme.Button("Bỏ qua bản này",ButtonStyle.Secondary);skip.Margin=new Padding(0);
   skip.Click+=(s,e)=>{if(pendingUpdate!=null){settings.UpdateSkipVersion=pendingUpdate.Latest;SaveSettings();Log(Core.L.F("Đã bỏ qua thông báo cho bản {0}.",pendingUpdate.Latest));}updateBanner.Visible=false;};
   buttons.Controls.Add(download);buttons.Controls.Add(skip);
   updateBannerText=new Label{Dock=DockStyle.Fill,AutoSize=false,TextAlign=ContentAlignment.MiddleLeft,Font=Theme.Strong,BackColor=Color.Transparent,ForeColor=updateBanner.ForeColor,AutoEllipsis=true};
   updateBanner.Controls.Add(updateBannerText);updateBanner.Controls.Add(buttons);
   return updateBanner;
  }

  void ShowUpdateBanner(UpdateInfo info){
   pendingUpdate=info;if(updateBanner==null)return;
   updateBannerText.Text=Core.L.F("Có bản Tweek Pro {0} mới (đang dùng {1}). Tải bộ cài để cập nhật — ứng dụng không tự ghi đè.",info.Latest,info.Current);
   updateBanner.Visible=true;
  }

  void OpenUpdatePage(UpdateInfo info){
   string url=!String.IsNullOrEmpty(info.InstallerUrl)?info.InstallerUrl:info.PageUrl;
   if(!String.IsNullOrEmpty(url))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});
  }
 }
}
