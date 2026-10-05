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
/// IBenchmarkQueryRepository
/// </summary>
public class BenchmarkQueryRepository : IBenchmarkQueryRepository
{
    private readonly IDapperContext _context;
    private readonly ILogger<BenchmarkQueryRepository> _logger;

    public BenchmarkQueryRepository(IDapperContext context, ILogger<BenchmarkQueryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }
    /// <summary>
    /// Procura a descrição de um prompt existente pelo texto. Retorna null se não existe.
    /// </summary>
    public async Task<string?> FindDescriptionByTextAsync(string textoPrompt)
    {
        using var connection = _context.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string?>(
            "SELECT Descricao FROM Prompts WHERE TextoPrompt = @TextoPrompt LIMIT 1",
            new { TextoPrompt = textoPrompt });
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
    /// Lê os feedbacks e recomendações dos dois juízes de uma resposta.
    /// Admite-se que a linha não exista (devolve um objeto vazio). Qualquer outra
    /// falha propaga-se, para que o diálogo de detalhe possa dizer "não consegui ler"
    /// em vez de "não há feedback registado" — que é uma informação errada.
    /// </summary>
    public async Task<PromptFeedback> GetBenchmarkJudgesFeedbackByIdAsync(int Id)
    {
        var sql = @"
                SELECT GeminiFeedback, OpenRouterFeedback, GeminiRecommendation, OpenRouterRecommendation, Juiz1Origem, Juiz2Origem FROM Respostas 
                WHERE Id = @Id;";

        using var connection = _context.CreateConnection();
        var response = await connection.QueryFirstOrDefaultAsync<PromptFeedback>(sql, new { Id });
        return response ?? new();
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
                    r.IdiomaSessao,
                    r.GeminiFactualRating,
                    r.GeminiFormattingRating,
                    r.GeminiComplianceRating,
                    r.GeminiRelevanceRating,
                    r.GeminiToneRating,
                    r.GeminiConcisenessRating,
                    r.GeminiClarityRating,
                    r.GeminiReadabilityRating,
                    r.GeminiHaloEffectRating,
                    r.GeminiSafetyRating,
                    r.GeminiLanguageConsistencyRating,
                    r.GeminiLoopDetectionRating,
                    r.GeminiRefusalHandled,
                    r.GeminiRating,
                    r.GeminiFeedback,
                    r.GeminiRecommendation,
                    r.OpenRouterFactualRating,
                    r.OpenRouterFormattingRating,
                    r.OpenRouterComplianceRating,
                    r.OpenRouterRelevanceRating,
                    r.OpenRouterToneRating,
                    r.OpenRouterConcisenessRating,
                    r.OpenRouterClarityRating,
                    r.OpenRouterReadabilityRating,
                    r.OpenRouterHaloEffectRating,
                    r.OpenRouterSafetyRating,
                    r.OpenRouterLanguageConsistencyRating,
                    r.OpenRouterLoopDetectionRating,
                    r.OpenRouterRefusalHandled,
                    r.OpenRouterRating,
                    r.OpenRouterFeedback,
                    r.OpenRouterRecommendation,
                    r.Juiz1Origem,
                    r.Juiz2Origem
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
    /// Obtém uma lista de avaliações de benchmark, incluindo as classificações dos modelos Gemini e OpenRouter, para cada prompt.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<BenchmarkEvaluationModel>> BenchmarkResponseEvaluationAsync()
    {
        StringBuilder sb = new();
        sb.Append("SELECT P.Id, R.Id AS ResponseId, R.PromptId, P.TextoPrompt, R.NomeModelo, ");

        // Métricas Gemini
        sb.Append("CAST(R.GeminiRating AS REAL) AS GeminiRating, R.GeminiFactualRating, R.GeminiFormattingRating, ");
        sb.Append("R.GeminiComplianceRating, R.GeminiRelevanceRating, R.GeminiToneRating, ");
        sb.Append("R.GeminiConcisenessRating, R.GeminiClarityRating, R.GeminiReadabilityRating, ");
        sb.Append("R.GeminiHaloEffectRating, R.GeminiSafetyRating, ");
        sb.Append("R.GeminiLanguageConsistencyRating, R.GeminiLoopDetectionRating, ");

        // Métricas OpenRouter
        sb.Append("CAST(R.OpenRouterRating AS REAL) AS OpenRouterRating, R.OpenRouterFactualRating, R.OpenRouterFormattingRating, ");
        sb.Append("R.OpenRouterComplianceRating, R.OpenRouterRelevanceRating, R.OpenRouterToneRating, ");
        sb.Append("R.OpenRouterConcisenessRating, R.OpenRouterClarityRating, R.OpenRouterReadabilityRating, ");
        sb.Append("R.OpenRouterHaloEffectRating, R.OpenRouterSafetyRating, ");
        sb.Append("R.OpenRouterLanguageConsistencyRating, R.OpenRouterLoopDetectionRating, ");

        // Métricas de Performance e Datas
        sb.Append("P.DataCriacao, P.Descricao, R.TokensPorSegundo, R.TempoPuroMs, R.TempoCargaMs, R.TamanhoTokens, ");
        sb.Append("R.GeminiFeedback, R.GeminiRecommendation, R.OpenRouterFeedback, R.OpenRouterRecommendation, ");
        sb.Append("R.Juiz1Origem, R.Juiz2Origem ");
        sb.Append("FROM Prompts P ");
        // INNER JOIN (e não LEFT): um prompt sem respostas não é uma avaliação e
        // produzia linhas fantasma com ResponseId = 0 — contavam para os totais
        // "por avaliar"/"filtrados", iam parar ao Excel/PDF como modelos em branco e
        // tornavam a eliminação um no-op silencioso (apagar o id 0 não apaga nada).
        sb.Append("INNER JOIN Respostas R ON R.PromptId = P.Id");

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
}
