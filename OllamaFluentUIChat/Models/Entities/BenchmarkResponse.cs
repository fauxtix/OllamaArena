namespace OllamaFluentUIChat.Models.Entities
{
    public class BenchmarkResponse
    {
        public int Id { get; set; }
        public int PromptId { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public string NomeModelo { get; set; } = string.Empty;
        public string TextoResposta { get; set; } = string.Empty;
        public double TokensPorSegundo { get; set; }
        public double TempoPuroMs { get; set; }
        public double TempoCargaMs { get; set; }
        public int TamanhoTokens { get; set; }

        public float? GeminiRating { get; set; }
        public string GeminiFeedback { get; set; } = string.Empty;
        public int? GeminiFactualRating { get; set; }
        public int? GeminiFormattingRating { get; set; }

        // NOVAS PROPRIEDADES GEMINI
        public int? GeminiComplianceRating { get; set; }
        public int? GeminiRelevanceRating { get; set; }
        public int? GeminiToneRating { get; set; }
        public int? GeminiConcisenessRating { get; set; }
        public int? GeminiClarityRating { get; set; }
        public int? GeminiReadabilityRating { get; set; }
        public int? GeminiHaloEffectRating { get; set; }
        public int? GeminiSafetyRating { get; set; }

        public float? ChatGptRating { get; set; }
        public string ChatGptFeedback { get; set; } = string.Empty;
        public int? ChatGptFactualRating { get; set; }
        public int? ChatGptFormattingRating { get; set; }

        // NOVAS PROPRIEDADES CHATGPT
        public int? ChatGptComplianceRating { get; set; }
        public int? ChatGptRelevanceRating { get; set; }
        public int? ChatGptToneRating { get; set; }
        public int? ChatGptConcisenessRating { get; set; }
        public int? ChatGptClarityRating { get; set; }
        public int? ChatGptReadabilityRating { get; set; }
        public int? ChatGptHaloEffectRating { get; set; }
        public int? ChatGptSafetyRating { get; set; }

        public string TempoPuroFormatado =>
    TimeSpan.FromMilliseconds(TempoPuroMs).ToString(@"m\:ss");
        public string TempoCargaFormatado =>
            TimeSpan.FromMilliseconds(TempoCargaMs).ToString(@"m\:ss");

    }
}