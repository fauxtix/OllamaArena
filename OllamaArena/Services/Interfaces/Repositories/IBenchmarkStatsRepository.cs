using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Interfaces.Repositories;

/// <summary>
/// Agregações sobre benchmarks: dashboard, séries temporais, rankings e séries para
/// gráficos. Separadas das projeções (<see cref="IBenchmarkQueryRepository"/>) porque
/// cada uma percorre respostas com SQL distinto e raramente são usadas em conjunto.
/// </summary>
public interface IBenchmarkStatsRepository
{
    Task<string?> GetBestModelAsync();
    Task<List<ModelRanking>> GetModelRankingAsync();
    Task<DashboardStats> GetDashboardStatsAsync();
    Task<List<ModelScoreTimeline>> GetScoreTimelineAsync();
    Task<List<ModelChartData>> GetModelChartDataAsync();
}