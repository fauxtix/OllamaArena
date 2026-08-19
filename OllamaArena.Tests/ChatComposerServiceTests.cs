using Microsoft.Extensions.Options;
using OllamaArena.Models.DTO;
using OllamaArena.Services;
using OllamaArena.Services.Interfaces.Repositories;

namespace OllamaArena.Tests;

public class ChatComposerServiceTests
{
    private const string SystemPrompt = "Instruções do sistema";
    private const string Welcome = "Olá!";

    private static ChatComposerService CriarServico(params (string Chave, string Valor)[] valores) =>
        new(Options.Create(new OllamaOptions()), new FakeSettingsRepository(valores));

    private static ChatMessage Msg(string texto, bool user) => new()
    {
        Text = texto,
        IsCurrentUser = user
    };

    private static List<ChatMessage> Historico(params ChatMessage[] msgs) => [.. msgs];

    [Fact]
    public void BuildHistory_PrimeiroElementoESystem()
    {
        var historico = ChatComposerService.BuildHistory(Historico(), SystemPrompt, Welcome);

        Assert.Equal("system", historico[0].Role);
        Assert.Equal(SystemPrompt, historico[0].Content);
    }

    [Fact]
    public void BuildHistory_OmiteVaziosPlaceholderESaudacao()
    {
        var mensagens = Historico(
            Msg("...", false),
            Msg(Welcome, false),
            Msg(string.Empty, false),
            Msg(" " , true),
            Msg("Qual é a capital de Portugal?", true));

        var historico = ChatComposerService.BuildHistory(mensagens, SystemPrompt, Welcome);

        Assert.Collection(historico,
            m => { Assert.Equal("system", m.Role); Assert.Equal(SystemPrompt, m.Content); },
            m => { Assert.Equal("user", m.Role); Assert.Contains("Qual é a capital de Portugal?", m.Content); });
    }

    [Fact]
    public void BuildHistory_FundeMensagensConsecutivasDoMesmoPapel()
    {
        var mensagens = Historico(
            Msg("primeira", true),
            Msg("segunda", true),
            Msg("resposta", false),
            Msg("pergunta final", true));

        var historico = ChatComposerService.BuildHistory(mensagens, SystemPrompt, Welcome);

        Assert.Collection(historico,
            m => { Assert.Equal("system", m.Role); Assert.Equal(SystemPrompt, m.Content); },
            m => { Assert.Equal("user", m.Role); Assert.Equal("primeira\nsegunda", m.Content); },
            m => { Assert.Equal("assistant", m.Role); Assert.Equal("resposta", m.Content); },
            m => { Assert.Equal("user", m.Role); Assert.Contains("pergunta final", m.Content); });
    }

    [Fact]
    public void BuildHistory_RemoveAssistantFinal()
    {
        var mensagens = Historico(
            Msg("pergunta", true),
            Msg("resposta", false));

        var historico = ChatComposerService.BuildHistory(mensagens, SystemPrompt, Welcome);

        Assert.Collection(historico,
            m => Assert.Equal("system", m.Role),
            m => Assert.Equal("user", m.Role));
    }

    [Fact]
    public async Task Prepare_SemPromptUserFinal_DevolveNull()
    {
        var servico = CriarServico();
        var mensagens = Historico(
            Msg("resposta sem pergunta", false));

        var preparado = await servico.Prepare(mensagens, "algo", SystemPrompt, Welcome, "modelo", 2048, true);

        Assert.Null(preparado);
    }

    [Fact]
    public async Task Prepare_PromptVazio_DevolveNull()
    {
        var servico = CriarServico();

        var preparado = await servico.Prepare(Historico(), "   ", SystemPrompt, Welcome, "modelo", 2048, true);

        Assert.Null(preparado);
    }

    [Fact]
    public async Task Prepare_DevolvePayloadComModeloStreamEContexto()
    {
        var servico = CriarServico(("Chat:EnableReasoning", "true"));
        var mensagens = Historico(Msg("O que é a inertia?", true));

        var preparado = await servico.Prepare(mensagens, "O que é a inertia?", SystemPrompt, Welcome, "qwen2.5", 4096, false, modelSupportsThinking: true);

        Assert.NotNull(preparado);
        Assert.Equal("qwen2.5", preparado.Payload.Model);
        Assert.True(preparado.Payload.Stream);
        Assert.Equal(4096, preparado.Payload.Options!["num_ctx"]);
        Assert.Equal(1.1, preparado.Payload.Options["repeat_penalty"]);
        Assert.True(preparado.EnableReasoning);
        Assert.True(preparado.Payload.Think);
    }

