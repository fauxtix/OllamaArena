using System.Text.Json.Serialization;

namespace OllamaArena.Models.DTO
{
    public class OllamaMetrics
    {
        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("load_duration")]
        public long LoadDuration { get; set; }

        [JsonPropertyName("eval_duration")]
        public long EvalDuration { get; set; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }
    }
}
