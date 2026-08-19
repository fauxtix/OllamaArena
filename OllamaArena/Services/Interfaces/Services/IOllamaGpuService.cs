using OllamaArena.Models.DTO;
using static OllamaArena.Models.DTO.OllamaModels;

namespace OllamaArena.Services.Interfaces.Services;

public interface IOllamaGpuService
{
    Task<OllamaResponse> GetLocalModelsAsync();
    Task<OllamaResponse> GetRunningModelsAsync();
    Task<bool> UnloadModelFromMemoryAsync(string modelName);
    Task<GpuStatus> CheckGpuCompatibility(long modelSizeInBytes);
    Task<int> GetModelContextLengthAsync(string modelName);
    Task<(int ContextLength, string TrainingYear)> GetExtendedModelMetadataAsync(string modelName);
    Task<int> GetRecommendedContextLengthAsync(long modelSizeInBytes, string modelName);
    Task<bool> ModelSupportsThinkingAsync(string modelName);
    Task<string?> GerarResumoDoPromptAsync(string userPrompt, string activeModel);
}
