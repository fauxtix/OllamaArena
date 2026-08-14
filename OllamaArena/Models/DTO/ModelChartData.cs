namespace OllamaArena.Models.DTO;

/// <summary>
/// Dados agregados por modelo para os gráficos do Dashboard: score ponderado,
/// média de tokens/s e médias das 12 métricas por juiz.
/// As listas de métricas seguem a ordem usada nos radares (Factual ... LoopDetection).
/// </summary>
public sealed class ModelChartData
{
    public string Model { get; set; } = string.Empty;
    public double Score { get; set; }
    public double TokensPorSegundo { get; set; }
    public int GeminiCount { get; set; }
    public int OpenRouterCount { get; set; }
    public List<double> GeminiMetrics { get; set; } = new();
    public List<double> OpenRouterMetrics { get; set; } = new();
}
