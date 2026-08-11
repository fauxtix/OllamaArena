using OllamaFluentUIChat.Services;

namespace OllamaFluentUIChat.Tests;

public class ChatMeasureTemperatureTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PromptVazio_DevolveTemperaturaFactual(string? prompt)
    {
        Assert.Equal(0.20, ChatMeasureTemperature.ObterTemperaturaRecomendada(prompt));
    }

    [Fact]
    public void PromptFactual_DevolveTemperaturaBaixa()
    {
        double temp = ChatMeasureTemperature.ObterTemperaturaRecomendada(
            "como resolver um problema de performance em C# passo a passo");

        Assert.InRange(temp, 0.0, 0.30);
    }

    [Fact]
    public void PromptCriativo_DevolveTemperaturaAlta()
    {
        double temp = ChatMeasureTemperature.ObterTemperaturaRecomendada(
            "inventa uma história mágica e escreve um poema no estilo de um autor");

        Assert.InRange(temp, 0.65, 0.90);
    }

    [Fact]
    public void PromptComCodigo_DevolveTemperaturaFactualForte()
    {
        double temp = ChatMeasureTemperature.ObterTemperaturaRecomendada(
            "public class Foo { int x = 1; if (x > 0) { System.Console.WriteLine(x); } }");

        Assert.Equal(0.10, temp);
    }

    [Theory]
    [InlineData("ação")]
    [InlineData("permissão")]
    [InlineData("João comeu pão com água")]
    public void RemoverAcentos_EliminaDiacriticos(string texto)
    {
        Assert.Equal(texto.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                        System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray().Aggregate("", (s, c) => s + c)
            .Normalize(System.Text.NormalizationForm.FormC),
            ChatMeasureTemperature.RemoverAcentos(texto));
    }
}
