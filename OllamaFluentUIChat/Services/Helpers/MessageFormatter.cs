using Markdig;
using System.Text.RegularExpressions;

namespace OllamaFluentUIChat.Services.Helpers
{
    /// <summary>
    /// Formatador de respostas do Ollama otimizado para Fluent UI
    /// Usa Markdig com pipeline completa + heurísticas
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
            var buffer = new List<string>();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                // Se contém pipes → é potencial célula
                if (trimmed.Contains("|"))
                {
                    buffer.Add(trimmed);
                    continue;
                }

                // Se não contém pipes → fechar tabela se existir
                if (buffer.Count > 0)
                {
                    result.AddRange(RebuildTableFromLines(buffer));
                    buffer.Clear();
                }

                result.Add(line);
            }

            // Fechar tabela no fim
            if (buffer.Count > 0)
                result.AddRange(RebuildTableFromLines(buffer));

            return string.Join("\n", result);
        }
        private static List<string> BuildTableRows(List<string> rows)
        {
            var output = new List<string>();
            if (rows.Count == 0) return output;

            int columns = EstimateColumnCount(rows);

            // Reconstrução tolerante
            for (int i = 0; i < rows.Count; i += columns)
            {
                var cells = rows.Skip(i).Take(columns).ToList();

                // Se a linha vier incompleta, preencher com vazio
                while (cells.Count < columns)
                    cells.Add("");

                output.Add("| " + string.Join(" | ", cells) + " |");

                // Criar separador apenas na primeira linha
                if (i == 0)
                {
                    var sep = Enumerable.Repeat("---", columns);
                    output.Add("| " + string.Join(" | ", sep) + " |");
                }
            }

            return output;
        }

        private static int EstimateColumnCount(List<string> rows)
        {
            // 1. Se houver linhas com múltiplos pipes, usar o maior número encontrado
            int maxPipes = 0;

            foreach (var r in rows)
            {
                int pipes = r.Count(c => c == '|');
                if (pipes > maxPipes)
                    maxPipes = pipes;
            }

            // maxPipes - 1 = número de colunas
            if (maxPipes >= 2)
                return maxPipes - 1;

            // 2. Se não houver pipes, tentar inferir pelo tamanho médio das células
            if (rows.Count > 0)
            {
                double avg = rows.Average(r => r.Length);

                if (avg < 12) return 5;   // células curtas → mais colunas
                if (avg < 20) return 4;
                if (avg < 35) return 3;
                return 2;
            }

            return 2;
        }

        private static List<string> RebuildTableFromLines(List<string> rawLines)
        {
            var cells = new List<string>();

            foreach (var line in rawLines)
            {
                var parts = line.Split('|')
                                .Select(p => p.Trim())
                                .Where(p => !string.IsNullOrWhiteSpace(p))
                                .ToList();

                cells.AddRange(parts);
            }

            return BuildTableRows(cells);
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