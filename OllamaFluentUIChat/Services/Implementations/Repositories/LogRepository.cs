using Dapper;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;

namespace OllamaFluentUIChat.Services.Implementations.Repositories;

public class LogRepository : ILogRepository
{
    private readonly IDapperContext _context;
    private readonly ILogger<LogRepository> _logger;

    public LogRepository(IDapperContext context, ILogger<LogRepository> logger)
    {
        _context = context;
        _logger = logger;
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
        try
        {
            var sql = "DELETE FROM Logs; PRAGMA optimize;"; // PRAGMA optimize refina os índices sem bloquear a BD
            using var connection = _context.CreateConnection();
            int affectedLines = await connection.ExecuteAsync(sql);
            return true;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao apagar Todos os logs");
            return false;
        }
    }

    public async Task DeleteLogByIdAsync(int id)
    {
        if (id < 1)
            return;

        var sql = "DELETE FROM Logs WHERE Id  = @Id; PRAGMA optimize;";

        try
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao apagar registo {id}");
        }
    }

    public async Task DeleteLogsByIdsAsync(IEnumerable<int> ids)
    {
        if (ids == null || !ids.Any())
            return;

        var sql = "DELETE FROM Logs WHERE Id IN @Ids; PRAGMA optimize;";

        try
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(sql, new { Ids = ids });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao apagar registos filtrados");
        }
    }
}
