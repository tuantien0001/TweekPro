using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using TweekPro.Core;

namespace TweekPro.Network {
 /// <summary>One Windows Firewall rule as stored under FirewallPolicy\FirewallRules, parsed from its pipe-delimited value.</summary>
 public class FirewallRule {
  public string Id="",Name="",Program="",Direction="",Action="",Description="";public bool Active;
  /// <summary>True for rules Tweek Pro created (name prefix), so only those are ever listed or removed.</summary>
  public bool Ours { get { return Name.StartsWith(FirewallBlock.Prefix,StringComparison.Ordinal); } }
 }

 /// <summary>Per-application network block through Windows Firewall: paired outbound/inbound Block rules per executable, removable from the vault.</summary>
 public static class FirewallBlock {
  public const string BackupKind="Firewall";
  public const string Prefix="TweekPro Block - ";
  public const string RulesKey=@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules";
  public static bool IsWindows { get { return StubbornFiles.IsWindows; } }
  /// <summary>Optional replacement for netsh + registry (tests and previews); receives the netsh argument list and returns nothing or throws.</summary>
  public static Action<string[]> Runner;
  /// <summary>Optional replacement for the registry rule dump (tests and previews).</summary>
  public static Func<List<FirewallRule>> RuleSource;

  /// <summary>Stable ASCII rule name for one executable: file name plus a short hash of the full path, so two same-named programs never collide.</summary>
  public static string RuleName(string exePath){
   string canon=Engine.Canon(exePath);
   string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canon.ToLowerInvariant()))).Replace("-","").Substring(0,8);
   var leaf=new StringBuilder();foreach(char c in Path.GetFileName(canon).ToLowerInvariant())leaf.Append(c<128&&(Char.IsLetterOrDigit(c)||c=='.'||c=='-'||c=='_'||c==' ')?c:'_');
   return Prefix+leaf+" ["+hash+"]";
  }

  /// <summary>Returns null when the program may be blocked, otherwise the localized reason it is refused (Windows core binaries, Tweek Pro itself, non-local paths).</summary>
  public static string BlockReason(string exePath){
   if(String.IsNullOrWhiteSpace(exePath))return L.T("Không xác định được tệp thực thi của tiến trình này.");
   if(!Advanced.LocalPath(exePath))return L.T("Chỉ chặn được chương trình nằm trên ổ đĩa cục bộ.");
   string canon;try{canon=Engine.Canon(exePath);}catch(Exception){return L.T("Đường dẫn không hợp lệ.");}
   if(!Path.GetExtension(canon).Equals(".exe",StringComparison.OrdinalIgnoreCase))return L.T("Chỉ chặn được tệp .exe.");
   string own="";try{own=Engine.Canon(Process.GetCurrentProcess().MainModule.FileName);}catch(Exception){}
   if(own!=""&&String.Equals(own,canon,StringComparison.OrdinalIgnoreCase))return L.T("Đây là Tweek Pro.");
   string leaf=Path.GetFileName(canon).ToLowerInvariant();
   if(ProcessControl.CoreProcesses.Contains(leaf)||leaf=="explorer.exe"||leaf=="taskhostw.exe"||leaf=="runtimebroker.exe"||leaf=="searchhost.exe"||leaf=="startmenuexperiencehost.exe")return L.T("Tiến trình cốt lõi của Windows — chặn mạng sẽ làm hỏng Windows Update, đăng nhập hoặc Defender.");
   string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
   if(!String.IsNullOrEmpty(windows)&&Engine.Under(canon,Engine.Canon(windows))){
    string system32=Engine.Canon(Path.Combine(windows,"System32")),sysWow=Engine.Canon(Path.Combine(windows,"SysWOW64"));
    if(Engine.Under(canon,system32)||Engine.Under(canon,sysWow)||String.Equals(Path.GetDirectoryName(canon),Engine.Canon(windows),StringComparison.OrdinalIgnoreCase))return L.T("Tệp hệ thống trong thư mục Windows — không chặn để tránh hỏng cập nhật và bảo mật.");
   }
   return null;
  }

  /// <summary>Parses one FirewallRules registry value ("v2.31|Action=Block|Active=TRUE|Dir=Out|App=C:\x.exe|Name=…|").</summary>
  public static FirewallRule Parse(string id,string value){
   var rule=new FirewallRule{Id=id??""};if(String.IsNullOrEmpty(value))return rule;
   foreach(string part in value.Split('|')){
    int eq=part.IndexOf('=');if(eq<=0)continue;string key=part.Substring(0,eq),val=part.Substring(eq+1);
    switch(key){
     case "Action":rule.Action=val;break;case "Active":rule.Active=val.Equals("TRUE",StringComparison.OrdinalIgnoreCase);break;case "Dir":rule.Direction=val;break;
     case "App":rule.Program=val;break;case "Name":rule.Name=val;break;case "Desc":rule.Description=val;break;
    }
   }
   return rule;
  }

  /// <summary>All rules Tweek Pro created, read from the local policy store (read-only registry, no netsh).</summary>
  public static List<FirewallRule> Rules(){
   if(RuleSource!=null)return (RuleSource()??new List<FirewallRule>()).Where(r=>r.Ours).ToList();
   var list=new List<FirewallRule>();if(!IsWindows)return list;
   try{using(var key=Registry.LocalMachine.OpenSubKey(RulesKey)){
    if(key==null)return list;
    foreach(string name in key.GetValueNames()){var rule=Parse(name,Convert.ToString(key.GetValue(name,"")));if(rule.Ours)list.Add(rule);}
   }}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(IOException){}
   return list;
  }

  /// <summary>True when an active Tweek Pro Block rule exists for this executable (either direction).</summary>
  public static bool IsBlocked(string exePath,List<FirewallRule> rules=null){
   if(String.IsNullOrWhiteSpace(exePath))return false;string canon;try{canon=Engine.Canon(exePath);}catch(Exception){return false;}
   return (rules??Rules()).Any(r=>r.Active&&r.Action.Equals("Block",StringComparison.OrdinalIgnoreCase)&&PathEquals(r.Program,canon));
  }

  /// <summary>Distinct executables currently blocked by Tweek Pro rules.</summary>
  public static List<string> BlockedPrograms(List<FirewallRule> rules=null){
   return (rules??Rules()).Where(r=>r.Active&&r.Action.Equals("Block",StringComparison.OrdinalIgnoreCase)&&r.Program!="").Select(r=>{try{return Engine.Canon(r.Program);}catch(Exception){return r.Program;}}).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>Path.GetFileName(p),StringComparer.CurrentCultureIgnoreCase).ToList();
  }

  /// <summary>True when a rule with this exact name is present in the policy store; a missing rule is treated as already removed.</summary>
  static bool RuleExists(string name){return Rules().Any(r=>r.Name==name);}

  static bool PathEquals(string stored,string canon){try{return String.Equals(Engine.Canon(stored),canon,StringComparison.OrdinalIgnoreCase);}catch(Exception){return false;}}

  static void Netsh(params string[] args){
   if(Runner!=null){Runner(args);return;}
   if(!IsWindows)throw new IOException(L.T("Chỉ hỗ trợ trên Windows."));
   var psi=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"netsh.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   psi.Arguments=String.Join(" ",args.Select(a=>{int eq=a.IndexOf('=');string v=eq<0?a:a.Substring(eq+1);return v.Contains(" ")||v.Contains("[")?(eq<0?"\""+a+"\"":a.Substring(0,eq+1)+"\""+v+"\""):a;}));
   using(var p=Process.Start(psi)){string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();if(!p.WaitForExit(20000)||p.ExitCode!=0)throw new IOException("netsh: "+output.Trim());}
  }

  /// <summary>Blocks all traffic of one executable (outbound and inbound rules) and records the block in the vault so Restore removes it.</summary>
  public static Backup Block(string exePath,string displayName){
   string reason=BlockReason(exePath);if(reason!=null)throw new IOException(reason);
   string canon=Engine.Canon(exePath);string rule=RuleName(canon);
   if(IsBlocked(canon))throw new IOException(L.F("{0} đã bị chặn mạng.",Path.GetFileName(canon)));
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Original=canon,Kind=BackupKind,AppName=String.IsNullOrWhiteSpace(displayName)?Path.GetFileName(canon):displayName,ValueName=rule,Purpose="Block",Payload=""};
   Engine.NoLinks(Engine.Vault,false);Directory.CreateDirectory(Path.Combine(Engine.Vault,backup.Id));Engine.SaveBackup(backup);
   try{
    Netsh("advfirewall","firewall","add","rule","name="+rule,"dir=out","action=block","program="+canon,"enable=yes","profile=any","description=Tweek Pro network block; remove from the Recovery Vault.");
    Netsh("advfirewall","firewall","add","rule","name="+rule,"dir=in","action=block","program="+canon,"enable=yes","profile=any","description=Tweek Pro network block; remove from the Recovery Vault.");
    backup.State="BackedUp";backup.Error=L.T("Đã chặn mạng (vào + ra) bằng Windows Firewall; khôi phục = gỡ quy tắc chặn.");Engine.SaveBackup(backup);
    Log.Info("Đã chặn mạng: "+canon+" (quy tắc "+rule+").");
    return backup;
   }catch(Exception e){backup.State="NeedsReview";backup.Error=e.Message;Engine.SaveBackup(backup);throw;}
  }

  /// <summary>Removes the Tweek Pro Block rules for one executable; other rules are never touched because deletion goes by our unique rule name.</summary>
  public static void Unblock(string exePath){
   string canon=Engine.Canon(exePath);string rule=RuleName(canon);
   if(RuleExists(rule))Netsh("advfirewall","firewall","delete","rule","name="+rule);
   Log.Info("Đã gỡ chặn mạng: "+canon+".");
   foreach(var b in Engine.Backups().Where(b=>b.Kind==BackupKind&&b.State=="BackedUp"&&PathEquals(b.Original,canon)&&b.VaultPath==null)){b.State="Restored";b.Error="";Engine.SaveBackup(b);}
  }

  /// <summary>Vault restore: removes the block rules recorded in this backup.</summary>
  public static void Restore(Backup b){
   if(b.Kind!=BackupKind)throw new IOException("Không phải bản ghi chặn mạng.");
   if(String.IsNullOrWhiteSpace(b.Original)||!Advanced.LocalPath(b.Original))throw new IOException("Đường dẫn trong bản ghi không hợp lệ.");
   string expected=RuleName(b.Original);if(!String.IsNullOrEmpty(b.ValueName)&&b.ValueName!=expected)throw new IOException("Tên quy tắc trong bản ghi không khớp đường dẫn; không gỡ quy tắc lạ.");
   if(RuleExists(expected))Netsh("advfirewall","firewall","delete","rule","name="+expected);
   b.State="Restored";b.Error="";Engine.SaveBackup(b);
   Log.Info("Đã gỡ chặn mạng từ Kho khôi phục: "+b.Original+".");
  }
 }
}
