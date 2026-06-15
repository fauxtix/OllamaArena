using System.Text.Json.Serialization;

namespace OllamaFluentUIChat.Models.DTO
{
    public class OllamaModels
    {
        public class OllamaResponse
        {
            [JsonPropertyName("models")]
            public List<ModelDetails> Models { get; set; } = [];
        }

        public class ModelDetails
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("size")]
            public long SizeInBytes { get; set; }

            public double SizeInGB => Math.Round((double)SizeInBytes / (1024 * 1024 * 1024), 2);
        }

        public class GpuStatus
        {
            public bool FitsInGpu { get; set; }
            public double AvailableVramGB { get; set; }
            public double EstimatedRequiredMemoryGB { get; set; }
        }
    }
}
