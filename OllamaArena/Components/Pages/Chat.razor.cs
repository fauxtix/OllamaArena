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
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using static OllamaArena.Models.DTO.OllamaModels;

namespace OllamaArena.Components.Pages
{
    /// <summary>
    /// Página de conversa com o Ollama. A página orquestra a UI e nada mais: o streaming
    /// vive em <see cref="IChatStreamClient"/>, a persistência em <see cref="IChatTurnWriter"/>
    /// e a preparação do pedido em <see cref="ChatComposerService"/>.
    /// </summary>
    public partial class Chat : IAsyncDisposable
    {
        [Inject] public IOllamaGpuService? GpuService { get; set; }
        [Inject] public IDialogService? DialogService { get; set; }
        [Inject] public IConversationRepository? ConversationRepo { get; set; }
        [Inject] public IChatTurnWriter ChatTurnWriter { get; set; } = default!;
        [Inject] public IChatStreamClient ChatStream { get; set; } = default!;
        [Inject] public PromptFilesService PromptFilesService { get; set; } = default!;
        [Inject] public ChatComposerService ChatComposer { get; set; } = default!;
        [Inject] public ILogger<Chat> _logger { get; set; } = default!;
        [Inject] public IStringLocalizer<SharedResources> L { get; set; } = default!;
        [Inject] public IOptions<OllamaOptions> OllamaSettings { get; set; } = default!;

        private readonly List<ChatMessage> _messages = new();
        private readonly List<string> _models = new();
        private readonly SemaphoreSlim _modelChangeGate = new(1, 1);

        private string _currentMessage = string.Empty;
        private bool _isThinking;

        private GpuStatus? _gpuReport;
        private long _modelSizeInBytes;

        private int _contextLength;
        private int _contextUsedTokens;
        private bool _showContextBar;

        private bool _modelSupportsThinking;
        private string _modelSupportsThinkingFor = string.Empty;

        private ElementReference messagesDiv;
        private bool userAtBottom = true;

        protected bool GPUDialogVisibility = false;

        private HistoryPanel? historyPanel;
        private GpuInfoDialog? gpuDialog;

        /// <summary>
        /// Dono único do cancelamento do turno em curso. Criado e descartado por
        /// <see cref="SendMessage"/>; os outros pontos só cancelam. Antes o mesmo
        /// CancellationTokenSource era descartado por <c>CancelRequest</c> e por um
        /// <c>finally</c>, e o reader do stream continuava a usar o token já libertado.
        /// </summary>
        private CancellationTokenSource? _turnCts;

        private string _modelName = string.Empty;

        private DotNetObjectReference<Chat>? _dotNetRef;
        private ElementReference chatInputRef;

        private bool isLoadingModels = false;

        /// <summary>
        /// Sobreposição de erro quando o Ollama não responde. Antes este campo nunca
        /// passava a <c>true</c>: o overlay estava no markup mas era código morto.
        /// </summary>
        private bool showOllamaError;

        /// <summary>Prompt de benchmark reutilizado entre turnos da mesma conversa.</summary>
        private int _currentPromptId;

        /// <summary>0 enquanto a conversa ainda não foi criada na base de dados.</summary>
        private int _conversationId;

        private bool _disposed;

        /// <summary>Temperatura em vigor no último turno, mostrada no cartão de métricas.</summary>
        private double temperature;

        private string ModelName
        {
            get => _modelName;
            set
            {
                if (string.Equals(_modelName, value, StringComparison.Ordinal)) return;

                _modelName = value;

                // Serializado: o seletor de modelo pode disparar várias vezes seguidas e
                // antes cada uma corria uma Task independente que escrevia em campos
                // partilhados (tamanho, GPU, contexto) sem coordenação.
                _ = OnModelChangedAsync();
            }
        }

        private int ContextProgressValue => Math.Min(_contextUsedTokens, _contextLength);
        private int ContextRemaining => Math.Max(0, _contextLength - _contextUsedTokens);
        private bool ContextNearLimit => _contextLength > 0 && (double)_contextUsedTokens / _contextLength >= 0.9;

        protected override void OnInitialized()
        {
            _messages.Add(new ChatMessage { User = "Ollama", Text = L["Chat.WelcomeMessage"] });
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender || _disposed) return;

            await GetModelsInfoAsync();

