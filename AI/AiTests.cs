using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TweekPro.Core;

namespace TweekPro.AI {
 /// <summary>Offline self-tests: request shapes for both providers, reply parsing, the tool loop with a fake transport, key storage.</summary>
 public static class AiTests {
  static void Assert(bool ok,string what){if(!ok)throw new Exception("AI test failed: "+what);}

  class FakeHost : IAiHost {
   public List<string> Ran=new List<string>();public bool Approve=true;public int Confirmations;
   public Task<string> RunTool(string name,JsonElement args){Ran.Add(name);if(name=="explain_service")return Task.FromResult("Spooler: prints things. safety=Optional");if(name=="stop_service")return Task.FromResult("Done: "+args.GetProperty("name").GetString()+" stopped.");return Task.FromResult("ok");}
   public Task<bool> ConfirmAction(string description){Confirmations++;return Task.FromResult(Approve);}
  }

  public static void Run(){
   // Secret storage round-trips and never returns garbage for foreign values.
   string stored=SecretStore.Protect("sk-ant-api03-abcdefghijklmnop");
   Assert(stored!=""&&stored!="sk-ant-api03-abcdefghijklmnop","key is not stored in clear text");
   Assert(SecretStore.Unprotect(stored)=="sk-ant-api03-abcdefghijklmnop","key round-trips");
   Assert(SecretStore.Protect("")==""&&SecretStore.Unprotect("")==""&&SecretStore.Unprotect("garbage")=="","empty and foreign values are harmless");
   Assert(SecretStore.Mask("sk-ant-api03-abcdefghijklmnop").StartsWith("sk-ant")&&SecretStore.Mask("sk-ant-api03-abcdefghijklmnop").EndsWith("mnop")&&!SecretStore.Mask("sk-ant-api03-abcdefghijklmnop").Contains("api03"),"mask hides the middle");
   Assert(Environment.OSVersion.Platform!=PlatformID.Win32NT||SecretStore.IsEncrypted(stored),"DPAPI used on Windows");

   Assert(AiClient.ValidateKey(AiClient.Anthropic,"")!=null,"empty key rejected");
   Assert(AiClient.ValidateKey(AiClient.Anthropic,"sk-abc")!=null,"OpenAI-looking key rejected for Anthropic");
   Assert(AiClient.ValidateKey(AiClient.Anthropic,"sk-ant-abc")==null,"Anthropic key accepted");
   Assert(AiClient.ValidateKey(AiClient.OpenAI,"sk-proj-abc")==null,"OpenAI key accepted");
   Assert(AiClient.ValidateKey(AiClient.OpenAI,"sk-proj abc")!=null,"whitespace rejected");
   Assert(AiClient.ErrorText(401,"{\"error\":{\"message\":\"invalid x-api-key\"}}").Contains("invalid x-api-key"),"error text extracted");
   Assert(AiClient.ErrorText(500,"not json").Contains("not json"),"non-JSON error kept");

   // Tool catalog is well-formed.
   var tools=AiTools.All();
   Assert(tools.Count>=12&&tools.Select(t=>t.Name).Distinct().Count()==tools.Count,"tools unique");
   Assert(tools.Where(t=>t.Mutating).Select(t=>t.Name).OrderBy(n=>n).SequenceEqual(new[]{"clean_junk","clean_leftovers","disable_startup_entry","end_process","start_service","stop_service","uninstall_application"}),"exactly the system-changing tools are marked mutating");
   foreach(var t in tools){var props=(Dictionary<string,object>)t.Parameters["properties"];Assert(t.Description.Length>20&&(string)t.Parameters["type"]=="object"&&props!=null,"tool "+t.Name+" schema");}
   var stopTool=AiTools.Find("stop_service");
   Assert(((List<object>)stopTool.Parameters["required"]).Cast<string>().SequenceEqual(new[]{"name"}),"required parameters recorded");
   Assert(AiTools.Describe(new AiToolCall{Name="stop_service",ArgumentsJson="{\"name\":\"Spooler\",\"disable\":true}"})=="stop_service (name = Spooler, disable = True)","tool call description");
   Assert(AiTools.SystemPrompt("vi",true).Contains("Vietnamese")&&AiTools.SystemPrompt("en",false).Contains("NOT running as administrator"),"system prompt reflects language and elevation");

   // Anthropic request shape.
   var history=new List<AiMessage>{AiMessage.User("hi"),new AiMessage{Role="assistant",Text="",ToolCalls=new List<AiToolCall>{new AiToolCall{Id="toolu_1",Name="explain_service",ArgumentsJson="{\"name\":\"Spooler\"}"}}},AiMessage.ToolResult("toolu_1","Spooler: prints"),new AiMessage{Role="assistant",Text="It prints."},AiMessage.User("thanks")};
   var anthropic=new AiClient{Provider=AiClient.Anthropic,ApiKey="sk-ant-x"};
   using(var doc=JsonDocument.Parse(anthropic.BuildAnthropicRequest("SYS",history,tools))){
    var root=doc.RootElement;
    Assert(root.GetProperty("model").GetString()==AiClient.DefaultAnthropicModel&&root.GetProperty("system").GetString()=="SYS","anthropic model/system");
    var msgs=root.GetProperty("messages");
    Assert(msgs.GetArrayLength()==5,"anthropic message count "+msgs.GetArrayLength());
    Assert(msgs[1].GetProperty("role").GetString()=="assistant"&&msgs[1].GetProperty("content")[0].GetProperty("type").GetString()=="tool_use"&&msgs[1].GetProperty("content")[0].GetProperty("input").GetProperty("name").GetString()=="Spooler","anthropic tool_use block");
    Assert(msgs[2].GetProperty("role").GetString()=="user"&&msgs[2].GetProperty("content")[0].GetProperty("type").GetString()=="tool_result"&&msgs[2].GetProperty("content")[0].GetProperty("tool_use_id").GetString()=="toolu_1","anthropic tool_result block");
    Assert(root.GetProperty("tools")[0].TryGetProperty("input_schema",out _),"anthropic tools use input_schema");
   }
   var h=anthropic.Headers();Assert(h["x-api-key"]=="sk-ant-x"&&h["anthropic-version"]=="2023-06-01","anthropic headers");

   // OpenAI request shape.
   var openai=new AiClient{Provider=AiClient.OpenAI,ApiKey="sk-x",Model="gpt-4.1-mini"};
   using(var doc=JsonDocument.Parse(openai.BuildOpenAIRequest("SYS",history,tools))){
    var root=doc.RootElement;var msgs=root.GetProperty("messages");
    Assert(root.GetProperty("model").GetString()=="gpt-4.1-mini"&&msgs[0].GetProperty("role").GetString()=="system","openai model/system");
    Assert(msgs.GetArrayLength()==6,"openai message count "+msgs.GetArrayLength());
    Assert(msgs[2].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("arguments").GetString()=="{\"name\":\"Spooler\"}","openai tool_calls kept as string arguments");
    Assert(msgs[3].GetProperty("role").GetString()=="tool"&&msgs[3].GetProperty("tool_call_id").GetString()=="toolu_1","openai tool message");
    Assert(root.GetProperty("tools")[0].GetProperty("type").GetString()=="function"&&root.TryGetProperty("max_completion_tokens",out _)&&!root.TryGetProperty("max_tokens",out _),"openai tools/function and max_completion_tokens on the official endpoint");
   }
   Assert(openai.Headers()["Authorization"]=="Bearer sk-x","openai bearer header");
   using(var doc=JsonDocument.Parse(new AiClient{Provider=AiClient.OpenAI,Model="llama3",Endpoint="http://localhost:11434/v1/chat/completions"}.BuildOpenAIRequest("s",new List<AiMessage>(),null)))Assert(doc.RootElement.TryGetProperty("max_tokens",out _),"compatible servers get max_tokens");
   var emptyTurn=new List<AiMessage>{AiMessage.User("a"),new AiMessage{Role="assistant",Text=""},AiMessage.User("b")};
   using(var doc=JsonDocument.Parse(openai.BuildOpenAIRequest("s",emptyTurn,null)))Assert(doc.RootElement.GetProperty("messages").GetArrayLength()==3,"empty assistant turn skipped for OpenAI");
   Assert(AiClient.ValidateKey(AiClient.OpenAI,"my-local-token",false)==null&&AiClient.ValidateKey(AiClient.OpenAI,"my-local-token",true)!=null,"custom endpoints accept any key format");
   Assert(AiClient.ValidateEndpoint("")==null&&AiClient.ValidateEndpoint("https://api.example.com/v1")==null&&AiClient.ValidateEndpoint("http://127.0.0.1:8787/v1/messages")==null,"https and loopback endpoints accepted");
   Assert(AiClient.ValidateEndpoint("http://api.example.com/v1")!=null&&AiClient.ValidateEndpoint("not a url")!=null,"clear-text remote endpoint rejected");
   Assert(AiClient.ErrorText(400,"{\"error\":{\"message\":null}}").StartsWith("HTTP 400"),"null error message tolerated");

   // Reply parsing.
   var a=AiClient.ParseAnthropicReply("{\"content\":[{\"type\":\"text\",\"text\":\"Let me check.\"},{\"type\":\"tool_use\",\"id\":\"toolu_9\",\"name\":\"list_services\",\"input\":{\"filter\":\"thirdparty\"}}],\"stop_reason\":\"tool_use\",\"usage\":{\"input_tokens\":12,\"output_tokens\":7}}");
   Assert(a.Text=="Let me check."&&a.ToolCalls.Count==1&&a.ToolCalls[0].Name=="list_services"&&a.ToolCalls[0].ArgumentsJson.Contains("thirdparty")&&a.InputTokens==12&&a.StopReason=="tool_use","anthropic reply parsed");
   var o=AiClient.ParseOpenAIReply("{\"choices\":[{\"finish_reason\":\"tool_calls\",\"message\":{\"content\":null,\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"end_process\",\"arguments\":\"{\\\"pid\\\":42}\"}}]}}],\"usage\":{\"prompt_tokens\":5,\"completion_tokens\":3}}");
   Assert(o.Text==""&&o.ToolCalls.Count==1&&o.ToolCalls[0].Id=="call_1"&&o.ToolCalls[0].ArgumentsJson=="{\"pid\":42}"&&o.OutputTokens==3,"openai reply parsed");
   var plain=AiClient.ParseOpenAIReply("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"Hello\"}}]}");
   Assert(plain.Text=="Hello"&&plain.ToolCalls.Count==0,"openai text reply");

   // Agent loop: tool call → confirmation → result → final answer, with a scripted transport.
   RunAgentLoop().GetAwaiter().GetResult();
  }

