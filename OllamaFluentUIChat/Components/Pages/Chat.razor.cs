using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaFluentUIChat.Components.Layout;
using OllamaFluentUIChat.Components.Pages.Components;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Text;
using System.Text.Json;
using static OllamaFluentUIChat.Models.DTO.OllamaModels;

namespace OllamaFluentUIChat.Components.Pages
{
    public partial class Chat
    {

        [Inject] public IOllamaGpuService? GpuService { get; set; }
        [Inject] public IDialogService? DialogService { get; set; }

        private List<ChatMessage> _messages = new();
        private string _currentMessage = string.Empty;
        private int _inputKey = 0;
        private bool _isThinking = false;

        private GpuStatus? _gpuReport;

        private ElementReference messagesDiv;
        private bool userAtBottom = true;

        protected bool GPUDialogVisibility = false;

        private HistoryPanel? historyPanel;

        private GpuInfoDialog gpuDialog;

        // Token para conseguir cancelar o HttpClient a meio do streaming
        private CancellationTokenSource? _cts;

        private string ModelName { get; set; } = "phi4-mini:latest";

        private const string OllamaEndpoint = "http://localhost:11434/api/chat";
        protected override void OnInitialized()
        {
            _messages.Add(new ChatMessage { User = "Ollama", Text = "Olá! Como posso ajudar?" });
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    var savedModel = await JS.InvokeAsync<string>("localStorage.getItem", "ollama_model");
                    if (!string.IsNullOrEmpty(savedModel))
                    {
                        ModelName = savedModel;
                    }

                    var allModels = await GpuService.GetLocalModelsAsync();

                    var currentModelDetails = allModels?.Models?
                        .FirstOrDefault(m => m.Name.Equals(ModelName, StringComparison.OrdinalIgnoreCase)
                                          || m.Model.Equals(ModelName, StringComparison.OrdinalIgnoreCase));

                    if (currentModelDetails != null)
                    {
                        _gpuReport = GpuService.CheckGpuCompatibility(currentModelDetails.SizeInBytes);
                    }

                    StateHasChanged(); 

                }
                catch { }
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

            // Esta chamada contorna o bug do ShowInfoAsync e usa o provider que já tens no Layout
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

            // 1. Adiciona a mensagem do Utilizador ao ecrã
            _messages.Add(new ChatMessage
            {
                User = "Tu",
                Text = userPrompt,
                IsCurrentUser = true
            });

            _currentMessage = string.Empty;
            _inputKey++;
            _isThinking = true;

            StateHasChanged();
            await ForceScrollToBottomAsync();

            // 2. Cria o marcador de posição para a resposta da IA
            var aiMessage = new ChatMessage
            {
                User = "Ollama",
                Text = "...",
                IsCurrentUser = false
            };
            _messages.Add(aiMessage);

            StateHasChanged();
            await ForceScrollToBottomAsync();

            _cts = new CancellationTokenSource();

            try
            {
                var historyPayload = new List<object>();

                historyPayload.Add(new
                {
                    role = "system",
                    content = "Respond in Portuguese. Do NOT use chain-of-thought. Do NOT reveal internal reasoning, planning, or thinking steps. Provide ONLY the final answer, concise and direct. If an explanation is needed, keep it short and high-level without exposing the reasoning process" 
                });

                foreach (var msg in _messages.Where(m => m.Text != "..."))
                {
                    historyPayload.Add(new
                    {
                        role = msg.IsCurrentUser ? "user" : "assistant",
                        content = msg.Text
                    });
                }

                var payload = new
                {
                    model = ModelName,
                    messages = historyPayload,
                    temperature = 0.7,
                    stream = true
                };

                var json = JsonSerializer.Serialize(payload);

                using var request = new HttpRequestMessage(HttpMethod.Post, OllamaEndpoint);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                using var reader = new StreamReader(stream);

                bool firstChunk = true;
                string? line;

                while ((line = await reader.ReadLineAsync(_cts.Token)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    string? chunkText = null;

                    // 1. message.content (phi4-mini, qwen)
                    if (root.TryGetProperty("message", out var msgProp))
                    {
                        if (msgProp.TryGetProperty("content", out var contentProp))
                        {
                            var text = contentProp.GetString();

                            // ignorar conteúdo vazio
                            if (!string.IsNullOrWhiteSpace(text))
                                chunkText = text;
                        }

                        // ignorar completamente o "thinking"
                        // NÃO colocar nada aqui
                    }

                    // 2. delta (mistral)
                    if (chunkText == null && root.TryGetProperty("delta", out var deltaProp))
                    {
                        var text = deltaProp.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            chunkText = text;
                    }

                    // 3. response (mistral, qwen)
                    if (chunkText == null && root.TryGetProperty("response", out var respProp))
                    {
                        var text = respProp.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            chunkText = text;
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
                }
            }
            catch (OperationCanceledException)
            {
                if (aiMessage.Text == "...")
                {
                    aiMessage.Text = "⏱️ O tempo de resposta expirou. O modelo demorou demasiado tempo ou a rede falhou.";
                }
                else
                {
                    aiMessage.Text += " *(Geração interrompida pelo utilizador)*";
                }
            }
            catch (HttpRequestException ex)
            {
                aiMessage.Text = $"❌ Erro de Ligação (Socket/Rede): Não foi possível comunicar com o Ollama. Detalhes: {ex.Message}";
            }
            catch (Exception ex)
            {
                aiMessage.Text = $"⚠️ Erro inesperado: {ex.Message}";
            }
            finally
            {
                stopwatch.Stop();

                await JS.InvokeVoidAsync("ollamaHistory.addEntry", new
                {
                    model = ModelName,
                    prompt = userPrompt,
                    date = DateTime.UtcNow,
                    durationMs = stopwatch.ElapsedMilliseconds
                });

                _isThinking = false;
                _cts?.Dispose();
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

        private void NewChat()
        {
            try { _cts?.Cancel(); } catch { }
            _messages.Clear();
            _currentMessage = string.Empty;
            _isThinking = false;

            _messages.Add(new ChatMessage
            {
                User = "Ollama",
                Text = "Olá! Como posso ajudar-te hoje?"
            });

            StateHasChanged();
        }

        private string FormatMistralMessage(string content, bool showCursor)
        {
            if (content == "...")
            {
                return "<div class='typing-dots'><span></span><span></span><span></span></div>";
            }

            var escaped = System.Net.WebUtility.HtmlEncode(content).Replace("\n", "<br>");
            //if (showCursor) escaped += "<span class='cursor'>|</span>";
            return escaped;
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

    }
}