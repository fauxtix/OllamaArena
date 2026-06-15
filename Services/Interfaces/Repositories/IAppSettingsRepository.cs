using static Services.Models.Entities.Application;

namespace Services.Interfaces.Repositories;
    public interface IAppSettingsRepository
{
    Task<ApplicationSettings> GetSettingsAsync();
    Task UpdateSettingsAsync(ApplicationSettings settings);
    Task UpdateLanguageAsync(string language);

}