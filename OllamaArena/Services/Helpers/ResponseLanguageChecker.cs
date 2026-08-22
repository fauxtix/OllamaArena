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

        /// <summary>Número mínimo de ocorrências da língua intrusa para considerar mistura.</summary>
        public const int OcorrenciasMinimasMistura = 4;

        /// <summary>Fração mínima do total de palavras que têm de ser da língua intrusa.</summary>
        public const double FracaoMinimaMistura = 0.08;

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

        /// <summary>Intrusões inglesas num texto português; "for" excluída por ser também palavra portuguesa ("se for necessário").</summary>
        private static readonly HashSet<string> IntrusoesEmTextoPt =
            StopwordsEn.Where(p => p != "for").ToHashSet(StringComparer.Ordinal);

        /// <summary>Intrusões portuguesas num texto inglês (sem colisões com stopwords EN).</summary>
        private static readonly HashSet<string> IntrusoesEmTextoEn = StopwordsPt.ToHashSet(StringComparer.Ordinal);

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
                string palavra = Normalizar(palavraBruta);

                if (StopwordsPt.Contains(palavra)) pontosPt++;
                else if (StopwordsEn.Contains(palavra)) pontosEn++;

                if (palavra.IndexOfAny(DiacriticosPt) >= 0) pontosPt++;
            }

            if (Math.Abs(pontosPt - pontosEn) < MargemMinima)
                return DetectedLanguage.Inconclusive;

            return pontosPt > pontosEn ? DetectedLanguage.Portuguese : DetectedLanguage.English;
        }

        /// <summary>
        /// Indica se um texto contém uma quantidade significativa de stopwords da língua
        /// oposta a <paramref name="codigoIdiomaBase"/> ("pt"/"en") — mistura de línguas,
        /// mesmo com o idioma dominante correto (ex.: frases inglesas incrustadas em
        /// português). Limiar conservador para evitar falsos positivos em textos técnicos.
        /// </summary>
        public static bool ContemMisturaSignificativa(string? texto, string? codigoIdiomaBase, out int ocorrenciasIntrusao)
        {
            ocorrenciasIntrusao = 0;
            if (string.IsNullOrWhiteSpace(texto))
                return false;

            bool basePortuguesa = !string.Equals(codigoIdiomaBase?.Trim(), "en", StringComparison.OrdinalIgnoreCase);
            var intrusoes = basePortuguesa ? IntrusoesEmTextoPt : IntrusoesEmTextoEn;

            string limpo = RegexUrl().Replace(RegexCodigo().Replace(texto, " "), " ");
            string[] palavras = limpo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (palavras.Length < PalavrasMinimas)
                return false;

            foreach (string palavraBruta in palavras)
            {
                if (intrusoes.Contains(Normalizar(palavraBruta)))
                    ocorrenciasIntrusao++;
            }

            return ocorrenciasIntrusao >= OcorrenciasMinimasMistura
                && ocorrenciasIntrusao >= palavras.Length * FracaoMinimaMistura;
        }

        private static string Normalizar(string palavraBruta) =>
            palavraBruta.Trim().ToLowerInvariant().Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')');

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
