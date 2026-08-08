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

        // Renomeações ChatGpt* → OpenRouter* (a avaliação passou a ser feita via OpenRouter).
        // Devem correr antes de qualquer ADD COLUMN com o nome de destino, senão o RENAME falha.
        string[] renomeacoesChatGpt =
        [
            "ChatGptRating",
            "ChatGptFeedback",
            "ChatGptFactualRating",
            "ChatGptFormattingRating",
            "ChatGptComplianceRating",
            "ChatGptRelevanceRating",
            "ChatGptToneRating",
            "ChatGptConcisenessRating",
            "ChatGptClarityRating",
            "ChatGptReadabilityRating",
            "ChatGptHaloEffectRating",
            "ChatGptSafetyRating",
            "ChatGptRecommendation"
        ];

        foreach (var coluna in renomeacoesChatGpt)
        {
            var destino = coluna.Replace("ChatGpt", "OpenRouter");
            if (columns.Contains(coluna) && !columns.Contains(destino))
            {
                connection.Execute($"ALTER TABLE Respostas RENAME COLUMN {coluna} TO {destino};");
                columns.Add(destino);
                columns.Remove(coluna);
            }
        }

        if (!columns.Contains("GeminiRecommendation"))
        {
            connection.Execute("ALTER TABLE Respostas ADD COLUMN GeminiRecommendation TEXT;");
        }

        if (!columns.Contains("OpenRouterRecommendation"))
        {
            connection.Execute("ALTER TABLE Respostas ADD COLUMN OpenRouterRecommendation TEXT;");
        }
    }
}
