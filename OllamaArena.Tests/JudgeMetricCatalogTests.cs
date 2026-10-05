using OllamaArena.Models.DTO;
using OllamaArena.Models.Entities;
using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

/// <summary>
/// Testes da fonte única das 12 métricas dos juízes e da definição única de resposta
/// avaliada. As duas listas são consumidas pela grelha de detalhe, pelo ranking do
/// Dashboard, pelas exportações e pela análise local: se divergirem entre si, os
/// números da aplicação discordam entre ecrãs.
/// </summary>
public class JudgeMetricCatalogTests
{
    [Fact]
    public void Catalogo_TemAsDozeMetricasNaOrdemEsperada()
    {
        Assert.Equal(12, JudgeMetricCatalog.Todas.Count);
        Assert.Equal(
            [
                JudgeMetricKey.Factual,
                JudgeMetricKey.Formatting,
                JudgeMetricKey.Compliance,
                JudgeMetricKey.Relevance,
                JudgeMetricKey.Tone,
                JudgeMetricKey.Conciseness,
                JudgeMetricKey.Clarity,
                JudgeMetricKey.Readability,
                JudgeMetricKey.HaloEffect,
                JudgeMetricKey.Safety,
                JudgeMetricKey.LanguageConsistency,
                JudgeMetricKey.LoopDetection
            ],
            JudgeMetricCatalog.Todas.Select(m => m.Chave));
    }

    [Fact]
    public void Catalogo_TemChavesDeChaveRecursoUnicas()
    {
        var chaves = JudgeMetricCatalog.Todas.Select(m => m.ChaveRecurso).ToList();

        Assert.Equal(chaves.Count, chaves.Distinct().Count());
        Assert.All(chaves, c => Assert.StartsWith("Evaluation.Metric", c));
    }

    [Fact]
    public void Catalogo_SafetyTemLimiarMaisEstritoQueAsRestantes()
    {
        var safety = JudgeMetricCatalog.Todas.Single(m => m.Chave == JudgeMetricKey.Safety);

        Assert.Equal(JudgeMetricCatalog.SafetyWeakThreshold, safety.NotaFraca);
        Assert.All(
            JudgeMetricCatalog.Todas.Where(m => m.Chave != JudgeMetricKey.Safety),
            m => Assert.Equal(JudgeMetricCatalog.DefaultWeakThreshold, m.NotaFraca));
    }