    [Fact]
    public async Task Prepare_ModeloSemThinking_OmiteCampoThink()
    {
        var servico = CriarServico(("Chat:EnableReasoning", "true"));
        var mensagens = Historico(Msg("O que é a inertia?", true));

        var preparado = await servico.Prepare(mensagens, "O que é a inertia?", SystemPrompt, Welcome, "qwen2.5", 4096, false, modelSupportsThinking: false);

        Assert.NotNull(preparado);
        Assert.Null(preparado.Payload.Think);
    }

    [Fact]
    public async Task Prepare_ThinkingDesativadoNaBd_EnviaThinkFalse()
    {
        var servico = CriarServico(("Chat:EnableReasoning", "false"));
        var mensagens = Historico(Msg("Pergunta", true));

        var preparado = await servico.Prepare(mensagens, "Pergunta", SystemPrompt, Welcome, "deepseek-r1", 4096, true, modelSupportsThinking: true);

        Assert.NotNull(preparado);
        Assert.False(preparado.EnableReasoning);
        Assert.Equal(false, preparado.Payload.Think);
    }

    [Fact]
    public async Task Prepare_ThinkingPorDefeito_EnviaThinkFalse()
    {
        var servico = CriarServico();
        var mensagens = Historico(Msg("Pergunta", true));

        var preparado = await servico.Prepare(mensagens, "Pergunta", SystemPrompt, Welcome, "deepseek-r1", 4096, true, modelSupportsThinking: true);

        Assert.NotNull(preparado);
        Assert.Equal(false, preparado.Payload.Think);
    }

    [Fact]
    public async Task Prepare_ThinkingAtivoEmModeloSemCapacidade_OmiteCampoThink()
    {
        var servico = CriarServico(("Chat:EnableReasoning", "true"));
        var mensagens = Historico(Msg("Pergunta", true));

        var preparado = await servico.Prepare(mensagens, "Pergunta", SystemPrompt, Welcome, "qwen2.5", 4096, true, modelSupportsThinking: false);

        Assert.NotNull(preparado);
        Assert.Null(preparado.Payload.Think);
    }

    [Fact]
    public async Task Prepare_OpcoesDeAmostragemSeguemTemperaturaRecomendada()
    {
        var servico = CriarServico();
        var prompt = "Traduz para português: hello world";
        double temp = ChatMeasureTemperature.ObterTemperaturaRecomendada(prompt);

        var preparado = await servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", 2048, true);

        Assert.NotNull(preparado);
        Assert.Equal(Math.Round(temp, 2), preparado.Payload.Options!["temperature"]);
        Assert.Equal(temp <= 0.25 ? 40 : 600, preparado.Payload.Options["top_k"]);
        Assert.Equal(temp <= 0.30 ? 0.85 : 0.92, preparado.Payload.Options["top_p"]);
    }

    [Fact]
    public async Task Prepare_MaxTokensVariaComFitInGpu()
    {
        var servico = CriarServico();
        var prompt = "Conta-me uma história criativa longa.";

        var semGpu = await servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", 0, false);
        var comGpu = await servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", 0, true);

        Assert.NotNull(semGpu);
        Assert.NotNull(comGpu);
        Assert.True(comGpu.MaxTokens > semGpu.MaxTokens);
    }

    [Fact]
    public async Task Prepare_MaxTokensNuncaExcedeContexto()
    {
        var servico = CriarServico();
        const int contexto = 512;
        var prompt = new string('a', 2000);

        var preparado = await servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", contexto, true);

        Assert.NotNull(preparado);
        Assert.True(preparado.MaxTokens <= contexto);
    }

    [Fact]
    public async Task Prepare_ComContextoReservadoClampaMaxTokensNoMinimo32()
    {
        var servico = CriarServico();
        const int contexto = 512;
        var prompt = new string('a', 2000);

        var preparado = await servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", contexto, true);

        Assert.NotNull(preparado);
        Assert.Equal(32, preparado.MaxTokens);
    }

    [Fact]
    public void EstimateTokens_TextoVazioDevolveZero()
    {
        Assert.Equal(0, ChatComposerService.EstimateTokens(null));
        Assert.Equal(0, ChatComposerService.EstimateTokens("   "));
    }

    [Fact]
    public void EstimateTokens_CaracteresPorQuatroComMinimoUm()
    {
        Assert.Equal(1, ChatComposerService.EstimateTokens("ab"));
        Assert.Equal(2, ChatComposerService.EstimateTokens("abcdefgh"));
    }

    [Fact]
    public void ExtractSummaryFromResponse_TextoValido_DevolveSummaryELimpaTexto()
    {
        var raw = "SUMMARY: Explica SQLite\n---\nSQLite é uma base de dados leve.";
        var (summary, clean) = ChatComposerService.ExtractSummaryFromResponse(raw);

        Assert.Equal("Explica SQLite", summary);
        Assert.Equal("SQLite é uma base de dados leve.", clean);
    }

