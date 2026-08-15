using System.Text;

namespace OllamaArena.Services.Helpers
{
    // Quebra de código de linha única colado numa fence (sem \n): C#/JS genérico
    // (BreakSingleLineCode) e modo Python dedicado (BreakSingleLinePython).
    public static partial class MessageFormatter
    {
        // Palavras-chave estruturais que iniciam declarações — usadas para descobrir onde um
        // comentário // (numa linha única colada) termina e o código recomeça.
        private static readonly string[] DeclarationKeywords =
        {
            "public", "private", "protected", "internal", "static", "class", "interface",
            "enum", "struct", "record", "namespace", "using", "return", "var", "new", "void",
            // Nota: "int" é excluído de propósito — é um sufixo comum em palavras de prosa
            // (print, point) e partia comentários colados indevidamente.
            "string", "byte", "bool", "char", "long", "double", "float", "decimal",
            "const", "readonly", "async", "await", "try", "catch", "finally", "throw",
            "foreach", "override", "virtual", "abstract", "sealed",
            // Palavras genéricas de baixa colisão com prosa — ajudam a resgatar código
            // colado de JS/Python/TS sem partir comentários de texto comum.
            "function", "import", "export", "def"
        };

        // Palavras que iniciam statements em Python — usadas para resgatar código colado
        // numa única linha. 'in/not/and/or/is' ficam de fora de propósito: aparecem a toda
        // hora no meio de expressões e partiriam código bem-formado.
        private static readonly string[] PythonKeywords =
        {
            "import", "from", "def", "class", "if", "elif", "else", "for", "while",
            "return", "yield", "try", "except", "finally", "with", "pass", "break",
            "continue", "raise", "assert", "async", "await", "del", "global", "nonlocal",
            "print", "lambda"
        };

