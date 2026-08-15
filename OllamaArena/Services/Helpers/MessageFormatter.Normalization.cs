using System.Text.RegularExpressions;

namespace OllamaArena.Services.Helpers
{
    // Heurísticas de deteção de ruído e normalizações básicas do markdown.
    public static partial class MessageFormatter
    {
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

            // ==================== IMPROVED LIST BREAKING ====================

            // 1. Numbered lists (your main issue)
            content = Regex.Replace(content,
                @"([^\n])(\s*[:;.!?])\s*(\d+\.\s)",
                "$1$2\n\n$3", RegexOptions.Multiline);

            content = Regex.Replace(content,
                @"([a-zA-Z0-9\)])\s*(\d+\.\s)",
                "$1\n\n$2", RegexOptions.Multiline);

            // 2. Bullet lists (* - +) — This was missing
            content = Regex.Replace(content,
                @"([^\n])(\s*[:;.!?])\s*([*\-+](\s|\s\*\*|\s\*\*\*))",
                "$1$2\n\n$3", RegexOptions.Multiline);

            content = Regex.Replace(content,
                @"([a-zA-Z0-9\)])\s*([*\-+](\s|\s\*\*|\s\*\*\*))",
                "$1\n\n$2", RegexOptions.Multiline);

            // 3. Clean spacing before bold/italic after any list marker
            //    (restrito ao início de linha: `***x**` → `* **x**`; não toca em `**x**` no meio do texto)
            content = Regex.Replace(content,
                @"^([ \t]*)([*\-+]|\d+\.)[ \t]*(\*\*\*|\*\*|\*)",
                "$1$2 $3", RegexOptions.Multiline);

            // 4. Original fallback (kept)
            content = Regex.Replace(content, @"([\.!?])\s*(\*+\s)", "$1\n\n$2");

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
    }
}
