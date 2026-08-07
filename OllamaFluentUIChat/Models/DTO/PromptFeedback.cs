namespace OllamaFluentUIChat.Models.DTO;

public class PromptFeedback
{
    public string GeminiFeedback { get; set; } = string.Empty;
    public string ChatGptFeedback { get; set; } = string.Empty;
    public string GeminiRecommendation { get; set; } = string.Empty;
    public string ChatGptRecommendation { get; set; } = string.Empty;
}
