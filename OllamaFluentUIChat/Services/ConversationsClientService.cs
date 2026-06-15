using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;

namespace OllamaFluentUIChat.Services
{
    public class ConversationsClientService : IConversationService
    {
        private readonly IConversationRepository _repo;
        public ConversationsClientService(IConversationRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }
        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            await _repo.DeleteAsync(id, ct);
        }
        public async Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken ct = default)
        {
            return await _repo.GetAllAsync(ct);
        }
        public async Task<Conversation?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            return await _repo.GetByIdAsync(id, ct);
        }
        public async Task<Conversation> UpsertAsync(Conversation conv, CancellationToken ct = default)
        {
            await _repo.UpdateAsync(conv, ct);
            return conv;
        }
    }
}
