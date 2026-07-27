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

    public double TokensPorSegundo { get; set; }
    public double TempoPuroMs { get; set; }
    public double TempoCargaMs { get; set; }
    public int TamanhoTokens { get; set; }

    // Ratings Antigos
    public int GeminiRating { get; set; }
    public int ChatGptRating { get; set; }

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

    // Ratings ChatGPT (1-5)
    public int? ChatGptFactualRating { get; set; }
    public int? ChatGptFormattingRating { get; set; }
    public int? ChatGptComplianceRating { get; set; }
    public int? ChatGptRelevanceRating { get; set; }
    public int? ChatGptToneRating { get; set; }
    public int? ChatGptConcisenessRating { get; set; }
    public int? ChatGptClarityRating { get; set; }
    public int? ChatGptReadabilityRating { get; set; }
    public int? ChatGptHaloEffectRating { get; set; }
    public int? ChatGptSafetyRating { get; set; }

    // Propriedades Formatadas
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