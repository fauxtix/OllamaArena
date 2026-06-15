using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace OllamaFluentUIChat.Services.Implementations.Services;

public sealed class OllamaChatService : IAiChatService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaChatService> _logger;
    private readonly IAiSettingsProvider _aiSettingsProvider;

    public OllamaChatService(
        HttpClient httpClient,
        ILogger<OllamaChatService> logger,
        IAiSettingsProvider aiSettingsProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiSettingsProvider = aiSettingsProvider ?? throw new ArgumentNullException(nameof(aiSettingsProvider));
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
            List<ChatMessageDto> messages,
            string? connectionId,
            string? correlationId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Obtém o endereço base da API do Ollama e o modelo ativo (ex: llama3, deepseek, etc.)
        var (baseUrl, model) = await _aiSettingsProvider.GetAiSettingsAsync();

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
        {
            _logger.LogError("Configurações do Ollama inválidas. Endereço ou Modelo em falta.");
            yield return "Erro interno: Configurações do serviço de IA não encontradas.";
            yield break;
        }

        var payload = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = 0.7,
            stream = true
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"{baseUrl.TrimEnd('/')}/api/chat"))
        {
            Content = JsonContent.Create(payload)
        };

        HttpResponseMessage response;
        string? connectionErrorMessage = null; // Var para guardar o erro fora do try-catch

        try
        {
            // ResponseHeadersRead é fulcral para iniciarmos a leitura do corpo da resposta em modo Stream
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha crítica ao enviar pedido HTTP para o Ollama em {Url}", baseUrl);
            connectionErrorMessage = $"Não foi possível ligar ao servidor do Ollama: {ex.Message}";
            response = null!; // Apenas para satisfazer o compilador, já que vamos fazer break
        }

        // Se houve erro de ligação, fazemos o yield e o break aqui (fora do try-catch)
        if (connectionErrorMessage != null)
        {
            yield return connectionErrorMessage;
            yield break;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Ollama respondeu com erro: {StatusCode} - {Error}", response.StatusCode, error);
            yield return $"Erro do Ollama ({response.StatusCode}): Erro ao processar o modelo.";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;

        // O Ollama devolve um objeto JSON por linha quando stream=true
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            string? content = null;

            try
            {
                using var json = JsonDocument.Parse(line);
                content = ExtractContent(json.RootElement);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Não foi possível interpretar a linha JSON do Ollama: {Line}", line);
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                yield return content;
            }
        }
    }
    private static string? ExtractContent(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var msg))
            return null;

        // Tenta extrair a propriedade regular de resposta
        string? content = msg.TryGetProperty("content", out var c)
            ? c.GetString()
            : null;

        if (!string.IsNullOrWhiteSpace(content))
        {
            return Clean(content);
        }

        // Tenta extrair blocos de pensamento de modelos estilo R1/Reasoning se presentes
        string? thinking = msg.TryGetProperty("thinking", out var t)
            ? t.GetString()
            : null;

        if (!string.IsNullOrWhiteSpace(thinking))
        {
            return Clean(thinking);
        }

        return null;
    }

    private static string Clean(string text)
    {
        // Remove tags estruturais que possam poluir a renderização crua da interface
        return text
            .Replace("<think>", "")
            .Replace("</think>", "");
    }
}
