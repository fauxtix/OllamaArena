using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaArena.Components.Pages.Components;
using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;
using OllamaArena.Resources;
using OllamaArena.Services;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Globalization;
using System.Text;
using System.Text.Json;

using static OllamaArena.Models.DTO.OllamaModels;

namespace OllamaArena.Components.Pages
{
    public partial class Chat : IDisposable
    {
        [Inject] public IOllamaGpuService? GpuService { get; set; }
        [Inject] public IDialogService? DialogService { get; set; }
        [Inject] public IBenchmarkRepository? BenchmarkRepo { get; set; }
        [Inject] public IConversationRepository? ConversationRepo { get; set; }
        [Inject] public PromptFilesService PromptFilesService { get; set; } = default!;
        [Inject] public ChatComposerService ChatComposer { get; set; } = default!;
        [Inject] public IHttpClientFactory? HttpClientFactory { get; set; }
        [Inject] public ILogger<App>? _logger { get; set; }
        [Inject] public IStringLocalizer<SharedResources> L { get; set; } = default!;
        [Inject] public IOptions<OllamaOptions> OllamaSettings { get; set; } = default!;

        private List<Models.DTO.ChatMessage> _messages = new();
        private string _currentMessage = string.Empty;
        private int _inputKey = 0;
        private bool _isThinking = false;

        private GpuStatus? _gpuReport;
        private long _modelSizeInBytes;

        private int _contextLength;
        private int _contextUsedTokens;
        private bool _showContextBar;

        private bool _modelSupportsThinking;
        private string _modelSupportsThinkingFor = "";

        private ElementReference messagesDiv;
        private bool userAtBottom = true;

        protected bool GPUDialogVisibility = false;

        private HistoryPanel? historyPanel;

        private GpuInfoDialog? gpuDialog;

        private CancellationTokenSource? _cts;

        private string _modelName = "";

        private DotNetObjectReference<Chat>? _dotNetRef;
        private ElementReference chatInputRef;

        private bool isLoadingModels = false;

        private int _currentPromptId = 0;

        private int _conversationId = 0;

        private bool showOllamaError = false;

        // Estado pendente para gravação automática
        private string _pendingUserPrompt = string.Empty;
        private Models.DTO.ChatMessage? _pendingAiMessage;
        private double _pendingTemperature;
        private string _pendingTempoFinal = string.Empty;
        private long _pendingEvalCount;
        private long _pendingEvalDurationNs;
        private long _pendingLoadDurationNs;
        private double _pendingStopwatchMs;

        private bool _saveSuccess;
        private string _saveResultDescricao = string.Empty;
        private string _descricaoAtual = string.Empty;

        private string ModelName
        {
            get => _modelName;
            set
            {
                if (_modelName == value) return;
                _modelName = value;
                _ = OnModelChangedAsync();
            }
        }
        private string OllamaEndpoint => $"{OllamaSettings.Value.BaseUrl}/api/chat";

        private List<string> _models = new();

        double temperature;

        protected override async Task OnInitializedAsync()
        {
            _messages.Add(new Models.DTO.ChatMessage { User = "Ollama", Text = L["Chat.WelcomeMessage"] });
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await GetModelsInfoAsync();

                _dotNetRef = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("chatInput.attachHandlers", chatInputRef, _dotNetRef);
            }
        }

