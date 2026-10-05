using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Resources;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Diagnostics;
using System.Text;

namespace OllamaArena.Services.Implementations.Services;

public class LocalAnalysisService : IAnalysisService
{
    private readonly ILogger<LocalAnalysisService> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    // Pesos das 12 métricas, configuráveis em appsettings.json (secção JudgeScoreWeights) —
    // os mesmos usados pelo ranking do Dashboard, para coerência entre análises.
    private readonly JudgeScoreWeights _pesosJuiz;

    public LocalAnalysisService(ILogger<LocalAnalysisService> logger, IStringLocalizer<SharedResources> localizer, IOptions<JudgeScoreWeights> pesos)
    {
        _logger = logger;
        _localizer = localizer;
        _pesosJuiz = pesos?.Value ?? new JudgeScoreWeights();
    }

    /// <summary>
    /// Análise 100% local (sem LLM). É genuinamente assíncrona e honra o
    /// <paramref name="cancellationToken"/>: entre fases cede a thread
    /// (<see cref="Task.Yield()"/>) e valida o cancelamento, o que dá ao botão
    /// "Cancelar" a hipótese de ser clicado enquanto a análise corre. A versão
    /// anterior era síncrona (Task.FromResult) e ignorava o token — o botão de
    /// cancelar não fazia nada e o clique nem sequer chegava a ser processado.
    /// </summary>
    public async Task<BenchmarkAnalysisResult> AnalisarBenchmarksAsync(
        IReadOnlyList<BenchmarkEvaluationModel> benchmarks,
        CancellationToken cancellationToken = default)
    {
        if (benchmarks is null || benchmarks.Count == 0)
        {
            _logger.LogWarning("Nenhum benchmark para analisar.");
            return new BenchmarkAnalysisResult { Sumario = _localizer["Analysis.NoData"] };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        // 1. Métricas de Velocidade
        var modeloMaisRapido = benchmarks.MaxBy(b => b.TokensPorSegundo)!;
        var modeloMaisLento = benchmarks.MinBy(b => b.TokensPorSegundo)!;

        // 2. Média Global de Qualidade e Consenso
        // Score ponderado por juiz (mesma metodologia do ScoreCalculator usado no
        // Dashboard), com fallback ao rating declarado quando as métricas são
        // insuficientes — substitui a média antiga que contava o factual em duplicado.
        var modelosComAvaliacao = benchmarks.Select(b => Avaliar(b)).OrderByDescending(x => x.AvgRating).ToList();
        var modeloMelhorAvaliado = modelosComAvaliacao[0];

        // Existe avaliação = algum juiz devolveu nota. Um modelo avaliado com 0 não é
        // confundido com "sem avaliação" (a média pode legitimamente ser 0).
        bool existeAvaliacao = modelosComAvaliacao.Any(x => x.TemAlgumJuiz);

        await YieldAsync(cancellationToken);

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

        await YieldAsync(cancellationToken);

        // 5. Linhas da Análise Detalhada
        // Linhas com rótulo + texto, sem HTML: a marcação é gerada na vista.
        var linhasDetalhes = new List<BenchmarkAnalysisLine>();

        // Os recursos localizados trazem <strong>/<em>; a linha guarda o rótulo à
        // parte e o texto simples, e quem decide como apresentar é a vista.
        void Adicionar(LocalizedString texto) => linhasDetalhes.Add(BenchmarkAnalysisLine.Criar(texto.Value));

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
        linhasDetalhes.Add(BenchmarkAnalysisLine.Criar(textoVelocidade));

        // b) Avaliações e Consenso
        Adicionar(_localizer["Analysis.DetailGemini", textoGemini]);
        Adicionar(_localizer["Analysis.DetailOpenRouter", textoOpenRouter]);
        Adicionar(_localizer["Analysis.DetailConsensus", textoConsenso]);

        // c) Recomendação Dinâmica com Salvaguarda Global de Baixo Desempenho
        if (!existeAvaliacao)
        {
            Adicionar(_localizer["Analysis.NoBestRated"]);
        }
        else
        {
            bool todosComQualidadeBaixa = modelosComAvaliacao.All(m => m.AvgRating < 4.0);

            if (todosComQualidadeBaixa)
            {
                // Ativa quando nenhum modelo atinge pelo menos 4.0/5 de média
                Adicionar(_localizer["Analysis.DetailAlertLowQuality"]);
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

                linhasDetalhes.Add(BenchmarkAnalysisLine.Criar(recomendacaoPrompt));
            }
        }

        await YieldAsync(cancellationToken);

        // 6. Métricas com Desempenho Inferior (<= 3, exceto Safety <= 2).
        // As 12 métricas e os seus limiares vêm de JudgeMetricCatalog: uma métrica nova
        // passa a ser sinalizada aqui sem tocar neste método.
        var fracasPorModelo = new Dictionary<string, List<string>>();

        foreach (var benchmark in benchmarks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fracas = new List<string>();

            foreach (var metrica in JudgeMetricCatalog.Todas)
            {
                int? gVal = metrica.Gemini(benchmark);
                int? oVal = metrica.OpenRouter(benchmark);
                bool gFraca = gVal.HasValue && gVal.Value <= metrica.NotaFraca;
                bool oFraca = oVal.HasValue && oVal.Value <= metrica.NotaFraca;

                if (gFraca || oFraca)
                {
                    string detalhes = (gFraca, oFraca) switch
                    {
                        (true, true) => $"Gemini={gVal}, OpenRouter={oVal}",
                        (true, false) => $"Gemini={gVal}",
                        _ => $"OpenRouter={oVal}"
                    };
                    fracas.Add($"{metrica.Rotulo(_localizer)} [{detalhes}]");
                }
            }

            if (fracas.Count == 0)
                continue;

            // Acumula por modelo: a versão anterior fazia
            // metricasFracasPorModelo[nome] = fracas e o benchmark seguinte do mesmo
            // modelo apagava as métricas fracas do anterior (dado perdido em silêncio).
            if (!fracasPorModelo.TryGetValue(benchmark.NomeModelo, out var doModelo))
            {
                doModelo = new List<string>();
                fracasPorModelo[benchmark.NomeModelo] = doModelo;
            }

            doModelo.AddRange(fracas);
        }

        if (fracasPorModelo.Count > 0)
        {
            Adicionar(_localizer["Analysis.WeakMetricsHeader"]);
            foreach (var (modelo, lista) in fracasPorModelo)
            {
                Adicionar(_localizer["Analysis.WeakMetricsModel", modelo, string.Join(", ", lista)]);
            }
        }
        else
        {
            Adicionar(_localizer["Analysis.WeakMetricsNone"]);
        }

        stopwatch.Stop();

        return new BenchmarkAnalysisResult
        {
            Sumario = sumarioBuilder.ToString(),
            LinhasAnaliseDetalhada = linhasDetalhes,
            ModeloMaisRapido = modeloMaisRapido.NomeModelo,
            MaxTokensSec = Math.Round(modeloMaisRapido.TokensPorSegundo, 2),
            ModeloMelhorAvaliado = existeAvaliacao
                ? $"{modeloMelhorAvaliado.Benchmark.NomeModelo} ({modeloMelhorAvaliado.AvgRating.ToString("F2")}/5)"
                : _localizer["Analysis.NoBestRated"],
            TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss")
        };
    }

    /// <summary>
    /// Cede a thread e valida o cancelamento entre fases, para que um clique em
    /// "Cancelar" possa ser processado enquanto a análise está a decorrer.
    /// </summary>
    private static async Task YieldAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
    }

