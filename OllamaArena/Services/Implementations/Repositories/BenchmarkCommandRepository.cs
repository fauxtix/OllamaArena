using Dapper;
using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Data;
using System.Globalization;
using System.Text;

namespace OllamaArena.Services.Implementations.Repositories;

/// <summary>
/// IBenchmarkCommandRepository
/// </summary>
public class BenchmarkCommandRepository : IBenchmarkCommandRepository
{
    private readonly IDapperContext _context;
    private readonly ILogger<BenchmarkCommandRepository> _logger;

    public BenchmarkCommandRepository(IDapperContext context, ILogger<BenchmarkCommandRepository> logger)
    {
        _context = context;
        _logger = logger;
    }
    /// <summary>
    /// Insere um novo prompt e retorna o ID gerado pelo SQLite.
    /// </summary>
    public async Task<int> CreatePromptAsync(string textoPrompt, double temperatura, string descricao)
    {
        var sql = @"
                INSERT INTO Prompts (Descricao, TextoPrompt, DataCriacao, Temperatura ) 
                VALUES (@Descricao, @TextoPrompt, @DataCriacao, @Temperatura);
                SELECT last_insert_rowid();";
        using var connection = _context.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            Descricao = descricao,
            TextoPrompt = textoPrompt,
            DataCriacao = DateTime.UtcNow.ToString("o"),
            Temperatura = temperatura
        });
    }

    /// <summary>
    /// Procura um prompt existente pelo texto. Se não existe, cria um novo.
    /// Retorna o ID e a descrição efectivamente guardada.
    /// </summary>
    public async Task<(int PromptId, string Descricao)> GetOrCreatePromptIdAsync(string textoPrompt, string descricao,
        IDbConnection? connection = null, IDbTransaction? transaction = null)
    {
        return await RepositoryScope.RunAsync(_context, connection, transaction, async (conn, tx) =>
        {
            var existing = await conn.QueryFirstOrDefaultAsync<(int Id, string Descricao)?>(new CommandDefinition(
                "SELECT Id, Descricao FROM Prompts WHERE TextoPrompt = @TextoPrompt LIMIT 1",
                new { TextoPrompt = textoPrompt }, tx));

            if (existing.HasValue)
                return (existing.Value.Id, existing.Value.Descricao ?? string.Empty);

            var sql = @"
                INSERT INTO Prompts (Descricao, TextoPrompt, DataCriacao, Temperatura)
                VALUES (@Descricao, @TextoPrompt, @DataCriacao, 0);
                SELECT last_insert_rowid();";

            var newId = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
            {
                Descricao = descricao,
                TextoPrompt = textoPrompt,
                DataCriacao = DateTime.UtcNow.ToString("o")
            }, tx));

            return (newId, descricao);
        });
    }

    /// <summary>
    /// Insere a resposta e as métricas de um modelo específico associada a um Prompt.
    /// </summary>
    public async Task<bool> CreateResponseAsync(BenchmarkResponse response,
        IDbConnection? connection = null, IDbTransaction? transaction = null)
    {
        StringBuilder sb = new();
        sb.Append("INSERT INTO Respostas ");
        sb.Append("(PromptId, NomeModelo, TextoResposta, TokensPorSegundo, ");
        sb.Append("TempoPuroMs, TempoCargaMs, TamanhoTokens, TempoProcessamento, IdiomaSessao, ");
        sb.Append("GeminiFactualRating, GeminiFormattingRating, GeminiRating, GeminiFeedback, GeminiRecommendation, ");
        sb.Append("OpenRouterFactualRating, OpenRouterFormattingRating, OpenRouterRating, OpenRouterFeedback, OpenRouterRecommendation) ");
        sb.Append("VALUES ");
        sb.Append("(@PromptId, @NomeModelo, @TextoResposta, @TokensPorSegundo, ");
        sb.Append("@TempoPuroMs, @TempoCargaMs, @TamanhoTokens, @TempoProcessamento, @IdiomaSessao, ");
        sb.Append("@GeminiFactualRating, @GeminiFormattingRating, @GeminiRating, @GeminiFeedback, @GeminiRecommendation, ");
        sb.Append("@OpenRouterFactualRating, @OpenRouterFormattingRating, @OpenRouterRating, @OpenRouterFeedback, @OpenRouterRecommendation);");

        try
        {
            int affected = await RepositoryScope.RunAsync(_context, connection, transaction,
                (conn, tx) => conn.ExecuteAsync(new CommandDefinition(sb.ToString(), response, tx)));

            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar Response");
            return false;
        }
    }

    /// <summary>
    /// Permite editar o texto de um prompt
    /// </summary>
    public async Task<bool> UpdatePromptAsync(int id, string novoTexto)
    {
        var sql = "UPDATE Prompts SET TextoPrompt = @TextoPrompt WHERE Id = @Id;";
        using var connection = _context.CreateConnection();
        int AffectedLines = await connection.ExecuteAsync(sql, new { TextoPrompt = novoTexto, Id = id });
        return AffectedLines > 0;
    }

    /// <summary>
    /// Permite atualizar o texto de uma resposta 
    /// </summary>
    public async Task<bool> UpdateResponseTextAsync(int responseId, string newText)
    {
        var sql = "UPDATE Respostas SET TextoResposta = @ResponseText WHERE Id = @Id;";
        using var connection = _context.CreateConnection();
        int AffectedLines = await connection.ExecuteAsync(sql, new { ResponseText = newText, Id = responseId });
        return AffectedLines > 0;
    }

    public async Task<bool> UpdateResponseEvaluationAsync(BenchmarkResponse res)
    {
        StringBuilder sb = new();
        sb.Append("UPDATE Respostas ");
        sb.Append("SET ");
        // Avaliações Gerais / Ratings Finais
        sb.Append("GeminiRating = @GeminiRating, GeminiFeedback = @GeminiFeedback, GeminiRecommendation = @GeminiRecommendation, ");
        sb.Append("OpenRouterRating = @OpenRouterRating, OpenRouterFeedback = @OpenRouterFeedback, OpenRouterRecommendation = @OpenRouterRecommendation, ");

        // Novas métricas específicas do Gemini
        sb.Append("GeminiFactualRating = @GeminiFactualRating, GeminiFormattingRating = @GeminiFormattingRating, ");
        sb.Append("GeminiComplianceRating = @GeminiComplianceRating, GeminiRelevanceRating = @GeminiRelevanceRating, ");
        sb.Append("GeminiToneRating = @GeminiToneRating, GeminiConcisenessRating = @GeminiConcisenessRating, ");
        sb.Append("GeminiClarityRating = @GeminiClarityRating, GeminiReadabilityRating = @GeminiReadabilityRating, ");
        sb.Append("GeminiHaloEffectRating = @GeminiHaloEffectRating, GeminiSafetyRating = @GeminiSafetyRating, ");
        sb.Append("GeminiLanguageConsistencyRating = @GeminiLanguageConsistencyRating, GeminiLoopDetectionRating = @GeminiLoopDetectionRating, ");

        // Novas métricas específicas do OpenRouter
        sb.Append("OpenRouterFactualRating = @OpenRouterFactualRating, OpenRouterFormattingRating = @OpenRouterFormattingRating, ");
        sb.Append("OpenRouterComplianceRating = @OpenRouterComplianceRating, OpenRouterRelevanceRating = @OpenRouterRelevanceRating, ");
        sb.Append("OpenRouterToneRating = @OpenRouterToneRating, OpenRouterConcisenessRating = @OpenRouterConcisenessRating, ");
        sb.Append("OpenRouterClarityRating = @OpenRouterClarityRating, OpenRouterReadabilityRating = @OpenRouterReadabilityRating, ");
        sb.Append("OpenRouterHaloEffectRating = @OpenRouterHaloEffectRating, OpenRouterSafetyRating = @OpenRouterSafetyRating, ");
        sb.Append("OpenRouterLanguageConsistencyRating = @OpenRouterLanguageConsistencyRating, OpenRouterLoopDetectionRating = @OpenRouterLoopDetectionRating, ");

        // Flag de recusa correta (REFUSAL_HANDLED): usado pelo ScoreCalculator
        sb.Append("GeminiRefusalHandled = @GeminiRefusalHandled, OpenRouterRefusalHandled = @OpenRouterRefusalHandled, ");

        // Origem de cada slot de juiz ('gemini'/'openrouter'/'externo'/null)
        sb.Append("Juiz1Origem = @Juiz1Origem, Juiz2Origem = @Juiz2Origem ");

        sb.Append("WHERE Id = @Id;");

        var sql = sb.ToString();
        using var connection = _context.CreateConnection();
        int affectedLines = await connection.ExecuteAsync(sql, res);
        return affectedLines > 0;
    }

    /// <summary>
    /// Apaga um prompt. As respostas suas não são apagadas aqui: o <c>ON DELETE CASCADE</c>
    /// declarado no DDL de <c>Respostas</c> faz isso, desde que o
    /// <c>PRAGMA foreign_keys = ON</c> que o <c>DapperContext</c> aplica a cada ligação
    /// esteja activo — por isso a exclusão tem de passar pela ligação do <c>DapperContext</c>.
    /// </summary>
    public async Task<bool> DeletePromptAsync(int promptId)
    {
        var sql = "DELETE FROM Prompts WHERE Id = @Id; PRAGMA optimize;";
        using var connection = _context.CreateConnection();
        int AffectedLines = await connection.ExecuteAsync(sql, new { Id = promptId });
        return AffectedLines > 0;
    }

    /// <summary>
    /// Apaga todos os prompts; as respostas seguem por <c>ON DELETE CASCADE</c> (ver
    /// <see cref="DeletePromptAsync"/>).
    /// </summary>
    public async Task<bool> DeleteAllPromptsAsync()
    {
        var sql = "DELETE FROM Prompts; PRAGMA optimize;";
        using var connection = _context.CreateConnection();
        int AffectedLines = await connection.ExecuteAsync(sql);
        return AffectedLines > 0;
    }

    /// <summary>
    /// Apaga apenas a resposta de um modelo específico. Se for a última resposta do
    /// prompt, apaga também o prompt (tudo na mesma transacção).
    /// </summary>
    public async Task<bool> DeleteSpecificResponseAsync(int responseId)
    {
        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Obter o PromptId da resposta
            var promptId = await connection.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
                "SELECT PromptId FROM Respostas WHERE Id = @ResponseId",
                new { ResponseId = responseId }, transaction));

            if (promptId == null)
            {
                transaction.Rollback();
                return false;
            }

            // Apagar a resposta
            int affected = await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM Respostas WHERE Id = @ResponseId",
                new { ResponseId = responseId }, transaction));

            if (affected > 0)
            {
                int remaining = await connection.QueryFirstAsync<int>(new CommandDefinition(
                    "SELECT COUNT(*) FROM Respostas WHERE PromptId = @PromptId",
                    new { PromptId = promptId.Value }, transaction));

                if (remaining == 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "DELETE FROM Prompts WHERE Id = @PromptId",
                        new { PromptId = promptId.Value }, transaction));
                }
            }

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            // Reverter não pode substituir a exceção original: se o rollback também
            // falhar (ligação morta, transação já com estado inválido) o erro
            // original é o que interessa ao quem chamou.
            Reverter(transaction);
            throw;
        }
    }

    /// <summary>
    /// Reverte a transacção sem dejar a exceção do próprio rollback esconder a original.
    /// </summary>
    private static void Reverter(IDbTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (Exception)
        {
            // Silenciado de propósito: o rollback é o mecanismo de recuperação, a
            // falha que interessa propagar é a da operação original.
        }
    }

    /// <summary>
    /// Apaga várias respostas numa única transação.
    /// No final, apaga os Prompts que ficaram sem qualquer resposta (órfãos) entre os afetados.
    /// </summary>
    public async Task<int> DeleteResponsesAsync(IEnumerable<int> responseIds)
    {
        var ids = responseIds.Distinct().ToList();
        if (ids.Count == 0) return 0;

        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var promptIds = (await connection.QueryAsync<int>(
                "SELECT DISTINCT PromptId FROM Respostas WHERE Id IN @Ids",
                new { Ids = ids },
                transaction)).ToList();

            int affected = await connection.ExecuteAsync(
                "DELETE FROM Respostas WHERE Id IN @Ids",
                new { Ids = ids },
                transaction);

            if (promptIds.Count > 0)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM Prompts WHERE Id IN @PromptIds AND Id NOT IN (SELECT DISTINCT PromptId FROM Respostas)",
                    new { PromptIds = promptIds },
                    transaction);
            }

            transaction.Commit();
            return affected;
        }
        catch
        {
            Reverter(transaction);
            throw;
        }
    }
}
