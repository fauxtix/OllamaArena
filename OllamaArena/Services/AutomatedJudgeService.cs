namespace OllamaArena.Services
{
    using OllamaArena.Services.Exceptions;
    using OllamaArena.Services.Helpers;
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Resultado estruturado da avaliação de um único juiz (Gemini ou OpenRouter).
    /// </summary>
    public class JudgeFeedbackResult
    {
        public bool Success { get; set; }
        public string RawText { get; set; } = string.Empty;
        public ParsedEvaluationResult Parsed { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resultado agregado das avaliações automáticas dos dois juízes.
    /// Cada juiz é tratado de forma independente: um pode falhar sem bloquear o outro.
    /// </summary>
    public class AutomatedJudgeResult
    {
        public JudgeFeedbackResult Gemini { get; set; } = new();
        public JudgeFeedbackResult OpenRouter { get; set; } = new();
        public bool HasErrors => !Gemini.Success || !OpenRouter.Success;
    }

    public class AutomatedJudgeService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AutomatedJudgeService> _logger;

        // URLs oficiais das APIs
        private const string GeminiModel = "gemini-flash-latest";
        private const string GeminiUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
        private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

        // Controlo de quota por provedor (as chamadas dos dois juízes são independentes).
        // Estático para sobreviver à recriação de instâncias do serviço (página recarregada).
        private static DateTime? _ultimoGemini;
        private static DateTime? _ultimoOpenRouter;
        private static readonly object RateLock = new();

        private int MinIntervalSeconds
        {
            get
            {
                int config = _configuration.GetValue<int>("AutomatedJudge:MinIntervalSeconds", 60);
                return config > 0 ? config : 60;
            }
        }

        public AutomatedJudgeService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<AutomatedJudgeService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Executa as avaliações automáticas dos dois juízes em paralelo, usando as chaves
        /// configuradas em user-secrets (secção ApiKeys).
        /// </summary>
        public async Task<AutomatedJudgeResult> GetFeedbacksAutomatizadosAsync(string prompt)
        {
            // 1. Valida as chaves de API ANTES de consumir qualquer quota.
            string geminiKey = LimparChave(_configuration["ApiKeys:Gemini"]);
            string openRouterKey = LimparChave(_configuration["ApiKeys:OpenRouter"]);

            bool geminiDisponivel = !string.IsNullOrWhiteSpace(geminiKey) && geminiKey.Length >= 10;
            bool openRouterDisponivel = !string.IsNullOrWhiteSpace(openRouterKey);

            if (!geminiDisponivel && !openRouterDisponivel)
            {
                const string aviso = "Nenhuma chave de API configurada. Configure-as em user-secrets (ApiKeys:Gemini / ApiKeys:OpenRouter).";
                _logger.LogWarning(aviso);
                return new AutomatedJudgeResult
                {
                    Gemini = Falha("Gemini", "A chave de API não está configurada ou é inválida."),
                    OpenRouter = Falha("OpenRouter", "A chave de API não está configurada ou é inválida.")
                };
            }

            // 2. Controlo de quota apenas dos provedores que vão ser usados (ainda não consome).
            VerificarRateLimit(geminiDisponivel, openRouterDisponivel);

            // 3. Chamadas em paralelo (provedores independentes).
            var geminiTask = geminiDisponivel
                ? CallGeminiApiAsync(prompt, geminiKey)
                : Task.FromResult(Falha("Gemini", "A chave de API não está configurada ou é inválida."));
            var openRouterTask = openRouterDisponivel
                ? CallOpenRouterApiAsync(prompt, openRouterKey)
                : Task.FromResult(Falha("OpenRouter", "A chave de API não está configurada ou é inválida."));

            await Task.WhenAll(geminiTask, openRouterTask).ConfigureAwait(false);

            var resultado = new AutomatedJudgeResult
            {
                Gemini = await geminiTask,
                OpenRouter = await openRouterTask
            };

            // 4. A quota só é marcada como consumida após sucesso (falhas não bloqueiam retries).
            MarcarQuotaSeSucesso(resultado);

            return resultado;
        }

        private void VerificarRateLimit(bool usarGemini, bool usarOpenRouter)
        {
            lock (RateLock)
            {
                int faltamGemini = usarGemini ? SegundosEmFalta(_ultimoGemini) : 0;
                int faltamOpenRouter = usarOpenRouter ? SegundosEmFalta(_ultimoOpenRouter) : 0;
                int maxFalta = Math.Max(faltamGemini, faltamOpenRouter);

                if (maxFalta > 0)
                {
                    throw new QuotaLimitException(maxFalta);
                }
            }
        }

        private static void MarcarQuotaSeSucesso(AutomatedJudgeResult resultado)
        {
            lock (RateLock)
            {
                if (resultado.Gemini.Success)
                    _ultimoGemini = DateTime.Now;

                if (resultado.OpenRouter.Success)
                    _ultimoOpenRouter = DateTime.Now;
            }
        }

        private int SegundosEmFalta(DateTime? ultimoPedido)
        {
            if (!ultimoPedido.HasValue)
                return 0;

            var tempoPassado = DateTime.Now - ultimoPedido.Value;
            int emFalta = MinIntervalSeconds - (int)tempoPassado.TotalSeconds;
            return Math.Max(0, emFalta);
        }

        private async Task<JudgeFeedbackResult> CallGeminiApiAsync(string prompt, string apiKey)
        {
            string geminiUrl = string.Format(GeminiUrlTemplate, GeminiModel, Uri.EscapeDataString(apiKey));
            var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

            return await ExecuteGeminiRequestAsync(geminiUrl, requestBody);
        }

        private async Task<JudgeFeedbackResult> ExecuteGeminiRequestAsync(string url, object requestBody)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return Falha("Gemini", "URL da API inválida.");
            }

            try
            {
                using var client = _httpClientFactory.CreateClient("Gemini");

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                using var response = await client.PostAsync(uri, content).ConfigureAwait(false);
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Falha("Gemini", $"Modelo '{GeminiModel}' não encontrado ou URL incorreta. Resposta: {responseContent}");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return Falha("Gemini", "Limite de requisições excedido.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Falha("Gemini", $"Erro HTTP ({response.StatusCode}): {responseContent}");
                }

                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textProp))
                {
                    string texto = textProp.GetString() ?? string.Empty;
                    return Ok(texto);
                }

                return Falha("Gemini", "A resposta não contém o formato esperado.");
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "[Gemini] Erro de rede.");
                return Falha("Gemini", $"Erro de rede: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "[Gemini] Erro ao processar resposta JSON.");
                return Falha("Gemini", $"Erro ao processar resposta JSON: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? ex.InnerException.Message : "Sem detalhe interno";
                _logger.LogError(ex, "[Gemini] Erro geral.");
                return Falha("Gemini", $"{ex.Message} | Detalhe: {inner}");
            }
        }

        private async Task<JudgeFeedbackResult> CallOpenRouterApiAsync(string prompt, string apiKey)
        {
            var requestBody = new
            {
                // Router automático gratuito: evita usar um ID de modelo fixo que possa ser desativado.
                model = "openrouter/free",
                messages = new[] { new { role = "user", content = prompt } }
            };

            return await ExecuteOpenRouterRequestAsync(apiKey, requestBody);
        }

        private async Task<JudgeFeedbackResult> ExecuteOpenRouterRequestAsync(string apiKey, object requestBody)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("OpenRouter");

                using var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                // Cabeçalhos obrigatórios exigidos pela plataforma do OpenRouter
                request.Headers.Add("HTTP-Referer", "http://localhost");
                request.Headers.Add("X-Title", "OllamaArena");

                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                using var response = await client.SendAsync(request).ConfigureAwait(false);
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("[OpenRouter] openrouter/free retornou 404.");
                    return Falha("OpenRouter", "Nenhum modelo gratuito funcionou.");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogError("[OpenRouter] 429 Rate Limit Response: {Response}", responseContent);
                    return Falha("OpenRouter", "Limite de requisições excedido.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[OpenRouter] Error ({StatusCode}): {Response}", response.StatusCode, responseContent);
                    return Falha("OpenRouter", $"Erro HTTP ({response.StatusCode}): {responseContent}");
                }

                _logger.LogInformation("[OpenRouter] Sucesso com modelo: openrouter/free");

                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var textProp))
                {
                    string texto = textProp.GetString() ?? string.Empty;
                    return Ok(texto);
                }

                return Falha("OpenRouter", "A resposta não contém o formato esperado.");
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "[OpenRouter] HttpRequestException.");
                return Falha("OpenRouter", $"Erro de rede: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "[OpenRouter] JsonException.");
                return Falha("OpenRouter", $"Erro ao processar resposta JSON: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? ex.InnerException.Message : "Sem detalhe interno";
                _logger.LogError(ex, "[OpenRouter] Exception.");
                return Falha("OpenRouter", $"{ex.Message} | Detalhe: {inner}");
            }
        }

        private static JudgeFeedbackResult Ok(string texto)
        {
            var parsed = EvaluationParser.ParseEvaluation(texto);
            return new JudgeFeedbackResult
            {
                Success = !string.IsNullOrWhiteSpace(texto),
                RawText = texto,
                Parsed = parsed
            };
        }

        private static JudgeFeedbackResult Falha(string juiz, string mensagem)
        {
            return new JudgeFeedbackResult
            {
                Success = false,
                ErrorMessage = $"Erro {juiz}: {mensagem}"
            };
        }

        private static string LimparChave(string? chave) =>
            chave?.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "") ?? string.Empty;
    }
}
