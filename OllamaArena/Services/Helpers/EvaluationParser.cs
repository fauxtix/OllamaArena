using System.Globalization;

namespace OllamaArena.Services.Helpers
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
        public int? LanguageConsistencyScore { get; set; }
        public int? LoopDetectionScore { get; set; }
        public float? FinalScore { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;

        /// <summary>
        /// Indica se o juiz marcou a resposta como recusa correta
        /// (REFUSAL_HANDLED: yes/no). Null quando o juiz não devolveu a linha
        /// (avaliações antigas ou manuais sem o marcador).
        /// </summary>
        public bool? RefusalHandled { get; set; }

        /// <summary>Flag 0/1/null para persistir em coluna INTEGER do SQLite.</summary>
        public int? RefusalHandledFlag => RefusalHandled.HasValue ? (RefusalHandled.Value ? 1 : 0) : null;
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

                else if (TryParseScore(line, "LANGUAGE_CONSISTENCY_SCORE:", out int languageConsistency))
                {
                    result.LanguageConsistencyScore = languageConsistency;
                    section = Section.None;
                }

                else if (TryParseScore(line, "LOOP_DETECTION_SCORE:", out int loopDetection))
                {
                    result.LoopDetectionScore = loopDetection;
                    section = Section.None;
                }

                else if (line.StartsWith("REFUSAL_HANDLED:", StringComparison.OrdinalIgnoreCase))
                {
                    result.RefusalHandled = ParseYesNo(ExtractValue(line, "REFUSAL_HANDLED:"));
                    section = Section.None;
                }

                else if (line.StartsWith("FINAL_SCORE:", StringComparison.OrdinalIgnoreCase))
                {
                    string rawValue = ExtractValue(line, "FINAL_SCORE:")
                        .Replace("[", "").Replace("]", "").Trim();

                    // Responde "4/5" em vez de "4"
                    int slash = rawValue.IndexOf('/');
                    if (slash >= 0)
                        rawValue = rawValue.Substring(0, slash).Trim();

                    // Parse invariante à cultura: pt-PT usa vírgula como separador decimal,
                    // o que descartaria "2.3". Normaliza vírgula para ponto.
                    rawValue = rawValue.Replace(',', '.');

                    if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
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

                // Responde "3/5" em vez de "3"
                int slash = rawValue.IndexOf('/');
                if (slash >= 0)
                    rawValue = rawValue.Substring(0, slash).Trim();

                // Parse invariante à cultura (evita divergências pt-PT vs en-US).
                return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out score);
            }
            return false;
        }

        private static string ExtractValue(string line, string prefix)
        {
            return line.Replace(prefix, "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        /// <summary>
        /// Interpreta o valor de REFUSAL_HANDLED: aceita yes/no, true/false,
        /// sim/não e 1/0 (com ou sem colchetes). Valor desconhecido devolve null.
        /// </summary>
        private static bool? ParseYesNo(string value)
        {
            var normalizado = value.Trim().Trim('[', ']').Trim().ToLowerInvariant();
            return normalizado switch
            {
                "yes" or "true" or "sim" or "1" => true,
                "no" or "false" or "nao" or "não" or "0" => false,
                _ => null
            };
        }
    }
}