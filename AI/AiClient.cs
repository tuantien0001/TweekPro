using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TweekPro.AI {
 /// <summary>One tool the model may call: name, description and JSON-schema parameters, plus whether it changes the system.</summary>
 public class AiTool {
  public string Name="", Description="";
  public Dictionary<string,object> Parameters=new Dictionary<string,object>{{"type","object"},{"properties",new Dictionary<string,object>()}};
  public bool Mutating;
 }

 /// <summary>A tool invocation requested by the model.</summary>
 public class AiToolCall { public string Id="", Name="", ArgumentsJson="{}"; }

 /// <summary>A conversation turn. Assistant turns may carry tool calls; "tool" turns carry one result each.</summary>
 public class AiMessage {
  public string Role="user", Text="";
  public List<AiToolCall> ToolCalls=new List<AiToolCall>();
  public string ToolCallId="";
  public static AiMessage User(string text){return new AiMessage{Role="user",Text=text};}
  public static AiMessage ToolResult(string callId,string text){return new AiMessage{Role="tool",ToolCallId=callId,Text=text};}
 }

 /// <summary>What the model answered: text and/or tool calls, plus token usage when reported.</summary>
 public class AiReply { public string Text=""; public List<AiToolCall> ToolCalls=new List<AiToolCall>(); public int InputTokens, OutputTokens; public string StopReason=""; }

 /// <summary>
 /// Minimal client for the Anthropic Messages API (Claude) and the OpenAI Chat Completions API (GPT / Codex models and
 /// compatible servers), with tool calling. The HTTP transport is injectable so the request/response code is testable offline.
 /// </summary>
 public class AiClient {
  public const string Anthropic="anthropic", OpenAI="openai";
  public const string DefaultAnthropicModel="claude-sonnet-4-5", DefaultOpenAIModel="gpt-4.1";
  public const string AnthropicEndpoint="https://api.anthropic.com/v1/messages", OpenAIEndpoint="https://api.openai.com/v1/chat/completions";
  public string Provider=Anthropic, Model="", ApiKey="", Endpoint="";
  public int MaxTokens=2048;
  /// <summary>Sends a POST and returns the response body; throws IOException with the API's error text on non-2xx.</summary>
  public Func<string,Dictionary<string,string>,string,Task<string>> Transport;
  static readonly HttpClient Http=CreateHttp();

  static HttpClient CreateHttp(){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var c=new HttpClient{Timeout=TimeSpan.FromSeconds(120)};
   c.DefaultRequestHeaders.UserAgent.ParseAdd("TweekPro/0.7");
   return c;
  }

  public AiClient(){Transport=HttpTransport;}

  public static string DefaultModel(string provider){return provider==OpenAI?DefaultOpenAIModel:DefaultAnthropicModel;}
  public static string DefaultEndpoint(string provider){return provider==OpenAI?OpenAIEndpoint:AnthropicEndpoint;}
  public static string ProviderLabel(string provider){return provider==OpenAI?"OpenAI (GPT / Codex)":"Anthropic (Claude)";}
  string EffectiveModel { get { return String.IsNullOrWhiteSpace(Model)?DefaultModel(Provider):Model.Trim(); } }
  string EffectiveEndpoint { get { return String.IsNullOrWhiteSpace(Endpoint)?DefaultEndpoint(Provider):Endpoint.Trim(); } }

  /// <summary>Checks the key format so obvious paste mistakes are caught before a network call.</summary>
  public static string ValidateKey(string provider,string key){
   key=(key??"").Trim();
   if(key=="")return Core.L.T("Chưa nhập API key.");
   if(key.Any(ch=>Char.IsWhiteSpace(ch)||ch>126))return Core.L.T("API key chứa khoảng trắng hoặc ký tự lạ — hãy dán lại.");
   if(provider==Anthropic&&!key.StartsWith("sk-ant-",StringComparison.Ordinal))return Core.L.T("Key của Anthropic bắt đầu bằng sk-ant-.");
   if(provider==OpenAI&&!key.StartsWith("sk-",StringComparison.Ordinal))return Core.L.T("Key của OpenAI bắt đầu bằng sk-.");
   return null;
  }

  static async Task<string> HttpTransport(string url,Dictionary<string,string> headers,string body){
   using(var request=new HttpRequestMessage(HttpMethod.Post,url)){
    request.Content=new StringContent(body,Encoding.UTF8,"application/json");
    foreach(var h in headers)request.Headers.TryAddWithoutValidation(h.Key,h.Value);
    using(var response=await Http.SendAsync(request).ConfigureAwait(false)){
     string text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
     if(!response.IsSuccessStatusCode)throw new IOException(ErrorText((int)response.StatusCode,text));
     return text;
    }
   }
  }

  /// <summary>Extracts the human-readable message from an API error body.</summary>
  public static string ErrorText(int status,string body){
   string detail=body??"";
   try{
    using(var doc=JsonDocument.Parse(body)){
     JsonElement error;
     if(doc.RootElement.TryGetProperty("error",out error)){
      JsonElement message;
      if(error.ValueKind==JsonValueKind.Object&&error.TryGetProperty("message",out message))detail=message.GetString();
      else if(error.ValueKind==JsonValueKind.String)detail=error.GetString();
     }
    }
   }catch(JsonException){}
   string hint=status==401?Core.L.T(" API key không hợp lệ hoặc đã bị thu hồi."):status==429?Core.L.T(" Hết hạn mức hoặc gửi quá nhanh — thử lại sau."):status==404?Core.L.T(" Tên model không tồn tại — kiểm tra ô Model."):"";
   return "HTTP "+status+": "+(detail.Length>400?detail.Substring(0,400)+"…":detail)+hint;
  }

  /// <summary>Sends the conversation and returns the model's reply.</summary>
  public async Task<AiReply> Send(string system,List<AiMessage> history,List<AiTool> tools){
   string body=Provider==OpenAI?BuildOpenAIRequest(system,history,tools):BuildAnthropicRequest(system,history,tools);
   var headers=Headers();
   string response=await Transport(EffectiveEndpoint,headers,body).ConfigureAwait(false);
   return Provider==OpenAI?ParseOpenAIReply(response):ParseAnthropicReply(response);
  }

  public Dictionary<string,string> Headers(){
   var h=new Dictionary<string,string>();
   if(Provider==OpenAI)h["Authorization"]="Bearer "+ApiKey.Trim();
   else{h["x-api-key"]=ApiKey.Trim();h["anthropic-version"]="2023-06-01";}
   return h;
  }

  static readonly JsonSerializerOptions JsonOptions=new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};

  static object ParseJsonObject(string json){
   try{using(var doc=JsonDocument.Parse(String.IsNullOrWhiteSpace(json)?"{}":json))return ToPlain(doc.RootElement);}
   catch(JsonException){return new Dictionary<string,object>();}
  }

  /// <summary>Converts a JsonElement tree into dictionaries/lists so it can be re-serialized inside a request.</summary>
  static object ToPlain(JsonElement e){
   switch(e.ValueKind){
    case JsonValueKind.Object:{var d=new Dictionary<string,object>();foreach(var p in e.EnumerateObject())d[p.Name]=ToPlain(p.Value);return d;}
    case JsonValueKind.Array:{var l=new List<object>();foreach(var i in e.EnumerateArray())l.Add(ToPlain(i));return l;}
    case JsonValueKind.String:return e.GetString();
    case JsonValueKind.Number:{long l;double dd;return e.TryGetInt64(out l)?(object)l:e.TryGetDouble(out dd)?(object)dd:0;}
    case JsonValueKind.True:return true;
    case JsonValueKind.False:return false;
    default:return null;
   }
  }

  public string BuildAnthropicRequest(string system,List<AiMessage> history,List<AiTool> tools){
   var messages=new List<object>();
   int i=0;
   while(i<history.Count){
    var m=history[i];
    if(m.Role=="tool"){
     var results=new List<object>();
     while(i<history.Count&&history[i].Role=="tool"){results.Add(new Dictionary<string,object>{{"type","tool_result"},{"tool_use_id",history[i].ToolCallId},{"content",history[i].Text}});i++;}
     messages.Add(new Dictionary<string,object>{{"role","user"},{"content",results}});
     continue;
    }
    if(m.Role=="assistant"){
     var content=new List<object>();
     if(!String.IsNullOrEmpty(m.Text))content.Add(new Dictionary<string,object>{{"type","text"},{"text",m.Text}});
     foreach(var c in m.ToolCalls)content.Add(new Dictionary<string,object>{{"type","tool_use"},{"id",c.Id},{"name",c.Name},{"input",ParseJsonObject(c.ArgumentsJson)}});
     if(content.Count>0)messages.Add(new Dictionary<string,object>{{"role","assistant"},{"content",content}});
    }else messages.Add(new Dictionary<string,object>{{"role","user"},{"content",m.Text}});
    i++;
   }
   var body=new Dictionary<string,object>{{"model",EffectiveModel},{"max_tokens",MaxTokens},{"system",system},{"messages",messages}};
   if(tools!=null&&tools.Count>0)body["tools"]=tools.Select(t=>(object)new Dictionary<string,object>{{"name",t.Name},{"description",t.Description},{"input_schema",t.Parameters}}).ToList();
   return JsonSerializer.Serialize(body,JsonOptions);
  }

  public string BuildOpenAIRequest(string system,List<AiMessage> history,List<AiTool> tools){
   var messages=new List<object>{new Dictionary<string,object>{{"role","system"},{"content",system}}};
   foreach(var m in history){
    if(m.Role=="tool")messages.Add(new Dictionary<string,object>{{"role","tool"},{"tool_call_id",m.ToolCallId},{"content",m.Text}});
    else if(m.Role=="assistant"){
     var d=new Dictionary<string,object>{{"role","assistant"},{"content",String.IsNullOrEmpty(m.Text)?null:m.Text}};
     if(m.ToolCalls.Count>0)d["tool_calls"]=m.ToolCalls.Select(c=>(object)new Dictionary<string,object>{{"id",c.Id},{"type","function"},{"function",new Dictionary<string,object>{{"name",c.Name},{"arguments",c.ArgumentsJson}}}}).ToList();
     messages.Add(d);
    }else messages.Add(new Dictionary<string,object>{{"role","user"},{"content",m.Text}});
   }
   var body=new Dictionary<string,object>{{"model",EffectiveModel},{"messages",messages}};
   if(!EffectiveModel.StartsWith("o",StringComparison.Ordinal)&&!EffectiveModel.StartsWith("gpt-5",StringComparison.Ordinal))body["max_tokens"]=MaxTokens;else body["max_completion_tokens"]=MaxTokens;
   if(tools!=null&&tools.Count>0)body["tools"]=tools.Select(t=>(object)new Dictionary<string,object>{{"type","function"},{"function",new Dictionary<string,object>{{"name",t.Name},{"description",t.Description},{"parameters",t.Parameters}}}}).ToList();
   return JsonSerializer.Serialize(body,JsonOptions);
  }

  public static AiReply ParseAnthropicReply(string json){
   var reply=new AiReply();
   using(var doc=JsonDocument.Parse(json)){
    var root=doc.RootElement;JsonElement content,usage,stop;
    if(root.TryGetProperty("stop_reason",out stop)&&stop.ValueKind==JsonValueKind.String)reply.StopReason=stop.GetString();
    if(root.TryGetProperty("content",out content)&&content.ValueKind==JsonValueKind.Array){
     var text=new StringBuilder();
     foreach(var block in content.EnumerateArray()){
      string type=block.GetProperty("type").GetString();
      if(type=="text"){if(text.Length>0)text.Append("\n");text.Append(block.GetProperty("text").GetString());}
      else if(type=="tool_use")reply.ToolCalls.Add(new AiToolCall{Id=block.GetProperty("id").GetString(),Name=block.GetProperty("name").GetString(),ArgumentsJson=block.GetProperty("input").GetRawText()});
     }
     reply.Text=text.ToString();
    }
    if(root.TryGetProperty("usage",out usage)){JsonElement v;if(usage.TryGetProperty("input_tokens",out v))reply.InputTokens=v.GetInt32();if(usage.TryGetProperty("output_tokens",out v))reply.OutputTokens=v.GetInt32();}
   }
   return reply;
  }

  public static AiReply ParseOpenAIReply(string json){
   var reply=new AiReply();
   using(var doc=JsonDocument.Parse(json)){
    var root=doc.RootElement;JsonElement choices,usage;
    if(root.TryGetProperty("choices",out choices)&&choices.GetArrayLength()>0){
     var choice=choices[0];JsonElement finish,message,content,calls;
     if(choice.TryGetProperty("finish_reason",out finish)&&finish.ValueKind==JsonValueKind.String)reply.StopReason=finish.GetString();
     if(choice.TryGetProperty("message",out message)){
      if(message.TryGetProperty("content",out content)&&content.ValueKind==JsonValueKind.String)reply.Text=content.GetString();
      if(message.TryGetProperty("tool_calls",out calls)&&calls.ValueKind==JsonValueKind.Array)
       foreach(var c in calls.EnumerateArray()){var fn=c.GetProperty("function");reply.ToolCalls.Add(new AiToolCall{Id=c.GetProperty("id").GetString(),Name=fn.GetProperty("name").GetString(),ArgumentsJson=fn.GetProperty("arguments").GetString()??"{}"});}
     }
    }
    if(root.TryGetProperty("usage",out usage)){JsonElement v;if(usage.TryGetProperty("prompt_tokens",out v))reply.InputTokens=v.GetInt32();if(usage.TryGetProperty("completion_tokens",out v))reply.OutputTokens=v.GetInt32();}
   }
   return reply;
  }
 }
}
