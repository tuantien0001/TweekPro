using System;
using System.Collections.Generic;
using System.Diagnostics;
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
  TabPage aiTab;ComboBox aiProvider,aiModel,aiMode;TextBox aiEndpoint,aiKey,aiInput;CheckBox aiShowKey;RichTextBox aiTranscript;Button aiSend,aiSave,aiTest,aiClear,aiModels;Label aiStatus,aiKeyState,aiKeyLabel;
  AiAgent aiAgent;bool aiBusy;

  /// <summary>Builds the AI Assistant tab: provider (cloud or local LM Studio/Ollama), model picker, DPAPI-protected key, action mode, a chat transcript and quick questions. The model queries and manages the app through tools.</summary>
  void BuildAiTab(){
   var tab=aiTab=new TabPage(Core.L.T("Trợ lý AI"));

   var settingsPanel=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(12,8,12,4),BackColor=Theme.Surface,WrapContents=true};Theme.BorderBottom(settingsPanel);
   settingsPanel.Controls.Add(new Label{Text=Core.L.T("Nhà cung cấp"),AutoSize=true,Margin=new Padding(4,9,4,0),ForeColor=Theme.Muted});
   aiProvider=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=180,Margin=new Padding(0,5,12,0),Font=Theme.Body,FlatStyle=FlatStyle.Flat};
   foreach(var provider in AiClient.Providers)aiProvider.Items.Add(AiClient.ProviderLabel(provider));
   aiProvider.SelectedIndex=Math.Max(0,Array.IndexOf(AiClient.Providers,settings.AiProvider));
   string lastProvider=settings.AiProvider;
   aiProvider.SelectedIndexChanged+=(s,e)=>{
    string p=AiProviderKey();
    if(aiModel.Text.Trim()==""||aiModel.Text==AiClient.DefaultModel(lastProvider))aiModel.Text=AiClient.DefaultModel(p);
    if(aiEndpoint.Text.Trim()==""||aiEndpoint.Text==AiClient.DefaultEndpoint(lastProvider))aiEndpoint.Text=AiClient.DefaultEndpoint(p);
    aiKey.Clear();aiModel.Items.Clear();lastProvider=p;UpdateAiProviderState();
   };
   settingsPanel.Controls.Add(aiProvider);
   settingsPanel.Controls.Add(new Label{Text="Model",AutoSize=true,Margin=new Padding(4,9,4,0),ForeColor=Theme.Muted});
   aiModel=new ComboBox{DropDownStyle=ComboBoxStyle.DropDown,Width=230,Margin=new Padding(0,5,4,0),Font=Theme.Body,FlatStyle=FlatStyle.Flat,Text=settings.AiModel==""?AiClient.DefaultModel(settings.AiProvider):settings.AiModel};
   settingsPanel.Controls.Add(aiModel);
   aiModels=Theme.Button("Tải danh sách model",ButtonStyle.Secondary);aiModels.Margin=new Padding(0,4,12,0);aiModels.Click+=async(s,e)=>await LoadAiModels();settingsPanel.Controls.Add(aiModels);
   aiKeyLabel=new Label{Text="API key",AutoSize=true,Margin=new Padding(4,9,4,0),ForeColor=Theme.Muted};settingsPanel.Controls.Add(aiKeyLabel);
   aiKey=new TextBox{Width=260,Font=Theme.Body,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,5,4,0),UseSystemPasswordChar=true,Text=SecretStore.Unprotect(settings.AiKeyProtected)};
   settingsPanel.Controls.Add(aiKey);
   aiShowKey=new CheckBox{Text=Core.L.T("Hiện"),AutoSize=true,Margin=new Padding(0,8,12,0),ForeColor=Theme.Muted};aiShowKey.CheckedChanged+=(s,e)=>aiKey.UseSystemPasswordChar=!aiShowKey.Checked;
   settingsPanel.Controls.Add(aiShowKey);
   aiSave=Theme.Button("Lưu cấu hình",ButtonStyle.Primary);aiSave.Margin=new Padding(0,4,6,0);aiSave.Click+=(s,e)=>SaveAiSettings(true);settingsPanel.Controls.Add(aiSave);
   aiTest=Theme.Button("Kiểm tra kết nối",ButtonStyle.Secondary);aiTest.Margin=new Padding(0,4,6,0);aiTest.Click+=async(s,e)=>await TestAiConnection();settingsPanel.Controls.Add(aiTest);
   aiKeyState=new Label{AutoSize=true,Margin=new Padding(8,9,4,0),ForeColor=Theme.Muted,Font=Theme.Small};settingsPanel.Controls.Add(aiKeyState);

   var advanced=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(12,2,12,6),BackColor=Theme.Surface,WrapContents=true};Theme.BorderBottom(advanced);
   advanced.Controls.Add(new Label{Text=Core.L.T("Điểm cuối API"),AutoSize=true,Margin=new Padding(4,9,4,0),ForeColor=Theme.Muted,Font=Theme.Small});
   aiEndpoint=new TextBox{Width=330,Font=Theme.Small,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,6,16,0),Text=settings.AiEndpoint==""?AiClient.DefaultEndpoint(settings.AiProvider):settings.AiEndpoint};
   advanced.Controls.Add(aiEndpoint);
   advanced.Controls.Add(new Label{Text=Core.L.T("Quyền thao tác của AI"),AutoSize=true,Margin=new Padding(4,9,4,0),ForeColor=Theme.Muted,Font=Theme.Small});
   aiMode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=250,Margin=new Padding(0,5,12,0),Font=Theme.Small,FlatStyle=FlatStyle.Flat};
   foreach(var mode in Core.AiActionModes.All)aiMode.Items.Add(Core.AiActionModes.Label(mode));
   aiMode.SelectedIndex=Math.Max(0,Array.IndexOf(Core.AiActionModes.All,settings.AiActionMode));
   aiMode.SelectedIndexChanged+=(s,e)=>{settings.AiActionMode=Core.AiActionModes.All[aiMode.SelectedIndex];settings.Normalize();SaveSettings();if(aiAgent!=null)aiAgent.ActionMode=settings.AiActionMode;UpdateAiKeyState();};
   advanced.Controls.Add(aiMode);

   var note=Theme.Note("Chọn LM Studio/Ollama để dùng model cục bộ, hoặc Claude/OpenAI bằng API key. Dữ liệu gửi tới máy chủ bạn chọn. Kho khôi phục lưu phần dọn còn sót, không hoàn tác trình gỡ ứng dụng. Chế độ Tự động chỉ dùng khi bạn chọn; giới hạn bảo vệ hệ thống luôn có hiệu lực.",NoteKind.Info);

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
   UpdateAiProviderState();UpdateAiKeyState();AiHello();
  }

  string AiProviderKey(){return AiClient.Providers[Math.Max(0,aiProvider.SelectedIndex)];}

  /// <summary>Local servers need no key, so the key box is optional there; the model list button only makes sense for OpenAI-style servers.</summary>
  void UpdateAiProviderState(){
   string p=AiProviderKey();bool local=AiClient.IsLocal(p);
   aiKeyLabel.Text=local?Core.L.T("API key (không cần)"):"API key";
   aiModels.Visible=AiClient.OpenAIWire(p);
  }

  /// <summary>True when the saved configuration can be used: a key for cloud providers, a chosen model for local ones.</summary>
  bool AiConfigured(){return AiClient.IsLocal(settings.AiProvider)?settings.AiModel!="":settings.AiKeyProtected!="";}

  string AiSetupHint(){return AiClient.IsLocal(settings.AiProvider)?Core.L.T("Mở LM Studio/Ollama, nạp một model, bấm \"Tải danh sách model\" rồi chọn model và Lưu cấu hình."):Core.L.T("Nhập API key của Anthropic (Claude) hoặc OpenAI (GPT/Codex) rồi bấm Lưu cấu hình để bắt đầu. Hoặc chọn LM Studio/Ollama để dùng model trên máy, không cần key.");}

  void UpdateAiKeyState(){
   bool local=AiClient.IsLocal(settings.AiProvider);bool has=settings.AiKeyProtected!="";
   if(local)aiKeyState.Text=settings.AiModel!=""?Core.L.T("Model cục bộ: ")+settings.AiModel:Core.L.T("Chưa chọn model cục bộ.");
   else aiKeyState.Text=!has?Core.L.T("Chưa lưu khóa."):SecretStore.IsEncrypted(settings.AiKeyProtected)?Core.L.T("Đã lưu (mã hóa DPAPI): ")+SecretStore.Mask(SecretStore.Unprotect(settings.AiKeyProtected)):Core.L.T("Đã lưu (chỉ mã hóa base64 — ngoài Windows): ")+SecretStore.Mask(SecretStore.Unprotect(settings.AiKeyProtected));
   aiKeyState.ForeColor=AiConfigured()?Theme.Success:Theme.Muted;
   aiStatus.Text=AiConfigured()?Core.L.F("Sẵn sàng — {0}, model {1}, chế độ: {2}. Enter để gửi; Shift+Enter xuống dòng.",AiClient.ProviderLabel(settings.AiProvider),settings.AiModel==""?AiClient.DefaultModel(settings.AiProvider):settings.AiModel,Core.AiActionModes.Label(settings.AiActionMode)):AiSetupHint();
   aiStatus.ForeColor=Theme.Muted;
  }

  /// <summary>Validates and persists provider, model, endpoint, action mode and the DPAPI-protected key; rebuilds the agent so the next question uses them.</summary>
  bool SaveAiSettings(bool announce){
   string provider=AiProviderKey();string key=aiKey.Text.Trim();string endpoint=aiEndpoint.Text.Trim();string model=aiModel.Text.Trim();
   bool official=endpoint==""||endpoint==AiClient.DefaultEndpoint(provider);
   string problem=AiClient.ValidateEndpoint(endpoint,provider)??((key==""&&!AiClient.RequiresKey(provider))?null:(key==""?null:AiClient.ValidateKey(provider,key,official)));
   if(problem==null&&AiClient.IsLocal(provider)&&model=="")problem=Core.L.T("Chưa chọn model cục bộ — bấm \"Tải danh sách model\" rồi chọn một model đã nạp trong LM Studio/Ollama.");
   if(problem!=null){if(announce)MessageBox.Show(this,problem,Core.L.T("Trợ lý AI"),MessageBoxButtons.OK,MessageBoxIcon.Warning);else{aiStatus.Text=problem;aiStatus.ForeColor=Theme.Danger;}return false;}
   string protectedKey;
   try{protectedKey=SecretStore.Protect(key);}
   catch(System.Security.Cryptography.CryptographicException e){Log(Core.L.T("Trợ lý AI: không mã hóa được khóa bằng DPAPI — ")+e.Message);if(announce)MessageBox.Show(this,Core.L.T("Windows không mã hóa được khóa (DPAPI). Khóa chưa được lưu.")+"\r\n"+e.Message,Core.L.T("API key"),MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
   bool changed=settings.AiProvider!=provider||settings.AiModel!=(model==AiClient.DefaultModel(provider)?"":model)||settings.AiEndpoint!=(official?"":endpoint)||SecretStore.Unprotect(settings.AiKeyProtected)!=key;
   settings.AiProvider=provider;settings.AiModel=model==AiClient.DefaultModel(provider)?"":model;settings.AiEndpoint=official?"":endpoint;settings.AiKeyProtected=protectedKey;
   settings.AiActionMode=Core.AiActionModes.All[Math.Max(0,aiMode.SelectedIndex)];
   SaveSettings();if(changed)aiAgent=null;UpdateAiKeyState();
   if(announce)Log(Core.L.F("Trợ lý AI: đã lưu cấu hình {0}, model {1}, chế độ {2}.",AiClient.ProviderLabel(provider),settings.AiModel==""?AiClient.DefaultModel(provider):settings.AiModel,Core.AiActionModes.Label(settings.AiActionMode)));
   return true;
  }

  /// <summary>Fetches /models from the configured OpenAI-compatible server and fills the model dropdown, keeping the current text selected when it still exists.</summary>
  async Task LoadAiModels(){
   string provider=AiProviderKey();string endpoint=aiEndpoint.Text.Trim();
   string problem=AiClient.ValidateEndpoint(endpoint,provider);
   if(problem!=null){aiStatus.Text=problem;aiStatus.ForeColor=Theme.Danger;return;}
   SetAiBusy(true,Core.L.T("Đang tải danh sách model…"));
   try{
    var client=new AiClient{Provider=provider,Endpoint=endpoint,ApiKey=aiKey.Text.Trim()};
    var models=await client.ListModels();
    string current=aiModel.Text.Trim();
    aiModel.BeginUpdate();aiModel.Items.Clear();foreach(var m in models)aiModel.Items.Add(m);aiModel.EndUpdate();
    if(models.Count==0){aiStatus.Text=Core.L.T("Máy chủ không có model nào — hãy nạp (load) một model trong LM Studio hoặc `ollama pull` trước.");aiStatus.ForeColor=Theme.Warning;return;}
    aiModel.Text=models.Contains(current)?current:models[0];
    aiStatus.Text=Core.L.F("Đã tải {0} model từ {1}. Chọn model rồi bấm Lưu cấu hình.",models.Count,AiClient.ModelsUrl(endpoint==""?AiClient.DefaultEndpoint(provider):endpoint));aiStatus.ForeColor=Theme.Success;
   }catch(Exception e){aiStatus.Text=Core.L.T("Không tải được danh sách model: ")+e.Message;aiStatus.ForeColor=Theme.Danger;}
   finally{SetAiBusy(false,null);}
  }

  AiClient BuildAiClient(){
   return new AiClient{Provider=settings.AiProvider,Model=settings.AiModel,Endpoint=settings.AiEndpoint,ApiKey=SecretStore.Unprotect(settings.AiKeyProtected)};
  }

  AiAgent Agent(){
   if(aiAgent==null)aiAgent=new AiAgent(BuildAiClient(),this){ActionMode=settings.AiActionMode,Trace=text=>RunOnUi(()=>AppendTranscript("tool",text))};
   return aiAgent;
  }

  async Task TestAiConnection(){
   if(!SaveAiSettings(false))return;
   if(!AiConfigured()){MessageBox.Show(this,AiSetupHint(),Core.L.T("Trợ lý AI"),MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
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
   aiBusy=busy;aiSend.Enabled=!busy;aiTest.Enabled=!busy;aiSave.Enabled=!busy;aiClear.Enabled=!busy;aiInput.Enabled=!busy;aiModels.Enabled=!busy;aiProvider.Enabled=!busy;aiModel.Enabled=!busy;aiEndpoint.Enabled=!busy;aiKey.Enabled=!busy;aiMode.Enabled=!busy;
   if(status!=null){aiStatus.Text=status;aiStatus.ForeColor=Theme.Muted;}
   Cursor=busy?Cursors.AppStarting:Cursors.Default;
  }

  void AiHello(){
   AppendTranscript("assistant",settings.AiActionMode==Core.AiActionModes.Auto
    ?Core.L.T("Xin chào! Tôi là trợ lý của Tweek Pro. Hỏi tôi về máy này hoặc ra lệnh trực tiếp — \"gỡ Zoom và dọn sạch phần còn sót\", \"kill dịch vụ MySQL\", \"dọn rác\" — tôi sẽ làm ngay và báo lại kết quả. Phần dọn còn sót được sao lưu; kho không hoàn tác trình gỡ chính thức; thư mục Windows/System32 luôn được bảo vệ.")
    :Core.L.T("Xin chào! Tôi là trợ lý của Tweek Pro. Hỏi tôi về máy này — ứng dụng, dịch vụ, mạng, rác, khởi động — hoặc nhờ tôi thao tác; mọi thay đổi đều hỏi bạn xác nhận trước."));
  }

  /// <summary>Sends a question through the agent and renders the answer; errors go to the transcript so the user sees them in context.</summary>
  async Task AskAi(string question){
   question=(question??"").Trim();if(question==""||aiBusy)return;
   if(!SaveAiSettings(false))return;
   if(!AiConfigured()){aiStatus.Text=AiSetupHint();aiStatus.ForeColor=Theme.Danger;return;}
   aiInput.Clear();AppendTranscript("user",question);
   SetAiBusy(true,Core.L.T("Đang hỏi model…"));
   var agent=Agent();
   try{
    string answer=await agent.Ask(question);
    if(agent.Client.ToolsUnsupported)AppendTranscript("tool",Core.L.T("Model này không hỗ trợ gọi công cụ; chỉ trả lời hướng dẫn, không thao tác trên máy."));
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
    case "uninstall_application":return await AiUninstall(Arg(args,"name"),ArgBool(args,"clean_leftovers",true));
    case "scan_leftovers":return await AiScanLeftovers(Arg(args,"name"),ArgBool(args,"deep",false));
    case "clean_leftovers":return await AiCleanLeftovers(Arg(args,"name"));
    case "clean_junk":return await AiCleanJunk(Arg(args,"groups"));
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

  AppEntry AiFindApp(string name,out string problem){
   problem=null;
   if(String.IsNullOrWhiteSpace(name)){problem="Error: name is required.";return null;}
   var matches=inventory.Where(a=>String.Equals(a.Name,name,StringComparison.OrdinalIgnoreCase)).ToList();
   if(matches.Count==0)matches=inventory.Where(a=>a.Name.IndexOf(name,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();
   if(matches.Count==0){problem="Error: no installed application matches \""+name+"\".";return null;}
   if(matches.Count>1){problem="Ambiguous: "+String.Join("; ",matches.Take(8).Select(a=>a.Name))+". Ask the user which one.";return null;}
   return matches[0];
  }

  /// <summary>
  /// Runs the official uninstaller (MSI silently in automatic mode), waits, then traces and — when asked — quarantines the leftovers.
  /// No Tweek Pro dialogs are shown; the assistant's action mode already decided whether the user was asked.
  /// </summary>
  async Task<string> AiUninstall(string name,bool cleanLeftovers){
   string problem;var target=AiFindApp(name,out problem);if(target==null)return problem;
   if(busy)return "Error: Tweek Pro is busy with another operation; try again later.";
   ProcessStartInfo info;
   try{info=Engine.UninstallInfo(target);}catch(Exception e){return "Error: "+e.Message;}
   bool quiet=target.Msi&&settings.AiActionMode==Core.AiActionModes.Auto;
   if(quiet)info.Arguments+=" /qn /norestart";
   await Task.Run(()=>Advanced.Capture(target));Remember(new[]{target});
   Log(Core.L.F("Trợ lý AI: mở trình gỡ {0}{1}.",target.Name,quiet?Core.L.T(" (MSI im lặng)"):""));
   busy=true;
   try{
    using(var process=Process.Start(info)){if(process!=null)await Task.Run(()=>process.WaitForExit());}
   }catch(Exception e){busy=false;return "Error: uninstaller could not be started — "+e.Message;}
   finally{busy=false;}
   bool remains=Engine.Installed(target.Id);
   if(remains){await Task.Delay(1500);remains=Engine.Installed(target.Id);}
   await Reload();
   if(remains){Log(target.Name+": "+Core.L.T("vẫn còn đăng ký cài đặt; không tự quét/dọn."));return "The uninstaller of "+target.Name+" finished but the application is still registered (the user cancelled it, or another uninstall window is still open). Nothing was cleaned.";}
   Log(target.Name+": "+Core.L.T("đã bỏ đăng ký."));
   var sb=new StringBuilder();sb.AppendLine("Done: "+target.Name+" is no longer registered"+(quiet?" (silent MSI uninstall)":"")+".");
   var found=await Task.Run(()=>Advanced.QuickScan(target).Items);
   PresentCandidates(candidates.Concat(found));
   sb.AppendLine("Leftover trace: "+found.Count+" item(s) found ("+found.Count(c=>c.ReviewOnly)+" review-only).");
   if(cleanLeftovers&&found.Count>0)sb.AppendLine(await AiQuarantine(found.Where(c=>!c.ReviewOnly).ToList(),target.Name));
   else if(found.Count>0)sb.AppendLine("Not cleaned yet; call clean_leftovers to move them to the Recovery Vault.");
   if(candidates.Count>0)tabs.SelectedTab=remnantsTab;
   return sb.ToString().TrimEnd();
  }

  /// <summary>Read-only leftover trace for an installed app or one from the uninstall history; results are merged into the Leftovers tab.</summary>
  async Task<string> AiScanLeftovers(string name,bool deep){
   List<AppEntry> targets;
   if(String.IsNullOrWhiteSpace(name)){
    targets=history.ToList();
    if(targets.Count==0)return "Error: uninstall history is empty; give an application name.";
   }else{
    string problem;var installed=AiFindApp(name,out problem);
    if(installed!=null)targets=new List<AppEntry>{installed};
    else{
     var past=history.Where(a=>a.Name.IndexOf(name,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();
     if(past.Count==0)return problem;
     targets=past;
    }
   }
   if(busy)return "Error: Tweek Pro is busy with another operation; try again later.";
   foreach(var t in targets){bool installed;try{installed=Engine.Installed(t.Id);}catch(Exception){installed=false;}if(installed)await Task.Run(()=>Advanced.Capture(t));}
   Log(Core.L.F("Trợ lý AI: quét phần còn sót của {0} ứng dụng ({1}).",targets.Count,deep?Core.L.T("quét sâu"):Core.L.T("quét nhanh")));
   var found=new List<Candidate>();var notes=new List<string>();
   await Task.Run(()=>{foreach(var t in targets){var r=deep?Advanced.DeepScan(t,CancellationToken.None):Advanced.QuickScan(t);found.AddRange(r.Items);notes.AddRange(r.Notes.Take(5));}});
   PresentCandidates(candidates.Concat(found));tabs.SelectedTab=remnantsTab;
   var sb=new StringBuilder();sb.AppendLine("apps="+String.Join("; ",targets.Select(t=>t.Name))+" found="+found.Count+" review_only="+found.Count(c=>c.ReviewOnly)+" total_listed="+candidates.Count+" columns: kind | app | path | reason");
   foreach(var c in found.Take(60))sb.AppendLine(c.Kind+" | "+c.AppName+" | "+Presentation.CandidatePath(c)+" | "+(c.ReviewOnly?"REVIEW-ONLY ":"")+c.Reason);
   if(found.Count>60)sb.AppendLine("… "+(found.Count-60)+" more in the Leftovers tab.");
   foreach(var n in notes.Take(8))sb.AppendLine("note: "+n);
   sb.AppendLine("Nothing was removed; call clean_leftovers to move them to the Recovery Vault.");
   return sb.ToString().TrimEnd();
  }

  async Task<string> AiCleanLeftovers(string name){
   var selected=candidates.Where(c=>!c.ReviewOnly&&(String.IsNullOrWhiteSpace(name)||c.AppName.IndexOf(name,StringComparison.CurrentCultureIgnoreCase)>=0)).ToList();
   if(selected.Count==0)return candidates.Count==0?"Nothing to clean: the Leftovers tab is empty. Run scan_leftovers first.":"Nothing to clean"+(String.IsNullOrWhiteSpace(name)?"":" for \""+name+"\"")+": only review-only items are listed.";
   if(busy)return "Error: Tweek Pro is busy with another operation; try again later.";
   return await AiQuarantine(selected,String.IsNullOrWhiteSpace(name)?"all":name);
  }

  /// <summary>Quarantines leftover items one by one through the engine (vault-backed), refusing anything the AI safety guard or the engine boundaries reject.</summary>
  async Task<string> AiQuarantine(List<Candidate> items,string label){
   int moved=0,refused=0,failed=0;var refusedLines=new List<string>();var failedLines=new List<string>();
   foreach(var c in items){
    string reason=(c.Kind=="Folder"||c.Kind=="File")?AiSafety.ProtectedReason(c.Path):null;
    if(reason!=null){refused++;refusedLines.Add(c.Path+" — "+reason);Log(Core.L.T("Trợ lý AI: từ chối xóa ")+c.Path+" — "+reason);continue;}
    try{
     await Task.Run(()=>Engine.Quarantine(c));
     moved++;candidates.Remove(c);Log(Core.L.T("Trợ lý AI: đã sao lưu và dọn ")+c.Path);
    }catch(Exception e){failed++;failedLines.Add(c.Path+" — "+e.Message);Log(Core.L.T("Trợ lý AI: chưa dọn được ")+c.Path+" — "+e.Message);}
   }
   RenderCandidates();LoadBackups();
   var sb=new StringBuilder();
   sb.AppendLine("Cleaned "+moved+" of "+items.Count+" leftover item(s) for "+label+" into the Recovery Vault; refused="+refused+" failed="+failed+".");
   foreach(var r in refusedLines.Take(10))sb.AppendLine("refused: "+r);
   foreach(var f in failedLines.Take(10))sb.AppendLine("failed: "+f);
   if(failed>0)sb.AppendLine("Failed items usually are locked by a running process; the user can retry from the Leftovers tab, which can also schedule them for deletion at reboot.");
   return sb.ToString().TrimEnd();
  }

  /// <summary>Cleans junk into the vault for every unlocked rule (optionally limited to groups/rule names), reusing the last preview when present.</summary>
  async Task<string> AiCleanJunk(string groups){
   if(junkRules==null||junkRules.Rules.Count==0)return "No junk rules loaded.";
   if(busy)return "Error: Tweek Pro is busy with another operation; try again later.";
   if(junkResults==null||junkResults.Count==0)await AiJunkPreview();
   var wanted=(groups??"").Split(new[]{',',';'},StringSplitOptions.RemoveEmptyEntries).Select(g=>g.Trim()).Where(g=>g!="").ToList();
   var selected=junkResults.Where(r=>!r.Locked&&r.Count>0&&(wanted.Count==0||wanted.Any(w=>String.Equals(w,r.Rule.Group,StringComparison.CurrentCultureIgnoreCase)||String.Equals(w,r.Rule.Name,StringComparison.CurrentCultureIgnoreCase)||String.Equals(w,Core.L.T(r.Rule.Group),StringComparison.CurrentCultureIgnoreCase)||String.Equals(w,Core.L.T(r.Rule.Name),StringComparison.CurrentCultureIgnoreCase)))).ToList();
   if(selected.Count==0)return "Nothing to clean: no unlocked rule with files"+(wanted.Count==0?"":" in \""+groups+"\"")+". Groups available: "+String.Join(", ",junkResults.Select(r=>r.Rule.Group).Distinct())+".";
   Log(Core.L.F("Trợ lý AI: dọn rác {0} quy tắc vào Kho khôi phục.",selected.Count));
   busy=true;
   Cleaner.JunkReport report;
   try{report=await Task.Run(()=>Cleaner.JunkCleaner.Clean(selected,false,CancellationToken.None));}
   catch(Exception e){return "Error: "+e.Message;}
   finally{busy=false;}
   junkResults=junkRules.Rules.Select(r=>new Cleaner.JunkRuleResult{Rule=r}).ToList();RenderJunk(false);LoadBackups();
   var sb=new StringBuilder();
   sb.AppendLine("Done: cleaned "+report.Cleaned+" file(s), "+Presentation.BytesLabel(report.Bytes)+" moved to the Recovery Vault; skipped_in_use="+report.SkippedInUse+" failed="+report.Failed+" rules="+String.Join(", ",selected.Select(r=>r.Rule.Name)));
   foreach(var err in report.Errors.Take(5))sb.AppendLine("error: "+err);
   return sb.ToString().TrimEnd();
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
