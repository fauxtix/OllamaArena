using Dapper;
using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Data;
using System.Globalization;
using System.Text;

namespace OllamaArena.Services.Implementations.Repositories;

/// <summary>
/// IBenchmarkStatsRepository
/// </summary>
public class BenchmarkStatsRepository : IBenchmarkStatsRepository
{
    private readonly IDapperContext _context;

    // Pesos das 12 métricas, configuráveis em appsettings.json (secção JudgeScoreWeights).
    private readonly JudgeScoreWeights _pesosJuiz;

    public BenchmarkStatsRepository(IDapperContext context, IOptions<JudgeScoreWeights> pesos)
    {
        _context = context;
        _pesosJuiz = pesos?.Value ?? new JudgeScoreWeights();
    }
    public async Task<string?> GetBestModelAsync()
    {
        var ranking = await GetModelRankingAsync();
        return ranking.FirstOrDefault()?.Model;
    }

    /// <summary>
    /// Ranking agregado por modelo, com score calculado pelo ScoreCalculator
    /// (média ponderada das 12 métricas por juiz). Numa recusa correta marcada
    /// pelo juiz (REFUSAL_HANDLED), Factual/Compliance/Relevance são excluídas da
    /// ponderação. Quando uma resposta não tem métricas suficientes para o cálculo
    /// ponderado, usa o FINAL_SCORE declarado pelo próprio juiz como fallback.
    /// </summary>
    public async Task<List<ModelRanking>> GetModelRankingAsync()
    {
        var sql = @"SELECT NomeModelo,
            GeminiFactualRating, GeminiFormattingRating, GeminiComplianceRating, GeminiRelevanceRating,
            GeminiToneRating, GeminiConcisenessRating, GeminiClarityRating, GeminiReadabilityRating,
            GeminiHaloEffectRating, GeminiSafetyRating, GeminiLanguageConsistencyRating, GeminiLoopDetectionRating,
            GeminiRefusalHandled,
            CAST(GeminiRating AS REAL) AS GeminiRating,
            OpenRouterFactualRating, OpenRouterFormattingRating, OpenRouterComplianceRating, OpenRouterRelevanceRating,
            OpenRouterToneRating, OpenRouterConcisenessRating, OpenRouterClarityRating, OpenRouterReadabilityRating,
            OpenRouterHaloEffectRating, OpenRouterSafetyRating, OpenRouterLanguageConsistencyRating, OpenRouterLoopDetectionRating,
            OpenRouterRefusalHandled,
            CAST(OpenRouterRating AS REAL) AS OpenRouterRating,
            TokensPorSegundo, TempoPuroMs, TamanhoTokens
            FROM Respostas;";

        using var connection = _context.CreateConnection();
        var rows = (await connection.QueryAsync<BenchmarkResponse>(sql)).ToList();

        var ranking = new List<ModelRanking>();

        foreach (var grupo in rows.GroupBy(r => r.NomeModelo))
        {
            double somaGemini = 0, somaOpenRouter = 0;
            int contagemGemini = 0, contagemOpenRouter = 0;

            foreach (var resposta in grupo)
            {
                // Recusa correta: exclui Factual/Compliance/Relevance da ponderação
                // (NULL/0 mantém o comportamento antigo de peso integral).
                var geminiScore = ScoreCalculator.CalcularScoreFinal(
                                      JudgeMetricCatalog.ScoreDaEntidade(resposta, juizGemini: true), _pesosJuiz,
                                      recusaCorreta: resposta.GeminiRefusalHandled == 1)
                                  ?? resposta.GeminiRating;
                if (geminiScore.HasValue)
                {
                    somaGemini += geminiScore.Value;
                    contagemGemini++;
                }

                var openRouterScore = ScoreCalculator.CalcularScoreFinal(
                                          JudgeMetricCatalog.ScoreDaEntidade(resposta, juizGemini: false), _pesosJuiz,
                                          recusaCorreta: resposta.OpenRouterRefusalHandled == 1)
                                      ?? resposta.OpenRouterRating;
                if (openRouterScore.HasValue)
                {
                    somaOpenRouter += openRouterScore.Value;
                    contagemOpenRouter++;
                }
            }

            int totalAvaliadas = contagemGemini + contagemOpenRouter;
            if (totalAvaliadas == 0)
                continue;

            double scoreGemini = contagemGemini > 0 ? Math.Round(somaGemini / contagemGemini, 2) : 0;
            double scoreOpenRouter = contagemOpenRouter > 0 ? Math.Round(somaOpenRouter / contagemOpenRouter, 2) : 0;

            ranking.Add(new ModelRanking
            {
                Model = grupo.Key,
                Score = Math.Round((somaGemini + somaOpenRouter) / totalAvaliadas, 2),
                GeminiScore = scoreGemini,
                OpenRouterScore = scoreOpenRouter,
                TotalResponses = grupo.Count(),
                JudgedResponses = totalAvaliadas,
                TokensPerSecond = Math.Round(grupo.Average(r => r.TokensPorSegundo), 2),
                AvgResponseTimeMs = Math.Round(grupo.Average(r => r.TempoPuroMs), 0),
                AvgResponseTokens = Math.Round(grupo.Average(r => r.TamanhoTokens), 0),
                JudgeDivergence = contagemGemini > 0 && contagemOpenRouter > 0
                    ? Math.Round(Math.Abs(scoreGemini - scoreOpenRouter), 2)
                    : null
            });
        }

        return ranking.OrderByDescending(r => r.Score).ToList();
    }

