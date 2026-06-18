namespace OllamaFluentUIChat.Models.Entities
{
    public class BenchmarkResponse
    {
        public int Id { get; set; }
        public int PromptId { get; set; }
        public string ModeloNome { get; set; } = string.Empty;
        public string TextoResposta { get; set; } = string.Empty;
        public double TokensPorSegundo { get; set; }
        public double TempoPuroMs { get; set; }
        public double TempoCargaMs { get; set; }
        public int TamanhoTokens { get; set; }
    }
}
