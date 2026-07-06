using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OllamaFluentUIChat.Services
{
    public static class CommonService
    {
        public static string FormatMessage(string content)
        {
            if (string.IsNullOrEmpty(content)) return "";

            if (content == "...")
            {
                return "<div class='typing-dots'><span></span><span></span><span></span></div>";
            }

            // --- VALIDAÇÃO 1: Garantir que blocos de código abertos são fechados ---
            int backtickCount = Regex.Matches(content, @"```").Count;
            if (backtickCount % 2 != 0)
            {
                content += "\n```";
            }

            // Força uma quebra de linha dupla ANTES do cardinal (#) para que o título nunca se cole ao texto anterior,
            // corrigindo o erro "OverviewThe Ming Dynasty..." que viste no anexo.
            content = Regex.Replace(content, @"([^\n])\s*(#{1,6}\s)", "$1\n\n$2");

            // --- VALIDAÇÃO 2: Corrigir falta de espaço em Cabeçalhos/Títulos Markdown ---
            content = Regex.Replace(content, @"^(#{1,6})([^\s#])", "$1 $2", RegexOptions.Multiline);

            // --- VALIDAÇÃO 3: Correção de Tabelas Markdown ---
            if (content.Contains("|"))
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var reconstructedContent = new List<string>();
                var currentTableRow = new List<string>();
                bool insideBrokenTable = false;

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();

                    if (trimmed.StartsWith("|") && trimmed.Count(c => c == '|') == 1)
                    {
                        insideBrokenTable = true;
                        string cellContent = trimmed.TrimStart('|').Trim();
                        currentTableRow.Add(cellContent);
                    }
                    else
                    {
                        if (insideBrokenTable && currentTableRow.Count > 0)
                        {
                            int separatorIndex = currentTableRow.FindIndex(c => c.StartsWith("-"));
                            int columnsCount = separatorIndex > 0 ? separatorIndex : 4;

                            for (int i = 0; i < currentTableRow.Count; i += columnsCount)
                            {
                                var rowCells = currentTableRow.Skip(i).Take(columnsCount).ToList();
                                if (rowCells.Count == 0 || rowCells.All(c => c.StartsWith("-"))) continue;

                                reconstructedContent.Add("| " + string.Join(" | ", rowCells) + " |");

                                if (i == 0)
                                {
                                    var separators = Enumerable.Range(0, rowCells.Count).Select(_ => "---");
                                    reconstructedContent.Add("| " + string.Join(" | ", separators) + " |");
                                }
                            }

                            currentTableRow.Clear();
                            insideBrokenTable = false;
                        }

                        if (insideBrokenTable && trimmed.StartsWith("|") && trimmed.Contains("-"))
                        {
                            continue;
                        }

                        reconstructedContent.Add(line);
                    }
                }

                if (currentTableRow.Count > 0)
                {
                    int separatorIndex = currentTableRow.FindIndex(c => c.StartsWith("-"));
                    int columnsCount = separatorIndex > 0 ? separatorIndex : 4;
                    for (int i = 0; i < currentTableRow.Count; i += columnsCount)
                    {
                        var rowCells = currentTableRow.Skip(i).Take(columnsCount).ToList();
                        if (rowCells.Count == 0 || rowCells.All(c => c.StartsWith("-"))) continue;

                        reconstructedContent.Add("| " + string.Join(" | ", rowCells) + " |");
                    }
                }

                content = string.Join("\n", reconstructedContent);

                if (!content.Contains("\n|"))
                {
                    content = Regex.Replace(content, @"([^\n])(\|)", "$1\n$2");
                }
                if (content.Contains("||"))
                {
                    content = content.Replace("||", "|");
                }
            }

            // --- VALIDAÇÃO 4: Injetar quebra de linha antes de marcadores de lista por asterisco ---
            content = Regex.Replace(content, @"([\.!?])\s*(\*+)", "$1\n\n$2");

            // --- VALIDAÇÃO 5: Corrigir falta de espaço após os dois pontos ---
            content = Regex.Replace(content, @":([^\s\*_])", ": $1");

            // --- VALIDAÇÃO 6: Corrigir falta de espaço pós-pontuação genérica ---
            content = Regex.Replace(content, @"([a-zA-Z])([\.!?])([A-Z])", "$1$2 $3");

            // --- VALIDAÇÃO 7: CRÍTICA PARA LISTAS NUMÉRICAS COLADAS ---
            content = Regex.Replace(content, @"([a-zA-Z:\*]+)(\d+\.\s+[A-Z])", "$1\n\n$2");

            // --- PROCESSAMENTO MARKDIG ---
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .UseBootstrap()
                .UseEmojiAndSmiley()
                .Build();

            var html = Markdown.ToHtml(content, pipeline);
            html = html.TrimEnd('\n', '\r', ' ');

            html = Regex.Replace(html, @"<p>(.*?)</p>", "<div>$1</div>", RegexOptions.Singleline);

            return html.Replace("<p>", "<div>").Replace("</p>", "</div>");
        }
        public static string FormatMessageV2(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            if (content == "...")
                return "<div class='typing-dots'><span></span><span></span><span></span></div>";

            // 1. Garantir que blocos de código ``` são fechados
            int backtickCount = Regex.Matches(content, @"```").Count;
            if (backtickCount % 2 != 0)
                content += "\n```";

            // 2. Inserir quebra de linha antes de headings colados
            content = Regex.Replace(
                content,
                @"([^\n])\s*(#{1,6}\s)",
                "$1\n\n$2"
            );

            // 3. Corrigir headings sem espaço
            content = Regex.Replace(
                content,
                @"^(#{1,6})([^\s#])",
                "$1 $2",
                RegexOptions.Multiline
            );

            // 4. Quebra de linha antes de listas por asterisco
            content = Regex.Replace(
                content,
                @"([\.!?])\s*(\*+\s)",
                "$1\n\n$2"
            );

            // 5. Quebra de linha antes de listas numéricas coladas
            content = Regex.Replace(
                content,
                @"([a-zA-Z:\*])(\d+\.\s+[A-Z0-9])",
                "$1\n\n$2"
            );

            // 6. Espaço após pontuação colada
            content = Regex.Replace(
                content,
                @"([a-zA-Z])([\.!?])([A-Z])",
                "$1$2 $3"
            );

            // 7. Espaço após dois pontos colados
            content = Regex.Replace(
                content,
                @":([^\s\*_])",
                ": $1"
            );

            // 8. NÃO reconstruir tabelas — preserva erros para avaliação
            // (modelos pequenos geram ruído, reconstruir mascara erros)

            // 9. Converter Markdown → HTML
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .Build();

            var html = Markdown.ToHtml(content, pipeline).TrimEnd('\n', '\r', ' ');

            // 10. Substituir <p> por <div> para Fluent UI
            html = Regex.Replace(html, @"<p>(.*?)</p>", "<div>$1</div>", RegexOptions.Singleline);
            html = html.Replace("<p>", "<div>").Replace("</p>", "</div>");

            return html;
        }

    }
}