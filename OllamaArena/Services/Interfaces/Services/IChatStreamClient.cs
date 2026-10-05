using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Interfaces.Services;

/// <summary>
/// Cliente de streaming do /api/chat. Isola a página de tudo o que é HTTP/NDJSON:
/// serialização do payload, leitura linha a linha, parsing de cada chunk, accumulates
/// métricas e julgamento de "done".
/// </summary>
public interface IChatStreamClient
{
    /// <summary>
    /// Envia o payload e consome o stream até ao fim.
    /// </summary>
    /// <param name="onProgress">
    /// Chamado com um instantâneo do stream para atualizar a UI. Já vem com limitação de
    /// frequência (<c>progressInterval</c>): não deve ser chamado a cada token.
    /// </param>
    /// <param name="progressInterval">Intervalo mínimo entre dois <paramref name="onProgress"/>.</param>
    /// <exception cref="ChatStreamException">Falha HTTP ou de transporte do Ollama.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> foi cancelado.</exception>
    Task<ChatStreamResult> SendAsync(
        OllamaChatPayload payload,
        bool captureReasoning,
        CancellationToken cancellationToken,
        Func<ChatStreamProgress, Task>? onProgress = null,
        TimeSpan? progressInterval = null);
}

/// <summary>
/// Erro de comunicação com o Ollama durante o streaming. A página mostra a mensagem ao
/// utilizador sem destruir a resposta parcial já recebida — antes, qualquer exceção
/// genérica substituía a resposta inteira por "Ocorreu um erro".
/// </summary>
public class ChatStreamException : Exception
{
    public ChatStreamException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}