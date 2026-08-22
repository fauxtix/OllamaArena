using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Diagnostics;
using System.Text.Json;

namespace OllamaArena.Services.Implementations.Services
{
    public class TranslationService : ITranslationService
    {
        private readonly HttpClient _http;
        private readonly PromptFilesService promptFilesService;
        private readonly IBenchmarkRepository _benchmarks;
        private readonly ILogger<TranslationService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public TranslationService(
            HttpClient http,
            IBenchmarkRepository benchmarks,
            ILogger<TranslationService> logger,
            PromptFilesService promptFilesService,
            IOptions<OllamaOptions> options)
        {
            _http = http;
            _benchmarks = benchmarks;
            _logger = logger;

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri(options.Value.BaseUrl);
            this.promptFilesService = promptFilesService;
        }

        public Task<TranslationResult> TranslateAsync(string text, string model)
            => TranslateAutoAsync(text);

        public async Task<TranslationResult> TranslateAutoAsync(string text)
        {
            var model = await _benchmarks.GetBestModelAsync();

            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("Sem informação de modelos para testar.");

            try
            {
                return await CallModelAsync(text, model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Modelo falhou. Fallback.");
                throw new InvalidOperationException(
                    $"Falha ao traduzir com o modelo selecionado ({model}).",
                    ex);
            }
        }

        private async Task<TranslationResult> CallModelAsync(string text, string model)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var prompt = await promptFilesService.GetPromptFileContentAsync("translation-prompt.txt");
                prompt = prompt?.Replace("{{TEXT}}", text)
                               .Replace(TargetLanguageResolver.Token, TargetLanguageResolver.GetTargetLanguage());

                var body = new
                {
                    model,
                    messages = new[]
                    {
                    new { role = "user", content = prompt }
                },
                    stream = false
                };

                var response = await _http.PostAsJsonAsync("/api/chat", body);
                response.EnsureSuccessStatusCode();

                var raw = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Resposta bruta: {Raw}", raw);

                var envelope = JsonSerializer.Deserialize<OllamaChatResponse>(raw, JsonOptions);
                var content = envelope?.Message?.Content ?? string.Empty;

                _logger.LogDebug("Conteúdo do modelo: {Content}", content);

                var cleaned = CleanResponse(content);

                var json = TryExtractJson(cleaned);

                if (json != null)
                {
                    var parsed = TryParse(json);

                    sw.Stop();
                    parsed?.ModelUsed = model;
                    parsed?.TimeSpentMs = sw.ElapsedMilliseconds;
                    if (parsed != null && IsValidTranslation(parsed, text))
                        return parsed;
                }


                return new TranslationResult
                {
                    TranslatedText = RemoveJsonIfPresent(cleaned).Trim(),
                    Confidence = 0.2,
                    ModelUsed = model,
                    TimeSpentMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na tradução");
                throw;
            }
        }

        private static string RemoveJsonIfPresent(string text)
        {
            var trimmed = text.Trim();

            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                return string.Empty;

            return text;
        }


        private static string CleanResponse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text.Trim().Trim('`').Trim();
        }

        private static string? TryExtractJson(string text)
        {
            var start = text.IndexOf('{');
            if (start < 0) return null;

            var depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                if (text[i] == '}') depth--;

                if (depth == 0)
                    return text.Substring(start, i - start + 1);
            }

            return null;
        }


        private TranslationResult? TryParse(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<TranslationResult>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao parsear JSON.");
                return null;
            }
        }


        private static bool IsValidTranslation(TranslationResult result, string original)
        {
            if (string.IsNullOrWhiteSpace(result.TranslatedText))
                return false;

            var t = result.TranslatedText.Trim();

            string[] invalidStarts =
            {
                "Aptitude:",
                "The response",
                "Translation:",
                "This answer",
                "The model",
                "This response",
                "The output"
            };

            if (invalidStarts.Any(s => t.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
                return false;

            string[] analysisWords =
            {
                "grammatically",
                "structured",
                "coherent",
                "accuracy",
                "evaluation",
                "score",
                "flawed",
                "incomplete"
            };

            //if (analysisWords.Any(w => t.Contains(w, StringComparison.OrdinalIgnoreCase)))
            //    return false;

            if (t.Equals(original.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}
