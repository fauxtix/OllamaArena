using System.Text.Json.Serialization;

namespace OllamaFluentUIChat.Models.DTO
{
    public class BenchmarkAnalysisResult
    {
        [JsonPropertyName("Sumario")]
        public string Sumario { get; set; } = string.Empty;

        [JsonPropertyName("AnaliseDetalhada")]
        public string AnaliseDetalhada { get; set; } = string.Empty;

        [JsonPropertyName("ModeloMaisRapido")]
        public string ModeloMaisRapido { get; set; } = string.Empty;

        [JsonPropertyName("MaxTokensSec")]
        public double MaxTokensSec { get; set; }

        [JsonPropertyName("ModeloMelhorAvaliado")]
        public string ModeloMelhorAvaliado { get; set; } = string.Empty;

        [JsonIgnore]
        public string TempoAnaliseFormatado { get; set; } = string.Empty;
    }
}
