using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Services.Interfaces.Repositories;

namespace OllamaArena.Services;

public sealed class ChatComposerService
{
    private readonly OllamaOptions _options;
    private readonly ISettingsRepository _settingsRepository;

    /// <summary>Marcador que o LLM usa na primeira linha para indicar o resumo.</summary>
    public const string SummaryMarker = "SUMMARY:";

    /// <summary>Separador entre o resumo e o corpo da resposta.</summary>
    public const string SummarySeparator = "---";

    public ChatComposerService(IOptions<OllamaOptions> options, ISettingsRepository settingsRepository)
    {
        _options = options.Value;
        _settingsRepository = settingsRepository;
    }

    public async Task<ChatPreparationResult> Prepare(ChatPrepareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserPrompt))
            return ChatPreparationResult.EmptyPrompt;

        var history = BuildHistory(request.Messages, request.SystemInstructions, request.WelcomeMessage);

        if (history.Count == 0 || history[^1].Role != "user")
            return ChatPreparationResult.EmptyPrompt;

        if (request.ContextLength > 0)
        {
            int historyBudget = (int)(request.ContextLength * _options.HistoryBudgetFraction);
            int estimatedHistoryTokens = history.Sum(m => EstimateTokens(m.Content));

            while (history.Count > 2 && estimatedHistoryTokens > historyBudget)
            {
                var removed = history[1]; // mantém o system (índice 0) e o prompt atual (último)
                history.RemoveAt(1);
                estimatedHistoryTokens -= EstimateTokens(removed.Content);
            }
        }

        double temperature = ChatMeasureTemperature.ObterTemperaturaRecomendada(request.UserPrompt);
        int baseTokens = request.FitsInGpu ? _options.BaseTokensGpu : _options.BaseTokensCpu;
        int maxTokens = temperature switch
        {
            <= 0.25 => (int)(baseTokens * 1.15),
            >= 0.75 => (int)(baseTokens * 0.90),
            _ => baseTokens
        };

        // Limita o num_predict para caber no contexto (reserva de ContextReserveFraction)
        if (request.ContextLength > 0)
        {
            int totalHistoryTokens = history.Sum(m => EstimateTokens(m.Content));
            int reservedForOutput = (int)(request.ContextLength * _options.ContextReserveFraction);
            int availableForOutput = request.ContextLength - totalHistoryTokens - reservedForOutput;

            // Guarda de tamanho: mesmo depois de aparar o histórico, o prompt actual pode
            // não caber. Sem esta verificação o Ollama responde 400 e o utilizador vê um
            // erro de rede em vez de "o teu prompt é grande demais para este modelo".
            if (totalHistoryTokens + reservedForOutput >= request.ContextLength)
            {
                return ChatPreparationResult.PromptTooLong(
                    totalHistoryTokens, request.ContextLength);
            }

            if (availableForOutput < 32) availableForOutput = 32;
            maxTokens = Math.Min(maxTokens, availableForOutput);
        }

        bool enableReasoning = await ResolveThinkingAsync();

        var payload = new OllamaChatPayload
        {
            Model = request.ModelName,
            Messages = history,
            Stream = true,
            // Para modelos com capacidade `thinking` envia o valor explícito (`true`/`false`):
            // omitir o campo faz o Ollama pensar por omissão e devolver o reasoning mesmo desativado.
            // Em modelos comuns o campo é omitido (senão o Ollama devolve 400 "does not support thinking").
            Think = request.ModelSupportsThinking ? enableReasoning : null,
            Options = new Dictionary<string, object>
            {
                { "num_ctx", request.ContextLength },
                { "temperature", Math.Round(temperature, 2) },
                { "num_predict", maxTokens },
                { "repeat_penalty", _options.RepeatPenalty },
                { "top_k", temperature <= 0.25 ? _options.TopKLow : _options.TopKHigh },
                { "top_p", temperature <= 0.30 ? _options.TopPLow : _options.TopPHigh }
            }
        };

        return ChatPreparationResult.Ready(new PreparedChatRequest
        {
            History = history,
            Temperature = temperature,
            MaxTokens = maxTokens,
            EnableReasoning = enableReasoning,
            Payload = payload
        });
    }

    /// <summary>
    /// Resolve se o reasoning (think) está ativo com prioridade: BD (página Settings) →
    /// configuração (appsettings/user-secrets) → default false. Mesmo padrão do AutomatedJudgeService.
    /// </summary>
    private async Task<bool> ResolveThinkingAsync()
    {
        try
        {
            string? valor = await _settingsRepository.GetValueAsync("Chat:EnableReasoning");
            if (valor is not null && bool.TryParse(valor, out bool ativo))
                return ativo;
        }
        catch
        {
            // Sem BD ou erro: usa a configuração.
        }

        return _options.EnableReasoning;
    }

    public static List<OllamaChatMessage> BuildHistory(
        IReadOnlyList<ChatMessage> messages,
        string systemInstructions,
        string welcomeMessage)
    {
        var historyPayload = new List<OllamaChatMessage>
        {
            new() { Role = "system", Content = systemInstructions }
        };

        foreach (var msg in messages)
        {
            if (!msg.HasPersistableContent) continue;
            if (msg.Text.StartsWith(welcomeMessage, StringComparison.OrdinalIgnoreCase)) continue;

            string roleAtual = msg.IsCurrentUser ? "user" : "assistant";

            if (historyPayload.Count == 0 || historyPayload[^1].Role != roleAtual)
            {
                historyPayload.Add(new OllamaChatMessage { Role = roleAtual, Content = msg.Text });
            }
            else
            {
                historyPayload[^1].Content += "\n" + msg.Text;
            }
        }

        if (historyPayload.Count > 0 && historyPayload[^1].Role == "assistant")
        {
            historyPayload.RemoveAt(historyPayload.Count - 1);
        }

        if (historyPayload.Count > 0 && historyPayload[^1].Role == "user")
        {
            string promptOriginal = historyPayload[^1].Content;
            historyPayload[^1].Content =
                $"[INSTRUCTION: Start your reply with EXACTLY this two-line format:\n{SummaryMarker} <3-5 word summary>\n{SummarySeparator}\nThen answer normally. Do NOT skip these two lines.]\n\n{promptOriginal}";
        }

        return historyPayload;
    }

    /// <summary>
    /// Extrai uma descrição inteligente a partir do prompt do utilizador: toma a
    /// primeira frase significativa (até ao primeiro ponto ou newline) e, se exceder
    /// maxLength, truncata na fronteira de palavra mais próxima.
    /// </summary>
    public static string SmartFallbackDescription(string prompt, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return string.Empty;

        string cleaned = prompt.Trim();

        // 1. Tomar a primeira linha não-vazia
        int newLineIdx = cleaned.IndexOf('\n');
        if (newLineIdx > 0)
            cleaned = cleaned[..newLineIdx].Trim();

        // 2. Se contiver ponto, tomar até ao primeiro
        int dotIdx = cleaned.IndexOf('.', StringComparison.Ordinal);
        if (dotIdx > 0 && dotIdx < cleaned.Length - 1)
            cleaned = cleaned[..dotIdx].Trim();

        // 3. Se ainda exceder maxLength, truncar na fronteira de palavra
        if (cleaned.Length <= maxLength)
            return cleaned;

        int corte = cleaned.LastIndexOf(' ', maxLength);
        if (corte <= maxLength / 2)
            corte = maxLength;

        return cleaned[..corte].Trim() + "...";
    }

    /// <summary>
    /// Extrai o SUMMARY e o separador --- do início da resposta do LLM,
    /// devolvendo o resumo (se existir) e o texto limpo sem o prefixo.
    /// Se o parse falhar, devolve (null, rawText) — fallback seguro.
    /// </summary>
    public static (string? summary, string cleanText) ExtractSummaryFromResponse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return (null, rawText);

        var lines = rawText.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        if (lines.Length < 2)
            return (null, rawText);

        string firstLine = lines[0].TrimStart();
        string secondLine = lines[1].Trim();

        if (!firstLine.StartsWith(SummaryMarker, StringComparison.OrdinalIgnoreCase))
            return (null, rawText);

        if (!string.Equals(secondLine, SummarySeparator, StringComparison.OrdinalIgnoreCase))
            return (null, rawText);

        string summary = firstLine[SummaryMarker.Length..].Trim();
        string cleanText = string.Join("\n", lines.Skip(2)).TrimStart();

        if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(cleanText))
            return (null, rawText);

        return (summary, cleanText);
    }

    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return Math.Max(1, text.Length / 4);
    }
}

