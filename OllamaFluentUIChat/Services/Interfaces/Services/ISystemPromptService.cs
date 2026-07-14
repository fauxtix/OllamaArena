namespace OllamaFluentUIChat.Services.Interfaces.Services;

public interface ISystemPromptService
{
    Task<string> GetSystemPrompt(CancellationToken cancellationToken);
}
