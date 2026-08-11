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
        public double Temperatura { get; set; } = 0;

        // Alterado para double (milissegundos), igual ao TempoPuroMs e TempoCargaMs
        public double TempoProcessamento { get; set; }

        public double TempoCargaMs { get; set; }
        public int TamanhoTokens { get; set; }

        public float? GeminiRating { get; set; }
        public string GeminiFeedback { get; set; } = string.Empty;
        public string GeminiRecommendation { get; set; } = string.Empty;
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
        public int? GeminiLanguageConsistencyRating { get; set; }
        public int? GeminiLoopDetectionRating { get; set; }

        public float? OpenRouterRating { get; set; }
        public string OpenRouterFeedback { get; set; } = string.Empty;
        public string OpenRouterRecommendation { get; set; } = string.Empty;
        public int? OpenRouterFactualRating { get; set; }
        public int? OpenRouterFormattingRating { get; set; }

        // NOVAS PROPRIEDADES OPENROUTER
        public int? OpenRouterComplianceRating { get; set; }
        public int? OpenRouterRelevanceRating { get; set; }
        public int? OpenRouterToneRating { get; set; }
        public int? OpenRouterConcisenessRating { get; set; }
        public int? OpenRouterClarityRating { get; set; }
        public int? OpenRouterReadabilityRating { get; set; }
        public int? OpenRouterHaloEffectRating { get; set; }
        public int? OpenRouterSafetyRating { get; set; }
        public int? OpenRouterLanguageConsistencyRating { get; set; }
        public int? OpenRouterLoopDetectionRating { get; set; }

        // PROPRIEDADES DE FORMATAÇÃO PARA A UI

        public string TempoPuroFormatado =>
            TimeSpan.FromMilliseconds(TempoPuroMs).ToString(@"m\:ss");

        public string TempoCargaFormatado =>
            TimeSpan.FromMilliseconds(TempoCargaMs).ToString(@"m\:ss");

        // Formato hh:mm:ss (mesmo do DurationDisplay)
        public string TempoProcessamentoDisplay =>
            TimeSpan.FromMilliseconds(TempoProcessamento).ToString(@"hh\:mm\:ss");

        // Formato curto "96.5s" ou "3.42s" se precisares de mostrar no formato antigo no UI
        public string TempoProcessamentoCurtoFormatado
        {
            get
            {
                var totalSegundos = TempoProcessamento / 1000.0;
                return totalSegundos < 10
                    ? $"{totalSegundos:F2}s"
                    : $"{totalSegundos:F1}s";
            }
        }
    }
}
