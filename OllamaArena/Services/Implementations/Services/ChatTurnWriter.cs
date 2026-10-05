using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;

namespace OllamaArena.Services.Implementations.Services;

/// <summary>
/// Escritor transaccional de turnos de chat. Todas as escritas de um turno partilham a
/// mesma ligação e transacção: ou o turno fica completo (conversa + 2 mensagens +
/// benchmark) ou nada fica gravado.
/// </summary>
public class ChatTurnWriter : IChatTurnWriter
{
    /// <summary>Tamanho máximo do título gerado a partir do prompt.</summary>
    private const int MaxTitleLength = 60;

    private readonly IDapperContext _context;
    private readonly IConversationRepository _conversations;
    private readonly IBenchmarkCommandRepository _benchmarks;
    private readonly ILogger<ChatTurnWriter> _logger;

    public ChatTurnWriter(
        IDapperContext context,
        IConversationRepository conversations,
        IBenchmarkCommandRepository benchmarks,
        ILogger<ChatTurnWriter> logger)
    {
        _context = context;
        _conversations = conversations;
        _benchmarks = benchmarks;
        _logger = logger;
    }

    public async Task<ChatTurnWriteResult> WriteAsync(ChatTurn turn, string modelName, int conversationId, int promptId)
    {
        ArgumentNullException.ThrowIfNull(turn);

        if (!turn.HasPersistableContent)
        {
            throw new InvalidOperationException(
                "Turno sem conteúdo persistível (vazio, cancelado ou falhado).");
        }

        string descricao = !string.IsNullOrWhiteSpace(turn.Summary)
            ? turn.Summary
            : ChatComposerService.SmartFallbackDescription(turn.UserPrompt);

        try
        {
            using var connection = _context.CreateConnection();
            using var transaction = connection.BeginTransaction();

            int effectiveConversationId = conversationId;
            if (effectiveConversationId == 0)
            {
                effectiveConversationId = await _conversations.CreateConversationAsync(
                    CriarTitulo(turn.UserPrompt), modelName, descricao, connection, transaction);

                if (effectiveConversationId == 0)
                    throw new InvalidOperationException("A criação da conversa devolveu id 0.");
            }

            bool userMessageOk = await _conversations.AddMessageAsync(new ChatConversationMessage
            {
                ConversationId = effectiveConversationId,
                Role = "user",
                Content = turn.UserPrompt,
                Temperature = turn.Temperature,
                Timestamp = DateTime.UtcNow
            }, connection, transaction);

            if (!userMessageOk)
                throw new InvalidOperationException("A gravação da mensagem do utilizador falhou.");

            bool assistantMessageOk = await _conversations.AddMessageAsync(new ChatConversationMessage
            {
                ConversationId = effectiveConversationId,
                Role = "assistant",
                Content = turn.AssistantText,
                Reasoning = string.IsNullOrWhiteSpace(turn.Reasoning) ? null : turn.Reasoning,
                Temperature = turn.Temperature,
                ElapsedTime = turn.ElapsedLabel,
                Timestamp = DateTime.UtcNow
            }, connection, transaction);

            if (!assistantMessageOk)
                throw new InvalidOperationException("A gravação da mensagem do assistente falhou.");

            int effectivePromptId = promptId;
            string storedDescription = descricao;

            // Só grava benchmark quando o Ollama devolveu métricas reais. Reutiliza o
            // prompt do turno anterior quando existe, para não duplicar linhas em Prompts.
            if (turn.HasBenchmarkMetrics)
            {
                if (effectivePromptId == 0)
                {
                    var (createdPromptId, savedDescription) =
                        await _benchmarks.GetOrCreatePromptIdAsync(turn.UserPrompt, descricao, connection, transaction);
                    effectivePromptId = createdPromptId;
                    storedDescription = savedDescription;
                }

                var response = new BenchmarkResponse
                {
                    PromptId = effectivePromptId,
                    NomeModelo = modelName,
                    TextoResposta = turn.AssistantText,
                    TokensPorSegundo = Math.Round(turn.TokensPerSecond, 1),
                    TempoPuroMs = turn.EvalDurationNs / 1_000_000.0,
                    TempoCargaMs = turn.LoadDurationNs / 1_000_000.0,
                    TempoProcessamento = turn.TotalMs,
                    TamanhoTokens = (int)turn.TotalTokens,
                    // Congela o idioma da sessão no momento da geração: o juiz usa este
                    // valor em reavaliações futuras (respostas antigas ficam null).
                    IdiomaSessao = turn.SessionLanguage
                };

                if (!await _benchmarks.CreateResponseAsync(response, connection, transaction))
                    throw new InvalidOperationException("A gravação da resposta de benchmark falhou.");
            }

            await _conversations.TouchConversationAsync(effectiveConversationId, connection, transaction);

            transaction.Commit();

            return new ChatTurnWriteResult(effectiveConversationId, effectivePromptId, storedDescription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao persistir turno do chat. Transacção revertida.");
            throw new ChatTurnPersistenceException("Não foi possível gravar a conversa.", ex);
        }
    }

    private static string CriarTitulo(string userPrompt)
        => userPrompt.Length > MaxTitleLength ? userPrompt[..MaxTitleLength] : userPrompt;
}