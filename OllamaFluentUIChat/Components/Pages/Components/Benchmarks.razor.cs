using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.PromptTemplates;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;

namespace OllamaFluentUIChat.Components.Pages.Components
{
    public partial class Benchmarks
    {
        [Inject] public required IBenchmarkRepository BenchmarkRepo { get; set; }
        [Inject] public required IOllamaGpuService GpuService { get; set; }
        [Inject] public required EvaluatePromptTemplate EvaluatePromptTemplate { get; set; }
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
                labels = new List<string> { "Tokens/s", "Eval (ms)", "Load (ms)", "Tokens" },
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
                    $"Tem a certeza que deseja apagar o Benchmark #{prompt.Id}? " +
                    $"Se confirmar, irá remover permanentemente todas as {totalRespostas} respostas associadas (Cascading Delete).",
                    "Sim, Apagar benchmark",
                    "Cancelar",
                    "Apagar Benchmark");

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
                    Console.WriteLine("✅ Copiado com sucesso!");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao copiar para o Clipboard.");
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
                if (resposta.GeminiRating is < 1 or > 5 || resposta.ChatGptRating is < 1 or > 5)
                {
                    if (DialogService != null)
                    {
                        await DialogService.ShowErrorAsync("O rating introduzido deve situar-se estritamente entre 1 e 5.", "Erro de Validação");
                    }
                    return;
                }

                bool guardado = await BenchmarkRepo.UpdateResponseEvaluationAsync(resposta);

                if (guardado && DialogService != null)
                {
                    await DialogService.ShowInfoAsync($"Avaliação do modelo {resposta.NomeModelo} atualizada com sucesso no SQLite.", "Sucesso");
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"[SQLITE ERROR] Falha ao atualizar avaliação: {ex.Message}");
            }
        }

        private async Task CopyPromptForEvaluationAsync(string originalPrompt, string modelResponse, string modelName)
        {
            if (string.IsNullOrEmpty(originalPrompt) || string.IsNullOrEmpty(modelResponse))
                return;

            isCreatingPrompt = true;

            try
            {
                var metadata = await GpuService.GetExtendedModelMetadataAsync(modelName);

                var trainingYear = metadata.TrainingYear;

                var formattedPrompt = await EvaluatePromptTemplate.EvaluationCopyPromptAsync(
                    originalPrompt, modelResponse, trainingYear, modelName);

                await CopyToClipboardAsync(formattedPrompt);

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro ao copiar prompt para avaliação.");
            }
            finally
            {
                isCreatingPrompt = false;
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