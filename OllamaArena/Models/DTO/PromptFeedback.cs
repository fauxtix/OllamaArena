namespace OllamaArena.Models.DTO;

public class PromptFeedback
{
    public string GeminiFeedback { get; set; } = string.Empty;
    public string OpenRouterFeedback { get; set; } = string.Empty;
    public string GeminiRecommendation { get; set; } = string.Empty;
    public string OpenRouterRecommendation { get; set; } = string.Empty;
}
