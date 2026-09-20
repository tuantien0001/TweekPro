using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;

namespace TweekPro.Network {
 /// <summary>Cumulative bytes for one PID since the session started.</summary>
 public class TrafficCounters { public long Sent, Received; }

 /// <summary>Per-process bytes plus rates computed between two snapshots.</summary>
 public class TrafficSample { public int Pid; public long Sent, Received; public double SentPerSecond, ReceivedPerSecond; }

 /// <summary>
 /// Real-time ETW session on the kernel TCP/IP provider (the same source Task Manager and Resource Monitor use for
 /// per-process network bytes). Requires administrator rights; never installs a driver. Events are aggregated per PID
 /// on the ETW thread and the UI only reads snapshots.
 /// </summary>
 public sealed class EtwNetworkSession:IDisposable {
  public const string SessionName="TweekPro-Network";
  TraceEventSession session;Thread worker;
  readonly ConcurrentDictionary<int,TrafficCounters> totals=new ConcurrentDictionary<int,TrafficCounters>();
  Dictionary<int,TrafficSample> previous=new Dictionary<int,TrafficSample>();DateTime previousStamp=DateTime.MinValue;
  long events;volatile bool running;string failure="";

  public bool IsRunning { get { return running; } }
  public string Failure { get { return failure; } }
  public long Events { get { return Interlocked.Read(ref events); } }
  public int EventsLost { get { try{return session==null?0:session.EventsLost;}catch(Exception){return 0;} } }

  /// <summary>True when the process is elevated, which the kernel provider requires.</summary>
  public static bool CanStart { get { return Core.Elevation.IsElevated; } }

  /// <summary>Starts (or restarts) the fixed-name session. An older session with the same name left by a crash is stopped first.</summary>
  public void Start(){
   if(running)return;
   if(!CanStart)throw new InvalidOperationException("Cần quyền quản trị để bật theo dõi băng thông (ETW).");
   failure="";
   try{
    StopStale();
    session=new TraceEventSession(SessionName){StopOnDispose=true,BufferSizeMB=64};
    session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP,KernelTraceEventParser.Keywords.None);
    var kernel=session.Source.Kernel;
    kernel.TcpIpSend+=e=>Add(e.ProcessID,e.size,0);
    kernel.TcpIpRecv+=e=>Add(e.ProcessID,0,e.size);
    kernel.TcpIpSendIPV6+=e=>Add(e.ProcessID,e.size,0);
    kernel.TcpIpRecvIPV6+=e=>Add(e.ProcessID,0,e.size);
    kernel.UdpIpSend+=e=>Add(e.ProcessID,e.size,0);
    kernel.UdpIpRecv+=e=>Add(e.ProcessID,0,e.size);
    kernel.UdpIpSendIPV6+=e=>Add(e.ProcessID,e.size,0);
    kernel.UdpIpRecvIPV6+=e=>Add(e.ProcessID,0,e.size);
    running=true;
    worker=new Thread(()=>{
     try{session.Source.Process();}
     catch(Exception e){failure=e.Message;}
     finally{running=false;}
    }){IsBackground=true,Name="TweekPro-ETW"};
    worker.Start();
    AppDomain.CurrentDomain.ProcessExit+=OnProcessExit;
   }catch(Exception e){failure=e.Message;running=false;try{if(session!=null)session.Dispose();}catch(Exception){}session=null;throw;}
  }

  void OnProcessExit(object sender,EventArgs e){Stop();}

  /// <summary>Stops a session with our fixed name that a previous crashed instance may have left active.</summary>
  static void StopStale(){
   try{
    foreach(string name in TraceEventSession.GetActiveSessionNames())if(String.Equals(name,SessionName,StringComparison.OrdinalIgnoreCase)){
     using(var stale=new TraceEventSession(SessionName,TraceEventSessionOptions.Attach))stale.Stop(true);
    }
   }catch(Exception){}
  }

  void Add(int pid,int sent,int received){
   Interlocked.Increment(ref events);
   var counters=totals.GetOrAdd(pid,_=>new TrafficCounters());
   if(sent>0)Interlocked.Add(ref counters.Sent,sent);
   if(received>0)Interlocked.Add(ref counters.Received,received);
  }

  /// <summary>Copies the totals and derives per-second rates against the previous snapshot.</summary>
  public Dictionary<int,TrafficSample> Snapshot(){
   var now=DateTime.UtcNow;var result=new Dictionary<int,TrafficSample>();
   double seconds=previousStamp==DateTime.MinValue?0:(now-previousStamp).TotalSeconds;
   foreach(var pair in totals){
    long sent=Interlocked.Read(ref pair.Value.Sent),received=Interlocked.Read(ref pair.Value.Received);
    var sample=new TrafficSample{Pid=pair.Key,Sent=sent,Received=received};
    TrafficSample last;
    if(seconds>0.2&&previous.TryGetValue(pair.Key,out last)){sample.SentPerSecond=Math.Max(0,(sent-last.Sent)/seconds);sample.ReceivedPerSecond=Math.Max(0,(received-last.Received)/seconds);}
    result[pair.Key]=sample;
   }
   previous=result;previousStamp=now;
   return result;
  }

  /// <summary>Removes counters for processes that no longer exist so long sessions do not grow unbounded.</summary>
  public void Trim(ICollection<int> livePids){
   foreach(var key in new List<int>(totals.Keys))if(!livePids.Contains(key)){TrafficCounters removed;totals.TryRemove(key,out removed);previous.Remove(key);}
  }

  /// <summary>Stops the ETW session and the processing thread; safe to call more than once.</summary>
  public void Stop(){
   AppDomain.CurrentDomain.ProcessExit-=OnProcessExit;
   var current=session;session=null;
   if(current==null){running=false;return;}
   try{current.Source.StopProcessing();}catch(Exception){}
   try{current.Stop(true);}catch(Exception){}
   try{current.Dispose();}catch(Exception){}
   if(worker!=null&&worker.IsAlive)worker.Join(3000);
   running=false;
  }

  public void Dispose(){Stop();}
 }
}
