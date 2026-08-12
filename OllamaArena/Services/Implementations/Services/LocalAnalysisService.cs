using Microsoft.Extensions.Localization;
using OllamaArena.Models.DTO;
using OllamaArena.Resources;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Diagnostics;
using System.Text;

namespace OllamaArena.Services.Implementations.Services;

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
        bool existeAvaliacao = modeloMelhorAvaliado.AvgRating > 0;

        // 3. Geração do Sumário Executivo
        var sumarioBuilder = new StringBuilder();
        sumarioBuilder.Append(_localizer["Analysis.SummaryFastest",
            modeloMaisRapido.NomeModelo,
            modeloMaisRapido.TokensPorSegundo.ToString("F1"),
            FormatarTempo(modeloMaisRapido.TempoPuroMs)]);
        sumarioBuilder.Append(" ");

        if (existeAvaliacao)
        {
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
        }
        else
        {
            sumarioBuilder.Append(_localizer["Analysis.NoBestRated"]);
        }
        sumarioBuilder.Append(" ");

        sumarioBuilder.Append(_localizer["Analysis.SummarySlowest",
            modeloMaisLento.NomeModelo,
            modeloMaisLento.TokensPorSegundo.ToString("F1"),
            FormatarTempo(modeloMaisLento.TempoPuroMs)]);

        // 4. Análise de Empates e Consenso (com salvaguarda quando não há avaliações factuais)
        string textoGemini = DestaqueFactual(
            _localizer["Analysis.NoFactualRatings"],
            benchmarks,
            b => b.GeminiFactualRating,
            (modelo, nota) => _localizer["Analysis.GeminiBest", modelo, nota],
            (modelos, nota) => _localizer["Analysis.GeminiTie", string.Join(" e ", modelos), nota]);

        string textoOpenRouter = DestaqueFactual(
            _localizer["Analysis.NoFactualRatings"],
            benchmarks,
            b => b.OpenRouterFactualRating,
            (modelo, nota) => _localizer["Analysis.OpenRouterBest", modelo, nota],
            (modelos, nota) => _localizer["Analysis.OpenRouterTie", string.Join(" e ", modelos), nota]);

        string textoConsenso;
        var geminiFactualMelhor = modeloMelhorAvaliado.Benchmark.GeminiFactualRating;
        var openRouterFactualMelhor = modeloMelhorAvaliado.Benchmark.OpenRouterFactualRating;
        if (geminiFactualMelhor.HasValue && openRouterFactualMelhor.HasValue)
        {
            textoConsenso = geminiFactualMelhor.Value == openRouterFactualMelhor.Value
                ? _localizer["Analysis.ConsensusHigh", geminiFactualMelhor.Value]
                : _localizer["Analysis.ConsensusDivergent", geminiFactualMelhor.Value, openRouterFactualMelhor.Value];
        }
        else
        {
            textoConsenso = _localizer["Analysis.NoFactualRatings"];
        }

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
        if (!existeAvaliacao)
        {
            linhasDetalhes.Add(_localizer["Analysis.NoBestRated"]);
        }
        else
        {
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
        }

        stopwatch.Stop();

        return Task.FromResult(new BenchmarkAnalysisResult
        {
            Sumario = sumarioBuilder.ToString(),
            LinhasAnaliseDetalhada = linhasDetalhes,
            ModeloMaisRapido = modeloMaisRapido.NomeModelo,
            MaxTokensSec = Math.Round(modeloMaisRapido.TokensPorSegundo, 2),
            ModeloMelhorAvaliado = existeAvaliacao
                ? $"{modeloMelhorAvaliado.Benchmark.NomeModelo} ({modeloMelhorAvaliado.AvgRating.ToString("F2")}/5)"
                : _localizer["Analysis.NoBestRated"],
            TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss")
        });
    }

    /// <summary>
    /// Devolve o destaque factual (melhor modelo ou empate) de um juiz, ou uma
    /// mensagem de fallback quando ainda não existem avaliações factuais.
    /// </summary>
    private static string DestaqueFactual(
        string mensagemSemDados,
        List<BenchmarkEvaluationModel> benchmarks,
        Func<BenchmarkEvaluationModel, int?> obterNota,
        Func<string, int, string> textoUnico,
        Func<List<string>, int, string> textoEmpate)
    {
        var comAvaliacao = benchmarks
            .Where(b => obterNota(b).HasValue)
            .Select(b => (Modelo: b.NomeModelo, Nota: obterNota(b)!.Value))
            .ToList();

        if (comAvaliacao.Count == 0)
            return mensagemSemDados;

        int maxNota = comAvaliacao.Max(x => x.Nota);
        var melhores = comAvaliacao.Where(x => x.Nota == maxNota).Select(x => x.Modelo).ToList();

        return melhores.Count > 1
            ? textoEmpate(melhores, maxNota)
            : textoUnico(melhores[0], maxNota);
    }

    private static string FormatarTempo(double tempoMs)
    {
        var ts = TimeSpan.FromMilliseconds(tempoMs);
        return ts.TotalSeconds >= 60
            ? $"{ts.Minutes}m {ts.Seconds}s"
            : $"{ts.TotalSeconds:F1}s";
    }
}
