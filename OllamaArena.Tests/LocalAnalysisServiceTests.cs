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
        => new(NullLogger<LocalAnalysisService>.Instance, new FakeLocalizer(),
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
        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => TextoCompleto(l).Contains("Analysis.NoFactualRatings"));
        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => TextoCompleto(l).Contains("Analysis.NoBestRated"));
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

        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => TextoCompleto(l).Contains("Analysis.GeminiTie"));
        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => TextoCompleto(l).Contains("Analysis.OpenRouterTie"));
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

        Assert.Contains(resultado.LinhasAnaliseDetalhada, l => TextoCompleto(l).Contains("Analysis.ConsensusHigh"));
    }

    [Fact]
    public async Task NotasGlobaisZero_CountamComoNaoAvaliadas()
    {
        var servico = CriarServico();
        var modelos = new List<BenchmarkEvaluationModel>
        {
            NovoModelo("ModeloA", 10.0, 1000, 0, null)
        };

        var resultado = await servico.AnalisarBenchmarksAsync(modelos);

        Assert.Equal("Analysis.NoBestRated", resultado.ModeloMelhorAvaliado);
    }

    [Fact]
    public async Task TokenJaCancelado_LancaOperationCanceled()
    {
        var servico = CriarServico();
        var modelos = new List<BenchmarkEvaluationModel> { NovoModelo("ModeloA", 10.0, 1000, 5, 5) };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => servico.AnalisarBenchmarksAsync(modelos, cts.Token));
    }

    [Fact]
    public async Task CancelamentoDuranteAAnalise_Interrompe()
    {
        // Métrica fraca para que o ciclo peça rótulos ao localizador.
        var modelos = Enumerable.Range(0, 50)
            .Select(i => NovoModelo($"Modelo{i}", 10.0 + i, 1000, 3, 2))
            .ToList();

        using var cts = new CancellationTokenSource();

        // Cancelar a meio do trabalho, de forma determinística: o localizador
        // dispara o cancelamento no primeiro rótulo de métrica pedido, que só
        // acontece dentro do ciclo que percorre os benchmarks. Assim prova-se que o
        // token é observado durante a análise e não só à entrada.
        var servico = new LocalAnalysisService(
            NullLogger<LocalAnalysisService>.Instance,
            new CanceladorLocalizer(cts),
            Options.Create(new JudgeScoreWeights()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => servico.AnalisarBenchmarksAsync(modelos, cts.Token));
    }

    [Fact]
    public async Task MetricasFracasDoMesmoModelo_AcumulamEntrePrompts()
    {
        var servico = CriarServico();

        var primeiro = NovoModelo("Repetido", 10.0, 1000, 3, 3);
        primeiro.GeminiFactualRating = 2;
        var segundo = NovoModelo("Repetido", 12.0, 1000, 3, 3);
        segundo.OpenRouterClarityRating = 1;

        var resultado = await servico.AnalisarBenchmarksAsync([primeiro, segundo]);

        var linhaModelo = Assert.Single(
            resultado.LinhasAnaliseDetalhada,
            l => TextoCompleto(l).Contains("Analysis.WeakMetricsModel"));

        // As duas métricas fracas (uma por benchmark) têm de sobreviver às duas.
        Assert.Contains("Evaluation.MetricFactual", TextoCompleto(linhaModelo));
        Assert.Contains("Evaluation.MetricClarity", TextoCompleto(linhaModelo));
    }

    /// <summary>
    /// Rótulo + texto da linha. Os recursos de teste devolvem a própria chave, por
    /// isso o conteúdo marcante identifica a origem da linha.
    /// </summary>
    private static string TextoCompleto(BenchmarkAnalysisLine linha)
        => linha.Rotulo is null ? linha.Texto : linha.Rotulo + linha.Texto;

    [Fact]
    public async Task NomeDeModeloComScript_NAoEntraNoRelatorioComoHTMLExecutavel()
    {
        var servico = CriarServico();
        var nomeHostil = "<img src=x onerror=alert(1)>";

        var modelo = NovoModelo(nomeHostil, 10.0, 1000, 3, 3);
        modelo.GeminiFactualRating = 2;

        var resultado = await servico.AnalisarBenchmarksAsync([modelo]);

        // O serviço devolve rótulo e texto simples e a vista escapa o que imprime,
        // por isso o nome hostil é apresentado ao utilizador tal e qual, como texto —
        // nunca como HTML. Não há stripping: o nome é conteúdo, não marcação.
        Assert.Contains(nomeHostil, resultado.Sumario);

        foreach (var linha in resultado.LinhasAnaliseDetalhada)
        {
            Assert.DoesNotContain("<strong>", linha.Texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<em>", linha.Texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LinhasDetalhada_NaoContemMarcacaoHTML()
    {
        var servico = CriarServico();

        var resultado = await servico.AnalisarBenchmarksAsync(
        [
            NovoModelo("llama3", 40.0, 1200, 5, 5),
            NovoModelo("qwen", 20.0, 2400, 4, 4)
        ]);

        Assert.NotEmpty(resultado.LinhasAnaliseDetalhada);

        foreach (var linha in resultado.LinhasAnaliseDetalhada)
        {
            var completa = TextoCompleto(linha);

            Assert.DoesNotContain("<", completa, StringComparison.Ordinal);
            Assert.DoesNotContain(">", completa, StringComparison.Ordinal);
            Assert.DoesNotContain("&lt;", completa, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("&le;", completa, StringComparison.OrdinalIgnoreCase);
        }
    }

    // A separação rótulo/texto não se testa aqui: o localizer de teste devolve a chave
    // do recurso em vez do seu valor, e portanto nunca há <strong> para extrair.
    // O formato real dos recursos está fixado em BenchmarkAnalysisLineTests.

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

    private class FakeLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name]
        {
            get
            {
                AoTraduzir(name);
                return new LocalizedString(name, name);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                AoTraduzir(name);
                return new LocalizedString(name, name + "(" + string.Join("|", arguments) + ")");
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

        /// <summary>Cada tradução executa a ação de subclasses, para testes de cancelamento.</summary>
        protected virtual void AoTraduzir(string name) { }
    }

    /// <summary>
    /// Dispara o cancelamento na primeira tradução de um rótulo de métrica, ou seja,
    /// a meio do trabalho da análise (e não antes de ela começar).
    /// </summary>
    private sealed class CanceladorLocalizer(CancellationTokenSource cts) : FakeLocalizer
    {
        protected override void AoTraduzir(string name)
        {
            if (name.StartsWith("Evaluation.Metric", StringComparison.Ordinal))
                cts.Cancel();
        }
    }
}
