namespace OllamaFluentUIChat.Models.Entities
{
public class BenchmarkResponse
{
    public int Id { get; set; }
    public int PromptId { get; set; }
    public string NomeModelo { get; set; } = string.Empty;
    public string TextoResposta { get; set; } = string.Empty;
    public double TokensPorSegundo { get; set; }
    public double TempoPuroMs { get; set; }
    public double TempoCargaMs { get; set; }
    public int TamanhoTokens { get; set; }
    
    public int? GeminiFactualRating { get; set; }   
    public int? GeminiFormattingRating { get; set; }
    
    public int? GeminiRating { get; set; }
    public string GeminiFeedback { get; set; } = string.Empty;
    
    public int? ChatGptFactualRating { get; set; }    
    public int? ChatGptFormattingRating { get; set; } 
    
    public int? ChatGptRating { get; set; }
    public string ChatGptFeedback { get; set; } = string.Empty;
}}
