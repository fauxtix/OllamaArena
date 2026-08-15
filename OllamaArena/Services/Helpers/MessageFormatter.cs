using Markdig;
using System.Text.RegularExpressions;

namespace OllamaArena.Services.Helpers
{
    /// <summary>
    /// Formatador de respostas do Ollama otimizado para Fluent UI
    /// Usa Markdig com pipeline completa + heurísticas
    /// </summary>
    public static partial class MessageFormatter
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

            // 0.1 Resgata fences com a tag de linguagem e/ou o fecho colados ao código
            //     na mesma linha (```csharpusing System;...;}}}` sem \n).
            content = RescueGluedFence(content);

            // 0.2 Isola código (fences ``` e spans inline `...`) para que as heurísticas
            //     abaixo não corrompam indentação/identificadores dentro de código.
            string masked = MaskCode(content, out var codeSegments);

            // 1. Detecta se precisa de formatação pesada
            bool needsHeavyFormatting = DetectNoise(masked);

            // 2. Normalizações básicas (sempre)
            masked = NormalizeBasic(masked);

            // 3. Heurísticas avançadas (quando necessário)
            if (needsHeavyFormatting)
                masked = ApplyUniversalSeparations(masked);

            // 4. Reconstrução inteligente de tabelas
            if (needsHeavyFormatting || masked.Contains("|"))
                masked = ReconstructTables(masked);

            // 4.1 Restaura o código original, intacto
            string restored = RestoreCode(masked, codeSegments);

            // 5. Conversão Markdown → HTML com pipeline completa
            var pipeline = BuildMarkdownPipeline();
            string html = Markdown.ToHtml(restored, pipeline).Trim();

            // 6. Ajustes finais para Fluent UI
            html = ConvertParagraphsToDivs(html);

            return html;
        }

        private static MarkdownPipeline BuildMarkdownPipeline()
        {
            return new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                //.UseBootstrap()
                //.UseEmojiAndSmiley()
                .UsePipeTables()
                .UseTaskLists()
                .UseAutoLinks()
                //.UseFootnotes()
                .UseDefinitionLists()
                .UseEmphasisExtras()
                .Build();
        }
    }
}
