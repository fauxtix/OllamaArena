namespace OllamaArena.Models.DTO
{
    public class TranslationResult
    {
        public string TranslatedText { get; set; } = "";
        public double Confidence { get; set; }
        public string? ModelUsed { get; set; }

        /// <summary>
        /// Sistema que produziu a tradução: "OpenRouter", "Gemini" ou "Ollama".
        /// A UI mostra-o para o utilizador saber qual motor foi usado.
        /// </summary>
        public string? Provider { get; set; }

        public long TimeSpentMs { get; set; }

        public string TimeSpentFormatted =>
            TimeSpan.FromMilliseconds(TimeSpentMs).ToString(@"m\:ss");

        /// <summary>Motor + modelo, para a pré-visualização da tradução.</summary>
        public string EngineDescription =>
            string.IsNullOrWhiteSpace(Provider)
                ? ModelUsed ?? string.Empty
                : string.IsNullOrWhiteSpace(ModelUsed)
                    ? Provider
                    : $"{Provider} · {ModelUsed}";
    }
}
