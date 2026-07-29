using System.Globalization;
using System.Text.RegularExpressions;

namespace OllamaFluentUIChat.Services
{
    public static class PromptSummarizer
    {
        private static readonly HashSet<string> PtStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a","o","as","os","um","uma","uns","umas","de","do","da","dos","das","em","no","na","nos","nas",
            "por","para","com","sem","sobre","entre","até","após","que","qual","quais","quando","onde","como",
            "porque","porquê","e","ou","mas","se","então","também","já","ainda","só","apenas","eu","tu","você",
            "voce","ele","ela","nós","vos","eles","elas","me","te","se","lhe","nos","vos","lhes","meu","minha",
            "meus","minhas","teu","tua","seu","sua","seus","suas","nosso","nossa","nossos","nossas","este","esta",
            "estes","estas","esse","essa","esses","essas","isto","isso","aquele","aquela","aqueles","aquelas",
            "aquilo","ser","estar","ter","haver","fazer","poder","dever","ir","vir","sou","és","é","somos","são",
            "estou","está","estamos","estão","tenho","tem","temos","têm","foi","era","será","não","sim","mais",
            "menos","muito","pouco","todo","toda","todos","todas","quero","gostaria","preciso","podes","pode",
            "faça","faca","cria","escreve","obter","dá","da","explicar","explica","ajuda","criar","gerar","escrever"
        };

        private static readonly HashSet<string> EnStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a","an","the","and","or","but","if","because","as","until","while","of","at","by","for","with",
            "about","against","between","into","through","during","before","after","above","below","to","from",
            "up","down","in","out","on","off","over","under","again","further","then","once","here","there",
            "when","where","why","how","all","any","both","each","few","more","most","other","some","such",
            "no","nor","not","only","own","same","so","than","too","very","s","t","can","will","just","don",
            "should","now","i","me","my","myself","we","our","ours","you","your","yours","he","him","his",
            "she","her","hers","it","its","they","them","their","what","which","who","whom","this","that",
            "these","those","am","is","are","was","were","be","been","being","have","has","had","do","does",
            "did","doing","would","could","should","ought","want","need","please","make","create","generate",
            "write","build","give","code","get","help","explain","main","reasons","why","still","been","used"
        };

        // Palavras que normalmente não devem aparecer no título (mesmo não sendo stop words clássicas)
        private static readonly HashSet<string> WeakWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "has","have","had","was","were","been","being","very","extremely","quickly","still","more","than",
            "under","year","years","approach","worked","effective","practical","scientific","biological",
            "developed","authorized","studied","explain","main","reasons","why","not","for","the","and"
        };

        public static string ExtractDescriptionPortuguese(string promptText, int maxWords = 3)
            => ExtractKeywords(promptText, PtStopWords, maxWords);

        public static string ExtractDescriptionEnglish(string promptText, int maxWords = 3)
            => ExtractKeywords(promptText, EnStopWords, maxWords);

        public static string ExtractDescription(string promptText, int maxWords = 3)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return "Prompt sem texto";

            var cleanText = Regex.Replace(promptText.ToLowerInvariant(), @"[^\w\sàáâãäéêíóôõöúçñ\-]", " ");
            var words = cleanText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            int enMatches = words.Count(w => EnStopWords.Contains(w));
            int ptMatches = words.Count(w => PtStopWords.Contains(w));

            var selectedStopWords = (words.Length < 6)
                ? (ptMatches >= 1 ? PtStopWords : EnStopWords)
                : (enMatches > ptMatches ? EnStopWords : PtStopWords);

            return ExtractKeywords(promptText, selectedStopWords, maxWords);
        }

        private static string ExtractKeywords(string promptText, HashSet<string> stopWords, int maxWords)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return "Empty Prompt";

            string cleanText = Regex.Replace(promptText.ToLowerInvariant(), @"[^\w\sàáâãäéêíóôõöúçñ\-]", " ");
            string[] words = cleanText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            var filteredWords = words
                .Select((w, index) => new { Word = w, Index = index })
                .Where(x => x.Word.Length > 2
                            && !stopWords.Contains(x.Word)
                            && !WeakWords.Contains(x.Word))
                .ToList();

            if (!filteredWords.Any())
            {
                return string.Join(" ", words.Take(maxWords).Select(Capitalize));
            }

            // --- Bigramas ---
            var bigrams = new List<(string Text, int Index, double Score)>();

            for (int i = 0; i < filteredWords.Count - 1; i++)
            {
                var current = filteredWords[i];
                var next = filteredWords[i + 1];

                if (next.Index - current.Index <= 2)
                {
                    string bigramText = $"{current.Word} {next.Word}";
                    double positionScore = 1.0 / (1 + current.Index);
                    double technicalBonus = GetTechnicalBonus(current.Word) + GetTechnicalBonus(next.Word);

                    double score = 5.0 + (positionScore * 3.0) + technicalBonus;
                    bigrams.Add((bigramText, current.Index, score));
                }
            }

            // --- Unigramas ---
            var unigrams = filteredWords
                .GroupBy(x => x.Word)
                .Select(g =>
                {
                    int frequency = g.Count();
                    int firstPosition = g.Min(x => x.Index);
                    double positionScore = 1.0 / (1 + firstPosition);
                    double technicalBonus = GetTechnicalBonus(g.Key);

                    double score = (frequency * 1.8) + (positionScore * 3.2) + technicalBonus;

                    return new { Text = g.Key, Score = score, FirstIndex = firstPosition };
                })
                .ToList();

            // Combinar e ordenar
            var candidates = bigrams
                .Select(b => new { b.Text, b.Score, FirstIndex = b.Index })
                .Concat(unigrams.Select(u => new { u.Text, u.Score, u.FirstIndex }))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.FirstIndex)
                .ToList();

            // Selecionar sem repetir palavras
            var selected = new List<string>();
            var usedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int wordCount = 0;

            foreach (var candidate in candidates)
            {
                var parts = candidate.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Any(p => usedWords.Contains(p))) continue;
                if (wordCount + parts.Length > maxWords) continue;

                selected.Add(candidate.Text);
                foreach (var p in parts) usedWords.Add(p);
                wordCount += parts.Length;

                if (wordCount >= maxWords) break;
            }

            if (!selected.Any())
            {
                return string.Join(" ", filteredWords
                    .OrderBy(x => x.Index)
                    .Take(maxWords)
                    .Select(x => Capitalize(x.Word)));
            }

            return string.Join(" ", selected.Select(s =>
                string.Join(" ", s.Split(' ').Select(Capitalize))));
        }

        // Dá bónus a palavras que parecem técnicas
        private static double GetTechnicalBonus(string word)
        {
            if (string.IsNullOrEmpty(word)) return 0;

            double bonus = 0;

            if (word.Length >= 7) bonus += 1.2;

            // TODO: Expand bonus for words, which are common in technical terms
            if (word.Contains('-') || word.Any(char.IsDigit)) bonus += 1.8;

            if (word.EndsWith("ine") || word.EndsWith("oid") || word.EndsWith("osis") ||
                word.EndsWith("itis") || word.EndsWith("emia") || word.EndsWith("vaccine"))
                bonus += 1.5;

            return bonus;
        }

        private static string Capitalize(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            return char.ToUpper(word[0], CultureInfo.InvariantCulture) + word.Substring(1);
        }
    }
}