            _dotNetRef = DotNetObjectReference.Create(this);

            try
            {
                await JS.InvokeVoidAsync("chatInput.attachHandlers", chatInputRef, _dotNetRef);
            }
            catch (JSException ex)
            {
                // Navegador navigated away ou interop ainda não pronto: não é fatal.
                _logger.LogWarning(ex, "Não foi possível anexar os handlers de input do chat.");
            }
        }

        // ============================================================
        //  MODELOS
        // ============================================================

        private async Task GetModelsInfoAsync()
        {
            try
            {
                isLoadingModels = true;
                StateHasChanged();

                var localModels = await LoadModelsFromLocalStorage();

                var allModels = (await GpuService!.GetLocalModelsAsync())?.Models;

                if (allModels is not { Count: > 0 })
                {
                    _logger.LogWarning("Ollama não devolveu nenhum modelo.");
                    showOllamaError = true;
                    return;
                }

                showOllamaError = false;

                if (localModels.Count == 0)
                {
                    _models.Clear();
                    _models.AddRange(allModels.Select(m => m.Model));
                    await SaveModelsToLocalStorage();
                }
                else
                {
                    _models.Clear();
                    _models.AddRange(localModels);
                }

                ModelName = await ResolverModeloPreferidoAsync(_models);

                await AplicarMetadadosModeloAsync(ModelName);
            }
            catch (HttpRequestException ex)
            {
                // O daemon do Ollama está em baixo: é o caso que o overlay descreve.
                _logger.LogError(ex, "Ollama inacessível: {Message}", ex.Message);
                showOllamaError = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar a lista de modelos do Ollama.");
            }
            finally
            {
                isLoadingModels = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        /// <summary>
        /// Carrega tamanho, compatibilidade GPU e limite de contexto do modelo. Antes esta
        /// lógica estava copiada em três sítios (arranque, troca de modelo e primeiro envio),
        /// com oRespectivo bloco de procura repetido.
        /// </summary>
        private async Task AplicarMetadadosModeloAsync(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return;

            try
            {
                var allModels = (await GpuService!.GetLocalModelsAsync())?.Models;

                var details = allModels?.FirstOrDefault(m =>
                    m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                    m.Model.Equals(modelName, StringComparison.OrdinalIgnoreCase));

                if (details is not null)
                {
                    _modelSizeInBytes = details.SizeInBytes;
                    _gpuReport = await GpuService.CheckGpuCompatibility(details.SizeInBytes);
                }

                _contextLength = await GetContextLengthAsync(modelName);
                _contextUsedTokens = 0;
                _showContextBar = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter metadados do modelo {ModelName}.", modelName);
            }
        }

        private async Task SaveModelsToLocalStorage()
        {
            await JS.InvokeVoidAsync("localStorage.setItem", "ollamaModels", JsonSerializer.Serialize(_models));
        }

        private async Task<List<string>> LoadModelsFromLocalStorage()
        {
            try
            {
                var modelsJson = await JS.InvokeAsync<string?>("localStorage.getItem", "ollamaModels");

                return string.IsNullOrEmpty(modelsJson)
                    ? []
                    : JsonSerializer.Deserialize<List<string>>(modelsJson) ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Cache local de modelos inválido; a recarregar do Ollama.");
                return [];
            }
        }

        // O modelo escolhido pelo utilizador é gravado em "ollama_model"; se já não existir
        // (modelo removido com ollama rm), cai no primeiro da lista.
        private async Task<string> ResolverModeloPreferidoAsync(List<string> modelos)
        {
            if (modelos.Count == 0) return string.Empty;

            try
            {
                var salvo = await JS.InvokeAsync<string?>("localStorage.getItem", "ollama_model");

                if (!string.IsNullOrWhiteSpace(salvo)
                    && modelos.Any(m => string.Equals(m, salvo, StringComparison.OrdinalIgnoreCase)))
                {
                    return salvo;
                }
            }
            catch (JSException)
            {
                // localStorage indisponível: usa o primeiro modelo.
            }

            return modelos[0];
        }

        private async Task OnModelChangedAsync()
        {
            await _modelChangeGate.WaitAsync();

            try
            {
                await SaveModel();

                await AplicarMetadadosModeloAsync(ModelName);

                _modelSupportsThinkingFor = string.Empty;
                _currentPromptId = 0;

                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao trocar de modelo para {ModelName}.", ModelName);
            }
            finally
            {
                _modelChangeGate.Release();
            }
        }

        private async Task SaveModel()
        {
            if (string.IsNullOrWhiteSpace(ModelName)) return;

            await JS.InvokeVoidAsync("localStorage.setItem", "ollama_model", ModelName);
        }

        private async Task<int> GetContextLengthAsync(string modelName)
        {
            try
            {
                if (GpuService is null) return OllamaSettings.Value.DefaultContextLength;

                return await GpuService.GetRecommendedContextLengthAsync(_modelSizeInBytes, modelName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao obter o contexto de {ModelName}; a usar o por omissão.", modelName);
                return OllamaSettings.Value.DefaultContextLength;
            }
        }

        private async Task EnsureModelSupportsThinkingAsync()
        {
            try
            {
                if (GpuService is null) return;
                if (string.Equals(_modelSupportsThinkingFor, ModelName, StringComparison.Ordinal)) return;

                _modelSupportsThinking = await GpuService.ModelSupportsThinkingAsync(ModelName);
                _modelSupportsThinkingFor = ModelName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao detetar a capacidade de raciocínio de {ModelName}.", ModelName);
                _modelSupportsThinking = false;
            }
        }

        // ============================================================
        //  CICLO DE UMA MENSAGEM
        // ============================================================

        private async Task SendMessage()
        {
            if (_disposed || _isThinking || string.IsNullOrWhiteSpace(_currentMessage)) return;

            var userPrompt = _currentMessage;
            var stopwatch = Stopwatch.StartNew();

            var userMessage = new ChatMessage
            {
                User = L["Chat.UserDisplayName"],
                Text = userPrompt,
                IsCurrentUser = true
            };

            var aiMessage = new ChatMessage
            {
                User = "Ollama",
                Text = ChatMessage.PendingPlaceholder,
                IsCurrentUser = false,
                ElapsedTime = "0.00s"
            };

            _messages.Add(userMessage);
            _messages.Add(aiMessage);

            _currentMessage = string.Empty;
            await ReporDimensoesDoPromptAsync();
            _isThinking = true;

            if (_contextLength == 0)
                _contextLength = await GetContextLengthAsync(ModelName);

            _contextUsedTokens = _contextUsedTokens + ChatComposerService.EstimateTokens(userPrompt);
            _showContextBar = true;

            StateHasChanged();
            await ForceScrollToBottomAsync();

            var elapsedTimer = CriarTemporizadorElapsed(aiMessage, stopwatch);

            // Único dono do CTS: criado e descartado aqui. CancelRequest e Dispose
            // limitam-se a cancelar, nunca a descartar.
            using var cts = new CancellationTokenSource();
            _turnCts = cts;

            ChatTurn? turn = null;

            try
            {
                var preparation = await PrepararTurnoAsync(userPrompt);

                switch (preparation.Status)
                {
                    case ChatPreparationStatus.EmptyPrompt:
                        _messages.Remove(aiMessage);
                        return;

                    case ChatPreparationStatus.PromptTooLong:
                        _messages.Remove(aiMessage);
                        await MostrarPromptDemasiadoGrandeAsync(preparation);
                        return;
                }

                var prepared = preparation.Request!;
                temperature = prepared.Temperature;
                userMessage.Temperature = Math.Round(temperature, 2);

                _logger.LogInformation(
                    "Ollama começou a responder após {Seconds:F1}s (modelo {ModelName}, contexto {Context})",
                    stopwatch.Elapsed.TotalSeconds, ModelName, _contextLength);

                var stream = await ChatStream.SendAsync(
                    prepared.Payload,
                    prepared.EnableReasoning,
                    cts.Token,
                    progress => AplicarProgressoAsync(progress, aiMessage, userPrompt));

                AplicarResultadoStream(stream, aiMessage);

                var (summary, cleanText) = ChatComposerService.ExtractSummaryFromResponse(aiMessage.Text);
                if (summary is not null)
                {
                    aiMessage.Summary = summary;
                    aiMessage.Text = cleanText;
                }

                aiMessage.LanguageWarning = LanguageWarningFor(aiMessage.Text);

                turn = new ChatTurn
                {
                    UserPrompt = userPrompt,
                    AssistantText = aiMessage.Text,
                    Reasoning = aiMessage.Reasoning,
                    Summary = aiMessage.Summary,
                    Temperature = temperature,
                    ElapsedLabel = aiMessage.ElapsedTime ?? string.Empty,
                    PromptTokens = stream.PromptTokens,
                    EvalTokens = stream.EvalTokens,
                    EvalDurationNs = stream.EvalDurationNs,
                    LoadDurationNs = stream.LoadDurationNs,
                    TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                    SessionLanguage = TargetLanguageResolver.GetTargetLanguage(),
                    Completed = true
                };

                if (stream.PromptTokens + stream.EvalTokens > 0)
                {
                    _contextUsedTokens = (int)(stream.PromptTokens + stream.EvalTokens);
                    _showContextBar = true;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Streaming cancelado pelo utilizador.");

                aiMessage.Text = aiMessage.IsPending
                    ? L["Chat.ResponseTimeout"]
                    : aiMessage.Text + L["Chat.ResponseCancelled"];
            }
            catch (ChatStreamException ex)
            {
                // Falha de comunicação: preserva o que já foi recebido. Antes, qualquer
                // exceção substituía a resposta inteira por "Ocorreu um erro".
                _logger.LogError(ex, "Erro de comunicação com o Ollama durante o streaming.");
                aiMessage.Text = aiMessage.IsPending ? ex.Message : aiMessage.Text + "\n\n" + ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado durante o processing da resposta.");
                aiMessage.Text = aiMessage.IsPending
                    ? L["Chat.UnexpectedError", ex.Message]
                    : aiMessage.Text + "\n\n" + L["Chat.UnexpectedError", ex.Message];
            }
            finally
            {
                stopwatch.Stop();
                elapsedTimer?.Dispose();

                aiMessage.ElapsedTime = FormatarTempo(stopwatch.Elapsed);

                _isThinking = false;

                if (ReferenceEquals(_turnCts, cts))
                    _turnCts = null;

                if (turn is not null)
                    await GravarTurnoAsync(turn);

                if (!_disposed)
                {
                    await InvokeAsync(StateHasChanged);
                    await ForceScrollToBottomAsync();
                }
            }
        }

        /// <summary>
        /// Actualiza a mensagem em streaming. Só é chamado com ritmo limitado (80 ms), o
        /// throttle vive no <see cref="OllamaChatStreamClient"/>. O scroll acompanha cada
        /// Actualização; quando o cliente deixou de o fazer, a lista só saltava para o fim
        /// depois da resposta completa.
        /// </summary>
        private async Task AplicarProgressoAsync(ChatStreamProgress progress, ChatMessage aiMessage, string userPrompt)
        {
            aiMessage.Text = progress.Text;
            aiMessage.Reasoning = progress.Reasoning;

            _contextUsedTokens = ChatComposerService.EstimateTokens(userPrompt)
                + (int)(progress.PromptTokens + progress.EstimatedOutputTokens);

            await InvokeAsync(StateHasChanged);

            // "stick" só quando já estamos perto do fundo: o JS não rouba o scroll de quem
            // subiu para reler, tal como antes da extracção do streaming.
            await StreamScrollAsync();
        }

        private static void AplicarResultadoStream(ChatStreamResult stream, ChatMessage aiMessage)
        {
            if (!string.IsNullOrEmpty(stream.Text))
                aiMessage.Text = stream.Text;

            // Quando o raciocínio está desativado o cliente não o captura, mesmo que o
            // modelo o envie (deepseek-r1 ignora `think: false`).
            if (!string.IsNullOrWhiteSpace(stream.Reasoning))
                aiMessage.Reasoning = stream.Reasoning;
        }

        private async Task<ChatPreparationResult> PrepararTurnoAsync(string userPrompt)
        {
            // O system prompt pode conter o token {{TARGET_LANGUAGE}}: resolve-o para o
            // idioma selecionado na UI (seletor PT/EN). Sem token, é no-op.
            var systemInstructions = (await PromptFilesService.GetPromptFileContentAsync("system-prompt.txt") ?? string.Empty)
                .Replace(TargetLanguageResolver.Token, TargetLanguageResolver.GetTargetLanguage());

            await EnsureModelSupportsThinkingAsync();

            return await ChatComposer.Prepare(new ChatPrepareRequest
            {
                Messages = _messages,
                UserPrompt = userPrompt,
                SystemInstructions = systemInstructions,
                WelcomeMessage = L["Chat.WelcomeMessage"],
                ModelName = ModelName,
                ContextLength = _contextLength,
                FitsInGpu = _gpuReport?.FitsInGpu == true,
                ModelSupportsThinking = _modelSupportsThinking
            });
        }

        private async Task GravarTurnoAsync(ChatTurn turn)
        {
            try
            {
                var result = await ChatTurnWriter.WriteAsync(turn, ModelName, _conversationId, _currentPromptId);

                _conversationId = result.ConversationId;
                _currentPromptId = result.PromptId;
            }
            catch (ChatTurnPersistenceException ex)
            {
                _logger.LogError(ex, "Não foi possível gravar o turno do chat.");

                if (DialogService is not null)
                {
                    await DialogService.ShowErrorAsync(
                        L["Chat.AutoSaveError"],
                        L["Chat.AutoSaveErrorTitle"]);
                }
            }
        }

        private async Task MostrarPromptDemasiadoGrandeAsync(ChatPreparationResult preparation)
        {
            _logger.LogWarning(
                "Prompt rejeitado: {Tokens} tokens para um contexto de {Context}.",
                preparation.PromptTokens, preparation.ContextLength);

            if (DialogService is not null)
            {
                await DialogService.ShowErrorAsync(
                    L["Chat.PromptTooLong", preparation.PromptTokens, preparation.ContextLength],
                    L["Chat.PromptTooLongTitle"]);
            }
        }

        private async Task CancelRequest()
        {
            // Só cancela: quemDispose é o dono do CTS (SendMessage).
            try
            {
                await (_turnCts?.CancelAsync() ?? Task.CompletedTask);
            }
            catch (ObjectDisposedException)
            {
                // O turno já terminou entre o clique e o cancelamento.
            }

            await DescarregarModeloAsync(ModelName);
        }

        private async Task DescarregarModeloAsync(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return;

            try
            {
                await GpuService!.UnloadModelFromMemoryAsync(modelName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao descarregar o modelo {ModelName} da memória.", modelName);
            }
        }

        // ============================================================
        //  UI
        // ============================================================

        protected void OpenHistory() => historyPanel?.Open();

        private async Task NewChat()
        {
            CancelarTurnoEmCurso();

            _messages.Clear();
            _currentMessage = string.Empty;
            _isThinking = false;

            _contextUsedTokens = 0;
            _showContextBar = false;

            _conversationId = 0;
            _currentPromptId = 0;

            _messages.Add(new ChatMessage { User = "Ollama", Text = L["Chat.WelcomeMessage"] });

            StateHasChanged();

            // Mesmo que o campo tenha ficado alto com prompts anteriores, "Novo Chat"
            // repõe a altura de "vazio".
            await ReporDimensoesDoPromptAsync();

            // Liberta a VRAM sem bloquear a UI: o utilizador vê logo a conversa nova.
            _ = DescarregarModeloAsync(ModelName);
        }

        private async Task LoadConversationAsync(int conversationId)
        {
            try
            {
                if (ConversationRepo is null) return;

                // Carregar histórico substitui o turno em curso; sem isto as mensagens
                // antigas continuavam a ser acrescentadas ao chat que já foi trocado.
                CancelarTurnoEmCurso();

                _isThinking = false;
                _messages.Clear();

                var messages = await ConversationRepo.GetMessagesAsync(conversationId);

                _conversationId = conversationId;
                _currentPromptId = 0;

                if (messages.Count == 0)
                {
                    _messages.Add(new ChatMessage { User = "Ollama", Text = L["Chat.WelcomeMessage"] });
                }
                else
                {
                    foreach (var msg in messages)
                    {
                        bool isUser = msg.Role == "user";

                        _messages.Add(new ChatMessage
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

                await InvokeAsync(StateHasChanged);
                await ForceScrollToBottomAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao carregar a conversa {ConversationId}.", conversationId);
            }
        }

        [JSInvokable]
        public async Task OnEnterPressedFromJs()
        {
            if (!_isThinking)
                await SendMessage();
        }

        private async Task OnScroll()
        {
            try
            {
                if (messagesDiv.Context is null) return;

                var state = await JS.InvokeAsync<ScrollState>("chatScroll.getScrollState", messagesDiv);

                if (state is not null)
                    userAtBottom = (state.ScrollHeight - state.ScrollTop - state.ClientHeight) < 80;
            }
            catch (JSException)
            {
                // Scroll indisponível durante navegação: ignorado.
            }
        }

        private async Task StreamScrollAsync()
        {
            try
            {
                if (messagesDiv.Context is null) return;

                await JS.InvokeVoidAsync("chatScroll.scrollDuringStream", messagesDiv);
            }
            catch (JSException)
            {
                // Ignorado: o scroll durante o stream é cosmético.
            }
        }

        private async Task ForceScrollToBottomAsync()
        {
            try
            {
                if (messagesDiv.Context is null) return;

                await JS.InvokeVoidAsync("chatScroll.scrollToBottom", messagesDiv);
            }
            catch (JSException)
            {
                // Ignorado: o scroll é cosmético.
            }
        }

        private string FormatMessage(string content) => MessageFormatter.FormatMessagePlus(content);

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

        // ============================================================
        //  UTILITÁRIOS
        // ============================================================

        private static string FormatarTempo(TimeSpan elapsed)
            => elapsed.TotalSeconds < 10
                ? $"{elapsed.TotalSeconds:F2}s"
                : $"{elapsed.TotalSeconds:F1}s";

        /// <summary>
        /// Cronómetro de "a gerar" mostrado enquanto o modelo ainda não devolveu o primeiro
        /// token. Só corre se o <paramref name="aiMessage"/> ainda for o marcador, para não
        /// competir com o temporizador de 150 ms do componente.
        /// </summary>
        private Timer CriarTemporizadorElapsed(ChatMessage aiMessage, Stopwatch stopwatch)
        {
            return new Timer(_ =>
            {
                if (_disposed || aiMessage.Text != ChatMessage.PendingPlaceholder) return;

                aiMessage.ElapsedTime = FormatarTempo(stopwatch.Elapsed);

                try
                {
                    _ = InvokeAsync(StateHasChanged);
                }
                catch (ObjectDisposedException)
                {
                    // Componente já destruído.
                }
            }, null, 0, 150);
        }

        /// <summary>
        /// Repõe a altura do campo de prompt no valor de "vazio". O <c>autoResize</c> do JS
        /// (<c>wwwroot/js/chat.js</c>) só corre em eventos <c>input</c> e o Blazor limpa o
        /// valor ligado sem disparar esse evento, pelo que a altura inline ganha a escrever
        /// uma linha sobrevivia ao envio. Já existia um helper JS para isto
        /// (<c>chatInput.resetHeight</c>), que ficou órfão quando o <c>@key</c> do input
        /// anterior foi removido.
        /// </summary>
        private async Task ReporDimensoesDoPromptAsync()
        {
            try
            {
                await JS.InvokeVoidAsync("chatInput.resetHeight", chatInputRef);
            }
            catch (JSException ex)
            {
                // Navegação/teardown: é cosmético.
                _logger.LogDebug(ex, "Não foi possível repor a altura do campo de prompt.");
            }
            catch (InvalidOperationException ex)
            {
                // Interop indisponível (render estático): o mesmo.
                _logger.LogDebug(ex, "Interop indisponível ao repor a altura do campo de prompt.");
            }
        }

        private void CancelarTurnoEmCurso()
        {
            try
            {
                _turnCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // O turno já terminou.
            }
        }

        // ============================================================
        //  LIBERTAÇÃO DE RECURSOS
        // ============================================================

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            _disposed = true;

            CancelarTurnoEmCurso();

            // O DotNetObjectReference tem de sobreviver ao handler que o vai usar:
            // primeiro desligamos o JS, só depois o libertamos.
            if (_dotNetRef is not null)
            {
                try
                {
                    await JS.InvokeVoidAsync("chatInput.detachHandlers", chatInputRef);
                }
                catch (Exception ex)
                {
                    // Já não há circuito (navegação, queda de ligação): nada a fazer.
                    _logger.LogDebug(ex, "chatInput.detachHandlers não foi executado.");
                }

                _dotNetRef.Dispose();
                _dotNetRef = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}