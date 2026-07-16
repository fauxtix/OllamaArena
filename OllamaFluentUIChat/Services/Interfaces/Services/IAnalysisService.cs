using OllamaFluentUIChat.Models.DTO;

namespace OllamaFluentUIChat.Services.Interfaces.Services;
public interface IAnalysisService
{
    Task<BenchmarkAnalysisResult> AnalisarBenchmarksAsync(
            List<BenchmarkEvaluationModel> benchmarks,
            CancellationToken cancellationToken = default);

    Task<BenchmarkAnalysisResult> AnalisarBenchmarksWithStreamingAsync(
        List<BenchmarkEvaluationModel> benchmarks,
        Action<string>? onPartialOutput,
        CancellationToken cancellationToken = default);
}
