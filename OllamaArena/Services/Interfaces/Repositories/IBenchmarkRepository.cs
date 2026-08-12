using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;

namespace OllamaArena.Services.Interfaces.Repositories;

public interface IBenchmarkRepository
{
    Task<bool> DeletePromptAndHistoryAsync(int promptId);
    Task<bool> DeleteSpecificResponseAsync(int responseId);
    Task<bool> UpdatePromptAsync(int id, string newText);
    Task<bool> UpdateResponseTextAsync(int responseId, string newText);
    Task<int> CreatePromptAsync(string textoPrompt, double temperatura);
    Task CreateResponseAsync(BenchmarkResponse response);
    Task<BenchmarkPrompt?> GetBenchmarkByIdAsync(int promptId);
    Task<List<BenchmarkPrompt>> GetAllBenchmarksAsync();
    Task<bool> UpdateResponseEvaluationAsync(BenchmarkResponse res);
    Task<IEnumerable<BenchmarkEvaluationModel>> BenchmarkResponseEvaluationAsync();
    Task<string?> GetBestModelAsync();
    Task<List<ModelRanking>> GetModelRankingAsync();
    Task<bool> DeleteAllPromptsAndHistoryAsync();
    Task<IEnumerable<HistoryResponse>> HistoryResponseAsync();
    Task<PromptFeedback> GetBenchmarkJudgesFeedbackByIdAsync(int promptId);
    Task<BenchmarkResponse> GetBenchmarkAnswersByIdAsync(int promptId);
}