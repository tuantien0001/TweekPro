using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TweekPro.Core {
 /// <summary>Captured result of a PowerShell invocation.</summary>
 public class PowerShellResult {
  public int ExitCode;
  public string Output="", Error="";
 }

 /// <summary>Runs Windows PowerShell non-interactively with an encoded command so quoting never depends on the shell.</summary>
 public static class PowerShell {
  /// <summary>Base64 of the UTF-16LE script, the format -EncodedCommand expects.</summary>
  public static string Encode(string script){return Convert.ToBase64String(Encoding.Unicode.GetBytes(script??""));}
  public static string Decode(string encoded){return Encoding.Unicode.GetString(Convert.FromBase64String(encoded??""));}

  /// <summary>Full argument line for powershell.exe: no profile, non-interactive, bypass policy for this process only.</summary>
  public static string Arguments(string script){return "-NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text -EncodedCommand "+Encode(script);}

  /// <summary>Location of Windows PowerShell 5.1; null off Windows or when missing.</summary>
  public static string Executable {
   get {
    if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return null;
    string sys=Environment.GetFolderPath(Environment.SpecialFolder.System);
    string path=Path.Combine(sys,@"WindowsPowerShell\v1.0\powershell.exe");
    return File.Exists(path)?path:null;
   }
  }

  public static PowerShellResult Run(string script,TimeSpan timeout){
   string exe=Executable;
   if(exe==null)throw new IOException(L.T("Không tìm thấy Windows PowerShell; tính năng này chỉ chạy trên Windows."));
   var info=new ProcessStartInfo(exe,Arguments(script)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   var result=new PowerShellResult();
   using(var p=Process.Start(info)){
    var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
    if(!p.WaitForExit((int)timeout.TotalMilliseconds)){try{p.Kill();}catch(Exception){}throw new IOException(L.T("PowerShell không phản hồi trong thời gian cho phép."));}
    result.Output=output.Result.Trim();result.Error=error.Result.Trim();result.ExitCode=p.ExitCode;
   }
   return result;
  }
 }
}
