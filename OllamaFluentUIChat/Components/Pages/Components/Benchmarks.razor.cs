using Microsoft.JSInterop;
using OllamaFluentUIChat.Models.Entities;

namespace OllamaFluentUIChat.Components.Pages.Components
{
    public partial class Benchmarks
    {

        private List<BenchmarkPrompt>? _promptsList;
        private BenchmarkPrompt? _selectedPrompt;
        private BenchmarkPrompt? _promptGrafico;
        private bool _mostrarGrafico = false;
        private bool _dialogGraficoVisible = false;
        private BenchmarkPrompt? _promptParaGrafico;

        protected override async Task OnInitializedAsync() => await GetDataAsync();

        private async Task GetDataAsync()
        {
            _promptsList = null;
            StateHasChanged();
            _promptsList = await BenchmarkRepo.GetAllBenchmarksAsync();

            if (_selectedPrompt != null && _promptsList != null)
            {
                _selectedPrompt = _promptsList.FirstOrDefault(p => p.Id == _selectedPrompt.Id);
            }

            StateHasChanged();
        }

        private void SeleccionarPrompt(BenchmarkPrompt prompt)
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
            await RenderizarGraficoPromptAsync(prompt);
        }

        private async Task RenderizarGraficoPromptAsync(BenchmarkPrompt prompt)
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
                    label = resp.ModeloNome,
                    borderColor = GerarCorAleatoria(),
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

        private string GerarCorAleatoria()
        {
            var rnd = new Random();
            return $"rgb({rnd.Next(255)}, {rnd.Next(255)}, {rnd.Next(255)})";
        }

        private async Task ApagarPromptAsync(BenchmarkPrompt prompt)
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

        private void AbrirDialogoGrafico(BenchmarkPrompt prompt)
        {
            _promptParaGrafico = prompt;
            _dialogGraficoVisible = true;
        }

        private async Task GuardarAvaliacaoAsync(BenchmarkResponse resposta)
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
                    Logger.LogInformation($"[BENCHMARK] Avaliação do modelo {resposta.ModeloNome} guardada com sucesso.");

                    await DialogService.ShowInfoAsync($"Avaliação do modelo {resposta.ModeloNome} atualizada com sucesso no SQLite.", "Sucesso");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"[SQLITE ERROR] Falha ao atualizar avaliação: {ex.Message}");
            }
        }
    }
}