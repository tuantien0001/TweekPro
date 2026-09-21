using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TweekPro.Core;

namespace TweekPro.Network {
 /// <summary>Self-tests for the firewall block: rule parsing, naming, refusals and the vault round-trip through a fake netsh.</summary>
 public static class FirewallBlockTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("FirewallBlockTests: "+message);}
  static void MustFail(Action a,string message){bool failed=false;try{a();}catch(Exception){failed=true;}Assert(failed,message);}

  public static void Run(){
   Parsing();Naming();Refusals();RoundTrip();
   Assert(L.Has("Chặn mạng")&&L.Has("Bỏ chặn mạng"),"block strings translated");
  }

  static void Parsing(){
   var r=FirewallBlock.Parse("{guid}","v2.31|Action=Block|Active=TRUE|Dir=Out|Profile=Public|App=C:\\Tools\\Agent.exe|Name=TweekPro Block - Agent.exe [1A2B3C4D]|Desc=Tweek Pro network block|EmbedCtxt=Tweek|");
   Assert(r.Action=="Block"&&r.Active&&r.Direction=="Out"&&r.Program=="C:\\Tools\\Agent.exe"&&r.Name.StartsWith(FirewallBlock.Prefix)&&r.Ours,"parsed fields");
   var other=FirewallBlock.Parse("x","v2.31|Action=Allow|Active=TRUE|Dir=In|App=C:\\Tools\\Agent.exe|Name=@FirewallAPI.dll,-28502|");
   Assert(!other.Ours&&other.Action=="Allow","foreign rule is not ours");
   Assert(FirewallBlock.Parse("y",null).Name==""&&FirewallBlock.Parse("z","garbage").Program=="","malformed values tolerated");
   var rules=new List<FirewallRule>{r,other};
   Assert(FirewallBlock.IsBlocked("C:\\Tools\\Agent.exe",rules)&&FirewallBlock.IsBlocked("c:\\tools\\AGENT.EXE",rules)&&!FirewallBlock.IsBlocked("C:\\Tools\\Other.exe",rules),"blocked lookup by path");
   var inactive=FirewallBlock.Parse("w","v2.31|Action=Block|Active=FALSE|Dir=Out|App=C:\\Tools\\Off.exe|Name=TweekPro Block - Off.exe [0]|");
   Assert(!FirewallBlock.IsBlocked("C:\\Tools\\Off.exe",new List<FirewallRule>{inactive}),"inactive rule does not count");
   Assert(FirewallBlock.BlockedPrograms(new List<FirewallRule>{r,inactive,other}).Count==1,"blocked programs distinct and active only");
  }

  static void Naming(){
   string a=FirewallBlock.RuleName("C:\\Program Files\\Ứng dụng\\Tải về.exe"),b=FirewallBlock.RuleName("C:\\Other\\Tải về.exe"),c=FirewallBlock.RuleName("c:\\program files\\ứng dụng\\TẢI VỀ.EXE");
   Assert(a.StartsWith(FirewallBlock.Prefix)&&a.All(ch=>ch<128),"rule name is ASCII: "+a);
   Assert(a!=b,"same file name in different folders gets different rules");
   Assert(a==c,"rule name is case-insensitive over the path");
   Assert(a.EndsWith("]")&&a.Contains("["),"rule name carries the path hash");
  }

  static void Refusals(){
   string system=Environment.GetFolderPath(Environment.SpecialFolder.System);
   Assert(FirewallBlock.BlockReason("")!=null&&FirewallBlock.BlockReason(null)!=null,"empty path refused");
   Assert(FirewallBlock.BlockReason("\\\\server\\share\\app.exe")!=null,"UNC path refused");
   Assert(FirewallBlock.BlockReason("C:\\Users\\Test\\notes.txt")!=null,"non-exe refused");
   Assert(FirewallBlock.BlockReason("C:\\Users\\Test\\svchost.exe")!=null,"core process name refused anywhere");
   if(!String.IsNullOrEmpty(system)){
    Assert(FirewallBlock.BlockReason(Path.Combine(system,"anything.exe"))!=null,"System32 refused");
    Assert(FirewallBlock.BlockReason(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"))!=null,"explorer.exe refused");
   }
   Assert(FirewallBlock.BlockReason("C:\\Users\\Test\\AppData\\Local\\Vendor\\updater.exe")==null,"ordinary user program allowed");
   Assert(FirewallBlock.BlockReason("C:\\Program Files\\Vendor\\Tool.exe")==null,"Program Files program allowed");
   MustFail(()=>FirewallBlock.Block("C:\\Users\\Test\\svchost.exe","svchost"),"Block refuses core names before touching netsh");
  }

  static void RoundTrip(){
   var calls=new List<string[]>();var store=new List<FirewallRule>();
   var oldRunner=FirewallBlock.Runner;var oldSource=FirewallBlock.RuleSource;
   FirewallBlock.Runner=args=>{
    calls.Add(args);string name=args.First(a=>a.StartsWith("name=")).Substring(5);
    if(args[2]=="add"){string dir=args.First(a=>a.StartsWith("dir=")).Substring(4),program=args.First(a=>a.StartsWith("program=")).Substring(8);store.Add(FirewallBlock.Parse(Guid.NewGuid().ToString(),"v2.31|Action=Block|Active=TRUE|Dir="+(dir=="out"?"Out":"In")+"|App="+program+"|Name="+name+"|"));}
    else if(args[2]=="delete"){if(store.RemoveAll(r=>r.Name==name)==0)throw new IOException("No rules match the specified criteria.");}
   };
   FirewallBlock.RuleSource=()=>store.ToList();
   string exe="C:\\Users\\Test\\AppData\\Local\\Vendor\\telemetry.exe";Backup b=null;
   try{
    b=FirewallBlock.Block(exe,"Vendor Telemetry");
    Assert(b.Kind==FirewallBlock.BackupKind&&b.State=="BackedUp"&&b.Purpose=="Block"&&b.ValueName==FirewallBlock.RuleName(exe)&&String.Equals(b.Original,Engine.Canon(exe),StringComparison.OrdinalIgnoreCase),"backup recorded");
    Assert(calls.Count==2&&calls.All(c=>c[2]=="add"&&c.Contains("action=block")&&c.Contains("enable=yes"))&&calls.Any(c=>c.Contains("dir=out"))&&calls.Any(c=>c.Contains("dir=in")),"two block rules added: "+calls.Count);
    Assert(store.Count==2&&FirewallBlock.IsBlocked(exe)&&FirewallBlock.Rules().Count==2&&FirewallBlock.BlockedPrograms().Count==1,"rules visible through the store");
    Assert(Engine.Backups().Any(x=>x.Id==b.Id&&x.Kind==FirewallBlock.BackupKind),"firewall backup listed in the vault");
    MustFail(()=>FirewallBlock.Block(exe,"again"),"double block refused");
    var forged=new Backup{Id=b.Id,Kind=FirewallBlock.BackupKind,Original=exe,ValueName=FirewallBlock.Prefix+"Windows Defender [00000000]",State="BackedUp"};
    MustFail(()=>FirewallBlock.Restore(forged),"restore refuses a rule name that does not match the path");
    MustFail(()=>FirewallBlock.Restore(new Backup{Id=b.Id,Kind="Service",Original=exe}),"restore refuses other kinds");
    Assert(store.Count==2,"refused restores did not delete anything");
    Engine.Restore(b);
    Assert(b.State=="Restored"&&store.Count==0&&calls.Count==3&&calls[2][2]=="delete","restore removed both rules by name");
    Assert(!FirewallBlock.IsBlocked(exe),"no longer blocked");
    FirewallBlock.Unblock(exe);Assert(calls.Count==3,"unblock of a missing rule is a no-op");
   }finally{FirewallBlock.Runner=oldRunner;FirewallBlock.RuleSource=oldSource;if(b!=null){try{Engine.Purge(b);}catch(Exception){}}}
   Assert(b!=null&&!Directory.Exists(Path.Combine(Engine.Vault,b.Id)),"fixture backup purged");
  }
 }
}
