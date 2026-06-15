namespace Services.Interfaces.Services
{
    public interface IAiSettingsProvider
    {
        Task<(string ModelBaseUrl, string ModelName)> GetAiSettingsAsync();
    }
}
