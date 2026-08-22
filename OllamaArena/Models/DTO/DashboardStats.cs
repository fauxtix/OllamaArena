namespace OllamaArena.Models.DTO;

/// <summary>
/// Estatísticas globais para os cartões do Dashboard: atividade de prompts e
/// cobertura de avaliação (respostas avaliadas pelos dois juízes).
/// </summary>
public sealed class DashboardStats
{
    public int TotalPrompts { get; set; }
    public DateTime? UltimaAtividade { get; set; }
    public int TotalRespostas { get; set; }

    /// <summary>Respostas com avaliação dos dois juízes (Gemini + OpenRouter).</summary>
    public int RespostasAvaliadasAmbosJuizes { get; set; }

    /// <summary>Cobertura de avaliação (0–100); null quando não há respostas.</summary>
    public double? CoberturaPercentual => TotalRespostas > 0
        ? Math.Round(RespostasAvaliadasAmbosJuizes * 100.0 / TotalRespostas, 0)
        : null;
}