    /// <summary>
    /// Estatísticas globais para os cartões do Dashboard: atividade de prompts
    /// (total e data do último) e cobertura de avaliação (respostas com os dois juízes).
    /// </summary>
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        using var connection = _context.CreateConnection();
        var stats = new DashboardStats
        {
            TotalPrompts = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Prompts;"),
            TotalRespostas = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Respostas;")
        };

        // Critério consistente com o ranking: resposta avaliada pelo juiz X tem
        // nota global declarada ou métricas detalhadas desse juiz.
        const string criterioGemini = "(GeminiRating IS NOT NULL OR GeminiFactualRating IS NOT NULL)";
        const string criterioOpenRouter = "(OpenRouterRating IS NOT NULL OR OpenRouterFactualRating IS NOT NULL)";
        stats.RespostasAvaliadasAmbosJuizes = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM Respostas WHERE {criterioGemini} AND {criterioOpenRouter};");

        // DataCriacao é gravada em ISO 8601 UTC ("o"); cair para Parse invariante se necessário.
        var ultimaRaw = await connection.ExecuteScalarAsync<string?>("SELECT MAX(DataCriacao) FROM Prompts;");
        if (!string.IsNullOrEmpty(ultimaRaw))
        {
            stats.UltimaAtividade = DateTimeOffset.TryParse(ultimaRaw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto)
                ? dto.UtcDateTime
                : null;
        }

