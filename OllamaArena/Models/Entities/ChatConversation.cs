namespace OllamaArena.Models.Entities
{
    public class ChatConversation
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public string NomeModelo { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
        public DateTime DataUltimaAtividade { get; set; }

        public string DataUltimaAtividadeFormatada => DataUltimaAtividade.ToLocalTime().ToString("g");
    }
}
