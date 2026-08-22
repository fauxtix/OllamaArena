using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaArena.Services;

namespace OllamaArena.Tests;

public class PromptFilesServiceTests : IDisposable
{
    private readonly string _raiz;
    private readonly PromptFilesService _servico;

    public PromptFilesServiceTests()
    {
        _raiz = Directory.CreateTempSubdirectory("ollama-arena-promptfiles").FullName;
        Directory.CreateDirectory(Path.Combine(_raiz, "Prompts"));
        Directory.CreateDirectory(Path.Combine(_raiz, "PlanoDeTestes"));
        File.WriteAllText(Path.Combine(_raiz, "Prompts", "system-prompt.txt"), "prompt do sistema");
        File.WriteAllText(Path.Combine(_raiz, "PlanoDeTestes", "Prompts_Teste_Juiz.txt"), "plano pt");
        File.WriteAllText(Path.Combine(_raiz, "PlanoDeTestes", "Prompts_Teste_Juiz.en.txt"), "plano en");

        _servico = new PromptFilesService(
            NullLogger<PromptFilesService>.Instance,
            new AmbienteFalso(_raiz));
    }

    [Fact]
    public void SemPasta_ListaApenasAPastaPadrao()
    {
        var ficheiros = _servico.GetPromptFiles();

        Assert.Equal(["system-prompt.txt"], ficheiros);
    }

    [Fact]
    public void PastaPlanoDeTestes_ListaApenasOsFicheirosDoPlano()
    {
        var ficheiros = _servico.GetPromptFiles("PlanoDeTestes");

        Assert.Equal(2, ficheiros.Count);
        Assert.Contains("Prompts_Teste_Juiz.txt", ficheiros);
        Assert.Contains("Prompts_Teste_Juiz.en.txt", ficheiros);
    }

    [Fact]
    public async Task LeituraComPasta_DevolveOConteudoDoPlano()
    {
        var conteudo = await _servico.GetPromptFileContentAsync("Prompts_Teste_Juiz.txt", "PlanoDeTestes");

        Assert.Equal("plano pt", conteudo);
    }

    [Fact]
    public async Task FicheiroDeOutraPasta_NaoEhLido()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _servico.GetPromptFileContentAsync("system-prompt.txt", "PlanoDeTestes"));
    }

    [Fact]
    public void PastaInexistente_DevolveListaVazia()
    {
        var ficheiros = _servico.GetPromptFiles("NaoExiste");

        Assert.Empty(ficheiros);
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
