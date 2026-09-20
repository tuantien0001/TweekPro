using System;
using System.IO;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace AppCare {
 public static class Presentation {
  public static string DateLabel(string raw){
   if(String.IsNullOrWhiteSpace(raw))return "Không rõ";
   DateTime date;
   // Compact MSI and ISO formats are unambiguous. Slash dates use installer US convention.
   string[] formats={"yyyyMMdd","yyyy-MM-dd","yyyy/MM/dd","M/d/yyyy","MM/dd/yyyy","d/M/yyyy","dd/MM/yyyy"};
   if(DateTime.TryParseExact(raw.Trim(),formats,CultureInfo.InvariantCulture,DateTimeStyles.None,out date))return date.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture);
   return "Không rõ";
  }
  public static string SizeLabel(long kb){return kb<=0?"Không rõ":kb>=1048576?(kb/1048576.0).ToString("N2")+" GB":(kb/1024.0).ToString("N1")+" MB";}
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern uint ExtractIconEx(string file,int index,out IntPtr large,out IntPtr small,uint count);
  [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
  public static bool ParseIcon(string raw,out string path,out int index){
   path="";index=0;if(String.IsNullOrWhiteSpace(raw))return false;
   string text=Environment.ExpandEnvironmentVariables(raw.Trim());
   var match=Regex.Match(text,@",\s*(-?\d+)\s*$");
   if(match.Success){if(!Int32.TryParse(match.Groups[1].Value,out index))return false;text=text.Substring(0,match.Index);}
   path=text.Trim().Trim('"');
   return Path.IsPathRooted(path)&&!path.StartsWith(@"\\")&&path.Length>2&&path[1]==':';
  }
  public static Bitmap AppIcon(AppEntry app){
   try{string path;int index;
   if(ParseIcon(app.DisplayIcon,out path,out index)&&new DriveInfo(Path.GetPathRoot(path)).DriveType==DriveType.Fixed&&File.Exists(path)&&((int)File.GetAttributes(path)&(0x1000|0x40000|0x400000|0x400))==0){
    IntPtr large=IntPtr.Zero,small=IntPtr.Zero;
    try{
     ExtractIconEx(path,index,out large,out small,1);IntPtr handle=large!=IntPtr.Zero?large:small;
     if(handle!=IntPtr.Zero)using(var icon=(Icon)Icon.FromHandle(handle).Clone())return icon.ToBitmap();
    }catch(ArgumentException){}catch(ExternalException){}finally{if(large!=IntPtr.Zero)DestroyIcon(large);if(small!=IntPtr.Zero)DestroyIcon(small);}
   }
   }catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}catch(ArgumentException){}
   return SystemIcons.Application.ToBitmap();
  }
 }
 public class SmoothListView:ListView { public SmoothListView(){DoubleBuffered=true;} }
}
