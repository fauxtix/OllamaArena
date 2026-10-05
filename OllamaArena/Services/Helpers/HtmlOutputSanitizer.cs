using Ganss.Xss;

namespace OllamaArena.Services.Helpers
{
    /// <summary>
    /// Allow-list de HTML para conteúdo gerado por modelos (respostas do chat, textos de
    /// benchmark). A saída do LLM é conteúdo não confiável — um prompt injection pode
    /// tentar injetar <c>&lt;script&gt;</c>, <c>&lt;img onerror=...&gt;</c> ou
    /// <c>&lt;a href="javascript:..."&gt;</c>, que são depois renderizados com
    /// <c>(MarkupString)</c> e executariam no browser do utilizador.
    ///
    /// A abordagem é allow-list: nada passa sem estar explicitamente listado. Qualquer tag
    /// ou atributo fora das listas — incluindo todos os handlers <c>on*</c>, que só passariam
    /// se a tag e o atributo estivessem ambos na allow-list — é removido, preservando
    /// sempre o texto filho.
    ///
    /// A configuração é imutável e partilhada: <see cref="HtmlSanitizer"/> é thread-safe e o
    /// parsing com AngleSharp é o passo mais caro do pipeline de formatação.
    /// </summary>
    public static class HtmlOutputSanitizer
    {
        /// <summary>
        /// Tags que o pipeline de markdown pode produzir legivelmente, mais as que o
        /// <c>MessageFormatter</c> reconstrói (div, span) e as de listas de tarefas do
        /// Markdig (input type=checkbox).
        /// </summary>
        private static readonly string[] Tags =
        [
            "a", "blockquote", "br", "code", "dd", "del", "details", "div", "dl", "dt",
            "em", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "input", "ins",
            "kbd", "li", "mark", "ol", "p", "pre", "s", "samp", "span", "strong",
            "sub", "summary", "sup", "table", "tbody", "td", "tfoot", "th", "thead",
            "tr", "u", "ul", "var"
        ];

        /// <summary>
        /// Atributos admissíveis, em lista plana: a biblioteca não suporta allow-list por
        /// tag e não é preciso. Todos estes são inertes — nenhum permite executar script,
        /// carregar recursos remotos ou submeter formulários, e <c>style</c> é limitado a
        /// <see cref="AllowedCssProperties"/>.
        /// </summary>
        private static readonly string[] Attributes =
        [
            // Necessários ao markdown: language-* nos blocos de código, contains-task-list.
            "class",
            "id",
            "title",
            "dir",
            "lang",
            // Alinhamento de tabelas e marcas inline (ex.: <td style="text-align:center">).
            "style",
            // Ligações. O esquema é filtrado por AllowedSchemes.
            "href",
            "rel",
            "target",
            // Tabelas.
            "colspan",
            "rowspan",
            "align",
            // Listas ordenadas e listas de tarefas.
            "start",
            "type",
            "checked",
            "disabled"
        ];

        /// <summary>Propriedades CSS admissíveis. Nada de url(), expression() ou behaviours.</summary>
        private static readonly string[] CssProperties =
        [
            "text-align", "vertical-align", "color", "background-color",
            "font-style", "font-weight", "text-decoration"
        ];

        /// <summary>Esquemas de URI admissíveis. Relativo (sem esquema) também é aceite.</summary>
        private static readonly string[] Schemes = ["http", "https", "mailto", "tel", "ftp"];

        private static readonly HtmlSanitizer Sanitizer = CriarSanitizer();

        /// <summary>
        /// Remove de <paramref name="html"/> tudo o que não esteja na allow-list.
        /// Devolve HTML seguro para renderização direta (<c>MarkupString</c>).
        /// </summary>
        public static string Sanitize(string? html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            return Sanitizer.Sanitize(html);
        }

        private static HtmlSanitizer CriarSanitizer()
        {
            // As colecções são get-only: a configuração faz-se esvaziando e populando.
            var sanitizer = new HtmlSanitizer
            {
                // Preserva o texto interior de tags removidas em vez de as apagar por inteiro.
                KeepChildNodes = true,
                // Sem atributos data-*: podem ser lidos por scripts de terceiros na página.
                AllowDataAttributes = false
            };

            Replace(sanitizer.AllowedTags, Tags);
            Replace(sanitizer.AllowedAttributes, Attributes);
            Replace(sanitizer.AllowedCssProperties, CssProperties);
            Replace(sanitizer.AllowedSchemes, Schemes);

            // Nenhuma regra @import/@font-face/@media: permitiria carregar recursos externos.
            sanitizer.AllowedAtRules.Clear();

            return sanitizer;
        }

        private static void Replace(ISet<string> target, IReadOnlyList<string> source)
        {
            target.Clear();

            for (int i = 0; i < source.Count; i++)
                target.Add(source[i]);
        }
    }
}