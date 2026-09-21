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
  public const string Anthropic="anthropic", OpenAI="openai", LmStudio="lmstudio", Ollama="ollama";
  public static readonly string[] Providers={Anthropic,OpenAI,LmStudio,Ollama};
  public const string DefaultAnthropicModel="claude-sonnet-4-5", DefaultOpenAIModel="gpt-4.1";
  public const string AnthropicEndpoint="https://api.anthropic.com/v1/messages", OpenAIEndpoint="https://api.openai.com/v1/chat/completions";
  public const string LmStudioEndpoint="http://localhost:1234/v1/chat/completions", OllamaEndpoint="http://localhost:11434/v1/chat/completions";
  public string Provider=Anthropic, Model="", ApiKey="", Endpoint="";
  public int MaxTokens=2048;
  /// <summary>Set after a local server rejected the tool list; the next requests go out without tools so plain chat still works.</summary>
  public bool ToolsUnsupported;
  /// <summary>Sends a POST and returns the response body; throws IOException with the API's error text on non-2xx.</summary>
  public Func<string,Dictionary<string,string>,string,Task<string>> Transport;
  /// <summary>Sends a GET and returns the response body (used for /models on local servers).</summary>
  public Func<string,Dictionary<string,string>,Task<string>> GetTransport;
  static readonly HttpClient Http=CreateHttp();

  static HttpClient CreateHttp(){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var c=new HttpClient{Timeout=System.Threading.Timeout.InfiniteTimeSpan};
   c.DefaultRequestHeaders.UserAgent.ParseAdd("TweekPro/0.7");
   return c;
  }

  public AiClient(){Transport=HttpTransport;GetTransport=HttpGet;}

  /// <summary>Local servers (LM Studio, Ollama) speak the OpenAI wire format, run on this PC and need no API key.</summary>
  public static bool IsLocal(string provider){return provider==LmStudio||provider==Ollama;}
  /// <summary>Whether the provider uses the OpenAI Chat Completions format (OpenAI itself and every local server).</summary>
  public static bool OpenAIWire(string provider){return provider==OpenAI||IsLocal(provider);}
  public static bool RequiresKey(string provider){return !IsLocal(provider);}
  public static string Normalize(string provider){provider=(provider??"").Trim().ToLowerInvariant();return Providers.Contains(provider)?provider:Anthropic;}
  public static string DefaultModel(string provider){return provider==OpenAI?DefaultOpenAIModel:IsLocal(provider)?"":DefaultAnthropicModel;}
  public static string DefaultEndpoint(string provider){return provider==OpenAI?OpenAIEndpoint:provider==LmStudio?LmStudioEndpoint:provider==Ollama?OllamaEndpoint:AnthropicEndpoint;}
  public static string ProviderLabel(string provider){return provider==OpenAI?"OpenAI (GPT / Codex)":provider==LmStudio?"LM Studio (local)":provider==Ollama?"Ollama (local)":"Anthropic (Claude)";}
  /// <summary>Request timeout: local models on consumer hardware can legitimately take minutes per answer.</summary>
  public static TimeSpan TimeoutFor(string provider){return IsLocal(provider)?TimeSpan.FromMinutes(10):TimeSpan.FromSeconds(120);}
  string EffectiveModel { get { return String.IsNullOrWhiteSpace(Model)?DefaultModel(Provider):Model.Trim(); } }
  string EffectiveEndpoint { get { return String.IsNullOrWhiteSpace(Endpoint)?DefaultEndpoint(Provider):Endpoint.Trim(); } }

  /// <summary>Checks the key format so obvious paste mistakes are caught before a network call. Local providers accept an empty key.</summary>
  public static string ValidateKey(string provider,string key,bool officialEndpoint=true){
   key=(key??"").Trim();
   if(IsLocal(provider))return key!=""&&key.Any(ch=>Char.IsWhiteSpace(ch)||ch>126)?Core.L.T("API key chứa khoảng trắng hoặc ký tự lạ — hãy dán lại."):null;
   if(key=="")return Core.L.T("Chưa nhập API key.");
   if(key.Any(ch=>Char.IsWhiteSpace(ch)||ch>126))return Core.L.T("API key chứa khoảng trắng hoặc ký tự lạ — hãy dán lại.");
   if(!officialEndpoint)return null;
   if(provider==Anthropic&&!key.StartsWith("sk-ant-",StringComparison.Ordinal))return Core.L.T("Key của Anthropic bắt đầu bằng sk-ant-.");
   if(provider==OpenAI&&!key.StartsWith("sk-",StringComparison.Ordinal))return Core.L.T("Key của OpenAI bắt đầu bằng sk-.");
   return null;
  }

  async Task<string> HttpTransport(string url,Dictionary<string,string> headers,string body){
   using(var request=new HttpRequestMessage(HttpMethod.Post,url)){
    request.Content=new StringContent(body,Encoding.UTF8,"application/json");
    return await SendHttp(request,headers).ConfigureAwait(false);
   }
  }

  async Task<string> HttpGet(string url,Dictionary<string,string> headers){
   using(var request=new HttpRequestMessage(HttpMethod.Get,url))return await SendHttp(request,headers).ConfigureAwait(false);
  }

  async Task<string> SendHttp(HttpRequestMessage request,Dictionary<string,string> headers){
   var timeout=TimeoutFor(Provider);
   try{
    foreach(var h in headers)request.Headers.TryAddWithoutValidation(h.Key,h.Value);
    using(var cts=new System.Threading.CancellationTokenSource(timeout))
    using(var response=await Http.SendAsync(request,cts.Token).ConfigureAwait(false)){
     string text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
     if(!response.IsSuccessStatusCode)throw new IOException(ErrorText((int)response.StatusCode,text));
     return text;
    }
   }catch(HttpRequestException e){throw new IOException((IsLocal(Provider)?Core.L.T("Không kết nối được tới máy chủ LLM cục bộ — hãy mở LM Studio/Ollama và bật Local Server: "):Core.L.T("Không kết nối được tới máy chủ API: "))+(e.InnerException??e).Message,e);}
   catch(TaskCanceledException e){throw new IOException(Core.L.F("Máy chủ API không trả lời trong {0} giây.",(int)timeout.TotalSeconds),e);}
  }

  /// <summary>
  /// Rejects endpoints that would send the key in clear text; localhost is always allowed, and local providers (which send no
  /// key) may also use plain http on a private LAN address so a model on another machine in the house can be used.
  /// </summary>
  public static string ValidateEndpoint(string endpoint,string provider=Anthropic){
   endpoint=(endpoint??"").Trim();if(endpoint=="")return null;
   Uri uri;if(!Uri.TryCreate(endpoint,UriKind.Absolute,out uri))return Core.L.T("Điểm cuối API không phải URL hợp lệ.");
   if(uri.Scheme==Uri.UriSchemeHttps)return null;
   if(uri.Scheme==Uri.UriSchemeHttp&&(uri.IsLoopback||(IsLocal(provider)&&IsPrivateHost(uri.Host))))return null;
   return IsLocal(provider)?Core.L.T("Máy chủ LLM cục bộ phải nằm ở localhost hoặc trong mạng nội bộ (10.x, 172.16–31.x, 192.168.x).") :Core.L.T("Điểm cuối API phải dùng https:// (http:// chỉ cho localhost) để không lộ khóa.");
  }

  /// <summary>RFC 1918 private ranges and .local names count as the home network.</summary>
  public static bool IsPrivateHost(string host){
   if(String.IsNullOrEmpty(host))return false;
   if(host.EndsWith(".local",StringComparison.OrdinalIgnoreCase))return true;
   IPAddress ip;if(!IPAddress.TryParse(host,out ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork)return false;
   byte[] b=ip.GetAddressBytes();
   return b[0]==10||(b[0]==172&&b[1]>=16&&b[1]<=31)||(b[0]==192&&b[1]==168);
  }

  /// <summary>Base URL of an OpenAI-compatible server, derived from its chat-completions endpoint (…/v1).</summary>
  public static string ModelsUrl(string chatEndpoint){
   string e=(chatEndpoint??"").Trim().TrimEnd('/');
   int i=e.LastIndexOf("/chat/completions",StringComparison.OrdinalIgnoreCase);
   if(i>0)e=e.Substring(0,i);
   return e+"/models";
  }

  /// <summary>Asks an OpenAI-compatible server which models it serves (LM Studio lists loaded and downloaded models, Ollama the pulled ones).</summary>
  public async Task<List<string>> ListModels(){
   string endpointProblem=ValidateEndpoint(EffectiveEndpoint,Provider);if(endpointProblem!=null)throw new IOException(endpointProblem);
   if(!OpenAIWire(Provider))throw new IOException(Core.L.T("Chỉ máy chủ tương thích OpenAI mới hỗ trợ liệt kê model."));
   string json=await GetTransport(ModelsUrl(EffectiveEndpoint),Headers()).ConfigureAwait(false);
   return ParseModelList(json);
  }

  public static List<string> ParseModelList(string json){
   var ids=new List<string>();
   using(var doc=JsonDocument.Parse(json)){
    JsonElement data;var root=doc.RootElement;
    var array=root.ValueKind==JsonValueKind.Array?root:root.TryGetProperty("data",out data)&&data.ValueKind==JsonValueKind.Array?data:root.TryGetProperty("models",out data)&&data.ValueKind==JsonValueKind.Array?data:default(JsonElement);
    if(array.ValueKind!=JsonValueKind.Array)return ids;
    foreach(var m in array.EnumerateArray()){
     JsonElement id;
     if(m.ValueKind==JsonValueKind.String)ids.Add(m.GetString());
     else if(m.TryGetProperty("id",out id)&&id.ValueKind==JsonValueKind.String)ids.Add(id.GetString());
     else if(m.TryGetProperty("name",out id)&&id.ValueKind==JsonValueKind.String)ids.Add(id.GetString());
    }
   }
   return ids.Where(i=>!String.IsNullOrWhiteSpace(i)).Distinct().OrderBy(i=>i,StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>Extracts the human-readable message from an API error body.</summary>
  public static string ErrorText(int status,string body){
   string detail=body??"";
   try{
    using(var doc=JsonDocument.Parse(body)){
     JsonElement error;
     if(doc.RootElement.TryGetProperty("error",out error)){
      JsonElement message;
      if(error.ValueKind==JsonValueKind.Object&&error.TryGetProperty("message",out message)&&message.ValueKind==JsonValueKind.String)detail=message.GetString()??"";
      else if(error.ValueKind==JsonValueKind.String)detail=error.GetString()??"";
     }
    }
   }catch(JsonException){}
   string hint=status==401?Core.L.T(" API key không hợp lệ hoặc đã bị thu hồi."):status==429?Core.L.T(" Hết hạn mức hoặc gửi quá nhanh — thử lại sau."):status==404?Core.L.T(" Tên model không tồn tại — kiểm tra ô Model."):"";
   return "HTTP "+status+": "+(detail.Length>400?detail.Substring(0,400)+"…":detail)+hint;
  }

  /// <summary>
  /// Sends the conversation and returns the model's reply. When a local server rejects the request because the loaded model
  /// has no tool support, the request is retried once without tools and the client remembers that for the session.
  /// </summary>
  public async Task<AiReply> Send(string system,List<AiMessage> history,List<AiTool> tools){
   string endpointProblem=ValidateEndpoint(EffectiveEndpoint,Provider);if(endpointProblem!=null)throw new IOException(endpointProblem);
   if(ToolsUnsupported)tools=null;
   if(IsLocal(Provider)&&String.IsNullOrWhiteSpace(Model))throw new IOException(Core.L.T("Chưa chọn model cục bộ — bấm \"Tải danh sách model\" rồi chọn một model đã nạp trong LM Studio/Ollama."));
   try{return await SendOnce(system,history,tools).ConfigureAwait(false);}
   catch(IOException e){
    if(!IsLocal(Provider)||tools==null||tools.Count==0||!LooksLikeToolRejection(e.Message))throw;
    ToolsUnsupported=true;
    return await SendOnce(system,history,null).ConfigureAwait(false);
   }
  }

  async Task<AiReply> SendOnce(string system,List<AiMessage> history,List<AiTool> tools){
   bool openai=OpenAIWire(Provider);
   if(ToolsUnsupported){
    system+=" Tools are unavailable for this model. Answer with guidance only; do not claim to inspect or change this PC.";
    history=history.Select(m=>new AiMessage{Role=m.Role=="tool"?"user":m.Role,Text=m.Role=="tool"?"Previous tool result: "+m.Text:m.Text}).Where(m=>!String.IsNullOrEmpty(m.Text)).ToList();
   }
   string body=openai?BuildOpenAIRequest(system,history,tools):BuildAnthropicRequest(system,history,tools);
   string response=await Transport(EffectiveEndpoint,Headers(),body).ConfigureAwait(false);
   var reply=openai?ParseOpenAIReply(response):ParseAnthropicReply(response);
   if(ToolsUnsupported){reply.ToolCalls.Clear();if(String.IsNullOrWhiteSpace(reply.Text))reply.Text=Core.L.T("Model này không hỗ trợ thao tác. Hãy chọn model có hỗ trợ gọi công cụ.");}
   return reply;
  }

  /// <summary>Heuristic over the server's 400 text: LM Studio / llama.cpp / Ollama name "tools" or the prompt template when a model cannot call functions.</summary>
  public static bool LooksLikeToolRejection(string message){
   string m=(message??"").ToLowerInvariant();
   if(!m.StartsWith("http 400")&&!m.StartsWith("http 422")&&!m.StartsWith("http 500"))return false;
   return m.Contains("tool")||m.Contains("function")||m.Contains("template")||m.Contains("jinja");
  }

  public Dictionary<string,string> Headers(){
   var h=new Dictionary<string,string>();
   string key=(ApiKey??"").Trim();
   if(OpenAIWire(Provider)){if(key!=""||!IsLocal(Provider))h["Authorization"]="Bearer "+key;}
   else{h["x-api-key"]=key;h["anthropic-version"]="2023-06-01";}
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
     if(String.IsNullOrEmpty(m.Text)&&m.ToolCalls.Count==0)continue;
     var d=new Dictionary<string,object>{{"role","assistant"},{"content",String.IsNullOrEmpty(m.Text)?null:m.Text}};
     if(m.ToolCalls.Count>0)d["tool_calls"]=m.ToolCalls.Select(c=>(object)new Dictionary<string,object>{{"id",c.Id},{"type","function"},{"function",new Dictionary<string,object>{{"name",c.Name},{"arguments",c.ArgumentsJson}}}}).ToList();
     messages.Add(d);
    }else messages.Add(new Dictionary<string,object>{{"role","user"},{"content",m.Text}});
   }
   var body=new Dictionary<string,object>{{"model",EffectiveModel},{"messages",messages}};
   if(EffectiveEndpoint==OpenAIEndpoint)body["max_completion_tokens"]=MaxTokens;else body["max_tokens"]=MaxTokens;
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
