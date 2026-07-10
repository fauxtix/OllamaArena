using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OllamaFluentUIChat.Models.DTO
{
    public class HistoryResponse
    {
        public int Id { get; set; }
        public string Prompt{ get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public float TempoPuro { get; set; }
        public float TempoCarga { get; set; }
        public DateTime DataCriacao { get; set; }
        public string DateDisplay => DataCriacao.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        public string DurationDisplay
        {
            get
            {
                var ts = TimeSpan.FromMilliseconds(TempoPuro);
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
        }

    }
}
