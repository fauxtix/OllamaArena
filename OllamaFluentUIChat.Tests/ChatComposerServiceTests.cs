using Microsoft.Extensions.Options;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Services;

namespace OllamaFluentUIChat.Tests;

public class ChatComposerServiceTests
{
    private const string SystemPrompt = "Instruções do sistema";
    private const string Welcome = "Olá!";

    private static ChatComposerService CriarServico() => new(Options.Create(new OllamaOptions()));

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
            m => { Assert.Equal("user", m.Role); Assert.Equal("Qual é a capital de Portugal?", m.Content); });
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
            m => { Assert.Equal("user", m.Role); Assert.Equal("pergunta final", m.Content); });
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
    public void Prepare_SemPromptUserFinal_DevolveNull()
    {
        var servico = CriarServico();
        var mensagens = Historico(
            Msg("resposta sem pergunta", false));

        var preparado = servico.Prepare(mensagens, "algo", SystemPrompt, Welcome, "modelo", 2048, true);

        Assert.Null(preparado);
    }

    [Fact]
    public void Prepare_PromptVazio_DevolveNull()
    {
        var servico = CriarServico();

        var preparado = servico.Prepare(Historico(), "   ", SystemPrompt, Welcome, "modelo", 2048, true);

        Assert.Null(preparado);
    }

    [Fact]
    public void Prepare_DevolvePayloadComModeloStreamEContexto()
    {
        var servico = CriarServico();
        var mensagens = Historico(Msg("O que é a inertia?", true));

        var preparado = servico.Prepare(mensagens, "O que é a inertia?", SystemPrompt, Welcome, "qwen2.5", 4096, false, modelSupportsThinking: true);

        Assert.NotNull(preparado);
        Assert.Equal("qwen2.5", preparado.Payload.Model);
        Assert.True(preparado.Payload.Stream);
        Assert.Equal(4096, preparado.Payload.Options!["num_ctx"]);
        Assert.Equal(1.1, preparado.Payload.Options["repeat_penalty"]);
        Assert.True(preparado.Payload.Think);
    }

    [Fact]
    public void Prepare_ModeloSemThinking_OmiteCampoThink()
    {
        var servico = CriarServico();
        var mensagens = Historico(Msg("O que é a inertia?", true));

        var preparado = servico.Prepare(mensagens, "O que é a inertia?", SystemPrompt, Welcome, "qwen2.5", 4096, false, modelSupportsThinking: false);

        Assert.NotNull(preparado);
        Assert.Null(preparado.Payload.Think);
    }

    [Fact]
    public void Prepare_ThinkingDesativadoNaConfiguracao_OmiteCampoThink()
    {
        var opcoes = new OllamaOptions { EnableReasoning = false };
        var servico = new ChatComposerService(Options.Create(opcoes));
        var mensagens = Historico(Msg("Pergunta", true));

        var preparado = servico.Prepare(mensagens, "Pergunta", SystemPrompt, Welcome, "deepseek-r1", 4096, true, modelSupportsThinking: true);

        Assert.NotNull(preparado);
        Assert.Null(preparado.Payload.Think);
    }

    [Fact]
    public void Prepare_OpcoesDeAmostragemSeguemTemperaturaRecomendada()
    {
        var servico = CriarServico();
        var prompt = "Traduz para português: hello world";
        double temp = ChatMeasureTemperature.ObterTemperaturaRecomendada(prompt);

        var preparado = servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", 2048, true);

        Assert.NotNull(preparado);
        Assert.Equal(Math.Round(temp, 2), preparado.Payload.Options!["temperature"]);
        Assert.Equal(temp <= 0.25 ? 40 : 600, preparado.Payload.Options["top_k"]);
        Assert.Equal(temp <= 0.30 ? 0.85 : 0.92, preparado.Payload.Options["top_p"]);
    }

    [Fact]
    public void Prepare_MaxTokensVariaComFitInGpu()
    {
        var servico = CriarServico();
        var prompt = "Conta-me uma história criativa longa.";

        var semGpu = servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", 0, false);
        var comGpu = servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", 0, true);

        Assert.NotNull(semGpu);
        Assert.NotNull(comGpu);
        Assert.True(comGpu.MaxTokens > semGpu.MaxTokens);
    }

    [Fact]
    public void Prepare_MaxTokensNuncaExcedeContexto()
    {
        var servico = CriarServico();
        const int contexto = 512;
        var prompt = new string('a', 2000);

        var preparado = servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", contexto, true);

        Assert.NotNull(preparado);
        Assert.True(preparado.MaxTokens <= contexto);
    }

    [Fact]
    public void Prepare_ComContextoReservadoClampaMaxTokensNoMinimo32()
    {
        var servico = CriarServico();
        const int contexto = 512;
        var prompt = new string('a', 2000);

        var preparado = servico.Prepare(Historico(Msg(prompt, true)), prompt, SystemPrompt, Welcome, "modelo", contexto, true);

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
}
