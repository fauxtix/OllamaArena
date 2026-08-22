using Markdig;
using System.Text.RegularExpressions;

namespace OllamaArena.Services.Helpers
{
    /// <summary>
    /// Formatador de respostas do Ollama otimizado para Fluent UI
    /// Usa Markdig com pipeline completa + heurísticas + sanitização HTML.
    /// </summary>
    public static partial class MessageFormatter
    {
        private static readonly HashSet<string> DangerousTags = new(StringComparer.OrdinalIgnoreCase)
        {
            "script", "iframe", "object", "embed", "applet", "form",
            "input", "button", "select", "textarea", "label",
            "base", "meta", "link"
        };

        private static readonly Regex DangerousTagRegex = new(
            @"<\s*/?\s*(script|iframe|object|embed|applet|form|input|button|select|textarea|label|base|meta|link)\b[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EventHandlerRegex = new(
            @"\s+on\w+\s*=\s*([""'])(.*?)\1",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex JavascriptUriRegex = new(
            @"href\s*=\s*([""'])\s*javascript\s*:",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DataBindingRegex = new(
            @"\{\{.*?\}\}",
            RegexOptions.Compiled);

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

            html = StripDangerousHtml(html);

            return html;
        }

        private static string StripDangerousHtml(string html)
        {
            html = DangerousTagRegex.Replace(html, "");
            html = EventHandlerRegex.Replace(html, "");
            html = JavascriptUriRegex.Replace(html, "href=$1#");
            html = DataBindingRegex.Replace(html, "");
            return html;
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
