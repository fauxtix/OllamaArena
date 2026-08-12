namespace OllamaArena.Models.DTO
{
    public class TranslationResult
    {
        public string TranslatedText { get; set; } = "";
        public double Confidence { get; set; }
        public string? ModelUsed { get; set; }
        public long TimeSpentMs { get; set; }

        public string TimeSpentFormatted =>
            TimeSpan.FromMilliseconds(TimeSpentMs).ToString(@"m\:ss");
    }
}
