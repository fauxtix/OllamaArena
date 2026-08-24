using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

/// <summary>
/// Testes da origem de cada slot de juiz: slot vazio → null, origem gravada mantém-se,
/// conteúdo legado sem origem mantém-se null e nova entrada manual → 'externo'.
/// </summary>
public class JudgeOriginResolverTests
{
    [Fact]
    public void CalcularOrigemSlot_SlotSemConteudo_RetornaNull()
    {
        var origem = JudgeOriginResolver.CalcularOrigemSlot(
            temConteudoAgora: false,
            temConteudoNaAbertura: false,
            origemGravada: null);

        Assert.Null(origem);
    }

    [Fact]
    public void CalcularOrigemSlot_ConteudoApagado_AposGravada_RetornaNull()
    {
        var origem = JudgeOriginResolver.CalcularOrigemSlot(
            temConteudoAgora: false,
            temConteudoNaAbertura: true,
            origemGravada: JudgeOriginResolver.Gemini);

        Assert.Null(origem);
    }

    [Theory]
    [InlineData(JudgeOriginResolver.Gemini)]
    [InlineData(JudgeOriginResolver.OpenRouter)]
    [InlineData(JudgeOriginResolver.Externo)]
    public void CalcularOrigemSlot_OrigemGravadaComConteudo_Mantem(string gravada)
    {
        var origem = JudgeOriginResolver.CalcularOrigemSlot(
            temConteudoAgora: true,
            temConteudoNaAbertura: false,
            origemGravada: gravada);

        Assert.Equal(gravada, origem);
    }

    [Fact]
    public void CalcularOrigemSlot_ColagemManualNova_RetornaExterno()
    {
        var origem = JudgeOriginResolver.CalcularOrigemSlot(
            temConteudoAgora: true,
            temConteudoNaAbertura: false,
            origemGravada: null);

        Assert.Equal(JudgeOriginResolver.Externo, origem);
    }

    [Fact]
    public void CalcularOrigemSlot_RegistoLegado_ComConteudoNaAbertura_RetornaNull()
    {
        // Registos anteriores à funcionalidade não devem receber origem inventada.
        var origem = JudgeOriginResolver.CalcularOrigemSlot(
            temConteudoAgora: true,
            temConteudoNaAbertura: true,
            origemGravada: null);

        Assert.Null(origem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("desconhecida")]
    public void Normalizar_ValorInvalido_RetornaNull(string? origem)
    {
        Assert.Null(JudgeOriginResolver.Normalizar(origem));
    }

    [Theory]
    [InlineData(JudgeOriginResolver.Gemini)]
    [InlineData(JudgeOriginResolver.OpenRouter)]
    [InlineData(JudgeOriginResolver.Externo)]
    public void Normalizar_ChaveConhecida_Mantem(string origem)
    {
        Assert.Equal(origem, JudgeOriginResolver.Normalizar(origem));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData("   ", 4f, true)]
    [InlineData("Feedback do juiz.", null, true)]
    public void TemConteudo_FeedbackOuNotaGlobal_DeterminaConteudo(string? feedback, float? notaGlobal, bool esperado)
    {
        Assert.Equal(esperado, JudgeOriginResolver.TemConteudo(feedback, notaGlobal));
    }
}
