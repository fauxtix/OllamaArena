namespace OllamaFluentUIChat.Models.DTO
{
    public class OllamaMetrics
    {
        [System.Text.Json.Serialization.JsonPropertyName("done")]
        public bool Done { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("load_duration")]
        public long LoadDuration { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("eval_duration")]
        public long EvalDuration { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }
    }
}
