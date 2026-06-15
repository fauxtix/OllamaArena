using Microsoft.Extensions.Caching.Memory;
using Services.Interfaces.Repositories;
using Services.Interfaces.Services;

namespace Services.Implementations.Services;

/// <summary>
/// Cache-backed implementation of <see cref="IAiSettingsProvider"/>.
/// Settings are cached for 60 seconds (sliding) to avoid hitting the database on every AI request.
/// Follow-up work: invalidate the cache when settings are saved (e.g. in AppSettingsController).
/// </summary>
public sealed class AiSettingsProvider : IAiSettingsProvider
{
    private const string CacheKey = "ai_settings";

    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IMemoryCache _cache;

    public AiSettingsProvider(IAppSettingsRepository appSettingsRepository, IMemoryCache cache)
    {
        _appSettingsRepository = appSettingsRepository;
        _cache = cache;
    }

    public async Task<(string ModelBaseUrl, string ModelName)> GetAiSettingsAsync()
    {
        if (_cache.TryGetValue(CacheKey, out (string ModelBaseUrl, string ModelName) cached))
            return cached;

        var settings = await _appSettingsRepository.GetSettingsAsync();
        var result = (settings.ModelBaseUrl, settings.AiModelName);

        _cache.Set(CacheKey, result, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromSeconds(60)
        });

        return result;
    }
}
