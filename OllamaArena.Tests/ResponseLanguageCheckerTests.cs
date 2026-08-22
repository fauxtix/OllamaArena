using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

public class ResponseLanguageCheckerTests
{
    private const string TextoPt =
        "A fabricação de explosivos é extremamente perigosa e ilegal na maioria dos países. " +
        "Este tipo de atividade pode resultar em ferimentos graves, por isso não forneço instruções sobre o assunto. " +
        "Se precisar de ajuda, procure as autoridades competentes da sua região.";

    private const string TextoEn =
        "Manufacturing explosives is extremely dangerous and illegal in most countries. " +
        "This kind of activity can result in serious injuries, so I will not provide instructions about it. " +
        "If you need help, please contact the competent authorities in your area.";

    [Fact]
    public void Detect_TextoClaramentePortugues_DevolvePortuguese()
    {
        Assert.Equal(DetectedLanguage.Portuguese, ResponseLanguageChecker.Detect(TextoPt));
    }

    [Fact]
    public void Detect_TextoClaramenteIngles_DevolveEnglish()
    {
        Assert.Equal(DetectedLanguage.English, ResponseLanguageChecker.Detect(TextoEn));
    }

    [Fact]
    public void Detect_TextoCurto_EInconclusivo()
    {
        Assert.Equal(DetectedLanguage.Inconclusive, ResponseLanguageChecker.Detect("Olá, tudo bem?"));
        Assert.Equal(DetectedLanguage.Inconclusive, ResponseLanguageChecker.Detect("Hello there!"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Detect_TextoVazio_EInconclusivo(string? texto)
    {
        Assert.Equal(DetectedLanguage.Inconclusive, ResponseLanguageChecker.Detect(texto));
    }

    [Fact]
    public void Detect_ApenasCodigo_EInconclusivo()
    {
        string texto = "```csharp\npublic class Foo { }\nvar result = service.GetData();\nreturn Ok(result);\n```\n" +
                       "```\nselect * from table where id = 1;\n```\n" +
                       "```\nfor (int i = 0; i < 10; i++) { sum += i; }\n```";

        Assert.Equal(DetectedLanguage.Inconclusive, ResponseLanguageChecker.Detect(texto));
    }

    [Fact]
    public void Detect_UrlsSaoIgnoradas()
    {
        string texto = "Consulte a documentação oficial para mais informações sobre este procedimento e sobre as condições de utilização do serviço. " +
                       "Veja também https://exemplo.com/docs/very/long/path?q=teste&lang=en-US para detalhes adicionais.";

        // A URL contém tokens com aspeto inglês (docs, long, path, lang); é removida antes da análise.
        Assert.Equal(DetectedLanguage.Portuguese, ResponseLanguageChecker.Detect(texto));
    }

    [Theory]
    [InlineData(DetectedLanguage.Portuguese, "pt", false)]
    [InlineData(DetectedLanguage.English, "en", false)]
    [InlineData(DetectedLanguage.English, "pt", true)]
    [InlineData(DetectedLanguage.Portuguese, "en", true)]
    [InlineData(DetectedLanguage.Inconclusive, "en", false)]
    [InlineData(DetectedLanguage.Inconclusive, "pt", false)]
    public void IsMismatch_ComparaDetecaoComEsperado(DetectedLanguage detetado, string esperado, bool mismatch)
    {
        Assert.Equal(mismatch, ResponseLanguageChecker.IsMismatch(detetado, esperado));
    }

    [Fact]
    public void IsMismatch_CodigoDesconhecido_FazFallbackParaPt()
    {
        Assert.True(ResponseLanguageChecker.IsMismatch(DetectedLanguage.English, "fr"));
        Assert.False(ResponseLanguageChecker.IsMismatch(DetectedLanguage.Portuguese, "fr"));
    }
}
