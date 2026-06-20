namespace OllamaFluentUIChat.Models.Entities
{
    public class LogEntity
    {
        public int Id { get; set; }
        public string? Timestamp { get; set; }
        public string? Level { get; set; }
        public string? Exception { get; set; }
        public string? RenderedMessage { get; set; }
        public string? Properties { get; set; }
    }

    public class Log
    {
        public int id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Exception { get; set; } = string.Empty;
        public string RenderedMessage { get; set; } = string.Empty;
        public string Properties { get; set; } = string.Empty;
    }
}
