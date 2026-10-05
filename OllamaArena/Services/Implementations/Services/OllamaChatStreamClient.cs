using OllamaArena.Models.DTO;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Interfaces.Services;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace OllamaArena.Services.Implementations.Services;

/// <summary>
/// Cliente de streaming do Ollama. Consome o NDJSON de /api/chat linha a linha, parseando
/// cada linha uma única vez (antes o "done" era parseado duas vezes: JsonDocument e depois
/// JsonSerializer sobre o mesmo texto).
/// </summary>
public class OllamaChatStreamClient : IChatStreamClient
{
    /// <summary>Intervalo por omissão entre actualizações da UI durante o streaming.</summary>
    public static readonly TimeSpan DefaultProgressInterval = TimeSpan.FromMilliseconds(80);

    /// <summary>
    /// Linhas malformadas que são ignoradas antes de desistir. Tolerar zero deixava um
    /// único chunk corrompido matar a resposta inteira.
    /// </summary>
    private const int MaxMalformedLines = 5;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OllamaChatStreamClient> _logger;

    public OllamaChatStreamClient(IHttpClientFactory httpClientFactory, ILogger<OllamaChatStreamClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ChatStreamResult> SendAsync(
        OllamaChatPayload payload,
        bool captureReasoning,
        CancellationToken cancellationToken,
        Func<ChatStreamProgress, Task>? onProgress = null,
        TimeSpan? progressInterval = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var client = _httpClientFactory.CreateClient("Ollama");

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(payload)
        };

        using var response = await SendWithCancellationAsync(client, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string body = await TryReadErrorBodyAsync(response, cancellationToken);
            string message = $"Ollama respondeu {(int)response.StatusCode} ({response.ReasonPhrase}). {body}".TrimEnd();

            _logger.LogError("Ollama devolveu {StatusCode} no /api/chat. Corpo: {Body}",
                (int)response.StatusCode, body);

            throw new ChatStreamException(message);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        return await ReadStreamAsync(reader, payload.Model, captureReasoning, cancellationToken, onProgress,
            progressInterval ?? DefaultProgressInterval);
    }

    private static async Task<HttpResponseMessage> SendWithCancellationAsync(
        HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new ChatStreamException(
                $"Não foi possível contactar o Ollama ({ex.Message}). Confirme que `ollama serve` está a correr.", ex);
        }
    }

    private async Task<ChatStreamResult> ReadStreamAsync(
        StreamReader reader,
        string modelName,
        bool captureReasoning,
        CancellationToken cancellationToken,
        Func<ChatStreamProgress, Task>? onProgress,
        TimeSpan progressInterval)
    {
        var throttle = new StreamProgressThrottle(progressInterval);
        var result = new ChatStreamAccumulator(captureReasoning);

        var stopwatch = Stopwatch.StartNew();
        bool firstChunkLogged = false;

        while (true)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                result.Accumulate(root);
                result.ReadFinalReasoning(root);

                if (!result.ReceivedText)
                    continue;

                if (!firstChunkLogged)
                {
                    firstChunkLogged = true;
                    _logger.LogInformation(
                        "Primeiro token recebido após {Seconds:F1}s (modelo {ModelName})",
                        stopwatch.Elapsed.TotalSeconds, modelName);
                }

                // Publica o primeiro token de imediato e os seguintes com limitação de
                // frequência: é aqui que se evita re-renderizar a UI a cada token.
                if (onProgress is not null && throttle.ShouldPublish())
                {
                    await onProgress(new ChatStreamProgress(
                        result.Text,
                        result.Reasoning,
                        result.TotalTokens,
                        result.EstimatedOutputTokens));
                }
            }
            catch (JsonException ex)
            {
                // Uma linha corrompida não deve deitar abaixo a resposta já recebida.
                _logger.LogWarning("Linha JSON inválida do Ollama ignorada: {Message}. Linha: {Line}",
                    ex.Message, TruncarParaLog(line));

                result.RecordMalformedLine();

                if (result.MalformedLines > MaxMalformedLines)
                {
                    throw new ChatStreamException(
                        $"O stream do Ollama devolveu {result.MalformedLines} linhas ilegíveis. " +
                        "A resposta foi interrompida para não devolver conteúdo corrompido.", ex);
                }
            }
        }

        stopwatch.Stop();

        return new ChatStreamResult
        {
            Text = result.Text,
            Reasoning = result.Reasoning,
            PromptTokens = result.PromptTokens,
            EvalTokens = result.EvalTokens,
            EvalDurationNs = result.EvalDurationNs,
            LoadDurationNs = result.LoadDurationNs,
            Done = result.Done
        };
    }

    private static async Task<string> TryReadErrorBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            return TruncarParaLog(body);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TruncarParaLog(string value)
        => value.Length <= 500 ? value : value[..500] + "…";
}