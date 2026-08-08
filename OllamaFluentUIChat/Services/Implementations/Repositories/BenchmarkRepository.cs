using Dapper;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Text;

namespace OllamaFluentUIChat.Services.Implementations.Repositories;

public class BenchmarkRepository : IBenchmarkRepository
{
    private readonly IDapperContext _context;
    private readonly ILogger<BenchmarkRepository> _logger;

    public BenchmarkRepository(IDapperContext context, ILogger<BenchmarkRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Insere um novo prompt e retorna o ID gerado pelo SQLite.
    /// </summary>
    public async Task<int> CreatePromptAsync(string textoPrompt, double temperatura)
    {
        var description = PromptSummarizer.ExtractDescription(textoPrompt);

        var sql = @"
                INSERT INTO Prompts (Descricao, TextoPrompt, DataCriacao, Temperatura ) 
                VALUES (@Descricao, @TextoPrompt, @DataCriacao, @Temperatura);
                SELECT last_insert_rowid();";
        using var connection = _context.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            Descricao = description,
            TextoPrompt = textoPrompt,
            DataCriacao = DateTime.UtcNow.ToString("o"),
            Temperatura = temperatura
        });
    }

    /// <summary>
    /// Insere a resposta e as métricas de um modelo específico associada a um Prompt.
    /// </summary>
    public async Task CreateResponseAsync(BenchmarkResponse response)
    {
        StringBuilder sb = new();
        sb.Append("INSERT INTO Respostas ");
        sb.Append("(PromptId, NomeModelo, TextoResposta, TokensPorSegundo, ");
        sb.Append("TempoPuroMs, TempoCargaMs, TamanhoTokens, TempoProcessamento, ");
        sb.Append("GeminiFactualRating, GeminiFormattingRating, GeminiRating, GeminiFeedback, GeminiRecommendation, ");
        sb.Append("OpenRouterFactualRating, OpenRouterFormattingRating, OpenRouterRating, OpenRouterFeedback, OpenRouterRecommendation) ");
        sb.Append("VALUES ");
        sb.Append("(@PromptId, @NomeModelo, @TextoResposta, @TokensPorSegundo, ");
        sb.Append("@TempoPuroMs, @TempoCargaMs, @TamanhoTokens, @TempoProcessamento, ");
        sb.Append("@GeminiFactualRating, @GeminiFormattingRating, @GeminiRating, @GeminiFeedback, @GeminiRecommendation, ");
        sb.Append("@OpenRouterFactualRating, @OpenRouterFormattingRating, @OpenRouterRating, @OpenRouterFeedback, @OpenRouterRecommendation);");



        try
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(sb.ToString(), response);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar Response");
        }
    }

    /// <summary>
    /// Retorna a lista de todos os prompts com as suas respetivas respostas (Ideal para a Split View de Comparação).
    /// </summary>
    public async Task<List<BenchmarkPrompt>> GetAllBenchmarksAsync()
    {
        var sql = @"
                SELECT 
                    p.Id, 
                    p.TextoPrompt, 
                    p.DataCriacao, 
                    p.Temperatura,
                    r.Id,
                    r.PromptId,
                    r.NomeModelo, 
                    r.TextoResposta, 
                    r.TokensPorSegundo, 
                    r.TempoPuroMs, 
                    r.TempoCargaMs, 
                    r.TamanhoTokens, 
                    r.GeminiFactualRating, 
                    r.GeminiFormattingRating, 
                    r.GeminiRating, 
                    r.GeminiFeedback, 
                    r.GeminiRecommendation, 
                    r.OpenRouterFactualRating, 
                    r.OpenRouterFormattingRating, 
                    r.OpenRouterRating, 
                    r.OpenRouterFeedback, 
                    r.OpenRouterRecommendation
                FROM Prompts p
                LEFT JOIN Respostas r ON p.Id = r.PromptId
                ORDER BY p.Id DESC, r.TokensPorSegundo DESC;";

        using var connection = _context.CreateConnection();
        var promptDictionary = new Dictionary<int, BenchmarkPrompt>();

        await connection.QueryAsync<BenchmarkPrompt, BenchmarkResponse, BenchmarkPrompt>(
            sql,
            (prompt, response) =>
            {
                if (!promptDictionary.TryGetValue(prompt.Id, out var existingPrompt))
                {
                    existingPrompt = prompt;
                    promptDictionary.Add(existingPrompt.Id, existingPrompt);
                }

                if (response != null)
                {
                    existingPrompt.Answers.Add(response);
                }

                return existingPrompt;
            },
            splitOn: "Id" // Diz ao Dapper que a segunda tabela começa na coluna 'Id' da Resposta
        );

        return promptDictionary.Values.ToList();
    }

    /// <summary>
    /// Procura um único prompt e as suas respostas através do ID.
    /// </summary>
    public async Task<BenchmarkPrompt?> GetBenchmarkByIdAsync(int promptId)
    {
        var sql = @"
                SELECT p.*, r.* FROM Prompts p
                LEFT JOIN Respostas r ON p.Id = r.PromptId
                WHERE p.Id = @Id;";

        BenchmarkPrompt? promptResult = null;

        using var connection = _context.CreateConnection();
        await connection.QueryAsync<BenchmarkPrompt, BenchmarkResponse, BenchmarkPrompt>(
            sql,
            (prompt, response) =>
            {
                if (promptResult == null)
                {
                    promptResult = prompt;
                }
                if (response != null)
                {
                    promptResult.Answers.Add(response);
                }
                return prompt;
            },
            new { Id = promptId },
            splitOn: "Id"
        );
        return promptResult;
    }

    /// <summary>
    /// Procura um único prompt e o feedback dos juizes através do ID.
    /// </summary>
    public async Task<PromptFeedback> GetBenchmarkJudgesFeedbackByIdAsync(int Id)
    {
        var sql = @"
                SELECT GeminiFeedback, OpenRouterFeedback, GeminiRecommendation, OpenRouterRecommendation FROM Respostas 
                WHERE Id = @Id;";

        try
        {
            using var connection = _context.CreateConnection();
            var response = await connection.QueryFirstAsync<PromptFeedback>(sql, new { Id });
            return response ?? new();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar feedback dos juízes para o prompt ID {PromptId}", Id);
            return new();
        }
    }


    /// <summary>
    /// Procura um único prompt e o feedback dos juizes através do ID.
    /// </summary>
    public async Task<BenchmarkResponse> GetBenchmarkAnswersByIdAsync(int id)
    {
        var sql = @"
                SELECT * FROM Respostas
                WHERE Id = @Id;";

        try
        {
            using var connection = _context.CreateConnection();
            var response = await connection.QueryFirstAsync<BenchmarkResponse>(sql, new { Id = id });
            return response ?? new();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar resposta para o  ID {id}", id);
            return new();
        }
    }


    /// <summary>
    /// Permite editar o texto de um prompt
    /// </summary>
    public async Task<bool> UpdatePromptAsync(int id, string novoTexto)
    {
        var description = PromptSummarizer.ExtractDescription(novoTexto);
        var sql = "UPDATE Prompts SET Descricao = @Descricao, TextoPrompt = @TextoPrompt WHERE Id = @Id;";
        using var connection = _context.CreateConnection();
        int AffectedLines = await connection.ExecuteAsync(sql, new { Descricao = description, TextoPrompt = novoTexto, Id = id });
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

    /// <summary>
    /// Apaga um prompt inteiro. Nota: Como configurámos ON DELETE CASCADE no DB Browser,
    /// todas as respostas associadas a este prompt serão apagadas automaticamente pelo SQLite!
    /// </summary>
    public async Task<bool> DeletePromptAndHistoryAsync(int promptId)
    {
        var sql = "DELETE FROM Prompts WHERE Id = @Id; VACUUM;";
        using var connection = _context.CreateConnection();
        int AffectedLines = await connection.ExecuteAsync(sql, new { Id = promptId });
        return AffectedLines > 0;
    }


    public async Task<bool> DeleteResponseByIdAsync(int responseId)
    {
        var sql = "DELETE FROM Respostas WHERE Id = @Id; VACUUM;";
        using var connection = _context.CreateConnection();
        int AffectedLines = await connection.ExecuteAsync(sql, new { Id = responseId });
        return AffectedLines > 0;
    }



    /// <summary>
    /// Apaga todos os Prompts. Como configurámos ON DELETE CASCADE no DB Browser,
    /// todas as respostas associadas serão apagadas automaticamente pelo SQLite!
    /// </summary>
    public async Task<bool> DeleteAllPromptsAndHistoryAsync()
    {
        var sql = "DELETE FROM Prompts; VACUUM;";
        using var connection = _context.CreateConnection();
        int AffectedLines = await connection.ExecuteAsync(sql);
        return AffectedLines > 0;
    }

    /// <summary>
    /// Apaga apenas a resposta de um modelo específico, 
    /// só apaga o prompt / pai, se estiver a apagar a única resposta ainda existente
    /// </summary>
    /// <summary>
    /// Apaga uma resposta específica. 
    /// Se for a última resposta do Prompt, também apaga o Prompt.
    /// </summary>
    public async Task<bool> DeleteSpecificResponseAsync(int responseId)
    {
        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Obter o PromptId da resposta
            var promptId = connection.QueryFirstOrDefault<int?>(
                "SELECT PromptId FROM Respostas WHERE Id = @ResponseId",
                new { ResponseId = responseId },
                transaction);

            if (promptId == null)
            {
                transaction.Rollback();
                return false;
            }

            // Apagar a resposta
            int affected = connection.Execute(
                "DELETE FROM Respostas WHERE Id = @ResponseId",
                new { ResponseId = responseId },
                transaction);

            if (affected > 0)
            {
                int remaining = connection.QueryFirst<int>(
                    "SELECT COUNT(*) FROM Respostas WHERE PromptId = @PromptId",
                    new { PromptId = promptId.Value },
                    transaction);

                if (remaining == 0)
                {
                    connection.Execute(
                        "DELETE FROM Prompts WHERE Id = @PromptId",
                        new { PromptId = promptId.Value },
                        transaction);
                }
            }

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
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

        // Novas métricas específicas do OpenRouter
        sb.Append("OpenRouterFactualRating = @OpenRouterFactualRating, OpenRouterFormattingRating = @OpenRouterFormattingRating, ");
        sb.Append("OpenRouterComplianceRating = @OpenRouterComplianceRating, OpenRouterRelevanceRating = @OpenRouterRelevanceRating, ");
        sb.Append("OpenRouterToneRating = @OpenRouterToneRating, OpenRouterConcisenessRating = @OpenRouterConcisenessRating, ");
        sb.Append("OpenRouterClarityRating = @OpenRouterClarityRating, OpenRouterReadabilityRating = @OpenRouterReadabilityRating, ");
        sb.Append("OpenRouterHaloEffectRating = @OpenRouterHaloEffectRating, OpenRouterSafetyRating = @OpenRouterSafetyRating ");

        sb.Append("WHERE Id = @Id;");

        var sql = sb.ToString();
        using var connection = _context.CreateConnection();
        int affectedLines = await connection.ExecuteAsync(sql, res);
        return affectedLines > 0;
    }
    /// <summary>
    /// Obtém uma lista de avaliações de benchmark, incluindo as classificações dos modelos Gemini e OpenRouter, para cada prompt.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<BenchmarkEvaluationModel>> BenchmarkResponseEvaluationAsync()
    {
        StringBuilder sb = new();
        sb.Append("SELECT P.Id, R.Id AS ResponseId, P.TextoPrompt, R.NomeModelo, ");

        // Métricas Gemini
        sb.Append("R.GeminiRating, R.GeminiFactualRating, R.GeminiFormattingRating, ");
        sb.Append("R.GeminiComplianceRating, R.GeminiRelevanceRating, R.GeminiToneRating, ");
        sb.Append("R.GeminiConcisenessRating, R.GeminiClarityRating, R.GeminiReadabilityRating, ");
        sb.Append("R.GeminiHaloEffectRating, R.GeminiSafetyRating, ");

        // Métricas OpenRouter
        sb.Append("R.OpenRouterRating, R.OpenRouterFactualRating, R.OpenRouterFormattingRating, ");
        sb.Append("R.OpenRouterComplianceRating, R.OpenRouterRelevanceRating, R.OpenRouterToneRating, ");
        sb.Append("R.OpenRouterConcisenessRating, R.OpenRouterClarityRating, R.OpenRouterReadabilityRating, ");
        sb.Append("R.OpenRouterHaloEffectRating, R.OpenRouterSafetyRating, ");

        // Métricas de Performance e Datas
        sb.Append("P.DataCriacao, P.Descricao, R.TokensPorSegundo, R.TempoPuroMs, R.TempoCargaMs, R.TamanhoTokens ");
        sb.Append("FROM Prompts P ");
        sb.Append("LEFT JOIN Respostas R ON R.PromptId = P.Id");

        var sql = sb.ToString();
        using var connection = _context.CreateConnection();
        var result = await connection.QueryAsync<BenchmarkEvaluationModel>(sql);
        return [.. result];
    }

    /// <summary>
    /// Get History and responses
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<HistoryResponse>> HistoryResponseAsync()
    {
        StringBuilder sb = new();
        sb.Append("SELECT P.id, P.Descricao, P.TextoPrompt Prompt, P.DataCriacao, P.Temperatura, ");
        sb.Append("R.TempoPuroMs TempoPuro, R.TempoCargaMs TempoCarga, R.NomeModelo Modelo, R.TempoProcessamento ");
        sb.Append("FROM prompts P ");
        sb.Append("INNER JOIN Respostas R ");
        sb.Append("ON P.id = R.PromptId ");
        sb.Append("ORDER BY P.id DESC");

        var sql = sb.ToString();
        using var connection = _context.CreateConnection();
        var result = await connection.QueryAsync<HistoryResponse>(sql);
        return [.. result];

    }
    public async Task<string?> GetBestModelAsync()
    {
        var sql = @"
        SELECT NomeModelo
        FROM (
            SELECT  
                NomeModelo,
                AVG((COALESCE(GeminiFactualRating, GeminiRating) + COALESCE(OpenRouterFactualRating, OpenRouterRating)) * 10.0) AS ScoreFinal
            FROM Respostas
            GROUP BY NomeModelo
        )
        ORDER BY ScoreFinal DESC
        LIMIT 1;";

        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(sql);
    }

}
