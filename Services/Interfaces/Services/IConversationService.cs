using Services.Models.DTO;

namespace Services.Interfaces.Services
{
    public interface IConversationService
    {
        Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken ct = default);
        Task<Conversation?> GetByIdAsync(string id, CancellationToken ct = default);
        Task<Conversation> UpsertAsync(Conversation conv, CancellationToken ct = default);
        Task DeleteAsync(string id, CancellationToken ct = default);
    }
}
