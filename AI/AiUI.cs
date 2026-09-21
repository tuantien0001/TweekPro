using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TweekPro.AI;
using TweekPro.Network;
using TweekPro.Services;

namespace TweekPro {
 public partial class MainForm : IAiHost {
  TabPage aiTab;ComboBox aiProvider;TextBox aiModel,aiEndpoint,aiKey,aiInput;CheckBox aiShowKey,aiAllow;RichTextBox aiTranscript;Button aiSend,aiSave,aiTest,aiClear;Label aiStatus,aiKeyState;
  AiAgent aiAgent;bool aiBusy;

  /// <summary>Builds the AI Assistant tab: provider/key settings (key stored with DPAPI), a chat transcript and quick questions. The model can query and, after confirmation, manage the app through tools.</summary>
  void BuildAiTab(){
   var tab=aiTab=new TabPage(Core.L.T("Trợ lý AI"));

   var settingsPanel=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(12,8,12,4),BackColor=Theme.Surface,WrapContents=true};Theme.BorderBottom(settingsPanel);
   settingsPanel.Controls.Add(new Label{Text=Core.L.T("Nhà cung cấp"),AutoSize=true,Margin=new Padding(4,9,4,0),ForeColor=Theme.Muted});
   aiProvider=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=190,Margin=new Padding(0,5,12,0),Font=Theme.Body,FlatStyle=FlatStyle.Flat};
   aiProvider.Items.Add(AiClient.ProviderLabel(AiClient.Anthropic));aiProvider.Items.Add(AiClient.ProviderLabel(AiClient.OpenAI));
   aiProvider.SelectedIndex=settings.AiProvider==AiClient.OpenAI?1:0;
   aiProvider.SelectedIndexChanged+=(s,e)=>{string p=AiProviderKey();if(aiModel.Text.Trim()==""||aiModel.Text==AiClient.DefaultModel(p==AiClient.OpenAI?AiClient.Anthropic:AiClient.OpenAI))aiModel.Text=AiClient.DefaultModel(p);aiEndpoint.Text=aiEndpoint.Text==AiClient.DefaultEndpoint(p==AiClient.OpenAI?AiClient.Anthropic:AiClient.OpenAI)?AiClient.DefaultEndpoint(p):aiEndpoint.Text;};
   settingsPanel.Controls.Add(aiProvider);
   settingsPanel.Controls.Add(new Label{Text="Model",AutoSize=true,Margin=new Padding(4,9,4,0),ForeColor=Theme.Muted});
   aiModel=new TextBox{Width=170,Font=Theme.Body,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,5,12,0),Text=settings.AiModel==""?AiClient.DefaultModel(settings.AiProvider):settings.AiModel};
   settingsPanel.Controls.Add(aiModel);
   settingsPanel.Controls.Add(new Label{Text="API key",AutoSize=true,Margin=new Padding(4,9,4,0),ForeColor=Theme.Muted});
   aiKey=new TextBox{Width=300,Font=Theme.Body,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,5,4,0),UseSystemPasswordChar=true,Text=SecretStore.Unprotect(settings.AiKeyProtected)};
   settingsPanel.Controls.Add(aiKey);
   aiShowKey=new CheckBox{Text=Core.L.T("Hiện"),AutoSize=true,Margin=new Padding(0,8,12,0),ForeColor=Theme.Muted};aiShowKey.CheckedChanged+=(s,e)=>aiKey.UseSystemPasswordChar=!aiShowKey.Checked;
   settingsPanel.Controls.Add(aiShowKey);
   aiSave=Theme.Button("Lưu khóa",ButtonStyle.Primary);aiSave.Margin=new Padding(0,4,6,0);aiSave.Click+=(s,e)=>SaveAiSettings(true);settingsPanel.Controls.Add(aiSave);
   aiTest=Theme.Button("Kiểm tra kết nối",ButtonStyle.Secondary);aiTest.Margin=new Padding(0,4,6,0);aiTest.Click+=async(s,e)=>await TestAiConnection();settingsPanel.Controls.Add(aiTest);
   aiKeyState=new Label{AutoSize=true,Margin=new Padding(8,9,4,0),ForeColor=Theme.Muted,Font=Theme.Small};settingsPanel.Controls.Add(aiKeyState);

   var advanced=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(12,2,12,6),BackColor=Theme.Surface,WrapContents=true};Theme.BorderBottom(advanced);
   advanced.Controls.Add(new Label{Text=Core.L.T("Điểm cuối API (tùy chọn, cho máy chủ tương thích)"),AutoSize=true,Margin=new Padding(4,9,4,0),ForeColor=Theme.Muted,Font=Theme.Small});
   aiEndpoint=new TextBox{Width=380,Font=Theme.Small,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,6,12,0),Text=settings.AiEndpoint==""?AiClient.DefaultEndpoint(settings.AiProvider):settings.AiEndpoint};
   advanced.Controls.Add(aiEndpoint);
   aiAllow=new CheckBox{Text=Core.L.T("Cho phép AI thực hiện thao tác thay đổi (mỗi lần đều hỏi xác nhận)"),AutoSize=true,Margin=new Padding(0,8,12,0),Checked=settings.AiAllowActions,ForeColor=Theme.Text};
   aiAllow.CheckedChanged+=(s,e)=>{settings.AiAllowActions=aiAllow.Checked;if(aiAgent!=null)aiAgent.AllowActions=aiAllow.Checked;};
   advanced.Controls.Add(aiAllow);

   var note=Theme.Note("Khóa API được mã hóa bằng Windows DPAPI theo tài khoản của bạn và chỉ gửi tới nhà cung cấp bạn chọn. AI đọc dữ liệu qua các công cụ chỉ đọc; mọi thao tác thay đổi (dừng dịch vụ, kết thúc tiến trình, tắt khởi động, gỡ ứng dụng) đều hiện hộp xác nhận và tuân thủ các giới hạn an toàn của Tweek Pro.",NoteKind.Info);

   aiTranscript=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Theme.Surface,ForeColor=Theme.Text,Font=Theme.Body,DetectUrls=false};
   var transcriptWrap=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16,12,16,8),BackColor=Theme.Surface};transcriptWrap.Controls.Add(aiTranscript);

   var quick=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(12,4,12,2),BackColor=Theme.Stripe,WrapContents=true};
   foreach(var q in new[]{"Máy tôi có gì đang chạy nền tốn RAM và tắt được không?","Dịch vụ nào của bên thứ ba có thể dừng an toàn?","Ứng dụng nào đang dùng mạng nhiều nhất?","Có bao nhiêu rác có thể dọn?","Mục khởi động nào nên tắt?"}){
    string question=q;var b=Theme.Button(question,ButtonStyle.Secondary);b.Font=Theme.Small;b.Margin=new Padding(0,2,6,2);b.Click+=async(s,e)=>await AskAi(Core.L.T(question));quick.Controls.Add(b);
   }

   var inputPanel=new Panel{Dock=DockStyle.Bottom,Height=74,Padding=new Padding(12,8,12,8),BackColor=Theme.Stripe};Theme.BorderTop(inputPanel);
   aiInput=new TextBox{Dock=DockStyle.Fill,Multiline=true,Font=Theme.Body,BorderStyle=BorderStyle.FixedSingle,AcceptsReturn=false};
   aiInput.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter&&!e.Shift){e.SuppressKeyPress=true;await AskAi(aiInput.Text);}};
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Right,Width=250,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(8,0,0,0)};
   aiSend=Theme.Button("Gửi",ButtonStyle.Primary);aiSend.Click+=async(s,e)=>await AskAi(aiInput.Text);
   aiClear=Theme.Button("Xóa hội thoại",ButtonStyle.Secondary);aiClear.Click+=(s,e)=>{if(aiAgent!=null)aiAgent.Reset();aiTranscript.Clear();AiHello();};
   buttons.Controls.Add(aiSend);buttons.Controls.Add(aiClear);
   inputPanel.Controls.Add(aiInput);inputPanel.Controls.Add(buttons);
   aiStatus=new Label{Dock=DockStyle.Bottom,Height=30,Padding=new Padding(16,0,16,0),TextAlign=ContentAlignment.MiddleLeft,BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Small};Theme.BorderTop(aiStatus);

   tab.Controls.Add(transcriptWrap);tab.Controls.Add(quick);tab.Controls.Add(inputPanel);tab.Controls.Add(aiStatus);tab.Controls.Add(note);tab.Controls.Add(advanced);tab.Controls.Add(settingsPanel);
   UpdateAiKeyState();AiHello();
  }

  string AiProviderKey(){return aiProvider.SelectedIndex==1?AiClient.OpenAI:AiClient.Anthropic;}

  void UpdateAiKeyState(){
   bool has=settings.AiKeyProtected!="";
   aiKeyState.Text=!has?Core.L.T("Chưa lưu khóa."):SecretStore.IsEncrypted(settings.AiKeyProtected)?Core.L.T("Đã lưu (mã hóa DPAPI): ")+SecretStore.Mask(SecretStore.Unprotect(settings.AiKeyProtected)):Core.L.T("Đã lưu (chỉ mã hóa base64 — ngoài Windows): ")+SecretStore.Mask(SecretStore.Unprotect(settings.AiKeyProtected));
   aiKeyState.ForeColor=has?Theme.Success:Theme.Muted;
   aiStatus.Text=has?Core.L.F("Sẵn sàng — {0}, model {1}. Enter để gửi; Shift+Enter xuống dòng.",AiClient.ProviderLabel(settings.AiProvider),settings.AiModel==""?AiClient.DefaultModel(settings.AiProvider):settings.AiModel):Core.L.T("Nhập API key của Anthropic (Claude) hoặc OpenAI (GPT/Codex) rồi bấm Lưu khóa để bắt đầu.");
  }

  /// <summary>Validates and persists provider, model, endpoint and the DPAPI-protected key; rebuilds the agent so the next question uses them.</summary>
  void SaveAiSettings(bool announce){
   string provider=AiProviderKey();string key=aiKey.Text.Trim();string endpoint=aiEndpoint.Text.Trim();
   bool official=endpoint==""||endpoint==AiClient.DefaultEndpoint(provider);
   string problem=AiClient.ValidateEndpoint(endpoint)??(key==""?null:AiClient.ValidateKey(provider,key,official));
   if(problem!=null){if(announce)MessageBox.Show(this,problem,Core.L.T("API key"),MessageBoxButtons.OK,MessageBoxIcon.Warning);else{aiStatus.Text=problem;aiStatus.ForeColor=Theme.Danger;}return;}
   settings.AiProvider=provider;settings.AiModel=aiModel.Text.Trim()==AiClient.DefaultModel(provider)?"":aiModel.Text.Trim();
   settings.AiEndpoint=official?"":endpoint;
   try{settings.AiKeyProtected=SecretStore.Protect(key);}
   catch(System.Security.Cryptography.CryptographicException e){Log(Core.L.T("Trợ lý AI: không mã hóa được khóa bằng DPAPI — ")+e.Message);if(announce)MessageBox.Show(this,Core.L.T("Windows không mã hóa được khóa (DPAPI). Khóa chưa được lưu.")+"\r\n"+e.Message,Core.L.T("API key"),MessageBoxButtons.OK,MessageBoxIcon.Error);return;}
   settings.AiAllowActions=aiAllow.Checked;
   SaveSettings();aiAgent=null;UpdateAiKeyState();
   if(announce)Log(key==""?Core.L.T("Trợ lý AI: đã xóa API key."):Core.L.F("Trợ lý AI: đã lưu khóa cho {0} ({1}).",AiClient.ProviderLabel(provider),SecretStore.IsEncrypted(settings.AiKeyProtected)?"DPAPI":"base64"));
  }

  AiClient BuildAiClient(){
   return new AiClient{Provider=settings.AiProvider,Model=settings.AiModel,Endpoint=settings.AiEndpoint,ApiKey=SecretStore.Unprotect(settings.AiKeyProtected)};
  }

  AiAgent Agent(){
   if(aiAgent==null)aiAgent=new AiAgent(BuildAiClient(),this){AllowActions=settings.AiAllowActions,Trace=text=>RunOnUi(()=>AppendTranscript("tool",text))};
   return aiAgent;
  }

  async Task TestAiConnection(){
   SaveAiSettings(false);
   if(settings.AiKeyProtected==""){MessageBox.Show(this,Core.L.T("Chưa nhập API key."),Core.L.T("API key"),MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
   SetAiBusy(true,Core.L.T("Đang kiểm tra kết nối…"));
   try{
    var client=BuildAiClient();client.MaxTokens=32;
    var reply=await client.Send("Reply with the single word OK.",new List<AiMessage>{AiMessage.User("ping")},null);
    aiStatus.Text=Core.L.F("Kết nối thành công — model trả lời: \"{0}\" ({1} token vào / {2} ra).",(reply.Text??"").Trim(),reply.InputTokens,reply.OutputTokens);aiStatus.ForeColor=Theme.Success;
    Log(Core.L.T("Trợ lý AI: kiểm tra kết nối thành công."));
   }catch(Exception e){aiStatus.Text=Core.L.T("Kết nối thất bại: ")+e.Message;aiStatus.ForeColor=Theme.Danger;Log(Core.L.T("Trợ lý AI: kết nối thất bại — ")+e.Message);}
   finally{SetAiBusy(false,null);}
  }

  void SetAiBusy(bool busy,string status){
   aiBusy=busy;aiSend.Enabled=!busy;aiTest.Enabled=!busy;aiSave.Enabled=!busy;aiClear.Enabled=!busy;aiInput.Enabled=!busy;
   if(status!=null){aiStatus.Text=status;aiStatus.ForeColor=Theme.Muted;}
   Cursor=busy?Cursors.AppStarting:Cursors.Default;
  }

  void AiHello(){
   AppendTranscript("assistant",Core.L.T("Xin chào! Tôi là trợ lý của Tweek Pro. Hỏi tôi về máy này — ứng dụng, dịch vụ, mạng, rác, khởi động — hoặc nhờ tôi thao tác; mọi thay đổi đều hỏi bạn xác nhận trước."));
  }

  /// <summary>Sends a question through the agent and renders the answer; errors go to the transcript so the user sees them in context.</summary>
  async Task AskAi(string question){
   question=(question??"").Trim();if(question==""||aiBusy)return;
   if(settings.AiKeyProtected==""){if(aiKey.Text.Trim()!="")SaveAiSettings(true);if(settings.AiKeyProtected==""){aiStatus.Text=Core.L.T("Nhập API key của Anthropic (Claude) hoặc OpenAI (GPT/Codex) rồi bấm Lưu khóa để bắt đầu.");aiStatus.ForeColor=Theme.Danger;return;}}
   aiInput.Clear();AppendTranscript("user",question);
   SetAiBusy(true,Core.L.T("Đang hỏi model…"));
   var agent=Agent();
   try{
    string answer=await agent.Ask(question);
    AppendTranscript("assistant",answer);
    aiStatus.Text=Core.L.F("Xong — {0} token vào / {1} ra trong phiên này.",agent.TotalInputTokens,agent.TotalOutputTokens);aiStatus.ForeColor=Theme.Muted;
   }catch(Exception e){AppendTranscript("error",e.Message);aiStatus.Text=Core.L.T("Lỗi: ")+e.Message;aiStatus.ForeColor=Theme.Danger;Log(Core.L.T("Trợ lý AI: ")+e.Message);}
   finally{SetAiBusy(false,null);aiInput.Focus();}
  }

  void AppendTranscript(string role,string text){
   if(aiTranscript.TextLength>0)aiTranscript.AppendText("\r\n\r\n");
   string label=role=="user"?Core.L.T("Bạn"):role=="assistant"?"Tweek AI":role=="tool"?Core.L.T("Công cụ AI"):Core.L.T("Lỗi");
   Color color=role=="user"?Theme.Primary:role=="assistant"?Theme.Header:role=="tool"?Theme.Muted:Theme.Danger;
   int start=aiTranscript.TextLength;aiTranscript.AppendText(label+"\r\n");
   aiTranscript.Select(start,label.Length);aiTranscript.SelectionColor=color;aiTranscript.SelectionFont=new Font(Theme.Body,FontStyle.Bold);
   int bodyStart=aiTranscript.TextLength;aiTranscript.AppendText(text);
   aiTranscript.Select(bodyStart,text.Length);aiTranscript.SelectionColor=role=="tool"?Theme.Muted:role=="error"?Theme.Danger:Theme.Text;aiTranscript.SelectionFont=role=="tool"?Theme.Small:Theme.Body;
   aiTranscript.Select(aiTranscript.TextLength,0);aiTranscript.ScrollToCaret();
  }

  void RunOnUi(Action action){if(IsDisposed)return;if(InvokeRequired)BeginInvoke(action);else action();}

  /// <summary>Runs an async UI-thread function from a worker continuation and awaits its result.</summary>
  Task<T> OnUi<T>(Func<Task<T>> func){
   var tcs=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
   if(IsDisposed){tcs.TrySetCanceled();return tcs.Task;}
   RunOnUi(async()=>{try{tcs.TrySetResult(await func());}catch(Exception e){tcs.TrySetException(e);}});
   return tcs.Task;
  }

  /// <summary>IAiHost: confirmation dialog for a mutating tool call, shown on the UI thread.</summary>
  public Task<bool> ConfirmAction(string description){
   return OnUi(()=>Task.FromResult(Confirm(Core.L.F("Trợ lý AI muốn thực hiện:\r\n\r\n{0}\r\n\r\nCho phép? Tweek Pro vẫn áp dụng mọi giới hạn an toàn (dịch vụ/tiến trình cốt lõi bị từ chối, thay đổi được lưu vào Kho khôi phục khi có thể).",description))));
  }

  static string Arg(JsonElement args,string name,string fallback=""){JsonElement v;return args.ValueKind==JsonValueKind.Object&&args.TryGetProperty(name,out v)&&v.ValueKind==JsonValueKind.String?v.GetString():fallback;}
  static int ArgInt(JsonElement args,string name,int fallback){JsonElement v;if(args.ValueKind!=JsonValueKind.Object||!args.TryGetProperty(name,out v))return fallback;int i;if(v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out i))return i;return v.ValueKind==JsonValueKind.String&&int.TryParse(v.GetString(),out i)?i:fallback;}
  static bool ArgBool(JsonElement args,string name,bool fallback){JsonElement v;if(args.ValueKind!=JsonValueKind.Object||!args.TryGetProperty(name,out v))return fallback;if(v.ValueKind==JsonValueKind.True)return true;if(v.ValueKind==JsonValueKind.False)return false;bool b;return v.ValueKind==JsonValueKind.String&&bool.TryParse(v.GetString(),out b)?b:fallback;}

  /// <summary>IAiHost: executes one tool over the window's live data on the UI thread.</summary>
  public Task<string> RunTool(string name,JsonElement args){return OnUi(()=>RunToolOnUi(name,args));}

  async Task<string> RunToolOnUi(string name,JsonElement args){
   switch(name){
    case "get_overview":return await AiOverview();
    case "list_applications":return AiApplications(Arg(args,"query"),ArgInt(args,"limit",40));
    case "list_services":return await AiServices(Arg(args,"filter","running"),Arg(args,"query"),ArgInt(args,"limit",60));
    case "explain_service":return await AiExplainService(Arg(args,"name"));
    case "list_network_activity":return AiNetwork(ArgInt(args,"limit",30));
    case "list_startup_entries":return AiStartup();
    case "list_vault":return AiVault(ArgInt(args,"limit",40));
    case "preview_junk":return await AiJunkPreview();
    case "stop_service":return await AiStopService(Arg(args,"name"),ArgBool(args,"disable",false));
    case "start_service":return await AiStartService(Arg(args,"name"));
    case "end_process":return await AiEndProcess(ArgInt(args,"pid",-1));
    case "disable_startup_entry":return await AiDisableStartup(Arg(args,"name"));
    case "uninstall_application":return await AiUninstall(Arg(args,"name"));
    case "open_tab":return AiOpenTab(Arg(args,"tab"));
    default:return "Error: unknown tool "+name;
   }
  }

  async Task<List<ServiceEntry>> AiEnsureServices(){
   if(!svcLoaded){var list=await Task.Run(()=>ServiceCatalog.List());services=list;svcLoaded=true;RenderServices();}
   return services;
  }

  async Task<string> AiOverview(){
   var sb=new StringBuilder();
   sb.AppendLine("os="+Environment.OSVersion.VersionString+" 64bit="+Environment.Is64BitOperatingSystem+" machine="+Environment.MachineName);
   sb.AppendLine("administrator="+Core.Elevation.IsElevated+" language="+Core.L.Lang+" tweekpro=0.7");
   sb.AppendLine("installed_applications="+inventory.Count);
   try{var svc=await AiEnsureServices();sb.AppendLine("services="+svc.Count+" running="+svc.Count(s=>s.Running)+" thirdparty_running="+svc.Count(s=>s.Running&&s.Safety==ServiceSafety.ThirdParty)+" optional_running="+svc.Count(s=>s.Running&&s.Safety==ServiceSafety.Optional));}catch(Exception e){sb.AppendLine("services=unavailable ("+e.Message+")");}
   try{var autoruns=Advanced.Autoruns();sb.AppendLine("startup_entries="+autoruns.Count(a=>a.Item!=null)+" disabled_by_tweekpro="+autoruns.Count(a=>a.Saved!=null));}catch(Exception e){sb.AppendLine("startup_entries=unavailable ("+e.Message+")");}
   try{var backups=Engine.Backups();sb.AppendLine("vault_backups="+backups.Count+" restorable="+backups.Count(b=>b.State!="Restored"));}catch(Exception e){sb.AppendLine("vault=unavailable ("+e.Message+")");}
   sb.AppendLine("network_processes="+netRows.Select(c=>c.Pid).Distinct().Count()+" connections="+netRows.Count+(netRows.Count==0?" (network tab not opened yet; list_network_activity refreshes it)":""));
   try{foreach(var d in DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed))sb.AppendLine("drive "+d.Name+" free="+Presentation.BytesLabel(d.TotalFreeSpace)+" total="+Presentation.BytesLabel(d.TotalSize));}catch(Exception){}
   if(healthReport!=null)sb.AppendLine("health_score="+healthReport.Score+" grade="+healthReport.Grade+" issues="+healthReport.Issues+" reclaimable="+Presentation.BytesLabel(healthReport.Reclaimable));
   else sb.AppendLine("health_score=not computed (user can run it in the Overview tab)");
   return sb.ToString().TrimEnd();
  }

  string AiApplications(string query,int limit){
   var list=inventory.Where(a=>String.IsNullOrEmpty(query)||(a.Name+" "+a.Publisher).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0).OrderByDescending(a=>a.Size).ToList();
   if(list.Count==0)return inventory.Count==0?"No applications loaded (inventory empty on this system).":"No application matches \""+query+"\".";
   var sb=new StringBuilder();sb.AppendLine("total="+list.Count+" showing="+Math.Min(limit,list.Count)+" columns: name | version | publisher | size | installed | folder");
   foreach(var a in list.Take(Math.Max(1,limit)))sb.AppendLine(a.Name+" | "+a.Version+" | "+a.Publisher+" | "+Presentation.SizeLabel(a.Size)+" | "+Presentation.DateLabel(a.InstallDate)+" | "+(a.Location??""));
   return sb.ToString().TrimEnd();
  }

  async Task<string> AiServices(string filter,string query,int limit){
   var all=await AiEnsureServices();
   IEnumerable<ServiceEntry> list=all;
   switch((filter??"running").ToLowerInvariant()){
    case "all":break;case "thirdparty":list=list.Where(s=>s.Safety==ServiceSafety.ThirdParty);break;case "optional":list=list.Where(s=>s.Safety==ServiceSafety.Optional);break;case "stopped":list=list.Where(s=>!s.Running);break;default:list=list.Where(s=>s.Running);break;
   }
   if(!String.IsNullOrEmpty(query))list=list.Where(s=>(s.Name+" "+s.DisplayName+" "+s.Explanation+" "+s.Publisher).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0);
   var rows=list.OrderByDescending(s=>s.Memory).ToList();
   if(rows.Count==0)return all.Count==0?"No services available on this system.":"No service matches.";
   var sb=new StringBuilder();sb.AppendLine("total="+rows.Count+" showing="+Math.Min(limit,rows.Count)+" columns: name | friendly | state | start_mode | pid | ram | safety | publisher | explanation");
   foreach(var s in rows.Take(Math.Max(1,limit)))sb.AppendLine(s.Name+" | "+s.Friendly+" | "+s.State+" | "+s.StartMode+" | "+(s.Pid>0?s.Pid.ToString():"-")+" | "+(s.Memory>=0?Presentation.BytesLabel(s.Memory):"-")+" | "+s.Safety+" | "+(s.Publisher==""?"Microsoft Windows":s.Publisher)+" | "+s.Explanation.Replace("\r\n"," "));
   return sb.ToString().TrimEnd();
  }

  async Task<ServiceEntry> AiFindService(string name){
   if(String.IsNullOrWhiteSpace(name))throw new IOException("name is required");
   var all=await AiEnsureServices();
   return all.FirstOrDefault(s=>String.Equals(s.Name,name,StringComparison.OrdinalIgnoreCase))??all.FirstOrDefault(s=>String.Equals(s.DisplayName,name,StringComparison.OrdinalIgnoreCase))??all.FirstOrDefault(s=>String.Equals(s.Friendly,name,StringComparison.OrdinalIgnoreCase));
  }

  async Task<string> AiExplainService(string name){
   var s=await AiFindService(name);
   if(s==null){
    var known=ServiceCatalog.Knowledge.Services.FirstOrDefault(k=>String.Equals(k.Name,ServiceCatalog.BaseNameOf(name),StringComparison.OrdinalIgnoreCase));
    if(known==null)return "Service \""+name+"\" is not installed on this PC and not in the knowledge base.";
    return "Not installed on this PC. Knowledge base: "+known.Friendly+" — "+(Core.L.English?known.En:known.Vi)+" (safety="+known.Safety+")";
   }
   return DescribeService(s).Replace("\r\n","\n")+"\nsafety="+s.Safety+" core_locked="+(s.Safety==ServiceSafety.Core);
  }

  string AiNetwork(int limit){
   if(netRows.Count==0)return "No network snapshot yet. Ask the user to open the Network tab (open_tab network) and try again.";
   var summaries=NetworkStats.Aggregate(netRows,netTraffic,NetworkStats.MinRate).OrderByDescending(s=>s.TotalPerSecond).ThenByDescending(s=>s.Connections).ToList();
   var sb=new StringBuilder();sb.AppendLine("processes="+summaries.Count+" connections="+netRows.Count+" bandwidth_capture="+(etw!=null&&etw.IsRunning)+" columns: process | pid | publisher | connections | established | remote_endpoints | send_rate | receive_rate | total_sent | total_received");
   foreach(var s in summaries.Take(Math.Max(1,limit))){var id=ProcessResolver.Resolve(s.Pid);sb.AppendLine(id.Display+" | "+s.Pid+" | "+id.Publisher+" | "+s.Connections+" | "+s.Established+" | "+s.RemoteEndpoints+" | "+Rate(s.SentPerSecond)+" | "+Rate(s.ReceivedPerSecond)+" | "+Presentation.BytesLabel(s.Sent)+" | "+Presentation.BytesLabel(s.Received));}
   return sb.ToString().TrimEnd();
  }

  string AiStartup(){
   var list=Advanced.Autoruns();
   if(list.Count==0)return "No startup entries found.";
   var sb=new StringBuilder();sb.AppendLine("entries="+list.Count+" columns: name | state | source | publisher | command");
   foreach(var a in list)sb.AppendLine(a.Name+" | "+(a.Item!=null?"enabled":"disabled_by_tweekpro")+" | "+a.Source+" | "+a.Publisher+" | "+a.Command);
   return sb.ToString().TrimEnd();
  }

  string AiVault(int limit){
   var list=Engine.Backups().OrderByDescending(b=>b.Created).ToList();
   if(list.Count==0)return "Recovery Vault is empty.";
   var sb=new StringBuilder();sb.AppendLine("backups="+list.Count+" columns: created | app | kind | state | original");
   foreach(var b in list.Take(Math.Max(1,limit)))sb.AppendLine(b.Created+" | "+b.AppName+" | "+b.Kind+" | "+b.State+" | "+b.Original);
   return sb.ToString().TrimEnd();
  }

  async Task<string> AiJunkPreview(){
   if(junkRules==null||junkRules.Rules.Count==0)return "No junk rules loaded.";
   var rules=junkRules.Rules.ToList();bool elevated=Core.Elevation.IsElevated;int minAge=settings.JunkMinAgeHours;
   var results=await Task.Run(()=>Cleaner.JunkCleaner.Preview(rules,elevated,minAge,CancellationToken.None));
   junkResults=results;RenderJunk(true);
   var sb=new StringBuilder();sb.AppendLine("total_files="+results.Sum(r=>r.Count)+" total_bytes="+Presentation.BytesLabel(results.Sum(r=>r.Bytes))+" locked_rules="+results.Count(r=>r.Locked)+" columns: rule | group | files | bytes | note");
   foreach(var r in results.OrderByDescending(r=>r.Bytes))sb.AppendLine(r.Rule.Name+" | "+r.Rule.Group+" | "+r.Count+" | "+Presentation.BytesLabel(r.Bytes)+" | "+(r.Locked?"locked: "+r.LockReason:r.Note));
   sb.AppendLine("Nothing was deleted; the user cleans from the Junk Cleaner tab.");
   return sb.ToString().TrimEnd();
  }

  async Task<string> AiStopService(string name,bool disable){
   var s=await AiFindService(name);if(s==null)return "Error: service \""+name+"\" not found.";
   if(s.Safety==ServiceSafety.Core)return "Refused: "+s.Name+" is a core Windows service; Tweek Pro never stops it.";
   if(!s.Running&&!disable)return s.Name+" is already stopped.";
   await Task.Run(()=>ProcessControl.StopService(s.ToHosted(),disable));
   Log(Core.L.F("Trợ lý AI: đã {0} dịch vụ {1}.",disable?Core.L.T("dừng và vô hiệu hóa"):Core.L.T("dừng"),ServiceLabel(s)));
   if(disable)LoadBackups();
   await LoadServices();
   return "Done: "+s.Name+(disable?" stopped and disabled; previous start mode "+s.StartMode+" saved to the Recovery Vault.":" stopped; it may start again at next boot ("+s.StartMode+").");
  }

  async Task<string> AiStartService(string name){
   var s=await AiFindService(name);if(s==null)return "Error: service \""+name+"\" not found.";
   if(s.Running)return s.Name+" is already running.";
   await Task.Run(()=>ServiceCatalog.Start(s.Name));
   Log(Core.L.F("Trợ lý AI: đã khởi động dịch vụ {0}.",ServiceLabel(s)));
   await LoadServices();
   return "Done: "+s.Name+" started.";
  }

  async Task<string> AiEndProcess(int pid){
   if(pid<=0)return "Error: pid is required.";
   var id=ProcessResolver.Resolve(pid);
   if(id.Exited||String.IsNullOrEmpty(id.Name))return "Refused: process "+pid+" is not running or its name cannot be read; only processes visible in list_network_activity or list_services can be ended.";
   string blocked=ProcessControl.TerminateBlockReason(pid,id.Name);
   if(blocked!=null)return "Refused: "+blocked;
   await Task.Run(()=>ProcessControl.Terminate(pid,id.Name));
   Log(Core.L.F("Trợ lý AI: đã kết thúc tiến trình {0} (PID {1}).",id.Display,pid));
   if(netRows.Count>0)await RefreshNetwork(false);
   return "Done: terminated "+id.Display+" (PID "+pid+").";
  }

  async Task<string> AiDisableStartup(string name){
   var a=Advanced.Autoruns().FirstOrDefault(x=>String.Equals(x.Name,name,StringComparison.OrdinalIgnoreCase));
   if(a==null)return "Error: startup entry \""+name+"\" not found.";
   if(a.Item==null)return a.Name+" is already disabled by Tweek Pro (restore from the Recovery Vault).";
   await Task.Run(()=>Advanced.Store(a.Item,true));
   if(autorunList.Items.Count>0)await LoadAutoruns();LoadBackups();
   Log(Core.L.F("Trợ lý AI: đã tắt mục khởi động {0}.",a.Name));
   return "Done: "+a.Name+" disabled and backed up to the Recovery Vault; takes effect at next sign-in.";
  }

  async Task<string> AiUninstall(string name){
   if(String.IsNullOrWhiteSpace(name))return "Error: name is required.";
   var matches=inventory.Where(a=>String.Equals(a.Name,name,StringComparison.OrdinalIgnoreCase)).ToList();
   if(matches.Count==0)matches=inventory.Where(a=>a.Name.IndexOf(name,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();
   if(matches.Count==0)return "Error: no installed application matches \""+name+"\".";
   if(matches.Count>1)return "Ambiguous: "+String.Join("; ",matches.Take(8).Select(a=>a.Name))+". Ask the user which one.";
   var target=matches[0];
   tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().FirstOrDefault(t=>t.Controls.Contains(apps)||t.Controls.Cast<Control>().Any(c=>c.Controls.Contains(apps)))??tabs.SelectedTab;
   foreach(ListViewItem i in apps.Items)i.Checked=i.Tag==target;
   if(apps.CheckedItems.Count==0)return "Error: "+target.Name+" is hidden by the current search filter; clear the search box in the Applications tab.";
   if(busy)return "Error: Tweek Pro is busy with another operation; try again later.";
   bool stillInstalled;
   try{await Uninstall();}
   catch(Exception e){return "Error: "+e.Message;}
   stillInstalled=Engine.Installed(target.Id);
   return (stillInstalled?"The uninstaller for "+target.Name+" was opened or the user cancelled; the application is still registered.":"Done: "+target.Name+" is no longer registered.")+" Leftover candidates in the Leftovers tab: "+candidates.Count+".";
  }

  string AiOpenTab(string key){
   TabPage page=null;
   switch((key??"").ToLowerInvariant()){
    case "overview":page=healthTab;break;case "applications":page=tabs.TabPages.Cast<TabPage>().FirstOrDefault(t=>t.Controls.Cast<Control>().Any(c=>c==apps||c.Controls.Contains(apps)));break;
    case "windows_apps":page=storeTab;break;case "leftovers":page=remnantsTab;break;case "junk":page=junkTab;break;case "empty":page=emptyTab;break;case "duplicates":page=dupeTab;break;
    case "analyzer":page=analyzerTab;break;case "vault":page=tabs.TabPages.Cast<TabPage>().FirstOrDefault(t=>t.Controls.Cast<Control>().Any(c=>c==backups||c.Controls.Contains(backups)));break;
    case "startup":page=autorunTab;break;case "network":page=netTab;break;case "services":page=servicesTab;break;case "tools":page=toolsTab;break;case "ai":page=aiTab;break;
    case "log":page=tabs.TabPages.Cast<TabPage>().FirstOrDefault(t=>t.Controls.Cast<Control>().Any(c=>c==log||c.Controls.Contains(log)));break;
   }
   if(page==null)return "Error: unknown tab \""+key+"\".";
   tabs.SelectedTab=page;return "Opened tab: "+page.Text;
  }

  /// <summary>Seeds a sample conversation so the tab can be previewed off Windows without an API key.</summary>
  public void PreviewAi(){
   aiTranscript.Clear();AiHello();
   AppendTranscript("user",Core.L.T("Dịch vụ nào của bên thứ ba có thể dừng an toàn?"));
   AppendTranscript("tool","list_services (filter = thirdparty): "+Core.L.T("xong"));
   AppendTranscript("assistant",Core.L.T("Có 4 dịch vụ bên thứ ba đang chạy, dừng đều an toàn:\r\n• Adobe Acrobat Update Service (AdobeARMservice) — 3 MB, trình cập nhật; Acrobat vẫn tự cập nhật khi mở.\r\n• MySQL80 — 412 MB RAM, máy chủ cơ sở dữ liệu; dừng nếu bạn không đang lập trình.\r\n• Zoom Sharing Service (ZoomCptService) — 12 MB, Zoom tự bật lại khi cần.\r\n• Brave Elevation Service — đã dừng, chỉ chạy khi cập nhật.\r\nBạn muốn tôi dừng MySQL80 để giải phóng 412 MB không? Tôi sẽ hỏi xác nhận trước khi làm."));
   tabs.SelectedTab=aiTab;
  }
 }
}
