namespace OllamaFluentUIChat.Models.DTO
{
    public class Conversation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Json { get; set; } = "[]"; // serialized List<ChatMessage>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
