using OllamaFluentUIChat.Models.DTO;

namespace OllamaFluentUIChat.Services.Implementations.Services;

public sealed class TranslateClientService
{
    private readonly HttpClient _http;
    private readonly ILogger<TranslateClientService> _logger;
    private readonly string _baseUrl;

    public TranslateClientService(HttpClient http, ILogger<TranslateClientService> logger, IConfiguration config)
    {
        _http = http;
        _logger = logger;
        _http.DefaultRequestHeaders.Add("ApiKey", config["ApiKey"]);
        _baseUrl = $"{config["BaseUrl"]}/api";
    }

    public async Task<TranslateResponseDto?> TranslateAsync(TranslateRequestDto req, CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = $"{_baseUrl}/ai/translate";
            using var resp = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(_http, uri, req, cancellationToken);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<TranslateResponseDto>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling translate API");
            return null;
        }
    }
}
