using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Interfaces.Services
{
    /// <summary>
    /// Sistemas de tradução, por ordem de preferência. O primeiro que responder com
    /// uma tradução válida é o usado; os seguintes só entram em fallback.
    /// </summary>
    public static class TranslationProviders
    {
        public const string OpenRouter = "OpenRouter";
        public const string Gemini = "Gemini";
        public const string Ollama = "Ollama";
    }

    /// <summary>Etapas do processo de tradução, para a UI indicar o sistema em uso.</summary>
    public enum TranslationProgressStep
    {
        /// <summary>Definições lidas (BD → configuração → default).</summary>
        DefinicoesResolvidas,

        /// <summary>Pedido enviado a um sistema; a resposta ainda está a gerar.</summary>
        ProvedorIniciado,

        /// <summary>O sistema respondeu e a tradução foi validada.</summary>
        ProvedorConcluido,

        /// <summary>Sem chave, erro de rede/HTTP, resposta inválida ou cancelado.</summary>
        ProvedorFalhou,

        /// <summary>Todos os sistemas terminaram.</summary>
        Concluido
    }

    /// <summary>
    /// Evento emitido durante a tradução. Como a cadeia pode mudar de sistema
    /// (fallback), <see cref="Provider"/> indica sempre o sistema em causa.
    /// </summary>
    public sealed class TranslationProgressEvent
    {
        public TranslationProgressStep Step { get; init; }

        /// <summary>Sistema em causa (OpenRouter, Gemini ou Ollama).</summary>
        public string? Provider { get; init; }

        /// <summary>Modelo usado pelo sistema, quando conhecido.</summary>
        public string? Modelo { get; init; }

        /// <summary>Motivo da falha, quando aplicável.</summary>
        public string? Detalhe { get; init; }
    }

    public interface ITranslationService
    {
        /// <summary>
        /// Traduz o texto com a cadeia OpenRouter → Gemini → Ollama (local), devolvendo
        /// o primeiro resultado válido. O sistema usado fica em <see cref="TranslationResult.Provider"/>.
        /// </summary>
        /// <param name="reportarProgresso">
        /// Callback opcional para a UI mostrar o sistema em uso e as passagens de fallback.
        /// É invocado a partir da thread que executa o serviço: quem o consome tem de
        /// mudar para a thread do renderer antes de tocar em estado de UI.
        /// </param>
        /// <param name="cancellationToken">Cancela o pedido em curso (botão "Cancelar").</param>
        Task<TranslationResult> TranslateAsync(
            string text,
            Action<TranslationProgressEvent>? reportarProgresso = null,
            CancellationToken cancellationToken = default);
    }
}
