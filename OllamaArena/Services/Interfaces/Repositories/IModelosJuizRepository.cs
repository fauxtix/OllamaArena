namespace OllamaArena.Services.Interfaces.Repositories;

public interface IModelosJuizRepository
{
    Task<List<string>> GetAllAsync();
    Task<bool> AddAsync(string nome);
    Task<bool> DeleteAsync(string nome);
}
