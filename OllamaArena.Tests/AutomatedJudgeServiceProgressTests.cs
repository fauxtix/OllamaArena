using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaArena.Services;

namespace OllamaArena.Tests;

/// <summary>
/// Contrato de progresso/cancelamento de <see cref="AutomatedJudgeService"/>: a UI
/// depende de saber quando cada juiz começa e termina, e de conseguir abortar os pedidos.
/// </summary>
public class AutomatedJudgeServiceProgressTests
{
    private const string ChaveGemini = "chave-gemini-longa-o-bastante";
    private const string ChaveOpenRouter = "chave-openrouter";
    private const string RespostaGemini = """{"candidates":[{"content":{"parts":[{"text":"FINAL_SCORE: 4"}]}}]}""";
    private const string RespostaOpenRouter = """{"choices":[{"message":{"content":"FINAL_SCORE: 4"}}]}""";

    // A quota entre avaliações é estática e por processo (sobrevive a recriações do
    // serviço), por isso os cenários que consomem quota esperam pelo intervalo mínimo
    // (1 s na configuração) e ficam independentes da ordem de execução na classe.
    private static Task CumprirIntervaloAsync() => Task.Delay(1_300);

    [Fact]
    public async Task Progresso_ReportaInicioEFimDeCadaJuiz()
    {
        await CumprirIntervaloAsync();

        var factory = new StubHttpClientFactory();
        factory.Adicionar("Gemini", new StubHttpMessageHandler(HttpStatusCode.OK, RespostaGemini));
        factory.Adicionar("OpenRouter", new StubHttpMessageHandler(HttpStatusCode.OK, RespostaOpenRouter));

        var servico = CriarServico(factory,
            ("ApiKeys:Gemini", ChaveGemini),
            ("ApiKeys:OpenRouter", ChaveOpenRouter));

        var eventos = new ConcurrentQueue<JudgeProgressEvent>();
        var resultado = await servico.GetFeedbacksAutomatizadosAsync("prompt", eventos.Enqueue);

        Assert.True(resultado.Gemini.Success);
        Assert.True(resultado.OpenRouter.Success);

        var lista = eventos.ToList();
        Assert.Contains(lista, e => e.Step == JudgeProgressStep.DefinicoesResolvidas);
        Assert.Contains(lista, e => e.Step == JudgeProgressStep.QuotaVerificada);
        Assert.Contains(lista, e => e.Step == JudgeProgressStep.Concluido);

        foreach (var juiz in new[] { AutomatedJudgeService.JuizGemini, AutomatedJudgeService.JuizOpenRouter })
        {
            var inicio = Assert.Single(lista, e => e.Juiz == juiz && e.Step == JudgeProgressStep.JuizIniciado);
            var fim = Assert.Single(lista, e => e.Juiz == juiz && e.Step == JudgeProgressStep.JuizConcluido);
            Assert.True(lista.IndexOf(inicio) < lista.IndexOf(fim), $"'{juiz}' terminou antes de começar.");
        }
    }

