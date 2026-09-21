using System;
using System.IO;
using System.Linq;

namespace TweekPro.Startup {
 /// <summary>Self-tests for startup insight: StartupApproved decoding, impact rating, location flags and annotation of synthetic entries (no registry writes).</summary>
 public static class StartupTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("StartupTests: "+message);}

  public static void Run(){
   // StartupApproved decoding: bit 0 of byte 0 is the disabled flag; bytes 4..11 carry the FILETIME of the decision.
   var enabled=StartupRating.Decode(new byte[]{2,0,0,0,0,0,0,0,0,0,0,0});Assert(enabled.Known&&enabled.Enabled&&!enabled.DisabledAt.HasValue,"02 = enabled");
   Assert(StartupRating.Decode(new byte[]{6,0,0,0,0,0,0,0,0,0,0,0}).Enabled,"06 = enabled (bit 0 clear)");
   long ft=new DateTime(2025,3,1,12,0,0,DateTimeKind.Utc).ToFileTimeUtc();var bytes=new byte[12];bytes[0]=3;Array.Copy(BitConverter.GetBytes(ft),0,bytes,4,8);
   var disabled=StartupRating.Decode(bytes);Assert(disabled.Known&&!disabled.Enabled&&disabled.DisabledAt.HasValue&&disabled.DisabledAt.Value.ToUniversalTime().Year==2025,"03 + FILETIME = disabled with date");
   Assert(!StartupRating.Decode(new byte[]{3}).DisabledAt.HasValue&&!StartupRating.Decode(new byte[]{3}).Enabled,"short disabled value has no date");
   var unknown=StartupRating.Decode(null);Assert(!unknown.Known&&unknown.Enabled,"missing value = enabled by default");
   var encodedOn=StartupRating.Encode(true,null);Assert(encodedOn.Length==12&&encodedOn[0]==2&&StartupRating.Decode(encodedOn).Enabled&&!StartupRating.Decode(encodedOn).DisabledAt.HasValue,"encode enabled");
   var when=new DateTime(2025,3,1,12,0,0,DateTimeKind.Utc);var encodedOff=StartupRating.Encode(false,when);var decodedOff=StartupRating.Decode(encodedOff);
   Assert(!decodedOff.Enabled&&decodedOff.DisabledAt.HasValue&&decodedOff.DisabledAt.Value.ToUniversalTime()==when,"encode disabled keeps the filetime");
   Assert(!StartupRating.Decode(new byte[0]).Known,"empty value = unknown");

   // Impact rating precedence: broken > disabled > size buckets; no target or size = unknown.
   Assert(StartupRating.Rate(true,false,true,-1)==StartupImpact.Broken,"missing target is broken");
   Assert(StartupRating.Rate(true,false,false,-1)==StartupImpact.Broken,"broken beats disabled");
   Assert(StartupRating.Rate(true,true,false,500L*1024*1024)==StartupImpact.Disabled,"disabled beats size");
   Assert(StartupRating.Rate(false,false,true,-1)==StartupImpact.Unknown,"no local target = unknown");
   Assert(StartupRating.Rate(true,true,true,-1)==StartupImpact.Unknown,"unsized target = unknown");
   Assert(StartupRating.Rate(true,true,true,StartupRating.MediumBytes-1)==StartupImpact.Low,"below 15 MB low");
   Assert(StartupRating.Rate(true,true,true,StartupRating.MediumBytes)==StartupImpact.Medium,"15 MB medium");
   Assert(StartupRating.Rate(true,true,true,StartupRating.HighBytes)==StartupImpact.High,"80 MB high");
   foreach(StartupImpact v in Enum.GetValues(typeof(StartupImpact)))Assert(!String.IsNullOrEmpty(StartupRating.ImpactLabel(v)),"label for "+v);

   // Location flags use the supplied roots only, so the test is independent of the machine's profile.
   string root=Path.Combine(Path.GetTempPath(),"tweekpro-startup-"+Guid.NewGuid().ToString("N"));
   string temp=Path.Combine(root,"Temp"),downloads=Path.Combine(root,"Downloads"),appData=Path.Combine(root,"AppData","Local"),programs=Path.Combine(root,"Program Files");
   var fromTemp=StartupRating.Flags(Path.Combine(temp,"x.exe"),true,true,temp,downloads,appData);Assert(fromTemp.Count==1&&fromTemp[0]=="Chạy từ thư mục Temp","temp flag");
   var fromDownloads=StartupRating.Flags(Path.Combine(downloads,"setup.exe"),true,false,temp,downloads,appData);Assert(fromDownloads.Count==1&&fromDownloads[0]=="Chạy từ Downloads","downloads flag, unsigned outside AppData adds nothing");
   var unsignedAppData=StartupRating.Flags(Path.Combine(appData,"Vendor","tool.exe"),true,false,temp,downloads,appData);Assert(unsignedAppData.Count==1&&unsignedAppData[0]=="Chưa ký, nằm trong AppData","unsigned AppData flag");
   Assert(StartupRating.Flags(Path.Combine(appData,"Vendor","tool.exe"),true,true,temp,downloads,appData).Count==0,"signed AppData is fine");
   Assert(StartupRating.Flags(Path.Combine(programs,"App","app.exe"),true,false,temp,downloads,appData).Count==0,"unsigned Program Files is not flagged");
   Assert(StartupRating.Flags(Path.Combine(temp,"x.exe"),false,false,temp,downloads,appData).Count==0,"missing targets get no location flag");
   Assert(StartupRating.Flags(Path.Combine(root,"TempOther","x.exe"),true,true,temp,downloads,appData).Count==0,"prefix without separator is not under root");
   Assert(StartupRating.Flags("",true,true,temp,downloads,appData).Count==0&&StartupRating.Flags(Path.Combine(temp,"x.exe"),true,true,"","","").Count==0,"empty inputs are safe");

   // StartupApproved key mapping mirrors Task Manager: Run/Run32 for registry values, StartupFolder for shortcuts, nothing for RunOnce.
   Assert(StartupInspector.ApprovedSubKey(new Candidate{Kind="RegistryValue",Path=Advanced.Run,Hive="HKCU",View="64"}).EndsWith("\\Run"),"HKCU Run");
   Assert(StartupInspector.ApprovedSubKey(new Candidate{Kind="RegistryValue",Path=Advanced.Run,Hive="HKLM",View="32"}).EndsWith("\\Run32"),"HKLM 32-bit Run");
   Assert(StartupInspector.ApprovedSubKey(new Candidate{Kind="RegistryValue",Path=Advanced.Run,Hive="HKCU",View="32"}).EndsWith("\\Run"),"HKCU 32-bit view shares Run");
   Assert(StartupInspector.ApprovedSubKey(new Candidate{Kind="File",Path="C:\\x\\a.lnk"}).EndsWith("\\StartupFolder"),"shortcut");
   Assert(StartupInspector.ApprovedSubKey(new Candidate{Kind="RegistryValue",Path=Advanced.RunOnce,Hive="HKCU",View="64"})==null,"RunOnce has no approval");
   Assert(StartupInspector.ApprovedSubKey(null)==null,"null candidate");

   // Annotate on synthetic entries: existing file gets size + rating, missing target is broken, vault-only rows count as disabled.
   Directory.CreateDirectory(root);string exe=Path.Combine(root,"tool.exe");File.WriteAllBytes(exe,new byte[1024]);
   try{
    var live=new AutorunEntry{Name="Tool",Command="\""+exe+"\" --tray",Item=new Candidate{Kind="RegistryValue",Path=Advanced.RunOnce,Hive="HKCU",View="64",ValueName="Tool"}};
    var gone=new AutorunEntry{Name="Gone",Command=Path.Combine(root,"missing.exe"),Item=new Candidate{Kind="RegistryValue",Path=Advanced.RunOnce,Hive="HKCU",View="64",ValueName="Gone"}};
    var remote=new AutorunEntry{Name="Remote",Command="\\\\server\\share\\agent.exe",Item=new Candidate{Kind="RegistryValue",Path=Advanced.RunOnce,Hive="HKCU",View="64",ValueName="Remote"}};
    var vaulted=new AutorunEntry{Name="Old",Command="x",Saved=new Backup{Id="1"}};
    var entries=new[]{live,gone,remote,vaulted}.ToList();StartupInspector.Annotate(entries);
    Assert(entries.All(e=>e.Insight!=null),"every entry annotated");
    Assert(live.Insight.Target==exe&&live.Insight.TargetExists&&live.Insight.Bytes==1024&&live.Insight.Impact==StartupImpact.Low&&live.Insight.RunsAtLogon,"live entry: "+live.Insight.Impact);
    Assert(gone.Insight.Impact==StartupImpact.Broken&&!gone.Insight.TargetExists&&!gone.Insight.RunsAtLogon&&gone.Insight.Bytes<0,"missing target broken");
    Assert(remote.Insight.Impact==StartupImpact.Unknown&&remote.Insight.Target==""&&remote.Insight.Flags.Count==0,"UNC command is not probed");
    Assert(vaulted.Insight.Impact==StartupImpact.Disabled&&!vaulted.Insight.Approval.Enabled&&vaulted.Insight.Approval.Known,"vault row is disabled");
    Assert(live.Insight.Approval.Enabled&&!live.Insight.Approval.Known,"RunOnce has no StartupApproved record");
   }finally{try{Directory.Delete(root,true);}catch(Exception){}}
  }
 }
}
