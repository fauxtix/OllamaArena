using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Interfaces.Services;

public interface IAnalysisService
{
    Task<BenchmarkAnalysisResult> AnalisarBenchmarksAsync(
            List<BenchmarkEvaluationModel> benchmarks,
            CancellationToken cancellationToken = default);
}