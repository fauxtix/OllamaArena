using System.Text.Json;
using OllamaArena.Models.DTO;
using static OllamaArena.Models.DTO.OllamaModels;

namespace OllamaArena.Tests;

public class OllamaCapabilitiesTests
{
    private static OllamaShowResponse Desserializar(string json) =>
        JsonSerializer.Deserialize<OllamaShowResponse>(json)!;

    [Fact]
    public void CapabilitiesComThinking_IndicaQueSuportaRaciocinio()
    {
        var show = Desserializar(
            """
            { "capabilities": ["completion", "tools", "thinking"] }
            """);

        Assert.True(show.SupportsThinking);
    }

    [Fact]
    public void CapabilitiesSemThinking_IndicaQueNaoSuporta()
    {
        var show = Desserializar(
            """
            { "capabilities": ["completion", "tools", "vision"] }
            """);

        Assert.False(show.SupportsThinking);
    }

    [Fact]
    public void ThinkingComMaiusculas_ContaComoSuporte()
    {
        var show = Desserializar(
            """
            { "capabilities": ["Thinking"] }
            """);

        Assert.True(show.SupportsThinking);
    }

    [Fact]
    public void SemArrayCapabilities_FicaDesconhecido()
    {
        var show = Desserializar(
            """
            { "parameters": "stop  \"<|im_end|>\"", "template": "{{ .Prompt }}" }
            """);

        Assert.Null(show.SupportsThinking);
    }

    [Fact]
    public void ArrayCapabilitiesVazio_IndicaQueNaoSuporta()
    {
        var show = Desserializar(
            """
            { "capabilities": [] }
            """);

        Assert.False(show.SupportsThinking);
    }

    [Fact]
    public void ModelDetailsSerializadoPreservaSuporteDesconhecido()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var models = new List<ModelDetails>
        {
            new() { Model = "deepseek-r1", Name = "deepseek-r1", SupportsThinking = true },
            new() { Model = "llama3.2", Name = "llama3.2", SupportsThinking = false },
            new() { Model = "modelo-antigo", Name = "modelo-antigo" }
        };

        var json = JsonSerializer.Serialize(models, options);
        var lidos = JsonSerializer.Deserialize<List<ModelDetails>>(json, options)!;

        Assert.True(lidos[0].SupportsThinking);
        Assert.False(lidos[1].SupportsThinking);
        Assert.Null(lidos[2].SupportsThinking);
    }
}
