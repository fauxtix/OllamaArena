using System.Text.Json.Serialization;

namespace OllamaFluentUIChat.Models.DTO;

public sealed class TranslateResponseDto
{
    [JsonPropertyName("translatedText")]
    public string TranslatedText { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;
}
