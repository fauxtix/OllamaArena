namespace OllamaArena.Services.Interfaces.Repositories;

public interface ISettingsRepository
{
    Task<string?> GetValueAsync(string chave);
    Task<Dictionary<string, string>> GetValuesAsync(IEnumerable<string> chaves);
    Task SetValueAsync(string chave, string valor);
    Task DeleteValueAsync(string chave);
}
