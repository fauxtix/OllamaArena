namespace OllamaFluentUIChat.Models.DTO
{
    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string User { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsCurrentUser { get; set; }
        public string? ElapsedTime { get; set; }
        public double Temperature { get; set; }
    }
}
