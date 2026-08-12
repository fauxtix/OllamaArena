namespace OllamaArena.Services.Interfaces.Services;

public interface ISystemPromptService
{
    Task<string> GetSystemPrompt(CancellationToken cancellationToken);
}
