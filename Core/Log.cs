using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TweekPro.Core {
 public enum LogLevel { Info, Warn, Error }

 /// <summary>Daily rotating text log (tweekpro-yyyyMMdd.log) with a retention limit; never records secrets or file contents.</summary>
 public static class Log {
  static readonly object gate=new object();
  static string directory;static int retentionDays=14;static string lastPruned="";
  public const string Prefix="tweekpro-";

  /// <summary>Points the log at a directory and sets how many daily files to keep.</summary>
  public static void Configure(string folder,int keepDays){lock(gate){directory=folder;retentionDays=Math.Max(1,keepDays);lastPruned="";}}
  public static string Directory { get { return directory; } }

  public static void Info(string message){Write(LogLevel.Info,message);}
  public static void Warn(string message){Write(LogLevel.Warn,message);}
  public static void Error(string message){Write(LogLevel.Error,message);}
  public static void Error(string message,Exception e){Write(LogLevel.Error,message+(e==null?"":" | "+e.GetType().Name+": "+e.Message));}

  /// <summary>Returns the file name for a given date so tests and pruning share one convention.</summary>
  public static string FileFor(DateTime date){return Prefix+date.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+".log";}

  /// <summary>Appends one line; failures are swallowed because logging must never break a user action.</summary>
  public static void Write(LogLevel level,string message){
   string dir=directory;if(String.IsNullOrEmpty(dir))return;
   string line=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)+" ["+level.ToString().ToUpperInvariant().PadRight(5)+"] "+(message??"").Replace("\r"," ").Replace("\n"," ")+Environment.NewLine;
   lock(gate){
    try{
     System.IO.Directory.CreateDirectory(dir);
     string today=DateTime.Now.ToString("yyyyMMdd",CultureInfo.InvariantCulture);
     File.AppendAllText(Path.Combine(dir,FileFor(DateTime.Now)),line,Encoding.UTF8);
     if(lastPruned!=today){lastPruned=today;Prune(dir,retentionDays,DateTime.Now);}
    }catch(Exception){}
   }
  }

  /// <summary>Deletes log files older than the retention window; only files matching the tweekpro-yyyyMMdd.log pattern are touched.</summary>
  public static int Prune(string dir,int keepDays,DateTime now){
   int removed=0;if(!System.IO.Directory.Exists(dir))return 0;
   DateTime cutoff=now.Date.AddDays(-Math.Max(1,keepDays)+1);
   foreach(string file in System.IO.Directory.GetFiles(dir,Prefix+"*.log")){
    DateTime stamp;if(!TryParseStamp(Path.GetFileName(file),out stamp))continue;
    if(stamp<cutoff){try{File.Delete(file);removed++;}catch(IOException){}catch(UnauthorizedAccessException){}}
   }
   return removed;
  }

  /// <summary>Extracts the date from a rotated log file name.</summary>
  public static bool TryParseStamp(string fileName,out DateTime stamp){
   stamp=DateTime.MinValue;
   if(fileName==null||!fileName.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase)||!fileName.EndsWith(".log",StringComparison.OrdinalIgnoreCase))return false;
   string digits=fileName.Substring(Prefix.Length,fileName.Length-Prefix.Length-4);
   return digits.Length==8&&digits.All(Char.IsDigit)&&DateTime.TryParseExact(digits,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out stamp);
  }
 }
}
