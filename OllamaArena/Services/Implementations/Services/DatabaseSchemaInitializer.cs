using Dapper;
using OllamaArena.Services.Interfaces.Services;
using System.Data;

namespace OllamaArena.Services.Implementations.Services;

/// <summary>
/// Cria o esquema SQLite e aplica migrações incrementais idempotentes.
/// A base de dados é criada automaticamente no arranque (ver Program.cs) e
/// cada migração só corre quando a tabela/coluna em falta, garantindo que
/// é segura para BDs existentes e para clones novos.
/// </summary>
public static class DatabaseSchemaInitializer
{
    private sealed class TableColumn
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Garante que as tabelas existem (CREATE TABLE IF NOT EXISTS) e aplica
    /// as migrações de colunas em seguida.
    /// </summary>
    public static void EnsureSchema(IDapperContext context)
    {
        using var connection = context.CreateConnection();
        connection.Open();

        EnsureTable(connection);
        SeedModelosJuiz(connection);
        EnsureColumns(connection);
    }

    private static void EnsureTable(IDbConnection connection)
    {
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS "Prompts" (
                "Id"          INTEGER PRIMARY KEY AUTOINCREMENT,
                "Descricao"   TEXT,
                "TextoPrompt" TEXT NOT NULL,
                "DataCriacao" TEXT NOT NULL,
                "Temperatura" REAL
            );
            """);

        connection.Execute("""
            CREATE TABLE IF NOT EXISTS "Respostas" (
                "Id"                         INTEGER PRIMARY KEY AUTOINCREMENT,
                "PromptId"                   INTEGER NOT NULL,
                "NomeModelo"                 TEXT NOT NULL,
                "TextoResposta"              TEXT NOT NULL,
                "TokensPorSegundo"           REAL,
                "TempoPuroMs"                REAL,
                "TempoCargaMs"               REAL,
                "TempoProcessamento"         REAL,
                "TamanhoTokens"              INTEGER,
                "IdiomaSessao"               TEXT,
                "GeminiRating"               NUMERIC,
                "GeminiFeedback"             TEXT,
                "GeminiRecommendation"       TEXT,
                "OpenRouterRating"           NUMERIC,
                "OpenRouterFeedback"         TEXT,
                "OpenRouterRecommendation"   TEXT,
                "GeminiFactualRating"        INTEGER,
                "GeminiFormattingRating"     INTEGER,
                "GeminiComplianceRating"     INTEGER,
                "GeminiRelevanceRating"      INTEGER,
                "GeminiToneRating"           INTEGER,
                "GeminiConcisenessRating"    INTEGER,
                "GeminiClarityRating"        INTEGER,
                "GeminiReadabilityRating"    INTEGER,
                "GeminiHaloEffectRating"     INTEGER,
                "GeminiSafetyRating"         INTEGER,
                "GeminiLanguageConsistencyRating" INTEGER,
                "GeminiLoopDetectionRating"  INTEGER,
                "OpenRouterFactualRating"    INTEGER,
                "OpenRouterFormattingRating" INTEGER,
                "OpenRouterComplianceRating" INTEGER,
                "OpenRouterRelevanceRating"  INTEGER,
                "OpenRouterToneRating"       INTEGER,
                "OpenRouterConcisenessRating" INTEGER,
                "OpenRouterClarityRating"    INTEGER,
                "OpenRouterReadabilityRating" INTEGER,
                "OpenRouterHaloEffectRating" INTEGER,
                "OpenRouterSafetyRating"     INTEGER,
                "OpenRouterLanguageConsistencyRating" INTEGER,
                "OpenRouterLoopDetectionRating" INTEGER,
                "GeminiRefusalHandled"       INTEGER,
                "OpenRouterRefusalHandled"   INTEGER,
                FOREIGN KEY ("PromptId") REFERENCES "Prompts" ("Id") ON DELETE CASCADE
            );
            """);

        connection.Execute("""
            CREATE TABLE IF NOT EXISTS "Logs" (
                "Id"              INTEGER PRIMARY KEY AUTOINCREMENT,
                "Timestamp"       TEXT,
                "Level"           TEXT,
                "Exception"       TEXT,
                "RenderedMessage" TEXT,
                "Properties"      TEXT
            );
            """);

        connection.Execute("""
            CREATE TABLE IF NOT EXISTS "Conversas" (
                "Id"                  INTEGER PRIMARY KEY AUTOINCREMENT,
                "Titulo"              TEXT,
                "Descricao"           TEXT,
                "NomeModelo"          TEXT,
                "DataCriacao"         TEXT,
                "DataUltimaAtividade" TEXT
            );
            """);

        connection.Execute("""
            CREATE TABLE IF NOT EXISTS "ConversaMensagens" (
                "Id"             INTEGER PRIMARY KEY AUTOINCREMENT,
                "ConversationId" INTEGER NOT NULL,
                "Role"           TEXT NOT NULL,
                "Content"        TEXT NOT NULL,
                "Reasoning"      TEXT,
                "Temperature"    REAL,
                "ElapsedTime"    TEXT,
                "Timestamp"      TEXT,
                FOREIGN KEY ("ConversationId") REFERENCES "Conversas" ("Id") ON DELETE CASCADE
            );
            """);

        // Definições configuráveis pela página Settings (chave/valor).
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS "Configuracoes" (
                "Chave" TEXT PRIMARY KEY,
                "Valor" TEXT NOT NULL
            );
            """);

