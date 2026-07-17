using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.PromptTemplates;
using OllamaFluentUIChat.Services.Implementations.Services;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Text;

namespace OllamaFluentUIChat.Components.Pages.Components
{
    public partial class Benchmarks
    {
        [Inject]
        public IOllamaGpuService GpuService { get; set; }
        private List<BenchmarkPrompt>? _promptsList;
        private BenchmarkPrompt? _selectedPrompt;
        private BenchmarkPrompt? _promptGrafico;
        private bool _mostrarGrafico = false;
        private bool _chartDialogVisible = false;
        private BenchmarkPrompt? _chartPrompt;
        private bool isLoading = false;

        private BenchmarkResponse? _selectedEvaluation;
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
            _promptsList = null;
            _promptsList = await BenchmarkRepo.GetAllBenchmarksAsync();

            if (_selectedPrompt != null && _promptsList != null)
            {
                _selectedPrompt = _promptsList.FirstOrDefault(p => p.Id == _selectedPrompt.Id);
            }

            isLoading = false; 
            StateHasChanged();
        }


        private void SelectPrompt(BenchmarkPrompt prompt)
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
            if (prompt.Answers == null || !prompt.Answers.Any())
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
                    $"Tem a certeza que deseja apagar o Prompt #{prompt.Id}? " +
                    $"Isto irá remover permanentemente todas as {totalRespostas} respostas associadas (Cascading Delete).",
                    "Sim, Apagar",
                    "Cancelar",
                    "Apagar Prompt");

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

            try
            {
                // 1. Força o foco na janela para o browser dar permissão de escrita
                await Task.Delay(100);
                await JS.InvokeVoidAsync("window.focus");

                // 2. Executa a cópia
                await JS.InvokeVoidAsync("navigator.clipboard.writeText", texto);

                // 3. Dá tempo ao Sistema Operativo para atualizar a área de transferência
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao tentar copiar texto para o Clipboard.");
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
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"[SQLITE ERROR] Falha ao atualizar avaliação: {ex.Message}");
            }
        }

        private async Task CopyPromptForEvaluationAsync(string originalPrompt, string modelResponse)
        {
            var modelName = _selectedEvaluation?.NomeModelo ?? "Modelo Desconhecido";
            var metadata = await GpuService.GetExtendedModelMetadataAsync(modelName);
            var trainingYear = metadata.TrainingYear;
            var formattedPrompt =  EvaluatePromptTemplate.EvaluationCopyPrompt(originalPrompt, modelResponse, trainingYear);
            await CopyToClipboardAsync(formattedPrompt);
        }

        private void OpenEvaluation(BenchmarkResponse resp)
        {
            _selectedEvaluation = resp;
            _evaluationDialogVisible = true;
        }
        private Task CloseDialogAsync()
        {
            _evaluationDialogVisible = false;
            return Task.CompletedTask;
        }
    }
}