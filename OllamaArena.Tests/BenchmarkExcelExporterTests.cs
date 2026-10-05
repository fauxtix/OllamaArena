using ClosedXML.Excel;
using Microsoft.Extensions.Localization;
using OllamaArena.Models.DTO;
using OllamaArena.Resources;
using OllamaArena.Services.Implementations.Services;

namespace OllamaArena.Tests;

/// <summary>
/// Testes da exportação Excel dos benchmarks. O ponto central é que os bytes já não
/// passam pelo circuito SignalR (o base64 contava com o limite de 32 MB e rebentava
/// em ficheiros grandes): o exportador grava o ficheiro e o endpoint HTTP serve-o.
/// </summary>
public class BenchmarkExcelExporterTests
{
    [Fact]
    public async Task GerarAsync_EscreveFicheiroQueEUmXlsxValido()
    {
        var exporter = new BenchmarkExcelExporter(new FakeLocalizer());
        var dados = new[] { Linha(ResponseId: 1, PromptId: 10, Modelo: "llama3") };

        var download = await exporter.GerarAsync(dados, "Benchmarks_Agrupados");

        try
        {
            Assert.EndsWith(".xlsx", download.NomeFicheiro);
            Assert.Equal("Benchmarks_Agrupados.xlsx", download.NomeFicheiro);
            Assert.True(Guid.TryParseExact(download.Identificador, "N", out _));

            var caminho = BenchmarkExcelExporter.ResolverCaminho(download.Identificador);
            Assert.NotNull(caminho);
            Assert.True(File.Exists(caminho));

            // Assinatura de um .xlsx (ZIP) e uma folha que abre sem erro.
            var cabecalho = File.ReadAllBytes(caminho!).Take(2).ToArray();
            Assert.Equal([0x50, 0x4B], cabecalho); // "PK"

            using var workbook = new XLWorkbook(caminho!);
            var folha = workbook.Worksheet("Benchmarks");

            Assert.Contains("llama3", folha.CellsUsed().Select(c => c.GetString()));
        }
        finally
        {
            Apagar(download.Identificador);
        }
    }

    [Fact]
    public async Task GerarAsync_SemDados_ProduzWorkbookVazioValido()
    {
        var exporter = new BenchmarkExcelExporter(new FakeLocalizer());

        var download = await exporter.GerarAsync([], "vazio");

        try
        {
            var caminho = BenchmarkExcelExporter.ResolverCaminho(download.Identificador);

            using var workbook = new XLWorkbook(caminho!);
            Assert.NotNull(workbook.Worksheet("Benchmarks"));
        }
        finally
        {
            Apagar(download.Identificador);
        }
    }

    [Fact]
    public async Task GerarAsync_DadosNulos_ArgumentNullException()
    {
        var exporter = new BenchmarkExcelExporter(new FakeLocalizer());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => exporter.GerarAsync(null!, "x"));
    }

    [Fact]
    public async Task GerarAsync_CadaExecricaoTemIdentificadorDistinto()
    {
        var exporter = new BenchmarkExcelExporter(new FakeLocalizer());
        var dados = new[] { Linha(ResponseId: 1, PromptId: 10, Modelo: "llama3") };

        var primeiro = await exporter.GerarAsync(dados, "a");
        var segundo = await exporter.GerarAsync(dados, "b");

        try
        {
            Assert.NotEqual(primeiro.Identificador, segundo.Identificador);
        }
        finally
        {
            Apagar(primeiro.Identificador);
            Apagar(segundo.Identificador);
        }
    }

    [Fact]
    public async Task GerarAsync_Cancelado_NaoEscreveFicheiro()
    {
        var exporter = new BenchmarkExcelExporter(new FakeLocalizer());
        var dados = new[] { Linha(ResponseId: 1, PromptId: 10, Modelo: "llama3") };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => exporter.GerarAsync(dados, "cancelado", cts.Token));
    }

    /// <summary>
    /// O endpoint reconstrói o caminho a partir do identificador. Se isso aceitasse
    /// texto livre, um "../../appsettings.json" seria servido como se fosse uma
    /// exportação.
    /// </summary>
    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("..%2f..%2fappsettings.json")]
    [InlineData("nao-e-um-guid")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("00000000-0000-0000-0000-000000000000")] // Guid com hífenes: formato inesperado
    public void ResolverCaminho_RejeitaIdentificadoresForaDoFormato(string identificador)
    {
        Assert.Null(BenchmarkExcelExporter.ResolverCaminho(identificador));
    }

    [Fact]
    public void ResolverCaminho_AceitaGuidNoFormatoN()
    {
        var id = Guid.NewGuid().ToString("N");

        var caminho = BenchmarkExcelExporter.ResolverCaminho(id);

        Assert.NotNull(caminho);
        Assert.Equal(id + ".xlsx", Path.GetFileName(caminho!));
        Assert.DoesNotContain("..", caminho!);
    }

    [Fact]
    public async Task LimparTemporariosAsync_NaoApagaFicahaRecemCriada()
    {
        var exporter = new BenchmarkExcelExporter(new FakeLocalizer());
        var download = await exporter.GerarAsync([], "recem");

        try
        {
            await exporter.LimparTemporariosAsync();

            var caminho = BenchmarkExcelExporter.ResolverCaminho(download.Identificador);
            Assert.True(File.Exists(caminho));
        }
        finally
        {
            Apagar(download.Identificador);
        }
    }

    private static void Apagar(string identificador)
    {
        var caminho = BenchmarkExcelExporter.ResolverCaminho(identificador);

        if (caminho is not null && File.Exists(caminho))
            File.Delete(caminho);
    }

    private static BenchmarkEvaluationModel Linha(int ResponseId, int PromptId, string Modelo) => new()
    {
        ResponseId = ResponseId,
        PromptId = PromptId,
        NomeModelo = Modelo,
        TextoPrompt = "Traduz este parágrafo.",
        Descricao = "Tradução",
        GeminiRating = 4,
        OpenRouterRating = 3,
        TokensPorSegundo = 42.5,
        TamanhoTokens = 120,
        TempoPuroMs = 1500,
        TempoCargaMs = 200,
        DataCriacao = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>Devolve a própria chave, para o teste não depender do idioma activo.</summary>
    private sealed class FakeLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, name + "(" + string.Join("|", arguments) + ")");

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
