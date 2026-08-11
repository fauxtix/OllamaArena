using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Models.Entities;

namespace OllamaFluentUIChat.Services.Interfaces.Repositories;

public interface IConversationRepository
{
    Task<int> CreateConversationAsync(string titulo, string nomeModelo);
    Task<bool> AddMessageAsync(ChatConversationMessage message);
    Task<bool> TouchConversationAsync(int conversationId);
    Task<List<ChatConversation>> GetConversationsAsync();
    Task<List<ChatConversationMessage>> GetMessagesAsync(int conversationId);
    Task<bool> DeleteConversationAsync(int conversationId);
    Task<List<ConversationExportItem>> GetAllForExportAsync();
    Task<(int Conversas, int Mensagens)> ImportAsync(List<ConversationExportItem> itens);
}