        private async Task GetModelsInfoAsync()
        {
            if (DialogService == null) return;

            try
            {
                isLoadingModels = true;
                StateHasChanged();

                var localModels = await LoadModelsFromLocalStorage();

                if (localModels.Count > 0)
                {
                    _models = localModels;

                    ModelName = _models.First();

                    var allModels = await GpuService!.GetLocalModelsAsync();
                    var currentModelDetails = allModels?.Models?
                        .FirstOrDefault(m =>
                            m.Name.Equals(ModelName, StringComparison.OrdinalIgnoreCase) ||
                            m.Model.Equals(ModelName, StringComparison.OrdinalIgnoreCase));

                    if (currentModelDetails != null)
                    {
                        _modelSizeInBytes = currentModelDetails.SizeInBytes;
                        _gpuReport = await GpuService.CheckGpuCompatibility(currentModelDetails.SizeInBytes);
                    }

                    StateHasChanged();
                }
                else
                {
                    var allModels = await GpuService!.GetLocalModelsAsync();
                    _models.Clear();
                    _models.AddRange(allModels.Models.Select(m => m.Model));

                    await SaveModelsToLocalStorage();

                    if (_models.Count > 0)
                    {
                        ModelName = _models.First();
                    }

                    var currentModelDetails = allModels?.Models?
                        .FirstOrDefault(m =>
                            m.Name.Equals(ModelName, StringComparison.OrdinalIgnoreCase) ||
                            m.Model.Equals(ModelName, StringComparison.OrdinalIgnoreCase));

                    if (currentModelDetails != null)
                    {
                        _modelSizeInBytes = currentModelDetails.SizeInBytes;
                        _gpuReport = await GpuService.CheckGpuCompatibility(currentModelDetails.SizeInBytes);
                    }

                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro detetado GetModelsInfoAsync");
            }
            finally
            {
                isLoadingModels = false;
                showOllamaError = false;
                StateHasChanged();
            }
        }
        private async Task SaveModelsToLocalStorage()
        {
            var modelsJson = JsonSerializer.Serialize(_models);
            await JS.InvokeVoidAsync("localStorage.setItem", "ollamaModels", modelsJson);
        }

        private async Task<List<string>> LoadModelsFromLocalStorage()
        {
            var modelsJson = await JS.InvokeAsync<string>("localStorage.getItem", "ollamaModels");
            if (!string.IsNullOrEmpty(modelsJson))
            {
                return JsonSerializer.Deserialize<List<string>>(modelsJson) ?? new List<string>();
            }
            return [];
        }


        protected void OpenHistory()
        {
            historyPanel?.Open();
        }

        private async Task OnScroll()
        {
            try
            {
                if (messagesDiv.Context != null)
                {
                    var state = await JS.InvokeAsync<ScrollState>("chatScroll.getScrollState", messagesDiv);
                    if (state != null)
                    {
                        userAtBottom = (state.ScrollHeight - state.ScrollTop - state.ClientHeight) < 80;
                    }
                }
            }
            catch { }
        }

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(_currentMessage) || _isThinking) return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var userPrompt = _currentMessage;

            var userMessage = new Models.DTO.ChatMessage
            {
                User = L["Chat.UserDisplayName"],
                Text = userPrompt,
                IsCurrentUser = true
            };
            _messages.Add(userMessage);


            _currentMessage = string.Empty;
            _inputKey++;
            _isThinking = true;
            if (_contextLength == 0)
                _contextLength = await GetContextLengthAsync(ModelName);

            int previousContextTokens = _contextUsedTokens;
            int promptTokenEstimate = ChatComposerService.EstimateTokens(userPrompt);
            int estimatedOutputTokens = 0;
            _contextUsedTokens = previousContextTokens + promptTokenEstimate;
            _showContextBar = true;
            StateHasChanged();
            await ForceScrollToBottomAsync();

            var aiMessage = new Models.DTO.ChatMessage
            {
                User = "Ollama",
                Text = "...",
                IsCurrentUser = false,
                ElapsedTime = "0.00s"
            };

            _messages.Add(aiMessage);
            StateHasChanged();
            await ForceScrollToBottomAsync();

            bool firstChunkReceived = false;

            var timer = new System.Threading.Timer(_ =>
            {
                if (firstChunkReceived || !_isThinking) return;

                var elapsed = stopwatch.Elapsed;
                string tempo = elapsed.TotalSeconds < 10
                    ? $"{elapsed.TotalSeconds:F2}s"
                    : $"{elapsed.TotalSeconds:F1}s";

                aiMessage.ElapsedTime = tempo;
                _ = InvokeAsync(StateHasChanged);
            }, null, 0, 150);

            _cts = new CancellationTokenSource();
            long loadDurationNs = 0;
            long evalDurationNs = 0;
            int evalCount = 0;
            string? errorMessage = null;

