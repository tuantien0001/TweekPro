using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using TweekPro.Core;

namespace TweekPro.Startup {
 public enum StartupImpact { Unknown, Disabled, Broken, Low, Medium, High }

 /// <summary>Windows' own StartupApproved verdict for one entry; Known=false when Windows has never recorded a decision (treated as enabled).</summary>
 public class StartupApproval {
  public bool Known; public bool Enabled=true; public DateTime? DisabledAt;
 }

 /// <summary>Read-only facts about one startup entry: resolved target, existence, size, signer and a heuristic impact rating.</summary>
 public class StartupInsight {
  public string Target=""; public bool TargetExists; public long Bytes=-1; public string Signer=""; public bool Signed;
  public StartupApproval Approval=new StartupApproval(); public StartupImpact Impact=StartupImpact.Unknown; public List<string> Flags=new List<string>();
  public bool Broken { get { return Impact==StartupImpact.Broken; } }
  public bool RunsAtLogon { get { return Approval.Enabled&&!Broken; } }
 }

 /// <summary>Pure rating rules shared by the UI, Health and tests; nothing here touches the registry or disk.</summary>
 public static class StartupRating {
  public const long MediumBytes=15L*1024*1024, HighBytes=80L*1024*1024;

  /// <summary>Decodes a StartupApproved binary value: bit 0 of the first byte set means disabled; bytes 4..11 hold the FILETIME of that decision.</summary>
  public static StartupApproval Decode(byte[] value){
   var a=new StartupApproval();if(value==null||value.Length<1)return a;
   a.Known=true;a.Enabled=(value[0]&1)==0;
   if(!a.Enabled&&value.Length>=12){long ticks=BitConverter.ToInt64(value,4);if(ticks>0)try{a.DisabledAt=DateTime.FromFileTimeUtc(ticks).ToLocalTime();}catch(ArgumentOutOfRangeException){}}
   return a;
  }

  /// <summary>12-byte StartupApproved value. Enabled is 0x02 (bit 0 clear). Disabled is 0x03 plus an optional UTC FILETIME at bytes 4..11.</summary>
  public static byte[] Encode(bool enabled,DateTime? disabledUtc){
   var bytes=new byte[12];bytes[0]=(byte)(enabled?2:3);
   if(!enabled&&disabledUtc.HasValue)Array.Copy(BitConverter.GetBytes(disabledUtc.Value.ToUniversalTime().ToFileTimeUtc()),0,bytes,4,8);
   return bytes;
  }

  public static StartupImpact Rate(bool hasTarget,bool targetExists,bool enabled,long bytes){
   if(hasTarget&&!targetExists)return StartupImpact.Broken;
   if(!enabled)return StartupImpact.Disabled;
   if(!hasTarget||bytes<0)return StartupImpact.Unknown;
   return bytes>=HighBytes?StartupImpact.High:bytes>=MediumBytes?StartupImpact.Medium:StartupImpact.Low;
  }

  /// <summary>Warnings for locations a legitimate installer would not use; the caller supplies the profile folders so tests stay deterministic.</summary>
  public static List<string> Flags(string target,bool targetExists,bool signed,string tempRoot,string downloadsRoot,string appDataRoot){
   var flags=new List<string>();if(String.IsNullOrEmpty(target)||!targetExists)return flags;
   string canon=Canon(target);
   if(Under(canon,tempRoot))flags.Add("Chạy từ thư mục Temp");
   else if(Under(canon,downloadsRoot))flags.Add("Chạy từ Downloads");
   if(!signed&&Under(canon,appDataRoot))flags.Add("Chưa ký, nằm trong AppData");
   return flags;
  }

  static string Canon(string p){try{return Path.GetFullPath(p).TrimEnd('\\','/');}catch(Exception){return p??"";}}
  static bool Under(string path,string root){
   if(String.IsNullOrEmpty(root))return false;string r=Canon(root);
   return path.StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||path.StartsWith(r+Path.AltDirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
  }

  public static string ImpactLabel(StartupImpact impact){
   switch(impact){
    case StartupImpact.Disabled:return "Windows đã tắt";
    case StartupImpact.Broken:return "Đích không tồn tại";
    case StartupImpact.Low:return "Thấp";
    case StartupImpact.Medium:return "Trung bình";
    case StartupImpact.High:return "Cao";
    default:return "Không rõ";
   }
  }
 }

