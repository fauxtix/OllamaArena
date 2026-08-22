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
        [Inject] public required IBenchmarkRepository BenchmarkRepo { get; set; }
        [Inject] public required IOllamaGpuService GpuService { get; set; }
        [Inject] public required EvaluatePromptTemplate EvaluatePromptTemplate { get; set; }
        [Inject] public AutomatedJudgeService _feedbackService { get; set; } = default!;
        [Inject] public required InternetConnectivityService Internet { get; set; }
        [Inject] public ILogger<App> _logger { get; set; } = default!;

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

        private bool _mostrarGrafico = false;
        private bool _chartDialogVisible = false;
        private bool isLoading = false;
        private bool isCreatingPrompt = false;
        private bool _evaluationDialogVisible;
        private bool _evaluationIsSaved;
        private bool _promptOpen;
        private DotNetObjectReference<Benchmarks>? _viewportDotNetRef;

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
                _promptsList = await BenchmarkRepo.GetAllBenchmarksAsync();

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
            StateHasChanged();
        }

        private static Icon GetPromptChevron(bool open)
            => open ? new Icons.Regular.Size12.ChevronUp() : new Icons.Regular.Size12.ChevronDown();

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
                    bool eliminado = await BenchmarkRepo.DeletePromptAndHistoryAsync(prompt.Id);
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
                    // TODO: Mostrar toast de sucesso (FluentToast ou similar)
                    _logger?.LogInformation("Texto copiado para a área de transferência.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro ao copiar para o Clipboard.");
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
            if (resposta == null || BenchmarkRepo == null) return;

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

                bool guardado = await BenchmarkRepo.UpdateResponseEvaluationAsync(resposta);

                if (guardado)
                {
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
            StateHasChanged();

            try
            {
                string formattedPrompt = await BuildPromptDeAvaliacaoAsync(resposta, originalPrompt, modelName);

                AutomatedJudgeResult resultado;
                try
                {
                    resultado = await _feedbackService.GetFeedbacksAutomatizadosAsync(formattedPrompt);
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
                StateHasChanged();
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
            }
        }

        /// <summary>
        /// Rede de segurança local para o idioma: se a resposta divergir claramente do
        /// idioma esperado (gravado na resposta ou, em falta, o idioma atual do seletor
        /// PT/EN), limita a nota Language Consistency do juiz e devolve uma nota para
        /// juntar à recomendação — mesmo quando o juiz LLM foi leniente.
        /// </summary>
        private string? AplicarGuardaIdioma(BenchmarkResponse resposta, ParsedEvaluationResult parsed)
        {
            if (!JudgeLanguageGuard.DeveCapar(resposta.TextoResposta, resposta.IdiomaSessao, out var detetado))
                return null;

            parsed.LanguageConsistencyScore = JudgeLanguageGuard.Capar(parsed.LanguageConsistencyScore);

            return L["Common.LocalLanguageOverride", NomeIdiomaDetetado(detetado), IdiomaEsperado(resposta)].Value;
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
            var benchmarkEvaluation = await BenchmarkRepo.GetBenchmarkAnswersByIdAsync(id);
            _selectedEvaluation = benchmarkEvaluation;
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
    }
}