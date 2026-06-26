using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Models.Entities;

namespace OllamaFluentUIChat.Services.Interfaces.Repositories
{
    public interface IBenchmarkRepository
    {
        Task<bool> DeletePromptAndHistoryAsync(int promptId);
        Task<bool> DeleteSpecificResponseAsync(int responseId);
        Task<bool> UpdatePromptAsync(int id, string newText);
        Task<bool> UpdateResponseTextAsync(int responseId, string newText);
        Task<int> CreatePromptAsync(string textoPrompt);
        Task CreateResponseAsync(BenchmarkResponse response);
        Task<BenchmarkPrompt?> GetBenchmarkByIdAsync(int promptId);
        Task<List<BenchmarkPrompt>> GetAllBenchmarksAsync();
        Task<bool> UpdateResponseEvaluationAsync(BenchmarkResponse res);
        Task<IEnumerable<BenchmarkEvaluation>> BenchmarkResponseEvaluationAsync();
        Task<string?> GetBestModelAsync();
    }
}