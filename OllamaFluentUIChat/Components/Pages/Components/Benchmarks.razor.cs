using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.PromptTemplates;
using OllamaFluentUIChat.Services;
using OllamaFluentUIChat.Services.Exceptions;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;

namespace OllamaFluentUIChat.Components.Pages.Components
{
    public partial class Benchmarks
    {
        [Inject] public required IBenchmarkRepository BenchmarkRepo { get; set; }
        [Inject] public required IOllamaGpuService GpuService { get; set; }
        [Inject] public required EvaluatePromptTemplate EvaluatePromptTemplate { get; set; }
        [Inject] public AutomatedJudgeService _feedbackService { get; set; } = default!;
        [Inject] public ILogger<App> _logger { get; set; } = default!;

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

        protected override async Task OnInitializedAsync() => await GetDataAsync();

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
            StateHasChanged();
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
                labels = new List<string> { L["Benchmarks.ChartLabelTokensPerSecond"], L["Benchmarks.ChartLabelEval"], L["Benchmarks.ChartLabelLoad"], L["Benchmarks.ChartLabelTokens"] },
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
                originalPrompt, resposta.TextoResposta, metadata.TrainingYear, modelName);
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
        /// Mapeia o resultado estruturado de um juiz para os campos da resposta.
        /// Se o juiz falhou, os campos ficam vazios/ editáveis para o utilizador colar manualmente.
        /// </summary>
        private static void PreencherAvaliacao(BenchmarkResponse resposta, JudgeFeedbackResult resultado, bool juizGemini)
        {
            if (!resultado.Success) return;

            var parsed = resultado.Parsed;

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
                resposta.GeminiRating = parsed.FinalScore;
                resposta.GeminiFeedback = string.IsNullOrWhiteSpace(parsed.Description) ? resultado.RawText : parsed.Description;
                resposta.GeminiRecommendation = parsed.Recommendation;
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
                resposta.OpenRouterRating = parsed.FinalScore;
                resposta.OpenRouterFeedback = string.IsNullOrWhiteSpace(parsed.Description) ? resultado.RawText : parsed.Description;
                resposta.OpenRouterRecommendation = parsed.Recommendation;
            }
        }
        private async Task OpenEvaluation(int id)
        {
            var benchmarkEvaluation = await BenchmarkRepo.GetBenchmarkAnswersByIdAsync(id);
            _selectedEvaluation = benchmarkEvaluation;
            _evaluationDialogVisible = true;
        }
        private Task CloseDialogAsync()
        {
            _evaluationDialogVisible = false;
            return Task.CompletedTask;
        }
    }
}