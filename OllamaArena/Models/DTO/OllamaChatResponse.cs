using System.Text.Json.Serialization;

namespace OllamaArena.Models.DTO
{
    public class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("message")]
        public OllamaChatMessage Message { get; set; } = new();
    }
}