    [Fact]
    public void ScoreDaEntidade_LeCadaMetricaDaEntidadeCorretamente()
    {
        var entidade = new BenchmarkResponse
        {
            GeminiRating = 5,
            OpenRouterRating = 4,
            GeminiFactualRating = 1,
            GeminiFormattingRating = 2,
            GeminiComplianceRating = 3,
            GeminiRelevanceRating = 4,
            GeminiToneRating = 5,
            GeminiConcisenessRating = 1,
            GeminiClarityRating = 2,
            GeminiReadabilityRating = 3,
            GeminiHaloEffectRating = 4,
            GeminiSafetyRating = 5,
            GeminiLanguageConsistencyRating = 2,
            GeminiLoopDetectionRating = 1,
            OpenRouterFactualRating = 5,
            OpenRouterFormattingRating = 5,
            OpenRouterComplianceRating = 5,
            OpenRouterRelevanceRating = 4,
            OpenRouterToneRating = 4,
            OpenRouterConcisenessRating = 3,
            OpenRouterClarityRating = 3,
            OpenRouterReadabilityRating = 3,
            OpenRouterHaloEffectRating = 3,
            OpenRouterSafetyRating = 4,
            OpenRouterLanguageConsistencyRating = 4,
            OpenRouterLoopDetectionRating = 3
        };

        var gemini = JudgeMetricCatalog.ScoreDaEntidade(entidade, juizGemini: true);

        Assert.Equal(1, gemini[JudgeMetricKey.Factual]);
        Assert.Equal(2, gemini[JudgeMetricKey.Formatting]);
        Assert.Equal(3, gemini[JudgeMetricKey.Compliance]);
        Assert.Equal(4, gemini[JudgeMetricKey.Relevance]);
        Assert.Equal(5, gemini[JudgeMetricKey.Tone]);
        Assert.Equal(1, gemini[JudgeMetricKey.Conciseness]);
        Assert.Equal(2, gemini[JudgeMetricKey.Clarity]);
        Assert.Equal(3, gemini[JudgeMetricKey.Readability]);
        Assert.Equal(4, gemini[JudgeMetricKey.HaloEffect]);
        Assert.Equal(5, gemini[JudgeMetricKey.Safety]);
        Assert.Equal(2, gemini[JudgeMetricKey.LanguageConsistency]);
        Assert.Equal(1, gemini[JudgeMetricKey.LoopDetection]);

        var openRouter = JudgeMetricCatalog.ScoreDaEntidade(entidade, juizGemini: false);

        Assert.Equal(5, openRouter[JudgeMetricKey.Factual]);
        Assert.Equal(5, openRouter[JudgeMetricKey.Formatting]);
        Assert.Equal(5, openRouter[JudgeMetricKey.Compliance]);
        Assert.Equal(4, openRouter[JudgeMetricKey.Relevance]);
        Assert.Equal(4, openRouter[JudgeMetricKey.Tone]);
        Assert.Equal(3, openRouter[JudgeMetricKey.Conciseness]);
        Assert.Equal(3, openRouter[JudgeMetricKey.Clarity]);
        Assert.Equal(3, openRouter[JudgeMetricKey.Readability]);
        Assert.Equal(3, openRouter[JudgeMetricKey.HaloEffect]);
        Assert.Equal(4, openRouter[JudgeMetricKey.Safety]);
        Assert.Equal(4, openRouter[JudgeMetricKey.LanguageConsistency]);
        Assert.Equal(3, openRouter[JudgeMetricKey.LoopDetection]);
    }

    [Fact]
    public void ScoreDaEntidadeEScoreDoModelo_DaoOMesmoScoreParaOMesmoJulgamento()
    {
        var entidade = EntidadeComTodasAsMetricas();
        var linha = ModeloComAsMesmasMetricas();

        foreach (var juizGemini in new[] { true, false })
        {
            var daEntidade = JudgeMetricCatalog.ScoreDaEntidade(entidade, juizGemini);
            var doModelo = JudgeMetricCatalog.ScoreDoModelo(linha, juizGemini);

            // Métrica a métrica: a igualdade de valor não pode depender da ordem de
            // construção nem de a entrada ser um record.
            Assert.Equal(daEntidade.Valores.ToList(), doModelo.Valores.ToList());

            // E o score ponderado é efetivamente o mesmo.
            Assert.Equal(
                ScoreCalculator.CalcularScoreFinal(daEntidade),
                ScoreCalculator.CalcularScoreFinal(doModelo));
        }
    }

    [Fact]
    public void ParesEntidade_CobremTodasAsMetricasNaOrdemDoCatalogo()
    {
        Assert.Equal(JudgeMetricCatalog.Todas.Count, JudgeMetricCatalog.ParesEntidade.Length);

        for (int i = 0; i < JudgeMetricCatalog.Todas.Count; i++)
        {
            var metrica = JudgeMetricCatalog.Todas[i];
            var par = JudgeMetricCatalog.ParesEntidade[i];

            var entidade = EntidadeComMetrica(i, valor: 3);

            Assert.Equal(metrica.GeminiEntidade(entidade), par.Gemini(entidade));
            Assert.Equal(metrica.OpenRouterEntidade(entidade), par.OpenRouter(entidade));
        }
    }

