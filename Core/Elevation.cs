using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace TweekPro.Core {
 /// <summary>Reports whether the current process runs as administrator and relaunches it with a single UAC prompt on request.</summary>
 public static class Elevation {
  static bool? elevated;

  /// <summary>True when the process token belongs to the local Administrators group with elevation.</summary>
  public static bool IsElevated {
   get {
    if(elevated==null){
     try{using(var identity=WindowsIdentity.GetCurrent())elevated=new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);}
     catch(Exception){elevated=false;}
    }
    return elevated.Value;
   }
  }

  /// <summary>Vietnamese badge text for the header.</summary>
  public static string BadgeText { get { return IsElevated?"Quyền quản trị viên":"Quyền người dùng"; } }

  /// <summary>Starts a second instance of this executable with the runas verb; the caller closes the current instance when this returns true.</summary>
  public static bool RelaunchAsAdministrator(string arguments=""){
   string exe=Process.GetCurrentProcess().MainModule.FileName;
   if(!File.Exists(exe))throw new IOException("Không xác định được tệp thực thi hiện tại.");
   var info=new ProcessStartInfo(exe,arguments??""){UseShellExecute=true,Verb="runas",WorkingDirectory=Path.GetDirectoryName(exe)};
   try{return Process.Start(info)!=null;}
   catch(System.ComponentModel.Win32Exception){return false;}
  }
 }
}
