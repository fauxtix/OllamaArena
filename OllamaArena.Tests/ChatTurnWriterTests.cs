using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Implementations.Repositories;
using OllamaArena.Services.Implementations.Services;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Data;

namespace OllamaArena.Tests;

/// <summary>
/// Testes do escritor transaccional de turnos contra SQLite em memória, com o esquema real.
/// O contrato que importa: um turno grava conversa + duas mensagens + resposta de benchmark
/// na mesma transacção, e uma falha em qualquer passo não deixa nada gravado.
/// </summary>
public class ChatTurnWriterTests : IDisposable
{
    private readonly SqliteConnection _anchor;
    private readonly string _connectionString;
    private readonly ChatTurnWriter _writer;

    public ChatTurnWriterTests()
    {
        SqliteTypeHandlers.Register();

        _connectionString = $"Data Source=file:turnowriter_{Guid.NewGuid():N}?mode=memory&cache=shared";
        _anchor = new SqliteConnection(_connectionString);
        _anchor.Open();

        var context = new ContextoTeste(_connectionString);
        DatabaseSchemaInitializer.EnsureSchema(context);

        IConversationRepository conversas = new ConversationRepository(
            context, NullLogger<ConversationRepository>.Instance);

        IBenchmarkCommandRepository benchmarks = new BenchmarkCommandRepository(
            context, NullLogger<BenchmarkCommandRepository>.Instance);

        _writer = new ChatTurnWriter(
            context, conversas, benchmarks, NullLogger<ChatTurnWriter>.Instance);
    }

    [Fact]
    public async Task WriteAsync_GravaConversaComDuasMensagensEBenchmark()
    {
        var resultado = await _writer.WriteAsync(TurnoCompleto(), "modelo-teste", conversationId: 0, promptId: 0);

        Assert.True(resultado.ConversationId > 0);
        Assert.True(resultado.PromptId > 0);

        Assert.Equal(1, Contar("Conversas"));
        Assert.Equal(2, Contar("ConversaMensagens"));
        Assert.Equal(1, Contar("Respostas"));

        Assert.Equal(
            ("user", "Pergunta de teste"),
            LerMensagem(resultado.ConversationId, "user"));

        Assert.Equal(
            ("assistant", "Resposta de teste"),
            LerMensagem(resultado.ConversationId, "assistant"));
    }

    [Fact]
    public async Task WriteAsync_ReutilizaOPromptIdNosTurnosSeguintes()
    {
        var primeiro = await _writer.WriteAsync(TurnoCompleto(), "modelo-teste", 0, 0);

        var segundo = await _writer.WriteAsync(
            TurnoCompleto(prompt: "Segunda pergunta de teste"), "modelo-teste", primeiro.ConversationId, primeiro.PromptId);

        Assert.Equal(primeiro.PromptId, segundo.PromptId);
        Assert.Equal(primeiro.ConversationId, segundo.ConversationId);

        // Duas respostas (uma por turno) sobre o mesmo prompt, e só um prompt gravado.
        Assert.Equal(2, Contar("Respostas"));
        Assert.Equal(1, Contar($"Prompts"));
    }

    [Fact]
    public async Task WriteAsync_CriaPromptNovoQuandoOAnteriorNãoExiste()
    {
        var primeiro = await _writer.WriteAsync(TurnoCompleto(), "modelo-teste", 0, 0);

        var segundo = await _writer.WriteAsync(
            TurnoCompleto(prompt: "Pergunta bem diferente"), "modelo-teste", primeiro.ConversationId, promptId: 0);

        Assert.NotEqual(primeiro.PromptId, segundo.PromptId);
        Assert.Equal(2, Contar($"Prompts"));
    }

    [Fact]
    public async Task WriteAsync_GravaMétricasDoBenchmarkECongelaOIdioma()
    {
        var turno = TurnoCompleto() with
        {
            PromptTokens = 100,
            EvalTokens = 50,
            EvalDurationNs = 2_000_000_000,
            LoadDurationNs = 500_000_000,
            SessionLanguage = "Portuguese (pt-PT)"
        };

        await _writer.WriteAsync(turno, "modelo-teste", 0, 0);

        using var ligacao = NovaLigacao(_connectionString);
        var resposta = ligacao.QuerySingle(
            "SELECT TamanhoTokens, TokensPorSegundo, TempoPuroMs, TempoCargaMs, IdiomaSessao FROM Respostas");

        Assert.Equal(150, resposta.TamanhoTokens);
        Assert.Equal(75.0, Math.Round((double)resposta.TokensPorSegundo, 1));
        Assert.Equal(2000.0, Math.Round((double)resposta.TempoPuroMs, 1));
        Assert.Equal(500.0, Math.Round((double)resposta.TempoCargaMs, 1));
        Assert.Equal("Portuguese (pt-PT)", resposta.IdiomaSessao);
    }

    [Fact]
    public async Task WriteAsync_SemMétricasNãoGravaBenchmark()
    {
        var turno = TurnoCompleto() with { EvalDurationNs = 0, EvalTokens = 0 };

        var resultado = await _writer.WriteAsync(turno, "modelo-teste", 0, 0);

        Assert.Equal(0, Contar("Respostas"));
        Assert.Equal(0, resultado.PromptId);
        Assert.Equal(2, Contar("ConversaMensagens"));
    }

    [Fact]
    public async Task WriteAsync_TurnoCancelado_LançaSemGravarNada()
    {
        var turno = TurnoCompleto() with { Completed = false };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _writer.WriteAsync(turno, "modelo-teste", 0, 0));

