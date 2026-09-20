using System;
using System.Linq;

namespace TweekPro.Network {
 /// <summary>Platform-neutral self-tests for the realtime packet animation model.</summary>
 public static class PacketAnimatorTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("PacketAnimatorTests: "+message);}

  public static void Run(){
   Assert(PacketAnimator.PacketsPerSecond(0)==0,"Idle spawns nothing");
   Assert(PacketAnimator.PacketsPerSecond(1)>=0.8,"Any real traffic keeps a visible floor");
   Assert(PacketAnimator.PacketsPerSecond(1e12)<=60,"Spawn cadence is capped");

   var a=new PacketAnimator();
   // Outbound-only traffic produces rising outbound packets and no inbound packets, all within the lane.
   for(int i=0;i<20;i++)a.Advance(0.05,4*1024*1024,0);
   Assert(a.Packets.Count>0,"Outbound traffic spawns packets");
   Assert(a.Packets.All(p=>p.Outbound),"Only outbound packets when receive rate is zero");
   Assert(a.Packets.All(p=>p.Position>=0f&&p.Position<=1f),"Positions stay within the lane");

   // Inbound-only traffic produces inbound packets.
   var b=new PacketAnimator();
   for(int i=0;i<20;i++)b.Advance(0.05,0,4*1024*1024);
   Assert(b.Packets.Count>0&&b.Packets.All(p=>!p.Outbound),"Only inbound packets when send rate is zero");

   // Determinism: identical inputs yield identical packet counts.
   var c=new PacketAnimator();for(int i=0;i<20;i++)c.Advance(0.05,4*1024*1024,0);
   Assert(c.Packets.Count==a.Packets.Count,"Animation is deterministic");

   // Positions advance by Speed*dt between frames.
   var d=new PacketAnimator();d.Advance(0.05,4*1024*1024,0);
   Assert(d.Packets.Count>0,"Spawned on first frame");
   float before=d.Packets[0].Position;d.Advance(0.1,4*1024*1024,0);
   float after=d.Packets.Count>0?d.Packets[0].Position:before;
   Assert(after>before,"Packet position advances over time");

   // When traffic stops, packets drain to empty.
   for(int i=0;i<40;i++)a.Advance(0.05,0,0);
   Assert(a.Packets.Count==0,"Packets drain out once traffic stops");

   // The packet count is capped even under sustained extreme throughput.
   var e=new PacketAnimator();for(int i=0;i<400;i++)e.Advance(0.05,1e12,1e12);
   Assert(e.Packets.Count<=PacketAnimator.MaxPackets,"Packet count is bounded: "+e.Packets.Count);

   a.Clear();Assert(a.Packets.Count==0,"Clear empties the animator");
  }
 }
}
