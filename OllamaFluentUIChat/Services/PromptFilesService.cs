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

    public List<string> GetPromptFilesAsync()
    {
        var promptsDir = Path.Combine(AppContext.BaseDirectory, "Prompts");

        if (!Directory.Exists(promptsDir))
            return new List<string>();

        return [.. Directory.GetFiles(promptsDir, "*.*")
            .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .OfType<string>()];
    }

    // Novo método integrado para carregar o conteúdo do ficheiro selecionado
    public async Task<string?> GetPromptFileContentAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName required", nameof(fileName));

        if (!IsValidPromptFilename(fileName))
            throw new ArgumentException("Invalid filename.", nameof(fileName));

        try
        {
            var promptsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Prompts"));
            var file = Path.GetFullPath(Path.Combine(promptsDir, fileName));
            string expectedPrefix = promptsDir + Path.DirectorySeparatorChar;

            // Proteção de Path Traversal idêntica à do SavePromptFileAsync
            if (!file.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid filename path.");

            if (!File.Exists(file))
                throw new FileNotFoundException("O ficheiro de prompt selecionado não existe.");

            return await File.ReadAllTextAsync(file, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler o ficheiro de prompt: {File}", fileName);
            throw;
        }
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
            throw;
        }
    }

    private static bool IsValidPromptFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        if (filename.Contains("..", StringComparison.Ordinal))
            return false;

        return true;
    }
}
