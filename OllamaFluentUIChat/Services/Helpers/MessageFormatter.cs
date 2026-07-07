using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OllamaFluentUIChat.Services.Helpers
{
    /// <summary>
    /// Formatador avançado de respostas do Ollama otimizado para Fluent UI
    /// Usa Markdig com pipeline completa + heurísticas inteligentes
    /// </summary>
    public static class MessageFormatter
    {
        public static string FormatMessagePlus(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            if (content == "...")
            {
                return "<div class='typing-dots'><span></span><span></span><span></span></div>";
            }

            // 0. Limpeza inicial
            content = RemoveInternalReasoning(content);

            // 1. Detecta se precisa de formatação pesada
            bool needsHeavyFormatting = DetectNoise(content);

            // 2. Normalizações básicas (sempre)
            content = NormalizeBasic(content);

            // 3. Heurísticas avançadas (quando necessário)
            if (needsHeavyFormatting)
                content = ApplyUniversalSeparations(content);

            // 4. Reconstrução inteligente de tabelas
            if (needsHeavyFormatting || content.Contains("|"))
                content = ReconstructTables(content);

            // 5. Conversão Markdown → HTML com pipeline completa
            var pipeline = BuildMarkdownPipeline();
            string html = Markdown.ToHtml(content, pipeline).Trim();

            // 6. Ajustes finais para Fluent UI
            html = ConvertParagraphsToDivs(html);

            return html;
        }

        #region Private Methods

        private static string RemoveInternalReasoning(string content)
        {
            // Remove <think> de modelos como DeepSeek, Phi, Qwen, etc.
            return Regex.Replace(content, @"<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        }

        private static bool DetectNoise(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            if (!content.Contains("\n") && content.Length > 180)
                return true;

            return Regex.IsMatch(content, @"\|[^\s]") ||
                   Regex.IsMatch(content, @"\d+%[A-Z]") ||
                   Regex.IsMatch(content, @"[a-z][A-Z]{2,}") ||
                   Regex.IsMatch(content, @"\d+\.[A-Za-z]") ||
                   Regex.IsMatch(content, @"[A-Za-z]+\s+\d+%") ||
                   content.Contains("||") ||
                   Regex.IsMatch(content, @"^\s*\|", RegexOptions.Multiline);
        }

        private static string NormalizeBasic(string content)
        {
            // Fecha blocos de código
            int backtickCount = Regex.Matches(content, @"```").Count;
            if (backtickCount % 2 != 0)
                content += "\n```";

            // Quebra antes de headings
            content = Regex.Replace(content, @"([^\n])\s*(#{1,6}\s)", "$1\n\n$2");

            // Corrige #Título → # Título
            content = Regex.Replace(content, @"^(#{1,6})([^\s#])", "$1 $2", RegexOptions.Multiline);

            // Quebras antes de listas
            content = Regex.Replace(content, @"([\.!?])\s*(\*+\s)", "$1\n\n$2");
            content = Regex.Replace(content, @"([a-zA-Z:\*])(\d+\.\s+[A-Z0-9])", "$1\n\n$2");

            return content;
        }

        private static string ApplyUniversalSeparations(string content)
        {
            content = Regex.Replace(content, @"([a-z])([A-Z])", "$1\n$2");
            content = Regex.Replace(content, @"(%)([A-Z])", "$1\n$2");
            content = Regex.Replace(content, @":([A-Z])", ":\n$1");
            content = Regex.Replace(content, @"([A-Za-z])(\d+%)", "$1\n$2");
            content = Regex.Replace(content, @"([a-z])([A-Z][a-z]+)", "$1\n$2");

            return content;
        }

        private static string ReconstructTables(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new List<string>();
            var currentTableRow = new List<string>();
            bool insideBrokenTable = false;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                // Tabelas extremamente quebradas (uma | por linha)
                if (trimmed.StartsWith("|") && trimmed.Count(c => c == '|') <= 1 && trimmed.Length > 2)
                {
                    insideBrokenTable = true;
                    string cell = trimmed.TrimStart('|').Trim();
                    if (!string.IsNullOrEmpty(cell))
                        currentTableRow.Add(cell);
                    continue;
                }

                if (insideBrokenTable && currentTableRow.Count > 0 &&
                    (!trimmed.StartsWith("|") || trimmed.Count(c => c == '|') > 1))
                {
                    result.AddRange(BuildTableRows(currentTableRow));
                    currentTableRow.Clear();
                    insideBrokenTable = false;
                }

                // Tabelas normais
                if (trimmed.Contains("|") && trimmed.Count(c => c == '|') >= 3)
                {
                    var parts = trimmed.Split('|')
                                       .Select(p => p.Trim())
                                       .Where(p => !string.IsNullOrWhiteSpace(p))
                                       .ToList();

                    if (parts.Count >= 2)
                    {
                        result.Add("| " + string.Join(" | ", parts) + " |");

                        if (!trimmed.Contains("---") && !result.Any(r => r.Contains("---")))
                        {
                            var separators = string.Join(" | ", Enumerable.Repeat("---", parts.Count));
                            result.Add("| " + separators + " |");
                        }
                        continue;
                    }
                }

                result.Add(line);
            }

            if (currentTableRow.Count > 0)
                result.AddRange(BuildTableRows(currentTableRow));

            return string.Join("\n", result);
        }

        private static List<string> BuildTableRows(List<string> rows)
        {
            if (rows.Count == 0) return new List<string>();

            int columnsCount = EstimateColumnCount(rows);
            var output = new List<string>();

            for (int i = 0; i < rows.Count; i += columnsCount)
            {
                var cells = rows.Skip(i).Take(columnsCount).ToList();
                if (cells.Count == 0 || cells.All(c => c.StartsWith("-"))) continue;

                output.Add("| " + string.Join(" | ", cells) + " |");

                if (i == 0 && !output.Any(x => x.Contains("---")))
                {
                    var separators = Enumerable.Repeat("---", cells.Count);
                    output.Add("| " + string.Join(" | ", separators) + " |");
                }
            }
            return output;
        }

        private static int EstimateColumnCount(List<string> rows)
        {
            var separatorIndex = rows.FindIndex(r => r.StartsWith("-"));
            return separatorIndex > 0 ? separatorIndex : Math.Max(3, (int)Math.Ceiling(rows.Count / 3.0));
        }

        private static MarkdownPipeline BuildMarkdownPipeline()
        {
            return new MarkdownPipelineBuilder()
                .UseAdvancedExtensions() 
                .UseSoftlineBreakAsHardlineBreak()
                .UseBootstrap()
                .UseEmojiAndSmiley()
                .UsePipeTables()
                .UseTaskLists() 
                .UseAutoLinks()
                .UseFootnotes()
                .UseDefinitionLists()
                .UseEmphasisExtras()
                .Build();
        }

        private static string ConvertParagraphsToDivs(string html)
        {
            html = Regex.Replace(html, @"<p>(.*?)</p>", "<div>$1</div>", RegexOptions.Singleline);
            html = html.Replace("<p>", "<div>").Replace("</p>", "</div>");
            return html.Trim();
        }

        #endregion
    }
}