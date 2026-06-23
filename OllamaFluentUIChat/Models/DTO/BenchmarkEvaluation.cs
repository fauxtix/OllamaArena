namespace OllamaFluentUIChat.Models.DTO
{
    public class BenchmarkEvaluation
    {
        public string ModeloNome { get; set; } = string.Empty;
        public string TextoPrompt { get; set; } = string.Empty;
        public int GeminiRating { get; set; }
        public int ChatGptRating { get; set; }
        public float TokensPorSegundo { get; set; }
        public float TempoPuroMs { get; set; }
        public float TempoCargaMs { get; set; }
        public int TamanhoTokens { get; set; }
        public string TempoPuroFormatado =>
            TimeSpan.FromMilliseconds(TempoPuroMs).ToString(@"m\:ss");
    }
}
