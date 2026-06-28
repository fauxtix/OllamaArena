namespace OllamaFluentUIChat.Models.Entities
{
public class BenchmarkResponse
{
    public int Id { get; set; }
    public int PromptId { get; set; }
    public string ModeloNome { get; set; } = string.Empty;
    public string TextoResposta { get; set; } = string.Empty;
    public double TokensPorSegundo { get; set; }
    public double TempoPuroMs { get; set; }
    public double TempoCargaMs { get; set; }
    public int TamanhoTokens { get; set; }
    
    // --- NOVOS CAMPOS ADICIONADOS ---
    public int? GeminiFactualRating { get; set; }    // Nota de Conteúdo (1-5)
    public int? GeminiFormattingRating { get; set; } // Nota de Formatação (1-5)
    
    // Mantém-se igual (será a nota Global/Final)
    public int? GeminiRating { get; set; }
    public string GeminiFeedback { get; set; } = string.Empty;
    
    // --- NOVOS CAMPOS ADICIONADOS ---
    public int? ChatGptFactualRating { get; set; }    // Nota de Conteúdo (1-5)
    public int? ChatGptFormattingRating { get; set; } // Nota de Formatação (1-5)
    
    // Mantém-se igual (será a nota Global/Final)
    public int? ChatGptRating { get; set; }
    public string ChatGptFeedback { get; set; } = string.Empty;
}}
