namespace OllamaFluentUIChat.Models.DTO
{
    public class HistoryEntry
    {
        public string Model { get; set; } = "";
        public string Prompt { get; set; } = "";
        public DateTime Date { get; set; }
        public long DurationMs { get; set; }

        public string DateDisplay => Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        public string DurationDisplay
        {
            get
            {
                var ts = TimeSpan.FromMilliseconds(DurationMs);
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
        }
    }
}
