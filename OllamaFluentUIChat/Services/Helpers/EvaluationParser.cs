namespace OllamaFluentUIChat.Services.Helpers
{
    public class ParsedEvaluationResult
    {
        public int? FactualScore { get; set; }
        public int? FormattingScore { get; set; }
        public int? ComplianceScore { get; set; }
        public int? RelevanceScore { get; set; }
        public int? ToneScore { get; set; }
        public int? ConcisenessScore { get; set; }
        public int? ClarityScore { get; set; }
        public int? ReadabilityScore { get; set; }
        public int? HaloEffectScore { get; set; }
        public int? SafetyScore { get; set; }
        public float? FinalScore { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public static class EvaluationParser
    {
        public static ParsedEvaluationResult ParseEvaluation(string rawAiResponse)
        {
            var result = new ParsedEvaluationResult();

            if (string.IsNullOrWhiteSpace(rawAiResponse))
                return result;

            using var reader = new StringReader(rawAiResponse);
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                // Limpa hífen ou asterisco inicial (ex: "- FACTUAL_SCORE:" ou "* FACTUAL_SCORE:")
                if (line.StartsWith("-") || line.StartsWith("*"))
                {
                    line = line.TrimStart('-', '*', ' ');
                }

                if (TryParseScore(line, "FACTUAL_SCORE:", out int factual))
                    result.FactualScore = factual;

                else if (TryParseScore(line, "FORMATTING_SCORE:", out int formatting))
                    result.FormattingScore = formatting;

                else if (TryParseScore(line, "COMPLIANCE_SCORE:", out int compliance))
                    result.ComplianceScore = compliance;

                else if (TryParseScore(line, "RELEVANCE_SCORE:", out int relevance))
                    result.RelevanceScore = relevance;

                else if (TryParseScore(line, "TONE_SCORE:", out int tone))
                    result.ToneScore = tone;

                else if (TryParseScore(line, "CONCISENESS_SCORE:", out int conciseness))
                    result.ConcisenessScore = conciseness;

                else if (TryParseScore(line, "CLARITY_SCORE:", out int clarity))
                    result.ClarityScore = clarity;

                else if (TryParseScore(line, "READABILITY_SCORE:", out int readability))
                    result.ReadabilityScore = readability;

                else if (TryParseScore(line, "HALO_EFFECT_SCORE:", out int halo))
                    result.HaloEffectScore = halo;

                else if (TryParseScore(line, "SAFETY_SCORE:", out int safety))
                    result.SafetyScore = safety;

                else if (line.StartsWith("FINAL_SCORE:", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(ExtractValue(line, "FINAL_SCORE:"), out float val))
                        result.FinalScore = val;
                }
                else if (line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("FEEDBACK:", StringComparison.OrdinalIgnoreCase))
                {
                    string prefix = line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase) ? "DESCRIPTION:" : "FEEDBACK:";
                    result.Description = ExtractValue(line, prefix);

                    string restOfText = reader.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(restOfText))
                    {
                        result.Description += " " + restOfText;
                    }
                }
            }

            // Se não encontrou tag FEEDBACK/DESCRIPTION, coloca a resposta inteira no feedback
            if (string.IsNullOrEmpty(result.Description))
            {
                result.Description = rawAiResponse;
            }

            return result;
        }

        private static bool TryParseScore(string line, string prefix, out int score)
        {
            score = 0;
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string rawValue = ExtractValue(line, prefix);

                // Remove colchetes extras se a IA responder "FACTUAL_SCORE: [5]" em vez de "FACTUAL_SCORE: 5"
                rawValue = rawValue.Replace("[", "").Replace("]", "").Trim();

                return int.TryParse(rawValue, out score);
            }
            return false;
        }

        private static string ExtractValue(string line, string prefix)
        {
            return line.Replace(prefix, "", StringComparison.OrdinalIgnoreCase).Trim();
        }
    }
}