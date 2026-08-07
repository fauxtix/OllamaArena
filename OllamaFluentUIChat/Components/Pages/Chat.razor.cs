using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaFluentUIChat.Components.Pages.Components;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.Resources;
using OllamaFluentUIChat.Services;
using OllamaFluentUIChat.Services.Helpers;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Models;
using System.Text;
using System.Text.Json;
using System.Web;
using static OllamaFluentUIChat.Models.DTO.OllamaModels;

namespace OllamaFluentUIChat.Components.Pages
{
    public partial class Chat : IDisposable
    {
        [Inject] public IOllamaGpuService? GpuService { get; set; }
        [Inject] public IDialogService? DialogService { get; set; }
        [Inject] public IBenchmarkRepository? BenchmarkRepo { get; set; }
        [Inject] public PromptFilesService PromptFilesService { get; set; } = default!;
        [Inject] public HttpClient? _httpClient { get; set; }
        [Inject] public IHttpClientFactory? HttpClientFactory { get; set; }
        [Inject] public ILogger<App>? _logger { get; set; }
        [Inject] public IStringLocalizer<SharedResources> L { get; set; } = default!;

        private List<Models.DTO.ChatMessage> _messages = new();
        private string _currentMessage = string.Empty;
        private int _inputKey = 0;
        private bool _isThinking = false;

        private GpuStatus? _gpuReport;
        private long _modelSizeInBytes;

        private int _contextLength;
        private int _contextUsedTokens;
        private bool _showContextBar;

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

        private int _currentPromptId;

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
        private const string OllamaEndpoint = "http://localhost:11434/api/chat";

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
                        _gpuReport = GpuService.CheckGpuCompatibility(currentModelDetails.SizeInBytes);
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
                        _gpuReport = GpuService.CheckGpuCompatibility(currentModelDetails.SizeInBytes);
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

            _messages.Add(new Models.DTO.ChatMessage
            {
                User = L["Chat.UserDisplayName"],
                Text = userPrompt,
                IsCurrentUser = true
            });

            _currentMessage = string.Empty;
            _inputKey++;
            _isThinking = true;

            if (_contextLength == 0)
                _contextLength = await GetContextLengthAsync(ModelName);

            int previousContextTokens = _contextUsedTokens;
            int promptTokenEstimate = EstimateTokens(userPrompt);
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
                var historyPayload = new List<OllamaChatMessage>();
                string systemInstructions = await PromptFilesService.GetPromptFileContentAsync("system-prompt.txt") ?? string.Empty;

                historyPayload.Add(new OllamaChatMessage
                {
                    Role = "system",
                    Content = systemInstructions
                });

                foreach (var msg in _messages)
                {
                    if (string.IsNullOrWhiteSpace(msg.Text) || msg.Text == "...") continue;
                    if (msg.Text.StartsWith(L["Chat.WelcomeMessage"], StringComparison.OrdinalIgnoreCase)) continue;

                    string roleAtual = msg.IsCurrentUser ? "user" : "assistant";

                    if (historyPayload.Count == 0 || historyPayload[^1].Role != roleAtual)
                    {
                        historyPayload.Add(new OllamaChatMessage
                        {
                            Role = roleAtual,
                            Content = msg.Text
                        });
                    }
                    else
                    {
                        historyPayload[^1].Content += "\n" + msg.Text;
                    }
                }

                if (historyPayload.Count > 0 && historyPayload[^1].Role == "assistant")
                {
                    historyPayload.RemoveAt(historyPayload.Count - 1);
                }

                if (historyPayload.Count == 0 || historyPayload[^1].Role != "user")
                {
                    _isThinking = false;
                    _messages.Remove(aiMessage);
                    StateHasChanged();
                    return;
                }

                if (_contextLength > 0)
                {
                    int historyBudget = (int)(_contextLength * 0.60);
                    int estimatedHistoryTokens = historyPayload.Sum(m => EstimateTokens(m.Content));

                    while (historyPayload.Count > 2 && estimatedHistoryTokens > historyBudget)
                    {
                        var removed = historyPayload[1]; // mantém o system (índice 0) e o prompt atual (último)
                        historyPayload.RemoveAt(1);
                        estimatedHistoryTokens -= EstimateTokens(removed.Content);
                    }
                }

                temperature = ChatMeasureTemperature.ObterTemperaturaRecomendada(userPrompt);
                int baseTokens = _gpuReport?.FitsInGpu == true ? 1800 : 1200;
                int maxTokens = temperature switch
                {
                    <= 0.25 => (int)(baseTokens * 1.15),
                    >= 0.75 => (int)(baseTokens * 0.90),
                    _ => baseTokens
                };

