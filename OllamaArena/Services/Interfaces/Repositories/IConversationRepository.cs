using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;

namespace OllamaArena.Services.Interfaces.Repositories;

public interface IConversationRepository
{
    Task<int> CreateConversationAsync(string titulo, string nomeModelo, string? descricao = null);
    Task<bool> UpdateDescricaoAsync(int conversationId, string descricao);
    Task<bool> AddMessageAsync(ChatConversationMessage message);
    Task<bool> TouchConversationAsync(int conversationId);
    Task<List<ChatConversation>> GetConversationsAsync();
    Task<List<ChatConversationMessage>> GetMessagesAsync(int conversationId);
    Task<bool> DeleteConversationAsync(int conversationId);
    Task<List<ConversationExportItem>> GetAllForExportAsync();
    Task<(int Conversas, int Mensagens)> ImportAsync(List<ConversationExportItem> itens);
}