 /// <summary>Collects StartupInsight for AutorunEntry rows; registry and Authenticode probes are Windows-only and never throw.</summary>
 public static class StartupInspector {
  public const string Approved=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";
  public const string ApprovalPurpose="StartupApproved";
  static readonly bool IsWindows=Environment.OSVersion.Platform==PlatformID.Win32NT;

  public static void Annotate(IEnumerable<AutorunEntry> entries){
   string temp=SafeFolder(()=>Path.GetTempPath()),appData=SafeFolder(()=>Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),roaming=SafeFolder(()=>Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),downloads=SafeFolder(DownloadsFolder);
   foreach(var e in entries){
    if(e.Item!=null&&(e.Item.Kind=="Service"||e.Item.Kind==StartupSources.BackupKind)){if(e.Insight==null)e.Insight=new StartupInsight();continue;}
    var i=new StartupInsight();e.Insight=i;
    if(e.Item==null){i.Impact=StartupImpact.Disabled;i.Approval=new StartupApproval{Known=true,Enabled=false};continue;}
    string exe=Advanced.CommandExe(e.Command);if(exe==""&&Advanced.LocalPath(e.Command))exe=e.Command;
    // Only probe drive-letter paths; UNC/URL/shell verbs stay Unknown with an empty target so the UI never pretends they are installed locally.
    if(Advanced.LocalPath(exe)){i.Target=exe;try{var info=new FileInfo(i.Target);i.TargetExists=info.Exists;if(i.TargetExists)i.Bytes=info.Length;}catch(Exception){}}
    bool hasTarget=i.Target!="";
    if(i.TargetExists){i.Signer=Signer(i.Target);i.Signed=i.Signer!=""&&i.Signer!="Không ký";}
    i.Approval=ReadApproval(e);
    i.Impact=StartupRating.Rate(hasTarget,i.TargetExists,i.Approval.Enabled,i.Bytes);
    i.Flags=StartupRating.Flags(i.Target,i.TargetExists,i.Signed,temp,downloads,appData);
    if(i.Flags.Count==0&&!i.Signed&&i.TargetExists&&Under(i.Target,roaming))i.Flags.Add("Chưa ký, nằm trong AppData");
   }
  }

  /// <summary>Maps an entry to its StartupApproved key: Run/Run32 for registry values, StartupFolder for .lnk files; the HKCU key covers user entries, HKLM machine-wide ones.</summary>
  public static string ApprovedSubKey(Candidate c){
   if(c==null)return null;
   if(c.Kind=="File")return Approved+@"\StartupFolder";
   if(c.Kind!="RegistryValue"||!String.Equals(c.Path,Advanced.Run,StringComparison.OrdinalIgnoreCase))return null;
   return Approved+(c.View=="32"&&c.Hive=="HKLM"?@"\Run32":@"\Run");
  }

  /// <summary>Turns a Windows-disabled Run value or Startup shortcut back on by writing StartupApproved, after the previous bytes are in the vault. Does not create a missing value and does not touch RunOnce.</summary>
  public static Backup SetEnabled(Candidate c){
   string sub=c==null?null:ApprovedSubKey(c);
   if(sub==null)throw new IOException(L.T("Mục này không có công tắc bật/tắt của Windows. Hãy tắt để đưa vào Kho, rồi bật lại từ Kho."));
   if(!IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   string hive=c.Kind=="File"?(Under(c.Path,SafeFolder(()=>Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)))?"HKLM":"HKCU"):(c.Hive=="HKLM"?"HKLM":"HKCU");
   string name=c.Kind=="File"?Path.GetFileName(c.Path):c.ValueName;
   if(!SafeValueName(name))throw new IOException(L.T("Tên giá trị không hợp lệ."));
   using(var root=Engine.Base(hive,"64"))using(var key=root.OpenSubKey(sub,true)){
    if(key==null)throw new IOException(L.T("Mục khởi động không còn tồn tại."));
    string match=key.GetValueNames().FirstOrDefault(n=>String.Equals(n,name,StringComparison.OrdinalIgnoreCase));
    var prior=match==null?null:key.GetValue(match) as byte[];
    if(prior==null||StartupRating.Decode(prior).Enabled)throw new IOException(L.T("Mục khởi động này đang bật."));
    var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Original=sub,Kind="RegistryValue",Hive=hive,View="64",AppName=String.IsNullOrEmpty(c.AppName)?match:c.AppName,ValueName=match,Purpose=ApprovalPurpose,Payload="value.xml"};
    Engine.NoLinks(Engine.Vault,false);Directory.CreateDirectory(Path.Combine(Engine.Vault,backup.Id));Engine.SaveBackup(backup);
    try{
     Engine.Save(Path.Combine(Engine.Vault,backup.Id,backup.Payload),new RegValue{Name=match,Kind=RegistryValueKind.Binary,Bytes=prior});
     key.SetValue(match,StartupRating.Encode(true,null),RegistryValueKind.Binary);
     backup.State="BackedUp";Engine.SaveBackup(backup);return backup;
    }catch(Exception e){backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;}
   }
  }

