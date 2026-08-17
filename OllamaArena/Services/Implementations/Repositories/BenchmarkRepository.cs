using Dapper;
using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Text;

namespace OllamaArena.Services.Implementations.Repositories;

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
    public async Task<(int PromptId, string Descricao)> GetOrCreatePromptIdAsync(string textoPrompt, string descricao)
    {
        using var connection = _context.CreateConnection();

        var existing = await connection.QueryFirstOrDefaultAsync<(int Id, string Descricao)?>(
            "SELECT Id, Descricao FROM Prompts WHERE TextoPrompt = @TextoPrompt LIMIT 1",
            new { TextoPrompt = textoPrompt });

        if (existing.HasValue)
            return (existing.Value.Id, existing.Value.Descricao ?? string.Empty);

        var sql = @"
            INSERT INTO Prompts (Descricao, TextoPrompt, DataCriacao, Temperatura)
            VALUES (@Descricao, @TextoPrompt, @DataCriacao, 0);
            SELECT last_insert_rowid();";

        var newId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            Descricao = descricao,
            TextoPrompt = textoPrompt,
            DataCriacao = DateTime.UtcNow.ToString("o")
        });

        return (newId, descricao);
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

    /// <summary>
    /// Apaga um prompt inteiro. Nota: Como configurámos ON DELETE CASCADE no DB Browser,
    /// todas as respostas associadas a este prompt serão apagadas automaticamente pelo SQLite!
    /// </summary>
    public async Task<bool> DeletePromptAndHistoryAsync(int promptId)
    {
        var sql = "DELETE FROM Prompts WHERE Id = @Id; PRAGMA optimize;";
        using var connection = _context.CreateConnection();
        int AffectedLines = await connection.ExecuteAsync(sql, new { Id = promptId });
        return AffectedLines > 0;
    }


    public async Task<bool> DeleteResponseByIdAsync(int responseId)
    {
        var sql = "DELETE FROM Respostas WHERE Id = @Id; PRAGMA optimize;";
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
        var sql = "DELETE FROM Prompts; PRAGMA optimize;";
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
        sb.Append("GeminiLanguageConsistencyRating = @GeminiLanguageConsistencyRating, GeminiLoopDetectionRating = @GeminiLoopDetectionRating, ");

        // Novas métricas específicas do OpenRouter
        sb.Append("OpenRouterFactualRating = @OpenRouterFactualRating, OpenRouterFormattingRating = @OpenRouterFormattingRating, ");
        sb.Append("OpenRouterComplianceRating = @OpenRouterComplianceRating, OpenRouterRelevanceRating = @OpenRouterRelevanceRating, ");
        sb.Append("OpenRouterToneRating = @OpenRouterToneRating, OpenRouterConcisenessRating = @OpenRouterConcisenessRating, ");
        sb.Append("OpenRouterClarityRating = @OpenRouterClarityRating, OpenRouterReadabilityRating = @OpenRouterReadabilityRating, ");
        sb.Append("OpenRouterHaloEffectRating = @OpenRouterHaloEffectRating, OpenRouterSafetyRating = @OpenRouterSafetyRating, ");
        sb.Append("OpenRouterLanguageConsistencyRating = @OpenRouterLanguageConsistencyRating, OpenRouterLoopDetectionRating = @OpenRouterLoopDetectionRating ");

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
        sb.Append("R.GeminiFeedback, R.GeminiRecommendation, R.OpenRouterFeedback, R.OpenRouterRecommendation ");
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
        var ranking = await GetModelRankingAsync();
        return ranking.FirstOrDefault()?.Model;
    }

    /// <summary>
    /// Ranking agregado por modelo, com score calculado pelo ScoreCalculator
    /// (média ponderada das 12 métricas por juiz). Quando uma resposta não tem
    /// métricas suficientes para o cálculo ponderado, usa o FINAL_SCORE declarado
    /// pelo próprio juiz como fallback.
    /// </summary>
    public async Task<List<ModelRanking>> GetModelRankingAsync()
    {
        var sql = @"SELECT NomeModelo,
            GeminiFactualRating, GeminiFormattingRating, GeminiComplianceRating, GeminiRelevanceRating,
            GeminiToneRating, GeminiConcisenessRating, GeminiClarityRating, GeminiReadabilityRating,
            GeminiHaloEffectRating, GeminiSafetyRating, GeminiLanguageConsistencyRating, GeminiLoopDetectionRating,
            CAST(GeminiRating AS REAL) AS GeminiRating,
            OpenRouterFactualRating, OpenRouterFormattingRating, OpenRouterComplianceRating, OpenRouterRelevanceRating,
            OpenRouterToneRating, OpenRouterConcisenessRating, OpenRouterClarityRating, OpenRouterReadabilityRating,
            OpenRouterHaloEffectRating, OpenRouterSafetyRating, OpenRouterLanguageConsistencyRating, OpenRouterLoopDetectionRating,
            CAST(OpenRouterRating AS REAL) AS OpenRouterRating
            FROM Respostas;";

        using var connection = _context.CreateConnection();
        var rows = (await connection.QueryAsync<BenchmarkResponse>(sql)).ToList();

        var pesos = new JudgeScoreWeights();
        var ranking = new List<ModelRanking>();

        foreach (var grupo in rows.GroupBy(r => r.NomeModelo))
        {
            double somaGemini = 0, somaOpenRouter = 0;
            int contagemGemini = 0, contagemOpenRouter = 0;

            foreach (var resposta in grupo)
            {
                var geminiScore = ScoreCalculator.CalcularScoreFinal(ToInput(resposta, juizGemini: true), pesos)
                                  ?? resposta.GeminiRating;
                if (geminiScore.HasValue)
                {
                    somaGemini += geminiScore.Value;
                    contagemGemini++;
                }

                var openRouterScore = ScoreCalculator.CalcularScoreFinal(ToInput(resposta, juizGemini: false), pesos)
                                      ?? resposta.OpenRouterRating;
                if (openRouterScore.HasValue)
                {
                    somaOpenRouter += openRouterScore.Value;
                    contagemOpenRouter++;
                }
            }

            int totalAvaliadas = contagemGemini + contagemOpenRouter;
            if (totalAvaliadas == 0)
                continue;

            ranking.Add(new ModelRanking
            {
                Model = grupo.Key,
                Score = Math.Round((somaGemini + somaOpenRouter) / totalAvaliadas, 2),
                GeminiScore = contagemGemini > 0 ? Math.Round(somaGemini / contagemGemini, 2) : 0,
                OpenRouterScore = contagemOpenRouter > 0 ? Math.Round(somaOpenRouter / contagemOpenRouter, 2) : 0,
                TotalResponses = grupo.Count(),
                JudgedResponses = totalAvaliadas
            });
        }

        return ranking.OrderByDescending(r => r.Score).ToList();
    }

    /// <summary>
    /// Dados agregados por modelo para os gráficos do Dashboard: score ponderado,
    /// média de tokens/s e médias das 12 métricas por juiz (radar e scatter).
    /// </summary>
    public async Task<List<ModelChartData>> GetModelChartDataAsync()
    {
        var sql = @"SELECT NomeModelo,
            GeminiFactualRating, GeminiFormattingRating, GeminiComplianceRating, GeminiRelevanceRating,
            GeminiToneRating, GeminiConcisenessRating, GeminiClarityRating, GeminiReadabilityRating,
            GeminiHaloEffectRating, GeminiSafetyRating, GeminiLanguageConsistencyRating, GeminiLoopDetectionRating,
            CAST(GeminiRating AS REAL) AS GeminiRating,
            OpenRouterFactualRating, OpenRouterFormattingRating, OpenRouterComplianceRating, OpenRouterRelevanceRating,
            OpenRouterToneRating, OpenRouterConcisenessRating, OpenRouterClarityRating, OpenRouterReadabilityRating,
            OpenRouterHaloEffectRating, OpenRouterSafetyRating, OpenRouterLanguageConsistencyRating, OpenRouterLoopDetectionRating,
            CAST(OpenRouterRating AS REAL) AS OpenRouterRating,
            TokensPorSegundo
            FROM Respostas;";

        using var connection = _context.CreateConnection();
        var rows = (await connection.QueryAsync<BenchmarkResponse>(sql)).ToList();

        var pesos = new JudgeScoreWeights();
        var resultado = new List<ModelChartData>();

        foreach (var grupo in rows.GroupBy(r => r.NomeModelo))
        {
            double somaGemini = 0, somaOpenRouter = 0;
            int contagemGemini = 0, contagemOpenRouter = 0;

            foreach (var resposta in grupo)
            {
                var geminiScore = ScoreCalculator.CalcularScoreFinal(ToInput(resposta, juizGemini: true), pesos)
                                  ?? resposta.GeminiRating;
                if (geminiScore.HasValue)
                {
                    somaGemini += geminiScore.Value;
                    contagemGemini++;
                }

                var openRouterScore = ScoreCalculator.CalcularScoreFinal(ToInput(resposta, juizGemini: false), pesos)
                                      ?? resposta.OpenRouterRating;
                if (openRouterScore.HasValue)
                {
                    somaOpenRouter += openRouterScore.Value;
                    contagemOpenRouter++;
                }
            }

            int totalAvaliadas = contagemGemini + contagemOpenRouter;
            if (totalAvaliadas == 0)
                continue;

            resultado.Add(new ModelChartData
            {
                Model = grupo.Key,
                Score = Math.Round((somaGemini + somaOpenRouter) / totalAvaliadas, 2),
                TokensPorSegundo = Math.Round(grupo.Average(r => r.TokensPorSegundo), 2),
                GeminiCount = contagemGemini,
                OpenRouterCount = contagemOpenRouter,
                GeminiMetrics = MediarMetricas(grupo, juizGemini: true),
                OpenRouterMetrics = MediarMetricas(grupo, juizGemini: false)
            });
        }

        return resultado.OrderByDescending(r => r.Score).ToList();
    }

    private static readonly (Func<BenchmarkResponse, int?> Gemini, Func<BenchmarkResponse, int?> OpenRouter)[] MetricasJuiz =
    [
        (r => r.GeminiFactualRating, r => r.OpenRouterFactualRating),
        (r => r.GeminiFormattingRating, r => r.OpenRouterFormattingRating),
        (r => r.GeminiComplianceRating, r => r.OpenRouterComplianceRating),
        (r => r.GeminiRelevanceRating, r => r.OpenRouterRelevanceRating),
        (r => r.GeminiToneRating, r => r.OpenRouterToneRating),
        (r => r.GeminiConcisenessRating, r => r.OpenRouterConcisenessRating),
        (r => r.GeminiClarityRating, r => r.OpenRouterClarityRating),
        (r => r.GeminiReadabilityRating, r => r.OpenRouterReadabilityRating),
        (r => r.GeminiHaloEffectRating, r => r.OpenRouterHaloEffectRating),
        (r => r.GeminiSafetyRating, r => r.OpenRouterSafetyRating),
        (r => r.GeminiLanguageConsistencyRating, r => r.OpenRouterLanguageConsistencyRating),
        (r => r.GeminiLoopDetectionRating, r => r.OpenRouterLoopDetectionRating)
    ];

    private static List<double> MediarMetricas(IEnumerable<BenchmarkResponse> respostas, bool juizGemini)
    {
        return MetricasJuiz.Select(par =>
        {
            var seletor = juizGemini ? par.Gemini : par.OpenRouter;
            var valores = respostas.Where(r => seletor(r).HasValue).Select(r => (double)seletor(r)!.Value).ToList();
            return valores.Count > 0 ? Math.Round(valores.Average(), 2) : 0;
        }).ToList();
    }

    private static JudgeScoreInput ToInput(BenchmarkResponse r, bool juizGemini)
    {
        if (juizGemini)
        {
            return new JudgeScoreInput(
                r.GeminiFactualRating, r.GeminiFormattingRating, r.GeminiComplianceRating, r.GeminiRelevanceRating,
                r.GeminiToneRating, r.GeminiConcisenessRating, r.GeminiClarityRating, r.GeminiReadabilityRating,
                r.GeminiHaloEffectRating, r.GeminiSafetyRating, r.GeminiLanguageConsistencyRating, r.GeminiLoopDetectionRating);
        }

        return new JudgeScoreInput(
            r.OpenRouterFactualRating, r.OpenRouterFormattingRating, r.OpenRouterComplianceRating, r.OpenRouterRelevanceRating,
            r.OpenRouterToneRating, r.OpenRouterConcisenessRating, r.OpenRouterClarityRating, r.OpenRouterReadabilityRating,
            r.OpenRouterHaloEffectRating, r.OpenRouterSafetyRating, r.OpenRouterLanguageConsistencyRating, r.OpenRouterLoopDetectionRating);
    }

}
