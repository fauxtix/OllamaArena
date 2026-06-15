using static OllamaFluentUIChat.Models.Entities.Application;

namespace OllamaFluentUIChat.Services.Interfaces.Repositories;
    public interface IAppSettingsRepository
{
    Task<ApplicationSettings> GetSettingsAsync();
    Task UpdateSettingsAsync(ApplicationSettings settings);
    Task UpdateLanguageAsync(string language);

}