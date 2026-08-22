using System.Globalization;
using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

public class TargetLanguageResolverTests
{
    [Theory]
    [InlineData("pt-PT", "European Portuguese (pt-PT)")]
    [InlineData("pt-BR", "European Portuguese (pt-PT)")]
    [InlineData("en-US", "English (en-US)")]
    [InlineData("en-GB", "English (en-US)")]
    public void GetTargetLanguage_MapeiaCulturasSuportadas(string cultura, string esperado)
    {
        var culturaOriginal = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultura);
        try
        {
            Assert.Equal(esperado, TargetLanguageResolver.GetTargetLanguage());
        }
        finally
        {
            CultureInfo.CurrentUICulture = culturaOriginal;
        }
    }

    [Fact]
    public void GetTargetLanguage_CulturaNaoSuportadaFazFallbackParaPt()
    {
        var culturaOriginal = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            Assert.Equal("European Portuguese (pt-PT)", TargetLanguageResolver.GetTargetLanguage());
        }
        finally
        {
            CultureInfo.CurrentUICulture = culturaOriginal;
        }
    }

    [Fact]
    public void Token_EConstanteEsperada()
    {
        Assert.Equal("{{TARGET_LANGUAGE}}", TargetLanguageResolver.Token);
    }

    [Fact]
    public void SubstituicaoToken_PromptComTokenEResolvido()
    {
        var prompt = "Always respond in {{TARGET_LANGUAGE}}.";
        var resolvido = prompt.Replace(TargetLanguageResolver.Token, "English (en-US)");

        Assert.DoesNotContain(TargetLanguageResolver.Token, resolvido);
        Assert.Contains("Always respond in English (en-US).", resolvido);
    }

    [Fact]
    public void SubstituicaoToken_SemTokenENoOp()
    {
        var prompt = "You are a concise assistant.";

        Assert.Equal(prompt, prompt.Replace(TargetLanguageResolver.Token, "English (en-US)"));
    }
}
