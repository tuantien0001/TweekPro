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
   Assert(tools.Where(t=>t.Mutating).Select(t=>t.Name).OrderBy(n=>n).SequenceEqual(new[]{"disable_startup_entry","end_process","start_service","stop_service","uninstall_application"}),"exactly the system-changing tools are marked mutating");
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
  }
 }
}
