using OllamaArena.Models.DTO;

namespace OllamaArena.Tests;

/// <summary>
/// Testes da conversão das linhas de análise (recursos localizados) em rótulo + texto
/// simples. As strings usadas aqui são cópias literais de
/// <c>Resources/SharedResources.resx</c>: o objectivo é fixar o contrato entre o
/// formato dos recursos e a vista, que já não recebe HTML.
/// </summary>
public class BenchmarkAnalysisLineTests
{
    [Fact]
    public void MarcadorComRotuloETexto_SeparaORotulo()
    {
        // "• <strong>Velocidade e Tempo:</strong> O modelo llama3 lidera…"
        var linha = BenchmarkAnalysisLine.Criar(
            "• <strong>Velocidade e Tempo:</strong> O modelo llama3 lidera a taxa de geração.");

        Assert.Equal("• Velocidade e Tempo:", linha.Rotulo);
        Assert.Equal("O modelo llama3 lidera a taxa de geração.", linha.Texto);
    }

    [Fact]
    public void MarcadorComEmoji_SobreviveAoRotulo()
    {
        var linha = BenchmarkAnalysisLine.Criar(
            "• ⚠️ <strong>Alerta de Desempenho:</strong> Nenhum dos modelos atingiu a excelência.");

        Assert.Equal("• ⚠️ Alerta de Desempenho:", linha.Rotulo);
        Assert.StartsWith("Nenhum dos modelos", linha.Texto);
    }

    [Fact]
    public void RotuloNoInicio_ComPlaceholdersInterpostos()
    {
        // "Analysis.WeakMetricsModel": "<strong>{0}:</strong> {1}"
        var linha = BenchmarkAnalysisLine.Criar("<strong>llama3:</strong> factual, coerência");

        Assert.Equal("llama3:", linha.Rotulo);
        Assert.Equal("factual, coerência", linha.Texto);
    }

    [Fact]
    public void LinhaSoComRotulo_FicaSemTexto()
    {
        // "Analysis.WeakMetricsHeader": "• <strong>Métricas com Desempenho Inferior (≤ 3):</strong>"
        var linha = BenchmarkAnalysisLine.Criar(
            "• <strong>Métricas com Desempenho Inferior (&le; 3):</strong>");

        Assert.Equal("• Métricas com Desempenho Inferior (≤ 3):", linha.Rotulo);
        Assert.Equal(string.Empty, linha.Texto);
    }

    [Fact]
    public void FraseSemMarcacao_FicaComoTextoSemRotulo()
    {
        // "Analysis.WeakMetricsNone": "Todas as métricas individuais atingiram nota ≥ 4."
        var linha = BenchmarkAnalysisLine.Criar("Todas as métricas individuais atingiram nota &ge; 4.");

        Assert.Null(linha.Rotulo);
        Assert.Equal("Todas as métricas individuais atingiram nota ≥ 4.", linha.Texto);
    }

    [Fact]
    public void TextoAntesDoRotulo_NaoEhConsideradoRotulo()
    {
        // Só há rótulo quando o <strong> abre a linha; com texto antes é uma frase.
        var linha = BenchmarkAnalysisLine.Criar("Veja <strong>o resumo</strong> abaixo.");

        Assert.Null(linha.Rotulo);
        Assert.Equal("Veja o resumo abaixo.", linha.Texto);
    }

    [Fact]
    public void MarcacaoInlineNoCorpo_ERetirada()
    {
        // "Analysis.DetailRecommendationSplit" tem <em> e um segundo <strong> no corpo.
        var linha = BenchmarkAnalysisLine.Criar(
            "• <strong>Recomendação por Prompt:</strong> Para <em>prompts simples</em>, "
            + "prefira o <strong>llama3</strong> (2x mais rápido).");

        Assert.Equal("• Recomendação por Prompt:", linha.Rotulo);
        Assert.Equal(
            "Para prompts simples, prefira o llama3 (2x mais rápido).",
            linha.Texto);
    }

    [Theory]
    [InlineData("&le;", "≤")]
    [InlineData("&ge;", "≥")]
    [InlineData("&amp;", "&")]
    public void EntidadesDosRecursos_SaoDescodificadas(string entidade, string esperado)
    {
        var linha = BenchmarkAnalysisLine.Criar($"<strong>Rótulo:</strong> valor {entidade} fim");

        Assert.Equal($"valor {esperado} fim", linha.Texto);
    }

    [Fact]
    public void EntidadeDeEspaco_ColapsaComOsEspacosRedundantes()
    {
        // "&nbsp;" vira um espaço e os espaços em excesso das tags removidas
        // colapsam: "a &nbsp; b" e "a   b" têm de dar o mesmo texto.
        var linha = BenchmarkAnalysisLine.Criar("<strong>Rótulo:</strong> a &nbsp;  b");

        Assert.Equal("a b", linha.Texto);
    }

    [Fact]
    public void EspacosRepetidos_DeixamDeSerDuplicados()
    {
        // Remover a marcação deixaria espaços duplos.
        var linha = BenchmarkAnalysisLine.Criar("<strong>A:</strong>  <em>x</em>   y ");

        Assert.Equal("A:", linha.Rotulo);
        Assert.Equal("x y", linha.Texto);
    }

    [Fact]
    public void NomeDeModeloHostil_FicaTextoLiteral()
    {
        // O nome vem da BD. A vista escapa o texto, por isso não há stripping de
        // tags desconhecidas: o nome tem de aparecer tal e qual, não ser amputado.
        var nome = "<img src=x onerror=alert(1)>";
        var linha = BenchmarkAnalysisLine.Criar($"<strong>{nome}:</strong> 5");

        Assert.Equal($"{nome}:", linha.Rotulo);
        Assert.Equal("5", linha.Texto);
    }

    [Fact]
    public void NomeDeModeloHostilNoCorpo_NaoComandaORotulo()
    {
        var nome = "<b>evil</b>";
        var linha = BenchmarkAnalysisLine.Criar($"<strong>Métricas:</strong> {nome} ficou fraco");

        Assert.Equal("Métricas:", linha.Rotulo);
        // Só a formatação inline é retirada; o resto é conteúdo do modelo.
        Assert.Equal("evil ficou fraco", linha.Texto);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EntradasVazias_ProduzemLinhaVazia(string? entrada)
    {
        var linha = BenchmarkAnalysisLine.Criar(entrada);

        Assert.Null(linha.Rotulo);
        Assert.Equal(string.Empty, linha.Texto);
    }
}