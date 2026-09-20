using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

namespace TweekPro.Network {
 /// <summary>One TCP or UDP endpoint owned by a process, as reported by the IP Helper API.</summary>
 public class ConnectionInfo {
  public string Protocol; public IPAddress LocalAddress, RemoteAddress; public int LocalPort, RemotePort; public int Pid; public string State;
  public string Key { get { return Protocol+"|"+Pid+"|"+Local+"|"+Remote; } }
  public string Local { get { return Endpoint(LocalAddress,LocalPort); } }
  public string Remote { get { return RemoteAddress==null?"":Endpoint(RemoteAddress,RemotePort); } }
  public bool IsLoopback { get { return RemoteAddress!=null&&(IPAddress.IsLoopback(RemoteAddress)||IPAddress.IsLoopback(LocalAddress)); } }
  static string Endpoint(IPAddress address,int port){return address==null?"":(address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6?"["+address+"]":address.ToString())+":"+port;}
 }

 /// <summary>Reads the live TCP/UDP tables with owning PIDs through GetExtendedTcpTable/GetExtendedUdpTable. No administrator rights needed.</summary>
 public static class ConnectionTable {
  const int AF_INET=2,AF_INET6=23,TCP_TABLE_OWNER_PID_ALL=5,UDP_TABLE_OWNER_PID=1;
  const uint ERROR_INSUFFICIENT_BUFFER=122,NO_ERROR=0;

  [DllImport("iphlpapi.dll",SetLastError=true)] static extern uint GetExtendedTcpTable(IntPtr table,ref int size,bool order,int family,int tableClass,uint reserved);
  [DllImport("iphlpapi.dll",SetLastError=true)] static extern uint GetExtendedUdpTable(IntPtr table,ref int size,bool order,int family,int tableClass,uint reserved);

  /// <summary>Vietnamese label for a MIB_TCP_STATE value.</summary>
  public static string StateName(int state){
   switch(state){
    case 1:return "Đã đóng";case 2:return "Đang lắng nghe";case 3:return "SYN đã gửi";case 4:return "SYN đã nhận";case 5:return "Đã kết nối";
    case 6:return "FIN_WAIT1";case 7:return "FIN_WAIT2";case 8:return "CLOSE_WAIT";case 9:return "Đang đóng";case 10:return "LAST_ACK";case 11:return "TIME_WAIT";case 12:return "DELETE_TCB";
    default:return "Không rõ ("+state+")";
   }
  }

  static int Port(uint raw){return (int)(((raw&0xFF)<<8)|((raw>>8)&0xFF));}

  /// <summary>Snapshot of every TCP (v4/v6) and UDP (v4/v6) endpoint with its owning PID.</summary>
  public static List<ConnectionInfo> Snapshot(){
   var list=new List<ConnectionInfo>();
   ReadTcp(AF_INET,list);ReadTcp(AF_INET6,list);ReadUdp(AF_INET,list);ReadUdp(AF_INET6,list);
   return list;
  }

  delegate uint TableReader(IntPtr buffer,ref int size);

  /// <summary>Allocates a buffer, retries when the table grows between the size query and the read, and returns the filled buffer.</summary>
  static IntPtr Fill(TableReader reader,out int size){
   size=0;reader(IntPtr.Zero,ref size);
   for(int attempt=0;attempt<4;attempt++){
    IntPtr buffer=Marshal.AllocHGlobal(size);
    uint result=reader(buffer,ref size);
    if(result==NO_ERROR)return buffer;
    Marshal.FreeHGlobal(buffer);
    if(result!=ERROR_INSUFFICIENT_BUFFER)throw new System.ComponentModel.Win32Exception((int)result,"Không đọc được bảng kết nối (mã "+result+").");
   }
   throw new InvalidOperationException("Bảng kết nối thay đổi liên tục; thử lại.");
  }

