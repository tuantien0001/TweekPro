using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace TweekPro.Extensions {
 /// <summary>What was cut out of one preference file so the vault can put it back.</summary>
 public class PrefEntry { public string File, Settings, Mac; public int Pinned=-1; }

 /// <summary>One ExtensionInstallForcelist policy value: where it lives and the extension id it forces.</summary>
 public class ForcelistEntry { public string Browser, Hive, View, Key, ValueName, Value, Id; }

 /// <summary>Vault payload (extension.xml) describing a removed extension.</summary>
 public class ExtensionBackupInfo {
  public string Browser, ProfileFolder, ExtensionId, Name, Folder, StoredFolder; public bool Firefox;
  public List<PrefEntry> Prefs=new List<PrefEntry>();
  public string RegistryHive, RegistryView, RegistryKey; public RegNode RegistryNode;
  public List<ForcelistEntry> Forcelist=new List<ForcelistEntry>();
 }

 /// <summary>
 /// Removes browser extensions while the browser is closed, always through the vault: the extension folder is moved, the
 /// extensions.settings entry (and its integrity MAC) is cut out of Preferences / Secure Preferences, registry install sources
 /// and ExtensionInstallForcelist values are saved then deleted. Firefox add-ons are moved out of the profile; Firefox drops
 /// them from its database on next start. Component extensions and add-ons installed outside the profile are refused.
 /// </summary>
 public static class ExtensionRemoval {
  public const string BackupKind="Extension";
  const string InfoPayload="extension.xml",ForcelistPayload="forcelist.xml";
  static readonly string[] PrefFiles={"Secure Preferences","Preferences"};
  public static readonly Dictionary<string,string[]> BrowserProcesses=new Dictionary<string,string[]>{{"Chrome",new[]{"chrome"}},{"Edge",new[]{"msedge"}},{"Brave",new[]{"brave"}},{"Vivaldi",new[]{"vivaldi"}},{"Chromium",new[]{"chromium","chrome"}},{"Opera",new[]{"opera"}},{"Firefox",new[]{"firefox"}}};
  static readonly Dictionary<string,string> PolicyKeys=new Dictionary<string,string>{{"Chrome",@"SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist"},{"Edge",@"SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallForcelist"},{"Brave",@"SOFTWARE\Policies\BraveSoftware\Brave\ExtensionInstallForcelist"},{"Chromium",@"SOFTWARE\Policies\Chromium\ExtensionInstallForcelist"}};
  static readonly Dictionary<string,string> ExternalKeys=new Dictionary<string,string>{{"Chrome",@"Software\Google\Chrome\Extensions"},{"Edge",@"Software\Microsoft\Edge\Extensions"},{"Chromium",@"Software\Chromium\Extensions"}};
  /// <summary>Test hook: replaces the policy key table (browser → HKCU/HKLM relative key).</summary>
  public static Dictionary<string,string> PolicyKeyOverride;
  static Dictionary<string,string> Policies { get { return PolicyKeyOverride??PolicyKeys; } }
  static bool Windows { get { return Environment.OSVersion.Platform==PlatformID.Win32NT; } }
  static readonly JsonSerializerOptions Compact=new JsonSerializerOptions{Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping,WriteIndented=false};

  /// <summary>Test hook: replaces the running-process check.</summary>
  public static Func<string,List<string>> RunningOverride;

  /// <summary>Process names of the browser that are currently running.</summary>
  public static List<string> Running(string browser){
   if(RunningOverride!=null)return RunningOverride(browser)??new List<string>();
   var running=new List<string>();string[] names;if(!BrowserProcesses.TryGetValue(browser??"",out names))return running;
   foreach(string name in names){try{var ps=Process.GetProcessesByName(name);if(ps.Length>0)running.Add(name);foreach(var p in ps)p.Dispose();}catch(Exception){}}
   return running;
  }

  /// <summary>Asks every window of the browser to close and waits up to the timeout; false when processes remain.</summary>
  public static bool CloseBrowser(string browser,TimeSpan timeout){
   string[] names;if(!BrowserProcesses.TryGetValue(browser??"",out names))return true;
   foreach(string name in names)foreach(var p in Process.GetProcessesByName(name)){try{if(p.MainWindowHandle!=IntPtr.Zero)p.CloseMainWindow();}catch(Exception){}finally{p.Dispose();}}
   var deadline=DateTime.UtcNow+timeout;
   while(DateTime.UtcNow<deadline){if(Running(browser).Count==0)return true;System.Threading.Thread.Sleep(400);}
   return Running(browser).Count==0;
  }

  /// <summary>Null when the extension can be removed by Tweek Pro; otherwise the reason it must be handled elsewhere.</summary>
  public static string RefuseReason(BrowserExtension e){
   if(e==null)return "Chưa chọn tiện ích.";
   if(e.Browser=="Firefox"){
    if(String.IsNullOrEmpty(e.Folder))return "Tiện ích Firefox này không có tệp trong hồ sơ (nhúng sẵn).";
    if(e.Source.StartsWith("Cài ngoài"))return "Cài từ registry/thư mục hệ thống — gỡ nguồn cài ở HKLM\\Software\\Mozilla\\Firefox\\Extensions hoặc thư mục cài của Firefox.";
    return null;
   }
   if(e.Location==5||e.Location==10)return "Tiện ích thành phần của trình duyệt, không gỡ được.";
   if(String.IsNullOrEmpty(e.Id)||e.Id.Length!=32||!e.Id.All(c=>c>='a'&&c<='p'))return "Mã tiện ích không hợp lệ.";
   return null;
  }

  static void RequireClosed(string browser){var running=Running(browser);if(running.Count>0)throw new IOException("Đóng "+browser+" ("+String.Join(", ",running)+") rồi thử lại: trình duyệt ghi lại cấu hình khi thoát và sẽ hủy thay đổi.");}

  /// <summary>Removes one extension into the vault; the profile folder is where its Preferences live (Firefox: the profile root).</summary>
  public static Backup Remove(BrowserExtension e,string profileFolder){
   string refuse=RefuseReason(e);if(refuse!=null)throw new IOException(refuse);
   if(String.IsNullOrEmpty(profileFolder)||!Directory.Exists(profileFolder))throw new IOException("Không tìm thấy thư mục hồ sơ: "+profileFolder);
   RequireClosed(e.Browser);Engine.NoLinks(Engine.Vault,false);
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Kind=BackupKind,Purpose=e.Browser+"|"+e.Id,AppName=e.Name+" ("+e.Browser+")",Original=String.IsNullOrEmpty(e.Folder)?profileFolder:e.Folder,Payload=InfoPayload};
   string folder=Path.Combine(Engine.Vault,backup.Id);Directory.CreateDirectory(folder);Engine.SaveBackup(backup);
   var info=new ExtensionBackupInfo{Browser=e.Browser,ProfileFolder=Engine.Canon(profileFolder),ExtensionId=e.Id,Name=e.Name,Folder=e.Folder??"",Firefox=e.Browser=="Firefox"};
   try{
    if(info.Firefox)RemoveFirefox(e,info,folder);else RemoveChromium(e,info,folder);
    Engine.Save(Path.Combine(folder,InfoPayload),info);Engine.Load<ExtensionBackupInfo>(Path.Combine(folder,InfoPayload));
    backup.State="BackedUp";Engine.SaveBackup(backup);
   }catch(Exception ex){try{Engine.Save(Path.Combine(folder,InfoPayload),info);}catch(Exception){}backup.State="NeedsReview";backup.Error=ex.Message;Engine.SaveBackup(backup);throw;}
   return backup;
  }

  static void RemoveFirefox(BrowserExtension e,ExtensionBackupInfo info,string vaultFolder){
   string path=Engine.Canon(e.Folder);string extDir=Path.Combine(info.ProfileFolder,"extensions");
   if(!Engine.Under(path,extDir))throw new IOException("Tiện ích nằm ngoài thư mục extensions của hồ sơ: "+path);
   if(!File.Exists(path)&&!Directory.Exists(path))throw new IOException("Không tìm thấy tệp tiện ích: "+path);
   string stored=Path.Combine(vaultFolder,"folder");Directory.CreateDirectory(stored);string target=Path.Combine(stored,Path.GetFileName(path));
   if(File.Exists(path)){Engine.NoLinks(path,false);File.Move(path,target);}else{Engine.NoLinks(path,true);MoveDirectory(path,target);}
   info.StoredFolder=Path.GetFileName(path);
  }

  static void RemoveChromium(BrowserExtension e,ExtensionBackupInfo info,string vaultFolder){
   string prefsCopy=Path.Combine(vaultFolder,"prefs");Directory.CreateDirectory(prefsCopy);
   foreach(string file in PrefFiles){
    string path=Path.Combine(info.ProfileFolder,file);if(!File.Exists(path))continue;
    File.Copy(path,Path.Combine(prefsCopy,file),true);
    var entry=CutFromPrefs(path,e.Id);if(entry!=null)info.Prefs.Add(entry);
   }
   string idRoot=Path.Combine(info.ProfileFolder,"Extensions",e.Id);
   if(e.Location!=4&&Directory.Exists(idRoot)&&(String.IsNullOrEmpty(e.Folder)||Engine.Under(Engine.Canon(e.Folder),idRoot)||Engine.Canon(e.Folder)==idRoot)){
    Engine.NoLinks(idRoot,true);string stored=Path.Combine(vaultFolder,"folder");MoveDirectory(idRoot,stored);info.StoredFolder="folder";
   }
   if(Windows){
    string external;
    if(ExternalKeys.TryGetValue(e.Browser,out external))foreach(string hive in new[]{"HKCU","HKLM"})foreach(string view in Advanced.Views){
     try{using(var root=Engine.Base(hive,view)){string key=external+"\\"+e.Id;RegNode node;using(var k=root.OpenSubKey(key)){if(k==null)continue;node=Engine.ReadTree(k);}info.RegistryHive=hive;info.RegistryView=view;info.RegistryKey=key;info.RegistryNode=node;root.DeleteSubKeyTree(key,false);}}catch(Exception ex){if(ex is UnauthorizedAccessException||ex is System.Security.SecurityException)continue;throw;}
     if(info.RegistryKey!=null)break;
    }
    var forced=ForcelistEntries().Where(f=>f.Browser==e.Browser&&f.Id.Equals(e.Id,StringComparison.OrdinalIgnoreCase)).ToList();
    if(forced.Count>0){DeleteForcelist(forced);info.Forcelist=forced;}
   }
  }

  /// <summary>Removes extensions.settings[id], protection.macs.extensions.settings[id] and the pinned/toolbar references from one preference file; null when nothing referenced the id.</summary>
  public static PrefEntry CutFromPrefs(string path,string id){
   JsonNode root;string text=File.ReadAllText(path,Encoding.UTF8);
   try{root=JsonNode.Parse(text,null,new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip});}catch(JsonException ex){throw new IOException(Path.GetFileName(path)+" không đọc được: "+ex.Message);}
   var obj=root as JsonObject;if(obj==null)return null;
   var entry=new PrefEntry{File=Path.GetFileName(path)};bool changed=false;
   var settings=Walk(obj,"extensions","settings");
   if(settings!=null){JsonNode removed;if(settings.TryGetPropertyValue(id,out removed)&&removed!=null){entry.Settings=removed.ToJsonString(Compact);settings.Remove(id);changed=true;}}
   var macs=Walk(obj,"protection","macs","extensions","settings");
   if(macs!=null){JsonNode removed;if(macs.TryGetPropertyValue(id,out removed)&&removed!=null){entry.Mac=removed.ToJsonString(Compact);macs.Remove(id);changed=true;}}
   foreach(string list in new[]{"pinned_extensions","toolbar"}){
    var ext=Walk(obj,"extensions");JsonNode arrNode;var arr=ext!=null&&ext.TryGetPropertyValue(list,out arrNode)?arrNode as JsonArray:null;if(arr==null)continue;
    for(int i=arr.Count-1;i>=0;i--){var s=arr[i] as JsonValue;string v;if(s!=null&&s.TryGetValue(out v)&&String.Equals(v,id,StringComparison.OrdinalIgnoreCase)){if(list=="pinned_extensions")entry.Pinned=i;arr.RemoveAt(i);changed=true;}}
   }
   if(!changed)return null;
   WriteAtomic(path,root.ToJsonString(Compact));
   return entry;
  }

  /// <summary>Puts a saved settings entry and MAC back when the live file no longer has them; the pinned reference is appended.</summary>
  public static void MergeIntoPrefs(string path,string id,PrefEntry entry){
   if(!File.Exists(path))return;
   var obj=JsonNode.Parse(File.ReadAllText(path,Encoding.UTF8),null,new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip}) as JsonObject;if(obj==null)return;
   bool changed=false;
   if(entry.Settings!=null){var settings=Ensure(obj,"extensions","settings");if(!settings.ContainsKey(id)){settings[id]=JsonNode.Parse(entry.Settings);changed=true;}}
   if(entry.Mac!=null){var macs=Ensure(obj,"protection","macs","extensions","settings");if(!macs.ContainsKey(id)){macs[id]=JsonNode.Parse(entry.Mac);changed=true;}}
   if(entry.Pinned>=0){var ext=Ensure(obj,"extensions");JsonNode arrNode;var arr=ext.TryGetPropertyValue("pinned_extensions",out arrNode)?arrNode as JsonArray:null;if(arr==null){arr=new JsonArray();ext["pinned_extensions"]=arr;}if(!arr.Any(n=>{string v;var jv=n as JsonValue;return jv!=null&&jv.TryGetValue(out v)&&v==id;})){arr.Add(id);changed=true;}}
   if(changed)WriteAtomic(path,obj.ToJsonString(Compact));
  }

  static JsonObject Walk(JsonObject obj,params string[] names){JsonObject cur=obj;foreach(string n in names){JsonNode next;if(cur==null||!cur.TryGetPropertyValue(n,out next))return null;cur=next as JsonObject;}return cur;}
  static JsonObject Ensure(JsonObject obj,params string[] names){JsonObject cur=obj;foreach(string n in names){JsonNode next;JsonObject child=cur.TryGetPropertyValue(n,out next)?next as JsonObject:null;if(child==null){child=new JsonObject();cur[n]=child;}cur=child;}return cur;}

  static void WriteAtomic(string path,string text){
   string tmp=path+".tweekpro.tmp";File.WriteAllText(tmp,text,new UTF8Encoding(false));
   if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
  }

  static void MoveDirectory(string source,string target){
   try{Directory.Move(source,target);return;}catch(IOException){}
   CopyTree(source,target);Directory.Delete(source,true);
  }
  static void CopyTree(string source,string target){
   Directory.CreateDirectory(target);
   foreach(string f in Directory.GetFiles(source))File.Copy(f,Path.Combine(target,Path.GetFileName(f)),true);
   foreach(string d in Directory.GetDirectories(source)){if((File.GetAttributes(d)&FileAttributes.ReparsePoint)!=0)continue;CopyTree(d,Path.Combine(target,Path.GetFileName(d)));}
  }

  /// <summary>Every ExtensionInstallForcelist value of every supported browser in HKLM and HKCU (both registry views).</summary>
  public static List<ForcelistEntry> ForcelistEntries(){
   var list=new List<ForcelistEntry>();if(!Windows)return list;
   foreach(var pair in Policies)foreach(string hive in new[]{"HKLM","HKCU"})foreach(string view in Advanced.Views){
    try{using(var root=Engine.Base(hive,view))using(var k=root.OpenSubKey(pair.Value)){if(k==null)continue;foreach(string name in k.GetValueNames()){string v=Convert.ToString(k.GetValue(name));int semi=v.IndexOf(';');list.Add(new ForcelistEntry{Browser=pair.Key,Hive=hive,View=view,Key=pair.Value,ValueName=name,Value=v,Id=semi>0?v.Substring(0,semi):v});}}}catch(Exception){}
   }
   return list.GroupBy(f=>f.Hive+"|"+(f.Hive=="HKCU"?"":f.View)+"|"+f.Key+"|"+f.ValueName,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
  }

  static void ValidatePolicyKey(string key){if(!Policies.Values.Any(k=>String.Equals(k,key,StringComparison.OrdinalIgnoreCase)))throw new IOException("Khóa chính sách nằm ngoài danh sách Forcelist cho phép: "+key);}

  static void DeleteForcelist(IEnumerable<ForcelistEntry> entries){
   foreach(var g in entries.GroupBy(f=>f.Hive+"|"+f.View+"|"+f.Key)){
    var first=g.First();ValidatePolicyKey(first.Key);
    using(var root=Engine.Base(first.Hive,first.View)){
     using(var k=root.OpenSubKey(first.Key,true)){if(k==null)continue;foreach(var f in g)k.DeleteValue(f.ValueName,false);if(k.GetValueNames().Length==0&&k.GetSubKeyNames().Length==0){k.Close();root.DeleteSubKey(first.Key,false);}}
    }
   }
  }

  /// <summary>Saves the given Forcelist values to the vault and deletes them; the browser stops forcing those extensions on its next start.</summary>
  public static Backup RemoveForcelist(IEnumerable<ForcelistEntry> entries){
   var list=entries.ToList();if(list.Count==0)throw new IOException("Không có giá trị Forcelist nào để dọn.");
   foreach(var f in list)ValidatePolicyKey(f.Key);
   Engine.NoLinks(Engine.Vault,false);
   var backup=new Backup{Id=Guid.NewGuid().ToString("N"),Created=DateTime.Now.ToString("s"),State="Pending",Kind=BackupKind,Purpose="forcelist",AppName="ExtensionInstallForcelist ("+list.Count+")",Original=String.Join(" | ",list.Select(f=>f.Hive+"\\"+f.Key).Distinct()),Payload=ForcelistPayload};
   string folder=Path.Combine(Engine.Vault,backup.Id);Directory.CreateDirectory(folder);Engine.SaveBackup(backup);
   try{Engine.Save(Path.Combine(folder,ForcelistPayload),list);DeleteForcelist(list);backup.State="BackedUp";Engine.SaveBackup(backup);}
   catch(Exception ex){backup.State="NeedsReview";backup.Error=ex.Message;Engine.SaveBackup(backup);throw;}
   return backup;
  }

  static void RestoreForcelist(IEnumerable<ForcelistEntry> entries){
   foreach(var f in entries){ValidatePolicyKey(f.Key);using(var root=Engine.Base(f.Hive,f.View))using(var k=root.CreateSubKey(f.Key))if(k.GetValue(f.ValueName)==null)k.SetValue(f.ValueName,f.Value??"",RegistryValueKind.String);}
  }

  /// <summary>Puts an extension (or Forcelist values) back; the browser must be closed. If the browser later rejects the restored settings entry, reinstall from the store.</summary>
  public static void Restore(Backup b){
   Guid id;if(!Guid.TryParseExact(b.Id,"N",out id))throw new IOException("Mã sao lưu không hợp lệ.");
   if(b.Kind!=BackupKind)throw new IOException("Không phải bản sao lưu tiện ích.");
   string folder=Path.Combine(Engine.VaultOf(b),b.Id);Engine.NoLinks(folder,true);
   if(b.Payload==ForcelistPayload){
    if(!Windows)throw new IOException("Chỉ khôi phục chính sách trên Windows.");
    RestoreForcelist(Engine.Load<List<ForcelistEntry>>(Path.Combine(folder,ForcelistPayload)));b.State="Restored";b.Error="";Engine.SaveBackup(b);return;
   }
   var info=Engine.Load<ExtensionBackupInfo>(Path.Combine(folder,InfoPayload));
   RequireClosed(info.Browser);
   if(!Directory.Exists(info.ProfileFolder))throw new IOException("Hồ sơ trình duyệt không còn: "+info.ProfileFolder);
   var notes=new List<string>();
   if(!String.IsNullOrEmpty(info.StoredFolder)){
    string stored=Path.Combine(folder,"folder");
    if(info.Firefox){string src=Path.Combine(stored,info.StoredFolder);string dst=Path.Combine(info.ProfileFolder,"extensions",info.StoredFolder);if(File.Exists(dst)||Directory.Exists(dst))notes.Add("Tệp tiện ích đã tồn tại, giữ bản hiện có.");else if(File.Exists(src)){Directory.CreateDirectory(Path.GetDirectoryName(dst));File.Move(src,dst);}else if(Directory.Exists(src))MoveDirectory(src,dst);}
    else{string dst=Path.Combine(info.ProfileFolder,"Extensions",info.ExtensionId);if(Directory.Exists(dst))notes.Add("Thư mục tiện ích đã tồn tại, giữ bản hiện có.");else if(Directory.Exists(stored)){Directory.CreateDirectory(Path.GetDirectoryName(dst));MoveDirectory(stored,dst);}}
   }
   foreach(var entry in info.Prefs)MergeIntoPrefs(Path.Combine(info.ProfileFolder,entry.File),info.ExtensionId,entry);
   if(Windows&&info.RegistryKey!=null&&info.RegistryNode!=null){try{using(var root=Engine.Base(info.RegistryHive,info.RegistryView))using(var k=root.CreateSubKey(info.RegistryKey))Engine.WriteTree(k,info.RegistryNode);}catch(Exception ex){notes.Add("Registry: "+ex.Message);}}
   if(Windows&&info.Forcelist.Count>0)RestoreForcelist(info.Forcelist);
   b.State="Restored";b.Error=String.Join(" ",notes);Engine.SaveBackup(b);
  }

  public static string BackupKindLabel(){return Core.L.T("Tiện ích trình duyệt");}
 }
}
