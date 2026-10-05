using Microsoft.Extensions.Logging.Abstractions;
using OllamaArena.Models.DTO;
using OllamaArena.Services.Implementations.Services;
using OllamaArena.Services.Interfaces.Services;
using System.Net;
using System.Text;

namespace OllamaArena.Tests;

/// <summary>
/// Testes do cliente de streaming contra um <see cref="HttpMessageHandler"/> falso: verificam
/// o consumo do NDJSON, a tolerância a linhas corrompidas, as métricas do chunk final e o
/// cancelamento, sem precisar de um servidor Ollama.
/// </summary>
public class OllamaChatStreamClientTests
{
    private static readonly OllamaChatPayload Payload = new()
    {
        Model = "modelo-teste",
        Messages = [new OllamaChatMessage { Role = "user", Content = "Olá" }],
        Stream = true
    };

    [Fact]
    public async Task SendAsync_ConsomeStreamEDevolveTextoEMétricas()
    {
        var handler = ResponderCom(Ndjson(
            """{"message":{"role":"assistant","content":"Olá"}}""",
            """{"message":{"role":"assistant","content":", tudo bem?"}}""",
            """{"model":"m","done":true,"load_duration":500,"eval_duration":2000,"prompt_eval_count":10,"eval_count":6}"""));

        var result = await handler.CriarCliente()
            .SendAsync(Payload, captureReasoning: false, CancellationToken.None);

        Assert.Equal("Olá, tudo bem?", result.Text);
        Assert.True(result.Done);
        Assert.Equal(10, result.PromptTokens);
        Assert.Equal(6, result.EvalTokens);
        Assert.Equal(2000, result.EvalDurationNs);
        Assert.Equal(500, result.LoadDurationNs);
    }

    [Fact]
    public async Task SendAsync_AcumulaReasoningContent()
    {
        var handler = ResponderCom(Ndjson(
            """{"message":{"content":"a","reasoning_content":"p1 "}}""",
            """{"message":{"content":"b","reasoning_content":"p2"}}""",
            """{"done":true}"""));

        var result = await handler.CriarCliente()
            .SendAsync(Payload, captureReasoning: true, CancellationToken.None);

        Assert.Equal("p1 p2", result.Reasoning);
    }

    [Fact]
    public async Task SendAsync_ToleraLinhasJsonCorrompidas()
    {
        var handler = ResponderCom(Ndjson(
            """{"message":{"content":"antes"}}""",
            "{isto não é json",
            """{"message":{"content":"depois"}}""",
            """{"done":true}"""));

        var result = await handler.CriarCliente()
            .SendAsync(Payload, captureReasoning: false, CancellationToken.None);

        Assert.Equal("antesdepois", result.Text);
        Assert.True(result.Done);
    }

    [Fact]
    public async Task SendAsync_DesisteNaSextaLinhaCorrompida()
    {
        // Política de tolerância: até 5 linhas ilegíveis são ignoradas; a 6.ª aborta o turno
        // para não devolver conteúdo corrompido.
        var linhas = Enumerable.Range(0, 8).Select(i => "{ lixo " + i).ToArray();
        var handler = ResponderCom(Ndjson(linhas));

        var erro = await Assert.ThrowsAsync<ChatStreamException>(() =>
            handler.CriarCliente().SendAsync(Payload, captureReasoning: false, CancellationToken.None));

        Assert.Contains("6 linhas ilegíveis", erro.Message);
    }

    [Fact]
    public async Task SendAsync_ErroHttpLançaChatStreamExceptionComOCorpo()
    {
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("modelo não encontrado")
        });

        var erro = await Assert.ThrowsAsync<ChatStreamException>(() =>
            handler.CriarCliente().SendAsync(Payload, captureReasoning: false, CancellationToken.None));

        Assert.Contains("500", erro.Message);
        Assert.Contains("modelo não encontrado", erro.Message);
    }

    [Fact]
    public async Task SendAsync_CancelamentoPropagaOperationCanceled()
    {
        var handler = ResponderCom(Ndjson("""{"message":{"content":"a"}}""", """{"done":true}"""));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.CriarCliente().SendAsync(Payload, captureReasoning: false, cts.Token));
    }

    [Fact]
    public async Task SendAsync_ProgressoÉPublicadoPeloMenosUmaVez()
    {
        var handler = ResponderCom(Ndjson(
            """{"message":{"content":"a"}}""",
            """{"message":{"content":"b"}}""",
            """{"done":true}"""));

        var publicados = new List<ChatStreamProgress>();

        await handler.CriarCliente().SendAsync(
            Payload,
            captureReasoning: false,
            CancellationToken.None,
            progresso =>
            {
                publicados.Add(progresso);
                return Task.CompletedTask;
            },
            TimeSpan.Zero);

        Assert.Equal("ab", publicados[^1].Text);
    }

    [Fact]
    public async Task SendAsync_EnviaOModeloEOsMensagensNoCorpoDoPost()
    {
        var handler = ResponderCom(Ndjson("""{"done":true}"""));

        await handler.CriarCliente().SendAsync(Payload, captureReasoning: false, CancellationToken.None);

        Assert.NotNull(handler.CorpoEnviado);
        Assert.Contains("modelo-teste", handler.CorpoEnviado);
        Assert.Contains("user", handler.CorpoEnviado);
        Assert.Equal("http://localhost:11434/api/chat", handler.UriPedido?.ToString());
    }

    private static string Ndjson(params string[] linhas)
        => string.Join("\n", linhas) + "\n";

    private static HandlerFalso ResponderCom(string corpo)
        => new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo) });

    /// <summary>Handler que responde com um corpo NDJSON pré-definido, sem servidor real.</summary>
    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> resposta) : HttpMessageHandler
    {
        public string? CorpoEnviado { get; private set; }

        public Uri? UriPedido { get; private set; }

        public OllamaChatStreamClient CriarCliente()
            => new(new FabricaHttpTeste(this), NullLogger<OllamaChatStreamClient>.Instance);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UriPedido = request.RequestUri;

            if (request.Content is not null)
                CorpoEnviado = await request.Content.ReadAsStringAsync(cancellationToken);

            return resposta(request);
        }
    }

    private sealed class FabricaHttpTeste(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            // O cliente "Ollama" real recebe BaseAddress em Program.cs; o cliente usa
            // o URI relativo "api/chat".
            BaseAddress = new Uri("http://localhost:11434/")
        };
    }
}