using OllamaFluentUIChat.Models.Entities;

namespace OllamaFluentUIChat.Services.Interfaces.Repositories
{
    public interface ILogRepository
    {
        Task<List<LogEntity>> GetAllLogsAsync();
        Task<LogEntity?> GetLogByIdAsync(int id);
        Task<bool> DeleteAllLogsAsync();
    }
}
