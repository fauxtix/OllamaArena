using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OllamaArena.Services.Implementations.Services
{
    /// <summary>
    /// Tradução de feedbacks com uma cadeia de sistemas: OpenRouter → Gemini → Ollama (local).
    /// O primeiro que responder com uma tradução utilizável é o usado; os restantes só
    /// entram em fallback e a UI é informada de cada passagem através dos eventos de progresso.
    /// </summary>
    public partial class TranslationService : ITranslationService
    {
        private const string GeminiUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
        private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

        // Temperatura 0: a tradução fica determinística e o JSON pedido pelo prompt
        // (traduzido + confiança) sai mais fiável.
        private const double TranslationTemperature = 0;

        // Confiança atribuída quando o sistema devolve texto válido mas fora do JSON pedido.
        private const double ConfiancaSemJson = 0.2;

        // Chaves aceites para a tradução, da mais específica para a mais genérica.
        private static readonly string[] ChavesTraducao =
            ["translatedText", "translated_text", "translation", "text"];

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpOllama;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApiCredentialsResolver _credentials;
        private readonly IBenchmarkRepository _benchmarks;
        private readonly ILogger<TranslationService> _logger;
        private readonly PromptFilesService _promptFiles;

        public TranslationService(
            HttpClient http,
            IHttpClientFactory httpClientFactory,
            ApiCredentialsResolver credentials,
            IBenchmarkRepository benchmarks,
            ILogger<TranslationService> logger,
            PromptFilesService promptFilesService,
            IOptions<OllamaOptions> options)
        {
            _httpOllama = http;
            _httpClientFactory = httpClientFactory;
            _credentials = credentials;
            _benchmarks = benchmarks;
            _logger = logger;
            _promptFiles = promptFilesService;

            if (_httpOllama.BaseAddress == null)
                _httpOllama.BaseAddress = new Uri(options.Value.BaseUrl);
        }

        public async Task<TranslationResult> TranslateAsync(
            string text,
            Action<TranslationProgressEvent>? reportarProgresso = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var definicoes = await _credentials.ResolverAsync().ConfigureAwait(false);
            reportarProgresso?.Invoke(new TranslationProgressEvent
            {
                Step = TranslationProgressStep.DefinicoesResolvidas,
                Modelo = definicoes.OpenRouterModelo
            });

            var prompt = await BuildPromptAsync(text, cancellationToken).ConfigureAwait(false);
            var falhas = new List<string>();

            // 1. OpenRouter (preferência; modelo definido na página Settings).
            if (string.IsNullOrWhiteSpace(definicoes.OpenRouterChave))
            {
                SemChave(TranslationProviders.OpenRouter, reportarProgresso, falhas);
            }
            else
            {
                var resultado = await TentarAsync(
                    TranslationProviders.OpenRouter,
                    definicoes.OpenRouterModelo,
                    text,
                    () => ChamarOpenRouterAsync(prompt, definicoes.OpenRouterChave, definicoes.OpenRouterModelo, cancellationToken),
                    reportarProgresso, falhas, cancellationToken).ConfigureAwait(false);

                if (resultado is not null)
                    return Concluir(resultado, reportarProgresso);
            }

            // 2. Gemini (fallback).
            if (!ApiCredentialsResolver.TemChaveValida(definicoes.GeminiChave))
            {
                SemChave(TranslationProviders.Gemini, reportarProgresso, falhas);
            }
            else
            {
                var resultado = await TentarAsync(
                    TranslationProviders.Gemini,
                    definicoes.GeminiModelo,
                    text,
                    () => ChamarGeminiAsync(prompt, definicoes.GeminiChave, definicoes.GeminiModelo, cancellationToken),
                    reportarProgresso, falhas, cancellationToken).ConfigureAwait(false);

                if (resultado is not null)
                    return Concluir(resultado, reportarProgresso);
            }

            // 3. Ollama local (último recurso): precisa de um modelo avaliado na BD.
            var modeloLocal = await _benchmarks.GetBestModelAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(modeloLocal))
            {
                const string aviso = "Ollama: sem informação de modelos para testar.";
                falhas.Add(aviso);
                reportarProgresso?.Invoke(new TranslationProgressEvent
                {
                    Step = TranslationProgressStep.ProvedorFalhou,
                    Provider = TranslationProviders.Ollama,
                    Detalhe = aviso
                });
            }
            else
            {
                var resultado = await TentarAsync(
                    TranslationProviders.Ollama,
                    modeloLocal,
                    text,
                    () => ChamarOllamaAsync(prompt, modeloLocal, cancellationToken),
                    reportarProgresso, falhas, cancellationToken).ConfigureAwait(false);

                if (resultado is not null)
                    return Concluir(resultado, reportarProgresso);
            }

            reportarProgresso?.Invoke(new TranslationProgressEvent { Step = TranslationProgressStep.Concluido });

            throw new InvalidOperationException(
                "Não foi possível traduzir o feedback com nenhum sistema. " + string.Join(" ", falhas));
        }

        private static TranslationResult Concluir(
            TranslationResult resultado,
            Action<TranslationProgressEvent>? reportarProgresso)
        {
            reportarProgresso?.Invoke(new TranslationProgressEvent
            {
                Step = TranslationProgressStep.ProvedorConcluido,
                Provider = resultado.Provider,
                Modelo = resultado.ModelUsed
            });
            reportarProgresso?.Invoke(new TranslationProgressEvent { Step = TranslationProgressStep.Concluido });
            return resultado;
        }

        private static void SemChave(
            string provider,
            Action<TranslationProgressEvent>? reportarProgresso,
            List<string> falhas)
        {
            const string aviso = "Chave de API não configurada.";
            falhas.Add($"{provider}: {aviso}");
            reportarProgresso?.Invoke(new TranslationProgressEvent
            {
                Step = TranslationProgressStep.ProvedorFalhou,
                Provider = provider,
                Detalhe = aviso
            });
        }

        /// <summary>
        /// Executa um sistema da cadeia. Devolve null (para o siguiente ser tentado)
        /// quando o sistema falha ou devolve algo que não é uma tradução; relança
        /// o cancelamento para a UI o distinguir de uma falha.
        /// </summary>
        private async Task<TranslationResult?> TentarAsync(
            string provider,
            string modelo,
            string textoOriginal,
            Func<Task<TranslationResult?>> chamada,
            Action<TranslationProgressEvent>? reportarProgresso,
            List<string> falhas,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            reportarProgresso?.Invoke(new TranslationProgressEvent
            {
                Step = TranslationProgressStep.ProvedorIniciado,
                Provider = provider,
                Modelo = modelo
            });

            var sw = Stopwatch.StartNew();

            try
            {
                var resultado = await chamada().ConfigureAwait(false);
                sw.Stop();

                if (resultado is null)
                    throw new InvalidOperationException("Resposta sem tradução válida.");

                if (EhCopiaDoOriginal(resultado.TranslatedText, textoOriginal))
                    throw new InvalidOperationException("Devolveu o texto original; não traduziu.");

                resultado.TimeSpentMs = sw.ElapsedMilliseconds;
                return resultado;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                reportarProgresso?.Invoke(new TranslationProgressEvent
                {
                    Step = TranslationProgressStep.ProvedorFalhou,
                    Provider = provider,
                    Modelo = modelo,
                    Detalhe = "Cancelado."
                });
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Provider}] Tradução falhou; a tentar o sistema seguinte.", provider);
                falhas.Add($"{provider}: {ex.Message}");

                reportarProgresso?.Invoke(new TranslationProgressEvent
                {
                    Step = TranslationProgressStep.ProvedorFalhou,
                    Provider = provider,
                    Modelo = modelo,
                    Detalhe = ex.Message
                });

                return null;
            }
        }

        private async Task<string> BuildPromptAsync(string text, CancellationToken cancellationToken)
        {
            var prompt = await _promptFiles
                .GetPromptFileContentAsync("translation-prompt.txt", cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return (prompt ?? string.Empty)
                .Replace("{{TEXT}}", text)
                .Replace(TargetLanguageResolver.Token, TargetLanguageResolver.GetTargetLanguage());
        }

        private async Task<TranslationResult?> ChamarOpenRouterAsync(
            string prompt, string apiKey, string modelo, CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient("OpenRouter");

            var requestBody = new
            {
                model = modelo,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = TranslationTemperature
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Cabeçalhos obrigatórios exigidos pela plataforma do OpenRouter
            request.Headers.Add("HTTP-Referer", "http://localhost");
            request.Headers.Add("X-Title", "OllamaArena");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("[OpenRouter] Resposta bruta: {Raw}", raw);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Erro HTTP ({(int)response.StatusCode}): {raw}");

            return Interpretar(ExtrairTextoOpenRouter(raw), TranslationProviders.OpenRouter, modelo);
        }

        private async Task<TranslationResult?> ChamarGeminiAsync(
            string prompt, string apiKey, string modelo, CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient("Gemini");

            string url = string.Format(GeminiUrlTemplate, modelo, Uri.EscapeDataString(apiKey));
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = TranslationTemperature }
            };

            using var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("[Gemini] Resposta bruta: {Raw}", raw);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Erro HTTP ({(int)response.StatusCode}): {raw}");

            return Interpretar(ExtrairTextoGemini(raw), TranslationProviders.Gemini, modelo);
        }

        private async Task<TranslationResult?> ChamarOllamaAsync(
            string prompt, string modelo, CancellationToken cancellationToken)
        {
            var body = new
            {
                model = modelo,
                messages = new[] { new { role = "user", content = prompt } },
                stream = false
            };

            using var response = await _httpOllama.PostAsJsonAsync("/api/chat", body, cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("[Ollama] Resposta bruta: {Raw}", raw);

            response.EnsureSuccessStatusCode();

            var envelope = JsonSerializer.Deserialize<OllamaChatResponse>(raw, JsonOptions);
            return Interpretar(envelope?.Message?.Content ?? string.Empty, TranslationProviders.Ollama, modelo);
        }

        private static string? ExtrairTextoOpenRouter(string responseContent)
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var textProp))
            {
                return textProp.GetString() ?? string.Empty;
            }

            throw new InvalidOperationException("A resposta não contém o formato esperado.");
        }

        private static string? ExtrairTextoGemini(string responseContent)
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var contentObj) &&
                contentObj.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textProp))
            {
                return textProp.GetString() ?? string.Empty;
            }

            throw new InvalidOperationException("A resposta não contém o formato esperado.");
        }

        /// <summary>
        /// Interpreta a resposta de um sistema. Devolve null apenas quando não há mesmo
        /// texto traduzido (vazio, ou a análise do prompt em vez da tradução), para que a
        /// cadeia passe ao sistema seguinte. Respostas que não cumprem o JSON pedido são
        /// recuperadas na mesma: um sistema que respondeu não deve ser descartado por um
        /// formato imperfeito.
        /// </summary>
        private TranslationResult? Interpretar(string? conteudo, string provider, string modelo)
        {
            if (string.IsNullOrWhiteSpace(conteudo))
                return null;

            var cleaned = CleanResponse(conteudo);
            var json = TryExtractJson(cleaned);

            if (json is not null)
            {
                var texto = LerTextoTraduzido(json);
                if (!string.IsNullOrWhiteSpace(texto) && !ComecaPorFraseProibida(texto))
                {
                    return new TranslationResult
                    {
                        TranslatedText = texto.Trim(),
                        Confidence = LerConfianca(json),
                        Provider = provider,
                        ModelUsed = modelo
                    };
                }
            }

            // Sem JSON utilizável: aceita-se o texto simples, desde que não comece por
            // uma frase de análise (que indica que o sistema resumiu o prompt em vez de
            // traduzir). Um JSON que não se deixou ler já foi tratado acima, por isso
            // aqui nunca se descarta uma tradução por "ter forma de JSON".
            var textoSimples = RemoveJsonIfPresent(cleaned).Trim();
            if (string.IsNullOrWhiteSpace(textoSimples) || ComecaPorFraseProibida(textoSimples))
            {
                _logger.LogWarning(
                    "[{Provider}] Resposta sem tradução utilizável ({Modelo}). Início: {Conteudo}",
                    provider, modelo, Resumir(conteudo!));
                return null;
            }

            return new TranslationResult
            {
                TranslatedText = textoSimples,
                Confidence = ConfiancaSemJson,
                Provider = provider,
                ModelUsed = modelo
            };
        }

        /// <summary>
        /// Lê a tradução de um objeto JSON devolvido pelo modelo, tolerando pequenas
        /// diferenças: chaves alternativas, aspas duplicadas (o prompt antigo pedia
        /// { ""translatedText"": ""..."" }, que não é JSON válido) e cerca de código.
        /// </summary>
        private static string? LerTextoTraduzido(string json)
        {
            foreach (var variante in JsonVariantes(json))
            {
                try
                {
                    using var doc = JsonDocument.Parse(variante);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                        continue;

                    foreach (var chave in ChavesTraducao)
                    {
                        if (!doc.RootElement.TryGetProperty(chave, out var prop))
                            continue;

                        if (prop.ValueKind == JsonValueKind.String)
                        {
                            var texto = prop.GetString();
                            if (!string.IsNullOrWhiteSpace(texto))
                                return texto;
                        }
                    }
                }
                catch (JsonException)
                {
                    // Variante seguinte.
                }
            }

            return null;
        }

        /// <summary>Confiança declarada pelo modelo, quando válida; caso contrário a confiança baixa.</summary>
        private static double LerConfianca(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(RepararAspasDuplas(json));
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("confidence", out var prop) &&
                    prop.ValueKind == JsonValueKind.Number &&
                    prop.TryGetDouble(out var valor) &&
                    valor >= 0 && valor <= 1)
                {
                    return valor;
                }
            }
            catch (JsonException)
            {
                // Confiança indisponível.
            }

            return ConfiancaSemJson;
        }

        /// <summary>Variantes tentadas por ordem: o texto tal e como veio e as aspas duplicadas reparadas.</summary>
        private static IEnumerable<string> JsonVariantes(string json)
        {
            yield return json;

            var reparado = RepararAspasDuplas(json);
            if (!string.Equals(reparado, json, StringComparison.Ordinal))
                yield return reparado;
        }

        /// <summary>
        /// Converte as aspas duplicadas numa resposta em JSON válido: vários modelos
        /// copiam literalmente o exemplo do prompt quando este traz ""assim"".
        /// </summary>
        private static string RepararAspasDuplas(string text)
            => text.Contains("\"\"", StringComparison.Ordinal)
                ? text.Replace("\"\"", "\"")
                : text;

        /// <summary>Corta o conteúdo para o log não despejar a resposta inteira na base de dados.</summary>
        private static string Resumir(string conteudo)
            => conteudo.Length <= 300 ? conteudo : conteudo[..300] + "…";

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

            // Remove a cerca de código markdown (```json ... ```) antes de trimar:
            // sem isto, o "json" inicial impedia que as backticks fossem removidas.
            return RegexCercaCodigo().Replace(text, string.Empty).Trim().Trim('`').Trim();
        }

        [GeneratedRegex("```[a-zA-Z]*\\s*|\\s*```", RegexOptions.Singleline)]
        private static partial Regex RegexCercaCodigo();

        [GeneratedRegex(@"\s+")]
        private static partial Regex RegexEspacos();

        [GeneratedRegex(@"[^\p{L}\p{N}]+")]
        private static partial Regex RegexNaoAlfanumericos();

        /// <summary>
        /// Indica que o sistema devolveu o texto original em vez de o traduzir — resposta
        /// preguiçosa ou recusa silenciosa, que não é aceitável como tradução. A comparação
        /// ignora caixa e toda a pontuação/espaçamento, porque as mesmas palavras noutra
        /// grafia não são uma tradução: "Olá, mundo." devolvido para um original "olá  mundo"
        /// conta como cópia.
        /// </summary>
        private static bool EhCopiaDoOriginal(string traduzido, string original)
        {
            if (string.IsNullOrWhiteSpace(original))
                return false;

            return string.Equals(Normalizar(traduzido), Normalizar(original), StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalizar(string texto)
            => RegexEspacos().Replace(RegexNaoAlfanumericos().Replace(texto, " "), " ").Trim();

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

        /// <summary>
        /// Frases que indicam que o sistema respondeu com a análise do prompt em vez da
        /// tradução (o texto "bruto" é sempre recusado).
        /// </summary>
        private static bool ComecaPorFraseProibida(string texto)
        {
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

            return invalidStarts.Any(s => texto.StartsWith(s, StringComparison.OrdinalIgnoreCase));
        }
    }
}