        Assert.Equal(0, Contar("Conversas"));
        Assert.Equal(0, Contar("ConversaMensagens"));
        Assert.Equal(0, Contar("Respostas"));
    }

    [Fact]
    public async Task WriteAsync_RespostaVazia_LançaSemGravarNada()
    {
        var turno = TurnoCompleto() with { AssistantText = "   " };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _writer.WriteAsync(turno, "modelo-teste", 0, 0));

        Assert.Equal(0, Contar("Conversas"));
        Assert.Equal(0, Contar("ConversaMensagens"));
    }

    [Fact]
    public async Task WriteAsync_FalhaNaGravação_ReverteTudoComChatTurnPersistenceException()
    {
        // Uma coluna NOT NULL sem valor faz a inserção estourar no meio da transacção:
        // nem a conversa nem as mensagens podem sobreviver.
        var writer = new ChatTurnWriter(
            new ContextoTesteQuebrado(_connectionString),
            new ConversationRepository(new ContextoTeste(_connectionString), NullLogger<ConversationRepository>.Instance),
            new BenchmarkCommandRepository(new ContextoTeste(_connectionString), NullLogger<BenchmarkCommandRepository>.Instance),
            NullLogger<ChatTurnWriter>.Instance);

        var erro = await Assert.ThrowsAsync<ChatTurnPersistenceException>(() =>
            writer.WriteAsync(TurnoCompleto(), "modelo-teste", 0, 0));

        Assert.IsType<SqliteException>(erro.InnerException);
        Assert.Equal(0, Contar("Conversas"));
        Assert.Equal(0, Contar("ConversaMensagens"));
        Assert.Equal(0, Contar("Respostas"));
    }

    [Fact]
    public async Task WriteAsync_UsaOSummaryComoDescriçãoDaConversa()
    {
        var turno = TurnoCompleto() with { Summary = "Resumo curto" };

        var resultado = await _writer.WriteAsync(turno, "modelo-teste", 0, 0);

        Assert.Equal("Resumo curto", resultado.Description);

        using var ligacao = NovaLigacao(_connectionString);
        Assert.Equal("Resumo curto", ligacao.QuerySingle<string>("SELECT Descricao FROM Conversas"));
    }

    [Fact]
    public async Task WriteAsync_SemSummaryUsaADescriçãoAutomática()
    {
        var resultado = await _writer.WriteAsync(TurnoCompleto(), "modelo-teste", 0, 0);

        Assert.False(string.IsNullOrWhiteSpace(resultado.Description));
    }

    [Fact]
    public async Task WriteAsync_AtualizaADataDaConversa()
    {
        var resultado = await _writer.WriteAsync(TurnoCompleto(), "modelo-teste", 0, 0);

        using (var anterior = NovaLigacao(_connectionString))
        {
            anterior.Execute("UPDATE Conversas SET DataUltimaAtividade = '2000-01-01' WHERE Id = @id",
                new { id = resultado.ConversationId });
        }

        await _writer.WriteAsync(
            TurnoCompleto(prompt: "Segunda pergunta"), "modelo-teste", resultado.ConversationId, resultado.PromptId);

        using var ligacao = NovaLigacao(_connectionString);
        var data = ligacao.QuerySingle<string>(
            "SELECT DataUltimaAtividade FROM Conversas WHERE Id = @id", new { id = resultado.ConversationId });

        Assert.NotEqual("2000-01-01", data);
    }

    private static ChatTurn TurnoCompleto(string prompt = "Pergunta de teste") => new()
    {
        UserPrompt = prompt,
        AssistantText = "Resposta de teste",
        Temperature = 0.3,
        ElapsedLabel = "1.23s",
        PromptTokens = 10,
        EvalTokens = 5,
        EvalDurationNs = 1_000_000_000,
        TotalMs = 1234,
        SessionLanguage = "Portuguese (pt-PT)"
    };

    private int Contar(string tabela)
    {
        using var ligacao = NovaLigacao(_connectionString);
        return ligacao.ExecuteScalar<int>($"SELECT COUNT(*) FROM {tabela}");
    }

    private (string Role, string Conteudo) LerMensagem(int conversationId, string role)
    {
        using var ligacao = NovaLigacao(_connectionString);
        return ligacao.QuerySingle<(string, string)>(
            "SELECT Role, Content FROM ConversaMensagens WHERE ConversationId = @id AND Role = @role",
            new { id = conversationId, role });
    }

    public void Dispose() => _anchor.Dispose();

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

    private sealed class ContextoTeste(string connectionString) : IDapperContext
    {
        public IDbConnection CreateConnection() => NovaLigacao(connectionString);

        public void Execute(Action<IDbConnection> @event)
        {
            using var ligacao = CreateConnection();
            @event(ligacao);
        }
    }

    /// <summary>
    /// Faz a inserção da mensagem do assistente falhar depois de a conversa e a mensagem do
    /// utilizador já terem sido escritas dentro da transacção — é o cenário de rollback.
    /// </summary>
    private sealed class ContextoTesteQuebrado(string connectionString) : IDapperContext
    {
        public IDbConnection CreateConnection()
        {
            var ligacao = NovaLigacao(connectionString);
            ligacao.CreateCommand();
            using var comando = ligacao.CreateCommand();
            comando.CommandText = "CREATE TRIGGER IF NOT EXISTS falha_assistente BEFORE INSERT ON ConversaMensagens " +
                                  "WHEN NEW.Role = 'assistant' SELECT RAISE(ABORT, 'falha propositada');";
            comando.ExecuteNonQuery();
            return ligacao;
        }

        public void Execute(Action<IDbConnection> @event)
        {
            using var ligacao = CreateConnection();
            @event(ligacao);
        }
    }
}