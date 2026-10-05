using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;
using Microsoft.JSInterop;
using OllamaArena.Models.Entities;
using OllamaArena.PromptTemplates;
using OllamaArena.Services;
using OllamaArena.Services.Exceptions;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;

namespace OllamaArena.Components.Pages.Components
{
    public partial class Benchmarks : IAsyncDisposable
    {
        // Só o que este componente lê e o que escreve: cada interface serve um
        // repositório distinto, injectado com o mesmo lifetime (o SQLite é o mesmo).
        [Inject] public required IBenchmarkQueryRepository QueryRepo { get; set; }
        [Inject] public required IBenchmarkCommandRepository CommandRepo { get; set; }
        [Inject] public required IOllamaGpuService GpuService { get; set; }
        [Inject] public required EvaluatePromptTemplate EvaluatePromptTemplate { get; set; }
        [Inject] public AutomatedJudgeService _feedbackService { get; set; } = default!;
        [Inject] public required InternetConnectivityService Internet { get; set; }
        [Inject] public ILogger<App> _logger { get; set; } = default!;
        [Inject] public IToastService ToastService { get; set; } = default!;

        private Orientation _splitterOrientation = Orientation.Horizontal;
        private string _panel1Size = "33.3%";
        private string _panel1MinSize = "250px";
        private string _panel2MinSize = "400px";
        private const int CompactBreakpoint = 768;

        private List<BenchmarkPrompt>? _promptsList;
        private BenchmarkPrompt? _selectedPrompt;
        private BenchmarkPrompt? _promptGrafico;
        private BenchmarkPrompt? _chartPrompt;
        private BenchmarkResponse? _selectedEvaluation;
        private bool _conteudoAberturaSlot1;
        private bool _conteudoAberturaSlot2;

        private bool _mostrarGrafico = false;
        private bool _chartDialogVisible = false;
        private bool isLoading = false;
        private bool isCreatingPrompt = false;
        private bool _evaluationDialogVisible;
        private bool _evaluationIsSaved;
        private bool _promptOpen;
        private readonly HashSet<int> _respostasAbertas = new();
        private DotNetObjectReference<Benchmarks>? _viewportDotNetRef;
        private AvaliacaoProgresso? _avaliacaoEmCurso;
        private CancellationTokenSource? _ctsCronometro;
        private CancellationTokenSource? _ctsAvaliacao;

        private const string ChavePassoMetadados = "fase:metadados";
        private const string ChavePassoValidacao = "fase:validacao";
        private const string NomeJuizGemini = "Gemini";
        private const string NomeJuizOpenRouter = "OpenRouter";
        private static string ChaveJuiz(string juiz) => $"juiz:{juiz}";

        private enum ProgressoEstado { Pendente, Ativo, Concluido, Falhou }

        /// <summary>
        /// Zebra do painel direito: o tom do cartão alterna a cada posição
        /// (ímpar/par), para se distinguir de relance um modelo do seguinte.
        /// As cores vivem no CSS (.tom-odd/.tom-even, com variante por tema);
        /// aqui decide-se apenas a que faixa o cartão pertence.
        /// </summary>
        private static string TomCartao(int indice) => indice % 2 == 0 ? "even" : "odd";

        /// <summary>
        /// Uma linha do indicador de progresso (uma fase do fluxo ou um juiz).
        /// O cronómetro só conta enquanto o passo está ativo, para não mostrar
        /// tempo de espera no passo que ainda não começou.
        /// </summary>
        private sealed class PassoProgresso
        {
            public string Chave { get; init; } = string.Empty;
            public string Rotulo { get; init; } = string.Empty;
            public ProgressoEstado Estado { get; set; } = ProgressoEstado.Pendente;
            public string? Detalhe { get; set; }
            public DateTime Inicio { get; set; } = DateTime.Now;
            public DateTime? Fim { get; set; }

            public int Segundos => (int)(((Fim ?? DateTime.Now) - Inicio).TotalSeconds);
            public bool MostrarTempo => Estado != ProgressoEstado.Pendente;
            public string? DetalheResumido => Detalhe is { Length: > 90 } ? string.Concat(Detalhe.AsSpan(0, 90), "…") : Detalhe;
        }

        /// <summary>
        /// Estado do indicador de progresso da avaliação automática de uma resposta.
        /// Só existe para dar feedback ao utilizador: a espera real acontece nos
        /// juízes em nuvem (em paralelo, e com um intervalo mínimo entre avaliações).
        /// </summary>
        private sealed class AvaliacaoProgresso
        {
            public int RespostaId { get; init; }
            public string Modelo { get; init; } = string.Empty;
            public DateTime Inicio { get; init; } = DateTime.Now;
            public bool ACancelar { get; set; }
            public List<PassoProgresso> Passos { get; } = new();