                // Limita o num_predict para caber no contexto (reserva de 25%)
                if (_contextLength > 0)
                {
                    int totalHistoryTokens = historyPayload.Sum(m => EstimateTokens(m.Content));
                    int reservedForOutput = (int)(_contextLength * 0.25);
                    int availableForOutput = _contextLength - totalHistoryTokens - reservedForOutput;
                    if (availableForOutput < 32) availableForOutput = 32;
                    maxTokens = Math.Min(maxTokens, availableForOutput);
                }

                var payload = new OllamaChatPayload
                {
                    Model = ModelName,
                    Messages = historyPayload,
                    Stream = true,
                    Options = new Dictionary<string, object>
            {
                { "num_ctx", _contextLength },
                { "temperature", Math.Round(temperature, 2) },
                { "num_predict", maxTokens },
                { "repeat_penalty", 1.1 },
                { "top_k", temperature <= 0.25 ? 40 : 600 },
                { "top_p", temperature <= 0.30 ? 0.85 : 0.92 }
            }
                };

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

                        if (root.TryGetProperty("message", out var msgProp) && msgProp.TryGetProperty("content", out var contentProp))
                        {
                            chunkText = contentProp.GetString();
                        }
                        else if (root.TryGetProperty("response", out var respProp))
                        {
                            chunkText = respProp.GetString();
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

                            estimatedOutputTokens += EstimateTokens(chunkText);
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

                            using var unloadRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/generate");
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

        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !_isThinking)
            {
                await SendMessage();
            }
        }

