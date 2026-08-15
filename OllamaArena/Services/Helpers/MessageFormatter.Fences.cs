using System.Text.RegularExpressions;

namespace OllamaArena.Services.Helpers
{
    // Resgate de fences ``` com a tag de linguagem e/ou o fecho colados ao código
    // na mesma linha (padrão de modelos pequenos como Gemma3/Qwen3/granite).
    public static partial class MessageFormatter
    {
        private static readonly string[] KnownCodeTags =
        {
            "csharp", "c#", "cs", "dotnet", "cplusplus", "cpp", "objective-c",
            "java", "kotlin", "scala", "javascript", "js", "jsx", "typescript", "ts", "tsx",
            "python", "py", "ruby", "rb", "php", "go", "golang", "rust", "rs", "swift", "dart",
            "haskell", "hs", "sql", "json", "xml", "html", "htm", "css", "bash", "sh", "shell",
            "powershell", "ps1", "yaml", "yml", "toml", "ini", "dockerfile", "makefile",
            "text", "plaintext", "markdown", "md"
        };

        private static string RescueGluedFence(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            // Resgata fences ``` com a tag de linguagem e/ou o fecho colados ao código na MESMA
            // linha (padrão de modelos pequenos: ```` ```csharpusing System;...;}}} ```` sem \n).
            // Fences bem-formadas ficam intocadas: só age quando a tag está colada ao corpo
            // ou quando há código real junto de um fecho ``` na mesma linha.
            return Regex.Replace(content,
                @"^(?<indent>[ \t]*)(?<fence>`{3,})(?<rest>[^\n]*)$",
                m =>
                {
                    string indent = m.Groups["indent"].Value;
                    string fence = m.Groups["fence"].Value;
                    string rest = m.Groups["rest"].Value;

                    if (string.IsNullOrWhiteSpace(rest))
                        return m.Value;

                    var (tag, body) = SplitKnownTag(rest);
                    bool glued = tag.Length > 0 && LooksLikeCodeStart(body);

                    if (tag.Length == 0)
                    {
                        // Sem tag conhecida: preserva o prefixo alfanumérico como info string.
                        int infoEnd = Regex.Match(rest, "^[A-Za-z0-9_+\\-.]*").Length;
                        tag = rest.Substring(0, infoEnd);
                        body = rest.Substring(infoEnd);
                    }

                    int closerIndex = body.LastIndexOf("```", StringComparison.Ordinal);
                    bool hasCloser = closerIndex >= 0;
                    if (!glued && !hasCloser)
                        return m.Value;

                    // Fecho ``` colado na mesma linha → move para linha própria.
                    string closer = "";
                    if (hasCloser)
                    {
                        closer = body.Substring(closerIndex);
                        body = body.Substring(0, closerIndex);
                    }

                    body = body.Trim();

                    // Alguns modelos repetem a tag de linguagem antes do fecho
                    // (`... } } csharp``` `) — remove-a para não sobrar resíduo no código.
                    body = StripTrailingTag(body);

                    if (body.Length == 0)
                        return m.Value;

                    // Corpo multilinha (tag colada apenas): conserva as linhas existentes.
                    // Corpo de linha única (padrão Gemma3): quebra instruções + indenta.
                    // Python tem regras próprias (indentação por ':', comentários #).
                    string formatted = body.Contains('\n')
                        ? body
                        : IsPythonTag(tag) ? BreakSingleLinePython(body) : BreakSingleLineCode(body);

                    string result = indent + fence + tag + "\n" + formatted;
                    if (closer.Length > 0)
                        result += "\n" + closer;
                    return result;
                },
                RegexOptions.Multiline);
        }

        private static (string Tag, string Body) SplitKnownTag(string text)
        {
            string best = "";
            foreach (var tag in KnownCodeTags)
            {
                if (text.StartsWith(tag, StringComparison.OrdinalIgnoreCase) && tag.Length > best.Length)
                    best = tag;
            }
            return best.Length == 0 ? ("", text) : (best, text.Substring(best.Length));
        }

        private static string StripTrailingTag(string body)
        {
            if (string.IsNullOrEmpty(body))
                return body;

            foreach (var tag in KnownCodeTags)
            {
                if (body.Length > tag.Length && body.EndsWith(tag, StringComparison.OrdinalIgnoreCase))
                {
                    // Só remove quando é uma palavra inteira precedida de espaço ou
                    // pontuação de fim de código — não toca em identificadores (mycsharp).
                    char before = body[body.Length - tag.Length - 1];
                    if (char.IsWhiteSpace(before) || before == ';' || before == '}' || before == ')' || before == ']')
                        return body.Substring(0, body.Length - tag.Length).TrimEnd();
                }
            }
            return body;
        }

        private static bool LooksLikeCodeStart(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            char c = text[0];
            if (c == '/' || c == '#' || c == '{' || c == '}' || c == ';')
                return true;
            foreach (var keyword in DeclarationKeywords)
            {
                if (text.StartsWith(keyword, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
