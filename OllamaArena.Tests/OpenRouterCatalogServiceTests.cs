using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaArena.Services;

namespace OllamaArena.Tests;

public class OpenRouterCatalogServiceTests
{
    [Fact]
    public async Task CatalogoCamelCase_DevolveModelosGratuitos()
    {
        var servico = CriarServico(
            """
            {
              "data": [
                { "id": "org/gratis-1:free", "name": "Gratis 1", "context_length": 32000, "pricing": { "prompt": "0", "completion": "0" } },
                { "id": "org/pago-1", "name": "Pago 1", "context_length": 128000, "pricing": { "prompt": "0.000005", "completion": "0.00002" } },
                { "id": "org/gratis-2:free", "name": "Gratis 2", "context_length": null, "pricing": { "prompt": "0", "completion": "0" } }
              ]
            }
            """);

        var resultado = await servico.GetFreeModelsAsync();

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, m => m.Id == "org/gratis-1:free" && m.Name == "Gratis 1" && m.ContextLength == 32000 && m.IsFree);
        Assert.Contains(resultado, m => m.Id == "org/gratis-2:free" && m.Name == "Gratis 2" && m.ContextLength == null);
        Assert.DoesNotContain(resultado, m => m.Id == "org/pago-1");
    }

    [Fact]
    public async Task ModeloPago_FicaDeFora()
    {
        var servico = CriarServico(
            """
            {
              "data": [
                { "id": "org/so-pago", "name": "So Pago", "pricing": { "prompt": "0.001", "completion": "0.004" } }
              ]
            }
            """);

        var resultado = await servico.GetFreeModelsAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task CatalogoVazio_DevolveListaVazia()
    {
        var servico = CriarServico("""{ "data": [] }""");

        var resultado = await servico.GetFreeModelsAsync();

        Assert.Empty(resultado);
    }

    private static OpenRouterCatalogService CriarServico(string json)
        => new(new HttpClient(new StubHttpMessageHandler(json)), NullLogger<OpenRouterCatalogService>.Instance);

    private sealed class StubHttpMessageHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
