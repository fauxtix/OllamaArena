
namespace OllamaFluentUIChat.Services.Interfaces.Repositories
{
    public interface IConversationRepository
    {
        Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<Conversation?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task InsertAsync(Conversation conv, CancellationToken cancellationToken = default);
        Task UpdateAsync(Conversation conv, CancellationToken cancellationToken = default);
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    }
}
