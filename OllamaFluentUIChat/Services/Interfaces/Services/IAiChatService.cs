using OllamaFluentUIChat.Models.DTO;

namespace OllamaFluentUIChat.Services.Interfaces.Services
{
    public interface IAiChatService
    {
        IAsyncEnumerable<string> StreamChatAsync(
            List<ChatMessageDto> messages,
            string? connectionId,
            string? correlationId,
            CancellationToken cancellationToken);
    }
}
