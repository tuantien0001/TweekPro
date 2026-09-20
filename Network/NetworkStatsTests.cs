using System;
using System.Collections.Generic;
using System.Net;

namespace TweekPro.Network {
 /// <summary>Platform-neutral self-tests for the network application view: direction classification, per-process rollup and labels.</summary>
 public static class NetworkStatsTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("NetworkStatsTests: "+message);}

  static ConnectionInfo Tcp(int pid,string state,string remote){
   var c=new ConnectionInfo{Protocol="TCP",Pid=pid,State=state,LocalAddress=IPAddress.Parse("192.168.0.10"),LocalPort=50000};
   if(remote!=null){c.RemoteAddress=IPAddress.Parse(remote);c.RemotePort=443;}
   return c;
  }
  static ConnectionInfo Udp(int pid){return new ConnectionInfo{Protocol="UDP",Pid=pid,State="",LocalAddress=IPAddress.Parse("192.168.0.10"),LocalPort=53};}

  public static void Run(){
   double min=NetworkStats.MinRate;
   Assert(NetworkStats.Classify(1000,0,min)==TrafficDirection.Outbound,"High send is outbound");
   Assert(NetworkStats.Classify(0,1000,min)==TrafficDirection.Inbound,"High receive is inbound");
   Assert(NetworkStats.Classify(1000,1000,min)==TrafficDirection.Both,"Both directions");
   Assert(NetworkStats.Classify(1,1,min)==TrafficDirection.Idle,"Below threshold is idle");

   var rows=new List<ConnectionInfo>{
    Tcp(100,"Đã kết nối","1.2.3.4"),
    Tcp(100,"Đã kết nối","5.6.7.8"),
    Tcp(100,"Đang lắng nghe",null),
    Udp(200)
   };
   var traffic=new Dictionary<int,TrafficSample>{
    {100,new TrafficSample{Pid=100,Sent=100000,Received=200,SentPerSecond=1000,ReceivedPerSecond=4}}
   };
   var summaries=NetworkStats.Aggregate(rows,traffic,min);
   Assert(summaries.Count==2,"Two processes summarised");
   Assert(summaries[0].Pid==100,"Busiest process listed first");
   var p1=summaries[0];
   Assert(p1.Connections==3&&p1.Established==2&&p1.Listening==1,"pid100 connection counts: c="+p1.Connections+" e="+p1.Established+" l="+p1.Listening);
   Assert(p1.RemoteEndpoints==2,"Distinct remote endpoints counted (listening excluded): "+p1.RemoteEndpoints);
   Assert(p1.Direction==TrafficDirection.Outbound&&p1.Sent==100000,"pid100 direction and totals folded in");
   var p2=summaries[1];
   Assert(p2.Udp==1&&p2.Connections==1&&p2.Direction==TrafficDirection.Idle,"pid200 udp idle");

   Assert(NetworkStats.DirectionKey(rows[0],traffic,min)=="out","Established row with outbound traffic -> out");
   Assert(NetworkStats.DirectionKey(rows[2],traffic,min)=="out","Listening row inherits its process direction when traffic flows");
   Assert(NetworkStats.DirectionKey(Tcp(300,"Đang lắng nghe",null),traffic,min)=="listen","Listening process without traffic -> listen");
   Assert(NetworkStats.DirectionKey(Tcp(300,"Đã kết nối","9.9.9.9"),traffic,min)=="idle","Connected process without traffic -> idle");
   var both=new Dictionary<int,TrafficSample>{{400,new TrafficSample{Pid=400,SentPerSecond=1000,ReceivedPerSecond=1000}}};
   Assert(NetworkStats.DirectionKey(Tcp(400,"Đã kết nối","9.9.9.9"),both,min)=="both","Two-way traffic -> both");

   Assert(NetworkStats.DirectionArrows("out")=="↑"&&NetworkStats.DirectionArrows("in")=="↓"&&NetworkStats.DirectionArrows("both")=="↑↓"&&NetworkStats.DirectionArrows("listen")=="◎"&&NetworkStats.DirectionArrows("idle")=="·","Direction arrows");
   Assert(NetworkStats.Rate(0)==""&&NetworkStats.Rate(2048)=="2.0 KB/s","Rate label");
  }
 }
}
