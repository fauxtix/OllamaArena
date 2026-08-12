namespace OllamaArena.Models.DTO
{
    public class OllamaChatMessage
    {
        [System.Text.Json.Serialization.JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

    }
}
