using Markdig;
using System.Text.RegularExpressions;

namespace OllamaFluentUIChat.Services.Helpers
{
    public static class MessageFormatter
    {
        public static string FormatMessagePROPlus(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";
            if (content == "...")
            {
                return "<div class='typing-dots'><span></span><span></span><span></span></div>";
            }

            // 0. Remover raciocínio interno (DeepSeek, Phi, etc.)
            content = Regex.Replace(content, @"<think>.*?</think>", "", RegexOptions.Singleline);

            // 1. Deteção automática de ruído
            bool needsHeavyFormatting = DetectNoise(content);

            // 2. Normalização mínima (sempre aplicada)
            content = NormalizeBasic(content);

            // 3. Heurísticas universais (apenas se houver ruído)
            if (needsHeavyFormatting)
                content = SeparateUniversalPatterns(content);

            // 4. Reconstrução mínima de tabelas (apenas se houver ruído)
            if (needsHeavyFormatting)
                content = ReconstructMinimalTables(content);

            // 5. Markdown → HTML
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .UseBootstrap()
                .UseEmojiAndSmiley()
                .Build();

            var html = Markdown.ToHtml(content, pipeline).Trim();

            // 6. Ajustes finais para Fluent UI
            html = html.Replace("<p>", "<div>").Replace("</p>", "</div>");

            return html;
        }

        // ---------------------------------------------------------
        // Deteção automática de ruído
        // ---------------------------------------------------------
        private static bool DetectNoise(string content)
        {
            if (!content.Contains("\n") && content.Length > 200)
                return true;

            if (Regex.IsMatch(content, @"\|[^\s]"))
                return true;

            if (Regex.IsMatch(content, @"\d+%[A-Z]"))
                return true;

            if (Regex.IsMatch(content, @"[a-z][A-Z]"))
                return true;

            if (Regex.IsMatch(content, @"\d+\.[A-Za-z]"))
                return true;

            if (Regex.IsMatch(content, @"[A-Za-z]+\s+\d+%\s+\d+%"))
                return true;

            return false;
        }

        // ---------------------------------------------------------
        // Normalização mínima
        // ---------------------------------------------------------
        private static string NormalizeBasic(string content)
        {
            int ticks = Regex.Matches(content, "```").Count;
            if (ticks % 2 != 0)
                content += "\n```";

            content = Regex.Replace(content, @"([^\n])\s*(#{1,6}\s)", "$1\n\n$2");
            content = Regex.Replace(content, @"^(#{1,6})([^\s#])", "$1 $2", RegexOptions.Multiline);
            content = Regex.Replace(content, @"([a-zA-Z])(\d+\.\s)", "$1\n\n$2");
            content = Regex.Replace(content, @"([^\n])(\*\s)", "$1\n\n$2");

            return content;
        }

        // ---------------------------------------------------------
        // Heurísticas universais
        // ---------------------------------------------------------
        private static string SeparateUniversalPatterns(string content)
        {
            content = Regex.Replace(content, @"([a-z])([A-Z])", "$1\n$2");
            content = Regex.Replace(content, @"(%)([A-Z])", "%\n$2");
            content = Regex.Replace(content, @":([A-Z])", ":\n$1");
            content = Regex.Replace(content, @"([A-Za-z])(\d+%)", "$1\n$2");
            content = Regex.Replace(content, @"([a-z])([A-Z][a-z]+)", "$1\n$2");

            content = Regex.Replace(content, @"((\b[A-Z][a-z]+\b.*?%){3,})", m =>
            {
                return string.Join("\n", m.Value.Split(' '));
            });

            return content;
        }

        // ---------------------------------------------------------
        // Reconstrução mínima de tabelas
        // ---------------------------------------------------------
        private static string ReconstructMinimalTables(string content)
        {
            var lines = content.Split('\n');
            var output = new List<string>();

            foreach (var line in lines)
            {
                int pipeCount = line.Count(c => c == '|');

                if (pipeCount >= 4 && !line.Contains("---"))
                {
                    var parts = line.Split('|')
                                    .Select(p => p.Trim())
                                    .Where(p => p.Length > 0)
                                    .ToList();

                    output.Add("| " + string.Join(" | ", parts) + " |");

                    if (parts.Count > 2)
                        output.Add("| " + string.Join(" | ", parts.Select(_ => "---")) + " |");
                }
                else
                {
                    output.Add(line);
                }
            }

            return string.Join("\n", output);
        }
    }
}
