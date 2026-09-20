using System;
using System.Collections.Generic;

namespace TweekPro.Network {
 /// <summary>One animated packet travelling along a lane; Position runs 0..1, Lane is a 0..1 horizontal offset.</summary>
 public class NetworkPacket { public bool Outbound; public float Position; public float Lane; }

 /// <summary>
 /// Time-driven model for the realtime packet-flow animation. Spawns packets at a cadence proportional to the current
 /// send/receive rates and advances them along their lane. Deterministic (no randomness) so it is unit-testable on Linux.
 /// </summary>
 public class PacketAnimator {
  /// <summary>One visual packet represents roughly this many bytes/second of throughput.</summary>
  public const double BytesPerPacket=32768;
  public const int MaxPackets=240;
  /// <summary>Lane fraction travelled per second; a packet crosses in ~1/Speed seconds.</summary>
  public const float Speed=2.2f;

  readonly List<NetworkPacket> packets=new List<NetworkPacket>();
  double outAccumulator, inAccumulator; long spawnCount;

  public IList<NetworkPacket> Packets { get { return packets; } }

  public void Clear(){packets.Clear();outAccumulator=inAccumulator=0;}

  /// <summary>Advances the animation by dt seconds given the current outbound/inbound byte rates.</summary>
  public void Advance(double dt,double outRate,double inRate){
   if(dt<0)dt=0;if(dt>0.5)dt=0.5;
   for(int i=packets.Count-1;i>=0;i--){packets[i].Position+=(float)(Speed*dt);if(packets[i].Position>1f)packets.RemoveAt(i);}
   outAccumulator+=PacketsPerSecond(outRate)*dt;
   inAccumulator+=PacketsPerSecond(inRate)*dt;
   while(outAccumulator>=1){outAccumulator-=1;Spawn(true);}
   while(inAccumulator>=1){inAccumulator-=1;Spawn(false);}
  }

  /// <summary>Spawn cadence for a rate: zero when idle, with a small floor so any real traffic stays visibly active.</summary>
  public static double PacketsPerSecond(double bytesPerSecond){
   if(bytesPerSecond<=0)return 0;
   double pps=bytesPerSecond/BytesPerPacket;
   if(pps<0.8)pps=0.8;
   if(pps>60)pps=60;
   return pps;
  }

  void Spawn(bool outbound){
   if(packets.Count>=MaxPackets)return;
   // Golden-ratio stepping spreads packets across the lane width without any randomness.
   float lane=(float)((spawnCount*0.6180339887)%1.0);
   spawnCount++;
   packets.Add(new NetworkPacket{Outbound=outbound,Position=0f,Lane=lane});
  }
 }
}
