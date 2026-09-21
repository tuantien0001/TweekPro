using System;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TweekPro {
 /// <summary>
 /// Code-drawn visual identity for Tweek Pro: a rounded gradient app logo (window/taskbar icon and header mark)
 /// and small vector glyphs for each tab, so the app never falls back to the default WinForms look.
 /// </summary>
 public static class Branding {
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
  [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern IntPtr SendMessage(IntPtr hWnd,int msg,IntPtr wParam,IntPtr lParam);
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
  const int WM_SETICON=0x80,ICON_SMALL=0,ICON_BIG=1;
  /// <summary>Distinct from older builds so Windows 11 taskbar drops a stale shell-icon cache entry for the same exe path.</summary>
  public const string AppUserModelId="TweekPro.App.0.7.tpc";

  static readonly Color LogoTop=Color.FromArgb(56,132,255);
  static readonly Color LogoBottom=Color.FromArgb(29,78,216);

  /// <summary>Compact header button: a globe glyph with the two-letter language code; clicking it switches the UI language.</summary>
  public static Button LanguageButton(string code,string tooltip){
   var b=new Button{Size=new Size(64,32),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,BackColor=Color.FromArgb(51,65,85),ForeColor=Color.White,Text="",TabStop=false,UseVisualStyleBackColor=false};
   b.FlatAppearance.BorderSize=0;b.FlatAppearance.MouseOverBackColor=Color.FromArgb(71,85,105);b.FlatAppearance.MouseDownBackColor=Color.FromArgb(30,41,59);
   b.Paint+=(s,e)=>DrawLanguageGlyph(e.Graphics,b.ClientRectangle,code,b.ForeColor);
   new ToolTip().SetToolTip(b,tooltip);
   b.AccessibleName=tooltip;
   return b;
  }

  /// <summary>Draws a small globe (meridian + equator) followed by the language code, centered in r.</summary>
  public static void DrawLanguageGlyph(Graphics g,Rectangle r,string code,Color color){
   g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
   int d=Math.Min(r.Height-12,18);
   using(var font=new Font("Segoe UI Semibold",9.5f))using(var brush=new SolidBrush(color))using(var pen=new Pen(color,1.6f)){
    var text=g.MeasureString(code,font);
    int total=d+6+(int)Math.Ceiling(text.Width);
    int x=r.X+(r.Width-total)/2,y=r.Y+(r.Height-d)/2;
    var globe=new Rectangle(x,y,d,d);
    g.DrawEllipse(pen,globe);
    g.DrawEllipse(pen,new RectangleF(x+d*0.3f,y,d*0.4f,d));
    g.DrawLine(pen,x,y+d/2f,x+d,y+d/2f);
    g.DrawLine(pen,x+d*0.12f,y+d*0.25f,x+d*0.88f,y+d*0.25f);
    g.DrawLine(pen,x+d*0.12f,y+d*0.75f,x+d*0.88f,y+d*0.75f);
    g.DrawString(code,font,brush,x+d+6,r.Y+(r.Height-text.Height)/2f);
   }
  }

  static readonly Lazy<Bitmap> AppLogo=new Lazy<Bitmap>(()=>{
   using(var stream=typeof(Branding).Assembly.GetManifestResourceStream("TweekPro.Branding.png"))
   using(var source=new Bitmap(stream))return new Bitmap(source);
  });

  static readonly Lazy<byte[]> AppIconBytes=new Lazy<byte[]>(()=>{
   using(var stream=typeof(Branding).Assembly.GetManifestResourceStream("TweekPro.Branding.ico"))
   using(var buffer=new MemoryStream()){stream.CopyTo(buffer);return buffer.ToArray();}
  });

  /// <summary>Draws the approved T + PC artwork from its embedded PNG resource.</summary>
  public static void DrawLogo(Graphics g,Rectangle r){
   var state=g.Save();
   try{
    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    int side=Math.Min(r.Width,r.Height);
    g.DrawImage(AppLogo.Value,new Rectangle(r.X+(r.Width-side)/2,r.Y+(r.Height-side)/2,side,side));
   }finally{g.Restore(state);}
  }

  /// <summary>Exports the approved multi-size icon without regenerating the old code-drawn logo.</summary>
  public static void WriteIconFile(string path){
   File.WriteAllBytes(path,AppIconBytes.Value);
  }

  /// <summary>Returns an independently owned icon at the requested Windows display size from the approved ICO frame (PNG entries, via GetHicon — System.Drawing.Icon cannot decode PNG-compressed ICO on .NET Framework).</summary>
  public static Icon AppIcon(int size){
   using(var bitmap=FrameBitmap(size)){
    IntPtr handle=bitmap.GetHicon();
    try{using(var temporary=Icon.FromHandle(handle))return (Icon)temporary.Clone();}
    finally{DestroyIcon(handle);}
   }
  }

  /// <summary>Registers a stable AppUserModelID before any window is created so the taskbar groups this process under the TPC branding identity.</summary>
  public static void RegisterAppUserModelId(){
   try{SetCurrentProcessExplicitAppUserModelID(AppUserModelId);}catch(Exception){}
  }

  /// <summary>
  /// Applies the approved icon to a form and forces both ICON_SMALL and ICON_BIG via WM_SETICON.
  /// Windows 11 ignores a 32px Form.Icon for the taskbar when UI scaling is high; ICON_SMALL must carry a large (256) frame.
  /// Caller owns the returned icons and must dispose them when the form closes.
  /// </summary>
  public static void ApplyWindowIcons(Form form,out Icon small,out Icon large){
   // ICON_SMALL uses 256 on purpose (Win11 taskbar quirk); title bar still scales down cleanly.
   Icon smallIcon=AppIcon(256),largeIcon=AppIcon(Math.Max(32,SystemInformation.IconSize.Width));
   small=smallIcon;large=largeIcon;
   form.Icon=largeIcon;form.ShowIcon=true;
   Action apply=()=>{
    if(!form.IsHandleCreated)return;
    SendMessage(form.Handle,WM_SETICON,(IntPtr)ICON_SMALL,smallIcon.Handle);
    SendMessage(form.Handle,WM_SETICON,(IntPtr)ICON_BIG,largeIcon.Handle);
   };
   if(form.IsHandleCreated)apply();
   else form.HandleCreated+=(s,e)=>apply();
   form.Shown+=(s,e)=>apply();
  }

  static Bitmap FrameBitmap(int size){
   byte[] data=AppIconBytes.Value;
   if(data.Length>=6&&BitConverter.ToUInt16(data,0)==0&&BitConverter.ToUInt16(data,2)==1){
    int count=BitConverter.ToUInt16(data,4),best=-1,bestDelta=int.MaxValue;
    for(int i=0;i<count;i++){
     int entry=6+16*i;int width=data[entry]==0?256:data[entry];
     int delta=Math.Abs(width-size);if(delta<bestDelta){bestDelta=delta;best=i;}
    }
    if(best>=0){
     int entry=6+16*best;int length=BitConverter.ToInt32(data,entry+8),offset=BitConverter.ToInt32(data,entry+12);
     if(offset>=0&&length>8&&offset+length<=data.Length&&data[offset]==0x89){
      using(var stream=new MemoryStream(data,offset,length,writable:false))
      using(var source=new Bitmap(stream)){
       if(source.Width==size&&source.Height==size)return new Bitmap(source);
       var scaled=new Bitmap(size,size);
       using(var g=Graphics.FromImage(scaled)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,0,0,size,size);}
       return scaled;
      }
     }
    }
   }
   var bitmap=new Bitmap(size,size);
   using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);DrawLogo(g,new Rectangle(0,0,size,size));}
   return bitmap;
  }

  /// <summary>Draws a small monochrome glyph identifying a tab, chosen from the tab's title.</summary>
  public static void DrawTabGlyph(Graphics g,string tabTitle,Rectangle r,Color color){
   var saved=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;
   using(var pen=new Pen(color,2f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
   using(var brush=new SolidBrush(color)){
    switch(GlyphKey(tabTitle)){
     case "pulse":Pulse(g,r,pen);break;
     case "apps":Apps(g,r,pen,brush);break;
     case "windows":Windows(g,r,brush);break;
     case "magnifier":Magnifier(g,r,pen);break;
     case "trash":Trash(g,r,pen);break;
     case "folder":Folder(g,r,pen);break;
     case "duplicate":Duplicate(g,r,pen);break;
     case "download":Download(g,r,pen);break;
     case "chart":Chart(g,r,brush);break;
     case "shield":Shield(g,r,pen);break;
     case "bolt":Bolt(g,r,brush);break;
     case "globe":Globe(g,r,pen);break;
     case "gear":Gear(g,r,pen,brush);break;
     case "doc":Document(g,r,pen);break;
     case "layers":Layers(g,r,pen,brush);break;
     case "spark":Spark(g,r,brush);break;
     default:g.FillEllipse(brush,r.X+r.Width/2-3,r.Y+r.Height/2-3,6,6);break;
    }
   }
   g.SmoothingMode=saved;
  }

  /// <summary>Maps a Vietnamese tab title to a glyph key.</summary>
  public static string GlyphKey(string title){
   string t=title??"";
   Func<string,string,bool> has=(vi,en)=>t.IndexOf(vi,StringComparison.OrdinalIgnoreCase)>=0||t.IndexOf(en,StringComparison.OrdinalIgnoreCase)>=0;
   if(has("Tổng quan","Overview"))return "pulse";
   if(has("Ứng dụng Windows","Windows Apps"))return "windows";
   if(has("Ứng dụng","Applications"))return "apps";
   if(has("còn sót","Leftovers"))return "magnifier";
   if(has("trùng","Duplicates"))return "duplicate";
   if(has("tải về cũ","Old Downloads"))return "download";
   if(has("rác","Junk"))return "trash";
   if(has("rỗng","Empty"))return "folder";
   if(has("Phân tích","Analyzer"))return "chart";
   if(has("Kho","Vault"))return "shield";
   if(has("Autorun","Startup")||t.IndexOf("Khởi động",StringComparison.OrdinalIgnoreCase)>=0)return "bolt";
   if(has("Mạng","Network"))return "globe";
   if(has("Dịch vụ hệ thống","Services"))return "layers";
   if(has("Trợ lý AI","AI Assistant"))return "spark";
   if(has("Tools","Công cụ"))return "gear";
   if(has("Nhật ký","Log"))return "doc";
   return "dot";
  }

  static GraphicsPath Rounded(Rectangle r,int radius){
   int d=Math.Max(1,radius*2);var path=new GraphicsPath();
   if(d>=r.Width||d>=r.Height){path.AddEllipse(r);path.CloseFigure();return path;}
   path.AddArc(r.X,r.Y,d,d,180,90);
   path.AddArc(r.Right-d,r.Y,d,d,270,90);
   path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);
   path.AddArc(r.X,r.Bottom-d,d,d,90,90);
   path.CloseFigure();return path;
  }

  /// <summary>Arrow pointing down into a tray: the Old Downloads tab.</summary>
  static void Download(Graphics g,Rectangle r,Pen pen){
   int cx=r.X+r.Width/2;int top=r.Y+1,tip=r.Bottom-5;
   g.DrawLine(pen,cx,top,cx,tip);
   g.DrawLine(pen,cx-4,tip-4,cx,tip);g.DrawLine(pen,cx+4,tip-4,cx,tip);
   g.DrawLine(pen,r.X+1,r.Bottom-2,r.Right-1,r.Bottom-2);
  }
  static void Pulse(Graphics g,Rectangle r,Pen pen){
   int mid=r.Y+r.Height/2;int w=r.Width;
   var points=new[]{new Point(r.X,mid),new Point(r.X+w*3/10,mid),new Point(r.X+w*4/10,r.Y+2),new Point(r.X+w*55/100,r.Bottom-2),new Point(r.X+w*65/100,mid),new Point(r.Right,mid)};
   g.DrawLines(pen,points);
  }
  /// <summary>Four-pane window mark used for Windows inbox/Store applications.</summary>
  static void Windows(Graphics g,Rectangle r,Brush brush){
   int gap=Math.Max(1,r.Width/9);int w=(r.Width-gap)/2,h=(r.Height-gap)/2;
   int x=r.X+(r.Width-(2*w+gap))/2,y=r.Y+(r.Height-(2*h+gap))/2;
   g.FillRectangle(brush,x,y,w,h);g.FillRectangle(brush,x+w+gap,y,w,h);g.FillRectangle(brush,x,y+h+gap,w,h);g.FillRectangle(brush,x+w+gap,y+h+gap,w,h);
  }

  /// <summary>Opaque-background fallback icon for a package without a readable logo; caller owns the bitmap.</summary>
  public static Bitmap WindowsAppGlyph(int size,Color color){
   var bmp=new Bitmap(size,size);
   using(var g=Graphics.FromImage(bmp)){
    g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);
    using(var back=new SolidBrush(Color.FromArgb(241,245,249)))using(var path=Rounded(new Rectangle(0,0,size-1,size-1),size/5))g.FillPath(back,path);
    int inset=size/4;using(var brush=new SolidBrush(color))Windows(g,new Rectangle(inset,inset,size-2*inset,size-2*inset),brush);
   }
   return bmp;
  }

  static void Folder(Graphics g,Rectangle r,Pen pen){
   int top=r.Y+3,tab=r.Y+r.Height/4;
   var body=new Rectangle(r.X,tab,r.Width,r.Bottom-tab);
   using(var path=Rounded(body,2))g.DrawPath(pen,path);
   g.DrawLines(pen,new[]{new Point(r.X+1,tab),new Point(r.X+1,top),new Point(r.X+r.Width*2/5,top),new Point(r.X+r.Width/2,tab)});
  }
  static void Apps(Graphics g,Rectangle r,Pen pen,Brush brush){
   int s=(r.Width-4)/2;int gap=4;
   for(int i=0;i<2;i++)for(int j=0;j<2;j++){
    var cell=new Rectangle(r.X+i*(s+gap),r.Y+j*(s+gap),s,s);
    using(var path=Rounded(cell,2))g.FillPath(brush,path);
   }
  }
  static void Magnifier(Graphics g,Rectangle r,Pen pen){
   int d=r.Width*3/5;g.DrawEllipse(pen,r.X,r.Y,d,d);
   g.DrawLine(pen,r.X+d-2,r.Y+d-2,r.Right,r.Bottom);
  }
  static void Trash(Graphics g,Rectangle r,Pen pen){
   int top=r.Y+r.Height/5;
   g.DrawLine(pen,r.X,top,r.Right,top);
   g.DrawLine(pen,r.X+r.Width/3,top,r.X+r.Width/3,r.Y+2);
   g.DrawLine(pen,r.X+r.Width*2/3,top,r.X+r.Width*2/3,r.Y+2);
   var body=new Rectangle(r.X+2,top,r.Width-4,r.Bottom-top);
   g.DrawLine(pen,body.X,body.Y,body.X+2,body.Bottom);
   g.DrawLine(pen,body.Right,body.Y,body.Right-2,body.Bottom);
   g.DrawLine(pen,body.X+2,body.Bottom,body.Right-2,body.Bottom);
   g.DrawLine(pen,r.X+r.Width/2,top+3,r.X+r.Width/2,body.Bottom-3);
  }
  static void Duplicate(Graphics g,Rectangle r,Pen pen){
   int s=r.Width*3/5;
   var back=new Rectangle(r.X,r.Y,s,s);var front=new Rectangle(r.Right-s,r.Bottom-s,s,s);
   using(var b=Rounded(back,2))g.DrawPath(pen,b);
   using(var f=Rounded(front,2))g.DrawPath(pen,f);
  }
  static void Chart(Graphics g,Rectangle r,Brush brush){
   int w=(r.Width-4)/3;int[] heights={r.Height/2,r.Height,r.Height*3/4};
   for(int i=0;i<3;i++){int h=heights[i];g.FillRectangle(brush,r.X+i*(w+2),r.Bottom-h,w,h);}
  }
  static void Shield(Graphics g,Rectangle r,Pen pen){
   var pts=new[]{new Point(r.X+r.Width/2,r.Y),new Point(r.Right,r.Y+r.Height/4),new Point(r.X+r.Width/2,r.Bottom),new Point(r.X,r.Y+r.Height/4)};
   g.DrawPolygon(pen,pts);
  }
  static void Bolt(Graphics g,Rectangle r,Brush brush){
   var pts=new[]{new PointF(r.X+r.Width*0.55f,r.Y),new PointF(r.X+r.Width*0.15f,r.Y+r.Height*0.58f),new PointF(r.X+r.Width*0.45f,r.Y+r.Height*0.58f),new PointF(r.X+r.Width*0.4f,r.Bottom),new PointF(r.X+r.Width*0.85f,r.Y+r.Height*0.42f),new PointF(r.X+r.Width*0.55f,r.Y+r.Height*0.42f)};
   g.FillPolygon(brush,pts);
  }
  static void Globe(Graphics g,Rectangle r,Pen pen){
   g.DrawEllipse(pen,r.X,r.Y,r.Width,r.Height);
   g.DrawEllipse(pen,r.X+r.Width/3,r.Y,r.Width/3,r.Height);
   g.DrawLine(pen,r.X,r.Y+r.Height/2,r.Right,r.Y+r.Height/2);
  }
  static void Gear(Graphics g,Rectangle r,Pen pen,Brush brush){
   int inset=3;var circle=new Rectangle(r.X+inset,r.Y+inset,r.Width-inset*2,r.Height-inset*2);
   g.DrawEllipse(pen,circle);
   float cx=r.X+r.Width/2f,cy=r.Y+r.Height/2f,outer=r.Width/2f,inner=r.Width/2f-inset;
   for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,cx+(float)Math.Cos(a)*inner,cy+(float)Math.Sin(a)*inner,cx+(float)Math.Cos(a)*outer,cy+(float)Math.Sin(a)*outer);}
   g.FillEllipse(brush,cx-2,cy-2,4,4);
  }
  static void Layers(Graphics g,Rectangle r,Pen pen,Brush brush){
   int h=Math.Max(3,r.Height/4),gap=Math.Max(1,(r.Height-h*3)/2);
   for(int i=0;i<3;i++){var row=new Rectangle(r.X+1,r.Y+i*(h+gap),r.Width-2,h);g.DrawRectangle(pen,row);g.FillEllipse(brush,row.Right-h+1,row.Y+h/2f-1.5f,3,3);}
  }
  static void Spark(Graphics g,Rectangle r,Brush brush){
   float cx=r.X+r.Width/2f,cy=r.Y+r.Height/2f,R=r.Width/2f,k=r.Width/7f;
   var pts=new[]{new PointF(cx,cy-R),new PointF(cx+k,cy-k),new PointF(cx+R,cy),new PointF(cx+k,cy+k),new PointF(cx,cy+R),new PointF(cx-k,cy+k),new PointF(cx-R,cy),new PointF(cx-k,cy-k)};
   g.FillPolygon(brush,pts);
   float s=R/2.6f,sx=r.Right-s,sy=r.Y+s;
   g.FillPolygon(brush,new[]{new PointF(sx,sy-s),new PointF(sx+s/3,sy-s/3),new PointF(sx+s,sy),new PointF(sx+s/3,sy+s/3),new PointF(sx,sy+s),new PointF(sx-s/3,sy+s/3),new PointF(sx-s,sy),new PointF(sx-s/3,sy-s/3)});
  }
  static void Document(Graphics g,Rectangle r,Pen pen){
   int fold=r.Width/3;
   var pts=new[]{new Point(r.X,r.Y),new Point(r.Right-fold,r.Y),new Point(r.Right,r.Y+fold),new Point(r.Right,r.Bottom),new Point(r.X,r.Bottom)};
   g.DrawPolygon(pen,pts);
   for(int i=1;i<=3;i++){int y=r.Y+fold+ (r.Height-fold)*i/4;g.DrawLine(pen,r.X+3,y,r.Right-3,y);}
  }
 }
}
