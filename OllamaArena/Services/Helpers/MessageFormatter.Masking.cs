using System.Text;
using System.Text.RegularExpressions;

namespace OllamaArena.Services.Helpers
{
    // Isolamento/restauração de código (fences e spans inline) durante as heurísticas,
    // mais o ajuste final dos parágrafos para divs no HTML do Fluent UI.
    public static partial class MessageFormatter
    {
        private static string MaskCode(string content, out List<string> segments)
        {
            segments = new List<string>();
            if (string.IsNullOrEmpty(content))
                return content;

            // 1. Blocos de código (fences ``` com ou sem tag de linguagem).
            //    Só são mascarados pares completos; uma fence desbalanceada permanece
            //    visível para o fallback do NormalizeBasic fechá-la no final.
            var fenceMatches = Regex.Matches(content, "`{3,}").Cast<Match>().ToList();
            int pairs = fenceMatches.Count / 2;

            var fenceRanges = new List<(int Start, int End)>();
            for (int i = 0; i < pairs; i++)
            {
                int open = fenceMatches[i * 2].Index;
                int close = fenceMatches[i * 2 + 1].Index + fenceMatches[i * 2 + 1].Length;
                fenceRanges.Add((open, close));
            }

            string semFences = ReplaceRanges(content, fenceRanges, segments);

            // 2. Código inline (`código` ou ``código``)
            return ReplaceRanges(semFences, FindInlineSpans(semFences), segments);
        }

        private static string RestoreCode(string masked, List<string> segments)
        {
            if (segments.Count == 0)
                return masked;

            return Regex.Replace(masked, "\x02CODE(\\d+)\x03",
                m => segments[int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)]);
        }

        private static string ReplaceRanges(string content, List<(int Start, int End)> ranges, List<string> segments)
        {
            if (ranges.Count == 0)
                return content;

            var builder = new StringBuilder(content.Length);
            int cursor = 0;

            foreach (var (start, end) in ranges)
            {
                if (start < cursor)
                    continue;

                builder.Append(content, cursor, start - cursor);
                builder.Append("\x02CODE").Append(segments.Count).Append("\x03");
                segments.Add(content.Substring(start, end - start));
                cursor = end;
            }

            builder.Append(content, cursor, content.Length - cursor);
            return builder.ToString();
        }

        private static List<(int Start, int End)> FindInlineSpans(string text)
        {
            var spans = new List<(int Start, int End)>();
            int i = 0;
            int n = text.Length;

            while (i < n)
            {
                if (text[i] != '`')
                {
                    i++;
                    continue;
                }

                int open = i;
                int runLen = 0;
                while (i < n && text[i] == '`')
                {
                    i++;
                    runLen++;
                }

                if (runLen >= 3)
                    continue; // resíduo de fence desbalanceada

                // procura o fecho com a mesma quantidade de crases, sem cruzar nova linha
                int j = i;
                while (j < n)
                {
                    if (text[j] == '\r' || text[j] == '\n')
                        break;

                    if (text[j] == '`')
                    {
                        int closeRun = 0;
                        while (j < n && text[j] == '`')
                        {
                            j++;
                            closeRun++;
                        }

                        if (closeRun == runLen)
                        {
                            spans.Add((open, j));
                            i = j;
                            break;
                        }

                        continue;
                    }

                    j++;
                }
            }

            return spans;
        }

        private static string ConvertParagraphsToDivs(string html)
        {
            html = Regex.Replace(html, @"<p>(.*?)</p>", "<div>$1</div>", RegexOptions.Singleline);
            html = html.Replace("<p>", "<div>").Replace("</p>", "</div>");
            return html.Trim();
        }
    }
}
