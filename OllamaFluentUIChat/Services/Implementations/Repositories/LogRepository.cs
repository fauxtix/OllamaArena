using Dapper;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;

namespace OllamaFluentUIChat.Services.Implementations.Repositories;

public class LogRepository : ILogRepository
{
    private readonly IDapperContext _context;

    public LogRepository(IDapperContext context)
    {
        _context = context;
    }

    public async Task<List<LogEntity>> GetAllLogsAsync()
    {
        var sql = "SELECT * FROM Logs ORDER BY Id DESC;";
        using var connection = _context.CreateConnection();
        var logs = await connection.QueryAsync<LogEntity>(sql);
        return logs.ToList();
    }

    public async Task<LogEntity?> GetLogByIdAsync(int id)
    {
        var sql = "SELECT * FROM Logs WHERE Id = @Id;";
        using var connection = _context.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<LogEntity>(sql, new { Id = id });
    }

    public async Task<bool> DeleteAllLogsAsync()
    {
        var sql = "DELETE FROM Logs; VACUUM;"; // VACUUM liberta o espaço em disco do SQLite imediatamente
        using var connection = _context.CreateConnection();
        int affectedLines = await connection.ExecuteAsync(sql);
        return true;
    }
}
