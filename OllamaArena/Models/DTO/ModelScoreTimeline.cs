namespace OllamaArena.Models.DTO;

/// <summary>
/// Ponto da linha temporal de um modelo: data do benchmark e score médio
/// acumulado até essa data (média de todos os scores anteriores, para mostrar
/// tendência em vez de ruído ponto-a-ponto).
/// </summary>
public sealed class TimelinePoint
{
    public DateTime Data { get; set; }
    public double ScoreAcumulado { get; set; }
}

/// <summary>
/// Série temporal do score de um modelo ao longo dos benchmarks (para o gráfico
/// de linhas "Evolução do score" do Dashboard).
/// </summary>
public sealed class ModelScoreTimeline
{
    public string Model { get; set; } = string.Empty;
    public List<TimelinePoint> Points { get; set; } = new();
}