        // Registo de modelos ":free" do OpenRouter para a combo-box da página Settings.
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS "ModelosJuiz" (
                "Id"          INTEGER PRIMARY KEY AUTOINCREMENT,
                "Nome"        TEXT NOT NULL UNIQUE,
                "DataCriacao" TEXT NOT NULL
            );
            """);
    }

    /// <summary>
    /// Popula a tabela ModelosJuiz apenas quando está vazia (para não repor modelos
    /// que o utilizador apagou). Lista curada de modelos ":free" gerais do OpenRouter.
    /// </summary>
    private static void SeedModelosJuiz(IDbConnection connection)
    {
        int total = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM ModelosJuiz;");
        if (total > 0)
            return;

        string[] modelosCurados =
        [
            "meta-llama/llama-3.3-70b-instruct:free",
            "openai/gpt-oss-120b:free",
            "openai/gpt-oss-20b:free",
            "qwen/qwen3-next-80b-a3b-instruct:free",
            "google/gemma-4-31b-it:free",
            "nvidia/nemotron-3-ultra-550b-a55b:free",
            "nvidia/nemotron-3-super-120b-a12b:free",
            "nvidia/nemotron-3-nano-30b-a3b:free"
        ];

        foreach (var nome in modelosCurados)
        {
            connection.Execute(
                "INSERT INTO ModelosJuiz (Nome, DataCriacao) VALUES (@Nome, @DataCriacao);",
                new { Nome = nome, DataCriacao = DateTime.UtcNow.ToString("o") });
        }
    }

    /// <summary>
    /// Migrações incrementais de colunas. Compatível com BDs antigas: cada
    /// migração verifica se a coluna já existe antes de a criar/renomear.
    /// </summary>
    public static void EnsureColumns(IDapperContext context)
    {
        using var connection = context.CreateConnection();
        connection.Open();
        EnsureColumns(connection);
    }

    private static void EnsureColumns(IDbConnection connection)
    {
        var columns = connection.Query<TableColumn>("PRAGMA table_info(Respostas);")
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

        EnsureColumn(connection, "Respostas", "GeminiLanguageConsistencyRating", "INTEGER");
        EnsureColumn(connection, "Respostas", "GeminiLoopDetectionRating", "INTEGER");
        EnsureColumn(connection, "Respostas", "OpenRouterLanguageConsistencyRating", "INTEGER");
        EnsureColumn(connection, "Respostas", "OpenRouterLoopDetectionRating", "INTEGER");

        // Flag de recusa correta (REFUSAL_HANDLED do juiz): 1 = recusa correta,
        // 0 = não é recusa, NULL = avaliação antiga sem o marcador.
        EnsureColumn(connection, "Respostas", "GeminiRefusalHandled", "INTEGER");
        EnsureColumn(connection, "Respostas", "OpenRouterRefusalHandled", "INTEGER");

        // Idioma da sessão (seletor PT/EN) ativo na geração da resposta; usado pelo
        // juiz para avaliar a adesão ao idioma esperado. Null = resposta antiga.
        EnsureColumn(connection, "Respostas", "IdiomaSessao", "TEXT");

        EnsureColumn(connection, "Conversas", "Descricao", "TEXT");
    }

    private static void EnsureColumn(IDbConnection connection, string table, string column, string type)
    {
        var colunas = connection.Query<TableColumn>($"PRAGMA table_info({table});")
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!colunas.Contains(column))
        {
            connection.Execute($"ALTER TABLE {table} ADD COLUMN {column} {type};");
        }
    }
}
