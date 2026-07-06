using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaFluentUIChat.Components.Pages.Components;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.PromptTemplates;
using OllamaFluentUIChat.Services;
using OllamaFluentUIChat.Services.Helpers;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
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
        [Inject] public HttpClient? _httpClient { get; set; }
        [Inject] public ILogger<App>? _logger { get; set; }

        private List<Models.DTO.ChatMessage> _messages = new();
        private string _currentMessage = string.Empty;
        private int _inputKey = 0;
        private bool _isThinking = false;

        private GpuStatus? _gpuReport;

        private ElementReference messagesDiv;
        private bool userAtBottom = true;

        protected bool GPUDialogVisibility = false;

        private HistoryPanel? historyPanel;

        private GpuInfoDialog? gpuDialog;

        private CancellationTokenSource? _cts;

        private string _modelName = "";

        private DotNetObjectReference<Chat>? _dotNetRef;
        private ElementReference chatInputRef;

        private int _currentPromptId;

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

        protected override void OnInitialized()
        {
            _messages.Add(new Models.DTO.ChatMessage { User = "Ollama", Text = "Olá! Como posso ajudar?" });
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _dotNetRef = DotNetObjectReference.Create(this);

                try
                {
                    _logger?.LogInformation("Carregando lista de modelos da cache...");
                    var storedList = await JS.InvokeAsync<string>("localStorage.getItem", "ollama_models");
                    if (!string.IsNullOrEmpty(storedList))
                    {
                        _models = JsonSerializer.Deserialize<List<string>>(storedList) ?? new();
                    }

                    if (string.IsNullOrEmpty(ModelName))
                    {
                        if (_models.Contains(_modelName))
                        {
                            ModelName = _modelName;
                        }
                        else if (_models.Any())
                        {
                            ModelName = _models.First();
                        }
                    }

                    var allModels = await GpuService!.GetLocalModelsAsync();
                    var currentModelDetails = allModels?.Models?
                        .FirstOrDefault(m =>
                            m.Name.Equals(ModelName, StringComparison.OrdinalIgnoreCase) ||
                            m.Model.Equals(ModelName, StringComparison.OrdinalIgnoreCase));

                    if (currentModelDetails != null)
                        _gpuReport = GpuService.CheckGpuCompatibility(currentModelDetails.SizeInBytes);

                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Erro detetado no OnAfterRenderAsync do Chat");
                }

                await JS.InvokeVoidAsync("chatInput.attachHandlers", chatInputRef, _dotNetRef);
                _logger?.LogInformation("Chat component initialized.");
            }
        }
        private async Task ShowGpuInfoDialogAsync()
        {
            if (DialogService == null) return;

            string textoInformativo =
                "O que significa 'CPU Fallback'?\n\n" +
                "A sua placa gráfica (GPU) não tem memória de vídeo (VRAM) suficiente livre para carregar este modelo de Inteligência Artificial por completo.\n\n" +
                "O que vai acontecer agora?\n" +
                "• O Ollama vai dividir o modelo, enviando o processamento para a memória RAM normal e para a CPU.\n" +
                "• O chat VAI FUNCIONAR e responderá corretamente.\n" +
                "• No entanto, a velocidade de resposta será bastante mais lenta (as letras aparecem mais devagar), porque a CPU não foi desenhada para a carga matemática dos LLMs.\n\n" +
                "Dica: Para velocidades máximas, tente usar modelos mais pequenos (como versões '2B' ou 'mini').";

            var parameters = new DialogParameters()
            {
                Title = "Informação do Sistema",
                PrimaryAction = "Fechar",
                PrimaryActionEnabled = true,
                SecondaryAction = null,
                Width = "500px"
            };

            await DialogService.ShowDialogAsync<MessageBox>(textoInformativo, parameters);
        }

        private void OpenHistory()
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

            _messages.Add(new Models.DTO.ChatMessage { User = "Tu", Text = userPrompt, IsCurrentUser = true });

            _currentMessage = string.Empty;
            _inputKey++;
            _isThinking = true;

            StateHasChanged();
            await ForceScrollToBottomAsync();

            var aiMessage = new Models.DTO.ChatMessage { User = "Ollama", Text = "...", IsCurrentUser = false };
            _messages.Add(aiMessage);

            StateHasChanged();
            await ForceScrollToBottomAsync();

            _cts = new CancellationTokenSource();

            long loadDurationNs = 0;
            long evalDurationNs = 0;
            long promptEvalCount = 0;
            int evalCount = 0;

            try
            {
                var historyPayload = new List<OllamaChatMessage>();
                var systemInstructions = ChatInstructionsPrompt.GetSystemInstructionPrompt();


                historyPayload.Add(new OllamaChatMessage
                {
                    Role = "system",
                    Content = systemInstructions
                });

                foreach (var msg in _messages)
                {
                    if (string.IsNullOrWhiteSpace(msg.Text) || msg.Text == "...") continue;
                    if (msg.Text.StartsWith("Olá!", StringComparison.OrdinalIgnoreCase)) continue;

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

                // Se por algum motivo o histórico ficou vazio (ex: falha de mapeamento), aborta de forma segura
                if (historyPayload.Count == 0 || historyPayload[^1].Role != "user")
                {
                    _isThinking = false;
                    _messages.Remove(aiMessage);
                    StateHasChanged();
                    return;
                }

                if (string.IsNullOrWhiteSpace(ModelName))
                {
                    ModelName = "phi4-mini:latest";
                }

                int maxTokens = _gpuReport?.FitsInGpu == true ? 1500 : 800;
                var payload = new OllamaChatPayload
                {
                    Model = ModelName,
                    Messages = historyPayload,
                    Stream = true,
                    Options = new Dictionary<string, object>
                    {
                        { "temperature", 0.2 },
                        { "num_predict", maxTokens }
                    }
                };
                var json = JsonSerializer.Serialize(payload);


                using var request = new HttpRequestMessage(HttpMethod.Post, OllamaEndpoint);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                if (_httpClient == null)
                {
                    _logger?.LogError("HttpClient is null. Cannot send request to Ollama API.");
                    return;
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                var buffer = new byte[4096];
                int bytesRead;

                bool firstChunk = true;

                if (_cts is null)
                {
                    //_cts = new CancellationTokenSource();
                    return;
                }

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token)) > 0)
                {
                    var chunkString = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // O Ollama pode enviar múltiplos objetos JSON separados por quebras de linha
                    var lines = chunkString.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var singleLine in lines)
                    {
                        if (string.IsNullOrWhiteSpace(singleLine)) continue;

                        try
                        {
                            using var doc = JsonDocument.Parse(singleLine);
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
                                }
                                else
                                {
                                    aiMessage.Text += chunkText;
                                }

                                StateHasChanged();
                                await StreamScrollAsync();
                            }

                            // Captura as métricas finais quando o Ollama terminar (done = true)
                            if (root.TryGetProperty("done", out var doneProp) && doneProp.GetBoolean() == true)
                            {
                                var metrics = JsonSerializer.Deserialize<OllamaMetrics>(singleLine);
                                if (metrics != null)
                                {
                                    int totalTokens = metrics.PromptEvalCount + metrics.EvalCount;
                                    loadDurationNs = metrics.LoadDuration;
                                    evalDurationNs = metrics.EvalDuration;
                                    evalCount = totalTokens; // metrics.EvalCount;
                                }
                            }
                        }
                        catch (JsonException jex)
                        {
                            aiMessage.Text += $"\n[Erro ao processar resposta do Ollama: {jex.Message}]";
                            _logger?.LogWarning("Failed to parse JSON chunk from Ollama API: {Chunk}... continuing the process", singleLine);
                            continue;
                        }
                        catch (OperationCanceledException ocEx)
                        {
                            _logger?.LogError(ocEx, "O streaming da resposta foi cancelado.");
                            aiMessage.Text = aiMessage.Text == "..." ? "⏱️ O tempo de resposta expirou." : aiMessage.Text + " *(Cancelado)*";
                        }

                        catch (Exception ex)
                        {
                            aiMessage.Text += $"\n[Erro inesperado: {ex.Message}]";
                            _logger?.LogError(ex, "Unexpected error while processing chunk from Ollama API.");
                        }
                    }
                }
            }
            catch (OperationCanceledException ocEx)
            {
                _logger?.LogError(ocEx, "O streaming da resposta foi cancelado.");
                aiMessage.Text = aiMessage.Text == "..." ? "⏱️ O tempo de resposta expirou." : aiMessage.Text + " *(Cancelado)*";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro inesperado durante o streaming da resposta.");
                aiMessage.Text = $"❌ Erro inesperado: {ex.Message}";
            }
            finally
            {
                stopwatch.Stop();

                // 4. Gravação na Base de Dados SQLite
                if (evalCount > 0 && evalDurationNs > 0)
                {
                    try
                    {
                        if (BenchmarkRepo != null)
                        {
                            if (_currentPromptId == 0)
                            {
                                _currentPromptId = await BenchmarkRepo.CreatePromptAsync(userPrompt);
                            }

                            double loadMs = loadDurationNs / 1000000.0;
                            double evalMs = evalDurationNs / 1000000.0;
                            double tokensPerSecond = evalCount / (evalMs / 1000.0);

                            var newResponse = new BenchmarkResponse
                            {
                                PromptId = _currentPromptId,
                                ModeloNome = ModelName,
                                TextoResposta = aiMessage.Text,
                                TokensPorSegundo = Math.Round(tokensPerSecond, 1),
                                TempoPuroMs = Math.Round(evalMs, 0),
                                TempoCargaMs = Math.Round(loadMs, 0),
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
                // Guardar modelo selecionado
                await SaveModel();
                // Recalcular GPU compatibility
                var allModels = await GpuService!.GetLocalModelsAsync();

                var currentModelDetails = allModels?.Models?
                    .FirstOrDefault(m =>
                        m.Name.Equals(ModelName, StringComparison.OrdinalIgnoreCase) ||
                        m.Model.Equals(ModelName, StringComparison.OrdinalIgnoreCase));

                if (currentModelDetails != null)
                    _gpuReport = GpuService.CheckGpuCompatibility(currentModelDetails.SizeInBytes);

                StateHasChanged();
            }
            catch { }
        }

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

            // --- AÇÃO ESPECIAL PARA O BENCHMARK: Descarregar o modelo da VRAM ---
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
                Text = "Olá! Como posso ajudar-te hoje?"
            });

            StateHasChanged();
        }


        private string FormatMessage(string content)
        {
            return MessageFormatter.FormatMessagePROPlus(content);
            //return CommonService.FormatMessage(content);
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
                            string realUrl = queryParams["uddg"];
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
        public void Dispose()
        {
            try
            {
                _cts?.Dispose();
                _dotNetRef?.Dispose();
            }
            catch { }
        }
    }
}