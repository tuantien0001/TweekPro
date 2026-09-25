using System;
using System.Collections.Generic;
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

  public const int DriveRing=72, DriveSlot=126, MinTextWidth=360;

  /// <summary>How many drive rings fit to the right of the text block without squeezing it below MinTextWidth.</summary>
  public static int DrivesThatFit(int cardWidth,int drives){
   int scoreRing=Math.Min(128,cardWidth);int room=cardWidth-scoreRing-52-MinTextWidth-24;
   return Math.Max(0,Math.Min(drives,room/DriveSlot));
  }

  /// <summary>Draws the score ring, grade badge, headline and one free-space ring per fixed drive into the given card rectangle.</summary>
  public static Color RowTint(HealthSeverity s){
   Color c=SeverityColor(s);return Color.FromArgb((c.R+255*7)/8,(c.G+255*7)/8,(c.B+255*7)/8);
  }

  public static void Draw(Graphics g,Rectangle card,HealthReport report,string busyText,IList<DriveGauge> drives,string compare){
   var saved=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
   using(var bg=new SolidBrush(Theme.Surface))g.FillRectangle(bg,card);
   using(var border=new Pen(Theme.Border))g.DrawRectangle(border,card.X,card.Y,card.Width-1,card.Height-1);
   int shown=drives==null?0:DrivesThatFit(card.Width,drives.Count);
   int drivesLeft=card.Right-24-shown*DriveSlot;
   if(shown>0){
    using(var divider=new Pen(Theme.Border))g.DrawLine(divider,drivesLeft-12,card.Y+20,drivesLeft-12,card.Bottom-20);
    for(int i=0;i<shown;i++)DrawDrive(g,new Rectangle(drivesLeft+i*DriveSlot,card.Y,DriveSlot,card.Height),drives[i]);
   }
   DrawScore(g,card,shown>0?drivesLeft-24:card.Right,report,busyText,compare);
   g.SmoothingMode=saved;
  }

  /// <summary>One drive: full ring whose filled arc is the used share, letter in the middle, "free / total" caption below.</summary>
  static void DrawDrive(Graphics g,Rectangle slot,DriveGauge d){
   var ringRect=new Rectangle(slot.X+(slot.Width-DriveRing)/2,slot.Y+(slot.Height-DriveRing-42)/2,DriveRing,DriveRing);
   Color accent=SeverityColor(d.Level);
   using(var track=new Pen(Theme.Border,8f))g.DrawEllipse(track,ringRect);
   float sweep=(float)(360.0*d.UsedFraction);
   if(sweep>=1f)using(var arc=new Pen(accent,8f){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawArc(arc,ringRect,-90,Math.Min(359.5f,sweep));
   using(var brush=new SolidBrush(Theme.Text))using(var center=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(d.Name,Theme.Strong,brush,ringRect,center);
   using(var text=new SolidBrush(Theme.Text))using(var muted=new SolidBrush(Theme.Muted))using(var line=Wrap(false)){
    line.Alignment=StringAlignment.Center;
    g.DrawString(Core.L.F("{0} trống",Presentation.BytesLabel(d.FreeBytes)),Theme.Small,text,new RectangleF(slot.X-2,ringRect.Bottom+6,slot.Width+4,18),line);
    g.DrawString("/ "+Presentation.BytesLabel(d.TotalBytes),Theme.Small,muted,new RectangleF(slot.X-2,ringRect.Bottom+22,slot.Width+4,18),line);
   }
  }

  static void DrawScore(Graphics g,Rectangle card,int right,HealthReport report,string busyText,string compare){
   int ring=Math.Min(card.Height-28,128);var ringRect=new Rectangle(card.X+24,card.Y+(card.Height-ring)/2,ring,ring);
   bool has=report!=null;int score=has?report.Score:0;Color accent=has?ScoreColor(score):Theme.Muted;
   using(var track=new Pen(Theme.Border,10f))g.DrawArc(track,ringRect,135,270);
   if(has)using(var arc=new Pen(accent,10f){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawArc(arc,ringRect,135,Math.Max(1f,270f*score/100f));
   using(var big=new Font("Segoe UI Semibold",ring>=110?30f:22f))using(var brush=new SolidBrush(has?Theme.Text:Theme.Muted))using(var muted=new SolidBrush(Theme.Muted)){
    string text=has?score.ToString():"—";var size=g.MeasureString(text,big);
    g.DrawString(text,big,brush,ringRect.X+(ringRect.Width-size.Width)/2,ringRect.Y+(ringRect.Height-size.Height)/2-6);
    var sub=g.MeasureString("/100",Theme.Small);g.DrawString("/100",Theme.Small,muted,ringRect.X+(ringRect.Width-sub.Width)/2,ringRect.Y+(ringRect.Height-size.Height)/2+size.Height-14);
   }
   int x=ringRect.Right+28,y=card.Y+22,w=Math.Max(40,right-x-24);
   if(!has){
    using(var brush=new SolidBrush(Theme.Text))g.DrawString(busyText??Core.L.T("Chưa kiểm tra"),Theme.Section,brush,x,y);
    using(var muted=new SolidBrush(Theme.Muted))using(var wrap=Wrap())g.DrawString(Core.L.T("Tweek Pro tự đo tệp rác, phần còn sót, thư mục rỗng, kho khôi phục, mục khởi động và dung lượng trống khi mở; bấm Kiểm tra ngay để đo lại. Chỉ đọc, không thay đổi gì."),Theme.Body,muted,new RectangleF(x,y+34,w,card.Bottom-y-40),wrap);
    return;
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
    g.DrawString(text,Theme.Small,muted,new RectangleF(x,y,w,18),line);
    if(!String.IsNullOrEmpty(compare))g.DrawString(compare,Theme.Small,muted,new RectangleF(x,y+18,w,18),line);
   }
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
  HealthReport report;string busyText,compare;IList<DriveGauge> drives;
  public HealthGaugePanel(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Canvas;}
  public HealthReport Report { get { return report; } set { report=value;Invalidate(); } }
  public string BusyText { get { return busyText; } set { busyText=value;Invalidate(); } }
  /// <summary>Fixed drives shown as free-space rings; null or empty hides the ring block.</summary>
  public IList<DriveGauge> Drives { get { return drives; } set { drives=value;Invalidate(); } }
  /// <summary>Sentence comparing this check with the previous one; empty on the first check.</summary>
  public string Compare { get { return compare; } set { compare=value;Invalidate(); } }
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);
   var card=new Rectangle(16,12,Math.Max(10,Width-32),Math.Max(10,Height-24));
   HealthRenderer.Draw(e.Graphics,card,report,busyText,drives,compare);
  }
 }
}
