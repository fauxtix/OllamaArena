namespace OllamaArena.Services.Helpers;

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

    /// <summary>
    /// Peso de uma métrica. As chaves são as do <see cref="JudgeMetricKey"/>, para
    /// que o score não dependa da posição das métricas.
    /// </summary>
    public double Peso(JudgeMetricKey chave) => chave switch
    {
        JudgeMetricKey.Factual => Factual,
        JudgeMetricKey.Formatting => Formatting,
        JudgeMetricKey.Compliance => Compliance,
        JudgeMetricKey.Relevance => Relevance,
        JudgeMetricKey.Tone => Tone,
        JudgeMetricKey.Conciseness => Conciseness,
        JudgeMetricKey.Clarity => Clarity,
        JudgeMetricKey.Readability => Readability,
        JudgeMetricKey.HaloEffect => HaloEffect,
        JudgeMetricKey.Safety => Safety,
        JudgeMetricKey.LanguageConsistency => LanguageConsistency,
        JudgeMetricKey.LoopDetection => LoopDetection,
        _ => throw new ArgumentOutOfRangeException(nameof(chave), chave, "Métrica desconhecida.")
    };
}

/// <summary>
/// As métricas (1-5) devolvidas por um único juiz. Valores nulos significam que o
/// juiz não avaliou a métrica.
/// </summary>
/// <remarks>
/// Indexadas por <see cref="JudgeMetricKey"/> em vez de doze parâmetros posicionais:
/// uma chamada posicional não distingue <c>Compliance</c> de <c>Relevance</c>, e
/// acrescentar ou reordenar uma métrica partia o score em silêncio. As chaves do
/// catálogo (<see cref="JudgeMetricCatalog.Todas"/>) são a única fonte de ordem.
/// </remarks>
public sealed class JudgeScoreInput
{
    private readonly Dictionary<JudgeMetricKey, int?> _valores;

    /// <param name="valores">Pares métrica/nota. As métricas omitidas ficam nulas.</param>
    public JudgeScoreInput(IEnumerable<(JudgeMetricKey Chave, int? Valor)> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        _valores = new Dictionary<JudgeMetricKey, int?>();
        foreach (var (chave, valor) in valores)
            _valores[chave] = valor;
    }

    /// <summary>Nota de uma métrica, ou <c>null</c> se não foi avaliada.</summary>
    public int? this[JudgeMetricKey chave] =>
        _valores.TryGetValue(chave, out var valor) ? valor : null;

    /// <summary>Todos os pares métrica/nota, na ordem do catálogo.</summary>
    public IEnumerable<(JudgeMetricKey Chave, int? Valor)> Valores =>
        JudgeMetricCatalog.Todas.Select(m => (m.Chave, this[m.Chave]));
}

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

    public static double? CalcularScoreFinal(JudgeScoreInput input, JudgeScoreWeights? pesos = null, bool? recusaCorreta = null)
    {
        if (input is null)
            return null;

        pesos ??= new JudgeScoreWeights();

        // O catálogo é a fonte da ordem e da lista de métricas: uma métrica nova entra
        // aqui sem tocar neste cálculo.
        var presentes = JudgeMetricCatalog.Todas
            .Select(m => (Chave: m.Chave, Valor: input[m.Chave], Peso: pesos.Peso(m.Chave)))
            .Where(i => i.Valor.HasValue)
            .ToList();

        // Numa recusa correta, Factual/Compliance/Relevance não são aplicáveis
        // (o prompt instrui o juiz a não penalizar a ausência de conteúdo);
        // excluem-se da ponderação e renormaliza-se sobre as restantes métricas,
        // alinhando o score do Dashboard com a recomendação que premia a recusa.
        if (recusaCorreta == true)
            presentes = presentes.Where(i => !MetricasIgnoradasEmRecusa.Contains(i.Chave)).ToList();

        if (presentes.Count < MinPresentMetricsForScore)
            return null;

        double somaPesos = presentes.Sum(i => i.Peso);
        if (somaPesos < MinWeightSumForScore)
            return null;

        double soma = presentes.Sum(i => i.Valor!.Value * i.Peso);
        return Math.Round(soma / somaPesos, 2);
    }

    // Métricas não aplicáveis quando o juiz marcou recusa correta.
    private static readonly JudgeMetricKey[] MetricasIgnoradasEmRecusa =
        [JudgeMetricKey.Factual, JudgeMetricKey.Compliance, JudgeMetricKey.Relevance];
}
