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
    private const string ModelName = "phi4-mini:3.8b";

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

        var modeloMaisRapido = benchmarks
            .OrderByDescending(b => b.TokensPorSegundo)
            .FirstOrDefault();

        var maxTokens = modeloMaisRapido?.TokensPorSegundo ?? 0;

        var modeloMelhorAvaliado = benchmarks
            .Select(b => new
            {
                Model = b.NomeModelo,
                AvgRating = (b.GeminiRating + b.ChatGptRating +
                            b.GeminiFormattingRating + b.ChatGptFormattingRating) / 4.0
            })
            .OrderByDescending(x => x.AvgRating)
            .FirstOrDefault()?.Model ?? "N/A";

        analysisSystemPrompt = await _promptFilesService.GetPromptFileContentAsync("analysis-prompt.txt") ?? string.Empty;

        var markdownTable = GenerateMarkdownTable(benchmarks);

        var payload = new
        {
            model = ModelName,
            messages = new[]
            {
        new { role = "system", content = analysisSystemPrompt },
        new { role = "user", content = $"Below is the table showing the benchmark results across prompts:\n\n{markdownTable}" }
    },
            stream = false,
            options = new
            {
                temperature = 0.0,
                seed = 42,
                top_k = 1,
                top_p = 0.1
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
            string generatedContent = doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var resultado = JsonSerializer.Deserialize<BenchmarkAnalysisResult>(generatedContent);

            if (resultado != null)
            {
                resultado.ModeloMaisRapido = modeloMaisRapido?.NomeModelo ?? "N/A";
                resultado.MaxTokensSec = Math.Round(maxTokens, 2); 
                resultado.ModeloMelhorAvaliado = modeloMelhorAvaliado;

                stopwatch.Stop();
                resultado.TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss");

                return resultado;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Análise cancelada pelo utilizador.");
            throw new OperationCanceledException();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na geração da análise textual");
        }

        stopwatch.Stop();
        return new BenchmarkAnalysisResult
        {
            Sumario = "Análise concluída com sucesso.",
            AnaliseDetalhada = "Não foi possível gerar a análise textual detalhada devido a um erro técnico.",
            ModeloMaisRapido = modeloMaisRapido?.NomeModelo ?? "N/A",
            MaxTokensSec = Math.Round(maxTokens, 2),
            ModeloMelhorAvaliado = modeloMelhorAvaliado,
            TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss")
        };
    }

    private string GenerateMarkdownTable(List<BenchmarkEvaluationModel> lista)
    {
        var sb = new StringBuilder();
        // A inclusão do 'Test ID' permite ao LLM distinguir as execuções do mesmo modelo
        sb.AppendLine("| Test ID | Model | Gemini Rating | ChatGPT Rating | Gemini Format Rating | ChatGPT Format Rating | Tokens/s | Time (ms) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");

        foreach (var item in lista)
        {
            sb.AppendLine($"| {item.Id} | {item.NomeModelo} | {item.GeminiRating} | {item.ChatGptRating} | {item.GeminiFormattingRating} | {item.ChatGptFormattingRating} | {item.TokensPorSegundo:F1} | {item.TempoPuroMs} |");
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


}