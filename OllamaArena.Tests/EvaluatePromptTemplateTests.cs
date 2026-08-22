using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaArena.PromptTemplates;
using OllamaArena.Services;
using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

public class EvaluatePromptTemplateTests : IDisposable
{
    private readonly string _raiz;
    private readonly EvaluatePromptTemplate _template;

    public EvaluatePromptTemplateTests()
    {
        _raiz = Directory.CreateTempSubdirectory("ollama-arena-evaltpl").FullName;
        Directory.CreateDirectory(Path.Combine(_raiz, "Prompts"));
        File.WriteAllText(
            Path.Combine(_raiz, "Prompts", "evaluation_prompt_template.txt"),
            "Model: {modelName}\nPrompt: {originalPrompt}\nResponse: {modelResponse}\nYear: {trainingYear}\nLanguage: {expectedLanguage}\nTheme: {promptTheme}\n{themeGuidance}");

        var servicoFicheiros = new PromptFilesService(
            NullLogger<PromptFilesService>.Instance,
            new AmbienteFalso(_raiz));

        _template = new EvaluatePromptTemplate(servicoFicheiros);
    }

    [Fact]
    public void ConstruirBlocoIdioma_ComIdioma_IndicaInstrucaoExplicita()
    {
        var bloco = EvaluatePromptTemplate.ConstruirBlocoIdioma("English (en-US)");

        Assert.Contains("English (en-US)", bloco);
        Assert.Contains("instructed to respond ONLY", bloco);
        Assert.DoesNotContain("Not recorded", bloco, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstruirBlocoIdioma_SemIdioma_MantemAvaliacaoSoPeloPrompt(string? idioma)
    {
        var bloco = EvaluatePromptTemplate.ConstruirBlocoIdioma(idioma);

        Assert.Contains("Not recorded", bloco);
        Assert.Contains("prompt only", bloco);
    }

    [Theory]
    [InlineData("English (en-US)")]
    [InlineData(null)]
    public async Task EvaluationCopyPromptAsync_SubstituiTodosOsPlaceholders(string? idioma)
    {
        string resultado = await _template.EvaluationCopyPromptAsync(
            "pergunta", "resposta", "2024", "llama3", expectedLanguage: idioma);

        Assert.DoesNotContain("{modelName}", resultado);
        Assert.DoesNotContain("{originalPrompt}", resultado);
        Assert.DoesNotContain("{modelResponse}", resultado);
        Assert.DoesNotContain("{trainingYear}", resultado);
        Assert.DoesNotContain("{expectedLanguage}", resultado);
        Assert.Contains("Model: llama3", resultado);
        Assert.Contains("Prompt: pergunta", resultado);
        Assert.Contains("Response: resposta", resultado);
        Assert.Contains("Year: 2024", resultado);

        // Sem idioma gravado (respostas antigas), o bloco assume o idioma atual da UI.
        string blocoEsperado = EvaluatePromptTemplate.ConstruirBlocoIdioma(
            string.IsNullOrWhiteSpace(idioma) ? TargetLanguageResolver.GetTargetLanguage() : idioma,
            gravadoNaResposta: !string.IsNullOrWhiteSpace(idioma));
        Assert.Contains($"Language: {blocoEsperado}", resultado);
    }

    [Fact]
    public void ConstruirBlocoIdioma_SemIdiomaGravado_MarcaComoSuposicao()
    {
        var bloco = EvaluatePromptTemplate.ConstruirBlocoIdioma(
            TargetLanguageResolver.GetTargetLanguage(), gravadoNaResposta: false);

        Assert.Contains("assumed from the current app language", bloco);
        Assert.Contains(TargetLanguageResolver.GetTargetLanguage(), bloco);
        Assert.Contains("respond ONLY", bloco);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EvaluationCopyPromptAsync_SemIdiomaGravado_InjetaIdiomaDaUI(string? idioma)
    {
        string resultado = await _template.EvaluationCopyPromptAsync(
            "pergunta", "resposta", "2024", "llama3", expectedLanguage: idioma);

        Assert.Contains("(assumed from the current app language", resultado);
        Assert.Contains(TargetLanguageResolver.GetTargetLanguage(), resultado);
        Assert.DoesNotContain("Not recorded.", resultado, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try { Directory.Delete(_raiz, recursive: true); } catch (IOException) { }
    }

    private sealed class AmbienteFalso(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "OllamaArena.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