    [Fact]
    public void ExtractSummaryFromResponse_SemSummary_DevolveNullETextoOriginal()
    {
        var raw = "SQLite é uma base de dados leve.";
        var (summary, clean) = ChatComposerService.ExtractSummaryFromResponse(raw);

        Assert.Null(summary);
        Assert.Equal(raw, clean);
    }

    [Fact]
    public void ExtractSummaryFromResponse_SemSeparador_DevolveNullETextoOriginal()
    {
        var raw = "SUMMARY: Explica SQLite\nSQLite é uma base de dados leve.";
        var (summary, clean) = ChatComposerService.ExtractSummaryFromResponse(raw);

        Assert.Null(summary);
        Assert.Equal(raw, clean);
    }

    [Fact]
    public void ExtractSummaryFromResponse_TextoVazio_DevolveNullETextoOriginal()
    {
        var (summary1, clean1) = ChatComposerService.ExtractSummaryFromResponse("");
        Assert.Null(summary1);
        Assert.Equal("", clean1);

        var (summary2, clean2) = ChatComposerService.ExtractSummaryFromResponse(null!);
        Assert.Null(summary2);
        Assert.Null(clean2);
    }

    [Fact]
    public void ExtractSummaryFromResponse_CaseInsensitive_DevolveSummary()
    {
        var raw = "summary: Resumo\n---\nCorpo da resposta.";
        var (summary, clean) = ChatComposerService.ExtractSummaryFromResponse(raw);

        Assert.Equal("Resumo", summary);
        Assert.Equal("Corpo da resposta.", clean);
    }

    [Fact]
    public void SmartFallbackDescription_PromptComPonto_TomaAteAoPonto()
    {
        var prompt = "Explica o que é SQLite. Dá exemplos.";
        var resultado = ChatComposerService.SmartFallbackDescription(prompt);

        Assert.Equal("Explica o que é SQLite", resultado);
    }

    [Fact]
    public void SmartFallbackDescription_PromptComNewline_TomaPrimeiraLinha()
    {
        var prompt = "Primeira linha importante\nSegunda linha irrelevante";
        var resultado = ChatComposerService.SmartFallbackDescription(prompt);

        Assert.Equal("Primeira linha importante", resultado);
    }

    [Fact]
    public void SmartFallbackDescription_PromptLongo_TruncNaFronteiraDePalavra()
    {
        var prompt = "Provide a concise summary listing the highest maximum temperature recorded in mainland Portugal for each month of the year 2020";
        var resultado = ChatComposerService.SmartFallbackDescription(prompt);

        Assert.StartsWith("Provide a concise summary listing", resultado);
        Assert.EndsWith("...", resultado);
        Assert.True(resultado.Length <= 53); // 50 + "..."
    }

    [Fact]
    public void SmartFallbackDescription_PromptCurto_DevolveTalQual()
    {
        var prompt = "O que é SQLite?";
        var resultado = ChatComposerService.SmartFallbackDescription(prompt);

        Assert.Equal("O que é SQLite?", resultado);
    }

    [Fact]
    public void SmartFallbackDescription_TextoVazio_DevolveVazio()
    {
        Assert.Equal(string.Empty, ChatComposerService.SmartFallbackDescription(""));
        Assert.Equal(string.Empty, ChatComposerService.SmartFallbackDescription("   "));
        Assert.Equal(string.Empty, ChatComposerService.SmartFallbackDescription(null!));
    }
}

/// <summary>
/// Repositório de definições em memória (substitui a BD SQLite nos testes).
/// </summary>
public sealed class FakeSettingsRepository : ISettingsRepository
{
    private readonly Dictionary<string, string> _valores = new(StringComparer.OrdinalIgnoreCase);

    public FakeSettingsRepository(params (string Chave, string Valor)[] valores)
    {
        foreach (var (chave, valor) in valores)
            _valores[chave] = valor;
    }

    public Task<string?> GetValueAsync(string chave) =>
        Task.FromResult(_valores.TryGetValue(chave, out var valor) ? valor : null);

    public Task<Dictionary<string, string>> GetValuesAsync(IEnumerable<string> chaves)
    {
        var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chave in chaves)
            if (_valores.TryGetValue(chave, out var valor))
                resultado[chave] = valor;
        return Task.FromResult(resultado);
    }

    public Task SetValueAsync(string chave, string valor)
    {
        _valores[chave] = valor;
        return Task.CompletedTask;
    }

    public Task DeleteValueAsync(string chave)
    {
        _valores.Remove(chave);
        return Task.CompletedTask;
    }
}
