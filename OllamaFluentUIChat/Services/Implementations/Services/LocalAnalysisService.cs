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
    private readonly PromptFilesService _promptFilesService;
    private string analysisSystemPrompt = string.Empty;

    private const string OllamaEndpoint = "http://localhost:11434/api/chat";
    private const string ModelName = "Qwen2.5:3b";

    public LocalAnalysisService(HttpClient httpClient,
                                ILogger<LocalAnalysisService> logger,
                                IBenchmarkRepository benchmarkRepository,
                                PromptFilesService promptFilesService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromMinutes(5); 
        _benchmarkRepository = benchmarkRepository;
        _promptFilesService = promptFilesService;
    }

    public async Task<BenchmarkAnalysisResult> AnalisarBenchmarksAsync(
        List<BenchmarkEvaluationModel> benchmarks,
        CancellationToken cancellationToken = default)
    {
        if (benchmarks == null || benchmarks.Count == 0)
        {
            _logger.LogWarning("Nenhum benchmark para analisar.");
            return new BenchmarkAnalysisResult { Sumario = "Não existem dados para analisar." };
        }

        var stopwatch = Stopwatch.StartNew();
         analysisSystemPrompt =  await _promptFilesService.GetPromptFileContentAsync("analysis-prompt.txt") ?? string.Empty;

        // 1. Converter os dados complexos numa tabela Markdown compacta (O LLM gosta deste formato)
        var markdownTable = GenerateMarkdownTable(benchmarks);

        var payload = new
        {
            model = ModelName,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = analysisSystemPrompt
                },
                new
                {
                    role = "user",
                    content = $"""
                        Below is the table showing the benchmark results.

                        {markdownTable}
                        """
                }
            },
            stream = false,
            options = new
            {
                temperature = 0.1
            },
            format = "json"
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OllamaEndpoint)
            {
                Content = requestContent
            };

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseBody);
            string contentGerado = doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var resultado = JsonSerializer.Deserialize<BenchmarkAnalysisResult>(contentGerado);
            stopwatch.Stop();

            resultado?.TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss");
            return resultado ?? GetFallback(benchmarks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Análise cancelada pelo utilizador.");
            throw new OperationCanceledException();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desserializar a resposta do modelo. Retornando fallback amigável.");
            stopwatch.Stop();

            // Fallback
            return new BenchmarkAnalysisResult
            {
                Sumario = "⚠️ A análise local foi concluída, mas o modelo não conseguiu estruturar os dados em JSON.",
                AnaliseDetalhada = "Erro durante o processamento.",
                ModeloMaisRapido = "Ver Detalhes",
                ModeloMelhorAvaliado = "Ver Detalhes",
                TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss")
            };
        }
    }

    private string GenerateMarkdownTable(List<BenchmarkEvaluationModel> lista)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Prompt ID | Model | Gemini Rating | ChatGPT Rating |  Gemini Format Rating | ChatGPT Format Rating | Tokens/s | Time it took |");
        sb.AppendLine("|---|---|---|---|---|---|");

        foreach (var item in lista)
        {
            sb.AppendLine($"| {item.PromptId} | {item.NomeModelo} | {item.GeminiRating} | {item.ChatGptRating} | {item.ChatGptFormattingRating} | {item.GeminiFormattingRating} {item.TokensPorSegundo:F1} | {item.TempoPuroFormatado} |");
        }

        return sb.ToString();
    }

    private BenchmarkAnalysisResult GetFallback(List<BenchmarkEvaluationModel> benchmarks)
    {
        var fasterModel = benchmarks.OrderByDescending(b => b.TokensPorSegundo).FirstOrDefault();
        return new BenchmarkAnalysisResult
        {
            Sumario = "Análise concluída (Fallback de segurança).",
            AnaliseDetalhada = "Ocorreu uma falha no parse do modelo, mas a recolha de dados básicos foi concluída.",
            ModeloMaisRapido = fasterModel?.NomeModelo ?? "N/A",
            MaxTokensSec = fasterModel?.TokensPorSegundo ?? 0,
            ModeloMelhorAvaliado = "N/A"
        };
    }

    public async Task<BenchmarkAnalysisResult> AnalisarBenchmarksWithStreamingAsync(
        List<BenchmarkEvaluationModel> benchmarks,
        Action<string>? onPartialOutput,
        CancellationToken cancellationToken = default)
    {
        if (benchmarks == null || !benchmarks.Any())
        {
            return new BenchmarkAnalysisResult { Sumario = "Não existem dados para analisar." };
        }

        var stopwatch = Stopwatch.StartNew();
        var markdownTable = GenerateMarkdownTable(benchmarks);

        var payload = new
        {
            model = ModelName,
            messages = new[]
            {
            new { role = "system", content = analysisSystemPrompt },
            new { role = "user", content = $"Segue a tabela:\n\n{markdownTable}" }
        },
            stream = false, 
            options = new { temperature = 0.1 },
            format = "json"
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, OllamaEndpoint)
        {
            Content = requestContent
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var sb = new StringBuilder();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync();
            if (line == null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);

            if (doc.RootElement.TryGetProperty("message", out var msg))
            {
                string? content = msg.GetProperty("content").GetString();
                sb.Append(content);
                onPartialOutput?.Invoke(content ?? string.Empty);
            }

            if (doc.RootElement.TryGetProperty("done", out var doneFlag) &&
                doneFlag.GetBoolean())
            {
                break;
            }
        }

        var jsonFinal = sb.ToString();

        try
        {
            var resultado = JsonSerializer.Deserialize<BenchmarkAnalysisResult>(jsonFinal);
            stopwatch.Stop();

            if (resultado != null)
            {
                resultado.TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss");
                return resultado;
            }
        }
        catch
        {
            // fallback
        }

        return GetFallback(benchmarks);
    }
}