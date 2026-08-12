using OllamaArena.Services.Interfaces.Services;
using OllamaArena.Services.Providers;

namespace OllamaArena.Services;

public class SystemPromptService : ISystemPromptService
{
    private readonly IPromptTemplateProvider _prompts;

    public SystemPromptService(IPromptTemplateProvider prompts)
    {
        _prompts = prompts;
    }

    public async Task<string> GetSystemPrompt(CancellationToken cancellationToken)
    {
        var systemTemplate = await _prompts.GetTemplateAsync("system-prompt.txt", cancellationToken);
        var output = PromptTemplateRenderer.Render(systemTemplate, new Dictionary<string, string?>());
        return output;
    }
}