  static void ReadTcp(int family,List<ConnectionInfo> list){
   int size;IntPtr buffer=Fill((IntPtr b,ref int s)=>GetExtendedTcpTable(b,ref s,true,family,TCP_TABLE_OWNER_PID_ALL,0),out size);
   try{
    int count=Marshal.ReadInt32(buffer);IntPtr row=IntPtr.Add(buffer,4);
    for(int i=0;i<count;i++){
     if(family==AF_INET){
      var r=(MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(row,typeof(MIB_TCPROW_OWNER_PID));
      list.Add(new ConnectionInfo{Protocol="TCP",State=StateName((int)r.state),LocalAddress=new IPAddress(r.localAddr),LocalPort=Port(r.localPort),RemoteAddress=r.state==2?null:new IPAddress(r.remoteAddr),RemotePort=Port(r.remotePort),Pid=(int)r.owningPid});
      row=IntPtr.Add(row,Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID)));
     }else{
      var r=(MIB_TCP6ROW_OWNER_PID)Marshal.PtrToStructure(row,typeof(MIB_TCP6ROW_OWNER_PID));
      list.Add(new ConnectionInfo{Protocol="TCPv6",State=StateName((int)r.state),LocalAddress=new IPAddress(r.localAddr,r.localScopeId),LocalPort=Port(r.localPort),RemoteAddress=r.state==2?null:new IPAddress(r.remoteAddr,r.remoteScopeId),RemotePort=Port(r.remotePort),Pid=(int)r.owningPid});
      row=IntPtr.Add(row,Marshal.SizeOf(typeof(MIB_TCP6ROW_OWNER_PID)));
     }
    }
   }finally{Marshal.FreeHGlobal(buffer);}
  }

  static void ReadUdp(int family,List<ConnectionInfo> list){
   int size;IntPtr buffer=Fill((IntPtr b,ref int s)=>GetExtendedUdpTable(b,ref s,true,family,UDP_TABLE_OWNER_PID,0),out size);
   try{
    int count=Marshal.ReadInt32(buffer);IntPtr row=IntPtr.Add(buffer,4);
    for(int i=0;i<count;i++){
     if(family==AF_INET){
      var r=(MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(row,typeof(MIB_UDPROW_OWNER_PID));
      list.Add(new ConnectionInfo{Protocol="UDP",State="",LocalAddress=new IPAddress(r.localAddr),LocalPort=Port(r.localPort),Pid=(int)r.owningPid});
      row=IntPtr.Add(row,Marshal.SizeOf(typeof(MIB_UDPROW_OWNER_PID)));
     }else{
      var r=(MIB_UDP6ROW_OWNER_PID)Marshal.PtrToStructure(row,typeof(MIB_UDP6ROW_OWNER_PID));
      list.Add(new ConnectionInfo{Protocol="UDPv6",State="",LocalAddress=new IPAddress(r.localAddr,r.localScopeId),LocalPort=Port(r.localPort),Pid=(int)r.owningPid});
      row=IntPtr.Add(row,Marshal.SizeOf(typeof(MIB_UDP6ROW_OWNER_PID)));
     }
    }
   }finally{Marshal.FreeHGlobal(buffer);}
  }

  [StructLayout(LayoutKind.Sequential)] struct MIB_TCPROW_OWNER_PID { public uint state,localAddr,localPort,remoteAddr,remotePort,owningPid; }
  [StructLayout(LayoutKind.Sequential)] struct MIB_TCP6ROW_OWNER_PID { [MarshalAs(UnmanagedType.ByValArray,SizeConst=16)] public byte[] localAddr; public uint localScopeId,localPort; [MarshalAs(UnmanagedType.ByValArray,SizeConst=16)] public byte[] remoteAddr; public uint remoteScopeId,remotePort,state,owningPid; }
  [StructLayout(LayoutKind.Sequential)] struct MIB_UDPROW_OWNER_PID { public uint localAddr,localPort,owningPid; }
  [StructLayout(LayoutKind.Sequential)] struct MIB_UDP6ROW_OWNER_PID { [MarshalAs(UnmanagedType.ByValArray,SizeConst=16)] public byte[] localAddr; public uint localScopeId,localPort,owningPid; }
 }
}
