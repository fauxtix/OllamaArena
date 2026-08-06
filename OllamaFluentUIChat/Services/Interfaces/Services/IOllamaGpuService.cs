using static OllamaFluentUIChat.Models.DTO.OllamaModels;

namespace OllamaFluentUIChat.Services.Interfaces.Services;

public interface IOllamaGpuService
{
    Task<OllamaResponse> GetLocalModelsAsync();
    Task<OllamaResponse> GetRunningModelsAsync();
    Task<bool> UnloadModelFromMemoryAsync(string modelName);
    GpuStatus CheckGpuCompatibility(long modelSizeInBytes);
    Task<int> GetModelContextLengthAsync(string modelName);
    Task<(int ContextLength, string TrainingYear)> GetExtendedModelMetadataAsync(string modelName);
    Task<int> GetRecommendedContextLengthAsync(long modelSizeInBytes, string modelName);
}
