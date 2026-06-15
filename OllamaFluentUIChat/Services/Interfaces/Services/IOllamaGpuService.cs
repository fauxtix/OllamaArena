using OllamaFluentUIChat.Models.DTO;

namespace OllamaFluentUIChat.Services.Interfaces.Services
{
    public interface IOllamaGpuService
    {
        OllamaModels.GpuStatus CheckGpuCompatibility(long modelSizeInBytes);
        Task<OllamaModels.OllamaResponse> GetLocalModelsAsync();
    }
}