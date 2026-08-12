using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

public class EvaluationParserTests
{
    [Fact]
    public void RespostaCompleta_ParseiaTodasAsDozeMetricas()
    {
        const string resposta = """
            FACTUAL_SCORE: 4
            FORMATTING_SCORE: 3
            COMPLIANCE_SCORE: 5
            RELEVANCE_SCORE: 4
            TONE_SCORE: 4
            CONCISENESS_SCORE: 3
            CLARITY_SCORE: 5
            READABILITY_SCORE: 4
            HALO_EFFECT_SCORE: 5
            SAFETY_SCORE: 5
            LANGUAGE_CONSISTENCY_SCORE: 4
            LOOP_DETECTION_SCORE: 5
            FINAL_SCORE: 4
            DESCRIPTION: Resposta clara e bem estruturada.
            RECOMMENDATION: Modelo adequado para esta tarefa.
            """;

        var resultado = EvaluationParser.ParseEvaluation(resposta);

        Assert.Equal(4, resultado.FactualScore);
        Assert.Equal(3, resultado.FormattingScore);
        Assert.Equal(5, resultado.ComplianceScore);
        Assert.Equal(4, resultado.RelevanceScore);
        Assert.Equal(4, resultado.ToneScore);
        Assert.Equal(3, resultado.ConcisenessScore);
        Assert.Equal(5, resultado.ClarityScore);
        Assert.Equal(4, resultado.ReadabilityScore);
        Assert.Equal(5, resultado.HaloEffectScore);
        Assert.Equal(5, resultado.SafetyScore);
        Assert.Equal(4, resultado.LanguageConsistencyScore);
        Assert.Equal(5, resultado.LoopDetectionScore);
        Assert.Equal(4f, resultado.FinalScore);
        Assert.Contains("Resposta clara", resultado.Description);
        Assert.Contains("Modelo adequado", resultado.Recommendation);
    }

    [Fact]
    public void PrefixosComHifenOuAsterisco_SaoAceites()
    {
        const string resposta = """
            - FACTUAL_SCORE: 2
            * FORMATTING_SCORE: 4
            - SAFETY_SCORE: 5
            - FINAL_SCORE: 3
            """;

        var resultado = EvaluationParser.ParseEvaluation(resposta);

        Assert.Equal(2, resultado.FactualScore);
        Assert.Equal(4, resultado.FormattingScore);
        Assert.Equal(5, resultado.SafetyScore);
        Assert.Equal(3f, resultado.FinalScore);
    }

    [Fact]
    public void RespostasEmFormatoBarraOuColchetes_SaoNormalizadas()
    {
        const string resposta = """
            FACTUAL_SCORE: [4]
            FORMATTING_SCORE: 4/5
            FINAL_SCORE: 4/5
            """;

        var resultado = EvaluationParser.ParseEvaluation(resposta);

        Assert.Equal(4, resultado.FactualScore);
        Assert.Equal(4, resultado.FormattingScore);
        Assert.Equal(4f, resultado.FinalScore);
    }

    [Fact]
    public void DescricaoMultiLinha_AgregaSecoes()
    {
        const string resposta = """
            FACTUAL_SCORE: 4
            FINAL_SCORE: 4
            DESCRIPTION: Primeira frase.
            Segunda frase da descrição.
            RECOMMENDATION: Recomendo.
            Continua a recomendação.
            """;

        var resultado = EvaluationParser.ParseEvaluation(resposta);

        Assert.Contains("Primeira frase", resultado.Description);
        Assert.Contains("Segunda frase", resultado.Description);
        Assert.Contains("Recomendo", resultado.Recommendation);
        Assert.Contains("Continua", resultado.Recommendation);
    }

    [Fact]
    public void RespostaVazia_DevolveResultadoVazio()
    {
        var resultado = EvaluationParser.ParseEvaluation("");

        Assert.Null(resultado.FactualScore);
        Assert.Null(resultado.FinalScore);
        Assert.Null(resultado.LoopDetectionScore);
    }

    [Fact]
    public void RespostaNula_DevolveResultadoVazio()
    {
        var resultado = EvaluationParser.ParseEvaluation(null!);

        Assert.Null(resultado.FactualScore);
        Assert.Null(resultado.FinalScore);
    }

    [Fact]
    public void SemTagDescription_UsaRespostaInteiraComoFeedback()
    {
        const string resposta = "A resposta contém apenas texto livre.";

        var resultado = EvaluationParser.ParseEvaluation(resposta);

        Assert.Equal(resposta, resultado.Description);
    }

    [Fact]
    public void LanguageConsistencyELoopDetection_SaoParseados()
    {
        const string resposta = """
            LANGUAGE_CONSISTENCY_SCORE: 3
            LOOP_DETECTION_SCORE: 2
            """;

        var resultado = EvaluationParser.ParseEvaluation(resposta);

        Assert.Equal(3, resultado.LanguageConsistencyScore);
        Assert.Equal(2, resultado.LoopDetectionScore);
    }
}
