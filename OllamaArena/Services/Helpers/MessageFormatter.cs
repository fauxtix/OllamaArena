using Markdig;
using System.Text.RegularExpressions;

namespace OllamaArena.Services.Helpers
{
    /// <summary>
    /// Formatador de respostas do Ollama otimizado para Fluent UI
    /// Usa Markdig com pipeline completa + heurísticas
    /// </summary>
    public static partial class MessageFormatter
    {
        public static string FormatMessagePlus(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            if (content == "...")
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

            return html;
        }

        private static MarkdownPipeline BuildMarkdownPipeline()
        {
            return new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                //.UseBootstrap()
                //.UseEmojiAndSmiley()
                .UsePipeTables()
                .UseTaskLists()
                .UseAutoLinks()
                //.UseFootnotes()
                .UseDefinitionLists()
                .UseEmphasisExtras()
                .Build();
        }
    }
}
