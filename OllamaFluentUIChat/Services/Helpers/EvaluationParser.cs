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
        public string Recommendation { get; set; } = string.Empty;
    }

    public static class EvaluationParser
    {
        public static ParsedEvaluationResult ParseEvaluation(string rawAiResponse)
        {
            var result = new ParsedEvaluationResult();

            if (string.IsNullOrWhiteSpace(rawAiResponse))
                return result;

            var section = Section.None;

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
                {
                    result.FactualScore = factual;
                    section = Section.None;
                }

                else if (TryParseScore(line, "FORMATTING_SCORE:", out int formatting))
                {
                    result.FormattingScore = formatting;
                    section = Section.None;
                }

                else if (TryParseScore(line, "COMPLIANCE_SCORE:", out int compliance))
                {
                    result.ComplianceScore = compliance;
                    section = Section.None;
                }

                else if (TryParseScore(line, "RELEVANCE_SCORE:", out int relevance))
                {
                    result.RelevanceScore = relevance;
                    section = Section.None;
                }

                else if (TryParseScore(line, "TONE_SCORE:", out int tone))
                {
                    result.ToneScore = tone;
                    section = Section.None;
                }

                else if (TryParseScore(line, "CONCISENESS_SCORE:", out int conciseness))
                {
                    result.ConcisenessScore = conciseness;
                    section = Section.None;
                }

                else if (TryParseScore(line, "CLARITY_SCORE:", out int clarity))
                {
                    result.ClarityScore = clarity;
                    section = Section.None;
                }

                else if (TryParseScore(line, "READABILITY_SCORE:", out int readability))
                {
                    result.ReadabilityScore = readability;
                    section = Section.None;
                }

                else if (TryParseScore(line, "HALO_EFFECT_SCORE:", out int halo))
                {
                    result.HaloEffectScore = halo;
                    section = Section.None;
                }

                else if (TryParseScore(line, "SAFETY_SCORE:", out int safety))
                {
                    result.SafetyScore = safety;
                    section = Section.None;
                }

                else if (line.StartsWith("FINAL_SCORE:", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(ExtractValue(line, "FINAL_SCORE:"), out float val))
                        result.FinalScore = val;
                    section = Section.None;
                }
                else if (line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("FEEDBACK:", StringComparison.OrdinalIgnoreCase))
                {
                    string prefix = line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase) ? "DESCRIPTION:" : "FEEDBACK:";
                    result.Description = AppendToSection(result.Description, ExtractValue(line, prefix));
                    section = Section.Description;
                }
                else if (line.StartsWith("RECOMMENDATION:", StringComparison.OrdinalIgnoreCase))
                {
                    result.Recommendation = AppendToSection(result.Recommendation, ExtractValue(line, "RECOMMENDATION:"));
                    section = Section.Recommendation;
                }
                else if (section == Section.Description)
                {
                    result.Description = AppendToSection(result.Description, line);
                }
                else if (section == Section.Recommendation)
                {
                    result.Recommendation = AppendToSection(result.Recommendation, line);
                }
            }

            // Se não encontrou tag FEEDBACK/DESCRIPTION, coloca a resposta inteira no feedback
            if (string.IsNullOrEmpty(result.Description))
            {
                result.Description = rawAiResponse;
            }

            return result;
        }

        private enum Section { None, Description, Recommendation }

        private static string AppendToSection(string current, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return current;

            return string.IsNullOrWhiteSpace(current)
                ? value
                : current + " " + value;
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