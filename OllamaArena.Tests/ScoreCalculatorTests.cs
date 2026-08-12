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

    private static JudgeScoreInput NovoInput(int factual, int formatting, int compliance, int relevance,
        int tone, int conciseness, int clarity, int readability, int halo, int safety,
        int language, int loop)
        => new(factual, formatting, compliance, relevance, tone, conciseness, clarity,
            readability, halo, safety, language, loop);
}
