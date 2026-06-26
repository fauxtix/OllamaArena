using System.Text.Json.Serialization;

namespace OllamaFluentUIChat.Models.DTO;

public sealed class TranslateRequestDto
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("sourceLanguage")]
    public string SourceLanguage { get; set; } = string.Empty;

    [JsonPropertyName("targetLanguage")]
    public string TargetLanguage { get; set; } = string.Empty;

    [JsonPropertyName("connectionId")]
    public string? ConnectionId { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}
