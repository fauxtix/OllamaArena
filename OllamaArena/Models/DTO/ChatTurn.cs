namespace OllamaArena.Models.DTO
{
    /// <summary>
    /// Resultado imutável de um turno de chat (pergunta + resposta), com as métricas do
    /// modelo. É o único objecto que atravessa o fluxo entre o cliente de streaming, a
    /// página e o escritor de persistência — antes o mesmo resultado viajava como 8
    /// parâmetros e depois como 8 campos "_pending*" na página.
    /// </summary>
    public sealed record ChatTurn
    {
        /// <summary>Prompt tal como o utilizador o escreveu (sem o bloco de instrução SUMMARY).</summary>
        public required string UserPrompt { get; init; }

        /// <summary>Resposta do modelo já sem o prefixo SUMMARY (ver <c>ChatComposerService</c>).</summary>
        public string AssistantText { get; init; } = string.Empty;

        /// <summary>Raciocínio do modelo, quando o modo think está ativo e o modelo o expõe.</summary>
        public string? Reasoning { get; init; }

        /// <summary>Resumo extraído da resposta (marcador SUMMARY do prompt).</summary>
        public string? Summary { get; init; }

        public double Temperature { get; init; }

        /// <summary>Tempo total já formatado para UI ("1.23s").</summary>
        public string ElapsedLabel { get; init; } = string.Empty;

        public long PromptTokens { get; init; }

        public long EvalTokens { get; init; }

        public long EvalDurationNs { get; init; }

        public long LoadDurationNs { get; init; }

        public double TotalMs { get; init; }

        /// <summary>Idioma da sessão no momento da geração (gelado para reavaliações futuras).</summary>
        public string? SessionLanguage { get; init; }

        /// <summary>
        /// Falso quando o turno foi cancelado ou falhou. O texto parcial é mostrado ao
        /// utilizador (e permanece na UI) mas não é persistido nem entra no benchmark —
        /// evita gravar meio turnos como se fosse uma resposta completa.
        /// </summary>
        public bool Completed { get; init; } = true;

        /// <summary>Tokens totais (prompt + resposta) — é o que a coluna TamanhoTokens guarda.</summary>
        public long TotalTokens => PromptTokens + EvalTokens;

        /// <summary>
        /// Só há benchmark quando o turno terminou e o Ollama devolveu métricas reais.
        /// Substitui o antigo <c>evalCount &lt;= 0 || evalDurationNs &lt;= 0</c>.
        /// </summary>
        public bool HasBenchmarkMetrics => Completed && EvalDurationNs > 0 && TotalTokens > 0;

        /// <summary>Tokens por segundo sobre a duração de avaliação (não sobre o total).</summary>
        public double TokensPerSecond =>
            EvalDurationNs > 0 ? TotalTokens / (EvalDurationNs / 1_000_000_000.0) : 0;

        /// <summary>
        /// Verdadeiro quando o turno tem conteúdo que faz sentido gravar. Turnos
        /// cancelados, falhados ou vazios nunca são persistidos.
        /// </summary>
        public bool HasPersistableContent =>
            Completed
            && !string.IsNullOrWhiteSpace(UserPrompt)
            && !string.IsNullOrWhiteSpace(AssistantText);
    }

    /// <summary>
    /// Resultado da escrita de um turno: os identificadores e a descrição que a página
    /// precisa de reter para os turnos seguintes (a conversa continua aberta e o prompt
    /// de benchmark é reutilizado em vez de duplicado).
    /// </summary>
    public sealed record ChatTurnWriteResult(int ConversationId, int PromptId, string Description);
}
