namespace OllamaArena.Models.Entities
{
    public class ChatConversationMessage
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Reasoning { get; set; }
        public double Temperature { get; set; }
        public string? ElapsedTime { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