  /// <summary>Writes the saved StartupApproved bytes back. Refuses any key other than Run, Run32 and StartupFolder.</summary>
  public static void RestoreApproval(Backup b){
   if(b==null||b.Purpose!=ApprovalPurpose)throw new IOException(L.T("Không phải bản sao lưu cờ khởi động."));
   if(!IsApprovalKey(b.Original))throw new IOException(L.T("Chỉ khôi phục được cờ StartupApproved."));
   if(!SafeValueName(b.ValueName))throw new IOException(L.T("Tên giá trị không hợp lệ."));
   if(!IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   string payload=Path.Combine(Engine.VaultOf(b),b.Id,b.Payload??"value.xml");Engine.NoLinks(payload,false);
   var value=Engine.Load<RegValue>(payload);
   if(value==null||value.Kind!=RegistryValueKind.Binary||value.Bytes==null||value.Bytes.Length<1||value.Bytes.Length>16)throw new IOException(L.T("Bản sao lưu trống."));
   if(!String.Equals(value.Name,b.ValueName,StringComparison.OrdinalIgnoreCase))throw new IOException(L.T("Bản sao lưu không khớp tên giá trị."));
   using(var root=Engine.Base(b.Hive=="HKLM"?"HKLM":"HKCU","64"))using(var key=root.OpenSubKey(b.Original,true)){
    if(key==null)throw new IOException(L.T("Mục khởi động không còn tồn tại."));
    key.SetValue(b.ValueName,value.Bytes,RegistryValueKind.Binary);
   }
   b.State="Restored";b.Error="";Engine.SaveBackup(b);
  }

  static bool IsApprovalKey(string path){
   return String.Equals(path,Remnants.TraceHunter.StartupApprovedRun,StringComparison.OrdinalIgnoreCase)||String.Equals(path,Remnants.TraceHunter.StartupApprovedRun32,StringComparison.OrdinalIgnoreCase)||String.Equals(path,Remnants.TraceHunter.StartupApprovedFolder,StringComparison.OrdinalIgnoreCase);
  }

  static bool SafeValueName(string name){return !String.IsNullOrEmpty(name)&&name.Length<=255&&name.IndexOf('\\')<0&&name.IndexOf('/')<0;}

  static StartupApproval ReadApproval(AutorunEntry e){
   var c=e.Item;string sub=ApprovedSubKey(c);if(sub==null||!IsWindows)return new StartupApproval();
   string hive=c.Kind=="File"?(Under(c.Path,SafeFolder(()=>Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)))?"HKLM":"HKCU"):c.Hive;
   string name=c.Kind=="File"?Path.GetFileName(c.Path):c.ValueName;
   try{
    using(var root=Engine.Base(hive,"64"))using(var key=root.OpenSubKey(sub)){
     if(key==null)return new StartupApproval();
     string match=key.GetValueNames().FirstOrDefault(n=>String.Equals(n,name,StringComparison.OrdinalIgnoreCase));
     if(match==null||key.GetValueKind(match)!=RegistryValueKind.Binary)return new StartupApproval();
     return StartupRating.Decode(key.GetValue(match) as byte[]);
    }
   }catch(Exception){return new StartupApproval();}
  }

  static string Signer(string path){if(!IsWindows)return "";try{return Network.ProcessResolver.PublisherOf(path);}catch(Exception){return "";}}
  static string SafeFolder(Func<string> f){try{return f()??"";}catch(Exception){return "";}}
  static string DownloadsFolder(){string p=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);return String.IsNullOrEmpty(p)?"":Path.Combine(p,"Downloads");}
  static bool Under(string path,string root){if(String.IsNullOrEmpty(root)||String.IsNullOrEmpty(path))return false;try{string p=Path.GetFullPath(path),r=Path.GetFullPath(root).TrimEnd('\\','/');return p.StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}catch(Exception){return false;}}
 }
}
