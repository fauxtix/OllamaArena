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

            // --- VALIDAÇÃO 2: Corrigir falta de espaço em Cabeçalhos/Títulos Markdown ---
            content = Regex.Replace(content, @"^(#{1,6})([^\s#])", "$1 $2", RegexOptions.Multiline);

            // --- VALIDAÇÃO 3: Correção de Tabelas Markdown (Inclui reconstrução de tabelas partidas na vertical) ---
            if (content.Contains("|"))
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var reconstructedContent = new List<string>();
                var currentTableRow = new List<string>();
                bool insideBrokenTable = false;

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();

                    // Deteta se a linha faz parte de uma tabela partida: começa por '|' e NÃO tem múltiplos '|' no meio
                    if (trimmed.StartsWith("|") && trimmed.Count(c => c == '|') == 1)
                    {
                        insideBrokenTable = true;
                        string cellContent = trimmed.TrimStart('|').Trim();
                        currentTableRow.Add(cellContent);
                    }
                    else
                    {
                        // Se saímos do padrão da tabela partida, descarrega o acumulado convertendo para horizontal
                        if (insideBrokenTable && currentTableRow.Count > 0)
                        {
                            int separatorIndex = currentTableRow.FindIndex(c => c.StartsWith("-"));
                            int columnsCount = separatorIndex > 0 ? separatorIndex : 4;

                            for (int i = 0; i < currentTableRow.Count; i += columnsCount)
                            {
                                var rowCells = currentTableRow.Skip(i).Take(columnsCount).ToList();

                                // Ignora se o bloco estiver vazio ou corrompido apenas com traços
                                if (rowCells.Count == 0 || rowCells.All(c => c.StartsWith("-"))) continue;

                                reconstructedContent.Add("| " + string.Join(" | ", rowCells) + " |");

                                // Injeta a linha separadora regulamentar do Markdown logo após o cabeçalho
                                if (i == 0)
                                {
                                    var separators = Enumerable.Range(0, rowCells.Count).Select(_ => "---");
                                    reconstructedContent.Add("| " + string.Join(" | ", separators) + " |");
                                }
                            }

                            currentTableRow.Clear();
                            insideBrokenTable = false;
                        }

                        // Protege contra linhas de traços espúrias perdidas no loop
                        if (insideBrokenTable && trimmed.StartsWith("|") && trimmed.Contains("-"))
                        {
                            continue;
                        }

                        reconstructedContent.Add(line);
                    }
                }

                // Salvaguarda caso o output acabe a meio da tabela
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

                // Correções padrão adicionais para tabelas horizontais normais
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

            // --- VALIDAÇÃO 5: Corrigir falta de espaço após os dois pontos (Evitando emojis acidentais com asteriscos) ---
            // Mudamos para apenas dar espaço se NÃO for seguido de formatação markdown colada
            content = Regex.Replace(content, @":([^\s\*_])", ": $1");

            // --- VALIDAÇÃO 6: Corrigir falta de espaço pós-pontuação genérica ---
            content = Regex.Replace(content, @"([a-zA-Z])([\.!?])([A-Z])", "$1$2 $3");

            // --- VALIDAÇÃO 7: CRÍTICA PARA LISTAS NUMÉRICAS COLADAS ---
            // Deteta padrões como "text.1." ou "text:1." ou "text2." e força uma quebra de linha dupla
            // Só quebra a linha se o número da lista estiver colado a dois pontos ou colado ao fim da frase anterior
            content = Regex.Replace(content, @"([a-zA-Z:]+)(\d+\.\s+[A-Z])", "$1\n\n$2");

            // --- PROCESSAMENTO MARKDIG (Configuração Ideal Reposta) ---
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .UseBootstrap()
                .UseEmojiAndSmiley()
                .Build();

            var html = Markdown.ToHtml(content, pipeline);
            html = html.TrimEnd('\n', '\r', ' ');

            // Substituição segura de <p> por <div> sem quebrar tabelas HTML
            html = Regex.Replace(html, @"<p>(.*?)</p>", "<div>$1</div>", RegexOptions.Singleline);

            return html.Replace("<p>", "<div>").Replace("</p>", "</div>");
        }
    }
}