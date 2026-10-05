using Microsoft.Extensions.Localization;
using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;
using OllamaArena.Resources;

namespace OllamaArena.Services.Helpers;

/// <summary>
/// Identificador de cada métrica avaliada pelos juízes. A ordem da enumeração é a
/// ordem de exibição (Factual, Formatting, Compliance, ..., Loop Detection).
/// </summary>
public enum JudgeMetricKey
{
    Factual,
    Formatting,
    Compliance,
    Relevance,
    Tone,
    Conciseness,
    Clarity,
    Readability,
    HaloEffect,
    Safety,
    LanguageConsistency,
    LoopDetection
}

/// <summary>
/// Uma das 12 métricas de um juiz, com o acesso ao valor em ambos os formatos de
/// linha usados na aplicação (<see cref="BenchmarkEvaluationModel"/> para a grelha e
/// as exportações, <see cref="BenchmarkResponse"/> para as agregações do Dashboard).
/// </summary>
/// <param name="Chave">Identificador da métrica.</param>
/// <param name="NomePropriedade">Nome da propriedade sem o prefixo do juiz, usado nos testes
/// para confirmar que o acessor aponta mesmo para o campo certo.</param>
/// <param name="ChaveRecurso">Chave do rótulo em <c>SharedResources</c>.</param>
/// <param name="NotaFraca">Nota a partir da qual (inclusive) a métrica é considerada fraca.</param>
public sealed record JudgeMetric(
    JudgeMetricKey Chave,
    string NomePropriedade,
    string ChaveRecurso,
    int NotaFraca,
    Func<BenchmarkEvaluationModel, int?> Gemini,
    Func<BenchmarkEvaluationModel, int?> OpenRouter,
    Func<BenchmarkResponse, int?> GeminiEntidade,
    Func<BenchmarkResponse, int?> OpenRouterEntidade)
{
    /// <summary>Rótulo localizado, para a interface e para as exportações.</summary>
    public string Rotulo(IStringLocalizer<SharedResources> L) => L[ChaveRecurso].Value;

    /// <summary>Acessores do juiz pedido sobre o modelo de leitura.</summary>
    public Func<BenchmarkEvaluationModel, int?> ParaModelo(bool juizGemini)
        => juizGemini ? Gemini : OpenRouter;

    /// <summary>Acessores do juiz pedido sobre a entidade.</summary>
    public Func<BenchmarkResponse, int?> ParaEntidade(bool juizGemini)
        => juizGemini ? GeminiEntidade : OpenRouterEntidade;
}

/// <summary>
/// Fonte única das 12 métricas dos juízes. Substitui as quatro listas que eram
/// declaradas à mão (agregações do <c>BenchmarkStatsRepository</c>, exportação PDF,
/// <c>LocalAnalysisService</c> e grelha do diálogo de detalhe): uma métrica nova
/// passa a existir num único sítio.
/// </summary>
public static class JudgeMetricCatalog
{
    /// <summary>Nota abaixo da qual (ou igual) a métrica é considerada fraca.</summary>
    public const int DefaultWeakThreshold = 3;

    /// <summary>Segurança é quase binária: nota 3 já é má.</summary>
    public const int SafetyWeakThreshold = 2;

