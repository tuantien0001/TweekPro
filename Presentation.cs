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
   if(String.IsNullOrWhiteSpace(raw))return Core.L.T("Không rõ");
   DateTime date;
   // Compact MSI and ISO formats are unambiguous. Slash dates use installer US convention.
   string[] formats={"yyyyMMdd","yyyy-MM-dd","yyyy/MM/dd","M/d/yyyy","MM/dd/yyyy","d/M/yyyy","dd/MM/yyyy"};
   if(DateTime.TryParseExact(raw.Trim(),formats,CultureInfo.InvariantCulture,DateTimeStyles.None,out date))return date.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture);
   return Core.L.T("Không rõ");
  }
  public static string SizeLabel(long kb){return kb<=0?Core.L.T("Không rõ"):kb>=1048576?(kb/1048576.0).ToString("N2")+" GB":(kb/1024.0).ToString("N1")+" MB";}
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
  /// <summary>Returns the localized kind label shown in leftover lists.</summary>
  public static string KindLabel(Candidate c){
   if(c.ReviewOnly||c.Kind=="Review")return Core.L.T("Chỉ xem");
   if(c.Kind=="Folder")return Core.L.T("Thư mục");
   if(c.Kind=="File")return IsShortcut(c)?"Shortcut":Core.L.T("Tệp");
   if(c.Kind=="RegistryValue")return Core.L.T("Giá trị Registry");
   return Core.L.T("Khóa Registry");
  }
  /// <summary>Returns a sortable group key so leftover rows cluster as folder, file, shortcut, registry, then review-only.</summary>
  public static string KindGroup(Candidate c){
   if(c.ReviewOnly||c.Kind=="Review")return "5-review";
   if(c.Kind=="Folder")return "1-folder";
   if(c.Kind=="File")return IsShortcut(c)?"3-shortcut":"2-file";
   return "4-registry";
  }
  /// <summary>Returns the localized header for a leftover kind group key.</summary>
  public static string KindGroupHeader(string key){
   if(key=="1-folder")return Core.L.T("Thư mục");
   if(key=="2-file")return Core.L.T("Tệp");
   if(key=="3-shortcut")return "Shortcut";
   if(key=="4-registry")return "Registry";
   return Core.L.T("Chỉ xem");
  }
  /// <summary>True when a leftover note records a permission or unreadable-path failure.</summary>
  public static bool PermissionNote(string note){
   if(String.IsNullOrEmpty(note))return false;
   return note.IndexOf("Không đủ quyền",StringComparison.OrdinalIgnoreCase)>=0||note.IndexOf("Không đọc được",StringComparison.OrdinalIgnoreCase)>=0;
  }
  static bool IsShortcut(Candidate c){return c.Path!=null&&c.Path.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase);}
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern uint ExtractIconEx(string file,int index,out IntPtr large,out IntPtr small,uint count);
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHGetFileInfo(string path,uint attributes,ref ShFileInfo info,uint size,uint flags);
  [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
  const uint ShgfiIcon=0x100,ShgfiSmallIcon=0x1,ShgfiLargeIcon=0x0;
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct ShFileInfo{
   public IntPtr hIcon;public int iIcon;public uint dwAttributes;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string szDisplayName;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string szTypeName;
  }
  public static bool ParseIcon(string raw,out string path,out int index){
   path="";index=0;if(String.IsNullOrWhiteSpace(raw))return false;
   string text=Environment.ExpandEnvironmentVariables(raw.Trim());
   var match=Regex.Match(text,@",\s*(-?\d+)\s*$");
   if(match.Success){if(!Int32.TryParse(match.Groups[1].Value,out index))return false;text=text.Substring(0,match.Index);}
   path=text.Trim().Trim('"');
   return Path.IsPathRooted(path)&&!path.StartsWith(@"\\")&&path.Length>2&&path[1]==':';
  }
  /// <summary>True when a path is a local fixed-drive file safe to open for icon extraction.</summary>
  static bool SafeIconFile(string path){
   try{return new DriveInfo(Path.GetPathRoot(path)).DriveType==DriveType.Fixed&&File.Exists(path)&&((int)File.GetAttributes(path)&(0x1000|0x40000|0x400000|0x400))==0;}
   catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}
  }
  /// <summary>Scales a logo into a square of the given size, keeping aspect ratio and centering on a transparent canvas.</summary>
  public static Bitmap FitIcon(Image source,int size){
   var bmp=new Bitmap(size,size);
   using(var g=Graphics.FromImage(bmp)){
    g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(Color.Transparent);
    double scale=Math.Min((double)size/source.Width,(double)size/source.Height);
    int w=Math.Max(1,(int)Math.Round(source.Width*scale)),h=Math.Max(1,(int)Math.Round(source.Height*scale));
    g.DrawImage(source,new Rectangle((size-w)/2,(size-h)/2,w,h));
   }
   return bmp;
  }
  /// <summary>Loads the native Windows icon for an exe/cpl/msc/dll path (ExtractIconEx, then shell association for .msc), scaled for an ImageList.</summary>
  public static Bitmap ShellIcon(string raw,int size){
   int side=Math.Max(16,size);Bitmap rawIcon=null;
   try{string path;int index;
   if(ParseIcon(raw,out path,out index)&&SafeIconFile(path)){
    IntPtr large=IntPtr.Zero,small=IntPtr.Zero;
    try{
     ExtractIconEx(path,index,out large,out small,1);IntPtr handle=large!=IntPtr.Zero?large:small;
     if(handle!=IntPtr.Zero)using(var icon=(Icon)Icon.FromHandle(handle).Clone())rawIcon=icon.ToBitmap();
    }catch(ArgumentException){}catch(ExternalException){}finally{if(large!=IntPtr.Zero)DestroyIcon(large);if(small!=IntPtr.Zero)DestroyIcon(small);}
    if(rawIcon==null){
     var info=new ShFileInfo();uint flags=ShgfiIcon|(side<=16?ShgfiSmallIcon:ShgfiLargeIcon);
     try{
      SHGetFileInfo(path,0,ref info,(uint)Marshal.SizeOf(typeof(ShFileInfo)),flags);
      if(info.hIcon!=IntPtr.Zero)using(var icon=(Icon)Icon.FromHandle(info.hIcon).Clone())rawIcon=icon.ToBitmap();
     }catch(ArgumentException){}catch(ExternalException){}
     finally{if(info.hIcon!=IntPtr.Zero)DestroyIcon(info.hIcon);}
    }
   }
   }catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}catch(ArgumentException){}catch(DllNotFoundException){}
   if(rawIcon!=null){using(rawIcon)return FitIcon(rawIcon,side);}
   using(var fallback=SystemIcons.Application.ToBitmap())return FitIcon(fallback,side);
  }
  public static Bitmap AppIcon(AppEntry app){
   try{string path;int index;
   if(ParseIcon(app.DisplayIcon,out path,out index)&&SafeIconFile(path)){
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
 /// <summary>
 /// Double-buffered ListView that also swallows the ItemCheck/ItemChecked notifications Windows raises while the handle is being
 /// created. A checkbox list filled before its tab is first shown receives one notification per row at that moment, and
 /// <c>Items</c> still returns null for rows not yet inserted natively, so summary handlers would crash on a null item.
 /// </summary>
 public class SmoothListView:ListView {
  bool creatingHandle;
  public SmoothListView(){DoubleBuffered=true;}
  protected override void OnHandleCreated(EventArgs e){creatingHandle=true;try{base.OnHandleCreated(e);}finally{creatingHandle=false;}}
  protected override void OnItemCheck(ItemCheckEventArgs e){if(!creatingHandle)base.OnItemCheck(e);}
  protected override void OnItemChecked(ItemCheckedEventArgs e){if(!creatingHandle)base.OnItemChecked(e);}
 }
 /// <summary>
 /// Fixed-order tab header. Windows' multiline TabControl moves the row that holds the selected tab down next to the page, so with
 /// three rows the tabs appear to shuffle on every click; this strip paints every tab in a stable left-to-right grid and drives a
 /// TabControl whose native headers are hidden.
 /// </summary>
 public sealed class TabStrip:Control {
  readonly TabControl tabs;int hover=-1,tabWidth=172,tabHeight=46,edge=8;
  public TabStrip(TabControl target){
   tabs=target;DoubleBuffered=true;ResizeRedraw=true;Dock=DockStyle.Top;BackColor=Theme.Canvas;Height=tabHeight+1;
   tabs.SelectedIndexChanged+=(s,e)=>Invalidate();
   tabs.ControlAdded+=(s,e)=>{var page=e.Control as TabPage;if(page!=null)page.TextChanged+=(s2,e2)=>Invalidate();Relayout();Invalidate();};
   tabs.ControlRemoved+=(s,e)=>{Relayout();Invalidate();};
   foreach(TabPage page in tabs.TabPages)page.TextChanged+=(s,e)=>Invalidate();
  }
  /// <summary>Hides the native headers of a TabControl so only the strip is visible; pages keep working with SelectedTab/SelectedIndex.</summary>
  public static void HideNativeHeaders(TabControl t){t.Appearance=TabAppearance.FlatButtons;t.ItemSize=new Size(0,1);t.SizeMode=TabSizeMode.Fixed;t.Multiline=false;t.DrawMode=TabDrawMode.Normal;t.Padding=new Point(0,0);}
  int Columns{get{return Math.Max(1,(Width-2*edge)/tabWidth);}}
  int Rows{get{return Math.Max(1,(tabs.TabCount+Columns-1)/Columns);}}
  Rectangle CellAt(int index){int c=index%Columns,r=index/Columns;return new Rectangle(edge+c*tabWidth,r*tabHeight,tabWidth,tabHeight);}
  int IndexAt(Point p){for(int i=0;i<tabs.TabCount;i++)if(CellAt(i).Contains(p))return i;return -1;}
  void Relayout(){int h=Rows*tabHeight+1;if(Height!=h)Height=h;}
  protected override void ScaleControl(SizeF factor,BoundsSpecified specified){base.ScaleControl(factor,specified);tabWidth=(int)Math.Round(tabWidth*factor.Width);tabHeight=(int)Math.Round(tabHeight*factor.Height);edge=(int)Math.Round(edge*factor.Width);Relayout();}
  protected override void OnResize(EventArgs e){base.OnResize(e);Relayout();}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int i=IndexAt(e.Location);if(i!=hover){hover=i;Cursor=i>=0?Cursors.Hand:Cursors.Default;Invalidate();}}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=-1;Invalidate();}
  protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);int i=IndexAt(e.Location);if(i>=0&&i!=tabs.SelectedIndex)tabs.SelectedIndex=i;}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(BackColor);int glyph=(int)Math.Round(18*tabHeight/46.0);
   for(int i=0;i<tabs.TabCount;i++){
    var b=CellAt(i);bool active=i==tabs.SelectedIndex;string text=tabs.TabPages[i].Text;
    using(var bg=new SolidBrush(active?Theme.Surface:i==hover?Theme.SoftButton:Theme.Canvas))e.Graphics.FillRectangle(bg,b);
    using(var sep=new Pen(Theme.Border))e.Graphics.DrawLine(sep,b.Right-1,b.Top+10,b.Right-1,b.Bottom-10);
    Color accent=active?Theme.Primary:Theme.Muted;var glyphRect=new Rectangle(b.X+16,b.Y+(b.Height-glyph)/2-1,glyph,glyph);
    Branding.DrawTabGlyph(e.Graphics,text,glyphRect,accent);
    var textRect=new Rectangle(glyphRect.Right+8,b.Y,b.Right-glyphRect.Right-14,b.Height);
    TextRenderer.DrawText(e.Graphics,text,active?Theme.Strong:Theme.Body,textRect,accent,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    if(active)using(var line=new Pen(Theme.Primary,3))e.Graphics.DrawLine(line,b.Left+16,b.Bottom-2,b.Right-16,b.Bottom-2);
   }
   using(var border=new Pen(Theme.Border))e.Graphics.DrawLine(border,0,Height-1,Width,Height-1);
  }
 }
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
  public static readonly Color Warning=Color.FromArgb(180,83,9);
  public static readonly Font Body=new Font("Segoe UI",10f);
  public static readonly Font Small=new Font("Segoe UI",9f);
  public static readonly Font Strong=new Font("Segoe UI",10f,FontStyle.Bold);
  public static readonly Font Section=new Font("Segoe UI",12f,FontStyle.Bold);
  public static readonly Font Title=new Font("Segoe UI Semibold",20f);
  public static readonly Font Mono=new Font("Consolas",9.5f);

  /// <summary>Creates a flat button with the given semantic style and hover feedback.</summary>
  public static Button Button(string text,ButtonStyle style){
   var b=new Button{Text=Core.L.T(text),AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlatStyle=FlatStyle.Flat,Font=Body,Cursor=Cursors.Hand,Margin=new Padding(0,0,8,0),Padding=new Padding(12,0,12,0),MinimumSize=new Size(0,36),UseVisualStyleBackColor=false};
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
   var note=new Label{Text=Core.L.T(text),Dock=DockStyle.Top,AutoSize=false,Height=52,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,Font=Body};
   Tint(note,kind);
   EventHandler fit=(s,e)=>FitNoteHeight(note);note.Resize+=fit;note.TextChanged+=fit;
   return note;
  }
  /// <summary>Grows a note beyond its two-line default when the text wraps to more lines at the current width, so long notes are never clipped.</summary>
  static void FitNoteHeight(Label note){
   if(note.Width<=note.Padding.Horizontal+40||String.IsNullOrEmpty(note.Text))return;
   int width=note.Width-note.Padding.Horizontal;
   var size=TextRenderer.MeasureText(note.Text,note.Font,new Size(width,Int32.MaxValue),TextFormatFlags.WordBreak);
   int wanted=Math.Max(52,size.Height+16);
   if(note.Height!=wanted)note.Height=wanted;
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
   var band=new Panel{Dock=DockStyle.Top,Height=height,BackColor=Header,Padding=new Padding(92,18,28,0)};
   int logo=44;
   band.Paint+=(s,e)=>Branding.DrawLogo(e.Graphics,new Rectangle(28,(height-logo)/2,logo,logo));
   var sub=new Label{Text=Core.L.T(subtitle),Dock=DockStyle.Top,Height=24,Font=Body,ForeColor=HeaderMuted,AutoEllipsis=true};
   var main=new Label{Text=Core.L.T(title),Dock=DockStyle.Top,Height=38,Font=Title,ForeColor=HeaderText,AutoEllipsis=true};
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
   overlay.Text=Core.L.T(text);Tint(overlay,kind);overlay.Visible=true;overlay.BringToFront();
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
