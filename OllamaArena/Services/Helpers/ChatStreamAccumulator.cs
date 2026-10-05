using OllamaArena.Models.DTO;
using System.Text.Json;

namespace OllamaArena.Services.Helpers;

/// <summary>
/// Acumula os chunks NDJSON do /api/chat num resultado. É separado do cliente HTTP para
/// poder ser testado directamente com <c>JsonElement</c> — não precisa de um servidor Ollama
/// nem de sockets para se verificar o parsing de <c>reasoning_content</c>/<c>thinking</c>,
/// do fallback <c>response</c> (API /api/generate) ou das métricas do <c>done</c>.
/// </summary>
public sealed class ChatStreamAccumulator
{
    private readonly bool _captureReasoning;

    /// <summary>
    /// O raciocínio do chunk que <see cref="Accumulate"/> acabou de processar. Sem esta
    /// guarda, <see cref="ReadFinalReasoning"/> voltava a acrescentar ao fim o mesmo texto
    /// que <see cref="Accumulate"/> já tinha somado.
    /// </summary>
    private bool _reasoningFromCurrentChunk;

    public ChatStreamAccumulator(bool captureReasoning)
    {
        _captureReasoning = captureReasoning;
    }

    public string Text { get; private set; } = string.Empty;

    public string? Reasoning { get; private set; }

    /// <summary>Verdadeiro no chunk actual que trouxe texto novo.</summary>
    public bool ReceivedText { get; private set; }

    public bool Done { get; private set; }

    public long PromptTokens { get; private set; }

    public long EvalTokens { get; private set; }

    public long EvalDurationNs { get; private set; }

    public long LoadDurationNs { get; private set; }

    /// <summary>Estimativa gulosa dos tokens de saída, para a barra de contexto.</summary>
    public long EstimatedOutputTokens { get; private set; }

    public long TotalTokens => PromptTokens + EvalTokens;

    /// <summary>Contador de linhas malformadas já toleradas.</summary>
    public int MalformedLines { get; private set; }

    /// <summary>Regista uma linha malformada (para a política de tolerância do cliente).</summary>
    public void RecordMalformedLine() => MalformedLines++;

    /// <summary>Lê texto, raciocínio e métricas de um chunk.</summary>
    public void Accumulate(JsonElement root)
    {
        ReceivedText = false;
        _reasoningFromCurrentChunk = false;

        string? chunkText = null;
        string? reasoningText = null;

        if (root.TryGetProperty("message", out var message))
        {
            if (message.TryGetProperty("content", out var content))
                chunkText = content.GetString();

            reasoningText = ReadReasoning(message);
        }
        else if (root.TryGetProperty("response", out var response))
        {
            // O /api/generate usa "response" em vez de "message".
            chunkText = response.GetString();
        }

        if (_captureReasoning && !string.IsNullOrWhiteSpace(reasoningText))
        {
            AppendReasoning(reasoningText);
            _reasoningFromCurrentChunk = true;
        }

        if (!string.IsNullOrWhiteSpace(chunkText))
        {
            Text += chunkText;
            EstimatedOutputTokens += ChatComposerService.EstimateTokens(chunkText);
            ReceivedText = true;
        }

        // O chunk final do Ollama traz as métricas e "done": true mas nenhuma mensagem, por
        // isso a leitura não pode ficar atrás de um return antecipado por falta de texto —
        // caso contrário as métricas reais do turno nunca chegariam ao benchmark.
        if (root.TryGetProperty("done", out var done) && done.ValueKind == JsonValueKind.True)
            ReadMetrics(root);
    }

    /// <summary>
    /// Alguns modelos (qwq, deepseek-r1) só enviam o raciocínio na mensagem final, já no
    /// chunk "done". Sem esta leitura o raciocínio desaparecia quando vinha isolado. Se o
    /// chunk já tiver sido contabilizado por <see cref="Accumulate"/>, não volta a somar.
    /// </summary>
    public void ReadFinalReasoning(JsonElement root)
    {
        if (!_captureReasoning || _reasoningFromCurrentChunk)
            return;

        if (!root.TryGetProperty("message", out var message))
            return;

        string? finalReasoning = ReadReasoning(message);
        if (string.IsNullOrWhiteSpace(finalReasoning))
            return;

        AppendReasoning(finalReasoning);
    }

    private void AppendReasoning(string value)
        => Reasoning = Reasoning is null ? value : Reasoning + value;

    private static string? ReadReasoning(JsonElement message)
    {
        if (message.TryGetProperty("reasoning_content", out var reasoningContent))
            return reasoningContent.GetString();

        if (message.TryGetProperty("thinking", out var thinking))
            return thinking.GetString();

        return null;
    }

    private void ReadMetrics(JsonElement root)
    {
        LoadDurationNs = ReadInt64(root, "load_duration");
        EvalDurationNs = ReadInt64(root, "eval_duration");
        EvalTokens = ReadInt64(root, "eval_count");
        PromptTokens = ReadInt64(root, "prompt_eval_count");

        // Confirma o "done" também quando veio sem content (chunks finais vazios).
        Done = true;
    }

    private static long ReadInt64(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return 0;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out long number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out long parsed) => parsed,
            _ => 0
        };
    }
}