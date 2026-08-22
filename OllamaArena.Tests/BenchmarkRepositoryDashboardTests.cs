using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Implementations.Repositories;
using OllamaArena.Services.Implementations.Services;
using OllamaArena.Services.Interfaces.Services;

namespace OllamaArena.Tests;

/// <summary>
/// Testes de integração dos métodos de agregação do Dashboard
/// (ranking com médias/divergência, estatísticas de cobertura e série temporal),
/// contra SQLite em memória com o esquema real da aplicação e dados conhecidos.
///
/// Dados semeado e valores esperados:
///   RA1 (P1, a): G12=4s, O12=5s            → G 4.00, O 5.00
///   RA2 (P2, a): G12=5s, OR declarado 3    → G 5.00, O 3.00 (fallback)
///   RB1 (P1, b): G12=1s                    → G 1.00 (sem OpenRouter)
///   RB2 (P2, b): sem avaliações            → ignorado nos scores
///   RC1 (P2, c): recusa correta (F/C/R=1, resto 9 métricas=5), OR declarado 4
///                                          → G 5.00 (exclui F/C/R), O 4.00
///   RD1 (P1, d): sem avaliações            → modelo excluído do ranking
/// </summary>
public class BenchmarkRepositoryDashboardTests : IDisposable
{
    private static readonly string[] Metricas =
    [
        "FactualRating", "FormattingRating", "ComplianceRating", "RelevanceRating",
        "ToneRating", "ConcisenessRating", "ClarityRating", "ReadabilityRating",
        "HaloEffectRating", "SafetyRating", "LanguageConsistencyRating", "LoopDetectionRating"
    ];

    private readonly SqliteConnection _anchor;
    private readonly string _connectionString;
    private readonly BenchmarkRepository _repo;

    public BenchmarkRepositoryDashboardTests()
    {
        SqliteTypeHandlers.Register();

        // BD em memória com cache partilhada: sobrevive enquanto a ligação âncora estiver aberta,
        // permitindo que cada método do repositório abra a sua própria ligação.
        _connectionString = $"Data Source=file:benchtests_{Guid.NewGuid():N}?mode=memory&cache=shared";
        _anchor = new SqliteConnection(_connectionString);
        _anchor.Open();

        var context = new ContextoTeste(_connectionString);
        DatabaseSchemaInitializer.EnsureSchema(context);

        SemearDados();
        _repo = new BenchmarkRepository(context, NullLogger<BenchmarkRepository>.Instance,
            Options.Create(new JudgeScoreWeights()));
    }

    [Fact]
    public async Task Ranking_CalculaScoresMediasEDivergencia()
    {
        var ranking = await _repo.GetModelRankingAsync();

        var c = ranking.Single(r => r.Model == "modelo-c");
        Assert.Equal(5.00, c.GeminiScore);
        Assert.Equal(4.00, c.OpenRouterScore);
        Assert.Equal(4.50, c.Score);
        Assert.Equal(1.00, c.JudgeDivergence);
        Assert.Equal(1, c.TotalResponses);
        Assert.Equal(2, c.JudgedResponses);
        Assert.Equal(50.00, c.TokensPerSecond);
        Assert.Equal(700, c.AvgResponseTimeMs);
        Assert.Equal(70, c.AvgResponseTokens);

        var a = ranking.Single(r => r.Model == "modelo-a");
        Assert.Equal(4.50, a.GeminiScore);
        Assert.Equal(4.00, a.OpenRouterScore);
        Assert.Equal(4.25, a.Score);
        Assert.Equal(0.50, a.JudgeDivergence);
        Assert.Equal(2, a.TotalResponses);
        Assert.Equal(4, a.JudgedResponses);
        Assert.Equal(15.00, a.TokensPerSecond);
        Assert.Equal(3000, a.AvgResponseTimeMs);
        Assert.Equal(200, a.AvgResponseTokens);

        var b = ranking.Single(r => r.Model == "modelo-b");
        Assert.Equal(1.00, b.GeminiScore);
        Assert.Equal(0, b.OpenRouterScore);
        Assert.Equal(1.00, b.Score);
        Assert.Null(b.JudgeDivergence);
        Assert.Equal(2, b.TotalResponses);
        Assert.Equal(1, b.JudgedResponses);
        Assert.Equal(35.00, b.TokensPerSecond);
        Assert.Equal(550, b.AvgResponseTimeMs);
        Assert.Equal(55, b.AvgResponseTokens);
    }

    [Fact]
    public async Task Ranking_OrdenaPorScoreEExcluiSemAvaliacoes()
    {
        var ranking = await _repo.GetModelRankingAsync();

        Assert.Equal(["modelo-c", "modelo-a", "modelo-b"], ranking.Select(r => r.Model));
        Assert.DoesNotContain(ranking, r => r.Model == "modelo-d");
    }

