using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Interfaces.Services;

public interface IOpenRouterCatalogService
{
    Task<List<OpenRouterCatalogModel>> GetFreeModelsAsync(CancellationToken cancellationToken = default);
}