        private async Task HandleJsKey(KeyboardEventArgs e)
        {
            await JS.InvokeVoidAsync("chatInput.handleKey", e, _dotNetRef);
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
                    _gpuReport = GpuService.CheckGpuCompatibility(currentModelDetails.SizeInBytes);
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
                if (GpuService == null) return 2048;
                return await GpuService.GetRecommendedContextLengthAsync(_modelSizeInBytes, modelName);
            }
            catch
            {
                return 2048;
            }
        }

        private static int EstimateTokens(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return Math.Max(1, text.Length / 4);
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

                    using var unloadRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/generate");
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


        private string FormatMessage(string content)
        {
            return MessageFormatter.FormatMessagePlus(content);
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

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pt-PT,pt;q=0.9,en-US;q=0.8,en;q=0.7");
                _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");

                string baseUrl = "https://duckduckgo.com";
                string queryString = $"?q={Uri.EscapeDataString(cleanQuery)}&v=l&kl=pt-pt";
                Uri requestUri = new Uri(baseUrl + queryString, UriKind.Absolute);

                var response = await _httpClient.GetAsync(requestUri);
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

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pt-PT,pt;q=0.9,en-US;q=0.8,en;q=0.7");

                string url = $"https://pt.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&format=json&origin=*";

                var response = await _httpClient.GetStringAsync(url);
                using var jsonDoc = JsonDocument.Parse(response);

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

        // não usada esta biblioteca (Ollama CSharp), mas mantida para referência futura (de momento, este processo parece mais lento do que o usado na app)
        private async Task SendMessageWithOllamaSharpAsync()
        {
            if (string.IsNullOrWhiteSpace(_currentMessage) || _isThinking) return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var userPrompt = _currentMessage;

            // --- UI: add user message ---
            _messages.Add(new Models.DTO.ChatMessage
            {
                User = L["Chat.UserDisplayName"],
                Text = userPrompt,
                IsCurrentUser = true
            });
            _currentMessage = string.Empty;
            _inputKey++;
            _isThinking = true;
            StateHasChanged();
            await ForceScrollToBottomAsync();

            // --- UI: placeholder AI message ---
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

            // Real-time timer (same as before)
            bool firstChunkReceived = false;
            var timer = new System.Threading.Timer(_ =>
            {
                if (firstChunkReceived || !_isThinking) return;
                var elapsed = stopwatch.Elapsed;
                aiMessage.ElapsedTime = elapsed.TotalSeconds < 10
                    ? $"{elapsed.TotalSeconds:F2}s"
                    : $"{elapsed.TotalSeconds:F1}s";
                _ = InvokeAsync(StateHasChanged);
            }, null, 0, 150);

            _cts = new CancellationTokenSource();
            long loadDurationNs = 0;
            long evalDurationNs = 0;
            int evalCount = 0;

            try
            {
                // ---------- Build history (same logic you already have) ----------
                var historyPayload = new List<OllamaSharp.Models.Chat.Message>();

                string systemInstructions = await PromptFilesService.GetPromptFileContentAsync("system-prompt.txt") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(systemInstructions))
                {
                    historyPayload.Add(new OllamaSharp.Models.Chat.Message
                    {
                        Role = OllamaSharp.Models.Chat.ChatRole.System,
                        Content = systemInstructions
                    });
                }

                foreach (var msg in _messages)
                {
                    if (string.IsNullOrWhiteSpace(msg.Text) || msg.Text == "...") continue;
                    if (msg.Text.StartsWith(L["Chat.WelcomeMessage"], StringComparison.OrdinalIgnoreCase)) continue;

                    var role = msg.IsCurrentUser
                        ? OllamaSharp.Models.Chat.ChatRole.User
                        : OllamaSharp.Models.Chat.ChatRole.Assistant;

                    if (historyPayload.Count == 0 || historyPayload[^1].Role != role)
                    {
                        historyPayload.Add(new OllamaSharp.Models.Chat.Message
                        {
                            Role = role,
                            Content = msg.Text
                        });
                    }
                    else
                    {
                        historyPayload[^1].Content += "\n" + msg.Text;
                    }
                }

                // Remove trailing assistant message if present
                if (historyPayload.Count > 0 && historyPayload[^1].Role == OllamaSharp.Models.Chat.ChatRole.Assistant)
                    historyPayload.RemoveAt(historyPayload.Count - 1);

                if (historyPayload.Count == 0 || historyPayload[^1].Role != OllamaSharp.Models.Chat.ChatRole.User)
                {
                    _isThinking = false;
                    _messages.Remove(aiMessage);
                    StateHasChanged();
                    return;
                }

                // ---------- Temperature & token limits (same logic) ----------
                double temperature = ChatMeasureTemperature.ObterTemperaturaRecomendada(userPrompt);
                int baseTokens = _gpuReport?.FitsInGpu == true ? 1800 : 1200;
                int maxTokens = temperature switch
                {
                    <= 0.25 => (int)(baseTokens * 1.15),
                    >= 0.75 => (int)(baseTokens * 0.90),
                    _ => baseTokens
                };

                // ---------- OllamaSharp client ----------
                // Re-use your existing HttpClient if you want
                //var ollama = new OllamaApiClient(_httpClient!, ModelName);
                var ollama = new OllamaApiClient("http://localhost:11434", ModelName);

                var request = new ChatRequest
                {
                    Model = ModelName,
                    Messages = historyPayload,
                    Stream = true,
                    Options = new RequestOptions
                    {
                        Temperature = (float)Math.Round(temperature, 2),
                        NumPredict = maxTokens,
                        RepeatPenalty = 1.1f,
                        TopK = temperature <= 0.25 ? 40 : 600,
                        TopP = temperature <= 0.30f ? 0.85f : 0.92f
                    }
                };

                bool firstChunk = true;

                await foreach (var chunk in ollama.ChatAsync(request, _cts.Token))
                {
                    if (chunk?.Message?.Content is { Length: > 0 } content)
                    {
                        if (firstChunk)
                        {
                            aiMessage.Text = content;
                            firstChunk = false;
                            firstChunkReceived = true;
                        }
                        else
                        {
                            aiMessage.Text += content;
                        }

                        StateHasChanged();
                        await StreamScrollAsync();
                    }

                    // Final metrics (done == true)
                    if (chunk is ChatDoneResponseStream done)
                    {
                        loadDurationNs = done.LoadDuration;
                        evalDurationNs = done.EvalDuration;
                        evalCount = done.PromptEvalCount + done.EvalCount;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                aiMessage.Text = aiMessage.Text == "..."
                    ? L["Chat.ResponseTimeout"]
                    : aiMessage.Text + L["Chat.ResponseCancelled"];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro inesperado durante o streaming (OllamaSharp).");
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

                // ----- Benchmark (exactly the same as before) -----
                if (evalCount > 0 && evalDurationNs > 0)
                {
                    try
                    {
                        if (BenchmarkRepo != null)
                        {
                            if (_currentPromptId == 0)
                                _currentPromptId = await BenchmarkRepo.CreatePromptAsync(userPrompt, temperature);

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

                _isThinking = false;
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                StateHasChanged();
                await ForceScrollToBottomAsync();
            }
        }

        private async Task SendMessageWithOllamaFastAsync()
        {
            if (string.IsNullOrWhiteSpace(_currentMessage) || _isThinking) return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var userPrompt = _currentMessage;

            // --- UI: add user message ---
            _messages.Add(new Models.DTO.ChatMessage
            {
                User = L["Chat.UserDisplayName"],
                Text = userPrompt,
                IsCurrentUser = true
            });
            _currentMessage = string.Empty;
            _inputKey++;
            _isThinking = true;
            StateHasChanged();
            await ForceScrollToBottomAsync();

            // --- AI placeholder ---
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

            // --- Timer UI ---
            bool firstChunkReceived = false;
            var timer = new System.Threading.Timer(_ =>
            {
                if (firstChunkReceived || !_isThinking) return;
                var elapsed = stopwatch.Elapsed;
                aiMessage.ElapsedTime = elapsed.TotalSeconds < 10
                    ? $"{elapsed.TotalSeconds:F2}s"
                    : $"{elapsed.TotalSeconds:F1}s";
                _ = InvokeAsync(StateHasChanged);
            }, null, 0, 150);

            _cts = new CancellationTokenSource();

            long loadDurationNs = 0;
            long evalDurationNs = 0;
            int evalCount = 0;

            try
            {
                // ---------- Build history ----------
                var historyPayload = new List<OllamaSharp.Models.Chat.Message>();

                string systemInstructions = await PromptFilesService.GetPromptFileContentAsync("system-prompt.txt") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(systemInstructions))
                {
                    historyPayload.Add(new OllamaSharp.Models.Chat.Message
                    {
                        Role = OllamaSharp.Models.Chat.ChatRole.System,
                        Content = systemInstructions
                    });
                }

                foreach (var msg in _messages)
                {
                    if (string.IsNullOrWhiteSpace(msg.Text) || msg.Text == "...") continue;
                    if (msg.Text.StartsWith(L["Chat.WelcomeMessage"], StringComparison.OrdinalIgnoreCase)) continue;

                    var role = msg.IsCurrentUser
                        ? OllamaSharp.Models.Chat.ChatRole.User
                        : OllamaSharp.Models.Chat.ChatRole.Assistant;

                    if (historyPayload.Count == 0 || historyPayload[^1].Role != role)
                    {
                        historyPayload.Add(new OllamaSharp.Models.Chat.Message
                        {
                            Role = role,
                            Content = msg.Text
                        });
                    }
                    else
                    {
                        historyPayload[^1].Content += "\n" + msg.Text;
                    }
                }

                if (historyPayload.Count > 0 && historyPayload[^1].Role == OllamaSharp.Models.Chat.ChatRole.Assistant)
                    historyPayload.RemoveAt(historyPayload.Count - 1);

                if (historyPayload.Count == 0 || historyPayload[^1].Role != OllamaSharp.Models.Chat.ChatRole.User)
                {
                    _isThinking = false;
                    _messages.Remove(aiMessage);
                    StateHasChanged();
                    return;
                }

                // ---------- Temperature & token limits ----------
                double temperature = ChatMeasureTemperature.ObterTemperaturaRecomendada(userPrompt);
                int baseTokens = _gpuReport?.FitsInGpu == true ? 1800 : 1200;
                int maxTokens = temperature switch
                {
                    <= 0.25 => (int)(baseTokens * 1.15),
                    >= 0.75 => (int)(baseTokens * 0.90),
                    _ => baseTokens
                };

                // ---------- HttpClient otimizado ----------
                var handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                    MaxConnectionsPerServer = 10
                };

                using var fastClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri("http://localhost:11434"),
                    Timeout = Timeout.InfiniteTimeSpan
                };

                var ollama = new OllamaApiClient(fastClient, ModelName);

                var request = new ChatRequest
                {
                    Model = ModelName,
                    Messages = historyPayload,
                    Stream = true,
                    Options = new RequestOptions
                    {
                        Temperature = (float)Math.Round(temperature, 2),
                        NumPredict = maxTokens,
                        RepeatPenalty = 1.1f,
                        TopK = temperature <= 0.25 ? 40 : 600,
                        TopP = temperature <= 0.30f ? 0.85f : 0.92f
                    }
                };

                // ---------- StringBuilder para acelerar concatenação ----------
                var sb = new System.Text.StringBuilder(4096);

                bool firstChunk = true;

                await foreach (var chunk in ollama.ChatAsync(request, _cts.Token))
                {
                    if (chunk?.Message?.Content is { Length: > 0 } content)
                    {
                        if (firstChunk)
                        {
                            sb.Append(content);
                            aiMessage.Text = sb.ToString();
                            firstChunk = false;
                            firstChunkReceived = true;
                        }
                        else
                        {
                            sb.Append(content);
                            aiMessage.Text = sb.ToString();
                        }

                        // UI menos agressiva
                        if (sb.Length % 512 == 0)
                        {
                            StateHasChanged();
                            await StreamScrollAsync();
                        }
                    }

                    if (chunk is ChatDoneResponseStream done)
                    {
                        loadDurationNs = done.LoadDuration;
                        evalDurationNs = done.EvalDuration;
                        evalCount = done.PromptEvalCount + done.EvalCount;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                aiMessage.Text = aiMessage.Text == "..."
                    ? L["Chat.ResponseTimeout"]
                    : aiMessage.Text + L["Chat.ResponseCancelled"];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro inesperado durante o streaming (OllamaFast).");
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

                // ----- Benchmark -----
                if (evalCount > 0 && evalDurationNs > 0)
                {
                    try
                    {
                        if (BenchmarkRepo != null)
                        {
                            if (_currentPromptId == 0)
                                _currentPromptId = await BenchmarkRepo.CreatePromptAsync(userPrompt, temperature);

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

                _isThinking = false;
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                StateHasChanged();
                await ForceScrollToBottomAsync();
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