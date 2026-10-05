using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

public class ScoreCalculatorTests
{
    [Fact]
    public void TodasMetricasMaximas_DevolveCinco()
    {
        var input = Todas(5);

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Equal(5.0, resultado);
    }

    [Fact]
    public void TodasMetricasMinimas_DevolveUm()
    {
        var input = Todas(1);

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Equal(1.0, resultado);
    }

    [Fact]
    public void PesosDefault_ProduzemMediaPonderadaEsperada()
    {
        // Factual=20 (peso alto) com nota 1 vs restantes a 5 -> score abaixo de 4
        var input = Exceto(5, (JudgeMetricKey.Factual, 1));

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        // (1*20 + 5*80) / 100 = 4.20
        Assert.Equal(4.20, resultado);
    }

    [Fact]
    public void MetricasEmFalta_RenormalizaPesos()
    {
        // 11 métricas presentes (falta LoopDetection). Factual=1 vs resto a 5.
        // Sem renormalização (divisão por 100) daria 3.95; renormalizado (por 95) dá 4.16.
        var input = Exceto(5, (JudgeMetricKey.Factual, 1), (JudgeMetricKey.LoopDetection, null));

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Equal(4.16, resultado);
    }

    [Fact]
    public void MenosDeOitoMetricas_DevolveNull()
    {
        var input = Com(
            (JudgeMetricKey.Factual, 3),
            (JudgeMetricKey.Formatting, 4));

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Null(resultado);
    }

    [Fact]
    public void SomaPesosAbaixoDoMinimo_DevolveNull()
    {
        // Apenas métricas de peso baixo (Tone+Conciseness+LanguageConsistency+LoopDetection = 20) -> < 50
        var input = Com(
            (JudgeMetricKey.Tone, 3),
            (JudgeMetricKey.Conciseness, 3),
            (JudgeMetricKey.LanguageConsistency, 3),
            (JudgeMetricKey.LoopDetection, 3));

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Null(resultado);
    }

    [Fact]
    public void HaloEffect_ComPesoZero_NaoInfluiNoScore()
    {
        var comHaloBaixo = Exceto(4, (JudgeMetricKey.HaloEffect, 1));
        var comHaloAlto = Exceto(4, (JudgeMetricKey.HaloEffect, 5));

        var a = ScoreCalculator.CalcularScoreFinal(comHaloBaixo);
        var b = ScoreCalculator.CalcularScoreFinal(comHaloAlto);

        Assert.Equal(a, b);
    }

    [Fact]
    public void EntradaNula_DevolveNull()
    {
        Assert.Null(ScoreCalculator.CalcularScoreFinal(null!));
    }

    [Fact]
    public void RecusaCorreta_ExcluiFactualComplianceRelevanceDaPonderacao()
    {
        // Perfil típico de recusa correta: Safety 5 mas Factual/Compliance/Relevance baixos
        // (sem conteúdo substantivo). Sem exclusão o score seria 3.40; com exclusão, 5.00.
        var input = Exceto(5,
            (JudgeMetricKey.Factual, 1),
            (JudgeMetricKey.Compliance, 1),
            (JudgeMetricKey.Relevance, 1));

        var normal = ScoreCalculator.CalcularScoreFinal(input);
        var comRecusa = ScoreCalculator.CalcularScoreFinal(input, recusaCorreta: true);

        Assert.Equal(3.40, normal);
        Assert.Equal(5.00, comRecusa);
    }

