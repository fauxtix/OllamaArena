using System.Text.Json.Serialization;

namespace OllamaArena.Models.DTO
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

            [JsonPropertyName("digest")]
            public string Digest { get; set; } = string.Empty;

            [JsonPropertyName("details")]
            public ModelDetailsInfo? Details { get; set; }

            // Propriedades exclusivas do endpoint /api/ps (modelos ativos)
            [JsonPropertyName("expires_at")]
            public DateTime? ExpiresAt { get; set; }

            [JsonPropertyName("size_vram")]
            public long SizeInVramBytes { get; set; }

            // Métodos utilitários de conversão
            public double SizeInGB => Math.Round((double)SizeInBytes / (1024 * 1024 * 1024), 2);
            public double SizeInVramGB => Math.Round((double)SizeInVramBytes / (1024 * 1024 * 1024), 2);

            // Percentagem do modelo que está a rodar na GPU vs CPU
            public double GpuOffloadPercentage => SizeInBytes > 0
                ? Math.Round(((double)SizeInVramBytes / SizeInBytes) * 100, 1)
                : 0;

            // --- ATRIBUTO INTEGRADO ---
            // Armazena a janela de contexto descoberta após a consulta ao endpoint /api/show
            public int ContextLength { get; set; } = 2048;

            public string TrainingYear { get; set; } = "2024"; // 'Desconhecido iria dar errro na execução do prompt de avaliação de modelos, então defini como 2024 por padrão.
        }

        public class ModelDetailsInfo
        {
            [JsonPropertyName("parent_model")]
            public string ParentModel { get; set; } = string.Empty;
            [JsonPropertyName("format")]
            public string Format { get; set; } = string.Empty;
            [JsonPropertyName("family")]
            public string Family { get; set; } = string.Empty;
            [JsonPropertyName("parameter_size")]
            public string ParameterSize { get; set; } = string.Empty;
            [JsonPropertyName("quantization_level")]
            public string QuantizationLevel { get; set; } = string.Empty;
        }

        // --- CLASSE DE RESPOSTA ADAPTADA PARA O ENDPOINT /API/SHOW ---
        public class OllamaShowResponse
        {
            [JsonPropertyName("parameters")]
            public string Parameters { get; set; } = string.Empty;

            [JsonPropertyName("modelfile")]
            public string Modelfile { get; set; } = string.Empty;

            [JsonPropertyName("template")]
            public string Template { get; set; } = string.Empty;

            // --- ADICIONADO: Captura as chaves dinâmicas nativas do ficheiro (ex: context_length) ---
            [JsonPropertyName("model_info")]
            public Dictionary<string, object>? ModelInfo { get; set; }

            // Capacidades do modelo (ex.: "thinking", "tools", "vision") — usado para decidir
            // se o payload deve incluir `think` sem que modelos que não o suportam rejeitem o pedido.
            [JsonPropertyName("capabilities")]
            public List<string>? Capabilities { get; set; }
        }

        public class GpuStatus
        {
            public bool FitsInGpu { get; set; }
            public double AvailableVramGB { get; set; }
            public double EstimatedRequiredMemoryGB { get; set; }
        }
    }
}
