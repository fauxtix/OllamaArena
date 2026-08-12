using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
using System.Text;
using System.Text.Json;
using System.Web;
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
        [Inject] public HttpClient? _httpClient { get; set; }
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

        private bool _webSearchEnabled = false;

        private int _currentPromptId;

        private int _conversationId;

        private bool showOllamaError = false;

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

            await PersistUserMessageAsync(userPrompt);

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

            try
            {
                string systemInstructions = await PromptFilesService.GetPromptFileContentAsync("system-prompt.txt") ?? string.Empty;

                await EnsureModelSupportsThinkingAsync();

                var prepared = ChatComposer.Prepare(
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

                if (_webSearchEnabled)
                {
                    string webContext = await BuscarContextoWebAsync(userPrompt);
                    if (!string.IsNullOrWhiteSpace(webContext))
                    {
                        prepared.Payload.Messages.Insert(1, new OllamaChatMessage
                        {
                            Role = "system",
                            Content = $"Contexto pesquisado na web (dados externos atuais):\n{webContext}"
                        });
                    }
                }

                temperature = prepared.Temperature;
                userMessage.Temperature = Math.Round(temperature, 2);

                var payload = prepared.Payload;

                var json = JsonSerializer.Serialize(payload);
                using var request = HttpClientFactory != null
                    ? new HttpRequestMessage(HttpMethod.Post, "api/chat")
                    : new HttpRequestMessage(HttpMethod.Post, OllamaEndpoint);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var ollamaClient = HttpClientFactory?.CreateClient("Ollama") ?? _httpClient;
                if (ollamaClient == null)
                {
                    _logger?.LogError("HttpClient indisponível. Não é possível enviar pedido ao Ollama.");
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

                        if (!string.IsNullOrWhiteSpace(reasoningText))
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
                            if (root.TryGetProperty("message", out var doneMsg)
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
                aiMessage.Text = aiMessage.Text == "..." ? L["Chat.ResponseTimeout"] : aiMessage.Text + L["Chat.ResponseCancelled"];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro inesperado durante o streaming da resposta.");
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

                if (evalCount > 0 && evalDurationNs > 0)
                {
                    try
                    {
                        if (BenchmarkRepo != null)
                        {
                            if (_currentPromptId == 0)
                            {
                                _currentPromptId = await BenchmarkRepo.CreatePromptAsync(userPrompt, temperature);
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
                                TempoProcessamento = stopwatch.Elapsed.TotalMilliseconds,
                                TamanhoTokens = evalCount
                            };

                            await BenchmarkRepo.CreateResponseAsync(newResponse);
                        }
                    }
                    catch (Exception dbEx)
                    {
                        _logger?.LogError(dbEx, "[SQLITE ERROR] Falha ao gravar dados: {Message}", dbEx.Message);
                    }
                }
                else
                {
                    _currentPromptId = 0;
                    _logger?.LogInformation("[BENCHMARK] Teste descartado para o modelo {ModelName}. Prompt incompleto.", ModelName);
                }

                await PersistAssistantMessageAsync(aiMessage, temperature, tempoFinal);

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

            _conversationId = 0;

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
                        _messages.Add(new Models.DTO.ChatMessage
                        {
                            User = msg.Role == "user" ? L["Chat.UserDisplayName"] : "Ollama",
                            Text = msg.Content,
                            IsCurrentUser = msg.Role == "user",
                            Reasoning = msg.Reasoning,
                            ElapsedTime = msg.ElapsedTime,
                            Temperature = msg.Temperature
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

        private string FormatMessage(string content)
        {
            return MessageFormatter.FormatMessagePlus(content);
        }        /// <summary>
        /// Cria um pedido web com cabeçalhos de browser. Os cabeçalhos vão no pedido
        /// (não no HttpClient partilhado) para evitar condições de corrida com o chat.
        /// </summary>
        private static HttpRequestMessage CriarRequestWeb(Uri uri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "pt-PT,pt;q=0.9,en-US;q=0.8,en;q=0.7");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", "none");
            return request;
        }

        private async Task<string> BuscarContextoWebAsync(string query)
        {
            try
            {
                var duckDuckGoTask = SearchWebContext_DuckDuckGo_Async(query);
                var wikipediaTask = SearchWebContext_Wikipedia_Async(query);

                await Task.WhenAll(duckDuckGoTask, wikipediaTask);

                var sb = new StringBuilder();
                string ddg = duckDuckGoTask.Result;
                string wiki = wikipediaTask.Result;

                if (!string.IsNullOrWhiteSpace(ddg))
                {
                    sb.AppendLine("=== Resultados DuckDuckGo ===");
                    sb.AppendLine(ddg.Trim());
                }

                if (!string.IsNullOrWhiteSpace(wiki))
                {
                    sb.AppendLine("=== Resultados Wikipedia ===");
                    sb.AppendLine(wiki.Trim());
                }

                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[RAG] Falha ao obter contexto web.");
                return string.Empty;
            }
        }

        private async Task<string> SearchWebContext_DuckDuckGo_Async(string query)
        {
            try
            {
                if (_httpClient == null)
                {
                    throw new InvalidOperationException("HttpClient is not initialized.");
                }

                string cleanQuery = query?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(cleanQuery))
                    return "Pesquisa vazia.";

                string baseUrl = "https://duckduckgo.com";
                string queryString = $"?q={Uri.EscapeDataString(cleanQuery)}&v=l&kl=pt-pt";
                Uri requestUri = new Uri(baseUrl + queryString, UriKind.Absolute);

                using var request = CriarRequestWeb(requestUri);
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();

                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                var titleNodes = doc.DocumentNode.SelectNodes("//a[@class='result-link']");

                if (titleNodes == null || !titleNodes.Any())
                {
                    titleNodes = doc.DocumentNode.SelectNodes("//td[@class='result-snippet']/preceding::tr//a");
                }

                if (titleNodes == null || !titleNodes.Any())
                    return "Não foram encontrados dados externos relevantes.";

                var sb = new StringBuilder();
                int count = 0;

                foreach (var titleNode in titleNodes)
                {
                    if (count >= 3) break;

                    string title = HtmlAgilityPack.HtmlEntity.DeEntitize(titleNode.InnerText.Trim());
                    string rawUrl = titleNode.GetAttributeValue("href", "");

                    if (string.IsNullOrEmpty(rawUrl) || rawUrl.Contains("://duckduckgo.com"))
                        continue;

                    string link = rawUrl;
                    if (link.StartsWith("//"))
                    {
                        link = $"https:{link}";
                    }
                    else if (link.StartsWith("/"))
                    {
                        link = $"https://duckduckgo.com{link}";
                    }

                    // Extrair o URL real que vem dentro do redirecionamento do DuckDuckGo (parâmetro uddg)
                    if (link.Contains("uddg="))
                    {
                        try
                        {
                            var uri = new Uri(link);
                            var queryParams = HttpUtility.ParseQueryString(uri.Query);
                            string realUrl = queryParams["uddg"] ?? string.Empty;
                            if (!string.IsNullOrEmpty(realUrl))
                            {
                                link = realUrl;
                            }
                        }
                        catch
                        {
                            if (!Uri.IsWellFormedUriString(link, UriKind.Absolute)) continue;
                        }
                    }

                    if (!Uri.IsWellFormedUriString(link, UriKind.Absolute))
                        continue;

                    var parentTr = titleNode.SelectSingleNode("./ancestor::tr");
                    var nextTr = parentTr?.NextSibling;

                    while (nextTr != null && nextTr.Name != "tr")
                    {
                        nextTr = nextTr.NextSibling;
                    }

                    var snippetNode = nextTr?.SelectSingleNode(".//td[@class='result-snippet']");
                    string snippet = snippetNode != null
                        ? HtmlAgilityPack.HtmlEntity.DeEntitize(snippetNode.InnerText.Trim())
                        : "Sem descrição disponível.";

                    sb.AppendLine($"[Fonte {count + 1}]");
                    sb.AppendLine($"Título: {title}");
                    sb.AppendLine($"Link: {link}");
                    sb.AppendLine($"Contexto: {snippet}");
                    sb.AppendLine();

                    count++;
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RAG ERROR] {ex.Message}");
                return string.Empty;
            }
        }
        private async Task<string> SearchWebContext_Wikipedia_Async(string query)
        {
            try
            {
                if (_httpClient == null)
                {
                    throw new InvalidOperationException("HttpClient is not initialized.");
                }

                string url = $"https://pt.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&format=json&origin=*";

                using var request = CriarRequestWeb(new Uri(url, UriKind.Absolute));
                var response = await _httpClient.SendAsync(request);
                var html = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(html);

                var searchResults = jsonDoc.RootElement.GetProperty("query").GetProperty("search");

                var sb = new StringBuilder();
                foreach (var item in searchResults.EnumerateArray().Take(4))
                {
                    string snippet = item.GetProperty("snippet").GetString() ?? "";
                    snippet = snippet.Replace("<span class=\"searchmatch\">", "").Replace("</span>", "");
                    sb.AppendLine(snippet);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RAG FAILURE] {ex.Message}");
                return string.Empty;
            }
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