    [Fact]
    public async Task Stats_CalculaCoberturaEAtividade()
    {
        var stats = await _repo.GetDashboardStatsAsync();

        Assert.Equal(3, stats.TotalPrompts);
        Assert.Equal(6, stats.TotalRespostas);
        Assert.Equal(3, stats.RespostasAvaliadasAmbosJuizes);
        Assert.Equal(50, stats.CoberturaPercentual);
        Assert.Equal(new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc), stats.UltimaAtividade);
    }

    [Fact]
    public async Task Timeline_MediaAcumuladaPorModeloEIgnoraSemAvaliacao()
    {
        var timeline = await _repo.GetScoreTimelineAsync();

        var a = timeline.Single(t => t.Model == "modelo-a");
        Assert.Equal(2, a.Points.Count);
        Assert.Equal(4.50, a.Points[0].ScoreAcumulado);
        Assert.Equal(4.25, a.Points[1].ScoreAcumulado);
        Assert.True(a.Points[0].Data <= a.Points[1].Data);

        var b = timeline.Single(t => t.Model == "modelo-b");
        Assert.Single(b.Points);
        Assert.Equal(1.00, b.Points[0].ScoreAcumulado);

        var c = timeline.Single(t => t.Model == "modelo-c");
        Assert.Single(c.Points);
        Assert.Equal(4.50, c.Points[0].ScoreAcumulado);

        Assert.DoesNotContain(timeline, t => t.Model == "modelo-d");
    }

    public void Dispose() => _anchor.Dispose();

    private void SemearDados()
    {
        using var conn = NovaLigacao(_connectionString);

        conn.Execute("""
            INSERT INTO Prompts (Id, Descricao, TextoPrompt, DataCriacao, Temperatura) VALUES
            (1, 'p1', 'prompt 1', '2026-01-01T10:00:00Z', 0.3),
            (2, 'p2', 'prompt 2', '2026-01-02T10:00:00Z', 0.3),
            (3, 'p3', 'prompt 3', '2026-01-03T10:00:00Z', 0.3);
            """);

        InserirResposta(conn, promptId: 1, modelo: "modelo-a", tokensS: 10, tempoMs: 2000, tamanho: 100,
            gemini12: Preencher(4), openRouter12: Preencher(5));
        InserirResposta(conn, promptId: 2, modelo: "modelo-a", tokensS: 20, tempoMs: 4000, tamanho: 300,
            gemini12: Preencher(5), openRouterRating: 3);
        InserirResposta(conn, promptId: 1, modelo: "modelo-b", tokensS: 30, tempoMs: 500, tamanho: 50,
            gemini12: Preencher(1));

        InserirResposta(conn, promptId: 2, modelo: "modelo-b", tokensS: 40, tempoMs: 600, tamanho: 60);

        // Recusa correta: Factual/Compliance/Relevance=1 são excluídas da ponderação;
        // as restantes 9 métricas=5 produzem score 5.00.
        var recusa = new int?[12];
        recusa[0] = 1; recusa[2] = 1; recusa[3] = 1;
        for (var i = 0; i < 12; i++)
            if (!recusa[i].HasValue) recusa[i] = 5;
        InserirResposta(conn, promptId: 2, modelo: "modelo-c", tokensS: 50, tempoMs: 700, tamanho: 70,
            gemini12: recusa, openRouterRating: 4, geminiRecusa: 1);

        InserirResposta(conn, promptId: 1, modelo: "modelo-d", tokensS: 1, tempoMs: 1, tamanho: 1);
    }

    private static int?[] Preencher(int valor)
    {
        var metricas = new int?[12];
        Array.Fill(metricas, valor);
        return metricas;
    }

    private static void InserirResposta(SqliteConnection conn, int promptId, string modelo,
        double tokensS, double tempoMs, int tamanho,
        int?[]? gemini12 = null, int?[]? openRouter12 = null,
        float? geminiRating = null, float? openRouterRating = null, int? geminiRecusa = null)
    {
        var colunas = new List<string> { "PromptId", "NomeModelo", "TextoResposta", "TokensPorSegundo", "TempoPuroMs", "TamanhoTokens" };
        var parametros = new DynamicParameters();
        parametros.Add("PromptId", promptId);
        parametros.Add("NomeModelo", modelo);
        parametros.Add("TextoResposta", "resposta");
        parametros.Add("TokensPorSegundo", tokensS);
        parametros.Add("TempoPuroMs", tempoMs);
        parametros.Add("TamanhoTokens", tamanho);

        void AdicionarMetricas(string prefixo, int?[]? valores)
        {
            for (var i = 0; i < Metricas.Length; i++)
            {
                colunas.Add(prefixo + Metricas[i]);
                parametros.Add(prefixo + Metricas[i], valores is null ? null : valores[i]);
            }
        }

        AdicionarMetricas("Gemini", gemini12);
        AdicionarMetricas("OpenRouter", openRouter12);

        colunas.Add("GeminiRating");
        parametros.Add("GeminiRating", geminiRating);
        colunas.Add("OpenRouterRating");
        parametros.Add("OpenRouterRating", openRouterRating);
        colunas.Add("GeminiRefusalHandled");
        parametros.Add("GeminiRefusalHandled", geminiRecusa);

        conn.Execute(
            $"INSERT INTO Respostas ({string.Join(", ", colunas)}) VALUES ({string.Join(", ", colunas.Select(c => "@" + c))});",
            parametros);
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

    private sealed class ContextoTeste : IDapperContext
    {
        private readonly string _connectionString;

        public ContextoTeste(string connectionString) => _connectionString = connectionString;

        public System.Data.IDbConnection CreateConnection() => NovaLigacao(_connectionString);

        public void Execute(Action<System.Data.IDbConnection> @event)
        {
            using var ligacao = CreateConnection();
            @event(ligacao);
        }
    }
}
