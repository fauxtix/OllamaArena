using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Services.Interfaces.Repositories;

namespace OllamaArena.Services;

public sealed class ChatComposerService
{
    private readonly OllamaOptions _options;
    private readonly ISettingsRepository _settingsRepository;

    public ChatComposerService(IOptions<OllamaOptions> options, ISettingsRepository settingsRepository)
    {
        _options = options.Value;
        _settingsRepository = settingsRepository;
    }

    public async Task<PreparedChatRequest?> Prepare(
        IReadOnlyList<ChatMessage> messages,
        string userPrompt,
        string systemInstructions,
        string welcomeMessage,
        string modelName,
        int contextLength,
        bool fitsInGpu,
        bool modelSupportsThinking = false)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            return null;

        var history = BuildHistory(messages, systemInstructions, welcomeMessage);

        if (history.Count == 0 || history[^1].Role != "user")
            return null;

        if (contextLength > 0)
        {
            int historyBudget = (int)(contextLength * _options.HistoryBudgetFraction);
            int estimatedHistoryTokens = history.Sum(m => EstimateTokens(m.Content));

            while (history.Count > 2 && estimatedHistoryTokens > historyBudget)
            {
                var removed = history[1]; // mantém o system (índice 0) e o prompt atual (último)
                history.RemoveAt(1);
                estimatedHistoryTokens -= EstimateTokens(removed.Content);
            }
        }

        double temperature = ChatMeasureTemperature.ObterTemperaturaRecomendada(userPrompt);
        int baseTokens = fitsInGpu ? _options.BaseTokensGpu : _options.BaseTokensCpu;
        int maxTokens = temperature switch
        {
            <= 0.25 => (int)(baseTokens * 1.15),
            >= 0.75 => (int)(baseTokens * 0.90),
            _ => baseTokens
        };

        // Limita o num_predict para caber no contexto (reserva de ContextReserveFraction)
        if (contextLength > 0)
        {
            int totalHistoryTokens = history.Sum(m => EstimateTokens(m.Content));
            int reservedForOutput = (int)(contextLength * _options.ContextReserveFraction);
            int availableForOutput = contextLength - totalHistoryTokens - reservedForOutput;
            if (availableForOutput < 32) availableForOutput = 32;
            maxTokens = Math.Min(maxTokens, availableForOutput);
        }

        bool enableReasoning = await ResolveThinkingAsync();

        var payload = new OllamaChatPayload
        {
            Model = modelName,
            Messages = history,
            Stream = true,
            // Para modelos com capacidade `thinking` envia o valor explícito (`true`/`false`):
            // omitir o campo faz o Ollama pensar por omissão e devolver o reasoning mesmo desativado.
            // Em modelos comuns o campo é omitido (senão o Ollama devolve 400 "does not support thinking").
            Think = modelSupportsThinking ? enableReasoning : null,
            Options = new Dictionary<string, object>
            {
                { "num_ctx", contextLength },
                { "temperature", Math.Round(temperature, 2) },
                { "num_predict", maxTokens },
                { "repeat_penalty", _options.RepeatPenalty },
                { "top_k", temperature <= 0.25 ? _options.TopKLow : _options.TopKHigh },
                { "top_p", temperature <= 0.30 ? _options.TopPLow : _options.TopPHigh }
            }
        };

        return new PreparedChatRequest
        {
            History = history,
            Temperature = temperature,
            MaxTokens = maxTokens,
            EnableReasoning = enableReasoning,
            Payload = payload
        };
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
            if (string.IsNullOrWhiteSpace(msg.Text) || msg.Text == "...") continue;
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

        return historyPayload;
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
