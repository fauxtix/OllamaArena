using OllamaArena.Services;
using System.Reflection;

namespace OllamaArena.Tests;

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

    /// <summary>
    /// As tabelas de pesos passaram a ser listas com verificação de duplicatas: quando uma
    /// escrita dictionary apagava a anterior, um termo duplicado mudava a temperatura
    /// recomendada em silêncio. Estes testes fixam esse contrato.
    /// </summary>
    [Fact]
    public void BuildWeights_ComTermoDuplicado_LançaEmVezDeSobrescrever()
    {
        var construir = typeof(ChatMeasureTemperature).GetMethod(
            "BuildWeights", BindingFlags.NonPublic | BindingFlags.Static)!;

        var termos = new (string Termo, float Peso)[]
        {
            ("código", 3.0f),
            ("código", 5.0f)
        };

        var erro = Assert.Throws<TargetInvocationException>(() => construir.Invoke(null, [termos]));

        Assert.IsType<InvalidOperationException>(erro.InnerException);
        Assert.Contains("código", erro.InnerException!.Message);
    }

    [Fact]
    public void BuildWeights_SemDuplicados_DevolveUmPesoPorTermo()
    {
        var construir = typeof(ChatMeasureTemperature).GetMethod(
            "BuildWeights", BindingFlags.NonPublic | BindingFlags.Static)!;

        var termos = new (string Termo, float Peso)[]
        {
            ("um", 1.0f),
            ("dois", 2.0f)
        };

        var pesos = (Dictionary<string, float>)construir.Invoke(null, [termos])!;

        Assert.Equal(2, pesos.Count);
        Assert.Equal(1.0f, pesos["um"]);
        Assert.Equal(2.0f, pesos["dois"]);
    }

    [Fact]
    public void TabelasDePesos_NãoTêmTermosDuplicadosEmCasa()
    {
        foreach (var nome in new[] { "TermosFactuaisList", "TermosCriativosList" })
        {
            var termos = (Array)typeof(ChatMeasureTemperature)
                .GetField(nome, BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

            var vistos = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < termos.Length; i++)
            {
                var par = termos.GetValue(i)!;
                string termo = (string)par.GetType().GetField("Item1")!.GetValue(par)!;

                Assert.True(vistos.Add(termo), $"'{termo}' está duplicado em {nome} (índice {i}).");
            }
        }
    }
}
