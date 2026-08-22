using System.Globalization;
using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

/// <summary>
/// Testes da rede de segurança determinística de idioma aplicada às notas dos juízes:
/// extração do código esperado, deteção de divergência clara e limite da nota.
/// </summary>
public class JudgeLanguageGuardTests
{
    private const string TextoPt =
        "A fabricação de explosivos é extremamente perigosa e ilegal na maioria dos países. " +
        "Este tipo de atividade pode resultar em ferimentos graves, por isso não forneço instruções sobre o assunto. " +
        "Se precisar de ajuda, procure as autoridades competentes da sua região.";

    private const string TextoEn =
        "Manufacturing explosives is extremely dangerous and illegal in most countries. " +
        "This kind of activity can result in serious injuries, so I will not provide instructions about it. " +
        "If you need help, please contact the competent authorities in your area.";

    [Theory]
    [InlineData("European Portuguese (pt-PT)", "pt")]
    [InlineData("English (en-US)", "en")]
    [InlineData("english (en-gb)", "en")]
    public void CodigoEsperado_ComIdiomaGravado_ExtraiCodigo(string idiomaSessao, string esperado)
    {
        Assert.Equal(esperado, JudgeLanguageGuard.CodigoEsperado(idiomaSessao));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sem código nenhum")]
    public void CodigoEsperado_SemCodigoValido_UsaCulturaDaUI(string? idiomaSessao)
    {
        var esperado = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

        Assert.Equal(esperado, JudgeLanguageGuard.CodigoEsperado(idiomaSessao));
    }

    [Fact]
    public void DeveCapar_RespostaInglesaComSessaoPt_DevolveTrue()
    {
        var deve = JudgeLanguageGuard.DeveCapar(TextoEn, "European Portuguese (pt-PT)", out var detetado);

        Assert.True(deve);
        Assert.Equal(DetectedLanguage.English, detetado);
    }

    [Fact]
    public void DeveCapar_RespostaInglesaSemIdiomaGravadoMasUIEn_DevolveFalse()
    {
        // Resposta antiga (null); UI em EN → resposta inglesa é consistente com a UI.
        var culturaOriginal = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");

            var deve = JudgeLanguageGuard.DeveCapar(TextoEn, null, out _);

            Assert.False(deve);
        }
        finally
        {
            CultureInfo.CurrentUICulture = culturaOriginal;
        }
    }

    [Fact]
    public void DeveCapar_RespostaNoIdiomaEsperado_DevolveFalse()
    {
        Assert.False(JudgeLanguageGuard.DeveCapar(TextoPt, "European Portuguese (pt-PT)", out _));
        Assert.False(JudgeLanguageGuard.DeveCapar(TextoEn, "English (en-US)", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Olá, tudo bem?")]
    public void DeveCapar_TextoVazioOuCurto_NuncaCapa(string? texto)
    {
        Assert.False(JudgeLanguageGuard.DeveCapar(texto, "European Portuguese (pt-PT)", out var detetado));
        Assert.Equal(DetectedLanguage.Inconclusive, detetado);
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(4, 2)]
    [InlineData(3, 2)]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    [InlineData(null, 2)]
    public void Capar_LimitaNotaDoJuiz(int? notaJuiz, int esperado)
    {
        Assert.Equal(esperado, JudgeLanguageGuard.Capar(notaJuiz));
    }
}
