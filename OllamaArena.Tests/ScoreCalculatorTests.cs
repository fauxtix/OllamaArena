using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

public class ScoreCalculatorTests
{
    [Fact]
    public void TodasMetricasMaximas_DevolveCinco()
    {
        var input = NovoInput(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5);

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Equal(5.0, resultado);
    }

    [Fact]
    public void TodasMetricasMinimas_DevolveUm()
    {
        var input = NovoInput(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Equal(1.0, resultado);
    }

    [Fact]
    public void PesosDefault_ProduzemMediaPonderadaEsperada()
    {
        // Factual=20 (peso alto) com nota 1 vs restantes a 5 -> score abaixo de 4
        var input = new JudgeScoreInput(1, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5);

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        // (1*20 + 5*80) / 100 = 4.20
        Assert.Equal(4.20, resultado);
    }

    [Fact]
    public void MetricasEmFalta_RenormalizaPesos()
    {
        // 11 métricas presentes (falta LoopDetection). Factual=1 vs resto a 5.
        // Sem renormalização (divisão por 100) daria 3.95; renormalizado (por 95) dá 4.16.
        var input = new JudgeScoreInput(1, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, null);

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Equal(4.16, resultado);
    }

    [Fact]
    public void MenosDeOitoMetricas_DevolveNull()
    {
        var input = new JudgeScoreInput(3, 4, null, null, null, null, null, null, null, null, null, null);

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Null(resultado);
    }

    [Fact]
    public void SomaPesosAbaixoDoMinimo_DevolveNull()
    {
        // Apenas métricas de peso baixo (Tone+Conciseness+LanguageConsistency+LoopDetection = 20) -> < 50
        var input = new JudgeScoreInput(null, null, null, null, 3, 3, null, null, null, null, 3, 3);

        var resultado = ScoreCalculator.CalcularScoreFinal(input);

        Assert.Null(resultado);
    }

    [Fact]
    public void HaloEffect_ComPesoZero_NaoInfluiNoScore()
    {
        var comHaloBaixo = NovoInput(4, 4, 4, 4, 4, 4, 4, 4, 1, 4, 4, 4);
        var comHaloAlto = NovoInput(4, 4, 4, 4, 4, 4, 4, 4, 5, 4, 4, 4);

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
        var input = NovoInput(1, 5, 1, 1, 5, 5, 5, 5, 5, 5, 5, 5);

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
        var input = new JudgeScoreInput(3, 4, 3, 3, 4, 4, 4, 4, null, 5, null, null);

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
        var input = NovoInput(1, 5, 1, 1, 5, 5, 5, 5, 5, 5, 5, 5);

        var resultado = ScoreCalculator.CalcularScoreFinal(input, recusaCorreta: recusaCorreta);

        // Igual ao comportamento antigo: (1*20 + 5*10 + 1*10 + 1*10 + 5*80) / 100 = 3.40
        Assert.Equal(3.40, resultado);
    }

    private static JudgeScoreInput NovoInput(int factual, int formatting, int compliance, int relevance,
        int tone, int conciseness, int clarity, int readability, int halo, int safety,
        int language, int loop)
        => new(factual, formatting, compliance, relevance, tone, conciseness, clarity,
            readability, halo, safety, language, loop);
}
