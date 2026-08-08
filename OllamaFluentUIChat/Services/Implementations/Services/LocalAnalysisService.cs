using Microsoft.Extensions.Localization;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Resources;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Diagnostics;
using System.Text;

namespace OllamaFluentUIChat.Services.Implementations.Services;

public class LocalAnalysisService : IAnalysisService
{
    private readonly ILogger<LocalAnalysisService> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public LocalAnalysisService(HttpClient httpClient, ILogger<LocalAnalysisService> logger, IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _localizer = localizer;
    }

    public Task<BenchmarkAnalysisResult> AnalisarBenchmarksAsync(
        List<BenchmarkEvaluationModel> benchmarks,
        CancellationToken cancellationToken = default)
    {
        if (benchmarks == null || benchmarks.Count == 0)
        {
            _logger.LogWarning("Nenhum benchmark para analisar.");
            return Task.FromResult(new BenchmarkAnalysisResult
            {
                Sumario = _localizer["Analysis.NoData"]
            });
        }

        var stopwatch = Stopwatch.StartNew();

        // 1. Métricas de Velocidade
        var modeloMaisRapido = benchmarks
            .OrderByDescending(b => b.TokensPorSegundo)
            .First();

        var modeloMaisLento = benchmarks
            .OrderBy(b => b.TokensPorSegundo)
            .First();

        // 2. Média Global de Qualidade e Consenso
        var modelosComAvaliacao = benchmarks.Select(b =>
        {
            double geminiFactual = Convert.ToDouble(b.GeminiFactualRating);
            double openRouterFactual = Convert.ToDouble(b.OpenRouterFactualRating);
            double geminiRating = Convert.ToDouble(b.GeminiRating);
            double openRouterRating = Convert.ToDouble(b.OpenRouterRating);

            return new
            {
                Benchmark = b,
                AvgRating = (geminiRating + geminiFactual + openRouterRating + openRouterFactual) / 4.0,
                DiferencaFactual = Math.Abs(geminiFactual - openRouterFactual)
            };
        }).OrderByDescending(x => x.AvgRating).ToList();

        var modeloMelhorAvaliado = modelosComAvaliacao.First();

        // 3. Geração do Sumário Executivo
        var sumarioBuilder = new StringBuilder();
        sumarioBuilder.Append(_localizer["Analysis.SummaryFastest",
            modeloMaisRapido.NomeModelo,
            modeloMaisRapido.TokensPorSegundo.ToString("F1"),
            FormatarTempo(modeloMaisRapido.TempoPuroMs)]);
        sumarioBuilder.Append(" ");

        if (modeloMelhorAvaliado.Benchmark.NomeModelo == modeloMaisRapido.NomeModelo)
        {
            sumarioBuilder.Append(_localizer["Analysis.SummaryAlsoBest",
                modeloMelhorAvaliado.Benchmark.NomeModelo,
                modeloMelhorAvaliado.AvgRating.ToString("F2")]);
        }
        else
        {
            sumarioBuilder.Append(_localizer["Analysis.SummaryBestRated",
                modeloMelhorAvaliado.Benchmark.NomeModelo,
                modeloMelhorAvaliado.AvgRating.ToString("F2")]);
        }
        sumarioBuilder.Append(" ");

        sumarioBuilder.Append(_localizer["Analysis.SummarySlowest",
            modeloMaisLento.NomeModelo,
            modeloMaisLento.TokensPorSegundo.ToString("F1"),
            FormatarTempo(modeloMaisLento.TempoPuroMs)]);

        // 4. Análise de Empates e Consenso
        var maxGeminiFactual = benchmarks.Max(b => b.GeminiFactualRating) ?? 0;
        var melhoresGemini = benchmarks.Where(b => b.GeminiFactualRating == maxGeminiFactual).Select(b => b.NomeModelo).ToList();
        string textoGemini = melhoresGemini.Count > 1
            ? _localizer["Analysis.GeminiTie", string.Join(" e ", melhoresGemini), maxGeminiFactual]
            : _localizer["Analysis.GeminiBest", melhoresGemini.First(), maxGeminiFactual];

        var maxOpenRouterFactual = benchmarks.Max(b => b.OpenRouterFactualRating) ?? 0;
        var melhoresOpenRouter = benchmarks.Where(b => b.OpenRouterFactualRating == maxOpenRouterFactual).Select(b => b.NomeModelo).ToList();
        string textoOpenRouter = melhoresOpenRouter.Count > 1
            ? _localizer["Analysis.OpenRouterTie", string.Join(" e ", melhoresOpenRouter), maxOpenRouterFactual]
            : _localizer["Analysis.OpenRouterBest", melhoresOpenRouter.First(), maxOpenRouterFactual];

        var modeloConsenso = modelosComAvaliacao.FirstOrDefault(m => m.Benchmark.NomeModelo == modeloMelhorAvaliado.Benchmark.NomeModelo);
        string textoConsenso = (modeloConsenso != null && modeloConsenso.DiferencaFactual == 0)
            ? _localizer["Analysis.ConsensusHigh", modeloConsenso.Benchmark.GeminiFactualRating ?? 0]
            : _localizer["Analysis.ConsensusDivergent",
                modeloConsenso?.Benchmark.GeminiFactualRating ?? 0,
                modeloConsenso?.Benchmark.OpenRouterFactualRating ?? 0];

        // 5. Linhas da Análise Detalhada
        var linhasDetalhes = new List<string>();

        // a) Velocidade
        string textoVelocidade = _localizer["Analysis.DetailSpeed",
            modeloMaisRapido.NomeModelo,
            modeloMaisRapido.TokensPorSegundo.ToString("F1"),
            FormatarTempo(modeloMaisRapido.TempoPuroMs)];
        if (benchmarks.Count > 1)
        {
            var diferencaVelocidade = modeloMaisRapido.TokensPorSegundo - modeloMaisLento.TokensPorSegundo;
            textoVelocidade += _localizer["Analysis.DetailSpeedComparison",
                modeloMaisLento.NomeModelo,
                diferencaVelocidade.ToString("F1"),
                FormatarTempo(modeloMaisLento.TempoPuroMs)];
        }
        linhasDetalhes.Add(textoVelocidade);

        // b) Avaliações e Consenso
        linhasDetalhes.Add(_localizer["Analysis.DetailGemini", textoGemini]);
        linhasDetalhes.Add(_localizer["Analysis.DetailOpenRouter", textoOpenRouter]);
        linhasDetalhes.Add(_localizer["Analysis.DetailConsensus", textoConsenso]);

        // c) Recomendação Dinâmica com Salvaguarda Global de Baixo Desempenho
        bool todosComQualidadeBaixa = modelosComAvaliacao.All(m => m.AvgRating < 4.0);

        if (todosComQualidadeBaixa)
        {
            // Ativa quando nenhum modelo atinge pelo menos 4.0/5 de média
            linhasDetalhes.Add(_localizer["Analysis.DetailAlertLowQuality"]);
        }
        else
        {
            double ganhoVelocidade = modeloMaisLento.TokensPorSegundo > 0
                ? modeloMaisRapido.TokensPorSegundo / modeloMaisLento.TokensPorSegundo
                : 1.0;

            string recomendacaoPrompt;
            if (modeloMaisRapido.NomeModelo != modeloMelhorAvaliado.Benchmark.NomeModelo)
            {
                recomendacaoPrompt = _localizer["Analysis.DetailRecommendationSplit",
                    modeloMaisRapido.NomeModelo,
                    ganhoVelocidade.ToString("F1"),
                    modeloMelhorAvaliado.Benchmark.NomeModelo];
            }
            else
            {
                string textoVelocidadeGanho = benchmarks.Count > 1
                    ? _localizer["Analysis.DetailSpeedMultiplier", ganhoVelocidade.ToString("F1")]
                    : "";
                recomendacaoPrompt = _localizer["Analysis.DetailRecommendationSingle",
                    modeloMaisRapido.NomeModelo,
                    textoVelocidadeGanho];
            }

            linhasDetalhes.Add(recomendacaoPrompt);
        }

        stopwatch.Stop();

        return Task.FromResult(new BenchmarkAnalysisResult
        {
            Sumario = sumarioBuilder.ToString(),
            LinhasAnaliseDetalhada = linhasDetalhes,
            ModeloMaisRapido = modeloMaisRapido.NomeModelo,
            MaxTokensSec = Math.Round(modeloMaisRapido.TokensPorSegundo, 2),
            ModeloMelhorAvaliado = $"{modeloMelhorAvaliado.Benchmark.NomeModelo} ({modeloMelhorAvaliado.AvgRating.ToString("F2")}/5)",
            TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss")
        });
    }

    private static string FormatarTempo(double tempoMs)
    {
        var ts = TimeSpan.FromMilliseconds(tempoMs);
        return ts.TotalSeconds >= 60
            ? $"{ts.Minutes}m {ts.Seconds}s"
            : $"{ts.TotalSeconds:F1}s";
    }
}
