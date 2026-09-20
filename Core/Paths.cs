using System;
using System.IO;

namespace TweekPro.Core {
 /// <summary>Outcome of the one-time data directory migration from AppCare to Tweek Pro.</summary>
 public enum MigrationOutcome { NothingToMigrate, AlreadyMigrated, Moved, Failed }

 /// <summary>Result details for a data directory migration attempt.</summary>
 public class MigrationResult {
  public MigrationOutcome Outcome; public string Source, Target, Error;
  public override string ToString(){return Outcome+" "+Source+" -> "+Target+(String.IsNullOrEmpty(Error)?"":" ("+Error+")");}
 }

 /// <summary>Locations of Tweek Pro data on this machine, including the legacy AppCare folder kept readable after rebranding.</summary>
 public static class Paths {
  public const string ProductFolder="TweekPro";
  public const string LegacyFolder="AppCare";
  static string root,legacy;

  /// <summary>%LOCALAPPDATA%\TweekPro (or an override set for tests).</summary>
  public static string Root {
   get { return root??(root=Path.Combine(LocalAppData(),ProductFolder)); }
   set { root=value; }
  }
  /// <summary>%LOCALAPPDATA%\AppCare, the data folder used by versions before 0.7.</summary>
  public static string Legacy {
   get { return legacy??(legacy=Path.Combine(LocalAppData(),LegacyFolder)); }
   set { legacy=value; }
  }
  public static string Backups { get { return Path.Combine(Root,"Backups"); } }
  public static string LegacyBackups { get { return Path.Combine(Legacy,"Backups"); } }
  public static string Sessions { get { return Path.Combine(Root,"sessions.xml"); } }
  public static string SettingsFile { get { return Path.Combine(Root,"settings.json"); } }
  public static string Logs { get { return Path.Combine(Root,"Logs"); } }
  public static string JunkRulesOverride { get { return Path.Combine(Root,"junk-rules.json"); } }

  static string LocalAppData(){
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   if(String.IsNullOrWhiteSpace(local))local=Path.Combine(Path.GetTempPath(),"TweekPro-fallback");
   return local;
  }

  /// <summary>Moves the legacy AppCare folder to the Tweek Pro location when the new folder does not exist yet; never merges or overwrites.</summary>
  public static MigrationResult Migrate(){return Migrate(Legacy,Root);}

  /// <summary>Testable core of the migration: renames source to target only when target is absent and source is a plain directory.</summary>
  public static MigrationResult Migrate(string source,string target){
   var result=new MigrationResult{Source=source,Target=target};
   try{
    if(Directory.Exists(target)){result.Outcome=Directory.Exists(source)?MigrationOutcome.AlreadyMigrated:MigrationOutcome.NothingToMigrate;return result;}
    if(!Directory.Exists(source)){result.Outcome=MigrationOutcome.NothingToMigrate;return result;}
    if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("Thư mục nguồn là liên kết; không di chuyển.");
    string parent=Path.GetDirectoryName(target);
    if(!String.IsNullOrEmpty(parent))Directory.CreateDirectory(parent);
    Directory.Move(source,target);
    result.Outcome=MigrationOutcome.Moved;
   }catch(Exception e){result.Outcome=MigrationOutcome.Failed;result.Error=e.Message;}
   return result;
  }
 }
}
