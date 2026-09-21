using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TweekPro {
 /// <summary>
 /// Code-drawn visual identity for Tweek Pro: a rounded gradient app logo (window/taskbar icon and header mark)
 /// and small vector glyphs for each tab, so the app never falls back to the default WinForms look.
 /// </summary>
 public static class Branding {
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

  static readonly Color LogoTop=Color.FromArgb(56,132,255);
  static readonly Color LogoBottom=Color.FromArgb(29,78,216);

  /// <summary>Draws the app logo (rounded gradient badge with a white monogram) into the given rectangle.</summary>
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

  public static void DrawLogo(Graphics g,Rectangle r){
   var saved=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;
   int radius=Math.Max(4,r.Width/4);
   using(var path=Rounded(r,radius))
   using(var fill=new LinearGradientBrush(r,LogoTop,LogoBottom,LinearGradientMode.Vertical)){
    g.FillPath(fill,path);
    // A soft top highlight gives the badge a little depth.
    var highlight=new Rectangle(r.X,r.Y,r.Width,r.Height/2);
    using(var glossPath=Rounded(highlight,radius))
    using(var gloss=new SolidBrush(Color.FromArgb(38,255,255,255)))g.FillPath(gloss,glossPath);
   }
   // A stylised "T" monogram plus a spark dot, drawn as vector shapes so it scales cleanly.
   using(var white=new SolidBrush(Color.White)){
    float unit=r.Width/16f;
    var bar=new RectangleF(r.X+unit*3.5f,r.Y+unit*4f,unit*9f,unit*2.1f);
    var stem=new RectangleF(r.X+unit*6.95f,r.Y+unit*4f,unit*2.1f,unit*8f);
    g.FillRectangle(white,bar);g.FillRectangle(white,stem);
    using(var spark=new SolidBrush(Color.FromArgb(191,219,254)))
     g.FillEllipse(spark,r.X+unit*10.5f,r.Y+unit*9.5f,unit*2.4f,unit*2.4f);
   }
   g.SmoothingMode=saved;
  }

  /// <summary>Renders the logo into a managed icon of the requested square size for the window and taskbar.</summary>
  public static Icon AppIcon(int size){
   using(var bitmap=new Bitmap(size,size)){
    using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);DrawLogo(g,new Rectangle(0,0,size,size));}
    IntPtr handle=bitmap.GetHicon();
    try{using(var temporary=Icon.FromHandle(handle))return (Icon)temporary.Clone();}
    finally{DestroyIcon(handle);}
   }
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
     case "chart":Chart(g,r,brush);break;
     case "shield":Shield(g,r,pen);break;
     case "bolt":Bolt(g,r,brush);break;
     case "globe":Globe(g,r,pen);break;
     case "gear":Gear(g,r,pen,brush);break;
     case "doc":Document(g,r,pen);break;
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
   if(has("rác","Junk"))return "trash";
   if(has("rỗng","Empty"))return "folder";
   if(has("Phân tích","Analyzer"))return "chart";
   if(has("Kho","Vault"))return "shield";
   if(has("Autorun","Startup")||t.IndexOf("Khởi động",StringComparison.OrdinalIgnoreCase)>=0)return "bolt";
   if(has("Mạng","Network"))return "globe";
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
  static void Document(Graphics g,Rectangle r,Pen pen){
   int fold=r.Width/3;
   var pts=new[]{new Point(r.X,r.Y),new Point(r.Right-fold,r.Y),new Point(r.Right,r.Y+fold),new Point(r.Right,r.Bottom),new Point(r.X,r.Bottom)};
   g.DrawPolygon(pen,pts);
   for(int i=1;i<=3;i++){int y=r.Y+fold+ (r.Height-fold)*i/4;g.DrawLine(pen,r.X+3,y,r.Right-3,y);}
  }
 }
}