        return stats;
    }

    private sealed class TimelineRow : BenchmarkResponse
    {
        public DateTime DataCriacao { get; set; }
    }

    /// <summary>
    /// Série temporal do score por modelo para o gráfico "Evolução do score":
    /// score combinado por resposta (mesmo critério do ranking) e média acumulada
    /// por modelo ao longo das datas dos prompts — mostra tendência em vez de ruído.
    /// </summary>
    public async Task<List<ModelScoreTimeline>> GetScoreTimelineAsync()
    {
        var sql = @"SELECT R.NomeModelo,
            P.DataCriacao,
            GeminiFactualRating, GeminiFormattingRating, GeminiComplianceRating, GeminiRelevanceRating,
            GeminiToneRating, GeminiConcisenessRating, GeminiClarityRating, GeminiReadabilityRating,
            GeminiHaloEffectRating, GeminiSafetyRating, GeminiLanguageConsistencyRating, GeminiLoopDetectionRating,
            GeminiRefusalHandled,
            CAST(GeminiRating AS REAL) AS GeminiRating,
            OpenRouterFactualRating, OpenRouterFormattingRating, OpenRouterComplianceRating, OpenRouterRelevanceRating,
            OpenRouterToneRating, OpenRouterConcisenessRating, OpenRouterClarityRating, OpenRouterReadabilityRating,
            OpenRouterHaloEffectRating, OpenRouterSafetyRating, OpenRouterLanguageConsistencyRating, OpenRouterLoopDetectionRating,
            OpenRouterRefusalHandled,
            CAST(OpenRouterRating AS REAL) AS OpenRouterRating
            FROM Respostas R
            INNER JOIN Prompts P ON P.Id = R.PromptId
            ORDER BY P.DataCriacao;";

        using var connection = _context.CreateConnection();
        var rows = (await connection.QueryAsync<TimelineRow>(sql)).ToList();

        var resultado = new List<ModelScoreTimeline>();

        foreach (var grupo in rows.GroupBy(r => r.NomeModelo))
        {
            double somaAcumulada = 0;
            int amostras = 0;
            var pontos = new List<TimelinePoint>();

            foreach (var resposta in grupo.OrderBy(r => r.DataCriacao))
            {
                var geminiScore = ScoreCalculator.CalcularScoreFinal(
                                      JudgeMetricCatalog.ScoreDaEntidade(resposta, juizGemini: true), _pesosJuiz,
                                      recusaCorreta: resposta.GeminiRefusalHandled == 1)
                                  ?? resposta.GeminiRating;
                var openRouterScore = ScoreCalculator.CalcularScoreFinal(
                                          JudgeMetricCatalog.ScoreDaEntidade(resposta, juizGemini: false), _pesosJuiz,
                                          recusaCorreta: resposta.OpenRouterRefusalHandled == 1)
                                      ?? resposta.OpenRouterRating;

                // Score combinado da resposta: média dos juízes disponíveis.
                double? scoreResposta = (geminiScore.HasValue, openRouterScore.HasValue) switch
                {
                    (true, true) => (geminiScore!.Value + openRouterScore!.Value) / 2,
                    (true, false) => geminiScore,
                    (false, true) => openRouterScore,
                    _ => null
                };

                if (!scoreResposta.HasValue)
                    continue;

                somaAcumulada += scoreResposta.Value;
                amostras++;
                pontos.Add(new TimelinePoint
                {
                    Data = resposta.DataCriacao,
                    ScoreAcumulado = Math.Round(somaAcumulada / amostras, 2)
                });
            }

            if (pontos.Count > 0)
                resultado.Add(new ModelScoreTimeline { Model = grupo.Key, Points = pontos });
        }

        return resultado;
    }

    /// <summary>
    /// Dados agregados por modelo para os gráficos do Dashboard: score ponderado,
    /// média de tokens/s e médias das 12 métricas por juiz (radar e scatter).
    /// </summary>
    public async Task<List<ModelChartData>> GetModelChartDataAsync()
    {
        var sql = @"SELECT NomeModelo,
            GeminiFactualRating, GeminiFormattingRating, GeminiComplianceRating, GeminiRelevanceRating,
            GeminiToneRating, GeminiConcisenessRating, GeminiClarityRating, GeminiReadabilityRating,
            GeminiHaloEffectRating, GeminiSafetyRating, GeminiLanguageConsistencyRating, GeminiLoopDetectionRating,
            GeminiRefusalHandled,
            CAST(GeminiRating AS REAL) AS GeminiRating,
            OpenRouterFactualRating, OpenRouterFormattingRating, OpenRouterComplianceRating, OpenRouterRelevanceRating,
            OpenRouterToneRating, OpenRouterConcisenessRating, OpenRouterClarityRating, OpenRouterReadabilityRating,
            OpenRouterHaloEffectRating, OpenRouterSafetyRating, OpenRouterLanguageConsistencyRating, OpenRouterLoopDetectionRating,
            OpenRouterRefusalHandled,
            CAST(OpenRouterRating AS REAL) AS OpenRouterRating,
            TokensPorSegundo
            FROM Respostas;";

        using var connection = _context.CreateConnection();
        var rows = (await connection.QueryAsync<BenchmarkResponse>(sql)).ToList();

        var resultado = new List<ModelChartData>();

        foreach (var grupo in rows.GroupBy(r => r.NomeModelo))
        {
            double somaGemini = 0, somaOpenRouter = 0;
            int contagemGemini = 0, contagemOpenRouter = 0;

            foreach (var resposta in grupo)
            {
                // Recusa correta: exclui Factual/Compliance/Relevance da ponderação
                // (NULL/0 mantém o comportamento antigo de peso integral).
                var geminiScore = ScoreCalculator.CalcularScoreFinal(
                                      JudgeMetricCatalog.ScoreDaEntidade(resposta, juizGemini: true), _pesosJuiz,
                                      recusaCorreta: resposta.GeminiRefusalHandled == 1)
                                  ?? resposta.GeminiRating;
                if (geminiScore.HasValue)
                {
                    somaGemini += geminiScore.Value;
                    contagemGemini++;
                }

                var openRouterScore = ScoreCalculator.CalcularScoreFinal(
                                          JudgeMetricCatalog.ScoreDaEntidade(resposta, juizGemini: false), _pesosJuiz,
                                          recusaCorreta: resposta.OpenRouterRefusalHandled == 1)
                                      ?? resposta.OpenRouterRating;
                if (openRouterScore.HasValue)
                {
                    somaOpenRouter += openRouterScore.Value;
                    contagemOpenRouter++;
                }
            }

            int totalAvaliadas = contagemGemini + contagemOpenRouter;
            if (totalAvaliadas == 0)
                continue;

            resultado.Add(new ModelChartData
            {
                Model = grupo.Key,
                Score = Math.Round((somaGemini + somaOpenRouter) / totalAvaliadas, 2),
                TokensPorSegundo = Math.Round(grupo.Average(r => r.TokensPorSegundo), 2),
                GeminiCount = contagemGemini,
                OpenRouterCount = contagemOpenRouter,
                GeminiMetrics = MediarMetricas(grupo, juizGemini: true),
                OpenRouterMetrics = MediarMetricas(grupo, juizGemini: false)
            });
        }

        return resultado.OrderByDescending(r => r.Score).ToList();
    }

    private static List<double> MediarMetricas(IEnumerable<BenchmarkResponse> respostas, bool juizGemini)
    {
        return JudgeMetricCatalog.ParesEntidade.Select(par =>
        {
            var seletor = juizGemini ? par.Gemini : par.OpenRouter;
            var valores = respostas.Where(r => seletor(r).HasValue).Select(r => (double)seletor(r)!.Value).ToList();
            return valores.Count > 0 ? Math.Round(valores.Average(), 2) : 0;
        }).ToList();
    }
}
