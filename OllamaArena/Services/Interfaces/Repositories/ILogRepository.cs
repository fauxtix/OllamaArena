using OllamaArena.Models.Entities;

namespace OllamaArena.Services.Interfaces.Repositories
{
    public interface ILogRepository
    {
        Task<List<LogEntity>> GetAllLogsAsync();
        Task<LogEntity?> GetLogByIdAsync(int id);
        Task<bool> DeleteAllLogsAsync();
        Task DeleteLogsByIdsAsync(IEnumerable<int> ids);
        Task DeleteLogByIdAsync(int id);
    }
}