    [Fact]
    public void ScoreDaEntidade_RespostaNula_LancaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => JudgeMetricCatalog.ScoreDaEntidade(null!, juizGemini: true));
    }

    private static BenchmarkResponse EntidadeComTodasAsMetricas()
    {
        var e = new BenchmarkResponse();
        for (int i = 0; i < JudgeMetricCatalog.Todas.Count; i++)
            Definir(i, valor: i + 1, e, linha: null);
        return e;
    }

    private static BenchmarkResponse EntidadeComMetrica(int indice, int valor, BenchmarkResponse? alvo = null)
    {
        alvo ??= new BenchmarkResponse();
        Definir(indice, valor, alvo, linha: null);
        return alvo;
    }

    private static BenchmarkEvaluationModel ModeloComAsMesmasMetricas()
    {
        var linha = new BenchmarkEvaluationModel();

        for (int i = 0; i < JudgeMetricCatalog.Todas.Count; i++)
            Definir(i, valor: i + 1, null, linha);

        return linha;
    }

    /// <summary>
    /// Escreve a métrica de índice <paramref name="indice"/> na entidade e/ou no
    /// modelo de leitura, por nome de propriedade — é o que garante que o catálogo
    /// não aponte para campos inexistentes.
    /// </summary>
    private static void Definir(int indice, int valor, BenchmarkResponse? entidade, BenchmarkEvaluationModel? linha)
    {
        var sufixo = JudgeMetricCatalog.Todas[indice].NomePropriedade + "Rating";

        Assert.NotNull(typeof(BenchmarkResponse).GetProperty("Gemini" + sufixo));
        Assert.NotNull(typeof(BenchmarkResponse).GetProperty("OpenRouter" + sufixo));
        Assert.NotNull(typeof(BenchmarkEvaluationModel).GetProperty("Gemini" + sufixo));
        Assert.NotNull(typeof(BenchmarkEvaluationModel).GetProperty("OpenRouter" + sufixo));

        if (entidade is not null)
        {
            typeof(BenchmarkResponse).GetProperty("Gemini" + sufixo)!.SetValue(entidade, valor);
            typeof(BenchmarkResponse).GetProperty("OpenRouter" + sufixo)!.SetValue(entidade, valor);
        }

        if (linha is not null)
        {
            typeof(BenchmarkEvaluationModel).GetProperty("Gemini" + sufixo)!.SetValue(linha, valor);
            typeof(BenchmarkEvaluationModel).GetProperty("OpenRouter" + sufixo)!.SetValue(linha, valor);
        }
    }
}

/// <summary>
/// Testes da definição única de "resposta avaliada". Antes da refatoração havia três
/// critérios diferentes na mesma feature (HasValue, &gt; 0 e == 0), o que fazia o
/// diálogo de confirmação da análise anunciar "0 benchmark(s) ainda não avaliados"
/// quando havia ratings nulos.
/// </summary>
public class BenchmarkEvaluationRulesTests
{
    [Fact]
    public void EstaAvaliada_DoisJuizesComNota_RetornaTrue() =>
        Assert.True(BenchmarkEvaluationRules.EstaAvaliada(Linha(4, 5)));

    [Theory]
    [InlineData(null, 5)]
    [InlineData(4, null)]
    [InlineData(0, 5)]
    [InlineData(4, 0)]
    [InlineData(null, null)]
    public void EstaAvaliada_FaltaNotaValidaEmQualquerJuiz_RetornaFalse(int? gemini, int? openRouter) =>
        Assert.False(BenchmarkEvaluationRules.EstaAvaliada(Linha(gemini, openRouter)));

    [Fact]
    public void ContarPorAvaliar_ContaRatingsNulosEOsZeros()
    {
        var linhas = new[] { Linha(5, 5), Linha(null, 5), Linha(4, 0), Linha(null, null) };

        Assert.Equal(3, BenchmarkEvaluationRules.ContarPorAvaliar(linhas));
        Assert.Equal(1, BenchmarkEvaluationRules.ContarAvaliadas(linhas));
    }

    [Fact]
    public void Contagens_DeConjuntoVazio_Zero()
    {
        Assert.Equal(0, BenchmarkEvaluationRules.ContarPorAvaliar([]));
        Assert.Equal(0, BenchmarkEvaluationRules.ContarAvaliadas([]));
    }

    private static BenchmarkEvaluationModel Linha(int? gemini, int? openRouter) => new()
    {
        GeminiRating = gemini,
        OpenRouterRating = openRouter
    };
}