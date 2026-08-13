using Microsoft.Extensions.Logging;
using OllamaArena.Models.DTO;
using OllamaArena.Services.Interfaces.Services;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OllamaArena.Services;

/// <summary>
/// Obtém o catálogo público de modelos do OpenRouter (GET https://openrouter.ai/api/v1/models,
/// sem autenticação) e filtra os gratuitos (:free / preço 0) para a página Settings.
/// </summary>
public class OpenRouterCatalogService : IOpenRouterCatalogService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterCatalogService> _logger;

    private const string ModelsUrl = "https://openrouter.ai/api/v1/models";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OpenRouterCatalogService(HttpClient httpClient, ILogger<OpenRouterCatalogService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<OpenRouterCatalogModel>> GetFreeModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(ModelsUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var catalogo = await JsonSerializer.DeserializeAsync<CatalogResponse>(stream, JsonOptions, cancellationToken);

            return (catalogo?.Data ?? new())
                .Select(m => new OpenRouterCatalogModel
                {
                    Id = m.Id ?? string.Empty,
                    Name = m.Name ?? string.Empty,
                    ContextLength = m.ContextLength,
                    IsFree = ParsePrice(m.Pricing?.Prompt) == 0 && ParsePrice(m.Pricing?.Completion) == 0
                })
                .Where(m => !string.IsNullOrWhiteSpace(m.Id) && m.IsFree)
                .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Não foi possível obter o catálogo de modelos OpenRouter.");
            throw;
        }
    }

    private static decimal ParsePrice(string? valor) =>
        decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var preco) ? preco : 0;

    private sealed class CatalogResponse
    {
        public List<CatalogEntry> Data { get; set; } = new();
    }

    private sealed class CatalogEntry
    {
        public string? Id { get; set; }
        public string? Name { get; set; }

        [JsonPropertyName("context_length")]
        public long? ContextLength { get; set; }

        public CatalogPricing? Pricing { get; set; }
    }

    private sealed class CatalogPricing
    {
        public string? Prompt { get; set; }
        public string? Completion { get; set; }
    }
}
