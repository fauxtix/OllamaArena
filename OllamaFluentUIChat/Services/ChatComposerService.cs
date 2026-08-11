using Microsoft.Extensions.Options;
using OllamaFluentUIChat.Models.DTO;

namespace OllamaFluentUIChat.Services;

public sealed class ChatComposerService
{
    private readonly OllamaOptions _options;

    public ChatComposerService(IOptions<OllamaOptions> options)
    {
        _options = options.Value;
    }

    public PreparedChatRequest? Prepare(
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

        var payload = new OllamaChatPayload
        {
            Model = modelName,
            Messages = history,
            Stream = true,
            // Só envia `think` quando o modelo suporta reasoning; em modelos comuns o Ollama
            // devolve 400 ("does not support thinking") se o campo for true.
            Think = _options.EnableReasoning && modelSupportsThinking ? true : null,
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
            Payload = payload
        };
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
    public required OllamaChatPayload Payload { get; init; }
}
