using Markdig;
using Microsoft.Extensions.Localization;
using OllamaArena.Resources;

namespace OllamaArena.Services.Helpers
{
    // Adicionamos o MarkdownRenderer como dependência aqui
    public class ReadMeService(ILogger<ReadMeService> logger, HttpClient http, MarkdownRenderer renderer, IStringLocalizer<SharedResources> L)
    {
        // Expõe o renderer para que a UI possa usá-lo
        public MarkdownRenderer MarkdownRenderer { get; } = renderer;

        public async Task<string> LoadReadmeAsync()
        {
            const string url = "https://raw.githubusercontent.com/fauxtix/OllamaArena/master/README.md";

            try
            {
                var markdown = await http.GetStringAsync(url);
                return markdown;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao carregar o README do GitHub.");
                return L["Readme.LoadError"] + Environment.NewLine +
                    L["Readme.LoadErrorHint"];
            }
        }
    }

    public class MarkdownRenderer
    {
        protected readonly MarkdownPipeline _pipeline =
            new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

        public string ToHtml(string markdown)
        {
            return Markdown.ToHtml(markdown, _pipeline);
        }
    }
}