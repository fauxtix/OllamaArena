using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Interfaces.Services;

/// <summary>
/// Persiste um turno completo (conversa + mensagens + resposta de benchmark) de forma
/// atómica. Substitui a cadeia de seis métodos "Persist*Async" encadeados na página, cada
/// um com o seu próprio <c>try/catch</c>, que deixavam conversas órfãs e mensagens
/// perdidas quando uma das escritas falhava.
/// </summary>
public interface IChatTurnWriter
{
    /// <summary>
    /// Grava o turno. <paramref name="conversationId"/> = 0 cria uma conversa nova;
    /// <paramref name="promptId"/> = 0 procura/cria o prompt de benchmark.
    /// </summary>
    /// <returns>
    /// Os identificadores a reter para o turno seguinte (conversa aberta e prompt
    /// reutilizado) e a descrição efectivamente guardada na base de dados.
    /// </returns>
    /// <exception cref="ChatTurnPersistenceException">
    /// Lançado quando a escrita falha; nada fica gravado.
    /// </exception>
    Task<ChatTurnWriteResult> WriteAsync(ChatTurn turn, string modelName, int conversationId, int promptId);
}

/// <summary>
/// Falha ao persistir um turno. Distingue "o turno não chegou a ser gravado" (a página
/// reverte o estado e avisa o utilizador) de erros de streaming, que sãopresented à parte.
/// </summary>
public class ChatTurnPersistenceException : Exception
{
    public ChatTurnPersistenceException(string message, Exception innerException) : base(message, innerException) { }
}