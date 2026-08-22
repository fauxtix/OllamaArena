using System.Text.RegularExpressions;

namespace OllamaArena.Services.Helpers
{
    /// <summary>Idioma dominante detetado num texto (heurística de stopwords).</summary>
    public enum DetectedLanguage
    {
        Portuguese,
        English,
        Inconclusive
    }

    /// <summary>
    /// Deteção local e determinística do idioma dominante de uma resposta do modelo
    /// (PT vs EN), para avisar quando a resposta não respeita o idioma selecionado na UI.
    /// Heurística simples: frequência de stopwords + diacríticos; sem dependências externas.
    /// </summary>
    public static partial class ResponseLanguageChecker
    {
        private const int PalavrasMinimas = 12;
        private const int MargemMinima = 2;

        [GeneratedRegex("```.*?```", RegexOptions.Singleline)]
        private static partial Regex RegexCodigo();

        [GeneratedRegex(@"https?://\S+")]
        private static partial Regex RegexUrl();

        private static readonly string[] StopwordsPt =
        [
            "não", "que", "de", "para", "com", "uma", "como", "mais", "isso", "este",
            "esta", "são", "dos", "das", "porque", "está", "pode", "muito", "também",
            "mas", "seu", "sua", "quando", "entre", "sobre", "será", "então", "assim"
        ];

        private static readonly string[] StopwordsEn =
        [
            "the", "and", "is", "of", "to", "you", "with", "for", "that", "this",
            "are", "from", "your", "have", "will", "can", "not", "but", "they",
            "which", "their", "there", "would", "should", "about", "into", "than"
        ];

        private static readonly char[] DiacriticosPt = ['ã', 'õ', 'ç', 'á', 'é', 'í', 'ó', 'ú', 'â', 'ê', 'ô'];

        /// <summary>
        /// Deteta o idioma dominante (PT vs EN) do texto. Blocos de código cercados e URLs
        /// são removidos antes da análise. Textos curtos, vazios ou sem vencedor claro
        /// devolvem <see cref="DetectedLanguage.Inconclusive"/>.
        /// </summary>
        public static DetectedLanguage Detect(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return DetectedLanguage.Inconclusive;

            string limpo = RegexUrl().Replace(RegexCodigo().Replace(text, " "), " ");
            string[] palavras = limpo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (palavras.Length < PalavrasMinimas)
                return DetectedLanguage.Inconclusive;

            int pontosPt = 0;
            int pontosEn = 0;

            foreach (string palavraBruta in palavras)
            {
                string palavra = palavraBruta.Trim().ToLowerInvariant().Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')');

                if (StopwordsPt.Contains(palavra)) pontosPt++;
                else if (StopwordsEn.Contains(palavra)) pontosEn++;

                if (palavra.IndexOfAny(DiacriticosPt) >= 0) pontosPt++;
            }

            if (Math.Abs(pontosPt - pontosEn) < MargemMinima)
                return DetectedLanguage.Inconclusive;

            return pontosPt > pontosEn ? DetectedLanguage.Portuguese : DetectedLanguage.English;
        }

        /// <summary>
        /// Indica se o idioma detetado conflita com o idioma esperado da sessão
        /// (<paramref name="expectedTwoLetterCode"/>: "pt"/"en"). Inconclusivo nunca gera conflito.
        /// </summary>
        public static bool IsMismatch(DetectedLanguage detected, string expectedTwoLetterCode)
        {
            var esperado = expectedTwoLetterCode?.ToLowerInvariant() switch
            {
                "pt" => DetectedLanguage.Portuguese,
                "en" => DetectedLanguage.English,
                _ => DetectedLanguage.Portuguese
            };

            return detected != DetectedLanguage.Inconclusive && detected != esperado;
        }
    }
}
