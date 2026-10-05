using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;
using System.Data;

namespace OllamaArena.Services.Interfaces.Repositories;

public interface IConversationRepository
{
    /// <summary>
    /// Cria uma conversa. <paramref name="connection"/>/<paramref name="transaction"/> são
    /// opcionais: quando fornecidos, a operação participa numa transacção já aberta pelo
    /// chamador (ver <c>ChatTurnWriter</c>); caso contrário abre a sua própria ligação.
    /// </summary>
    Task<int> CreateConversationAsync(string titulo, string nomeModelo, string? descricao = null,
        IDbConnection? connection = null, IDbTransaction? transaction = null);

    Task<bool> UpdateDescricaoAsync(int conversationId, string descricao);

    /// <summary>Ver <see cref="CreateConversationAsync"/> para o semântica de transacção.</summary>
    Task<bool> AddMessageAsync(ChatConversationMessage message,
        IDbConnection? connection = null, IDbTransaction? transaction = null);

    Task<bool> TouchConversationAsync(int conversationId,
        IDbConnection? connection = null, IDbTransaction? transaction = null);
    Task<List<ChatConversation>> GetConversationsAsync();
    Task<List<ChatConversationMessage>> GetMessagesAsync(int conversationId);
    Task<bool> DeleteConversationAsync(int conversationId);
    Task<List<ConversationExportItem>> GetAllForExportAsync();
    Task<(int Conversas, int Mensagens)> ImportAsync(List<ConversationExportItem> itens);
    Task ClearAllConversationsAsync();
}
