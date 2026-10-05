using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;

namespace OllamaArena.Services.Interfaces.Repositories;

/// <summary>
/// Read models de benchmarks: o que a grelha de avaliações, o painel de histórico e o
/// detalhe de um prompt projectam. Só leitura — as projeções mais caras (agregações e
/// séries temporais) ficam em <see cref="IBenchmarkStatsRepository"/>.
/// </summary>
public interface IBenchmarkQueryRepository
{
    Task<string?> FindDescriptionByTextAsync(string textoPrompt);
    Task<BenchmarkPrompt?> GetBenchmarkByIdAsync(int promptId);
    Task<List<BenchmarkPrompt>> GetAllBenchmarksAsync();
    Task<PromptFeedback> GetBenchmarkJudgesFeedbackByIdAsync(int promptId);
    Task<BenchmarkResponse> GetBenchmarkAnswersByIdAsync(int promptId);
    Task<IEnumerable<BenchmarkEvaluationModel>> BenchmarkResponseEvaluationAsync();
    Task<IEnumerable<HistoryResponse>> HistoryResponseAsync();
}