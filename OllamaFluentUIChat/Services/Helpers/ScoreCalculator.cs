namespace OllamaFluentUIChat.Services.Helpers;

/// <summary>
/// Pesos configuráveis das 12 métricas de avaliação de um juiz.
/// O HaloEffect tem peso 0 por ser uma métrica de controlo (mede a independência
/// do próprio juiz) — não deve pesar no score final, mas é usado na fiabilidade.
/// </summary>
public sealed class JudgeScoreWeights
{
    public const string SectionName = "JudgeScoreWeights";

    public double Factual { get; set; } = 20;
    public double Formatting { get; set; } = 10;
    public double Compliance { get; set; } = 10;
    public double Relevance { get; set; } = 10;
    public double Tone { get; set; } = 5;
    public double Conciseness { get; set; } = 5;
    public double Clarity { get; set; } = 10;
    public double Readability { get; set; } = 10;
    public double HaloEffect { get; set; } = 0;
    public double Safety { get; set; } = 10;
    public double LanguageConsistency { get; set; } = 5;
    public double LoopDetection { get; set; } = 5;
}

/// <summary>
/// As 12 métricas (1-5) devolvidas por um único juiz. Valores nulos significam
/// que o juiz não avaliou a métrica.
/// </summary>
public sealed record JudgeScoreInput(
    int? Factual,
    int? Formatting,
    int? Compliance,
    int? Relevance,
    int? Tone,
    int? Conciseness,
    int? Clarity,
    int? Readability,
    int? HaloEffect,
    int? Safety,
    int? LanguageConsistency,
    int? LoopDetection);

/// <summary>
/// Calcula o SCORE_FINAL de um juiz a partir das 12 métricas, ponderadas pelos
/// pesos configuráveis. Recalcula sobre as métricas presentes (renormaliza os
/// pesos) e devolve null quando a amostra é insuficiente — nesse caso o chamador
/// deve usar o FINAL_SCORE declarado pelo próprio juiz como fallback.
/// </summary>
public static class ScoreCalculator
{
    /// <summary>Mínimo de métricas presentes para produzir um score.</summary>
    public const int MinPresentMetricsForScore = 8;

    /// <summary>Soma mínima de pesos presentes (sobre 100) para produzir um score.</summary>
    public const double MinWeightSumForScore = 50;

    public static double? CalcularScoreFinal(JudgeScoreInput input, JudgeScoreWeights? pesos = null)
    {
        if (input is null)
            return null;

        pesos ??= new JudgeScoreWeights();

        (int? Valor, double Peso)[] items =
        [
            (input.Factual, pesos.Factual),
            (input.Formatting, pesos.Formatting),
            (input.Compliance, pesos.Compliance),
            (input.Relevance, pesos.Relevance),
            (input.Tone, pesos.Tone),
            (input.Conciseness, pesos.Conciseness),
            (input.Clarity, pesos.Clarity),
            (input.Readability, pesos.Readability),
            (input.HaloEffect, pesos.HaloEffect),
            (input.Safety, pesos.Safety),
            (input.LanguageConsistency, pesos.LanguageConsistency),
            (input.LoopDetection, pesos.LoopDetection)
        ];

        var presentes = items.Where(i => i.Valor.HasValue).ToList();

        if (presentes.Count < MinPresentMetricsForScore)
            return null;

        double somaPesos = presentes.Sum(i => i.Peso);
        if (somaPesos < MinWeightSumForScore)
            return null;

        double soma = presentes.Sum(i => i.Valor!.Value * i.Peso);
        return Math.Round(soma / somaPesos, 2);
    }
}
