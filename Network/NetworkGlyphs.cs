using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TweekPro.Network {
 /// <summary>Draws directional network packet indicators used as row icons in the network application view.</summary>
 public static class NetworkGlyphs {
  static readonly Color PacketOut=Color.FromArgb(37,99,235);
  static readonly Color PacketIn=Color.FromArgb(22,163,74);
  static readonly Color PacketIdle=Color.FromArgb(148,163,184);

  /// <summary>Draws a packet indicator: "out" up arrow, "in" down arrow, "both", "listen" ring or idle dot.</summary>
  public static void Draw(Graphics g,Rectangle r,string kind){
   var saved=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;
   switch(kind){
    case "out":Arrow(g,r,true,PacketOut);break;
    case "in":Arrow(g,r,false,PacketIn);break;
    case "both":
     Arrow(g,new Rectangle(r.X,r.Y,r.Width/2,r.Height),true,PacketOut);
     Arrow(g,new Rectangle(r.X+r.Width/2,r.Y,r.Width/2,r.Height),false,PacketIn);
     break;
    case "listen":
     using(var pen=new Pen(PacketIdle,1.6f)){int d=Math.Min(r.Width,r.Height)-4;g.DrawEllipse(pen,r.X+2,r.Y+2,d,d);}
     using(var b=new SolidBrush(PacketIdle))g.FillEllipse(b,r.X+r.Width/2-2,r.Y+r.Height/2-2,4,4);
     break;
    default:
     using(var b=new SolidBrush(PacketIdle))g.FillEllipse(b,r.X+r.Width/2-3,r.Y+r.Height/2-3,6,6);
     break;
   }
   g.SmoothingMode=saved;
  }

  /// <summary>Renders one packet icon into a transparent square bitmap for a WinForms ImageList.</summary>
  public static Bitmap Icon(string kind,int size){
   var bmp=new Bitmap(size,size);
   using(var g=Graphics.FromImage(bmp)){g.Clear(Color.Transparent);Draw(g,new Rectangle(1,1,size-2,size-2),kind);}
   return bmp;
  }

  static void Arrow(Graphics g,Rectangle r,bool up,Color color){
   float cx=r.X+r.Width/2f;float top=r.Y+2,bottom=r.Bottom-2;float half=Math.Max(2.5f,r.Width/5f);
   using(var brush=new SolidBrush(color))
   using(var pen=new Pen(color,Math.Max(1.6f,r.Width/9f)){StartCap=LineCap.Round,EndCap=LineCap.Round}){
    if(up){
     g.FillPolygon(brush,new[]{new PointF(cx,top),new PointF(cx-half,top+half*1.4f),new PointF(cx+half,top+half*1.4f)});
     g.DrawLine(pen,cx,top+half,cx,bottom-half*1.2f);
     g.FillRectangle(brush,cx-half*0.7f,bottom-half*1.1f,half*1.4f,half*1.1f);
    }else{
     g.FillPolygon(brush,new[]{new PointF(cx,bottom),new PointF(cx-half,bottom-half*1.4f),new PointF(cx+half,bottom-half*1.4f)});
     g.DrawLine(pen,cx,top+half*1.2f,cx,bottom-half);
     g.FillRectangle(brush,cx-half*0.7f,top,half*1.4f,half*1.1f);
    }
   }
  }
 }
}
