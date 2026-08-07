using Dapper;
using OllamaFluentUIChat.Services.Interfaces.Services;

namespace OllamaFluentUIChat.Services.Implementations.Services;

/// <summary>
/// Migrações idempotentes do esquema SQLite. Como a base de dados é criada externamente
/// (DB Browser) e acompanha o repositório, as colunas novas são adicionadas aqui,
/// de forma segura para BDs existentes e para clones futuros.
/// </summary>
public static class DatabaseSchemaInitializer
{
    private sealed class TableColumn
    {
        public string Name { get; set; } = string.Empty;
    }

    public static void EnsureRecommendationColumns(IDapperContext context)
    {
        using var connection = context.CreateConnection();
        connection.Open();

        var columns = connection.Query<TableColumn>("PRAGMA table_info(Respostas);")
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columns.Contains("GeminiRecommendation"))
        {
            connection.Execute("ALTER TABLE Respostas ADD COLUMN GeminiRecommendation TEXT;");
        }

        if (!columns.Contains("ChatGptRecommendation"))
        {
            connection.Execute("ALTER TABLE Respostas ADD COLUMN ChatGptRecommendation TEXT;");
        }
    }
}
