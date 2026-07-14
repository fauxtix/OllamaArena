using OllamaFluentUIChat.Services.Providers;

namespace OllamaFluentUIChat.Services;

public class PromptFilesService
{
    private readonly ILogger<PromptFilesService> _logger;
    private readonly IPromptTemplateProvider _promptProvider;

    public PromptFilesService(ILogger<PromptFilesService> logger, IPromptTemplateProvider promptProvider)
    {
        _logger = logger;
        _promptProvider = promptProvider;
    }

    public async Task<List<string>> GetPromptFilesAsync(CancellationToken cancellationToken = default)
    {
        return [];
    }


    public async Task SavePromptFileAsync(string fileName, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("fileName required", nameof(fileName));
        if (content is null) throw new ArgumentNullException(nameof(content));
        if (content.Length > 200_000) throw new InvalidOperationException("Prompt file too large.");

        if (!IsValidPromptFilename(fileName) || string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Invalid filename or content.", nameof(fileName));

        try
        {
            string rootDir = AppContext.BaseDirectory;

            var promptsDir = Path.GetFullPath(Path.Combine(rootDir, "Prompts"));
            Directory.CreateDirectory(promptsDir);

            var file = Path.GetFullPath(Path.Combine(promptsDir, fileName));
            string expectedPrefix = promptsDir + Path.DirectorySeparatorChar;

            if (!file.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid filename.");

            await File.WriteAllTextAsync(file, content, cancellationToken);

            _promptProvider.Invalidate(fileName);

            _logger.LogInformation("Saved prompt file: {File}", file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gravar system prompt");
            throw; // Relança a exceção para que o ToastService no Blazor saiba que falhou e mostre a mensagem de erro
        }
    }
    private static bool IsValidPromptFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        // Reject any path separators or parent-directory traversal attempts.
        if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        if (filename.Contains("..", StringComparison.Ordinal))
            return false;

        return true;
    }

}