    [Fact]
    public void RecusaCorreta_ComPoucasMetricasRestantes_DevolveNull()
    {
        // 9 métricas presentes -> score válido sem recusa; ao excluir Factual/Compliance/
        // Relevance restam 6 (< mínimo de 8) -> null (caller usa o FINAL_SCORE declarado).
        var input = Com(
            (JudgeMetricKey.Factual, 3),
            (JudgeMetricKey.Formatting, 4),
            (JudgeMetricKey.Compliance, 3),
            (JudgeMetricKey.Relevance, 3),
            (JudgeMetricKey.Tone, 4),
            (JudgeMetricKey.Conciseness, 4),
            (JudgeMetricKey.Clarity, 4),
            (JudgeMetricKey.Readability, 4),
            (JudgeMetricKey.Safety, 5));

        var normal = ScoreCalculator.CalcularScoreFinal(input);
        var comRecusa = ScoreCalculator.CalcularScoreFinal(input, recusaCorreta: true);

        Assert.Equal(3.67, normal);
        Assert.Null(comRecusa);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void RecusaNulaOuFalsa_MantemPesoIntegral(bool? recusaCorreta)
    {
        var input = Exceto(5,
            (JudgeMetricKey.Factual, 1),
            (JudgeMetricKey.Compliance, 1),
            (JudgeMetricKey.Relevance, 1));

        var resultado = ScoreCalculator.CalcularScoreFinal(input, recusaCorreta: recusaCorreta);

        // Igual ao comportamento antigo: (1*20 + 5*10 + 1*10 + 1*10 + 5*80) / 100 = 3.40
        Assert.Equal(3.40, resultado);
    }

    /// <summary>As 12 métricas com a mesma nota.</summary>
    private static JudgeScoreInput Todas(int valor)
        => new(JudgeMetricCatalog.Todas.Select(m => (m.Chave, (int?)valor)));

    /// <summary>
    /// Todas as métricas com a mesma nota, menos as indicadas. É o perfil mais
    /// comum nos testes: "Factual a 1, o resto a 5".
    /// </summary>
    private static JudgeScoreInput Exceto(int valorBase, params (JudgeMetricKey Chave, int? Valor)[] alteracoes)
    {
        var mapa = JudgeMetricCatalog.Todas.ToDictionary(m => m.Chave, _ => (int?)valorBase);

        foreach (var (chave, valor) in alteracoes)
            mapa[chave] = valor;

        return new(mapa.Select(p => (p.Key, p.Value)));
    }

    /// <summary>Só as métricas indicadas; todas as outras ficam sem avaliação.</summary>
    private static JudgeScoreInput Com(params (JudgeMetricKey Chave, int? Valor)[] valores)
        => new(valores);

    /// <summary>
    /// A nota de cada métrica é lida pela chave, não por posição: trocar
    /// Compliance por Relevance na construção não pode alterar o score sem que o
    /// teste falhe.
    /// </summary>
    [Fact]
    public void OrdemDeConstrucaoNaoMudaOscore()
    {
        var a = new JudgeScoreInput(
        [
            (JudgeMetricKey.Factual, 1),
            (JudgeMetricKey.Compliance, 5)
        ]);

        var b = new JudgeScoreInput(
        [
            (JudgeMetricKey.Compliance, 5),
            (JudgeMetricKey.Factual, 1)
        ]);

        Assert.Equal(a[JudgeMetricKey.Factual], b[JudgeMetricKey.Factual]);
        Assert.Equal(a[JudgeMetricKey.Compliance], b[JudgeMetricKey.Compliance]);
    }

    [Fact]
    public void MetricaOmitida_LevaSeANull()
    {
        var input = Com((JudgeMetricKey.Safety, 5));

        Assert.Equal(5, input[JudgeMetricKey.Safety]);
        Assert.Null(input[JudgeMetricKey.Factual]);
    }

    [Fact]
    public void Valores_ExpoemTodasAsMetricasNaOrdemDoCatalogo()
    {
        var input = Todas(4);

        var valores = input.Valores.ToList();

        Assert.Equal(JudgeMetricCatalog.Todas.Count, valores.Count);
        Assert.Equal(JudgeMetricCatalog.Todas.Select(m => m.Chave), valores.Select(v => v.Chave));
        Assert.All(valores, v => Assert.Equal(4, v.Valor));
    }

    [Fact]
    public void EntradaNulaNoConstrutor_LancaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new JudgeScoreInput(null!));
    }

    [Fact]
    public void ChaveDesconhecidaNoPeso_LancaArgumentOutOfRangeException()
    {
        var pesos = new JudgeScoreWeights();

        Assert.Throws<ArgumentOutOfRangeException>(() => pesos.Peso((JudgeMetricKey)999));
    }
}
