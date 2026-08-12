namespace OllamaArena.Models.DTO;

/// <summary>
/// Score agregado de um modelo por juiz e global, calculado com o ScoreCalculator
/// (média ponderada das 12 métricas) sobre todas as respostas avaliadas do modelo.
/// </summary>
public sealed class ModelRanking
{
    public string Model { get; set; } = string.Empty;
    public double Score { get; set; }
    public double GeminiScore { get; set; }
    public double OpenRouterScore { get; set; }
    public int TotalResponses { get; set; }
    public int JudgedResponses { get; set; }
}
