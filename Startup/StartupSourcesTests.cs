using System;

namespace TweekPro.Startup {
 /// <summary>Self-tests for the extra autorun sources: packaged-task state, name cleanup, key allow-list and which services count as logon services. No registry writes.</summary>
 public static class StartupSourcesTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("StartupSourcesTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch{failed=true;}if(!failed)throw new Exception("StartupSourcesTests: "+message);}

  public static void Run(){
   Assert(StartupTaskRules.Runs(StartupTaskRules.Enabled)&&StartupTaskRules.Runs(StartupTaskRules.EnabledByPolicy),"enabled states run");
   Assert(!StartupTaskRules.Runs(StartupTaskRules.Disabled)&&!StartupTaskRules.Runs(StartupTaskRules.DisabledByUser)&&!StartupTaskRules.Runs(StartupTaskRules.DisabledByPolicy),"disabled states do not run");
   Assert(StartupTaskRules.Locked(StartupTaskRules.DisabledByPolicy)&&StartupTaskRules.Locked(StartupTaskRules.EnabledByPolicy)&&!StartupTaskRules.Locked(StartupTaskRules.Enabled),"only policy states are locked");
   Assert(StartupTaskRules.AppLabel("Claude_pzs8sxrjxfjjc","ClaudeStartup")=="Claude","package family drops the publisher id");
   Assert(StartupTaskRules.AppLabel("Microsoft.WindowsTerminal_8wekyb3d8bbwe","StartTerminalOnLoginTask")=="Windows Terminal","terminal");
   Assert(StartupTaskRules.AppLabel("Microsoft.GamingApp_8wekyb3d8bbwe","Xbox.App.Tasks.FullTrustComponent")=="Xbox","xbox");
   Assert(StartupTaskRules.AppLabel("Microsoft.6365217CE6EB4_8wekyb3d8bbwe","microsoftdefender")=="Microsoft Defender","defender task id wins");
   Assert(StartupTaskRules.AppLabel("MSTeams_8wekyb3d8bbwe","TeamsTfwStartupTask")=="Microsoft Teams","teams");
   string package,task;
   Assert(StartupSources.SplitTask(StartupSources.AppModelRoot+@"\Claude_pzs8sxrjxfjjc\ClaudeStartup",out package,out task)&&package=="Claude_pzs8sxrjxfjjc"&&task=="ClaudeStartup","two segments accepted");
   Assert(StartupSources.SplitTask(StartupSources.AppModelRoot+@"\Microsoft.Copilot_8wekyb3d8bbwe\Copilot.StartupTaskId",out package,out task)&&task=="Copilot.StartupTaskId","dotted task id accepted");
   Assert(!StartupSources.SplitTask(StartupSources.AppModelRoot+@"\Claude\task\extra",out package,out task),"three segments refused");
   Assert(!StartupSources.SplitTask(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\Claude",out package,out task),"Run key is not a startup task");
   Assert(!StartupSources.SplitTask(StartupSources.AppModelRoot+@"\..\Windows\task",out package,out task),"dotdot refused");
   Assert(StartupSources.IsAutoService("Auto","Own Process")&&StartupSources.IsAutoService("Automatic","Share Process"),"automatic Win32 services count");
   Assert(!StartupSources.IsAutoService("Manual","Own Process")&&!StartupSources.IsAutoService("Disabled","Own Process"),"manual and disabled do not count");
   Assert(!StartupSources.IsAutoService("Auto","Kernel Driver")&&!StartupSources.IsAutoService("Boot","File System Driver"),"drivers do not count");
   MustFail(()=>Network.ProcessControl.DisableAutostart(new Network.HostedService{Name="RpcSs",DisplayName="RPC",StartMode="Auto"}),"core service refused");
   MustFail(()=>Network.ProcessControl.DisableAutostart(new Network.HostedService{Name="bad/name",StartMode="Auto"}),"invalid service name refused");
   MustFail(()=>Network.ProcessControl.DisableAutostart(new Network.HostedService{Name="RtkAudio",StartMode="Boot"}),"boot driver refused");
   MustFail(()=>StartupSources.Restore(new Backup{Kind=StartupSources.BackupKind,Original=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"}),"restore refuses a non-task path");
   MustFail(()=>StartupInspector.SetEnabled(null),"null candidate has no approval switch");
   MustFail(()=>StartupInspector.SetEnabled(new Candidate{Kind="RegistryValue",Path=Advanced.RunOnce,Hive="HKCU",View="64",ValueName="Once"}),"RunOnce has no approval switch");
   MustFail(()=>StartupInspector.RestoreApproval(null),"null approval restore");
   MustFail(()=>StartupInspector.RestoreApproval(new Backup{Purpose=StartupInspector.ApprovalPurpose,Original=Advanced.Run,ValueName="X"}),"Run key is not an approval backup");
  }
 }
}