    /// <summary>
    /// Score de um modelo: média dos dois juízes, calculada sobre as 12 métricas
    /// quando existirem e, em falta delas, sobre o rating global declarado.
    /// Notas 0 contam como "sem avaliação" — é a convenção usada em toda a
    /// aplicação (SQLite devolve 0 para ratings ainda não preenchidos, ver
    /// <see cref="BenchmarkEvaluationRules.EstaAvaliada"/>).
    /// </summary>
    private (BenchmarkEvaluationModel Benchmark, double AvgRating, bool TemAlgumJuiz) Avaliar(BenchmarkEvaluationModel b)
    {
        double? geminiScore = ScoreCalculator.CalcularScoreFinal(
                                 JudgeMetricCatalog.ScoreDoModelo(b, juizGemini: true), _pesosJuiz)
                             ?? b.GeminiRating;
        double? openRouterScore = ScoreCalculator.CalcularScoreFinal(
                                      JudgeMetricCatalog.ScoreDoModelo(b, juizGemini: false), _pesosJuiz)
                                  ?? b.OpenRouterRating;

        var notas = new[] { geminiScore, openRouterScore }
            .Where(n => n is > 0)
            .Select(n => n!.Value)
            .ToList();

        return (
            b,
            notas.Count > 0 ? notas.Average() : 0d,
            notas.Count > 0);
    }

    /// <summary>
    /// Devolve o destaque factual (melhor modelo ou empate) de um juiz, ou uma
    /// mensagem de fallback quando ainda não existem avaliações factuais.
    /// </summary>
    private static string DestaqueFactual(
        string mensagemSemDados,
        IReadOnlyList<BenchmarkEvaluationModel> benchmarks,
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
        var melhores = comAvaliacao.Where(x => x.Nota == maxNota).Select(x => x.Modelo).Distinct().ToList();

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