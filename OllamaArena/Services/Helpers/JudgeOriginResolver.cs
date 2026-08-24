using Microsoft.Extensions.Localization;
using OllamaArena.Resources;

namespace OllamaArena.Services.Helpers;

/// <summary>
/// Origem de cada slot de juiz numa avaliação de benchmark:
/// 'gemini'/'openrouter' quando o slot foi preenchido pelo serviço automático,
/// 'externo' quando o utilizador colou/preencheu manualmente, null quando o slot
/// está vazio ("sem resposta") ou pertence a um registo anterior à funcionalidade.
/// </summary>
public static class JudgeOriginResolver
{
    public const string Gemini = "gemini";
    public const string OpenRouter = "openrouter";
    public const string Externo = "externo";

    /// <summary>Um slot tem conteúdo se tem feedback não vazio ou nota global atribuída.</summary>
    public static bool TemConteudo(string? feedback, float? notaGlobal)
        => !string.IsNullOrWhiteSpace(feedback) || notaGlobal.HasValue;

    /// <summary>Devolve a origem apenas se for uma chave conhecida; caso contrário null.</summary>
    public static string? Normalizar(string? origem)
        => origem is Gemini or OpenRouter or Externo ? origem : null;

    /// <summary>
    /// Calcula a origem a gravar para um slot, na ordem: slot sem conteúdo → null;
    /// origem já gravada (em memória ou BD) → mantém-se; slot já tinha conteúdo à
    /// abertura do diálogo mas nunca teve origem (registo legado) → mantém-se null
    /// para não inventar origem; nova entrada manual → 'externo'.
    /// </summary>
    public static string? CalcularOrigemSlot(
        bool temConteudoAgora,
        bool temConteudoNaAbertura,
        string? origemGravada)
    {
        if (!temConteudoAgora) return null;

        var gravada = Normalizar(origemGravada);
        if (gravada != null) return gravada;

        if (temConteudoNaAbertura) return null;

        return Externo;
    }
}

/// <summary>Mapeia uma origem para o rótulo localizado exibido ao utilizador.</summary>
public static class JudgeLabelResolver
{
    public static string Rotulo(IStringLocalizer<SharedResources> L, string? origem, bool temConteudo, bool primeiroSlot)
        => JudgeOriginResolver.Normalizar(origem) switch
        {
            JudgeOriginResolver.Gemini => L["Evaluation.JudgeGemini"].Value,
            JudgeOriginResolver.OpenRouter => L["Evaluation.JudgeOpenRouter"].Value,
            JudgeOriginResolver.Externo => primeiroSlot ? L["Evaluation.Judge1"].Value : L["Evaluation.Judge2"].Value,
            _ => temConteudo
                ? (primeiroSlot ? L["Evaluation.JudgeGemini"].Value : L["Evaluation.JudgeOpenRouter"].Value)
                : L["Evaluation.NoResponse"].Value
        };

    public static bool SemResposta(string? origem, bool temConteudo)
        => JudgeOriginResolver.Normalizar(origem) is null && !temConteudo;
}