            try
            {
                // O system prompt pode conter o token {{TARGET_LANGUAGE}}: resolve-o para o
                // idioma selecionado na UI (seletor PT/EN). Sem token, é no-op.
                string systemInstructions = (await PromptFilesService.GetPromptFileContentAsync("system-prompt.txt") ?? string.Empty)
                    .Replace(TargetLanguageResolver.Token, TargetLanguageResolver.GetTargetLanguage());

                await EnsureModelSupportsThinkingAsync();

                var prepared = await ChatComposer.Prepare(
                    _messages,
                    userPrompt,
                    systemInstructions,
                    L["Chat.WelcomeMessage"],
                    ModelName,
                    _contextLength,
                    _gpuReport?.FitsInGpu == true,
                    _modelSupportsThinking);

                if (prepared is null)
                {
                    _isThinking = false;
                    _messages.Remove(aiMessage);
                    StateHasChanged();
                    return;
                }

                // Quando o reasoning está desativado, não capturamos nem guardamos o raciocínio:
                // modelos tipo deepseek-r1 geram-no sempre (não respeitam `think: false`).
                bool capturarReasoning = prepared.EnableReasoning;

                temperature = prepared.Temperature;
                userMessage.Temperature = Math.Round(temperature, 2);



                var payload = prepared.Payload;

                var json = JsonSerializer.Serialize(payload);
                using var request = HttpClientFactory != null
                    ? new HttpRequestMessage(HttpMethod.Post, "api/chat")
                    : new HttpRequestMessage(HttpMethod.Post, OllamaEndpoint);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var ollamaClient = HttpClientFactory?.CreateClient("Ollama");
                if (ollamaClient == null)
                {
                    _logger?.LogError("HttpClient indisponível. Não é possível enviar pedido ao Ollama.");
                    _isThinking = false;
                    _cts?.Dispose();
                    _cts = null;
                    StateHasChanged();
                    return;
                }

                using var response = await ollamaClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                _logger?.LogInformation("Ollama começou a responder após {Seconds:F1}s (modelo {ModelName}, contexto {Context})",
                    stopwatch.Elapsed.TotalSeconds, ModelName, _contextLength);

                using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                bool firstChunk = true;

                if (_cts is null) return;

                var cts = _cts;

                while (true)
                {
                    string? line = await reader.ReadLineAsync(cts.Token);
                    if (line is null) break;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;

                        string? chunkText = null;
                        string? reasoningText = null;

                        if (root.TryGetProperty("message", out var msgProp))
                        {
                            if (msgProp.TryGetProperty("content", out var contentProp))
                            {
                                chunkText = contentProp.GetString();
                            }

                            if (msgProp.TryGetProperty("reasoning_content", out var reasoningProp))
                            {
                                reasoningText = reasoningProp.GetString();
                            }
                            else if (msgProp.TryGetProperty("thinking", out var thinkingProp))
                            {
                                reasoningText = thinkingProp.GetString();
                            }
                        }
                        else if (root.TryGetProperty("response", out var respProp))
                        {
                            chunkText = respProp.GetString();
                        }

                        if (capturarReasoning && !string.IsNullOrWhiteSpace(reasoningText))
                        {
                            if (aiMessage.Reasoning is null)
                            {
                                aiMessage.Reasoning = reasoningText;
                            }
                            else
                            {
                                aiMessage.Reasoning += reasoningText;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(chunkText))
                        {
                            if (firstChunk)
                            {
                                aiMessage.Text = chunkText;
                                firstChunk = false;
                                firstChunkReceived = true;
                                _logger?.LogInformation("Primeiro token recebido após {Seconds:F1}s (modelo {ModelName})", stopwatch.Elapsed.TotalSeconds, ModelName);
                            }
                            else
                            {
                                aiMessage.Text += chunkText;
                            }

                            estimatedOutputTokens += ChatComposerService.EstimateTokens(chunkText);
                            _contextUsedTokens = previousContextTokens + promptTokenEstimate + estimatedOutputTokens;

                            StateHasChanged();
                            await StreamScrollAsync();
                        }

                        if (root.TryGetProperty("done", out var doneProp) && doneProp.GetBoolean() == true)
                        {
                            var metrics = JsonSerializer.Deserialize<OllamaMetrics>(line);
                            if (metrics != null)
                            {
                                loadDurationNs = metrics.LoadDuration;
                                evalDurationNs = metrics.EvalDuration;
                                evalCount = metrics.PromptEvalCount + metrics.EvalCount;

                                if (evalCount > 0)
                                {
                                    _contextUsedTokens = evalCount;
                                    _showContextBar = true;
                                }
                            }

                            // Alguns modelos (ex.: qwq, deepseek-r1) enviam o reasoning apenas na mensagem final
                            if (capturarReasoning
                                && root.TryGetProperty("message", out var doneMsg)
                                && (doneMsg.TryGetProperty("reasoning_content", out var doneReasoning)
                                    || doneMsg.TryGetProperty("thinking", out doneReasoning)))
                            {
                                string? finalReasoning = doneReasoning.GetString();
                                if (!string.IsNullOrWhiteSpace(finalReasoning))
                                {
                                    aiMessage.Reasoning = (aiMessage.Reasoning is null)
                                        ? finalReasoning
                                        : aiMessage.Reasoning + finalReasoning;
                                }
                            }

                        }
                    }
                    catch (JsonException jex)
                    {
                        aiMessage.Text += $"\n{L["Chat.OllamaParseError", jex.Message]}";
                        _logger?.LogWarning("Failed to parse JSON line from Ollama API: {Line}... continuing the process", line);
                        continue;
                    }
                }

            }
            catch (OperationCanceledException ocEx)
            {
                _logger?.LogError(ocEx, "O streaming da resposta foi cancelado.");
                errorMessage = aiMessage.Text == "..." ? L["Chat.ResponseTimeout"] : L["Chat.ResponseCancelled"];
                aiMessage.Text = aiMessage.Text == "..." ? L["Chat.ResponseTimeout"] : aiMessage.Text + L["Chat.ResponseCancelled"];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro inesperado durante o streaming da resposta.");
                errorMessage = L["Chat.UnexpectedError", ex.Message];
                aiMessage.Text = L["Chat.UnexpectedError", ex.Message];
            }
            finally
            {
                stopwatch.Stop();
                timer?.Dispose();

                string tempoFinal = stopwatch.Elapsed.TotalSeconds < 10
                    ? $"{stopwatch.Elapsed.TotalSeconds:F2}s"
                    : $"{stopwatch.Elapsed.TotalSeconds:F1}s";

                aiMessage.ElapsedTime = tempoFinal;

                if (errorMessage is null)
                {
                    var (summary, cleanText) = ChatComposerService.ExtractSummaryFromResponse(aiMessage.Text);
                    if (summary is not null)
                    {
                        aiMessage.Summary = summary;
                        aiMessage.Text = cleanText;
                    }

                    aiMessage.LanguageWarning = LanguageWarningFor(aiMessage.Text);

                    await HandlePostStreamAsync(userPrompt, aiMessage, temperature,
                        evalCount, evalDurationNs, loadDurationNs,
                        stopwatch.Elapsed.TotalMilliseconds, tempoFinal);
                }

                _isThinking = false;
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                StateHasChanged();
                await ForceScrollToBottomAsync();
            }
        }
        private async Task CancelRequest()
        {
            try
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    await _cts.CancelAsync();
                    _cts.Dispose();
                    _cts = null;

                    // 2. BENCHMARK: Força o Ollama a libertar a GPU imediatamente
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var unloadPayload = new
                            {
                                model = ModelName,
                                keep_alive = 0
                            };

                            var unloadJson = JsonSerializer.Serialize(unloadPayload);

                            using var unloadRequest = new HttpRequestMessage(HttpMethod.Post, $"{OllamaSettings.Value.BaseUrl}/api/generate");
                            unloadRequest.Content = new StringContent(unloadJson, Encoding.UTF8, "application/json");

                            await Http.SendAsync(unloadRequest);
                        }
                        catch { }
                    });
                }
            }
            catch { }
        }

        private async Task StreamScrollAsync()
        {
            try
            {
                await Task.Delay(10);
                if (messagesDiv.Context != null)
                {
                    await JS.InvokeVoidAsync("chatScroll.scrollDuringStream", messagesDiv);
                }
            }
            catch { }
        }

        private async Task ForceScrollToBottomAsync()
        {
            try
            {
                await Task.Delay(30);
                if (messagesDiv.Context != null)
                {
                    await JS.InvokeVoidAsync("chatScroll.scrollToBottom", messagesDiv);
                }
            }
            catch { }
        }

        private async Task SaveModel()
        {
            await JS.InvokeVoidAsync("localStorage.setItem", "ollama_model", ModelName);

        }

        private async Task OnModelChangedAsync()
        {
            try
            {
                await SaveModel();
                var allModels = await GpuService!.GetLocalModelsAsync();

                var currentModelDetails = allModels?.Models?
                    .FirstOrDefault(m =>
                        m.Name.Equals(ModelName, StringComparison.OrdinalIgnoreCase) ||
                        m.Model.Equals(ModelName, StringComparison.OrdinalIgnoreCase));

                if (currentModelDetails != null)
                {
                    _modelSizeInBytes = currentModelDetails.SizeInBytes;
                    _gpuReport = await GpuService.CheckGpuCompatibility(currentModelDetails.SizeInBytes);
                }

                _contextLength = await GetContextLengthAsync(ModelName);
                _contextUsedTokens = 0;
                _showContextBar = false;

                StateHasChanged();
            }
            catch { }
        }

        private async Task<int> GetContextLengthAsync(string modelName)
        {
            try
            {
                if (GpuService == null) return OllamaSettings.Value.DefaultContextLength;
                return await GpuService.GetRecommendedContextLengthAsync(_modelSizeInBytes, modelName);
            }
            catch
            {
                return OllamaSettings.Value.DefaultContextLength;
            }
        }

        private async Task EnsureModelSupportsThinkingAsync()
        {
            try
            {
                if (GpuService == null || _modelSupportsThinkingFor == ModelName) return;

                _modelSupportsThinking = await GpuService.ModelSupportsThinkingAsync(ModelName);
                _modelSupportsThinkingFor = ModelName;
            }
            catch
            {
                _modelSupportsThinking = false;
            }
        }

        private int ContextProgressValue => Math.Min(_contextUsedTokens, _contextLength);
        private int ContextRemaining => Math.Max(0, _contextLength - _contextUsedTokens);
        private bool ContextNearLimit => _contextLength > 0 && (double)_contextUsedTokens / _contextLength >= 0.9;

        [JSInvokable]
        public async Task OnEnterPressedFromJs()
        {
            if (!_isThinking)
                await SendMessage();
        }

        private void NewChat()
        {
            try { _cts?.Cancel(); } catch { }

            _messages.Clear();
            _currentMessage = string.Empty;
            _isThinking = false;

            _contextUsedTokens = 0;
            _showContextBar = false;

            ClearPendingState();
            _descricaoAtual = string.Empty;

            // --- AÇÃO PARA O BENCHMARK: Descarregar o modelo da VRAM ---
            _ = Task.Run(async () =>
            {
                try
                {
                    var unloadPayload = new
                    {
                        model = ModelName,
                        keep_alive = 0
                    };

                    var unloadJson = JsonSerializer.Serialize(unloadPayload);

                    using var unloadRequest = new HttpRequestMessage(HttpMethod.Post, $"{OllamaSettings.Value.BaseUrl}/api/generate");
                    unloadRequest.Content = new StringContent(unloadJson, Encoding.UTF8, "application/json");

                    await Http.SendAsync(unloadRequest);
                }
                catch { /* Ignora erros em background */ }
            });

            _messages.Add(new Models.DTO.ChatMessage
            {
                User = "Ollama",
                Text = L["Chat.WelcomeMessage"]
            });

            StateHasChanged();
        }

        private async Task LoadConversationAsync(int conversationId)
        {
            try
            {
                if (ConversationRepo == null) return;

                _isThinking = false;
                _messages.Clear();

                var messages = await ConversationRepo.GetMessagesAsync(conversationId);
                _conversationId = conversationId;

                if (messages.Count == 0)
                {
                    _messages.Add(new Models.DTO.ChatMessage
                    {
                        User = "Ollama",
                        Text = L["Chat.WelcomeMessage"]
                    });
                }
                else
                {
                    foreach (var msg in messages)
                    {
                        bool isUser = msg.Role == "user";
                        _messages.Add(new Models.DTO.ChatMessage
                        {
                            User = isUser ? L["Chat.UserDisplayName"] : "Ollama",
                            Text = msg.Content,
                            IsCurrentUser = isUser,
                            Reasoning = msg.Reasoning,
                            ElapsedTime = msg.ElapsedTime,
                            Temperature = msg.Temperature,
                            LanguageWarning = isUser ? null : LanguageWarningFor(msg.Content)
                        });
                    }
                }

                StateHasChanged();
                await ForceScrollToBottomAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Falha ao carregar conversa {ConversationId}", conversationId);
            }
        }

        /// <summary>
        /// Aviso local quando a resposta do modelo não respeita o idioma selecionado na UI
        /// (seletor PT/EN). Deteção heurística; inconclusivo nunca gera aviso.
        /// </summary>
        private string? LanguageWarningFor(string? text)
        {
            var detetado = ResponseLanguageChecker.Detect(text);

            if (!ResponseLanguageChecker.IsMismatch(detetado, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName))
                return null;

            return L["Common.LanguageMismatch", TargetLanguageResolver.GetTargetLanguage()];
        }


        private async Task PersistUserMessageAsync(string userPrompt)
        {
            try
            {
                if (ConversationRepo == null) return;

                if (_conversationId == 0)
                {
                    string titulo = userPrompt.Length > 60 ? userPrompt[..60] : userPrompt;
                    _conversationId = await ConversationRepo.CreateConversationAsync(titulo, ModelName);
                }

                if (_conversationId == 0) return;

                await ConversationRepo.AddMessageAsync(new ChatConversationMessage
                {
                    ConversationId = _conversationId,
                    Role = "user",
                    Content = userPrompt,
                    Temperature = temperature,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception dbEx)
            {
                _logger?.LogError(dbEx, "[SQLITE ERROR] Falha ao gravar mensagem do utilizador: {Message}", dbEx.Message);
            }
        }

        private async Task PersistAssistantMessageAsync(Models.DTO.ChatMessage aiMessage, double temp, string tempoFinal)
        {
            try
            {
                if (ConversationRepo == null || _conversationId == 0) return;
                if (aiMessage.Text == "..." || string.IsNullOrWhiteSpace(aiMessage.Text)) return;

                await ConversationRepo.AddMessageAsync(new ChatConversationMessage
                {
                    ConversationId = _conversationId,
                    Role = "assistant",
                    Content = aiMessage.Text,
                    Reasoning = string.IsNullOrWhiteSpace(aiMessage.Reasoning) ? null : aiMessage.Reasoning,
                    Temperature = temp,
                    ElapsedTime = tempoFinal,
                    Timestamp = DateTime.UtcNow
                });

                await ConversationRepo.TouchConversationAsync(_conversationId);
            }
            catch (Exception dbEx)
            {
                _logger?.LogError(dbEx, "[SQLITE ERROR] Falha ao gravar resposta do assistente: {Message}", dbEx.Message);
            }
        }

        private async Task PersistBenchmarkAsync(string userPrompt, Models.DTO.ChatMessage aiMessage, double temp, long evalCount, long evalDurationNs, long loadDurationNs, double totalMs, string descricao)
        {
            if (evalCount <= 0 || evalDurationNs <= 0) return;

            try
            {
                if (BenchmarkRepo == null) return;

                if (_currentPromptId == 0)
                {
                    var (promptId, descGuardada) = await BenchmarkRepo.GetOrCreatePromptIdAsync(userPrompt, descricao);
                    _currentPromptId = promptId;
                    _descricaoAtual = descGuardada;
                }

                double evalSeconds = evalDurationNs / 1_000_000_000.0;
                double tokensPerSecond = evalCount / evalSeconds;

                var newResponse = new BenchmarkResponse
                {
                    PromptId = _currentPromptId,
                    NomeModelo = ModelName,
                    TextoResposta = aiMessage.Text,
                    TokensPorSegundo = Math.Round(tokensPerSecond, 1),
                    TempoPuroMs = evalDurationNs / 1_000_000.0,
                    TempoCargaMs = loadDurationNs / 1_000_000.0,
                    TempoProcessamento = totalMs,
                    TamanhoTokens = (int)evalCount,
                    // Congela o idioma da sessão no momento da geração: o juiz usa este
                    // valor em reavaliações futuras (respostas antigas ficam null).
                    IdiomaSessao = TargetLanguageResolver.GetTargetLanguage()
                };

                await BenchmarkRepo.CreateResponseAsync(newResponse);
            }
            catch (Exception dbEx)
            {
                _logger?.LogError(dbEx, "[SQLITE ERROR] Falha ao gravar benchmark: {Message}", dbEx.Message);
            }
        }

        private async Task HandlePostStreamAsync(
            string userPrompt,
            Models.DTO.ChatMessage aiMessage,
            double temperature,
            long evalCount,
            long evalDurationNs,
            long loadDurationNs,
            double totalMs,
            string tempoFinal)
        {
            _pendingUserPrompt = userPrompt;
            _pendingAiMessage = aiMessage;
            _pendingTemperature = temperature;
            _pendingTempoFinal = tempoFinal;
            _pendingEvalCount = evalCount;
            _pendingEvalDurationNs = evalDurationNs;
            _pendingLoadDurationNs = loadDurationNs;
            _pendingStopwatchMs = totalMs;

            string descricao = !string.IsNullOrWhiteSpace(aiMessage.Summary)
                ? aiMessage.Summary
                : ChatComposerService.SmartFallbackDescription(userPrompt);

            _descricaoAtual = descricao;

            try
            {
                if (_conversationId != 0)
                {
                    await PersistFollowUpExchangeAsync(descricao);
                }
                else
                {
                    _saveSuccess = await PersistAllWithDescriptionAsync(descricao);
                    _saveResultDescricao = descricao;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro ao gravar resposta automaticamente.");
                if (DialogService is not null)
                {
                    await DialogService.ShowErrorAsync(
                        L["Chat.AutoSaveError"],
                        L["Chat.AutoSaveErrorTitle"]);
                }
            }

            ClearPendingState();
        }

        private async Task PersistFollowUpExchangeAsync(string descricao)
        {
            if (ConversationRepo == null) return;

            await ConversationRepo.AddMessageAsync(new ChatConversationMessage
            {
                ConversationId = _conversationId,
                Role = "user",
                Content = _pendingUserPrompt,
                Temperature = _pendingTemperature,
                Timestamp = DateTime.UtcNow
            });

            if (_pendingAiMessage != null)
            {
                await PersistAssistantMessageAsync(_pendingAiMessage, _pendingTemperature, _pendingTempoFinal);
            }

            await PersistBenchmarkAsync(
                _pendingUserPrompt,
                _pendingAiMessage!,
                _pendingTemperature,
                _pendingEvalCount,
                _pendingEvalDurationNs,
                _pendingLoadDurationNs,
                _pendingStopwatchMs,
                descricao);
        }

        private async Task<bool> PersistAllWithDescriptionAsync(string descricao)
        {
            if (ConversationRepo == null) return false;

            try
            {
                string titulo = _pendingUserPrompt.Length > 60 ? _pendingUserPrompt[..60] : _pendingUserPrompt;
                _conversationId = await ConversationRepo.CreateConversationAsync(titulo, ModelName, descricao);

                if (_conversationId == 0) return false;

                await ConversationRepo.AddMessageAsync(new ChatConversationMessage
                {
                    ConversationId = _conversationId,
                    Role = "user",
                    Content = _pendingUserPrompt,
                    Temperature = _pendingTemperature,
                    Timestamp = DateTime.UtcNow
                });

                if (_pendingAiMessage != null)
                {
                    await PersistAssistantMessageAsync(_pendingAiMessage, _pendingTemperature, _pendingTempoFinal);
                }

                await PersistBenchmarkAsync(
                    _pendingUserPrompt,
                    _pendingAiMessage!,
                    _pendingTemperature,
                    _pendingEvalCount,
                    _pendingEvalDurationNs,
                    _pendingLoadDurationNs,
                    _pendingStopwatchMs,
                    descricao);

                return true;
            }
            catch (Exception dbEx)
            {
                _logger?.LogError(dbEx, "[SQLITE ERROR] Falha ao gravar conversa com descrição: {Message}", dbEx.Message);
                return false;
            }
        }

        private void ClearPendingState()
        {
            _pendingUserPrompt = string.Empty;
            _pendingAiMessage = null;
            _pendingTemperature = 0;
            _pendingTempoFinal = string.Empty;
            _pendingEvalCount = 0;
            _pendingEvalDurationNs = 0;
            _pendingLoadDurationNs = 0;
            _pendingStopwatchMs = 0;
        }

        private string FormatMessage(string content)
        {
            return MessageFormatter.FormatMessagePlus(content);
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _dotNetRef?.Dispose();
            }
            catch { }
        }
    }
}
