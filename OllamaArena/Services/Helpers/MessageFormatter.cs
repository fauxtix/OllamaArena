using Markdig;
using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Helpers
{
    /// <summary>
    /// Formatador de respostas do Ollama otimizado para Fluent UI.
    /// Usa Markdig com pipeline completa + heurísticas + sanitização HTML por allow-list.
    /// </summary>
    public static partial class MessageFormatter
    {
        public static string FormatMessagePlus(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            if (content == ChatMessage.PendingPlaceholder)
            {
                return "<div class='typing-dots'><span></span><span></span><span></span></div>";
            }

            content = RemoveInternalReasoning(content);

            content = RescueGluedFence(content);

            string masked = MaskCode(content, out var codeSegments);

            bool needsHeavyFormatting = DetectNoise(masked);

            masked = NormalizeBasic(masked);

            if (needsHeavyFormatting)
                masked = ApplyUniversalSeparations(masked);

            if (needsHeavyFormatting || masked.Contains("|"))
                masked = ReconstructTables(masked);

            string restored = RestoreCode(masked, codeSegments);

            var pipeline = BuildMarkdownPipeline();
            string html = Markdown.ToHtml(restored, pipeline).Trim();

            html = ConvertParagraphsToDivs(html);

            // Última barreira antes do (MarkupString): allow-list estrita. Sem isto, o
            // Markdig emite HTML cru do texto do modelo (ex.: <img src=x onerror=...>).
            return HtmlOutputSanitizer.Sanitize(html);
        }

        private static MarkdownPipeline BuildMarkdownPipeline()
        {
            return new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .UsePipeTables()
                .UseTaskLists()
                .UseAutoLinks()
                .UseDefinitionLists()
                .UseEmphasisExtras()
                .Build();
        }
    }
}
