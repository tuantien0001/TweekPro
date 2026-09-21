using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TweekPro.Core;

namespace TweekPro.AI {
 /// <summary>Executes tools on behalf of the assistant; implemented by the main window over its live data.</summary>
 public interface IAiHost {
  /// <summary>Runs a tool and returns a plain-text (or JSON) result for the model. Mutating tools are only called after the user confirmed.</summary>
  Task<string> RunTool(string name,JsonElement arguments);
  /// <summary>Asks the user to approve a system-changing tool call; the description is what the model is about to do.</summary>
  Task<bool> ConfirmAction(string description);
 }

 /// <summary>Tool catalog shared by both providers: read-only queries run freely, mutating ones need a confirmation.</summary>
 public static class AiTools {
  static AiTool Tool(string name,string description,bool mutating,params object[] parameters){
   var props=new Dictionary<string,object>();var required=new List<object>();
   for(int i=0;i+3<=parameters.Length;i+=3){
    string pname=(string)parameters[i];string ptype=(string)parameters[i+1];string pdesc=(string)parameters[i+2];
    bool req=pname.EndsWith("!");if(req)pname=pname.TrimEnd('!');
    var spec=new Dictionary<string,object>{{"type",ptype},{"description",pdesc}};
    props[pname]=spec;if(req)required.Add(pname);
   }
   var schema=new Dictionary<string,object>{{"type","object"},{"properties",props}};
   if(required.Count>0)schema["required"]=required;
   return new AiTool{Name=name,Description=description,Mutating=mutating,Parameters=schema};
  }

  public static List<AiTool> All(){
   return new List<AiTool>{
    Tool("get_overview","Snapshot of this PC and of Tweek Pro: OS, whether running as administrator, UI language, counts of installed apps, services, startup entries, vault backups, and the last health score if one was computed. Call this first when the user asks a broad question.",false),
    Tool("list_applications","Installed desktop applications (name, version, publisher, size, install date, install folder). Filter with a case-insensitive substring.",false,"query","string","Substring to match in name or publisher; empty for all.","limit","integer","Maximum rows (default 40)."),
    Tool("list_services","Windows services with Tweek Pro's plain-language explanation and safety verdict (core/windows/optional/thirdparty). filter: running | all | thirdparty | optional | stopped.",false,"filter","string","running | all | thirdparty | optional | stopped (default running).","query","string","Substring to match in name, display name, explanation or publisher.","limit","integer","Maximum rows (default 60)."),
    Tool("explain_service","Full explanation of one service by its short name (e.g. Spooler, wuauserv, AdobeARMservice): what it does, who installed it, whether it is safe to stop.",false,"name!","string","Service short name."),
    Tool("list_network_activity","Processes currently using the network, with connection counts, remote endpoint counts and current send/receive rates when bandwidth capture is on. Sorted by activity.",false,"limit","integer","Maximum rows (default 30)."),
    Tool("list_startup_entries","Programs that start with Windows (autorun registry keys and Startup folders) and whether each is currently enabled or disabled by Tweek Pro.",false),
    Tool("list_vault","Recovery Vault backups: what Tweek Pro removed or changed and can restore (files, registry, junk, services, Windows apps).",false,"limit","integer","Maximum rows (default 40)."),
    Tool("preview_junk","Read-only scan of junk rules (temp files, caches, logs, system junk): per-rule file count and bytes. Takes a few seconds. Nothing is deleted.",false),
    Tool("stop_service","Stop a Windows service. With disable=true also set start mode to Disabled and save the old mode to the Recovery Vault. Core Windows services are refused.",true,"name!","string","Service short name.","disable","boolean","Also disable so it does not start again (default false)."),
    Tool("start_service","Start a stopped Windows service.",true,"name!","string","Service short name."),
    Tool("end_process","Terminate a process by PID. Core Windows processes and Tweek Pro itself are refused.",true,"pid!","integer","Process id."),
    Tool("disable_startup_entry","Disable a startup entry by its name; the registration is backed up to the Recovery Vault so it can be re-enabled.",true,"name!","string","Startup entry name as returned by list_startup_entries."),
    Tool("uninstall_application","Open the official uninstaller of an installed application, then scan for leftovers. The user must confirm in Tweek Pro's dialogs; the uninstaller itself may ask again.",true,"name!","string","Application name as returned by list_applications (exact or unique substring)."),
    Tool("open_tab","Switch Tweek Pro to a tab so the user sees the data: overview | applications | windows_apps | leftovers | junk | empty | duplicates | analyzer | vault | startup | network | services | tools | log | ai.",false,"tab!","string","Tab key."),
   };
  }

  public static AiTool Find(string name){return All().FirstOrDefault(t=>t.Name==name);}

  /// <summary>Human-readable summary of a pending tool call for the confirmation dialog.</summary>
  public static string Describe(AiToolCall call){
   string args="";
   try{
    using(var doc=JsonDocument.Parse(String.IsNullOrWhiteSpace(call.ArgumentsJson)?"{}":call.ArgumentsJson))
     args=String.Join(", ",doc.RootElement.EnumerateObject().Select(p=>p.Name+" = "+p.Value.ToString()));
   }catch(JsonException){args=call.ArgumentsJson;}
   return call.Name+(args==""?"":" ("+args+")");
  }