        private static string BreakSingleLineCode(string code)
        {
            var builder = new StringBuilder(code.Length + 64);
            int parenDepth = 0;
            int blockDepth = 0;
            bool inString = false;
            bool inChar = false;
            bool inComment = false;
            bool isDocComment = false;
            bool inBlockComment = false;

            for (int i = 0; i < code.Length; i++)
            {
                char ch = code[i];

                if (inString)
                {
                    builder.Append(ch);
                    if (ch == '\\' && i + 1 < code.Length)
                    {
                        builder.Append(code[i + 1]);
                        i++;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (inChar)
                {
                    builder.Append(ch);
                    if (ch == '\\' && i + 1 < code.Length)
                    {
                        builder.Append(code[i + 1]);
                        i++;
                    }
                    else if (ch == '\'')
                    {
                        inChar = false;
                    }
                    continue;
                }

                if (inComment)
                {
                    // Comentário numa linha única: termina ao encontrar palavra-chave
                    // estrutural (o código "colado" após o comentário), respeitando as
                    // regras de CanBreakCommentKeyword (doc /// e palavras embutidas).
                    string? keyword = MatchKeyword(code, i);
                    if (keyword != null && CanBreakCommentKeyword(code, i, keyword.Length, isDocComment))
                    {
                        inComment = false;
                        AppendLineBreak(builder, blockDepth);
                        builder.Append(keyword);
                        i += keyword.Length - 1;
                    }
                    else
                    {
                        builder.Append(ch);
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    // Comentário em bloco /* */: emite literalmente sem quebrar em ; { } —
                    // o texto interno é documentação, não código.
                    builder.Append(ch);
                    if (ch == '*' && i + 1 < code.Length && code[i + 1] == '/')
                    {
                        builder.Append('/');
                        i++;
                        inBlockComment = false;
                        // Código colado após o fecho (/* docs */public ...) recomeça noutra linha.
                        if (i + 1 < code.Length)
                            AppendLineBreak(builder, blockDepth);
                    }
                    continue;
                }

                // Código normal.
                if (ch == '"')
                {
                    inString = true;
                    builder.Append(ch);
                    continue;
                }
                if (ch == '\'')
                {
                    inChar = true;
                    builder.Append(ch);
                    continue;
                }
                if (ch == '/' && i + 1 < code.Length && code[i + 1] == '*')
                {
                    inBlockComment = true;
                    builder.Append("/*");
                    i++;
                    continue;
                }
                if (ch == '/' && i + 1 < code.Length && code[i + 1] == '/')
                {
                    inComment = true;
                    isDocComment = i + 2 < code.Length && code[i + 2] == '/';
                    AppendLineBreak(builder, blockDepth);
                    builder.Append("//");
                    i++;
                    continue;
                }

                if (ch == '(' || ch == '[')
                {
                    parenDepth++;
                    builder.Append(ch);
                    continue;
                }
                if (ch == ')' || ch == ']')
                {
                    if (parenDepth > 0)
                        parenDepth--;
                    builder.Append(ch);
                    continue;
                }

                if (parenDepth == 0)
                {
                    if (ch == '{')
                    {
                        blockDepth++;
                        builder.Append(ch);
                        AppendLineBreak(builder, blockDepth);
                        continue;
                    }
                    if (ch == '}')
                    {
                        if (blockDepth > 0)
                            blockDepth--;
                        builder.Append(ch);
                        AppendLineBreak(builder, blockDepth);
                        continue;
                    }
                    if (ch == ';')
                    {
                        builder.Append(ch);
                        AppendLineBreak(builder, blockDepth);
                        continue;
                    }
                }

                builder.Append(ch);
            }

            return CleanupBreaks(builder.ToString());
        }

        private static string? MatchKeyword(string code, int index)
        {
            string? best = null;
            foreach (var keyword in DeclarationKeywords)
            {
                if (index + keyword.Length <= code.Length &&
                    string.Compare(code, index, keyword, 0, keyword.Length, StringComparison.Ordinal) == 0 &&
                    (best == null || keyword.Length > best.Length))
                {
                    best = keyword;
                }
            }
            return best;
        }

        private static bool CanBreakCommentKeyword(string code, int index, int keywordLength, bool isDocComment)
        {
            // Comentários XML doc (///): o código colado só recomeça depois de um fecho `>`
            // de tag (`</returns>public string...`). Palavras como "return" em "<returns>"
            // ou "string." no texto documental não devem partir o comentário.
            if (isDocComment)
                return index > 0 && code[index - 1] == '>';

            // Comentários // normais: quebra apenas quando a palavra-chave não faz parte de
            // uma palavra maior (print → int; bytes → byte). Código colado como
            // "servicepublic" (palavra-chave seguida de espaço) continua a quebrar.
            int after = index + keywordLength;
            return after >= code.Length || !char.IsLetterOrDigit(code[after]);
        }

        private static void AppendLineBreak(StringBuilder builder, int blockDepth)
        {
            if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
                builder.Append('\n');
            builder.Append(' ', blockDepth * 4);
        }

        private static string CleanupBreaks(string text)
        {
            var lines = text.Split('\n')
                .Select(line => line.TrimEnd())
                .Where(line => line.Length > 0);
            return string.Join("\n", lines);
        }

        private static bool IsPythonTag(string tag)
        {
            return string.Equals(tag, "python", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tag, "py", StringComparison.OrdinalIgnoreCase);
        }

        private static string? MatchPythonKeyword(string code, int index)
        {
            string? best = null;
            foreach (var keyword in PythonKeywords)
            {
                if (index + keyword.Length <= code.Length &&
                    string.Compare(code, index, keyword, 0, keyword.Length, StringComparison.Ordinal) == 0 &&
                    (best == null || keyword.Length > best.Length))
                {
                    best = keyword;
                }
            }
            return best;
        }

        private static string BreakSingleLinePython(string code)
        {
            var builder = new StringBuilder(code.Length + 64);
            int braceDepth = 0;      // { } — dicionários/sets: o ':' lá dentro não abre bloco.
            int bracketDepth = 0;    // [ ] — slices: o ':' lá dentro não abre bloco.
            char stringQuote = '\0'; // string simples " ou '
            bool inTriple = false;
            char tripleChar = '\0';  // docstrings """ ou '''
            bool inComment = false;  // comentário # (numa linha única, termina em keyword)
            char prevSignificant = '\0';

            for (int i = 0; i < code.Length; i++)
            {
                char ch = code[i];

                if (inTriple)
                {
                    builder.Append(ch);
                    if (ch == tripleChar && i + 2 < code.Length && code[i + 1] == tripleChar && code[i + 2] == tripleChar)
                    {
                        builder.Append(tripleChar).Append(tripleChar);
                        i += 2;
                        inTriple = false;
                    }
                    else if (!char.IsWhiteSpace(ch))
                    {
                        prevSignificant = ch;
                    }
                    continue;
                }

                if (stringQuote != '\0')
                {
                    builder.Append(ch);
                    if (ch == '\\' && i + 1 < code.Length)
                    {
                        builder.Append(code[i + 1]);
                        i++;
                    }
                    else if (ch == stringQuote)
                    {
                        stringQuote = '\0';
                    }
                    prevSignificant = ch;
                    continue;
                }

                if (inComment)
                {
                    // Comentário # numa linha única: termina na próxima palavra-chave de
                    // statement (regra igual ao // não-doc — não pode estar embutida).
                    string? keyword = MatchPythonKeyword(code, i);
                    if (keyword != null &&
                        (i + keyword.Length >= code.Length || !char.IsLetterOrDigit(code[i + keyword.Length])))
                    {
                        inComment = false;
                        AppendLineBreak(builder, 0);
                        builder.Append(keyword);
                        i += keyword.Length - 1;
                        prevSignificant = keyword[keyword.Length - 1];
                    }
                    else
                    {
                        builder.Append(ch);
                        prevSignificant = ch;
                    }
                    continue;
                }

                // Código normal (Python).
                if ((ch == '"' || ch == '\'') && i + 2 < code.Length && code[i + 1] == ch && code[i + 2] == ch)
                {
                    inTriple = true;
                    tripleChar = ch;
                    builder.Append(ch).Append(ch).Append(ch);
                    i += 2;
                    prevSignificant = ch;
                    continue;
                }
                if (ch == '"' || ch == '\'')
                {
                    stringQuote = ch;
                    builder.Append(ch);
                    prevSignificant = ch;
                    continue;
                }
                if (ch == '#')
                {
                    inComment = true;
                    AppendLineBreak(builder, 0);
                    builder.Append('#');
                    prevSignificant = '#';
                    continue;
                }
                if (ch == '{')
                {
                    braceDepth++;
                    builder.Append(ch);
                    prevSignificant = ch;
                    continue;
                }
                if (ch == '}')
                {
                    if (braceDepth > 0)
                        braceDepth--;
                    builder.Append(ch);
                    prevSignificant = ch;
                    continue;
                }
                if (ch == '[')
                {
                    bracketDepth++;
                    builder.Append(ch);
                    prevSignificant = ch;
                    continue;
                }
                if (ch == ']')
                {
                    if (bracketDepth > 0)
                        bracketDepth--;
                    builder.Append(ch);
                    prevSignificant = ch;
                    continue;
                }

                if (char.IsLetter(ch) || ch == '_')
                {
                    // Statement a começar por keyword (início, após ':', ')' ou ']').
                    // 'foo() if x' parte — aceitável num resgate; a leitura continua boa.
                    string? keyword = MatchPythonKeyword(code, i);
                    bool startsStatement = prevSignificant == '\0' || prevSignificant == ':' ||
                                           prevSignificant == ')' || prevSignificant == ']';
                    if (keyword != null &&
                        (i + keyword.Length >= code.Length || !char.IsLetterOrDigit(code[i + keyword.Length])) &&
                        startsStatement)
                    {
                        AppendLineBreak(builder, 0);
                        builder.Append(keyword);
                        i += keyword.Length - 1;
                        prevSignificant = keyword[keyword.Length - 1];
                        continue;
                    }

                    // Novo statement após um cabeçalho de bloco que termina em ':' — mas
                    // não dentro de dict/set/slice (`{a: b}`, `x[1:]`).
                    if (prevSignificant == ':' && braceDepth == 0 && bracketDepth == 0)
                        AppendLineBreak(builder, 0);

                    builder.Append(ch);
                    prevSignificant = ch;
                    continue;
                }

                builder.Append(ch);
                if (!char.IsWhiteSpace(ch))
                    prevSignificant = ch;
            }

            return ApplyPythonIndent(CleanupBreaks(builder.ToString()));
        }

        private static string ApplyPythonIndent(string text)
        {
            var result = new StringBuilder(text.Length + 64);
            int level = 0;
            foreach (var rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith('#'))
                {
                    result.Append(' ', level * 4).Append(line).Append('\n');
                    continue;
                }

                int wordEnd = line.IndexOfAny(new[] { ' ', '(', ':', '=', '\t' });
                string firstWord = wordEnd > 0 ? line.Substring(0, wordEnd) : line;
                if (firstWord == "else" || firstWord == "elif" || firstWord == "except" || firstWord == "finally")
                    level = Math.Max(0, level - 1);

                result.Append(' ', level * 4).Append(line).Append('\n');
                if (line.EndsWith(':'))
                    level++;
            }
            return result.ToString().TrimEnd();
        }
    }
}
