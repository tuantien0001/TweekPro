using System;
using System.Collections.Generic;
using System.Linq;

namespace TweekPro.Network {
 /// <summary>Dominant traffic direction of a process between two bandwidth snapshots.</summary>
 public enum TrafficDirection { Idle, Inbound, Outbound, Both }

 /// <summary>Per-process rollup of the connection table plus optional ETW bandwidth, for the application view.</summary>
 public class ProcessNetworkSummary {
  public int Pid;
  public int Connections, Established, Listening, Udp, RemoteEndpoints;
  public long Sent, Received; public double SentPerSecond, ReceivedPerSecond;
  public TrafficDirection Direction=TrafficDirection.Idle;
  internal readonly HashSet<string> Remotes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  public double TotalPerSecond { get { return SentPerSecond+ReceivedPerSecond; } }
 }

 /// <summary>Platform-neutral helpers for the network application view: per-process rollup, direction and rate labels.</summary>
 public static class NetworkStats {
  /// <summary>Below this many bytes/second a stream counts as idle, filtering out counter noise.</summary>
  public const double MinRate=16;

  /// <summary>Classifies the dominant direction from the send and receive rates.</summary>
  public static TrafficDirection Classify(double sentPerSecond,double receivedPerSecond,double minRate){
   bool up=sentPerSecond>=minRate,down=receivedPerSecond>=minRate;
   if(up&&down)return TrafficDirection.Both;
   if(up)return TrafficDirection.Outbound;
   if(down)return TrafficDirection.Inbound;
   return TrafficDirection.Idle;
  }

  /// <summary>Groups connection rows by owning process and folds in bandwidth samples, ordered by current throughput.</summary>
  public static List<ProcessNetworkSummary> Aggregate(IEnumerable<ConnectionInfo> rows,IDictionary<int,TrafficSample> traffic,double minRate){
   var map=new Dictionary<int,ProcessNetworkSummary>();
   foreach(var c in rows){
    ProcessNetworkSummary s;if(!map.TryGetValue(c.Pid,out s)){s=new ProcessNetworkSummary{Pid=c.Pid};map[c.Pid]=s;}
    s.Connections++;
    if(c.State=="Đang lắng nghe")s.Listening++;
    else if(c.State=="Đã kết nối")s.Established++;
    if((c.Protocol??"").StartsWith("UDP",StringComparison.OrdinalIgnoreCase))s.Udp++;
    if(!String.IsNullOrEmpty(c.Remote))s.Remotes.Add(c.Remote);
   }
   foreach(var s in map.Values){
    s.RemoteEndpoints=s.Remotes.Count;
    TrafficSample t;if(traffic!=null&&traffic.TryGetValue(s.Pid,out t)&&t!=null){s.Sent=t.Sent;s.Received=t.Received;s.SentPerSecond=t.SentPerSecond;s.ReceivedPerSecond=t.ReceivedPerSecond;}
    s.Direction=Classify(s.SentPerSecond,s.ReceivedPerSecond,minRate);
   }
   return map.Values.OrderByDescending(s=>s.TotalPerSecond).ThenByDescending(s=>s.Connections).ThenBy(s=>s.Pid).ToList();
  }

  /// <summary>Image-list key for the directional packet icon of a single connection row.</summary>
  public static string DirectionKey(ConnectionInfo c,IDictionary<int,TrafficSample> traffic,double minRate){
   TrafficSample t;
   if(traffic!=null&&traffic.TryGetValue(c.Pid,out t)&&t!=null){
    switch(Classify(t.SentPerSecond,t.ReceivedPerSecond,minRate)){
     case TrafficDirection.Outbound:return "out";
     case TrafficDirection.Inbound:return "in";
     case TrafficDirection.Both:return "both";
    }
   }
   return c!=null&&c.State=="Đang lắng nghe"?"listen":"idle";
  }

  /// <summary>Short arrow label for the direction column (↑ out, ↓ in, ↑↓ both, ◎ listening, · idle).</summary>
  public static string DirectionArrows(string key){
   switch(key){
    case "out":return "↑";
    case "in":return "↓";
    case "both":return "↑↓";
    case "listen":return "◎";
    default:return "·";
   }
  }

  /// <summary>Formats a byte-per-second rate, or an empty string below one byte per second.</summary>
  public static string Rate(double bytesPerSecond){return bytesPerSecond<1?"":Presentation.BytesLabel((long)bytesPerSecond)+"/s";}
 }
}