public sealed class PreparedChatRequest
{
    public required List<OllamaChatMessage> History { get; init; }
    public required double Temperature { get; init; }
    public required int MaxTokens { get; init; }
    public required bool EnableReasoning { get; init; }
    public required OllamaChatPayload Payload { get; init; }
}

/// <summary>
/// Entrada de <see cref="ChatComposerService.Prepare"/>. Um record em vez de 8 parâmetros
/// posicionais, em que dois <c>bool</c> vizinhos eram trocados à vista.
/// </summary>
public sealed record ChatPrepareRequest
{
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public required string UserPrompt { get; init; }
    public required string SystemInstructions { get; init; }
    public required string WelcomeMessage { get; init; }
    public required string ModelName { get; init; }

    /// <summary>0 quando o limite de contexto do modelo é desconhecido (sem verificação).</summary>
    public int ContextLength { get; init; }

    public bool FitsInGpu { get; init; }

    /// <summary>Capacidade "thinking" declarada pelo /api/show do modelo.</summary>
    public bool ModelSupportsThinking { get; init; }
}

public enum ChatPreparationStatus
{
    Ready,

    /// <summary>Não há prompt válido a enviar.</summary>
    EmptyPrompt,

    /// <summary>O prompt não cabe no contexto do modelo, mesmo após aparar o histórico.</summary>
    PromptTooLong
}

/// <summary>
/// Resultado da preparação. Antes era <c>PreparedChatRequest?</c>, onde <c>null</c> significava
/// ao mesmo tempo "prompt vazio" e "não sei porquê" — a página não conseguia distinguir.
/// </summary>
public sealed record ChatPreparationResult
{
    public required ChatPreparationStatus Status { get; init; }

    public PreparedChatRequest? Request { get; init; }

    /// <summary>Tokens estimados que o histórico já ocupa (para a mensagem de erro).</summary>
    public int PromptTokens { get; init; }

    public int ContextLength { get; init; }

    public static ChatPreparationResult Ready(PreparedChatRequest request)
        => new() { Status = ChatPreparationStatus.Ready, Request = request };

    public static ChatPreparationResult EmptyPrompt
        => new() { Status = ChatPreparationStatus.EmptyPrompt };

    public static ChatPreparationResult PromptTooLong(int promptTokens, int contextLength)
        => new()
        {
            Status = ChatPreparationStatus.PromptTooLong,
            PromptTokens = promptTokens,
            ContextLength = contextLength
        };
}
