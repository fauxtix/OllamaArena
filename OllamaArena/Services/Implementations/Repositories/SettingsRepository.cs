using Dapper;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;

namespace OllamaArena.Services.Implementations.Repositories;

/// <summary>
/// Guarda definições chave/valor configuráveis pela página Settings.
/// A prioridade de resolução nos serviços é: BD (definido na UI) → configuração (user-secrets/appsettings).
/// </summary>
public class SettingsRepository : ISettingsRepository
{
    private sealed class Configuracao
    {
        public string Chave { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
    }

    private readonly IDapperContext _context;

    public SettingsRepository(IDapperContext context)
    {
        _context = context;
    }

    public async Task<string?> GetValueAsync(string chave)
    {
        using var connection = _context.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT Valor FROM Configuracoes WHERE Chave = @Chave;", new { Chave = chave });
    }

    public async Task<Dictionary<string, string>> GetValuesAsync(IEnumerable<string> chaves)
    {
        var lista = chaves.ToList();
        if (lista.Count == 0)
            return new Dictionary<string, string>();

        using var connection = _context.CreateConnection();
        var valores = await connection.QueryAsync<Configuracao>(
            "SELECT Chave, Valor FROM Configuracoes WHERE Chave IN @Chaves;",
            new { Chaves = lista });

        return valores.ToDictionary(v => v.Chave, v => v.Valor, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetValueAsync(string chave, string valor)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync("""
            INSERT INTO Configuracoes (Chave, Valor) VALUES (@Chave, @Valor)
            ON CONFLICT(Chave) DO UPDATE SET Valor = excluded.Valor;
            """, new { Chave = chave, Valor = valor });
    }

    public async Task DeleteValueAsync(string chave)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM Configuracoes WHERE Chave = @Chave;", new { Chave = chave });
    }
}
