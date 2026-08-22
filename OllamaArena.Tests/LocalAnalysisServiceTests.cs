using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Resources;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Implementations.Services;

namespace OllamaArena.Tests;

public class LocalAnalysisServiceTests
{
    private static LocalAnalysisService CriarServico()
        => new(null!, NullLogger<LocalAnalysisService>.Instance, new FakeLocalizer(),
            Options.Create(new JudgeScoreWeights()));

    [Fact]
    public async Task ListaVazia_DevolveNoDataSemCrash()
    {
        var servico = CriarServico();

        var resultado = await servico.AnalisarBenchmarksAsync([]);

        Assert.Equal("Analysis.NoData", resultado.Sumario);
        Assert.Empty(resultado.LinhasAnaliseDetalhada);
        Assert.Equal(string.Empty, resultado.ModeloMelhorAvaliado);
    }

    [Fact]
    public async Task ListaNula_DevolveNoDataSemCrash()
    {
        var servico = CriarServico();

        var resultado = await servico.AnalisarBenchmarksAsync(null!);

        Assert.Equal("Analysis.NoData", resultado.Sumario);
    }

    [Fact]
    public async Task SemAvaliacoesFactuais_NaoRebenta()
    {
        var servico = CriarServico();
        var modelos = new List<BenchmarkEvaluationModel>
        {
            NovoModelo("ModeloA", 10.0, 1000, null, null),
            NovoModelo("ModeloB", 20.0, 500, null, null)
        };

        var resultado = await servico.AnalisarBenchmarksAsync(modelos);

        Assert.Equal("Analysis.NoBestRated", resultado.ModeloMelhorAvaliado);
        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => l.Contains("Analysis.NoFactualRatings"));
        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => l.Contains("Analysis.NoBestRated"));
    }

    [Fact]
    public async Task ModeloMaisRapido_Identificado()
    {
        var servico = CriarServico();
        var modelos = new List<BenchmarkEvaluationModel>
        {
            NovoModelo("Lento", 5.0, 4000, null, null),
            NovoModelo("Rapido", 40.0, 500, null, null)
        };

        var resultado = await servico.AnalisarBenchmarksAsync(modelos);

        Assert.Equal("Rapido", resultado.ModeloMaisRapido);
        Assert.Equal(40.0, resultado.MaxTokensSec);
    }

    [Fact]
    public async Task MelhorAvaliado_IdentificadoQuandoExistemNotas()
    {
        var servico = CriarServico();
        var modelos = new List<BenchmarkEvaluationModel>
        {
            NovoModelo("Fraco", 10.0, 1000, 2, 2),
            NovoModelo("Topo", 8.0, 1200, 5, 5)
        };

        var resultado = await servico.AnalisarBenchmarksAsync(modelos);

        Assert.Contains("Topo", resultado.ModeloMelhorAvaliado);
    }

    [Fact]
    public async Task EmpateFactual_ProduzTextoDeEmpate()
    {
        var servico = CriarServico();
        var modelos = new List<BenchmarkEvaluationModel>
        {
            NovoModelo("ModeloA", 10.0, 1000, 5, 5),
            NovoModelo("ModeloB", 20.0, 500, 5, 5)
        };

        var resultado = await servico.AnalisarBenchmarksAsync(modelos);

        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => l.Contains("Analysis.GeminiTie"));
        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => l.Contains("Analysis.OpenRouterTie"));
    }

    [Fact]
    public async Task AvaliacoesConvergentes_ProduzemConsensoAlto()
    {
        var servico = CriarServico();
        var modelos = new List<BenchmarkEvaluationModel>
        {
            NovoModelo("ModeloA", 10.0, 1000, 5, 5)
        };

        var resultado = await servico.AnalisarBenchmarksAsync(modelos);

        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => l.Contains("Analysis.ConsensusHigh"));
    }

    private static BenchmarkEvaluationModel NovoModelo(
        string nome, double tokensSeg, double tempoMs, int? notaGeral, int? notaFactual)
        => new()
        {
            Id = nome.GetHashCode(),
            PromptId = 1,
            ResponseId = nome.GetHashCode(),
            DataCriacao = DateTime.Now,
            NomeModelo = nome,
            Descricao = nome,
            TextoPrompt = "Prompt de teste",
            Temperatura = 0.3,
            TokensPorSegundo = tokensSeg,
            TempoPuroMs = tempoMs,
            TempoCargaMs = 0,
            TamanhoTokens = 100,
            GeminiRating = notaGeral ?? 0,
            OpenRouterRating = notaGeral ?? 0,
            GeminiFactualRating = notaFactual,
            OpenRouterFactualRating = notaFactual
        };

    private sealed class FakeLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, name + "(" + string.Join("|", arguments) + ")");

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
