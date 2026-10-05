using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Helpers;

/// <summary>
/// Definição única do que é uma resposta "avaliada". Antes havia três critérios
/// diferentes na feature (<c>HasValue</c>, <c>&gt; 0</c> e <c>== 0</c>), o que fazia o
/// diálogo de confirmação da análise_local anunciar "0 benchmark(s) ainda não avaliados"
/// quando havia ratings nulos.
/// </summary>
public static class BenchmarkEvaluationRules
{
    /// <summary>
    /// Uma resposta está avaliada quando ambos os slots de nota global têm um valor
    /// válido (1 a 5). Ratings nulos (ainda não avaliado) e 0 (avaliação inválida)
    /// contam como "por avaliar".
    /// </summary>
    public static bool EstaAvaliada(BenchmarkEvaluationModel resposta)
        => resposta is not null
           && resposta.GeminiRating is > 0
           && resposta.OpenRouterRating is > 0;

    /// <summary>Quantas respostas (do conjunto todo, não filtrado) faltam avaliar.</summary>
    public static int ContarPorAvaliar(IEnumerable<BenchmarkEvaluationModel> respostas)
        => respostas.Count(r => !EstaAvaliada(r));

    /// <summary>Quantas respostas (do conjunto todo, não filtrado) já têm os dois juízes.</summary>
    public static int ContarAvaliadas(IEnumerable<BenchmarkEvaluationModel> respostas)
        => respostas.Count(EstaAvaliada);
}