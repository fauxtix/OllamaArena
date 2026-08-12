using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Interfaces.Services
{
    public interface ITranslationService
    {
        // Usa o modelo explícito
        Task<TranslationResult> TranslateAsync(string text, string model);

        // Usa o melhor modelo conhecido pela BD
        Task<TranslationResult> TranslateAutoAsync(string text);
    }
}
