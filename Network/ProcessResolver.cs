using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TweekPro.Network {
 /// <summary>Name, path and signer of a process, cached per PID and re-validated by start time to survive PID reuse.</summary>
 public class ProcessIdentity {
  public int Pid; public string Name="", Path="", Publisher=""; public long StartTime; public bool Exited;
  public string Display { get { return String.IsNullOrEmpty(Name)?"PID "+Pid:Name; } }
 }

 /// <summary>Resolves PIDs to executable names without administrator rights, using PROCESS_QUERY_LIMITED_INFORMATION.</summary>
 public static class ProcessResolver {
  const int PROCESS_QUERY_LIMITED_INFORMATION=0x1000;
  static readonly ConcurrentDictionary<int,ProcessIdentity> cache=new ConcurrentDictionary<int,ProcessIdentity>();
  static readonly ConcurrentDictionary<string,string> publishers=new ConcurrentDictionary<string,string>(StringComparer.OrdinalIgnoreCase);

  [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(int access,bool inherit,int pid);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder name,ref int size);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessTimes(IntPtr process,out long creation,out long exit,out long kernel,out long user);

  /// <summary>Pre-populates the cache with a synthetic identity (layout previews and tests on machines without those processes).</summary>
  public static void Seed(ProcessIdentity identity){identity.StartTime=StartTimeOf(identity.Pid);cache[identity.Pid]=identity;}

  /// <summary>Returns identity for a PID, reusing the cache when the start time still matches.</summary>
  public static ProcessIdentity Resolve(int pid){
   if(pid==0)return new ProcessIdentity{Pid=0,Name="System Idle Process",Publisher="Windows"};
   if(pid==4)return new ProcessIdentity{Pid=4,Name="System",Publisher="Windows"};
   long start=StartTimeOf(pid);
   ProcessIdentity cached;
   if(cache.TryGetValue(pid,out cached)&&cached.StartTime==start)return cached;
   var identity=new ProcessIdentity{Pid=pid,StartTime=start};
   IntPtr handle=IsWindows?OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,false,pid):IntPtr.Zero;
   if(handle!=IntPtr.Zero){
    try{
     var buffer=new StringBuilder(1024);int size=buffer.Capacity;
     if(QueryFullProcessImageName(handle,0,buffer,ref size)){identity.Path=buffer.ToString(0,size);identity.Name=Path.GetFileName(identity.Path);}
    }finally{CloseHandle(handle);}
   }
   if(identity.Name==""){
    try{using(var p=Process.GetProcessById(pid))identity.Name=p.ProcessName+".exe";}catch(ArgumentException){identity.Name="(đã kết thúc)";identity.Exited=true;}catch(InvalidOperationException){identity.Name="(đã kết thúc)";identity.Exited=true;}catch(Exception){identity.Name="PID "+pid;}
   }
   identity.Publisher=identity.Path==""?"":PublisherOf(identity.Path);
   cache[pid]=identity;
   return identity;
  }

  static readonly bool IsWindows=Environment.OSVersion.Platform==PlatformID.Win32NT;

  static long StartTimeOf(int pid){
   if(!IsWindows)return 0;
   IntPtr handle=OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,false,pid);
   if(handle==IntPtr.Zero)return 0;
   try{long creation,exit,kernel,user;return GetProcessTimes(handle,out creation,out exit,out kernel,out user)?creation:0;}
   finally{CloseHandle(handle);}
  }

  /// <summary>Authenticode signer common name, cached per path; "Không ký" when the file carries no signature.</summary>
  public static string PublisherOf(string path){
   string known;if(publishers.TryGetValue(path,out known))return known;
   string result;
   try{
    var cert=X509Certificate.CreateFromSignedFile(path);
    using(var cert2=new X509Certificate2(cert))result=cert2.GetNameInfo(X509NameType.SimpleName,false);
    if(String.IsNullOrEmpty(result))result="Đã ký";
   }catch(Exception){result="Không ký";}
   publishers[path]=result;
   return result;
  }

  /// <summary>Drops cache entries for processes no longer present so the cache cannot grow without bound.</summary>
  public static void Trim(ICollection<int> livePids){
   foreach(var key in new List<int>(cache.Keys))if(!livePids.Contains(key)){ProcessIdentity removed;cache.TryRemove(key,out removed);}
  }
 }

 /// <summary>Optional asynchronous reverse DNS with a bounded cache; nothing is queried unless the user turns it on.</summary>
 public static class HostResolver {
  static readonly ConcurrentDictionary<string,string> names=new ConcurrentDictionary<string,string>();
  static readonly ConcurrentDictionary<string,byte> pending=new ConcurrentDictionary<string,byte>();
  const int MaxEntries=2000;

  /// <summary>Returns the cached host name, or an empty string while a lookup is in flight or when disabled.</summary>
  public static string Lookup(IPAddress address,bool enabled){
   if(address==null||IPAddress.IsLoopback(address)||address.Equals(IPAddress.Any)||address.Equals(IPAddress.IPv6Any))return "";
   string key=address.ToString();string known;
   if(names.TryGetValue(key,out known))return known;
   if(!enabled||names.Count>MaxEntries)return "";
   if(pending.TryAdd(key,0)){
    try{
     Dns.BeginGetHostEntry(address,ar=>{
      string result="";
      try{var entry=Dns.EndGetHostEntry(ar);result=entry.HostName??"";}catch(Exception){result="—";}
      names[key]=result;byte b;pending.TryRemove(key,out b);
     },null);
    }catch(Exception){names[key]="—";byte b;pending.TryRemove(key,out b);}
   }
   return "";
  }

  /// <summary>Clears every cached and pending lookup (used when the user disables resolution).</summary>
  public static void Clear(){names.Clear();pending.Clear();}
 }
}
