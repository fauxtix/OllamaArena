using Dapper;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;

namespace OllamaArena.Services.Implementations.Repositories;

/// <summary>
/// Registo de modelos ":free" do OpenRouter exibidos na combo-box da página Settings.
/// A escolha ativa do juiz continua em Configuracoes ("AutomatedJudge:OpenRouterModel").
/// </summary>
public class ModelosJuizRepository : IModelosJuizRepository
{
    private readonly IDapperContext _context;

    public ModelosJuizRepository(IDapperContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetAllAsync()
    {
        using var connection = _context.CreateConnection();
        var nomes = await connection.QueryAsync<string>(
            "SELECT Nome FROM ModelosJuiz ORDER BY Nome COLLATE NOCASE;");
        return nomes.ToList();
    }

    public async Task<bool> AddAsync(string nome)
    {
        using var connection = _context.CreateConnection();
        int linhas = await connection.ExecuteAsync(
            """
            INSERT OR IGNORE INTO ModelosJuiz (Nome, DataCriacao) VALUES (@Nome, @DataCriacao);
            """,
            new { Nome = nome, DataCriacao = DateTime.UtcNow.ToString("o") });
        return linhas > 0;
    }

    public async Task<bool> DeleteAsync(string nome)
    {
        using var connection = _context.CreateConnection();
        int linhas = await connection.ExecuteAsync(
            "DELETE FROM ModelosJuiz WHERE Nome = @Nome;", new { Nome = nome });
        return linhas > 0;
    }
}
