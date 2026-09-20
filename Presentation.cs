using System;
using System.IO;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace TweekPro {
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
  /// <summary>Formats a sortable ISO timestamp from a backup manifest as a friendly local date and time.</summary>
  public static string StampLabel(string iso){
   DateTime stamp;
   if(DateTime.TryParseExact(iso??"","s",CultureInfo.InvariantCulture,DateTimeStyles.None,out stamp))return stamp.ToString("dd/MM/yyyy HH:mm",CultureInfo.InvariantCulture);
   return iso??"";
  }
  /// <summary>Formats a byte count for display; negative values mean the size is unknown.</summary>
  public static string BytesLabel(long bytes){
   if(bytes<0)return "—";
   if(bytes<1024)return bytes+" B";
   if(bytes<1048576)return (bytes/1024.0).ToString("N1")+" KB";
   if(bytes<1073741824)return (bytes/1048576.0).ToString("N1")+" MB";
   return (bytes/1073741824.0).ToString("N2")+" GB";
  }
  /// <summary>Shortens a long path for single-line status text, keeping the drive and the leaf.</summary>
  public static string ShortPath(string path,int max){
   if(String.IsNullOrEmpty(path)||path.Length<=max)return path??"";
   int keep=Math.Max(8,max/3);
   return path.Substring(0,keep)+"…"+path.Substring(path.Length-(max-keep-1));
  }
  /// <summary>Formats a leftover candidate path, including hive, view and value name when present.</summary>
  public static string CandidatePath(Candidate c){return (c.Hive==null?"":c.Hive+" ["+c.View+"]\\")+c.Path+(c.ValueName==null?"":" :: "+c.ValueName);}
  /// <summary>Returns the Vietnamese kind label shown in leftover lists.</summary>
  public static string KindLabel(Candidate c){
   if(c.ReviewOnly||c.Kind=="Review")return "Chỉ xem";
   if(c.Kind=="Folder")return "Thư mục";
   if(c.Kind=="File")return IsShortcut(c)?"Shortcut":"Tệp";
   if(c.Kind=="RegistryValue")return "Giá trị Registry";
   return "Khóa Registry";
  }
  /// <summary>Returns a sortable group key so leftover rows cluster as folder, file, shortcut, registry, then review-only.</summary>
  public static string KindGroup(Candidate c){
   if(c.ReviewOnly||c.Kind=="Review")return "5-review";
   if(c.Kind=="Folder")return "1-folder";
   if(c.Kind=="File")return IsShortcut(c)?"3-shortcut":"2-file";
   return "4-registry";
  }
  /// <summary>Returns the Vietnamese header for a leftover kind group key.</summary>
  public static string KindGroupHeader(string key){
   if(key=="1-folder")return "Thư mục";
   if(key=="2-file")return "Tệp";
   if(key=="3-shortcut")return "Shortcut";
   if(key=="4-registry")return "Registry";
   return "Chỉ xem";
  }
  /// <summary>True when a leftover note records a permission or unreadable-path failure.</summary>
  public static bool PermissionNote(string note){
   if(String.IsNullOrEmpty(note))return false;
   return note.IndexOf("Không đủ quyền",StringComparison.OrdinalIgnoreCase)>=0||note.IndexOf("Không đọc được",StringComparison.OrdinalIgnoreCase)>=0;
  }
  static bool IsShortcut(Candidate c){return c.Path!=null&&c.Path.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase);}
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
 public enum ButtonStyle { Secondary, Primary, Danger }
 public enum NoteKind { Info, Warning, Success, Error }
 /// <summary>Shared palette, typography and control factories so every window uses the same visual language.</summary>
 public static class Theme {
  public static readonly Color Canvas=Color.FromArgb(244,246,250);
  public static readonly Color Surface=Color.White;
  public static readonly Color Border=Color.FromArgb(226,232,240);
  public static readonly Color Text=Color.FromArgb(30,41,59);
  public static readonly Color Muted=Color.FromArgb(100,116,139);
  public static readonly Color Primary=Color.FromArgb(37,99,235);
  public static readonly Color PrimaryDark=Color.FromArgb(29,78,216);
  public static readonly Color Danger=Color.FromArgb(220,38,38);
  public static readonly Color DangerDark=Color.FromArgb(185,28,28);
  public static readonly Color Header=Color.FromArgb(15,23,42);
  public static readonly Color HeaderText=Color.White;
  public static readonly Color HeaderMuted=Color.FromArgb(148,163,184);
  public static readonly Color Stripe=Color.FromArgb(248,250,252);
  public static readonly Color SoftButton=Color.FromArgb(241,245,249);
  public static readonly Color SoftButtonHover=Color.FromArgb(226,232,240);
  public static readonly Color Success=Color.FromArgb(22,163,74);
  public static readonly Font Body=new Font("Segoe UI",10f);
  public static readonly Font Small=new Font("Segoe UI",9f);
  public static readonly Font Strong=new Font("Segoe UI",10f,FontStyle.Bold);
  public static readonly Font Section=new Font("Segoe UI",12f,FontStyle.Bold);
  public static readonly Font Title=new Font("Segoe UI Semibold",20f);
  public static readonly Font Mono=new Font("Consolas",9.5f);

  /// <summary>Creates a flat button with the given semantic style and hover feedback.</summary>
  public static Button Button(string text,ButtonStyle style){
   var b=new Button{Text=text,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlatStyle=FlatStyle.Flat,Font=Body,Cursor=Cursors.Hand,Margin=new Padding(0,0,8,0),Padding=new Padding(12,0,12,0),MinimumSize=new Size(0,36),UseVisualStyleBackColor=false};
   b.FlatAppearance.BorderSize=1;
   if(style==ButtonStyle.Primary){b.BackColor=Primary;b.ForeColor=Color.White;b.FlatAppearance.BorderColor=Primary;b.FlatAppearance.MouseOverBackColor=PrimaryDark;b.FlatAppearance.MouseDownBackColor=PrimaryDark;}
   else if(style==ButtonStyle.Danger){b.BackColor=Danger;b.ForeColor=Color.White;b.FlatAppearance.BorderColor=Danger;b.FlatAppearance.MouseOverBackColor=DangerDark;b.FlatAppearance.MouseDownBackColor=DangerDark;}
   else {b.BackColor=Surface;b.ForeColor=Text;b.FlatAppearance.BorderColor=Border;b.FlatAppearance.MouseOverBackColor=SoftButton;b.FlatAppearance.MouseDownBackColor=SoftButtonHover;}
   b.EnabledChanged+=(s,e)=>{if(style==ButtonStyle.Secondary)b.ForeColor=b.Enabled?Text:Muted;};
   return b;
  }
  /// <summary>Creates a top-docked toolbar that wraps its buttons and grows vertically when needed.</summary>
  public static FlowLayoutPanel Toolbar(){
   var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(16,12,8,4),BackColor=Surface};
   BorderBottom(bar);
   return bar;
  }
  /// <summary>Creates a tinted, single-purpose message strip such as a warning or a result summary.</summary>
  public static Label Note(string text,NoteKind kind){
   var note=new Label{Text=text,Dock=DockStyle.Top,AutoSize=false,Height=52,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,Font=Body};
   Tint(note,kind);
   return note;
  }
  /// <summary>Applies the background and foreground colors that correspond to a note kind.</summary>
  public static void Tint(Control control,NoteKind kind){
   if(kind==NoteKind.Warning){control.BackColor=Color.FromArgb(255,251,235);control.ForeColor=Color.FromArgb(146,64,14);}
   else if(kind==NoteKind.Success){control.BackColor=Color.FromArgb(240,253,244);control.ForeColor=Color.FromArgb(22,101,52);}
   else if(kind==NoteKind.Error){control.BackColor=Color.FromArgb(254,242,242);control.ForeColor=Color.FromArgb(153,27,27);}
   else {control.BackColor=Color.FromArgb(239,246,255);control.ForeColor=Color.FromArgb(30,64,175);}
  }
  /// <summary>Creates the dark header band with a title and an explanatory subtitle.</summary>
  public static Panel HeaderBand(string title,string subtitle,int height){
   var band=new Panel{Dock=DockStyle.Top,Height=height,BackColor=Header,Padding=new Padding(28,18,28,0)};
   var sub=new Label{Text=subtitle,Dock=DockStyle.Top,Height=24,Font=Body,ForeColor=HeaderMuted,AutoEllipsis=true};
   var main=new Label{Text=title,Dock=DockStyle.Top,Height=38,Font=Title,ForeColor=HeaderText,AutoEllipsis=true};
   band.Controls.Add(sub);
   band.Controls.Add(main);
   return band;
  }
  /// <summary>Creates a bottom status strip with a top border.</summary>
  public static Label StatusBar(){
   var status=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Surface,ForeColor=Muted,Font=Small,AutoEllipsis=true};
   BorderTop(status);
   return status;
  }
  /// <summary>Applies list styling shared by all detail views.</summary>
  public static void StyleList(ListView list){
   list.Dock=DockStyle.Fill;list.View=View.Details;list.FullRowSelect=true;list.HideSelection=false;list.GridLines=false;list.BorderStyle=BorderStyle.None;list.MultiSelect=false;
   list.BackColor=Surface;list.ForeColor=Text;list.Font=Body;list.ShowItemToolTips=true;
  }
  /// <summary>Paints a one pixel divider along the bottom edge of a control.</summary>
  public static void BorderBottom(Control control){
   control.Paint+=(s,e)=>{using(var pen=new Pen(Border))e.Graphics.DrawLine(pen,0,control.Height-1,control.Width,control.Height-1);};
  }
  /// <summary>Paints a one pixel divider along the top edge of a control.</summary>
  public static void BorderTop(Control control){
   control.Paint+=(s,e)=>{using(var pen=new Pen(Border))e.Graphics.DrawLine(pen,0,0,control.Width,0);};
  }
  /// <summary>Applies an alternating background to list rows to improve scanning of long lists.</summary>
  public static void StripeRow(ListViewItem item,int index){item.BackColor=index%2==0?Surface:Stripe;}
  /// <summary>Creates a fill host that can show either a list or a centered empty/loading/error overlay.</summary>
  public static Panel ListHost(Control list,out Label overlay){
   var host=new Panel{Dock=DockStyle.Fill,BackColor=Surface};
   overlay=new Label{Dock=DockStyle.Fill,Visible=false,TextAlign=ContentAlignment.MiddleCenter,Font=Body,Padding=new Padding(48,24,48,24)};
   list.Dock=DockStyle.Fill;
   host.Controls.Add(list);
   host.Controls.Add(overlay);
   return host;
  }
  /// <summary>Shows a tinted overlay above a list, or hides it when text is null or empty.</summary>
  public static void SetOverlay(Label overlay,string text,NoteKind kind){
   if(overlay==null)return;
   if(String.IsNullOrEmpty(text)){overlay.Visible=false;return;}
   overlay.Text=text;Tint(overlay,kind);overlay.Visible=true;overlay.BringToFront();
  }
  /// <summary>Places a row into a named group, creating the group in kind-key order if needed.</summary>
  public static void AssignGroup(ListView list,ListViewItem item,string key,string header){
   list.ShowGroups=true;
   ListViewGroup group=null;
   foreach(ListViewGroup existing in list.Groups)if(existing.Name==key){group=existing;break;}
   if(group==null){
    group=new ListViewGroup(key,header);
    int index=0;
    while(index<list.Groups.Count&&String.Compare(list.Groups[index].Name,key,StringComparison.Ordinal)<0)index++;
    list.Groups.Insert(index,group);
   }
   item.Group=group;
  }
 }
}
