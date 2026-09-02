using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OllamaArena.Models.Entities;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Implementations.Repositories;
using OllamaArena.Services.Implementations.Services;

namespace OllamaArena.Tests;

/// <summary>
/// Testes de persistência do idioma da sessão (IdiomaSessao) nas respostas de
/// benchmark: gravação via CreateResponseAsync e leitura via GetAllBenchmarksAsync,
/// contra SQLite em memória com o esquema real da aplicação.
/// </summary>
public class BenchmarkRepositoryIdiomaSessaoTests : IDisposable
{
    private readonly SqliteConnection _anchor;
    private readonly string _connectionString;
    private readonly BenchmarkRepository _repo;

    public BenchmarkRepositoryIdiomaSessaoTests()
    {
        SqliteTypeHandlers.Register();

        // BD em memória com cache partilhada: sobrevive enquanto a ligação âncora estiver aberta,
        // permitindo que cada método do repositório abra a sua própria ligação.
        _connectionString = $"Data Source=file:idioroundtrip_{Guid.NewGuid():N}?mode=memory&cache=shared";
        _anchor = new SqliteConnection(_connectionString);
        _anchor.Open();

        var context = new ContextoTeste(_connectionString);
        DatabaseSchemaInitializer.EnsureSchema(context);

        _repo = new BenchmarkRepository(context, NullLogger<BenchmarkRepository>.Instance,
            Options.Create(new JudgeScoreWeights()));
    }

    [Fact]
    public async Task CreateResponseAsync_GravaELerIdiomaSessao()
    {
        int promptId = await CriarPromptAsync();
        await _repo.CreateResponseAsync(NovaResposta(promptId, "English (en-US)"));

        var lida = await LerUnicaRespostaAsync(promptId);

        Assert.Equal("English (en-US)", lida.IdiomaSessao);
    }

    [Fact]
    public async Task CreateResponseAsync_SemIdioma_LerNull()
    {
        int promptId = await CriarPromptAsync();
        await _repo.CreateResponseAsync(NovaResposta(promptId, null));

        var lida = await LerUnicaRespostaAsync(promptId);

        Assert.Null(lida.IdiomaSessao);
    }

    private static BenchmarkResponse NovaResposta(int promptId, string? idiomaSessao) => new()
    {
        PromptId = promptId,
        NomeModelo = "modelo-teste",
        TextoResposta = "Resposta de teste.",
        TokensPorSegundo = 10,
        TempoPuroMs = 1000,
        TempoCargaMs = 500,
        TempoProcessamento = 1500,
        TamanhoTokens = 20,
        IdiomaSessao = idiomaSessao
    };

    [Fact]
    public async Task GetAllBenchmarks_CarregaAs12MetricasE_RefusalHandled()
    {
        int promptId = await CriarPromptAsync();

        // Semeia diretamente via SQL (o CreateResponseAsync só persiste Factual/Formatting).
        using (var ligacao = NovaLigacao(_connectionString))
        {
            ligacao.Execute("""
                INSERT INTO Respostas
                (PromptId, NomeModelo, TextoResposta,
                 GeminiFactualRating, GeminiFormattingRating, GeminiComplianceRating, GeminiRelevanceRating,
                 GeminiToneRating, GeminiConcisenessRating, GeminiClarityRating, GeminiReadabilityRating,
                 GeminiHaloEffectRating, GeminiSafetyRating, GeminiLanguageConsistencyRating, GeminiLoopDetectionRating,
                 GeminiRefusalHandled, GeminiRating,
                 OpenRouterFactualRating, OpenRouterFormattingRating, OpenRouterComplianceRating, OpenRouterRelevanceRating,
                 OpenRouterToneRating, OpenRouterConcisenessRating, OpenRouterClarityRating, OpenRouterReadabilityRating,
                 OpenRouterHaloEffectRating, OpenRouterSafetyRating, OpenRouterLanguageConsistencyRating, OpenRouterLoopDetectionRating,
                 OpenRouterRefusalHandled, OpenRouterRating)
                VALUES
                (@PromptId, 'modelo-grafico', 'resposta',
                 4, 5, 4, 3, 5, 4, 5, 4, 4, 5, 3, 5, 0, 4,
                 5, 4, 4, 4, 4, 4, 3, 4, 4, 4, 4, 4, 1, 4);
                """, new { PromptId = promptId });
        }

        var lida = await LerUnicaRespostaAsync(promptId);

        Assert.Equal(5, lida.GeminiFormattingRating);
        Assert.Equal(3, lida.GeminiRelevanceRating);
        Assert.Equal(5, lida.GeminiLoopDetectionRating);
        Assert.Equal(0, lida.GeminiRefusalHandled);
        Assert.Equal(3, lida.GeminiLanguageConsistencyRating);
        Assert.Equal(5, lida.OpenRouterFactualRating);
        Assert.Equal(3, lida.OpenRouterClarityRating);
        Assert.Equal(4, lida.OpenRouterComplianceRating);
        Assert.Equal(1, lida.OpenRouterRefusalHandled);
    }

    private async Task<int> CriarPromptAsync()
    {
        var (promptId, _) = await _repo.GetOrCreatePromptIdAsync("Prompt de teste único.", "Teste");
        return promptId;
    }

    private async Task<BenchmarkResponse> LerUnicaRespostaAsync(int promptId)
    {
        var prompts = await _repo.GetAllBenchmarksAsync();
        return prompts.Single(p => p.Id == promptId).Answers.Single();
    }

    public void Dispose()
    {
        _anchor.Dispose();
    }

    private sealed class ContextoTeste(string connectionString) : OllamaArena.Services.Interfaces.Services.IDapperContext
    {
        public System.Data.IDbConnection CreateConnection() => NovaLigacao(connectionString);

        public void Execute(Action<System.Data.IDbConnection> @event)
        {
            using var ligacao = CreateConnection();
            @event(ligacao);
        }
    }

    private static SqliteConnection NovaLigacao(string connectionString)
    {
        var ligacao = new SqliteConnection(connectionString);
        ligacao.Open();
        using (var comando = ligacao.CreateCommand())
        {
            comando.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            comando.ExecuteNonQuery();
        }
        return ligacao;
    }
}
