using OllamaArena.Services.Helpers;
using System.Text.Json;

namespace OllamaArena.Tests;

/// <summary>
/// Testes do acumulador de chunks NDJSON do /api/chat. O ponto sensível é o chunk final:
/// o Ollama envia <c>done: true</c> com as métricas e <b>sem</b> mensagem, pelo que a leitura
/// de métricas não pode ficar atrás de um "return" por falta de texto — caso contrário o
/// turno nunca tiveria tokens reais para o benchmark nem para a barra de contexto.
/// </summary>
public class ChatStreamAccumulatorTests
{
    private static JsonElement Chunk(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public void Accumulate_ConcatenaTextoDeVariosChunks()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: false);

        acc.Accumulate(Chunk("""{"message":{"role":"assistant","content":"Olá"}}"""));
        acc.Accumulate(Chunk("""{"message":{"role":"assistant","content":", mundo"}}"""));

        Assert.Equal("Olá, mundo", acc.Text);
        Assert.False(acc.Done);
    }

    [Fact]
    public void Accumulate_AcumulaReasoningContent()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: true);

        acc.Accumulate(Chunk("""{"message":{"content":"a","reasoning_content":"pensamento 1 "}}"""));
        acc.Accumulate(Chunk("""{"message":{"content":"b","reasoning_content":"pensamento 2"}}"""));

        Assert.Equal("pensamento 1 pensamento 2", acc.Reasoning);
    }

    [Fact]
    public void Accumulate_UsaThinkingComoFallbackDeReasoningContent()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: true);

        acc.Accumulate(Chunk("""{"message":{"content":"a","thinking":"via thinking"}}"""));

        Assert.Equal("via thinking", acc.Reasoning);
    }

    [Fact]
    public void Accumulate_ComRaciocinioDesligado_NaoGuardaReasoning()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: false);

        acc.Accumulate(Chunk("""{"message":{"content":"a","reasoning_content":"segredo"}}"""));

        Assert.Null(acc.Reasoning);
    }

    [Fact]
    public void Accumulate_ChunkFinalSemMensagem_LemMétricasEMarcaDone()
    {
        // Chunk final real do Ollama: sem "message", com "done": true e as métricas.
        var acc = new ChatStreamAccumulator(captureReasoning: false);

        acc.Accumulate(Chunk("""{"message":{"role":"assistant","content":"resposta"}}"""));
        acc.Accumulate(Chunk("""
            {"model":"m","done":true,"done_reason":"stop",
             "total_duration":3000,"load_duration":500,
             "prompt_eval_count":120,"prompt_eval_duration":400,
             "eval_count":80,"eval_duration":2000}
            """));

        Assert.True(acc.Done);
        Assert.Equal(120, acc.PromptTokens);
        Assert.Equal(80, acc.EvalTokens);
        Assert.Equal(2000, acc.EvalDurationNs);
        Assert.Equal(500, acc.LoadDurationNs);
        Assert.Equal(200, acc.TotalTokens);
        Assert.Equal("resposta", acc.Text);
    }

    [Fact]
    public void Accumulate_MétricasComoStringSaoLidas()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: false);

        acc.Accumulate(Chunk("""{"done":true,"prompt_eval_count":"42","eval_count":"8"}"""));

        Assert.True(acc.Done);
        Assert.Equal(50, acc.TotalTokens);
    }

    [Fact]
    public void ReadFinalReasoning_CapturaRaciocínioQueSóVemNoChunkFinal()
    {
        // qwq / deepseek-r1: o raciocínio chega isolado na mensagem do chunk "done".
        var acc = new ChatStreamAccumulator(captureReasoning: true);
        var final = Chunk("""{"message":{"content":"resposta","reasoning_content":"só no fim"},"done":true}""");

        acc.Accumulate(final);
        acc.ReadFinalReasoning(final);

        Assert.Equal("só no fim", acc.Reasoning);
    }

    [Fact]
    public void ReadFinalReasoning_NãoDuplicaRaciocínioJáContabilizado()
    {
        // Regressão: o raciocínio do chunk era somado por Accumulate e outra vez por
        // ReadFinalReasoning, duplicando o texto no histórico e na persistência.
        var acc = new ChatStreamAccumulator(captureReasoning: true);
        var chunk = Chunk("""{"message":{"content":"resposta","reasoning_content":"pensamento"},"done":true}""");

        acc.Accumulate(chunk);
        acc.ReadFinalReasoning(chunk);

        Assert.Equal("pensamento", acc.Reasoning);
    }

    [Fact]
    public void Accumulate_ComRaciocinioDesligado_ReadFinalReasoningNaoFazNada()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: false);
        var final = Chunk("""{"message":{"content":"resposta","reasoning_content":"ignorado"},"done":true}""");

        acc.Accumulate(final);
        acc.ReadFinalReasoning(final);

        Assert.Null(acc.Reasoning);
    }

    [Fact]
    public void Accumulate_FallbackParaResponseDoApiGenerate()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: false);

        acc.Accumulate(Chunk("""{"response":"texto do /api/generate","done":false}"""));

        Assert.Equal("texto do /api/generate", acc.Text);
        Assert.True(acc.ReceivedText);
    }

    [Fact]
    public void Accumulate_ChunkVazioNãoMarcaTextoRecebido()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: true);

        acc.Accumulate(Chunk("""{"message":{"role":"assistant","content":""}}"""));

        Assert.False(acc.ReceivedText);
        Assert.Equal(string.Empty, acc.Text);
    }

    [Fact]
    public void Accumulate_EstimaTokensDeSaídaParaABarraDeContexto()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: false);

        acc.Accumulate(Chunk("""{"message":{"content":"uma frase qualquer"}}"""));

        Assert.True(acc.EstimatedOutputTokens > 0);
    }

    [Fact]
    public void RecordMalformedLine_ContaLinhasIlegíveis()
    {
        var acc = new ChatStreamAccumulator(captureReasoning: false);

        acc.RecordMalformedLine();
        acc.RecordMalformedLine();

        Assert.Equal(2, acc.MalformedLines);
    }
}