  static async Task RunAgentLoop(){
   var host=new FakeHost();
   var requests=new List<string>();
   var client=new AiClient{Provider=AiClient.Anthropic,ApiKey="sk-ant-test"};
   int turn=0;
   client.Transport=(url,headers,body)=>{
    requests.Add(body);turn++;
    Assert(url==AiClient.AnthropicEndpoint&&headers["x-api-key"]=="sk-ant-test","transport receives endpoint and headers");
    if(turn==1)return Task.FromResult("{\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"explain_service\",\"input\":{\"name\":\"Spooler\"}}],\"stop_reason\":\"tool_use\",\"usage\":{\"input_tokens\":10,\"output_tokens\":2}}");
    if(turn==2)return Task.FromResult("{\"content\":[{\"type\":\"tool_use\",\"id\":\"t2\",\"name\":\"stop_service\",\"input\":{\"name\":\"Spooler\",\"disable\":false}}],\"stop_reason\":\"tool_use\",\"usage\":{\"input_tokens\":20,\"output_tokens\":3}}");
    return Task.FromResult("{\"content\":[{\"type\":\"text\",\"text\":\"Spooler stopped.\"}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":30,\"output_tokens\":4}}");
   };
   var agent=new AiAgent(client,host);
   var trace=new List<string>();agent.Trace=trace.Add;
   string answer=await agent.Ask("stop the print spooler");
   Assert(answer=="Spooler stopped.","final answer returned");
   Assert(host.Ran.SequenceEqual(new[]{"explain_service","stop_service"}),"tools executed in order");
   Assert(host.Confirmations==1,"only the mutating tool asked for confirmation");
   Assert(requests.Count==3&&requests[2].Contains("\"tool_result\"")&&requests[2].Contains("Done: Spooler stopped."),"tool results fed back to the model");
   Assert(agent.History.Count==6&&agent.TotalInputTokens==60&&agent.TotalOutputTokens==9,"history and usage accumulated");
   Assert(trace.Count==2,"trace per tool call");

   // Declined confirmation is reported to the model instead of executing.
   host=new FakeHost{Approve=false};turn=0;
   client.Transport=(url,headers,body)=>{turn++;if(turn==1)return Task.FromResult("{\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"end_process\",\"input\":{\"pid\":42}}],\"stop_reason\":\"tool_use\"}");Assert(body.Contains("Denied: the user declined"),"denial forwarded");return Task.FromResult("{\"content\":[{\"type\":\"text\",\"text\":\"Understood.\"}]}");};
   agent=new AiAgent(client,host);
   Assert(await agent.Ask("kill 42")=="Understood."&&host.Ran.Count==0&&host.Confirmations==1,"declined action not executed");

   // Actions switched off: no confirmation dialog, tool not run.
   host=new FakeHost();turn=0;
   client.Transport=(url,headers,body)=>{turn++;if(turn==1)return Task.FromResult("{\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"stop_service\",\"input\":{\"name\":\"Spooler\"}}],\"stop_reason\":\"tool_use\"}");Assert(body.Contains("Denied: the user disabled"),"disabled-actions denial forwarded");return Task.FromResult("{\"content\":[{\"type\":\"text\",\"text\":\"OK\"}]}");};
   agent=new AiAgent(client,host){AllowActions=false};
   Assert(await agent.Ask("stop spooler")=="OK"&&host.Ran.Count==0&&host.Confirmations==0,"actions disabled blocks without asking");

   // Unknown tool and bad JSON arguments are reported, not thrown; read-only tools never confirm.
   host=new FakeHost();
   agent=new AiAgent(client,host);
   Assert((await agent.Execute(new AiToolCall{Id="x",Name="nope",ArgumentsJson="{}"})).StartsWith("Error: unknown tool"),"unknown tool");
   Assert((await agent.Execute(new AiToolCall{Id="x",Name="list_services",ArgumentsJson="{bad"})).StartsWith("Error: arguments"),"bad arguments");
   Assert((await agent.Execute(new AiToolCall{Id="x",Name="list_services",ArgumentsJson="{\"filter\":\"all\"}"}))=="ok"&&host.Confirmations==0,"read-only tool runs without confirmation");

   // Runaway tool loops stop after MaxRounds.
   client.Transport=(url,headers,body)=>Task.FromResult("{\"content\":[{\"type\":\"tool_use\",\"id\":\"t\",\"name\":\"list_vault\",\"input\":{}}],\"stop_reason\":\"tool_use\"}");
   agent=new AiAgent(client,new FakeHost());
   string stopped=await agent.Ask("loop");
   Assert(stopped==L.T("Trợ lý đã gọi công cụ quá nhiều vòng và dừng lại. Hãy hỏi cụ thể hơn.")&&agent.History.Count==1+AiAgent.MaxRounds*2+1,"loop bounded");

   // An empty reply becomes a visible sentence and does not poison the history.
   client.Transport=(url,headers,body)=>Task.FromResult("{\"content\":[],\"stop_reason\":\"max_tokens\"}");
   agent=new AiAgent(client,new FakeHost());
   string emptyAnswer=await agent.Ask("hi");
   Assert(emptyAnswer.Contains("max_tokens")&&agent.History.Count==2&&agent.History[1].Text==emptyAnswer,"empty reply reported");

   // Transport errors surface as exceptions to the UI.
   client.Transport=(url,headers,body)=>{throw new System.IO.IOException("HTTP 401: invalid key");};
   agent=new AiAgent(client,new FakeHost());
   bool threw=false;try{await agent.Ask("hi");}catch(System.IO.IOException e){threw=e.Message.Contains("401");}
   Assert(threw,"transport error propagates");

   // Automatic mode: mutating tools run without any confirmation dialog; the transcript trace says so.
   host=new FakeHost();turn=0;var traces=new List<string>();
   client=new AiClient{Provider=AiClient.Anthropic,ApiKey="sk-ant-x"};
   client.Transport=(url,headers,body)=>{turn++;if(turn==1){Assert(body.Contains("run immediately without asking"),"auto mode explained in system prompt");return Task.FromResult("{\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"stop_service\",\"input\":{\"name\":\"Spooler\"}}],\"stop_reason\":\"tool_use\"}");}Assert(body.Contains("Spooler stopped"),"auto result fed back");return Task.FromResult("{\"content\":[{\"type\":\"text\",\"text\":\"Stopped.\"}],\"stop_reason\":\"end_turn\"}");};
   agent=new AiAgent(client,host){ActionMode=AiActionModes.Auto,Trace=t=>traces.Add(t)};
   Assert(await agent.Ask("stop spooler")=="Stopped."&&host.Ran.Count==1&&host.Confirmations==0,"auto mode runs without confirmation");
   Assert(traces.Any(t=>t.Contains(L.T("tự động thực hiện"))),"auto mode traced");
   Assert(AiActionModes.Normalize("AUTO")==AiActionModes.Auto&&AiActionModes.Normalize("nonsense")==AiActionModes.Confirm&&AiActionModes.Normalize("",true)==AiActionModes.ReadOnly&&AiActionModes.Normalize("confirm",true)==AiActionModes.Confirm,"action mode normalisation");
   var legacyAgent=new AiAgent(client,host){AllowActions=false};
   Assert(legacyAgent.ActionMode==AiActionModes.ReadOnly&&!legacyAgent.AllowActions,"legacy AllowActions=false maps to read-only");
   Assert(AiTools.SystemPrompt("vi",true,AiActionModes.ReadOnly).Contains("currently disabled")&&AiTools.SystemPrompt("vi",true,AiActionModes.Confirm).Contains("confirmation dialog")&&AiTools.SystemPrompt("vi",true).Contains("System32"),"prompt reflects mode and hard limits");
   Assert(AiTools.Find("scan_leftovers")!=null&&!AiTools.Find("scan_leftovers").Mutating&&AiTools.Find("clean_leftovers").Mutating&&AiTools.Find("clean_junk").Mutating&&AiTools.Find("uninstall_application").Parameters.ToString()!="","leftover and junk tools catalogued");

   // Local providers: LM Studio / Ollama speak OpenAI, need no key, get long timeouts and private-LAN http.
   Assert(AiClient.IsLocal(AiClient.LmStudio)&&AiClient.IsLocal(AiClient.Ollama)&&!AiClient.IsLocal(AiClient.OpenAI)&&AiClient.OpenAIWire(AiClient.Ollama)&&!AiClient.RequiresKey(AiClient.LmStudio),"local provider flags");
   Assert(AiClient.DefaultEndpoint(AiClient.LmStudio)=="http://localhost:1234/v1/chat/completions"&&AiClient.DefaultEndpoint(AiClient.Ollama)=="http://localhost:11434/v1/chat/completions"&&AiClient.DefaultModel(AiClient.LmStudio)=="","local defaults");
   Assert(AiClient.Normalize("LMSTUDIO")==AiClient.LmStudio&&AiClient.Normalize("bogus")==AiClient.Anthropic&&AiClient.Normalize("openai")==AiClient.OpenAI,"provider normalisation");
   Assert(AiClient.ValidateKey(AiClient.LmStudio,"")==null&&AiClient.ValidateKey(AiClient.Ollama,"any token")!=null&&AiClient.ValidateKey(AiClient.OpenAI,"")!=null,"local key optional");
   Assert(AiClient.ValidateEndpoint("http://192.168.1.20:1234/v1/chat/completions",AiClient.LmStudio)==null&&AiClient.ValidateEndpoint("http://192.168.1.20:1234/v1/chat/completions",AiClient.OpenAI)!=null&&AiClient.ValidateEndpoint("http://8.8.8.8/v1",AiClient.Ollama)!=null&&AiClient.ValidateEndpoint("http://mypc.local:11434/v1/chat/completions",AiClient.Ollama)==null,"private LAN only for local providers");
   Assert(AiClient.TimeoutFor(AiClient.Ollama)>AiClient.TimeoutFor(AiClient.Anthropic),"local timeout longer");
   Assert(AiClient.ModelsUrl("http://localhost:1234/v1/chat/completions")=="http://localhost:1234/v1/models"&&AiClient.ModelsUrl("http://localhost:11434/v1/")=="http://localhost:11434/v1/models","models url derived");
   var ids=AiClient.ParseModelList("{\"object\":\"list\",\"data\":[{\"id\":\"qwen3-coder-30b-a3b-instruct\",\"object\":\"model\"},{\"id\":\"google/gemma-4-12b-qat\"},{\"id\":\"text-embedding-nomic\"}]}");
   Assert(ids.Count==3&&ids[0]=="google/gemma-4-12b-qat"&&ids.Contains("qwen3-coder-30b-a3b-instruct"),"lm studio model list parsed");
   Assert(AiClient.ParseModelList("{\"models\":[{\"name\":\"llama3:8b\"},{\"name\":\"llama3:8b\"}]}").Count==1&&AiClient.ParseModelList("[\"a\",\"b\"]").Count==2&&AiClient.ParseModelList("{}").Count==0,"other model list shapes");
   var local=new AiClient{Provider=AiClient.LmStudio,Model="qwen3-coder-30b-a3b-instruct"};
   Assert(!local.Headers().ContainsKey("Authorization")&&new AiClient{Provider=AiClient.LmStudio,ApiKey="tok"}.Headers()["Authorization"]=="Bearer tok"&&new AiClient{Provider=AiClient.OpenAI,ApiKey=""}.Headers().ContainsKey("Authorization"),"local headers omit empty bearer");
   string listed=null;local.GetTransport=(url,headers)=>{listed=url;return Task.FromResult("{\"data\":[{\"id\":\"m1\"}]}");};
   Assert((await local.ListModels())[0]=="m1"&&listed=="http://localhost:1234/v1/models","list models hits /v1/models");
   bool anthropicListThrew=false;try{await new AiClient{Provider=AiClient.Anthropic}.ListModels();}catch(System.IO.IOException){anthropicListThrew=true;}
   Assert(anthropicListThrew,"model listing only for OpenAI-style servers");
   bool noModelThrew=false;try{await new AiClient{Provider=AiClient.Ollama,Transport=(u,h,b)=>Task.FromResult("{}")}.Send("s",new List<AiMessage>{AiMessage.User("x")},null);}catch(System.IO.IOException e){noModelThrew=e.Message.Contains(L.T("Chưa chọn model cục bộ"));}
   Assert(noModelThrew,"local provider requires a chosen model");

   // A local model without tool support: the 400 is swallowed once, the request is retried without tools and the client remembers.
   var localBodies=new List<string>();
   local.Transport=(url,headers,body)=>{localBodies.Add(body);if(localBodies.Count==1)throw new System.IO.IOException("HTTP 400: This model does not support tools. Use a different template.");return Task.FromResult("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"hello\"},\"finish_reason\":\"stop\"}]}");};
   var localReply=await local.Send("s",new List<AiMessage>{AiMessage.User("hi")},AiTools.All());
   Assert(localReply.Text=="hello"&&localBodies.Count==2&&localBodies[0].Contains("\"tools\"")&&!localBodies[1].Contains("\"tools\"")&&local.ToolsUnsupported,"tool rejection falls back to plain chat");
   Assert(localBodies[1].Contains("Tools are unavailable"),"fallback tells model not to claim actions");
   local.Transport=(u,h,b)=>{Assert(!b.Contains("tool_call_id")&&!b.Contains("tool_calls"),"plain chat strips historical tool protocol");return Task.FromResult("{\"choices\":[{\"message\":{\"content\":\"guidance\",\"tool_calls\":[{\"id\":\"x\",\"function\":{\"name\":\"end_process\",\"arguments\":\"{}\"}}]}}]}");};
   var fallback=await local.Send("s",new List<AiMessage>{new AiMessage{Role="assistant",ToolCalls=new List<AiToolCall>{new AiToolCall{Id="old",Name="list_services"}}},AiMessage.ToolResult("old","sample")},AiTools.All());
   Assert(fallback.ToolCalls.Count==0,"unsupported model cannot execute returned tool calls");
   Assert(localBodies[1].Contains("\"max_tokens\"")&&localBodies[1].Contains("\"model\":\"qwen3-coder-30b-a3b-instruct\""),"local request uses max_tokens and the chosen model");
   Assert(AiClient.LooksLikeToolRejection("HTTP 400: tools are not supported")&&!AiClient.LooksLikeToolRejection("HTTP 401: bad key")&&!AiClient.LooksLikeToolRejection("HTTP 400: context length exceeded"),"tool rejection heuristic");
   bool otherErrorThrew=false;var strict=new AiClient{Provider=AiClient.Ollama,Model="llama3"};strict.Transport=(u,h,b)=>{throw new System.IO.IOException("HTTP 500: out of memory");};
   try{await strict.Send("s",new List<AiMessage>{AiMessage.User("x")},AiTools.All());}catch(System.IO.IOException){otherErrorThrew=true;}
   Assert(otherErrorThrew&&!strict.ToolsUnsupported,"unrelated local errors still propagate");

   // The AI safety guard refuses Windows, drive roots and top-level roots regardless of the engine.
   string win=@"C:\Windows";var roots=new[]{@"C:\Program Files",@"C:\Program Files (x86)",@"C:\Users\bob",@"C:\Users\bob\AppData\Local",@"C:\ProgramData",@"C:\Users"};
   foreach(var bad in new[]{@"C:\Windows\System32\drivers\etc",@"C:\Windows\System32",@"C:\Windows",@"c:\windows\winsxs\x",@"C:\",@"D:\",@"C:\Program Files",@"C:\Users",@"C:\Users\bob",@"C:\Program Files\Common Files\Foo",@"C:\Program Files\WindowsApps\Foo",@"\\?\C:\Windows\Temp",@"C:\Users\bob\AppData\Local\Microsoft\WindowsApps",""})
    Assert(AiSafety.ProtectedReason(bad,win,roots)!=null,"ai safety refuses "+bad);
   foreach(var bad in new[]{@"C:\Temp\..\Windows\notepad.exe",@"C:\Users\bob\Downloads\..",@"C:\Temp\..",@"C:\Windows.\Temp",@"C:Windows\x",@"..\Windows\x",@"\\server\share\file",@"C:\Temp\x:stream"})
    Assert(AiSafety.ProtectedReason(bad,win,roots)!=null,"ai safety refuses ambiguous/traversal path "+bad);
   Assert(new Settings().AiActionMode==AiActionModes.Confirm,"new settings require explicit automatic-mode selection");
   host=new FakeHost();agent=new AiAgent(client,host){ActionMode="invalid"};
   await agent.Execute(new AiToolCall{Id="x",Name="end_process",ArgumentsJson="{\"pid\":42}"});
   Assert(host.Confirmations==1,"invalid runtime action mode does not bypass confirmation");
   foreach(var ok in new[]{@"C:\Program Files\Zoom",@"C:\Users\bob\AppData\Local\Zoom",@"C:\Users\bob\AppData\Roaming\Foo\cache",@"C:\ProgramData\Adobe\ARM",@"C:\Users\bob\AppData\Local\Programs\Foo\Installer"})
    Assert(AiSafety.ProtectedReason(ok,win,roots)==null,"ai safety allows "+ok);
   Assert(AiSafety.ProtectedReason(@"C:\Windows\System32\foo.dll",win,roots).Contains("Windows"),"ai safety reason names Windows");
  }
 }
}
