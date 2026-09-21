using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace TweekPro.Core {
 /// <summary>User preferences persisted as settings.json in the data directory. Only non-sensitive values are stored.</summary>
 [DataContract]
 public class Settings {
  [DataMember(Name="version")] public int Version=1;
  [DataMember(Name="deepScanAfterUninstall")] public bool DeepScanAfterUninstall=true;
  [DataMember(Name="networkRefreshSeconds")] public int NetworkRefreshSeconds=2;
  [DataMember(Name="networkResolveHosts")] public bool NetworkResolveHosts=false;
  [DataMember(Name="networkStartBandwidthWhenElevated")] public bool NetworkStartBandwidthWhenElevated=true;
  [DataMember(Name="purgeDefaultDays")] public int PurgeDefaultDays=90;
  [DataMember(Name="junkMinAgeHours")] public int JunkMinAgeHours=24;
  [DataMember(Name="duplicateMinKB")] public int DuplicateMinKB=1;
  [DataMember(Name="duplicateKeepNewest")] public bool DuplicateKeepNewest=false;
  [DataMember(Name="logRetentionDays")] public int LogRetentionDays=14;
  [DataMember(Name="windowWidth")] public int WindowWidth=0;
  [DataMember(Name="windowHeight")] public int WindowHeight=0;
  [DataMember(Name="language")] public string Language=L.Vietnamese;

  /// <summary>Clamps values that would make the UI unusable if the file was edited by hand.</summary>
  public void Normalize(){
   if(NetworkRefreshSeconds<1)NetworkRefreshSeconds=1;if(NetworkRefreshSeconds>30)NetworkRefreshSeconds=30;
   if(PurgeDefaultDays<0)PurgeDefaultDays=0;if(PurgeDefaultDays>3650)PurgeDefaultDays=3650;
   if(JunkMinAgeHours<0)JunkMinAgeHours=0;if(JunkMinAgeHours>24*30)JunkMinAgeHours=24*30;
   if(DuplicateMinKB<0)DuplicateMinKB=0;if(DuplicateMinKB>1048576)DuplicateMinKB=1048576;
   if(LogRetentionDays<1)LogRetentionDays=1;if(LogRetentionDays>365)LogRetentionDays=365;
   Language=L.Normalize(Language);
   if(WindowWidth<0)WindowWidth=0;if(WindowHeight<0)WindowHeight=0;
  }

  /// <summary>Loads settings from the given file, returning defaults when the file is missing or unreadable.</summary>
  public static Settings Load(string path){
   try{
    if(!File.Exists(path))return new Settings();
    using(var stream=File.OpenRead(path)){
     var settings=(Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream)??new Settings();
     settings.Normalize();return settings;
    }
   }catch(Exception){return new Settings();}
  }

  /// <summary>Writes settings atomically (temp file then replace) so a crash never leaves a truncated file.</summary>
  public void Save(string path){
   Normalize();
   string dir=Path.GetDirectoryName(path);if(!String.IsNullOrEmpty(dir))Directory.CreateDirectory(dir);
   string tmp=path+".tmp";
   using(var stream=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None))
   using(var writer=JsonReaderWriterFactory.CreateJsonWriter(stream,Encoding.UTF8,true,true,"  ")){
    new DataContractJsonSerializer(typeof(Settings)).WriteObject(writer,this);writer.Flush();stream.Flush(true);
   }
   if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
  }
 }
}
