using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TweekPro.Network {
 /// <summary>Draws the packet-flow strip: outbound packets rise on the left lane, inbound packets fall on the right lane.</summary>
 public static class PacketFlowRenderer {
  static readonly Color Surface=Color.White,Muted=Color.FromArgb(100,116,139),Track=Color.FromArgb(226,232,240);
  static readonly Color OutColor=Color.FromArgb(37,99,235),InColor=Color.FromArgb(22,163,74);

  public static void Draw(Graphics g,Rectangle r,IEnumerable<NetworkPacket> packets,double outRate,double inRate){
   var saved=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;
   using(var bg=new SolidBrush(Surface))g.FillRectangle(bg,r);
   int mid=r.X+r.Width/2;
   using(var tp=new Pen(Track,1f)){
    g.DrawLine(tp,r.X+r.Width/4,r.Y+20,r.X+r.Width/4,r.Bottom-6);
    g.DrawLine(tp,r.X+3*r.Width/4,r.Y+20,r.X+3*r.Width/4,r.Bottom-6);
    using(var dp=new Pen(Track,1f){DashStyle=DashStyle.Dot})g.DrawLine(dp,mid,r.Y+4,mid,r.Bottom-4);
   }
   float laneW=r.Width/2f-32;float travel=r.Height-28;
   foreach(var p in packets){
    float baseX=p.Outbound?r.X+r.Width*0.25f:r.X+r.Width*0.75f;
    float x=baseX+(p.Lane-0.5f)*laneW*0.7f;
    float y=p.Outbound?r.Bottom-6-p.Position*travel:r.Y+22+p.Position*travel;
    Color c=p.Outbound?OutColor:InColor;
    float fade=p.Outbound?1f-p.Position*0.35f:1f-p.Position*0.35f;
    int alpha=Math.Max(70,Math.Min(255,(int)(235*fade)));
    using(var b=new SolidBrush(Color.FromArgb(alpha,c)))g.FillRectangle(b,x-3f,y-3f,6f,6f);
   }
   using(var f=new Font("Segoe UI",8.25f,FontStyle.Bold)){
    using(var ob=new SolidBrush(OutColor))g.DrawString("Ra ↑  "+RateText(outRate),f,ob,r.X+10,r.Y+4);
    using(var ib=new SolidBrush(InColor))g.DrawString("Vào ↓  "+RateText(inRate),f,ib,mid+10,r.Y+4);
   }
   g.SmoothingMode=saved;
  }

  static string RateText(double bytesPerSecond){return bytesPerSecond<1?"0 B/s":Presentation.BytesLabel((long)bytesPerSecond)+"/s";}
 }

 /// <summary>A double-buffered strip control that animates packet flow in realtime from the current send/receive rates.</summary>
 public sealed class PacketFlowStrip:Panel {
  readonly PacketAnimator animator=new PacketAnimator();
  readonly Timer timer=new Timer{Interval=50};
  double outRate,inRate;DateTime last=DateTime.MinValue;

  public PacketFlowStrip(){
   DoubleBuffered=true;
   timer.Tick+=(s,e)=>{
    var now=DateTime.UtcNow;
    double dt=last==DateTime.MinValue?0.05:(now-last).TotalSeconds;
    last=now;animator.Advance(dt,outRate,inRate);Invalidate();
   };
  }

  /// <summary>Sets the current byte rates driving the spawn cadence.</summary>
  public void SetRates(double outboundPerSecond,double inboundPerSecond){outRate=Math.Max(0,outboundPerSecond);inRate=Math.Max(0,inboundPerSecond);}

  /// <summary>Starts the animation timer.</summary>
  public void Begin(){last=DateTime.MinValue;if(!timer.Enabled)timer.Start();}

  /// <summary>Stops the animation timer and clears in-flight packets.</summary>
  public void End(){timer.Stop();animator.Clear();Invalidate();}

  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);PacketFlowRenderer.Draw(e.Graphics,ClientRectangle,animator.Packets,outRate,inRate);}

  protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
 }
}