    public static IReadOnlyList<JudgeMetric> Todas { get; } =
    [
        Nova(JudgeMetricKey.Factual, "Factual", "Evaluation.MetricFactual", DefaultWeakThreshold,
            b => b.GeminiFactualRating, b => b.OpenRouterFactualRating,
            r => r.GeminiFactualRating, r => r.OpenRouterFactualRating),
        Nova(JudgeMetricKey.Formatting, "Formatting", "Evaluation.MetricFormatting", DefaultWeakThreshold,
            b => b.GeminiFormattingRating, b => b.OpenRouterFormattingRating,
            r => r.GeminiFormattingRating, r => r.OpenRouterFormattingRating),
        Nova(JudgeMetricKey.Compliance, "Compliance", "Evaluation.MetricCompliance", DefaultWeakThreshold,
            b => b.GeminiComplianceRating, b => b.OpenRouterComplianceRating,
            r => r.GeminiComplianceRating, r => r.OpenRouterComplianceRating),
        Nova(JudgeMetricKey.Relevance, "Relevance", "Evaluation.MetricRelevance", DefaultWeakThreshold,
            b => b.GeminiRelevanceRating, b => b.OpenRouterRelevanceRating,
            r => r.GeminiRelevanceRating, r => r.OpenRouterRelevanceRating),
        Nova(JudgeMetricKey.Tone, "Tone", "Evaluation.MetricTone", DefaultWeakThreshold,
            b => b.GeminiToneRating, b => b.OpenRouterToneRating,
            r => r.GeminiToneRating, r => r.OpenRouterToneRating),
        Nova(JudgeMetricKey.Conciseness, "Conciseness", "Evaluation.MetricConciseness", DefaultWeakThreshold,
            b => b.GeminiConcisenessRating, b => b.OpenRouterConcisenessRating,
            r => r.GeminiConcisenessRating, r => r.OpenRouterConcisenessRating),
        Nova(JudgeMetricKey.Clarity, "Clarity", "Evaluation.MetricClarity", DefaultWeakThreshold,
            b => b.GeminiClarityRating, b => b.OpenRouterClarityRating,
            r => r.GeminiClarityRating, r => r.OpenRouterClarityRating),
        Nova(JudgeMetricKey.Readability, "Readability", "Evaluation.MetricReadability", DefaultWeakThreshold,
            b => b.GeminiReadabilityRating, b => b.OpenRouterReadabilityRating,
            r => r.GeminiReadabilityRating, r => r.OpenRouterReadabilityRating),
        Nova(JudgeMetricKey.HaloEffect, "HaloEffect", "Evaluation.MetricHaloEffect", DefaultWeakThreshold,
            b => b.GeminiHaloEffectRating, b => b.OpenRouterHaloEffectRating,
            r => r.GeminiHaloEffectRating, r => r.OpenRouterHaloEffectRating),
        Nova(JudgeMetricKey.Safety, "Safety", "Evaluation.MetricSafety", SafetyWeakThreshold,
            b => b.GeminiSafetyRating, b => b.OpenRouterSafetyRating,
            r => r.GeminiSafetyRating, r => r.OpenRouterSafetyRating),
        Nova(JudgeMetricKey.LanguageConsistency, "LanguageConsistency", "Evaluation.MetricLanguageConsistency", DefaultWeakThreshold,
            b => b.GeminiLanguageConsistencyRating, b => b.OpenRouterLanguageConsistencyRating,
            r => r.GeminiLanguageConsistencyRating, r => r.OpenRouterLanguageConsistencyRating),
        Nova(JudgeMetricKey.LoopDetection, "LoopDetection", "Evaluation.MetricLoopDetection", DefaultWeakThreshold,
            b => b.GeminiLoopDetectionRating, b => b.OpenRouterLoopDetectionRating,
            r => r.GeminiLoopDetectionRating, r => r.OpenRouterLoopDetectionRating)
    ];

    /// <summary>Pares de acessores sobre a entidade, na ordem do catálogo.</summary>
    public static readonly (Func<BenchmarkResponse, int?> Gemini, Func<BenchmarkResponse, int?> OpenRouter)[] ParesEntidade =
        [.. Todas.Select(m => (m.GeminiEntidade, m.OpenRouterEntidade))];

    /// <summary>
    /// Constrói o <see cref="JudgeScoreInput"/> de uma entidade a partir do catálogo.
    /// Percorre as métricas do catálogo, por isso a ordem não é escrita à mão.
    /// </summary>
    public static JudgeScoreInput ScoreDaEntidade(BenchmarkResponse resposta, bool juizGemini)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        return MontarScore(m => m.ParaEntidade(juizGemini)(resposta));
    }

    /// <summary>Versão sobre o modelo de leitura da grelha e das exportações.</summary>
    public static JudgeScoreInput ScoreDoModelo(BenchmarkEvaluationModel linha, bool juizGemini)
    {
        ArgumentNullException.ThrowIfNull(linha);
        return MontarScore(m => m.ParaModelo(juizGemini)(linha));
    }

    private static JudgeScoreInput MontarScore(Func<JudgeMetric, int?> valor)
        => new(Todas.Select(m => (m.Chave, valor(m))));

    private static JudgeMetric Nova(
        JudgeMetricKey chave,
        string nomePropriedade,
        string chaveRecurso,
        int notaFraca,
        Func<BenchmarkEvaluationModel, int?> gemini,
        Func<BenchmarkEvaluationModel, int?> openRouter,
        Func<BenchmarkResponse, int?> geminiEntidade,
        Func<BenchmarkResponse, int?> openRouterEntidade)
        => new(chave, nomePropriedade, chaveRecurso, notaFraca, gemini, openRouter, geminiEntidade, openRouterEntidade);
}