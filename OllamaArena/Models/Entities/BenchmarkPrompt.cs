namespace OllamaArena.Models.Entities
{
    public class BenchmarkPrompt
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public string TextoPrompt { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
        public double Temperatura { get; set; } = 0;

        // Propriedade de navegação para a UI saber que respostas este prompt teve
        public List<BenchmarkResponse> Answers { get; set; } = new();

        public string DataCriacaoFormatada => DataCriacao.ToLocalTime().ToString("g");
    }
}
