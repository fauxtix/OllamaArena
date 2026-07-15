// Services/LocalAnalysisService.cs
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace OllamaFluentUIChat.Services.Implementations.Services;

public class LocalAnalysisService : IAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalAnalysisService> _logger;
    private readonly IBenchmarkRepository _benchmarkRepository;
    private const string OllamaEndpoint = "http://localhost:11434/api/chat";

    // Modelos recomendados para boa obediência de JSON
    private const string ModelName = "qwen2.5:3b"; // ou Gemma3:4B, llama3.2, etc.

    public LocalAnalysisService(HttpClient httpClient, ILogger<LocalAnalysisService> logger, IBenchmarkRepository benchmarkRepository)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromMinutes(10); // Aumentado para análises longas
        _benchmarkRepository = benchmarkRepository;
    }

    public async Task<BenchmarkAnalysisResult> AnalisarBenchmarksAsync(
        List<BenchmarkEvaluationModel> benchmarks,
        CancellationToken cancellationToken = default)
    {
        if (benchmarks == null || !benchmarks.Any())
        {
            _logger.LogWarning("Nenhum benchmark para analisar.");
            return new BenchmarkAnalysisResult { Sumario = "Não existem dados para analisar." };
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var markdownTabela = GerarTabelaMarkdown(benchmarks);

            string systemPrompt = """
You are an AI assistant specialised in analysing software engineering benchmark results.

You will receive a table containing benchmark results for multiple language models.

Analyse only the benchmark data provided by the user.

Return exactly one valid JSON object.

Rules:
- Return only the JSON object.
- Do not use Markdown code fences.
- Do not write any text before or after the JSON.
- Do not invent information.
- Return model names exactly as they appear in the benchmark table.

The JSON object must contain exactly:
{
  "Sumario": "",
  "AnaliseDetalhada": "",
  "ModeloMaisRapido": "",
  "MaxTokensSec": 0.0,
  "ModeloMelhorAvaliado": ""
}

Requirements:

Sumario
- Write in European Portuguese.
- Maximum 4 sentences.
- Summarise the benchmark results.
- Mention the main conclusions.

AnaliseDetalhada
- Write in European Portuguese.
- Use Markdown.
- Compare speed (Tokens/s), execution time and quality ratings.
- Explain the most relevant trade-offs.
- Base every conclusion only on the benchmark data.
- Use \n for line breaks.

ModeloMaisRapido
- Return exactly the model with the highest Tokens/s.

MaxTokensSec
- Return exactly the Tokens/s value corresponding to ModeloMaisRapido.

ModeloMelhorAvaliado
- Return exactly the model with the highest average of Gemini Rating and ChatGPT Rating.
""";

            var payload = new
            {
                model = ModelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new
                    {
                        role = "user",
                        content = $"""
                            Segue a tabela de resultados dos benchmarks:

                            {markdownTabela}
                            """
                    }
                },
                stream = false,
                options = new { temperature = 0.1 },
                format = "json"
            };

            var requestContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, OllamaEndpoint)
            {
                Content = requestContent
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseBody);
            string contentGerado = doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var resultado = JsonSerializer.Deserialize<BenchmarkAnalysisResult>(contentGerado);

            stopwatch.Stop();

            if (resultado != null)
            {
                resultado.TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss");
                return resultado;
            }

            return ObterFallback(benchmarks, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Análise local foi cancelada pelo utilizador.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao analisar benchmarks localmente.");
            stopwatch.Stop();
            return new BenchmarkAnalysisResult
            {
                Sumario = "⚠️ Ocorreu um erro durante a análise.",
                AnaliseDetalhada = $"Erro: {ex.Message}",
                TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss")
            };
        }
    }

    private string GerarTabelaMarkdown(List<BenchmarkEvaluationModel> lista)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Prompt ID | Modelo | Gemini Rating | ChatGPT Rating | Tokens/s | Tempo |");
        sb.AppendLine("|---|---|---|---|---|---|");

        foreach (var item in lista)
        {
            sb.AppendLine($"| {item.PromptId} | {item.NomeModelo} | {item.GeminiRating} | {item.ChatGptRating} | {item.TokensPorSegundo:F1} | {item.TempoPuroFormatado} |");
        }

        return sb.ToString();
    }

    private BenchmarkAnalysisResult ObterFallback(List<BenchmarkEvaluationModel> benchmarks, TimeSpan elapsed)
    {
        var maisRapido = benchmarks.OrderByDescending(b => b.TokensPorSegundo).FirstOrDefault();
        return new BenchmarkAnalysisResult
        {
            Sumario = "Análise concluída (Fallback de segurança).",
            AnaliseDetalhada = "Ocorreu uma falha no processamento da resposta do modelo.",
            ModeloMaisRapido = maisRapido?.NomeModelo ?? "N/A",
            MaxTokensSec = maisRapido?.TokensPorSegundo ?? 0,
            ModeloMelhorAvaliado = "N/A",
            TempoAnaliseFormatado = elapsed.ToString(@"mm\:ss")
        };
    }
}