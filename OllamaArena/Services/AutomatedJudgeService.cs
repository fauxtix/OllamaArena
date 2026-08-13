namespace OllamaArena.Services
{
    using OllamaArena.Services.Exceptions;
    using OllamaArena.Services.Helpers;
    using OllamaArena.Services.Interfaces.Repositories;
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
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILogger<AutomatedJudgeService> _logger;

        // URLs oficiais das APIs
        private const string GeminiModel = "gemini-flash-latest";
        private const string GeminiUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
        private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

        // Temperatura determinística dos juízes (0 = mesmo resultado para o mesmo input/modelo);
        // fixa e não configurável — o juiz deve ser determinístico para benchmarks comparáveis.
        private const double JudgeTemperature = 0;

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

        public AutomatedJudgeService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ISettingsRepository settingsRepository,
            ILogger<AutomatedJudgeService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _settingsRepository = settingsRepository;
            _logger = logger;
        }

        /// <summary>
        /// Resolve as definições dos juízes com prioridade: BD (página Settings) → configuração (user-secrets/appsettings) → default.
        /// </summary>
        private async Task<DefinicoesJuiz> ObterDefinicoesAsync()
        {
            var daBd = await _settingsRepository.GetValuesAsync(
            [
                "ApiKeys:Gemini",
                "ApiKeys:OpenRouter",
                "AutomatedJudge:OpenRouterModel"
            ]);

            string geminiKey = daBd.GetValueOrDefault("ApiKeys:Gemini")
                ?? _configuration["ApiKeys:Gemini"]
                ?? string.Empty;

            string openRouterKey = daBd.GetValueOrDefault("ApiKeys:OpenRouter")
                ?? _configuration["ApiKeys:OpenRouter"]
                ?? string.Empty;

            // Modelo do juiz OpenRouter: "openrouter/free" (router gratuito, default)
            // ou um modelo ":free" fixo por user (configurado na página Settings).
            string openRouterModelo = daBd.GetValueOrDefault("AutomatedJudge:OpenRouterModel")
                ?? _configuration["AutomatedJudge:OpenRouterModel"]
                ?? "openrouter/free";

            return new DefinicoesJuiz(geminiKey, openRouterKey, openRouterModelo);
        }

        private sealed record DefinicoesJuiz(
            string GeminiKey,
            string OpenRouterKey,
            string OpenRouterModelo);

        /// <summary>
        /// Executa as avaliações automáticas dos dois juízes em paralelo, usando as chaves
        /// configuradas na página Settings (BD) ou, em fallback, em user-secrets (secção ApiKeys).
        /// </summary>
        public async Task<AutomatedJudgeResult> GetFeedbacksAutomatizadosAsync(string prompt)
        {
            // 0. Resolve as definições com prioridade BD (página Settings) → configuração → default.
            var definicoes = await ObterDefinicoesAsync();

            // 1. Valida as chaves de API ANTES de consumir qualquer quota.
            string geminiKey = LimparChave(definicoes.GeminiKey);
            string openRouterKey = LimparChave(definicoes.OpenRouterKey);

            bool geminiDisponivel = !string.IsNullOrWhiteSpace(geminiKey) && geminiKey.Length >= 10;
            bool openRouterDisponivel = !string.IsNullOrWhiteSpace(openRouterKey);

            if (!geminiDisponivel && !openRouterDisponivel)
            {
                const string aviso = "Nenhuma chave de API configurada. Configure-as na página Settings ou em user-secrets (ApiKeys:Gemini / ApiKeys:OpenRouter).";
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
                ? CallGeminiApiAsync(prompt, geminiKey, JudgeTemperature)
                : Task.FromResult(Falha("Gemini", "A chave de API não está configurada ou é inválida."));
            var openRouterTask = openRouterDisponivel
                ? CallOpenRouterApiAsync(prompt, openRouterKey, definicoes.OpenRouterModelo, JudgeTemperature)
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

        private async Task<JudgeFeedbackResult> CallGeminiApiAsync(string prompt, string apiKey, double temperatura)
        {
            string geminiUrl = string.Format(GeminiUrlTemplate, GeminiModel, Uri.EscapeDataString(apiKey));
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = temperatura }
            };

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

        private async Task<JudgeFeedbackResult> CallOpenRouterApiAsync(string prompt, string apiKey, string modelo, double temperatura)
        {
            return await ExecuteOpenRouterRequestAsync(apiKey, prompt, modelo, temperatura);
        }

        private async Task<JudgeFeedbackResult> ExecuteOpenRouterRequestAsync(string apiKey, string prompt, string modelo, double temperatura)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("OpenRouter");

                var (status, conteudo) = await EnviarPedidoOpenRouterAsync(client, apiKey, modelo, prompt, temperatura).ConfigureAwait(false);

                // Fallback automático: modelo ":free" fixo removido/indisponível -> tenta o router gratuito.
                if (status == System.Net.HttpStatusCode.NotFound)
                {
                    if (string.Equals(modelo, "openrouter/free", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("[OpenRouter] openrouter/free retornou 404.");
                        return Falha("OpenRouter", "Nenhum modelo gratuito funcionou.");
                    }

                    _logger.LogWarning("[OpenRouter] Modelo '{Modelo}' devolveu 404; a tentar openrouter/free.", modelo);
                    (status, conteudo) = await EnviarPedidoOpenRouterAsync(client, apiKey, "openrouter/free", prompt, temperatura).ConfigureAwait(false);
                    if (status == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("[OpenRouter] openrouter/free também devolveu 404.");
                        return Falha("OpenRouter", "Nenhum modelo gratuito funcionou.");
                    }
                }

                if (status == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogError("[OpenRouter] 429 Rate Limit Response: {Response}", conteudo);
                    return Falha("OpenRouter", "Limite de requisições excedido.");
                }

                int codigo = (int)status;
                if (codigo < 200 || codigo >= 300)
                {
                    _logger.LogError("[OpenRouter] Error ({StatusCode}): {Response}", status, conteudo);
                    return Falha("OpenRouter", $"Erro HTTP ({status}): {conteudo}");
                }

                _logger.LogInformation("[OpenRouter] Sucesso com modelo: {Modelo}", modelo);

                string? texto = ExtrairTextoOpenRouter(conteudo);
                if (texto is null)
                {
                    return Falha("OpenRouter", "A resposta não contém o formato esperado.");
                }

                return Ok(texto);
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

        private async Task<(System.Net.HttpStatusCode Status, string Conteudo)> EnviarPedidoOpenRouterAsync(
            HttpClient client, string apiKey, string modelo, string prompt, double temperatura)
        {
            var requestBody = new
            {
                // Modelo configurável: "openrouter/free" (default) ou um modelo ":free" fixo por user.
                model = modelo,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = temperatura
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            // Cabeçalhos obrigatórios exigidos pela plataforma do OpenRouter
            request.Headers.Add("HTTP-Referer", "http://localhost");
            request.Headers.Add("X-Title", "OllamaArena");

            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request).ConfigureAwait(false);
            string conteudo = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return (response.StatusCode, conteudo);
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

            return null;
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
