using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace TweekPro.Core {
 /// <summary>What the escalation ladder had to do to complete an operation on a stubborn path.</summary>
 public class StubbornOutcome {
  public bool Succeeded;
  public int AttributesCleared;
  public bool TookOwnership;
  public int Attempts;
  public Exception LastError;
  /// <summary>Human-readable summary of the steps taken, empty when the first attempt succeeded.</summary>
  public string Steps {
   get {
    var parts=new List<string>();
    if(AttributesCleared>0)parts.Add(L.F("bỏ thuộc tính chỉ đọc/ẩn/hệ thống của {0} mục",AttributesCleared));
    if(TookOwnership)parts.Add(L.T("chiếm quyền sở hữu và cấp toàn quyền cho Administrators"));
    return String.Join("; ",parts);
   }
  }
 }

 /// <summary>
 /// Removes the obstacles that make leftovers of uninstalled applications "undeletable": read-only/hidden/system
 /// attributes, ACLs that deny Administrators, and files locked by a running process (scheduled for deletion at reboot).
 /// Nothing here widens the target set: callers must already have validated the path against the safety boundaries.
 /// </summary>
 public static class StubbornFiles {
  public static bool IsWindows { get { return Environment.OSVersion.Platform==PlatformID.Win32NT; } }
  const int ErrorSharingViolation=32, ErrorLockViolation=33, ErrorAccessDenied=5;

  /// <summary>True when the exception means another process holds the file open.</summary>
  public static bool IsLocked(Exception e){
   var io=e as IOException;if(io==null||e is UnauthorizedAccessException)return false;
   int code=e.HResult&0xFFFF;
   return code==ErrorSharingViolation||code==ErrorLockViolation;
  }

  /// <summary>True when the exception is a permission problem that ownership takeover may fix.</summary>
  public static bool IsAccessDenied(Exception e){
   if(e is UnauthorizedAccessException)return true;
   var io=e as IOException;if(io==null)return false;
   return (e.HResult&0xFFFF)==ErrorAccessDenied;
  }

  /// <summary>Clears ReadOnly, Hidden and System on the path and, for directories, on every descendant. Returns how many entries changed.</summary>
  public static int ClearAttributes(string path){
   int changed=0;
   const FileAttributes mask=FileAttributes.ReadOnly|FileAttributes.Hidden|FileAttributes.System;
   Action<string> clear=p=>{
    try{
     var current=File.GetAttributes(p);
     if((current&mask)!=0){var next=current&~mask;if(next==0)next=FileAttributes.Normal;File.SetAttributes(p,next);changed++;}
    }catch(Exception){}
   };
   if(Directory.Exists(path)){
    clear(path);
    foreach(string entry in EnumerateSafe(path))clear(entry);
   }else if(File.Exists(path))clear(path);
   return changed;
  }

  /// <summary>
  /// Makes the Administrators group owner of the path (and descendants), removes every Deny entry and grants Administrators Full Control.
  /// Deny entries must go because they also bind an elevated token and would keep blocking the move. Windows only; false when unavailable.
  /// </summary>
  public static bool TakeOwnership(string path){
   if(!IsWindows)return false;
   try{
    bool privileged=EnablePrivilege("SeTakeOwnershipPrivilege")&EnablePrivilege("SeRestorePrivilege")&EnablePrivilege("SeBackupPrivilege");
    if(!privileged)Log.Warn("Không bật được đủ đặc quyền chiếm quyền sở hữu; sẽ thử với quyền hiện có.");
    var admins=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);
    bool any=false;
    if(Directory.Exists(path)){
     any|=OwnDirectory(path,admins);
     foreach(string entry in EnumerateSafe(path)){if(Directory.Exists(entry))any|=OwnDirectory(entry,admins);else any|=OwnFile(entry,admins);}
    }else if(File.Exists(path))any|=OwnFile(path,admins);
    return any;
   }catch(Exception e){Log.Warn("Không chiếm được quyền sở hữu "+path+": "+e.Message);return false;}
  }

  /// <summary>
  /// Runs the operation; when it fails with a permission error, clears attributes, then takes ownership, retrying after each step.
  /// Locked files are not retried (the lock will not go away) so the caller can offer deletion at reboot instead.
  /// </summary>
  public static StubbornOutcome Escalate(string path,Action operation){
   var outcome=new StubbornOutcome();
   var steps=new Func<bool>[]{()=>{outcome.AttributesCleared=ClearAttributes(path);return outcome.AttributesCleared>0;},()=>{outcome.TookOwnership=TakeOwnership(path);return outcome.TookOwnership;}};
   int step=0;
   while(true){
    outcome.Attempts++;
    try{operation();outcome.Succeeded=true;return outcome;}
    catch(Exception e){
     outcome.LastError=e;
     if(IsLocked(e)||!IsAccessDenied(e))throw;
     bool progressed=false;
     while(step<steps.Length&&!progressed)progressed=steps[step++]();
     if(!progressed)throw;
    }
   }
  }

  /// <summary>
  /// Asks Windows to delete the path at the next boot (MoveFileEx MOVEFILE_DELAY_UNTIL_REBOOT). Files are scheduled first, then
  /// folders deepest-first, because the session manager processes the list in order. Returns the number of entries scheduled; 0 off-Windows.
  /// </summary>
  public static int ScheduleDeleteOnReboot(string path){
   int failed;return ScheduleDeleteOnReboot(path,out failed);
  }

  /// <summary>Same as ScheduleDeleteOnReboot, also reporting how many entries Windows refused (each refusal is logged with its Win32 error).</summary>
  public static int ScheduleDeleteOnReboot(string path,out int failed){
   failed=0;
   if(!IsWindows)return 0;
   int scheduled=0,refused=0;
   Action<string> schedule=p=>{if(MoveFileEx(Prefix(p),null,MoveFileDelayUntilReboot))scheduled++;else{refused++;Log.Warn("MoveFileEx từ chối hẹn xóa "+p+" (lỗi Win32 "+Marshal.GetLastWin32Error()+").");}};
   if(Directory.Exists(path)){
    var entries=EnumerateSafe(path).ToList();
    foreach(string f in entries.Where(File.Exists))schedule(f);
    foreach(string d in entries.Where(Directory.Exists).OrderByDescending(d=>d.Length))schedule(d);
    schedule(path);
   }else if(File.Exists(path))schedule(path);
   failed=refused;
   return scheduled;
  }

  /// <summary>Registry location Windows reads at boot for pending renames/deletes; exposed so the UI can explain where the schedule lives.</summary>
  public const string PendingRenamesKey=@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations";

  static string Prefix(string p){return p.StartsWith(@"\\?\")?p:@"\\?\"+p;}

  static IEnumerable<string> EnumerateSafe(string root){
   var stack=new Stack<string>();stack.Push(root);
   while(stack.Count>0){
    string dir=stack.Pop();
    string[] files;try{files=Directory.GetFiles(dir);}catch(Exception){files=new string[0];}
    foreach(string f in files)yield return f;
    string[] dirs;try{dirs=Directory.GetDirectories(dir);}catch(Exception){dirs=new string[0];}
    foreach(string d in dirs){
     bool link;try{link=(File.GetAttributes(d)&FileAttributes.ReparsePoint)!=0;}catch(Exception){link=true;}
     if(link)continue;
     yield return d;stack.Push(d);
    }
   }
  }

  /// <summary>Owner is written through a fresh descriptor (no read first) so a DACL that denies READ_CONTROL cannot stop the takeover.</summary>
  static bool OwnDirectory(string path,SecurityIdentifier admins){
   try{
    var info=new DirectoryInfo(path);
    var owner=new DirectorySecurity();owner.SetOwner(admins);info.SetAccessControl(owner);
    var security=info.GetAccessControl(AccessControlSections.Access);
    if(!RemoveDenies(security)){security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);}
    security.AddAccessRule(new FileSystemAccessRule(admins,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
    info.SetAccessControl(security);return true;
   }catch(Exception e){Log.Warn("Không chiếm được quyền thư mục "+path+": "+e.Message);return false;}
  }

  static bool OwnFile(string path,SecurityIdentifier admins){
   try{
    var info=new FileInfo(path);
    var owner=new FileSecurity();owner.SetOwner(admins);info.SetAccessControl(owner);
    var security=info.GetAccessControl(AccessControlSections.Access);
    if(!RemoveDenies(security)){security=new FileSecurity();security.SetAccessRuleProtection(true,false);}
    security.AddAccessRule(new FileSystemAccessRule(admins,FileSystemRights.FullControl,AccessControlType.Allow));
    info.SetAccessControl(security);return true;
   }catch(Exception e){Log.Warn("Không chiếm được quyền tệp "+path+": "+e.Message);return false;}
  }

  /// <summary>
  /// Drops every Deny entry. Inherited denies cannot be removed in place, so inheritance is cut without preserving them
  /// (the item is about to be moved or deleted anyway). Returns false when the DACL is non-canonical and must be rebuilt.
  /// </summary>
  static bool RemoveDenies(FileSystemSecurity security){
   try{
    var rules=security.GetAccessRules(true,true,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();
    if(rules.Any(r=>r.AccessControlType==AccessControlType.Deny&&r.IsInherited))security.SetAccessRuleProtection(true,false);
    foreach(var rule in security.GetAccessRules(true,false,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Where(r=>r.AccessControlType==AccessControlType.Deny).ToList())security.RemoveAccessRuleAll(rule);
    return true;
   }catch(InvalidOperationException){return false;}
  }

  /// <summary>Enables one privilege on the process token; false when the token does not hold it (AdjustTokenPrivileges reports ERROR_NOT_ALL_ASSIGNED).</summary>
  static bool EnablePrivilege(string name){
   IntPtr token;
   if(!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle,TokenAdjustPrivileges|TokenQuery,out token))return false;
   try{
    var privileges=new TokenPrivileges{PrivilegeCount=1,Attributes=SePrivilegeEnabled};
    if(!LookupPrivilegeValue(null,name,out privileges.Luid))return false;
    if(!AdjustTokenPrivileges(token,false,ref privileges,0,IntPtr.Zero,IntPtr.Zero))return false;
    return Marshal.GetLastWin32Error()==0;
   }finally{CloseHandle(token);}
  }

  const int MoveFileDelayUntilReboot=0x4;
  const uint TokenAdjustPrivileges=0x20, TokenQuery=0x8, SePrivilegeEnabled=0x2;
  /// <summary>Native LUID: two 32-bit halves, 4-byte aligned.</summary>
  [StructLayout(LayoutKind.Sequential,Pack=4)] internal struct Luid{public uint Low;public int High;}
  /// <summary>Native TOKEN_PRIVILEGES with one entry: {DWORD Count; LUID; DWORD Attributes} = 16 bytes, LUID at offset 4.</summary>
  [StructLayout(LayoutKind.Sequential,Pack=4)] internal struct TokenPrivileges{public uint PrivilegeCount;public Luid Luid;public uint Attributes;}
  /// <summary>Exposed for the self-test so the interop layout is verified on every platform.</summary>
  public static int TokenPrivilegesSize { get { return Marshal.SizeOf(typeof(TokenPrivileges)); } }
  public static int TokenPrivilegesLuidOffset { get { return (int)Marshal.OffsetOf(typeof(TokenPrivileges),"Luid"); } }
  public static int TokenPrivilegesAttributesOffset { get { return (int)Marshal.OffsetOf(typeof(TokenPrivileges),"Attributes"); } }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool MoveFileEx(string existing,string target,int flags);
  [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
  [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool LookupPrivilegeValue(string system,string name,out Luid luid);
  [DllImport("advapi32.dll",SetLastError=true)] static extern bool AdjustTokenPrivileges(IntPtr token,bool disableAll,ref TokenPrivileges newState,uint length,IntPtr previous,IntPtr returnLength);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
 }
}
