using OllamaFluentUIChat.Models.Entities;

namespace OllamaFluentUIChat.Models.DTO;

public class ConversationExportItem
{
    public string Titulo { get; set; } = string.Empty;
    public string NomeModelo { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataUltimaAtividade { get; set; }
    public List<ChatConversationMessage> Mensagens { get; set; } = [];
}
