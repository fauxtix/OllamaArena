namespace OllamaFluentUIChat.Services.Helpers
{
    public static class EvaluationParser
    {
        public static void ParseEvaluation(string rawAiResponse, out int? factual, out int? formatting, out float? final, out string description)
        {
            factual = null;
            formatting = null;
            final = null;
            description = string.Empty;

            if (string.IsNullOrWhiteSpace(rawAiResponse)) return;

            using var reader = new StringReader(rawAiResponse);
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.StartsWith("FACTUAL_SCORE:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Replace("FACTUAL_SCORE:", "", StringComparison.OrdinalIgnoreCase).Trim(), out int val))
                        factual = val;
                }
                else if (line.StartsWith("FORMATTING_SCORE:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Replace("FORMATTING_SCORE:", "", StringComparison.OrdinalIgnoreCase).Trim(), out int val))
                        formatting = val;
                }
                else if (line.StartsWith("FINAL_SCORE:", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(line.Replace("FINAL_SCORE:", "", StringComparison.OrdinalIgnoreCase).Trim(), out float val))
                        final = val;
                }
                else if (line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
                {
                    description = line.Replace("DESCRIPTION:", "", StringComparison.OrdinalIgnoreCase).Trim();
                    string restOfText = reader.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(restOfText))
                    {
                        description += " " + restOfText;
                    }
                }
            }

            if (string.IsNullOrEmpty(description))
            {
                description = rawAiResponse;
            }
        }
    }
}