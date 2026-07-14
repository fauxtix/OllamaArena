using System.Collections.Concurrent;
using System.Text;

namespace OllamaFluentUIChat.Services.Providers;

public interface IPromptTemplateProvider
{
    Task<string> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default);
    void Invalidate(string templateName);
}

public sealed class PromptTemplateProvider : IPromptTemplateProvider
{
    private readonly ILogger<PromptTemplateProvider> _logger;
    private readonly string _promptsRoot;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public PromptTemplateProvider(ILogger<PromptTemplateProvider> logger)
    {
        _logger = logger;
        _promptsRoot = Path.Combine(AppContext.BaseDirectory, "Prompts");
    }

    public async Task<string> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name is required.", nameof(templateName));

        var path = Path.Combine(_promptsRoot, templateName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Prompt template not found: {path}");

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load prompt template {TemplateName} from {PromptsRoot}", templateName, _promptsRoot);
            throw;
        }
    }

    public void Invalidate(string templateName)
    {
        if (!string.IsNullOrWhiteSpace(templateName))
            _cache.TryRemove(templateName, out _);
    }
}

public static class PromptTemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string?> values)
    {
        if (template is null)
            throw new ArgumentNullException(nameof(template));

        if (values is null || values.Count == 0)
            return template;

        var s = template;
        foreach (var kvp in values)
        {
            var token = "{" + kvp.Key + "}";
            // Keep token replacement case-sensitive (templates likely expect exact token), but you may switch to OrdinalIgnoreCase if you need.
            s = s.Replace(token, kvp.Value ?? string.Empty, StringComparison.Ordinal);
        }

        return s;
    }
}
