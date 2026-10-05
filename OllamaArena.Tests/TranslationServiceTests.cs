using System.Net;
using System.Data;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;
using OllamaArena.Services;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Implementations.Services;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;

namespace OllamaArena.Tests;

/// <summary>
/// Cadeia de tradução OpenRouter → Gemini → Ollama: ordem de preferência, passagens
/// de fallback, eventos de progresso e cancelamento.
/// </summary>
public class TranslationServiceTests : IDisposable
{
    private const string ChaveValida = "chave-de-teste-1234567890";
    private const string ModeloOpenRouter = "openrouter/free";
    private const string ModeloLocal = "modelo-local";

    private readonly string _raiz;
    private readonly PromptFilesService _prompts;

    public TranslationServiceTests()
    {
        _raiz = Directory.CreateTempSubdirectory("ollama-arena-translation").FullName;
        Directory.CreateDirectory(Path.Combine(_raiz, "Prompts"));
        File.WriteAllText(
            Path.Combine(_raiz, "Prompts", "translation-prompt.txt"),
            "Traduz para {{TARGET_LANGUAGE}}: {{TEXT}}");

        _prompts = new PromptFilesService(
            NullLogger<PromptFilesService>.Instance,
            new AmbienteFalso(_raiz));
    }

    [Fact]
    public async Task OpenRouterResponde_EUsaOpenRouterSemTocarNosOutros()
    {
        var factory = new StubHttpClientFactory();
        factory.Adicionar("OpenRouter", HttpStatusCode.OK, EnvelopeOpenRouter("{\"translatedText\": \"Olá, mundo.\", \"confidence\": 0.9}"));

        var servico = CriarServico(factory);
        var resultado = await servico.TranslateAsync("Hello, world.");

        Assert.Equal("Olá, mundo.", resultado.TranslatedText);
        Assert.Equal(TranslationProviders.OpenRouter, resultado.Provider);
        Assert.Equal(ModeloOpenRouter, resultado.ModelUsed);
        Assert.Equal(0, factory.PedidosEm("Gemini"));
        Assert.Equal(0, factory.PedidosEm("Ollama"));
    }

    [Fact]
    public async Task OpenRouterFalha_PassaParaOGemini()
    {
        var factory = new StubHttpClientFactory();
        factory.Adicionar("OpenRouter", HttpStatusCode.InternalServerError, "erro");
        factory.Adicionar("Gemini", HttpStatusCode.OK, EnvelopeGemini("{\"translatedText\": \"Olá, mundo.\", \"confidence\": 0.8}"));

        var servico = CriarServico(factory);
        var eventos = new List<TranslationProgressEvent>();
        var resultado = await servico.TranslateAsync("Hello, world.", eventos.Add);

        Assert.Equal(TranslationProviders.Gemini, resultado.Provider);
        Assert.Equal("Olá, mundo.", resultado.TranslatedText);
        Assert.Equal(0, factory.PedidosEm("Ollama"));

        // A UI vê a falha do OpenRouter e depois o arranque do Gemini.
        Assert.Contains(eventos, e => e.Step == TranslationProgressStep.ProvedorFalhou
                                    && e.Provider == TranslationProviders.OpenRouter);
        Assert.Contains(eventos, e => e.Step == TranslationProgressStep.ProvedorIniciado
                                    && e.Provider == TranslationProviders.Gemini);
    }

    [Fact]
    public async Task SemChaves_UsarOllamaLocal()
    {
        var ollama = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            EnvelopeOllama("{\"translatedText\": \"Olá, mundo.\", \"confidence\": 0.7}"));

        var servico = CriarServico(new StubHttpClientFactory(), ollama, semChaves: true);
        var resultado = await servico.TranslateAsync("Hello, world.");

