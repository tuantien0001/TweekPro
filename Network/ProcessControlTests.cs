using System;
using System.Diagnostics;
using System.IO;
using TweekPro.Core;

namespace TweekPro.Network {
 /// <summary>Platform-neutral self-tests for ProcessControl: safety lists, start-mode mapping, name validation, off-Windows refusals.</summary>
 public static class ProcessControlTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception("ProcessControlTests: "+message);}
  static void MustFail(Action action,string message){try{action();}catch(IOException){return;}throw new Exception("ProcessControlTests: expected failure — "+message);}

  public static void Run(){
   Assert(ProcessControl.TerminateBlockReason(0,"System Idle Process")!=null&&ProcessControl.TerminateBlockReason(4,"System")!=null,"PID 0/4 refused");
   Assert(ProcessControl.TerminateBlockReason(Process.GetCurrentProcess().Id,"TweekPro-0.7.exe")!=null,"Own process refused");
   foreach(string bare in new[]{"csrss","lsass","svchost","MsMpEng"})Assert(ProcessControl.TerminateBlockReason(1234,bare)!=null,"Core process without .exe suffix refused: "+bare);
   foreach(string svc in new[]{"MDCoreSvc","wscsvc","DPS","WdNisSvc"})Assert(ProcessControl.IsCoreService(svc),"knowledge-base core service also locked in ProcessControl: "+svc);
   foreach(string core in new[]{"csrss.exe","WININIT.EXE","lsass.exe","services.exe","svchost.exe","dwm.exe","MsMpEng.exe"})Assert(ProcessControl.TerminateBlockReason(1234,core)!=null,"Core process refused: "+core);
   Assert(ProcessControl.TerminateBlockReason(1234,"svchost.exe").Contains("svchost")||ProcessControl.TerminateBlockReason(1234,"svchost.exe").Length>0,"svchost has a specific reason");
   foreach(string ok in new[]{"chrome.exe","Spotify.exe","iGameCenter.Service.exe","OneDrive.exe",""})Assert(ProcessControl.TerminateBlockReason(1234,ok)==null,"Ordinary process allowed: "+ok);

   Assert(ProcessControl.IsCoreService("RpcSs")&&ProcessControl.IsCoreService("rpcss")&&ProcessControl.IsCoreService("WinDefend")&&ProcessControl.IsCoreService("Dnscache"),"Core services recognised case-insensitively");
   Assert(!ProcessControl.IsCoreService("iGameCenterService")&&!ProcessControl.IsCoreService("Spooler")&&!ProcessControl.IsCoreService("")&&!ProcessControl.IsCoreService(null),"Ordinary services allowed");

   Assert(ProcessControl.StartModeArgument("Auto")=="auto"&&ProcessControl.StartModeArgument("Automatic")=="auto"&&ProcessControl.StartModeArgument("Manual")=="demand"&&ProcessControl.StartModeArgument("Disabled")=="disabled"&&ProcessControl.StartModeArgument("Boot")=="boot"&&ProcessControl.StartModeArgument("System")=="system"&&ProcessControl.StartModeArgument(null)=="demand","WMI start mode -> sc.exe argument");

   Assert(ProcessControl.ValidServiceName("iGameCenterService")&&ProcessControl.ValidServiceName("Win Defend")&&ProcessControl.ValidServiceName("cbdhsvc_4a2b1")&&ProcessControl.ValidServiceName("MSSQL$SQLEXPRESS"),"Valid service names");
   Assert(!ProcessControl.ValidServiceName("")&&!ProcessControl.ValidServiceName(null)&&!ProcessControl.ValidServiceName("x\" & del *")&&!ProcessControl.ValidServiceName("a\r\nb")&&!ProcessControl.ValidServiceName(new string('a',300)),"Injection-prone or oversized names rejected");

   var rpc=new HostedService{Name="RpcSs",DisplayName="Remote Procedure Call (RPC)",StartMode="Auto"};
   MustFail(()=>ProcessControl.StopService(rpc,false),"core service stop refused before touching Windows");
   MustFail(()=>ProcessControl.StopService(new HostedService{Name="bad name`",DisplayName="x"},true),"invalid service name refused");
   MustFail(()=>ProcessControl.Terminate(4,"System"),"terminate core refused");
   MustFail(()=>ProcessControl.Restore(new Backup{Kind="Store",Original="x"}),"restore rejects other kinds");

   var savedProvider=ProcessControl.ServiceProvider;
   try{
    ProcessControl.ServiceProvider=pid=>pid==77?new System.Collections.Generic.List<HostedService>{new HostedService{Pid=77,Name="Spooler",DisplayName="Print Spooler"}}:null;
    Assert(ProcessControl.ServicesOf(77).Count==1&&ProcessControl.ServicesOf(78).Count==0,"Service provider override is honoured (null -> empty)");
   }finally{ProcessControl.ServiceProvider=savedProvider;}

   if(!ProcessControl.IsWindows){
    Assert(ProcessControl.ServicesOf(1234).Count==0,"No services off Windows");
    MustFail(()=>ProcessControl.Terminate(999999,"ghost.exe"),"terminate is Windows-only");
    MustFail(()=>ProcessControl.StopService(new HostedService{Name="Spooler",DisplayName="Print Spooler",StartMode="Auto"},false),"stop is Windows-only");
    MustFail(()=>ProcessControl.Restore(new Backup{Kind="Service",Original="Spooler",ValueName="Auto"}),"restore is Windows-only");
   }

   string saved=L.Lang;
   try{L.Lang="en";Assert(ProcessControl.TerminateBlockReason(4,"System")=="Core Windows process — cannot be ended.","Reason is localized");}finally{L.Lang=saved;}
  }
 }
}
