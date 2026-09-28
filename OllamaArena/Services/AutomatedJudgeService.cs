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

    /// <summary>
    /// Eventos emitidos durante a avaliação automática, para a UI dar feedback
    /// de progresso. Os dois juízes correm em paralelo, por isso cada um
    /// reporta o seu próprio ciclo de vida (identificado por <see cref="JudgeProgressEvent.Juiz"/>).
    /// </summary>
    public enum JudgeProgressStep
    {
        /// <summary>Definições lidas (BD -> configuração -> default).</summary>
        DefinicoesResolvidas,

        /// <summary>Intervalo mínimo entre avaliações verificado.</summary>
        QuotaVerificada,

        /// <summary>Pedido ao juiz enviado; a resposta ainda está a gerar.</summary>
        JuizIniciado,

        /// <summary>Juiz respondeu e a resposta foi interpretada.</summary>
        JuizConcluido,

        /// <summary>Juiz indisponível, sem chave, erro de rede/HTTP ou cancelado.</summary>
        JuizFalhou,

        /// <summary>O juiz mudou de modelo e voltou a avaliar (fallback do OpenRouter).</summary>
        ModeloAlternativo,

        /// <summary>Todos os juízes terminaram.</summary>
        Concluido
    }

    public sealed class JudgeProgressEvent
    {
        public JudgeProgressStep Step { get; init; }
        public string? Juiz { get; init; }
        public string? Detalhe { get; init; }
    }

    public class AutomatedJudgeService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILogger<AutomatedJudgeService> _logger;

        // Nomes dos juízes usados nos eventos de progresso e nas mensagens de erro.
        public const string JuizGemini = "Gemini";
        public const string JuizOpenRouter = "OpenRouter";

        // URLs oficiais das APIs
        private const string GeminiModel = "gemini-flash-latest";
        private const string GeminiUrlTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
        private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

        private const string MensagemSemChave = "A chave de API não está configurada ou é inválida.";

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
        /// <param name="prompt">Prompt de avaliação já formatado.</param>
        /// <param name="reportarProgresso">
        /// Callback opcional para dar feedback de progresso à UI. Como as chamadas correm
        /// em paralelo, é invocado a partir de threads diferentes da que chamou o método:
        /// quem o consome tem de mudar para a thread do renderer antes de tocar em estado de UI.
        /// </param>
        /// <param name="cancellationToken">Cancela os pedidos HTTP em curso (botão "Cancelar" na UI).</param>
        public async Task<AutomatedJudgeResult> GetFeedbacksAutomatizadosAsync(
            string prompt,
            Action<JudgeProgressEvent>? reportarProgresso = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 0. Resolve as definições com prioridade BD (página Settings) → configuração → default.
            var definicoes = await ObterDefinicoesAsync();
            reportarProgresso?.Invoke(new JudgeProgressEvent
            {
                Step = JudgeProgressStep.DefinicoesResolvidas,
                Detalhe = definicoes.OpenRouterModelo
            });

            // 1. Valida as chaves de API ANTES de consumir qualquer quota.
            string geminiKey = LimparChave(definicoes.GeminiKey);
            string openRouterKey = LimparChave(definicoes.OpenRouterKey);

            bool geminiDisponivel = !string.IsNullOrWhiteSpace(geminiKey) && geminiKey.Length >= 10;
            bool openRouterDisponivel = !string.IsNullOrWhiteSpace(openRouterKey);

            if (!geminiDisponivel && !openRouterDisponivel)
            {
                const string aviso = "Nenhuma chave de API configurada. Configure-as na página Settings ou em user-secrets (ApiKeys:Gemini / ApiKeys:OpenRouter).";
                _logger.LogWarning(aviso);
                ReportarSemChave(JuizGemini, reportarProgresso);
                ReportarSemChave(JuizOpenRouter, reportarProgresso);
                reportarProgresso?.Invoke(new JudgeProgressEvent { Step = JudgeProgressStep.Concluido });

                return new AutomatedJudgeResult
                {
                    Gemini = Falha(JuizGemini, MensagemSemChave),
                    OpenRouter = Falha(JuizOpenRouter, MensagemSemChave)
                };
            }

            // 2. Controlo de quota apenas dos provedores que vão ser usados (ainda não consome).
            VerificarRateLimit(geminiDisponivel, openRouterDisponivel);
            reportarProgresso?.Invoke(new JudgeProgressEvent { Step = JudgeProgressStep.QuotaVerificada });

            // 3. Chamadas em paralelo (provedores independentes). Cada juiz reporta o seu
            //    próprio início/fim; um único token cancela os dois pedidos em curso.
            var geminiTask = geminiDisponivel
                ? ExecutarJuizAsync(JuizGemini, CallGeminiApiAsync(prompt, geminiKey, JudgeTemperature, cancellationToken), reportarProgresso)
                : FalhaSemChaveAsync(JuizGemini, reportarProgresso);
            var openRouterTask = openRouterDisponivel
                ? ExecutarJuizAsync(JuizOpenRouter, CallOpenRouterApiAsync(prompt, openRouterKey, definicoes.OpenRouterModelo, JudgeTemperature, cancellationToken, reportarProgresso), reportarProgresso)
                : FalhaSemChaveAsync(JuizOpenRouter, reportarProgresso);

            await Task.WhenAll(geminiTask, openRouterTask).ConfigureAwait(false);

            var resultado = new AutomatedJudgeResult
            {
                Gemini = await geminiTask,
                OpenRouter = await openRouterTask
            };

            // 4. A quota só é marcada como consumida após sucesso (falhas não bloqueiam retries).
            MarcarQuotaSeSucesso(resultado);
            reportarProgresso?.Invoke(new JudgeProgressEvent { Step = JudgeProgressStep.Concluido });

            return resultado;
        }

        /// <summary>
        /// Envolve a chamada de um juiz para reportar início e fim (ou falha) no progresso.
        /// Não altera o resultado nem as exceções: devolve exatamente o que o juiz deu.
        /// </summary>
        private static async Task<JudgeFeedbackResult> ExecutarJuizAsync(
            string juiz,
            Task<JudgeFeedbackResult> chamada,
            Action<JudgeProgressEvent>? reportarProgresso)
        {
            if (reportarProgresso is null)
                return await chamada.ConfigureAwait(false);

            reportarProgresso(new JudgeProgressEvent { Step = JudgeProgressStep.JuizIniciado, Juiz = juiz });

            try
            {
                var resultado = await chamada.ConfigureAwait(false);
                reportarProgresso(new JudgeProgressEvent
                {
                    Step = resultado.Success ? JudgeProgressStep.JuizConcluido : JudgeProgressStep.JuizFalhou,
                    Juiz = juiz,
                    Detalhe = resultado.Success ? null : resultado.ErrorMessage
                });
                return resultado;
            }
            catch (Exception ex)
            {
                // Inclui OperationCanceledException: a UI precisa de saber que o juiz parou,
                // e a exceção continua a subir para abortar a avaliação.
                reportarProgresso(new JudgeProgressEvent
                {
                    Step = JudgeProgressStep.JuizFalhou,
                    Juiz = juiz,
                    Detalhe = ex is OperationCanceledException ? "Cancelado." : ex.Message
                });
                throw;
            }
        }

        /// <summary>
        /// Juiz sem chave configurada: reporta logo a falha e devolve o resultado sem
        /// gastar rede, para a UI não ficar à espera de um juiz que nunca vai responder.
        /// </summary>
        private Task<JudgeFeedbackResult> FalhaSemChaveAsync(string juiz, Action<JudgeProgressEvent>? reportarProgresso)
        {
            ReportarSemChave(juiz, reportarProgresso);
            return Task.FromResult(Falha(juiz, MensagemSemChave));
        }

        private static void ReportarSemChave(string juiz, Action<JudgeProgressEvent>? reportarProgresso)
            => reportarProgresso?.Invoke(new JudgeProgressEvent
            {
                Step = JudgeProgressStep.JuizFalhou,
                Juiz = juiz,
                Detalhe = MensagemSemChave
            });

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

        private async Task<JudgeFeedbackResult> CallGeminiApiAsync(string prompt, string apiKey, double temperatura, CancellationToken cancellationToken)
        {
            string geminiUrl = string.Format(GeminiUrlTemplate, GeminiModel, Uri.EscapeDataString(apiKey));
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = temperatura }
            };

            return await ExecuteGeminiRequestAsync(geminiUrl, requestBody, cancellationToken);
        }

        private async Task<JudgeFeedbackResult> ExecuteGeminiRequestAsync(string url, object requestBody, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return Falha(JuizGemini, "URL da API inválida.");
            }

            try
            {
                using var client = _httpClientFactory.CreateClient("Gemini");

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                using var response = await client.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Falha(JuizGemini, $"Modelo '{GeminiModel}' não encontrado ou URL incorreta. Resposta: {responseContent}");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return Falha(JuizGemini, "Limite de requisições excedido.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Falha(JuizGemini, $"Erro HTTP ({response.StatusCode}): {responseContent}");
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

                return Falha(JuizGemini, "A resposta não contém o formato esperado.");
            }
            catch (OperationCanceledException)
            {
                // Cancelamento pedido pelo utilizador: tem de subir, não virar "falha".
                throw;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "[Gemini] Erro de rede.");
                return Falha(JuizGemini, $"Erro de rede: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "[Gemini] Erro ao processar resposta JSON.");
                return Falha(JuizGemini, $"Erro ao processar resposta JSON: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? ex.InnerException.Message : "Sem detalhe interno";
                _logger.LogError(ex, "[Gemini] Erro geral.");
                return Falha(JuizGemini, $"{ex.Message} | Detalhe: {inner}");
            }
        }

        private async Task<JudgeFeedbackResult> CallOpenRouterApiAsync(
            string prompt, string apiKey, string modelo, double temperatura,
            CancellationToken cancellationToken, Action<JudgeProgressEvent>? reportarProgresso)
        {
            return await ExecuteOpenRouterRequestAsync(apiKey, prompt, modelo, temperatura, cancellationToken, reportarProgresso);
        }

        private async Task<JudgeFeedbackResult> ExecuteOpenRouterRequestAsync(
            string apiKey, string prompt, string modelo, double temperatura,
            CancellationToken cancellationToken, Action<JudgeProgressEvent>? reportarProgresso)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("OpenRouter");

                var (status, conteudo) = await EnviarPedidoOpenRouterAsync(client, apiKey, modelo, prompt, temperatura, cancellationToken).ConfigureAwait(false);

                // Fallback automático: modelo ":free" fixo removido/indisponível -> tenta o router gratuito.
                if (status == System.Net.HttpStatusCode.NotFound)
                {
                    if (string.Equals(modelo, "openrouter/free", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("[OpenRouter] openrouter/free retornou 404.");
                        return Falha(JuizOpenRouter, "Nenhum modelo gratuito funcionou.");
                    }

                    _logger.LogWarning("[OpenRouter] Modelo '{Modelo}' devolveu 404; a tentar openrouter/free.", modelo);
                    reportarProgresso?.Invoke(new JudgeProgressEvent
                    {
                        Step = JudgeProgressStep.ModeloAlternativo,
                        Juiz = JuizOpenRouter,
                        Detalhe = "openrouter/free"
                    });
                    (status, conteudo) = await EnviarPedidoOpenRouterAsync(client, apiKey, "openrouter/free", prompt, temperatura, cancellationToken).ConfigureAwait(false);
                    if (status == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("[OpenRouter] openrouter/free também devolveu 404.");
                        return Falha(JuizOpenRouter, "Nenhum modelo gratuito funcionou.");
                    }
                }

                if (status == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogError("[OpenRouter] 429 Rate Limit Response: {Response}", conteudo);
                    return Falha(JuizOpenRouter, "Limite de requisições excedido.");
                }

                int codigo = (int)status;
                if (codigo < 200 || codigo >= 300)
                {
                    _logger.LogError("[OpenRouter] Error ({StatusCode}): {Response}", status, conteudo);
                    return Falha(JuizOpenRouter, $"Erro HTTP ({status}): {conteudo}");
                }

                _logger.LogInformation("[OpenRouter] Sucesso com modelo: {Modelo}", modelo);

                string? texto = ExtrairTextoOpenRouter(conteudo);
                if (texto is null)
                {
                    return Falha(JuizOpenRouter, "A resposta não contém o formato esperado.");
                }

                return Ok(texto);
            }
            catch (OperationCanceledException)
            {
                // Cancelamento pedido pelo utilizador: tem de subir, não virar "falha".
                throw;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "[OpenRouter] HttpRequestException.");
                return Falha(JuizOpenRouter, $"Erro de rede: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "[OpenRouter] JsonException.");
                return Falha(JuizOpenRouter, $"Erro ao processar resposta JSON: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? ex.InnerException.Message : "Sem detalhe interno";
                _logger.LogError(ex, "[OpenRouter] Exception.");
                return Falha("OpenRouter", $"{ex.Message} | Detalhe: {inner}");
            }
        }

        private async Task<(System.Net.HttpStatusCode Status, string Conteudo)> EnviarPedidoOpenRouterAsync(
            HttpClient client, string apiKey, string modelo, string prompt, double temperatura,
            CancellationToken cancellationToken)
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

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