        Assert.Equal(TranslationProviders.Ollama, resultado.Provider);
        Assert.Equal(ModeloLocal, resultado.ModelUsed);
        Assert.Equal(1, ollama.Pedidos);
    }

    [Fact]
    public async Task RespostaInvalida_ProvedorContaComoFalha()
    {
        var factory = new StubHttpClientFactory();
        // Devolve o texto original (não é tradução): tem de ser recusado.
        factory.Adicionar("OpenRouter", HttpStatusCode.OK, EnvelopeOpenRouter("{\"translatedText\": \"Hello, world.\", \"confidence\": 0.9}"));
        factory.Adicionar("Gemini", HttpStatusCode.OK, EnvelopeGemini("{\"translatedText\": \"Olá, mundo.\", \"confidence\": 0.8}"));

        var servico = CriarServico(factory);
        var resultado = await servico.TranslateAsync("Hello, world.");

        Assert.Equal(TranslationProviders.Gemini, resultado.Provider);
    }

    [Fact]
    public async Task RespostaIgualAoOriginal_ProvedorContaComoFalha()
    {
        var factory = new StubHttpClientFactory();
        // Mesma frase, com caixa e pontuação diferentes: continua a ser uma não-tradução.
        factory.Adicionar("OpenRouter", HttpStatusCode.OK, EnvelopeOpenRouter("{\"translatedText\": \"hello   world\", \"confidence\": 0.9}"));
        factory.Adicionar("Gemini", HttpStatusCode.OK, EnvelopeGemini("{\"translatedText\": \"Olá, mundo.\", \"confidence\": 0.8}"));

        var servico = CriarServico(factory);
        var resultado = await servico.TranslateAsync("Hello, world.");

        Assert.Equal(TranslationProviders.Gemini, resultado.Provider);
    }

    [Fact]
    public async Task TodosOsSistemasFalham_IndicaORazaoDeCadaUm()
    {
        var ollama = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "erro");
        var factory = new StubHttpClientFactory();
        factory.Adicionar("OpenRouter", HttpStatusCode.TooManyRequests, "429");
        factory.Adicionar("Gemini", HttpStatusCode.InternalServerError, "erro");

        var servico = CriarServico(factory, ollama);
        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.TranslateAsync("Hello, world."));

        Assert.Contains(TranslationProviders.OpenRouter, excecao.Message);
        Assert.Contains(TranslationProviders.Gemini, excecao.Message);
        Assert.Contains(TranslationProviders.Ollama, excecao.Message);
    }

    [Fact]
    public async Task Cancelamento_PropagaSemSerConvertidoEmFalha()
    {
        var factory = new StubHttpClientFactory();
        factory.AdicionarBloqueante("OpenRouter");

        var servico = CriarServico(factory);
        using var cts = new CancellationTokenSource();
        var traducao = servico.TranslateAsync("Hello, world.", null, cts.Token);
        await cts.CancelAsync();

        // O cancelamento tem de escapar tal e qual: a UI distingue "o utilizador
        // cancelou" de "a tradução falhou" e não mostra diálogo de erro no primeiro caso.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => traducao);
    }

    private TranslationService CriarServico(
        StubHttpClientFactory factory,
        HttpMessageHandler? ollama = null,
        bool semChaves = false)
    {
        var httpOllama = new HttpClient(ollama ?? new StubHttpMessageHandler(HttpStatusCode.OK, "{}"))
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        var settings = semChaves
            ? new FakeSettingsRepository(("AutomatedJudge:OpenRouterModel", ModeloOpenRouter))
            : new FakeSettingsRepository(
                (ApiCredentialsResolver.ChaveGemini, ChaveValida),
                (ApiCredentialsResolver.ChaveOpenRouter, ChaveValida),
                (ApiCredentialsResolver.ChaveModeloOpenRouter, ModeloOpenRouter));

        var credenciais = new ApiCredentialsResolver(settings, new ConfigurationManager());

        return new TranslationService(
            httpOllama,
            factory,
            credenciais,
            new FakeBenchmarkRepository(),
            NullLogger<TranslationService>.Instance,
            _prompts,
            Options.Create(new OllamaOptions { BaseUrl = "http://localhost:11434" }));
    }

    private static string EnvelopeOpenRouter(string content)
        => $"{{\"choices\": [{{\"message\": {{ \"content\": {JsonEscape(content)} }}}}]}}";

    private static string EnvelopeGemini(string content)
        => $"{{\"candidates\": [{{\"content\": {{ \"parts\": [{{ \"text\": {JsonEscape(content)} }}] }} }}]}}";

    private static string EnvelopeOllama(string content)
        => $"{{\"model\": \"{ModeloLocal}\", \"message\": {{ \"role\": \"assistant\", \"content\": {JsonEscape(content)} }}}}";

    private static string JsonEscape(string value)
        => System.Text.Json.JsonSerializer.Serialize(value);

    public void Dispose()
    {
        try { Directory.Delete(_raiz, recursive: true); } catch (IOException) { }
    }

    private sealed class AmbienteFalso(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "OllamaArena.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Dictionary<string, HttpMessageHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _pedidos = new(StringComparer.OrdinalIgnoreCase);

        public void Adicionar(string nome, HttpStatusCode status, string json)
            => _handlers[nome] = new StubHttpMessageHandler(status, json, () => Contar(nome));

        public void AdicionarBloqueante(string nome)
            => _handlers[nome] = new BlockingHttpMessageHandler(() => Contar(nome));

        public int PedidosEm(string nome)
            => _pedidos.TryGetValue(nome, out var total) ? total : 0;

        private void Contar(string nome) => _pedidos[nome] = PedidosEm(nome) + 1;

        public HttpClient CreateClient(string name)
        {
            var handler = _handlers.TryGetValue(name, out var registado)
                ? registado
                : new StubHttpMessageHandler(HttpStatusCode.NotFound, "sem handler configurado");

            return new HttpClient(handler);
        }
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode status, string json, Action? aoPedir = null) : HttpMessageHandler
    {
        public int Pedidos { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedidos++;
            aoPedir?.Invoke();

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class BlockingHttpMessageHandler(Action? aoPedir = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            aoPedir?.Invoke();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    /// <summary>
    /// Repositório mínimo: a tradução só consulta o melhor modelo (para o Ollama).
    /// Como a dependência passou a ser <c>IBenchmarkStatsRepository</c> e não o
    /// repositório inteiro, o duplo só implementa as cinco agregações.
    /// </summary>
    private sealed class FakeBenchmarkRepository : IBenchmarkStatsRepository
    {
        public Task<string?> GetBestModelAsync() => Task.FromResult<string?>(ModeloLocal);

        public Task<List<ModelRanking>> GetModelRankingAsync() => throw new NotSupportedException();
        public Task<List<ModelChartData>> GetModelChartDataAsync() => throw new NotSupportedException();
        public Task<DashboardStats> GetDashboardStatsAsync() => throw new NotSupportedException();
        public Task<List<ModelScoreTimeline>> GetScoreTimelineAsync() => throw new NotSupportedException();
    }
}
