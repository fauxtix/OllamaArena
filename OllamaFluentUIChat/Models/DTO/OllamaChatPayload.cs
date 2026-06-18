namespace OllamaFluentUIChat.Models.DTO
{
    public class OllamaChatPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("messages")]
        public List<OllamaChatMessage> Messages { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("stream")]
        public bool Stream { get; set; }

        // Usar um dicionário evita que o System.Text.Json envie campos extra ou inválidos
        [System.Text.Json.Serialization.JsonPropertyName("options")]
        public Dictionary<string, object>? Options { get; set; }

    }
}