  /// <summary>System prompt: who the assistant is, what it may do, and how to behave around destructive actions.</summary>
  public static string SystemPrompt(string language,bool elevated){
   return "You are the built-in assistant of Tweek Pro, a Windows management app (uninstall & leftovers, junk cleaner, duplicates, empty folders, disk analyzer, recovery vault, startup, realtime network view, services with plain-language explanations, Windows Store apps). "
    +"You answer questions about this PC using the tools and can manage the app on the user's behalf. Rules: "
    +"(1) Prefer tools over guessing; call get_overview first for broad questions. "
    +"(2) Read-only tools run immediately. Mutating tools (stop_service, start_service, end_process, disable_startup_entry, uninstall_application) show the user a confirmation dialog inside Tweek Pro; never claim an action happened unless the tool result says so. "
    +"(3) Never suggest stopping core Windows services or deleting anything under Windows\\System32; Tweek Pro refuses those anyway. Explain risks briefly before proposing a change. "
    +"(4) Be concise and concrete: use the actual names, PIDs, sizes returned by tools. Use short bullet lists for more than three items. "
    +"(5) Answer in "+(language=="en"?"English":"Vietnamese (tiếng Việt)")+" unless the user writes in another language. "
    +"The app is "+(elevated?"running as administrator.":"NOT running as administrator, so some actions may fail.")+" Today is "+DateTime.Now.ToString("yyyy-MM-dd")+".";
  }
 }

 /// <summary>
 /// Drives the conversation: sends the history, executes requested tools through the host (confirming mutating ones),
 /// feeds results back and repeats until the model answers with text. Bounded to a few rounds per question.
 /// </summary>
 public class AiAgent {
  public const int MaxRounds=8;
  public readonly AiClient Client;public readonly IAiHost Host;
  public readonly List<AiMessage> History=new List<AiMessage>();
  public List<AiTool> Tools=AiTools.All();
  public bool AllowActions=true;
  /// <summary>Progress callback: tool name and outcome text, for the transcript.</summary>
  public Action<string> Trace=delegate{};
  public int TotalInputTokens, TotalOutputTokens;

  public AiAgent(AiClient client,IAiHost host){Client=client;Host=host;}

  /// <summary>Appends the user's question, runs the tool loop and returns the final answer text.</summary>
  public async Task<string> Ask(string question){
   History.Add(AiMessage.User(question));
   string system=AiTools.SystemPrompt(L.Lang,Elevation.IsElevated);
   for(int round=0;round<MaxRounds;round++){
    var reply=await Client.Send(system,History,Tools).ConfigureAwait(false);
    TotalInputTokens+=reply.InputTokens;TotalOutputTokens+=reply.OutputTokens;
    History.Add(new AiMessage{Role="assistant",Text=reply.Text??"",ToolCalls=reply.ToolCalls});
    if(reply.ToolCalls.Count==0)return reply.Text??"";
    foreach(var call in reply.ToolCalls){
     string result=await Execute(call).ConfigureAwait(false);
     History.Add(AiMessage.ToolResult(call.Id,result));
    }
   }
   string stop=L.T("Trợ lý đã gọi công cụ quá nhiều vòng và dừng lại. Hãy hỏi cụ thể hơn.");
   History.Add(new AiMessage{Role="assistant",Text=stop});
   return stop;
  }

  /// <summary>Runs one tool call with the safety gate; errors are returned to the model as text instead of aborting the turn.</summary>
  public async Task<string> Execute(AiToolCall call){
   var tool=AiTools.Find(call.Name);
   if(tool==null){Trace(call.Name+": "+L.T("công cụ không tồn tại"));return "Error: unknown tool "+call.Name;}
   JsonElement args;
   try{args=JsonDocument.Parse(String.IsNullOrWhiteSpace(call.ArgumentsJson)?"{}":call.ArgumentsJson).RootElement.Clone();}
   catch(JsonException e){return "Error: arguments are not valid JSON: "+e.Message;}
   if(tool.Mutating){
    if(!AllowActions){Trace(AiTools.Describe(call)+": "+L.T("bị chặn — thao tác thay đổi đang tắt"));return "Denied: the user disabled system-changing actions for the assistant. Explain what you would do instead.";}
    bool ok=await Host.ConfirmAction(AiTools.Describe(call)).ConfigureAwait(false);
    if(!ok){Trace(AiTools.Describe(call)+": "+L.T("người dùng từ chối"));return "Denied: the user declined this action in the confirmation dialog.";}
   }
   try{
    string result=await Host.RunTool(call.Name,args).ConfigureAwait(false)??"";
    string first=result.Split('\n')[0].Trim();
    bool failed=first.StartsWith("Error",StringComparison.Ordinal)||first.StartsWith("Refused",StringComparison.Ordinal)||first.StartsWith("Ambiguous",StringComparison.Ordinal);
    Trace(AiTools.Describe(call)+": "+(failed?first:L.T("xong")+(first==""?"":" — "+(first.Length>140?first.Substring(0,140)+"…":first))));
    return result;
   }catch(Exception e){Trace(AiTools.Describe(call)+": "+L.T("lỗi")+" — "+e.Message);return "Error: "+e.Message;}
  }

  public void Reset(){History.Clear();TotalInputTokens=0;TotalOutputTokens=0;}
 }
}
