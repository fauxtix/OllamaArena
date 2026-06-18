using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaFluentUIChat.Components.Pages.Components;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Text;
using System.Text.Json;
using static OllamaFluentUIChat.Models.DTO.OllamaModels;

namespace OllamaFluentUIChat.Components.Pages
{
    public partial class Chat : IDisposable
    {
        [Inject] public IOllamaGpuService? GpuService { get; set; }
        [Inject] public IDialogService? DialogService { get; set; }
        [Inject] public IBenchmarkRepository? BenchmarkRepo { get; set; }

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

        // Token para conseguir cancelar o HttpClient a meio do streaming
        private CancellationTokenSource? _cts;

        private string _modelName = "phi4-mini:latest";

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
                _ = OnModelChangedAsync(); // dispara atualização GPU + gravação
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
                    // 1) Carregas a lista de modelos da cache
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
                catch { }

                await JS.InvokeVoidAsync("chatInput.attachHandlers", chatInputRef, _dotNetRef);
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

            // Inicia o cronómetro da App para telemetria (.NET)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var userPrompt = _currentMessage;

            // 1. Adiciona o Prompt do utilizador à UI
            _messages.Add(new Models.DTO.ChatMessage { User = "Tu", Text = userPrompt, IsCurrentUser = true });

            _currentMessage = string.Empty;
            _inputKey++;
            _isThinking = true;

            StateHasChanged();
            await ForceScrollToBottomAsync();

            // 2. Cria o marcador de posição para a resposta da IA
            var aiMessage = new Models.DTO.ChatMessage { User = "Ollama", Text = "...", IsCurrentUser = false };
            _messages.Add(aiMessage);

            StateHasChanged();
            await ForceScrollToBottomAsync();

            _cts = new CancellationTokenSource();

            // Variáveis para armazenar as métricas oficiais do Ollama
            long loadDurationNs = 0;
            long evalDurationNs = 0;
            int evalCount = 0;

            try
            {
                // Criamos a lista de mensagens usando a nossa classe explícita
                var historyPayload = new List<OllamaChatMessage>();

                // 1. Adiciona o System Prompt obrigatório
                historyPayload.Add(new OllamaChatMessage
                {
                    Role = "system",
                    Content = "Respond in Portuguese. Do NOT use chain-of-thought. Do NOT reveal internal reasoning, planning, or thinking steps. Provide ONLY the final answer, concise and direct."
                });

                // 2. Mapeia o histórico da UI filtrando de forma robusta para o formato do Ollama
                foreach (var msg in _messages)
                {
                    // Ignora placeholders de processamento e mensagens do sistema locais
                    if (string.IsNullOrWhiteSpace(msg.Text) || msg.Text == "...") continue;
                    if (msg.Text.StartsWith("Olá!", StringComparison.OrdinalIgnoreCase)) continue;

                    string roleAtual = msg.IsCurrentUser ? "user" : "assistant";

                    // Evita duplicar papéis seguidos no payload (o Ollama exige estritamente user -> assistant -> user)
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
                        // Se o mesmo papel se repetir consecutivamente, junta o texto na última mensagem
                        historyPayload[^1].Content += "\n" + msg.Text;
                    }
                }

                // GARANTIA CRÍTICA: O histórico para o /api/chat TEM de terminar sempre com uma mensagem do 'user'
                if (historyPayload.Count > 0 && historyPayload[^1].Role == "assistant")
                {
                    historyPayload.RemoveAt(historyPayload.Count - 1);
                }

                // Se por algum motivo o histórico ficou vazio (ex: falha de mapeamento), aborta de forma segura
                if (historyPayload.Count == 0 || historyPayload[^1].Role != "user")
                {
                    _isThinking = false;
                    _messages.Remove(aiMessage); // Remove o placeholder "..."
                    StateHasChanged();
                    return;
                }

                if (string.IsNullOrWhiteSpace(ModelName))
                {
                    ModelName = "phi4-mini:latest";
                }

                var payload = new OllamaChatPayload
                {
                    Model = ModelName,
                    Messages = historyPayload,
                    Stream = true,
                    Options = new Dictionary<string, object>
                    {
                        { "temperature", 0.7 }
                    }
                };

                var json = JsonSerializer.Serialize(payload);


                using var request = new HttpRequestMessage(HttpMethod.Post, OllamaEndpoint);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                var buffer = new byte[4096];
                int bytesRead;

                bool firstChunk = true;

                if(_cts is null)
                {
                    _cts = new CancellationTokenSource();
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

                            // Extração do conteúdo de texto da resposta
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
                                    loadDurationNs = metrics.LoadDuration;
                                    evalDurationNs = metrics.EvalDuration;
                                    evalCount = metrics.EvalCount;
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            continue;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                aiMessage.Text = aiMessage.Text == "..." ? "⏱️ O tempo de resposta expirou." : aiMessage.Text + " *(Cancelado)*";
            }
            catch (Exception ex)
            {
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
                        System.Diagnostics.Debug.WriteLine($"[SQLITE ERROR] Falha ao gravar dados: {dbEx.Message}");
                    }
                }
                else
                {
                    _currentPromptId = 0;
                    System.Diagnostics.Debug.WriteLine($"[BENCHMARK] Teste descartado para o modelo {ModelName}. Prompt incompleto.");
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

        private async Task OnModelChangedAsync()
        {
            try
            {
                // Guardar modelo selecionado
                await JS.InvokeVoidAsync("localStorage.setItem", "ollama_model", ModelName);

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
            if (string.IsNullOrEmpty(content)) return "";

            if (content == "...")
            {
                return "<div class='typing-dots'><span></span><span></span><span></span></div>";
            }

            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .Build();

            var html = Markdown.ToHtml(content, pipeline);
            html = html.TrimEnd('\n', '\r', ' ');

            return html.Replace("<p>", "<div>").Replace("</p>", "</div>");
        }

        private async Task<string> BuscarContextoWebAsync(string query)
        {
            try
            {
                using var client = new HttpClient();
                // Forçamos o User-Agent para o DuckDuckGo aceitar o pedido
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                // Pedido à versão leve do DuckDuckGo
                var html = await client.GetStringAsync($"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}");

                // Um parsing simples de texto para extrair os primeiros resultados relevantes
                // Nota: Isto é uma solução leve. Para produção, usar Bing Search ou Google API é mais robusto.
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                var snippets = doc.DocumentNode.SelectNodes("//a[@class='result__snippet']");
                if (snippets == null) return "Não foram encontrados dados recentes sobre este assunto.";

                var sb = new StringBuilder();
                foreach (var snippet in snippets.Take(3)) // Pega nos 3 primeiros resultados
                {
                    sb.AppendLine(snippet.InnerText.Trim());
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[Erro ao aceder à internet: {ex.Message}]";
            }
        }

        public void Dispose()
        {
            try
            {
                _cts?.Dispose();
                _dotNetRef?.Dispose(); // Liberta a referência que o JS tem do C#
            }
            catch { }
        }
    }
}