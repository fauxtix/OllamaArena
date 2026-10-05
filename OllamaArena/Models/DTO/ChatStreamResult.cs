namespace OllamaArena.Models.DTO
{
    /// <summary>Estado final de uma chamada a /api/chat em streaming.</summary>
    public sealed record ChatStreamResult
    {
        public string Text { get; init; } = string.Empty;

        public string? Reasoning { get; init; }

        public long PromptTokens { get; init; }

        public long EvalTokens { get; init; }

        public long EvalDurationNs { get; init; }

        public long LoadDurationNs { get; init; }

        /// <summary>Verdadeiro quando o Ollama fechou o stream com "done": true.</summary>
        public bool Done { get; init; }
    }

    /// <summary>
    /// Instantâneo do stream para atualizar a UI. A página limita a frequência com que
    /// re-renderiza a partir destes snapshots, em vez de o fazer a cada token.
    /// </summary>
    public sealed record ChatStreamProgress(
        string Text,
        string? Reasoning,
        long PromptTokens,
        long EstimatedOutputTokens);
}
