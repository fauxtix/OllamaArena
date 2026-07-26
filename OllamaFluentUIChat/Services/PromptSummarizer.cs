using System.Text.RegularExpressions;

namespace OllamaFluentUIChat.Services
{

    public static class PromptSummarizer
    {
        // Stop words em Português
        private static readonly HashSet<string> PtStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "o", "as", "os", "de", "do", "da", "dos", "das", "em", "no", "na", "nos", "nas",
        "um", "uma", "uns", "umas", "por", "para", "com", "como", "que", "e", "ou", "se", "eu",
        "voce", "você", "ele", "ela", "me", "te", "lhe", "nos", "vos", "lhes", "meu", "minha",
        "seu", "sua", "nosso", "nossa", "este", "esta", "isto", "esse", "essa", "isso", "aquele",
        "aquela", "aquilo", "estou", "esta", "está", "estamos", "estao", "estão", "sou", "é",
        "somos", "sao", "são", "fazer", "criar", "gerar", "escrever", "quero", "gostaria", "preciso",
        "faça", "faca", "cria", "escreve", "obter", "dá", "da"
    };

        // Stop words em Inglês
        private static readonly HashSet<string> EnStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "if", "because", "as", "until", "while", "of", "at",
        "by", "for", "with", "about", "against", "between", "into", "through", "during", "before",
        "after", "above", "below", "to", "from", "up", "down", "in", "out", "on", "off", "over",
        "under", "again", "further", "then", "once", "here", "there", "when", "where", "why",
        "how", "all", "any", "both", "each", "few", "more", "most", "other", "some", "such",
        "no", "nor", "not", "only", "own", "same", "so", "than", "too", "very", "s", "t", "can",
        "will", "just", "don", "should", "now", "i", "you", "he", "she", "it", "we", "they",
        "my", "your", "his", "her", "its", "our", "their", "want", "need", "please", "make",
        "create", "generate", "write", "build", "give", "code", "get"
    };

        /// <summary>
        /// Resume um prompt em Português em 2 a 3 palavras-chave.
        /// </summary>
        public static string ExtractDescriptionPortuguese(string promptText, int maxWords = 3)
        {
            return ExtractKeywords(promptText, PtStopWords, maxWords);
        }

        /// <summary>
        /// Resume um prompt em Inglês em 2 a 3 palavras-chave.
        /// </summary>
        public static string ExtractDescriptionEnglish(string promptText, int maxWords = 3)
        {
            return ExtractKeywords(promptText, EnStopWords, maxWords);
        }

        /// <summary>
        /// Tenta detetar o idioma e resume o prompt em 2 a 3 palavras-chave.
        /// </summary>
        public static string ExtractDescription(string promptText, int maxWords = 3)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return "Prompt sem texto";

            // Deteta se o prompt parece ser em Inglês contando a presença de stop words comuns em inglês
            var cleanText = Regex.Replace(promptText.ToLowerInvariant(), @"[^\w\s]", " ");
            var words = cleanText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            int enMatches = words.Count(w => EnStopWords.Contains(w));
            int ptMatches = words.Count(w => PtStopWords.Contains(w));

            // Se detetar mais stop words em inglês, usa o dicionário de inglês
            var selectedStopWords = enMatches > ptMatches ? EnStopWords : PtStopWords;

            return ExtractKeywords(promptText, selectedStopWords, maxWords);
        }

        private static string ExtractKeywords(string promptText, HashSet<string> stopWords, int maxWords)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return "Empty Prompt";

            string cleanText = Regex.Replace(promptText.ToLowerInvariant(), @"[^\w\s]", " ");
            string[] words = cleanText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            var filteredWords = words
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .ToList();

            if (!filteredWords.Any())
            {
                return string.Join(" ", words.Take(maxWords).Select(Capitalize));
            }

            var wordScores = filteredWords
                .GroupBy(w => w)
                .Select(g => new { Word = g.Key, Frequency = g.Count() })
                .OrderByDescending(x => x.Frequency)
                .ThenByDescending(x => x.Word.Length)
                .Select(x => Capitalize(x.Word))
                .Take(maxWords);

            return string.Join(" ", wordScores);
        }

        private static string Capitalize(string word) =>
            char.ToUpper(word[0]) + word.Substring(1);
    }
}

