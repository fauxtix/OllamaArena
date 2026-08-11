namespace OllamaFluentUIChat.Models.DTO;

public class BenchmarkEvaluationModel
{
    public int Id { get; set; }
    public int PromptId { get; set; }
    public int ResponseId { get; set; }
    public DateTime DataCriacao { get; set; }
    public string NomeModelo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string TextoPrompt { get; set; } = string.Empty;
    public double Temperatura { get; set; } = 0;

    public double TokensPorSegundo { get; set; }
    public double TempoPuroMs { get; set; }
    public double TempoCargaMs { get; set; }
    public int TamanhoTokens { get; set; }

    // Ratings Antigos
    public int GeminiRating { get; set; }
    public int OpenRouterRating { get; set; }

    // Ratings Gemini (1-5)
    public int? GeminiFactualRating { get; set; }
    public int? GeminiFormattingRating { get; set; }
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

    // Ratings OpenRouter (1-5)
    public int? OpenRouterFactualRating { get; set; }
    public int? OpenRouterFormattingRating { get; set; }
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

    public string TempoPuroFormatado
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(TempoPuroMs);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        }
    }

    public string TempoCargaFormatado
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(TempoCargaMs);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        }
    }
    public string DataCriacaoFormatada =>
        DataCriacao.ToString("dd/MM/yyyy HH:mm");
}