            public int Segundos => (int)(DateTime.Now - Inicio).TotalSeconds;

            public PassoProgresso? PassoPorChave(string chave)
                => Passos.FirstOrDefault(p => p.Chave == chave);
        }

        /// <summary>
        /// Verdadeiro enquanto qualquer operação bloqueante está em curso
        /// (avaliação automática ou cópia do prompt) — desabilita os botões concorrentes.
        /// </summary>
        private bool OperacaoEmCurso => isCreatingPrompt || _avaliacaoEmCurso is not null;

        private bool AvaliacaoDesteModelo(int respostaId) => _avaliacaoEmCurso?.RespostaId == respostaId;

        private void IniciarAvaliacaoProgresso(BenchmarkResponse resposta)
        {
            TerminarAvaliacaoProgresso();

            var progresso = new AvaliacaoProgresso
            {
                RespostaId = resposta.Id,
                Modelo = resposta.NomeModelo
            };

            // Os passos são criados já na ordem real do fluxo, para o utilizador ver
            // de imediato o que falta (os dois Starts só quando o pedido arranca).
            progresso.Passos.Add(new PassoProgresso
            {
                Chave = ChavePassoMetadados,
                Rotulo = L["Benchmarks.ProgressStepPreparing"].Value,
                Estado = ProgressoEstado.Ativo
            });
            progresso.Passos.Add(new PassoProgresso
            {
                Chave = ChaveJuiz(NomeJuizGemini),
                Rotulo = L["Benchmarks.ProgressStepJudge", NomeJuizGemini].Value
            });
            progresso.Passos.Add(new PassoProgresso
            {
                Chave = ChaveJuiz(NomeJuizOpenRouter),
                Rotulo = L["Benchmarks.ProgressStepJudge", NomeJuizOpenRouter].Value
            });
            progresso.Passos.Add(new PassoProgresso
            {
                Chave = ChavePassoValidacao,
                Rotulo = L["Benchmarks.ProgressStepParsing"].Value
            });

            _avaliacaoEmCurso = progresso;
            _ctsAvaliacao = new CancellationTokenSource();
            _ctsCronometro = new CancellationTokenSource();
            _ = AtualizarCronometroAsync(_ctsCronometro.Token);

            StateHasChanged();
        }

