using OllamaFluentUIChat.Services.Interfaces.Services;
using OllamaFluentUIChat.Services.Providers;

namespace OllamaFluentUIChat.Services;

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
