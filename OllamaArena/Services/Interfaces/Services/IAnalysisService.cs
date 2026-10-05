using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Interfaces.Services;

public interface IAnalysisService
{
    /// <summary>
    /// Análise local do conjunto de avaliações. O <paramref name="cancellationToken"/>
    /// é honrado entre fases: cancelar interrompe o cálculo e propaga
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<BenchmarkAnalysisResult> AnalisarBenchmarksAsync(
            IReadOnlyList<BenchmarkEvaluationModel> benchmarks,
            CancellationToken cancellationToken = default);
}