        /// <summary>
        /// Cronómetro de 1 s para manter os segundos decorridos visíveis. Corre fora do
        /// circuito do renderer, por isso a atualização do estado tem de ser marshalled
        /// com InvokeAsync; cancelado em TerminarAvaliacaoProgresso/DisposeAsync.
        /// </summary>
        private async Task AtualizarCronometroAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    if (_avaliacaoEmCurso is null)
                        return;

                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException)
            {
                // Terminação normal (avaliação concluída ou componente descartado).
            }
            catch (ObjectDisposedException)
            {
                // Componente já descartado durante o decode.
            }
            catch (InvalidOperationException)
            {
                // Circuito do renderer indisponível (navegação/reconexão).
            }
        }

        /// <summary>
        /// Os eventos do serviço chegam em threadpool (as chamadas aos juízes são
        /// paralelas), por isso o estado do componente só é tocado dentro de
        /// InvokeAsync. O método nunca deixa a exceção escapar para o task fire-and-forget.
        /// </summary>
        private async Task ReportarProgressoJuizAsync(JudgeProgressEvent evento)
        {
            try
            {
                await InvokeAsync(() =>
                {
                    if (_avaliacaoEmCurso is null)
                        return;

                    AplicarEventoJuiz(_avaliacaoEmCurso, evento);
                    StateHasChanged();
                });
            }
            catch (ObjectDisposedException)
            {
                // Componente descartado durante o decode.
            }
            catch (InvalidOperationException)
            {
                // Circuito do renderer indisponível.
            }
            catch (OperationCanceledException)
            {
                // Renderer a cancelar o trabalho pendente.
            }
        }

        private void AplicarEventoJuiz(AvaliacaoProgresso progresso, JudgeProgressEvent evento)
        {
            if (string.IsNullOrEmpty(evento.Juiz))
                return;

            var passo = progresso.PassoPorChave(ChaveJuiz(evento.Juiz));
            if (passo is null)
                return;

            switch (evento.Step)
            {
                case JudgeProgressStep.JuizIniciado:
                    passo.Estado = ProgressoEstado.Ativo;
                    passo.Inicio = DateTime.Now;
                    passo.Fim = null;
                    passo.Detalhe = null;
                    break;

                case JudgeProgressStep.JuizConcluido:
                    passo.Estado = ProgressoEstado.Concluido;
                    passo.Fim = DateTime.Now;
                    passo.Detalhe = null;
                    break;

                case JudgeProgressStep.JuizFalhou:
                    passo.Estado = ProgressoEstado.Falhou;
                    passo.Fim = DateTime.Now;
                    passo.Detalhe = evento.Detalhe;
                    break;

                case JudgeProgressStep.ModeloAlternativo:
                    // Continua a avaliar, mas com outro modelo (fallback do OpenRouter).
                    passo.Estado = ProgressoEstado.Ativo;
                    passo.Detalhe = L["Benchmarks.ProgressStepFallback", evento.Detalhe ?? string.Empty].Value;
                    break;
            }
        }

        private void ConcluirPasso(string chave)
        {
            var passo = _avaliacaoEmCurso?.PassoPorChave(chave);
            if (passo is null)
                return;

            passo.Estado = ProgressoEstado.Concluido;
            passo.Fim = DateTime.Now;
            StateHasChanged();
        }

        private void IniciarPasso(string chave)
        {
            var passo = _avaliacaoEmCurso?.PassoPorChave(chave);
            if (passo is null)
                return;

            passo.Estado = ProgressoEstado.Ativo;
            passo.Inicio = DateTime.Now;
            passo.Fim = null;
            StateHasChanged();
        }

        private void CancelarAvaliacao()
        {
            var progresso = _avaliacaoEmCurso;
            if (progresso is null || progresso.ACancelar)
                return;

            progresso.ACancelar = true;
            try
            {
                _ctsAvaliacao?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Avaliação já terminada.
            }

            StateHasChanged();
        }

        private void TerminarAvaliacaoProgresso()
        {
            try
            {
                _ctsCronometro?.Cancel();
                _ctsCronometro?.Dispose();
                _ctsAvaliacao?.Cancel();
                _ctsAvaliacao?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Já descartado.
            }

            _ctsCronometro = null;
            _ctsAvaliacao = null;
            _avaliacaoEmCurso = null;
        }

        private static string MarcadorPasso(ProgressoEstado estado) => estado switch
        {
            ProgressoEstado.Concluido => "✓",
            ProgressoEstado.Falhou => "✗",
            ProgressoEstado.Ativo => "⟳",
            _ => "○"
        };

        private string TextoPasso(ProgressoEstado estado) => estado switch
        {
            ProgressoEstado.Ativo => L["Benchmarks.ProgressStateRunning"],
            ProgressoEstado.Concluido => L["Benchmarks.ProgressStateDone"],
            ProgressoEstado.Falhou => L["Benchmarks.ProgressStateFailed"],
            _ => L["Benchmarks.ProgressStatePending"]
        };


        protected override async Task OnInitializedAsync() => await GetDataAsync();

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    var largura = await JS.InvokeAsync<int>("appViewport.getWidth");
                    AplicarLayoutCompacto(largura);
                    _viewportDotNetRef = DotNetObjectReference.Create(this);
                    await JS.InvokeVoidAsync("appViewport.subscribeResize", _viewportDotNetRef);
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Não foi possível subscrever o tamanho do viewport no Benchmarks.");
                }
            }
        }

        [JSInvokable]
        public Task OnViewportResized(int largura)
        {
            AplicarLayoutCompacto(largura);
            StateHasChanged();
            return Task.CompletedTask;
        }

        private void AplicarLayoutCompacto(int largura)
        {
            if (largura <= CompactBreakpoint)
            {
                _splitterOrientation = Orientation.Vertical;
                _panel1Size = "40%";
                _panel1MinSize = "120px";
                _panel2MinSize = "200px";
            }
            else
            {
                _splitterOrientation = Orientation.Horizontal;
                _panel1Size = "33.3%";
                _panel1MinSize = "250px";
                _panel2MinSize = "400px";
            }
        }

        public async ValueTask DisposeAsync()
        {
            TerminarAvaliacaoProgresso();

            try
            {
                if (_viewportDotNetRef != null)
                {
                    await JS.InvokeVoidAsync("appViewport.unsubscribeResize");
                    _viewportDotNetRef.Dispose();
                    _viewportDotNetRef = null;
                }
            }
            catch
            {
                // Ignora falhas de cleanup (ex.: navegação intermédia)
            }
        }

        protected void RefreshPage()
        {
            NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
        }
        private async Task GetDataAsync()
        {
            isLoading = true;
            StateHasChanged();
            try
            {
                _promptsList = null;
                _promptsList = await QueryRepo.GetAllBenchmarksAsync();

                if (_selectedPrompt != null && _promptsList != null)
                {
                    _selectedPrompt = _promptsList.FirstOrDefault(p => p.Id == _selectedPrompt.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro ao obter os dados dos prompts de benchmark.");
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }


        private void  SelectPrompt(BenchmarkPrompt prompt)
        {
            _mostrarGrafico = false;
            _selectedPrompt = prompt;
            _promptOpen = false;
            _respostasAbertas.Clear();
            StateHasChanged();
        }

        private static Icon GetPromptChevron(bool open)
            => open ? new Icons.Regular.Size12.ChevronUp() : new Icons.Regular.Size12.ChevronDown();

        private bool RespostaAberta(BenchmarkResponse resp)
            => resp.Id > 0 && _respostasAbertas.Contains(resp.Id);

        private void AlternarResposta(int id)
        {
            if (!_respostasAbertas.Remove(id))
            {
                _respostasAbertas.Add(id);
            }
        }

        private static void TogglePromptTecla(KeyboardEventArgs e, Action toggle)
        {
            if (e.Key == " " || e.Key == "Enter")
            {
                toggle();
            }
        }

        private async Task MostrarGraficoPrompt(BenchmarkPrompt prompt)
        {
            _promptGrafico = prompt;
            _mostrarGrafico = true;
            _selectedPrompt = null;

            await Task.Delay(50);
            await RenderChartPromptAsync(prompt);
        }

        private async Task RenderChartPromptAsync(BenchmarkPrompt prompt)
        {
            if (prompt.Answers == null || prompt.Answers.Count == 0)
                return;

            var dados = new
            {
                labels = new List<string>
                {
                    L["Benchmarks.ChartLabelTokensPerSecond"],
                    L["Benchmarks.ChartLabelEval"],
                    L["Benchmarks.ChartLabelLoad"],
                    L["Benchmarks.ChartLabelTokens"]
                },
                datasets = new List<object>()
            };

            foreach (var resp in prompt.Answers)
            {
                dados.datasets.Add(new
                {
                    label = resp.NomeModelo,
                    borderColor = GenerateRandomColor(),
                    fill = false,
                    data = new List<double>
                {
                    resp.TokensPorSegundo,
                    resp.TempoPuroMs,
                    resp.TempoCargaMs,
                    resp.TamanhoTokens
                }
                });
            }

            await JS.InvokeVoidAsync("benchmarkCharts.renderGrafico", dados);
        }

        private string GenerateRandomColor()
        {
            var rnd = new Random();
            return $"rgb({rnd.Next(255)}, {rnd.Next(255)}, {rnd.Next(255)})";
        }

        private async Task DeletePromptAsync(BenchmarkPrompt prompt)
        {
            if (prompt == null || DialogService == null) return;

            try
            {
                int totalRespostas = prompt.Answers?.Count ?? 0;
                var confirmacao = await DialogService.ShowConfirmationAsync(
                    L["Benchmarks.DeleteConfirmMessage", prompt.Id, totalRespostas],
                    L["Benchmarks.DeleteConfirmYes"],
                    L["Common.Cancel"],
                    L["Benchmarks.DeleteConfirmTitle"]);

                if (confirmacao == null) return;
                var resultado = await confirmacao.Result;

                if (resultado != null && !resultado.Cancelled)
                {
                    bool eliminado = await CommandRepo.DeletePromptAsync(prompt.Id);
                    if (eliminado)
                    {
                        if (_selectedPrompt?.Id == prompt.Id) _selectedPrompt = null;
                        await GetDataAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Erro ao apagar o Prompt #{prompt.Id}");
            }
        }

        private string ConverterQuebrasLinha(string? texto) =>
            texto?.Replace("\n", "<br />") ?? "";

        private async Task CopyToClipboardAsync(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return;

            isCreatingPrompt = true;
            try
            {
                bool copied = await JS.InvokeAsync<bool>("copyToClipboard", texto);

                if (copied)
                {
                    ToastService.ShowSuccess(L["Benchmarks.CopySuccess"], timeout: 3000);
                    _logger?.LogInformation("Texto copiado para a área de transferência.");
                }
                else
                {
                    // A área de transferência pode estar bloqueada pelo browser (permissão
                    // negada, contexto não seguro). O JS devolve false em vez de lançar.
                    ToastService.ShowError(L["Benchmarks.CopyError"]);
                    _logger?.LogWarning("O browser recusou a escrita na área de transferência.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro ao copiar para o Clipboard.");
                ToastService.ShowError($"{L["Benchmarks.CopyError"]} {ex.Message}");
            }
            finally
            {
                isCreatingPrompt = false;
            }
        }
        private void OpenChartDialog(BenchmarkPrompt prompt)
        {
            _chartPrompt = prompt;
            _chartDialogVisible = true;
        }

        private async Task SaveEvaluationAsync(BenchmarkResponse resposta)
        {
            if (resposta == null || CommandRepo == null) return;

            try
            {
                if (resposta.GeminiRating is < 1 or > 5 || resposta.OpenRouterRating is < 1 or > 5)
                {
                    if (DialogService != null)
                    {
                        await DialogService.ShowErrorAsync(L["Benchmarks.ValidationRatingMessage"], L["Benchmarks.ValidationRatingTitle"]);
                    }
                    return;
                }

                // Origem de cada slot: automático carimba na PreencherAvaliacao;
                // colagem manual vira 'externo'; slots vazios/legados ficam a null.
                resposta.Juiz1Origem = JudgeOriginResolver.CalcularOrigemSlot(
                    JudgeOriginResolver.TemConteudo(resposta.GeminiFeedback, resposta.GeminiRating),
                    _conteudoAberturaSlot1,
                    resposta.Juiz1Origem);
                resposta.Juiz2Origem = JudgeOriginResolver.CalcularOrigemSlot(
                    JudgeOriginResolver.TemConteudo(resposta.OpenRouterFeedback, resposta.OpenRouterRating),
                    _conteudoAberturaSlot2,
                    resposta.Juiz2Origem);

                bool guardado = await CommandRepo.UpdateResponseEvaluationAsync(resposta);

                if (guardado)
                {
                    // A avaliação manual edita uma instância nova vinda da BD (OpenEvaluation);
                    // substituímos o item correspondente na lista para os badges do rodapé refletirem a nota sem recarregar a página.
                    var respostas = _selectedPrompt?.Answers;
                    if (respostas != null)
                    {
                        var idx = respostas.FindIndex(a => a.Id == resposta.Id);
                        if (idx >= 0) respostas[idx] = resposta;
                    }

                    await CloseDialogAsync();
                    await DialogService.ShowInfoAsync(L["Benchmarks.EvaluationSaved", resposta.NomeModelo], L["Common.Success"]);
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"[SQLITE ERROR] Falha ao atualizar avaliação: {ex.Message}");
            }
        }

        private async Task<string> BuildPromptDeAvaliacaoAsync(BenchmarkResponse resposta, string originalPrompt, string modelName)
        {
            var metadata = await GpuService.GetExtendedModelMetadataAsync(modelName);

            return await EvaluatePromptTemplate.EvaluationCopyPromptAsync(
                originalPrompt, resposta.TextoResposta, metadata.TrainingYear, modelName,
                expectedLanguage: resposta.IdiomaSessao);
        }

        /// <summary>
        /// Fluxo automatizado: gere o prompt, pede a avaliação aos dois juízes (Gemini e OpenRouter)
        /// e abre o diálogo de avaliação já preenchido. A gravação só acontece quando o utilizador
        /// confirma no diálogo ("Guardar avaliação").
        /// </summary>
        private async Task AvaliarAutomaticamenteAsync(BenchmarkResponse resposta, string originalPrompt, string modelName)
        {
            if (resposta == null || string.IsNullOrEmpty(originalPrompt) || string.IsNullOrEmpty(resposta.TextoResposta))
                return;

            if (AvaliacaoJaGuardada(resposta))
            {
                var confirmacao = await DialogService.ShowConfirmationAsync(
                    L["Benchmarks.EvaluationAlreadySavedMessage"],
                    L["Benchmarks.EvaluationAlreadySavedContinue"],
                    L["Common.Cancel"],
                    L["Benchmarks.EvaluationAlreadySavedTitle"]);

                if (confirmacao == null) return;
                var resultado = await confirmacao.Result;
                if (resultado != null && resultado.Cancelled) return;
            }

            if (!await Internet.HasInternetAsync())
            {
                if (DialogService != null)
                {
                    await DialogService.ShowErrorAsync(
                        L["Benchmarks.NoInternetError"],
                        L["Benchmarks.AutomatedJudgeErrorTitle"]);
                }
                return;
            }

            isCreatingPrompt = true;
            IniciarAvaliacaoProgresso(resposta);
            var tokenAvaliacao = _ctsAvaliacao!.Token;

            try
            {
                string formattedPrompt = await BuildPromptDeAvaliacaoAsync(resposta, originalPrompt, modelName);
                ConcluirPasso(ChavePassoMetadados);

                AutomatedJudgeResult resultado;
                try
                {
                    resultado = await _feedbackService.GetFeedbacksAutomatizadosAsync(
                        formattedPrompt,
                        evento => _ = ReportarProgressoJuizAsync(evento),
                        tokenAvaliacao);
                }
                catch (QuotaLimitException quotaEx)
                {
                    if (DialogService != null)
                    {
                        await DialogService.ShowWarningAsync(
                            L["Benchmarks.QuotaLimitMessage", quotaEx.SegundosRestantes],
                            L["Benchmarks.QuotaLimitTitle"]);
                    }
                    return;
                }

                // Nenhum dos juízes devolveu resposta -> alerta o utilizador e não abre o diálogo.
                if (!resultado.Gemini.Success && !resultado.OpenRouter.Success)
                {
                    var errosTotais = new List<string>
                    {
                        resultado.Gemini.ErrorMessage,
                        resultado.OpenRouter.ErrorMessage
                    };
                    errosTotais.RemoveAll(string.IsNullOrWhiteSpace);

                    _logger?.LogWarning("Avaliação automática sem resposta de nenhum juiz: {Erros}", string.Join(" | ", errosTotais));

                    if (DialogService != null)
                    {
                        await DialogService.ShowErrorAsync(
                            $"{L["Benchmarks.AutomatedJudgesNoResponseMessage"]}\n{string.Join(Environment.NewLine, errosTotais)}",
                            L["Benchmarks.AutomatedJudgeErrorTitle"]);
                    }
                    return;
                }

                // Snapshot do conteúdo à abertura deste fluxo: slots já preenchidos
                // sem origem gravada são tratados como legados (não lhes inventamos origem).
                _conteudoAberturaSlot1 = JudgeOriginResolver.TemConteudo(resposta.GeminiFeedback, resposta.GeminiRating);
                _conteudoAberturaSlot2 = JudgeOriginResolver.TemConteudo(resposta.OpenRouterFeedback, resposta.OpenRouterRating);

                IniciarPasso(ChavePassoValidacao);
                PreencherAvaliacao(resposta, resultado.Gemini, juizGemini: true);
                PreencherAvaliacao(resposta, resultado.OpenRouter, juizGemini: false);
                if (resultado.HasErrors)
                {
                    var erros = new List<string>();
                    if (!resultado.Gemini.Success) erros.Add(resultado.Gemini.ErrorMessage);
                    if (!resultado.OpenRouter.Success) erros.Add(resultado.OpenRouter.ErrorMessage);

                    _logger?.LogWarning("Avaliação automática com erros parciais: {Erros}", string.Join(" | ", erros));

                    if (DialogService != null)
                    {
                        await DialogService.ShowWarningAsync(
                            string.Join(Environment.NewLine, erros),
                            L["Benchmarks.AutomatedJudgeErrorTitle"]);
                    }
                }

                _selectedEvaluation = resposta;
                _evaluationIsSaved = false;
                _evaluationDialogVisible = true;
            }
            catch (OperationCanceledException)
            {
                // Cancelado pelo utilizador (botão "Cancelar" na barra de progresso):
                // não é erro — a resposta fica como estava e o aviso é apenas o log.
                _logger?.LogInformation("Avaliação automática de {Modelo} cancelada pelo utilizador.", modelName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro ao executar avaliação automática.");
                if (DialogService != null)
                {
                    await DialogService.ShowErrorAsync(L["Benchmarks.AutomatedJudgeError"], L["Benchmarks.AutomatedJudgeErrorTitle"]);
                }
            }
            finally
            {
                isCreatingPrompt = false;
                TerminarAvaliacaoProgresso();

                // Navegar para fora durante a avaliação cancela o pedido em curso e traz
                // este finally para um componente já descartado: o render não pode explodir.
                try
                {
                    await InvokeAsync(StateHasChanged);
                }
                catch (ObjectDisposedException)
                {
                    // Componente descartado a meio da avaliação.
                }
                catch (InvalidOperationException)
                {
                    // Circuito do renderer indisponível.
                }
            }
        }

        /// <summary>
        /// Fallback manual: copia apenas o prompt formatado para a área de transferência,
        /// para quando as APIs dos juízes não estiverem disponíveis.
        /// </summary>
        private async Task CopiarPromptAvaliacaoAsync(BenchmarkResponse resposta, string originalPrompt, string modelName)
        {
            if (resposta == null || string.IsNullOrEmpty(originalPrompt) || string.IsNullOrEmpty(resposta.TextoResposta))
                return;

            isCreatingPrompt = true;
            StateHasChanged();

            try
            {
                string formattedPrompt = await BuildPromptDeAvaliacaoAsync(resposta, originalPrompt, modelName);
                await CopyToClipboardAsync(formattedPrompt);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro ao copiar prompt para avaliação.");
            }
            finally
            {
                isCreatingPrompt = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Aviso local quando a resposta gravada não respeita o idioma selecionado na UI
        /// (seletor PT/EN). Avaliação em render-time, heurística; inconclusivo nunca avisa.
        /// </summary>
        private string? LanguageWarningFor(string? text)
        {
            var detetado = ResponseLanguageChecker.Detect(text);

            if (!ResponseLanguageChecker.IsMismatch(detetado, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName))
                return null;

            return L["Common.LanguageMismatch", TargetLanguageResolver.GetTargetLanguage()];
        }

        /// <summary>
        /// Indica se a resposta já tem uma avaliação gravada na base de dados.
        /// </summary>
        private static bool AvaliacaoJaGuardada(BenchmarkResponse resposta) =>            resposta.GeminiRating != null &&
            resposta.GeminiFeedback != null &&
            resposta.OpenRouterRating != null &&
            resposta.OpenRouterFeedback != null;

        /// <summary>
        /// Mapeia o resultado estruturado de um juiz para os campos da resposta.
        /// Se o juiz falhou, os campos ficam vazios/ editáveis para o utilizador colar manualmente.
        /// Aplica a guarda determinística de idioma (JudgeLanguageGuard) a ambos os juízes.
        /// </summary>
        private void PreencherAvaliacao(BenchmarkResponse resposta, JudgeFeedbackResult resultado, bool juizGemini)
        {
            if (!resultado.Success) return;

            var parsed = resultado.Parsed;
            var notaIdioma = AplicarGuardaIdioma(resposta, parsed);

            if (juizGemini)
            {
                resposta.GeminiFactualRating = parsed.FactualScore;
                resposta.GeminiFormattingRating = parsed.FormattingScore;
                resposta.GeminiComplianceRating = parsed.ComplianceScore;
                resposta.GeminiRelevanceRating = parsed.RelevanceScore;
                resposta.GeminiToneRating = parsed.ToneScore;
                resposta.GeminiConcisenessRating = parsed.ConcisenessScore;
                resposta.GeminiClarityRating = parsed.ClarityScore;
                resposta.GeminiReadabilityRating = parsed.ReadabilityScore;
                resposta.GeminiHaloEffectRating = parsed.HaloEffectScore;
                resposta.GeminiSafetyRating = parsed.SafetyScore;
                resposta.GeminiLanguageConsistencyRating = parsed.LanguageConsistencyScore;
                resposta.GeminiLoopDetectionRating = parsed.LoopDetectionScore;
                resposta.GeminiRefusalHandled = parsed.RefusalHandledFlag;
                resposta.GeminiRating = parsed.FinalScore;
                resposta.GeminiFeedback = string.IsNullOrWhiteSpace(parsed.Description) ? resultado.RawText : parsed.Description;
                resposta.GeminiRecommendation = CombinarComNotaIdioma(notaIdioma, parsed.Recommendation);
                resposta.Juiz1Origem = JudgeOriginResolver.Gemini;
            }
            else
            {
                resposta.OpenRouterFactualRating = parsed.FactualScore;
                resposta.OpenRouterFormattingRating = parsed.FormattingScore;
                resposta.OpenRouterComplianceRating = parsed.ComplianceScore;
                resposta.OpenRouterRelevanceRating = parsed.RelevanceScore;
                resposta.OpenRouterToneRating = parsed.ToneScore;
                resposta.OpenRouterConcisenessRating = parsed.ConcisenessScore;
                resposta.OpenRouterClarityRating = parsed.ClarityScore;
                resposta.OpenRouterReadabilityRating = parsed.ReadabilityScore;
                resposta.OpenRouterHaloEffectRating = parsed.HaloEffectScore;
                resposta.OpenRouterSafetyRating = parsed.SafetyScore;
                resposta.OpenRouterLanguageConsistencyRating = parsed.LanguageConsistencyScore;
                resposta.OpenRouterLoopDetectionRating = parsed.LoopDetectionScore;
                resposta.OpenRouterRefusalHandled = parsed.RefusalHandledFlag;
                resposta.OpenRouterRating = parsed.FinalScore;
                resposta.OpenRouterFeedback = string.IsNullOrWhiteSpace(parsed.Description) ? resultado.RawText : parsed.Description;
                resposta.OpenRouterRecommendation = CombinarComNotaIdioma(notaIdioma, parsed.Recommendation);
                resposta.Juiz2Origem = JudgeOriginResolver.OpenRouter;
            }
        }

        /// <summary>
        /// Rede de segurança local para o idioma: se a resposta divergir claramente do
        /// idioma esperado (gravado na resposta ou, em falta, o idioma atual do seletor
        /// PT/EN), limita a nota Language Consistency do juiz a 2; se está no idioma
        /// certo mas mistura línguas, limita a 3. Devolve uma nota para juntar à
        /// recomendação — mesmo quando o juiz LLM foi leniente.
        /// </summary>
        private string? AplicarGuardaIdioma(BenchmarkResponse resposta, ParsedEvaluationResult parsed)
        {
            if (JudgeLanguageGuard.DeveCapar(resposta.TextoResposta, resposta.IdiomaSessao, out var detetado))
            {
                parsed.LanguageConsistencyScore = JudgeLanguageGuard.Capar(parsed.LanguageConsistencyScore);
                return L["Common.LocalLanguageOverride", NomeIdiomaDetetado(detetado), IdiomaEsperado(resposta)].Value;
            }

            if (JudgeLanguageGuard.DeveCaparPorMistura(resposta.TextoResposta, resposta.IdiomaSessao))
            {
                parsed.LanguageConsistencyScore = JudgeLanguageGuard.CaparMistura(parsed.LanguageConsistencyScore);
                return L["Common.LocalLanguageMixOverride", LinguaIntrusa(resposta), IdiomaEsperado(resposta)].Value;
            }

            return null;
        }

        private string LinguaIntrusa(BenchmarkResponse resposta)
        {
            return JudgeLanguageGuard.CodigoEsperado(resposta.IdiomaSessao) == "pt"
                ? L["Common.DetectedLang.En"].Value
                : L["Common.DetectedLang.Pt"].Value;
        }

        private string NomeIdiomaDetetado(DetectedLanguage detetado)
        {
            return (detetado == DetectedLanguage.Portuguese
                ? L["Common.DetectedLang.Pt"]
                : L["Common.DetectedLang.En"]).Value;
        }

        private static string IdiomaEsperado(BenchmarkResponse resposta)
        {
            return string.IsNullOrWhiteSpace(resposta.IdiomaSessao)
                ? TargetLanguageResolver.GetTargetLanguage()
                : resposta.IdiomaSessao!;
        }

        private static string CombinarComNotaIdioma(string? notaIdioma, string? recomendacao)
        {
            if (string.IsNullOrWhiteSpace(notaIdioma)) return recomendacao ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recomendacao)) return notaIdioma;
            return $"{notaIdioma} {recomendacao}";
        }
        private async Task OpenEvaluation(int id)
        {
            var benchmarkEvaluation = await QueryRepo.GetBenchmarkAnswersByIdAsync(id);
            _selectedEvaluation = benchmarkEvaluation;
            _conteudoAberturaSlot1 = JudgeOriginResolver.TemConteudo(benchmarkEvaluation?.GeminiFeedback, benchmarkEvaluation?.GeminiRating);
            _conteudoAberturaSlot2 = JudgeOriginResolver.TemConteudo(benchmarkEvaluation?.OpenRouterFeedback, benchmarkEvaluation?.OpenRouterRating);
            _evaluationIsSaved =
                benchmarkEvaluation?.GeminiRating != null &&
                benchmarkEvaluation.GeminiFeedback != null &&
                benchmarkEvaluation.OpenRouterRating != null &&
                benchmarkEvaluation.OpenRouterFeedback != null;
            _evaluationDialogVisible = true;
        }
        private Task CloseDialogAsync()
        {
            _evaluationDialogVisible = false;
            return Task.CompletedTask;
        }

        private string RotuloColuna(bool juiz1, BenchmarkResponse resp)
        {
            var temConteudo = juiz1
                ? JudgeOriginResolver.TemConteudo(resp.GeminiFeedback, resp.GeminiRating)
                : JudgeOriginResolver.TemConteudo(resp.OpenRouterFeedback, resp.OpenRouterRating);

            return JudgeLabelResolver.Rotulo(
                L,
                juiz1 ? resp.Juiz1Origem : resp.Juiz2Origem,
                temConteudo,
                primeiroSlot: juiz1);
        }

        private bool SemRespostaColuna(bool juiz1, BenchmarkResponse resp)
        {
            var temConteudo = juiz1
                ? JudgeOriginResolver.TemConteudo(resp.GeminiFeedback, resp.GeminiRating)
                : JudgeOriginResolver.TemConteudo(resp.OpenRouterFeedback, resp.OpenRouterRating);

            return JudgeLabelResolver.SemResposta(
                juiz1 ? resp.Juiz1Origem : resp.Juiz2Origem,
                temConteudo);
        }
    }
}