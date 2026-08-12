namespace OllamaArena.Models.DTO
{
    public class BenchmarkAnalysisResult
    {
        public string Sumario { get; set; } = string.Empty;
        public List<string> LinhasAnaliseDetalhada { get; set; } = new();
        public string ModeloMaisRapido { get; set; } = string.Empty;
        public double MaxTokensSec { get; set; }
        public string ModeloMelhorAvaliado { get; set; } = string.Empty;
        public string TempoAnaliseFormatado { get; set; } = string.Empty;
    }
}