    [Fact]
    public async Task Progresso_AmbosOsJuizesComErro_ReportaFalhaDeCadaUm()
    {
        var factory = new StubHttpClientFactory();
        factory.Adicionar("Gemini", new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "erro"));
        factory.Adicionar("OpenRouter", new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "erro"));

        var servico = CriarServico(factory,
            ("ApiKeys:Gemini", ChaveGemini),
            ("ApiKeys:OpenRouter", ChaveOpenRouter));

        var eventos = new ConcurrentQueue<JudgeProgressEvent>();
        var resultado = await servico.GetFeedbacksAutomatizadosAsync("prompt", eventos.Enqueue);

        Assert.False(resultado.Gemini.Success);
        Assert.False(resultado.OpenRouter.Success);
        Assert.True(resultado.HasErrors);

        var lista = eventos.ToList();
        foreach (var juiz in new[] { AutomatedJudgeService.JuizGemini, AutomatedJudgeService.JuizOpenRouter })
        {
            var falha = Assert.Single(lista, e => e.Juiz == juiz && e.Step == JudgeProgressStep.JuizFalhou);
            Assert.Contains("Erro HTTP", falha.Detalhe!);
        }
    }

    [Fact]
    public async Task Progresso_SemChaveDoGemini_ReportaFalhaSemChamarAApi()
    {
        var factory = new StubHttpClientFactory();
        factory.Adicionar("OpenRouter", new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "erro"));

        var servico = CriarServico(factory, ("ApiKeys:OpenRouter", ChaveOpenRouter));

        var eventos = new ConcurrentQueue<JudgeProgressEvent>();
        await servico.GetFeedbacksAutomatizadosAsync("prompt", eventos.Enqueue);

        // O juiz sem chave é reportado logo como falhado (não fica à espera) e
        // consome zero requisições: nada é enviado para a API do Gemini.
        var falha = Assert.Single(eventos, e =>
            e.Juiz == AutomatedJudgeService.JuizGemini && e.Step == JudgeProgressStep.JuizFalhou);
        Assert.Contains("chave", falha.Detalhe!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(eventos, e => e.Juiz == AutomatedJudgeService.JuizGemini && e.Step == JudgeProgressStep.JuizIniciado);
        Assert.Equal(0, factory.PedidosPorNome.GetValueOrDefault("Gemini"));
    }

    [Fact]
    public async Task Progresso_ModeloIndisponivel_ReportaModeloAlternativoDoFallback()
    {
        await CumprirIntervaloAsync();

        var factory = new StubHttpClientFactory();
        factory.Adicionar("Gemini", new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "erro"));
        factory.Adicionar("OpenRouter", new StubHttpMessageHandler(
            (HttpStatusCode.NotFound, "modelo removido"),
            (HttpStatusCode.OK, RespostaOpenRouter)));

        var servico = CriarServico(factory,
            ("ApiKeys:Gemini", ChaveGemini),
            ("ApiKeys:OpenRouter", ChaveOpenRouter),
            ("AutomatedJudge:OpenRouterModel", "org/modelo-removido:free"));

        var eventos = new ConcurrentQueue<JudgeProgressEvent>();
        var resultado = await servico.GetFeedbacksAutomatizadosAsync("prompt", eventos.Enqueue);

        var fallback = Assert.Single(eventos, e => e.Step == JudgeProgressStep.ModeloAlternativo);
        Assert.Equal(AutomatedJudgeService.JuizOpenRouter, fallback.Juiz);
        Assert.Equal("openrouter/free", fallback.Detalhe);

        // O Gemini falhou, mas o fallback do OpenRouter devolveu avaliação.
        Assert.False(resultado.Gemini.Success);
        Assert.True(resultado.OpenRouter.Success);
    }

    [Fact]
    public async Task Cancelar_AbortaOsPedidosEmCurso()
    {
        await CumprirIntervaloAsync();

        var factory = new StubHttpClientFactory();
        factory.Adicionar("Gemini", new BlockingHttpMessageHandler());
        factory.Adicionar("OpenRouter", new BlockingHttpMessageHandler());

        var servico = CriarServico(factory,
            ("ApiKeys:Gemini", ChaveGemini),
            ("ApiKeys:OpenRouter", ChaveOpenRouter));

        var eventos = new ConcurrentQueue<JudgeProgressEvent>();
        var algumJuizIniciou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Reportar(JudgeProgressEvent evento)
        {
            eventos.Enqueue(evento);
            if (evento.Step == JudgeProgressStep.JuizIniciado)
                algumJuizIniciou.TrySetResult();
        }

        using var cts = new CancellationTokenSource();
        var avaliacao = servico.GetFeedbacksAutomatizadosAsync("prompt", Reportar, cts.Token);

        await algumJuizIniciou.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        // O cancelamento propaga (não é convertido em "falha" do juiz), para a UI
        // distinguir "o utilizador cancelou" de "os juízes falharam".
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => avaliacao);

        var lista = eventos.ToList();
        Assert.DoesNotContain(lista, e => e.Step == JudgeProgressStep.Concluido);
        Assert.Contains(lista, e => e.Step == JudgeProgressStep.JuizFalhou && e.Detalhe == "Cancelado.");
    }

    private static AutomatedJudgeService CriarServico(
        StubHttpClientFactory factory,
        params (string Chave, string Valor)[] settings)
    {
        var configuration = new ConfigurationManager();
        configuration["AutomatedJudge:MinIntervalSeconds"] = "1";

        return new AutomatedJudgeService(
            factory,
            configuration,
            new FakeSettingsRepository(settings),
            NullLogger<AutomatedJudgeService>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Dictionary<string, HttpClient> _clientes = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, int> PedidosPorNome { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Adicionar(string nome, HttpMessageHandler handler)
            => _clientes[nome] = new HttpClient(handler);

        public HttpClient CreateClient(string name)
        {
            PedidosPorNome[name] = PedidosPorNome.GetValueOrDefault(name) + 1;
            return _clientes[name];
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Json)> _respostas;

        public StubHttpMessageHandler(HttpStatusCode status, string json)
            : this([(status, json)])
        {
        }

        public StubHttpMessageHandler(params (HttpStatusCode Status, string Json)[] respostas)
        {
            _respostas = new Queue<(HttpStatusCode, string)>(respostas);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, json) = _respostas.Count > 0
                ? _respostas.Dequeue()
                : (HttpStatusCode.OK, "{}");

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class BlockingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
