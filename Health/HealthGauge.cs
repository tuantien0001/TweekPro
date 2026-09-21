using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TweekPro.Health {
 /// <summary>Pure GDI+ rendering of the health score card so the same drawing can be previewed off-screen.</summary>
 public static class HealthRenderer {
  /// <summary>Color that represents a score: green when healthy, shading to red as it drops.</summary>
  public static Color ScoreColor(int score){
   if(score>=75)return Theme.Success;
   if(score>=60)return Color.FromArgb(217,119,6);
   if(score>=40)return Color.FromArgb(234,88,12);
   return Theme.Danger;
  }

  public static Color SeverityColor(HealthSeverity s){
   return s==HealthSeverity.High?Theme.Danger:s==HealthSeverity.Medium?Color.FromArgb(234,88,12):s==HealthSeverity.Low?Color.FromArgb(217,119,6):Theme.Success;
  }

  /// <summary>Draws the score ring, grade badge and headline into the given card rectangle.</summary>
  public static void Draw(Graphics g,Rectangle card,HealthReport report,string busyText){
   var saved=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
   using(var bg=new SolidBrush(Theme.Surface))g.FillRectangle(bg,card);
   using(var border=new Pen(Theme.Border))g.DrawRectangle(border,card.X,card.Y,card.Width-1,card.Height-1);
   int ring=Math.Min(card.Height-28,128);var ringRect=new Rectangle(card.X+24,card.Y+(card.Height-ring)/2,ring,ring);
   bool has=report!=null;int score=has?report.Score:0;Color accent=has?ScoreColor(score):Theme.Muted;
   using(var track=new Pen(Theme.Border,10f))g.DrawArc(track,ringRect,135,270);
   if(has)using(var arc=new Pen(accent,10f){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawArc(arc,ringRect,135,Math.Max(1f,270f*score/100f));
   using(var big=new Font("Segoe UI Semibold",ring>=110?30f:22f))using(var brush=new SolidBrush(has?Theme.Text:Theme.Muted))using(var muted=new SolidBrush(Theme.Muted)){
    string text=has?score.ToString():"—";var size=g.MeasureString(text,big);
    g.DrawString(text,big,brush,ringRect.X+(ringRect.Width-size.Width)/2,ringRect.Y+(ringRect.Height-size.Height)/2-6);
    var sub=g.MeasureString("/100",Theme.Small);g.DrawString("/100",Theme.Small,muted,ringRect.X+(ringRect.Width-sub.Width)/2,ringRect.Y+(ringRect.Height-size.Height)/2+size.Height-14);
   }
   int x=ringRect.Right+28,y=card.Y+22,w=card.Right-x-24;
   if(!has){
    using(var brush=new SolidBrush(Theme.Text))g.DrawString(busyText??Core.L.T("Chưa kiểm tra"),Theme.Section,brush,x,y);
    using(var muted=new SolidBrush(Theme.Muted))using(var wrap=Wrap())g.DrawString(Core.L.T("Bấm Kiểm tra ngay để đo tệp rác, phần còn sót, thư mục rỗng, kho khôi phục, mục khởi động và dung lượng trống. Chỉ đọc, không thay đổi gì."),Theme.Body,muted,new RectangleF(x,y+34,w,card.Bottom-y-40),wrap);
    g.SmoothingMode=saved;return;
   }
   using(var badgeFont=new Font("Segoe UI Semibold",13f))using(var badgeBrush=new SolidBrush(accent))using(var white=new SolidBrush(Color.White)){
    string badge=report.Grade+"  •  "+report.GradeLabel;var bs=g.MeasureString(badge,badgeFont);
    var pill=new Rectangle(x,y,(int)bs.Width+24,(int)bs.Height+8);
    using(var path=Pill(pill))g.FillPath(badgeBrush,path);
    g.DrawString(badge,badgeFont,white,pill.X+12,pill.Y+4);
    y=pill.Bottom+10;
   }
   using(var brush=new SolidBrush(Theme.Text))using(var wrap=Wrap())g.DrawString(report.Headline,Theme.Strong,brush,new RectangleF(x,y,w,44),wrap);
   y+=46;
   using(var muted=new SolidBrush(Theme.Muted))using(var line=Wrap(false)){
    string text=Core.L.F("Có thể giải phóng: {0}   •   {1} khu vực cần chú ý   •   Kiểm tra lúc {2}",Presentation.BytesLabel(report.Reclaimable),report.Issues,report.Generated.ToString("HH:mm dd/MM"));
    g.DrawString(text,Theme.Small,muted,new RectangleF(x,y,w,20),line);
   }
   g.SmoothingMode=saved;
  }

  static StringFormat Wrap(bool multiline=true){
   var f=new StringFormat(StringFormat.GenericDefault){Trimming=StringTrimming.EllipsisCharacter};
   if(!multiline)f.FormatFlags|=StringFormatFlags.NoWrap;
   return f;
  }

  static GraphicsPath Pill(Rectangle r){
   int d=r.Height;var p=new GraphicsPath();
   p.AddArc(r.X,r.Y,d,d,90,180);p.AddArc(r.Right-d,r.Y,d,d,270,180);p.CloseFigure();return p;
  }
 }

 /// <summary>Double-buffered panel that paints the health score card for the current report.</summary>
 public sealed class HealthGaugePanel:Panel {
  HealthReport report;string busyText;
  public HealthGaugePanel(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Canvas;}
  public HealthReport Report { get { return report; } set { report=value;Invalidate(); } }
  public string BusyText { get { return busyText; } set { busyText=value;Invalidate(); } }
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);
   var card=new Rectangle(16,12,Math.Max(10,Width-32),Math.Max(10,Height-24));
   HealthRenderer.Draw(e.Graphics,card,report,busyText);
  }
 }
}
