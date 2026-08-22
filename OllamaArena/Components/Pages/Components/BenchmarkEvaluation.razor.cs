using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;
using OllamaArena.Models.Entities;
using OllamaArena.Services.Helpers;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;

namespace OllamaArena.Components.Pages.Components
{
    public partial class BenchmarkEvaluation
    {
        [Parameter] public BenchmarkResponse Response { get; set; } = default!;
        [Parameter] public EventCallback OnEvaluationChanged { get; set; }

        private bool _geminiRecommendationOpen;
        private bool _openRouterRecommendationOpen;

        private static Icon GetChevron(bool open)
            => open ? new Icons.Regular.Size12.ChevronUp() : new Icons.Regular.Size12.ChevronDown();

        private static void ToggleRecomendacaoTecla(KeyboardEventArgs e, Action toggle)
        {
            if (e.Key == " " || e.Key == "Enter")
            {
                toggle();
            }
        }

        private async Task OnGeminiInputChanged(string value)
        {
            if (Response is null) return;

            if (value.Contains("FACTUAL_SCORE:", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = EvaluationParser.ParseEvaluation(value);
                var notaIdioma = AplicarGuardaIdiomaLocal(parsed);

                Response.GeminiFactualRating = parsed.FactualScore;
                Response.GeminiFormattingRating = parsed.FormattingScore;
                Response.GeminiComplianceRating = parsed.ComplianceScore;
                Response.GeminiRelevanceRating = parsed.RelevanceScore;
                Response.GeminiToneRating = parsed.ToneScore;
                Response.GeminiConcisenessRating = parsed.ConcisenessScore;
                Response.GeminiClarityRating = parsed.ClarityScore;
                Response.GeminiReadabilityRating = parsed.ReadabilityScore;
                Response.GeminiHaloEffectRating = parsed.HaloEffectScore;
                Response.GeminiSafetyRating = parsed.SafetyScore;
                Response.GeminiLanguageConsistencyRating = parsed.LanguageConsistencyScore;
                Response.GeminiLoopDetectionRating = parsed.LoopDetectionScore;
                Response.GeminiRefusalHandled = parsed.RefusalHandledFlag;

                Response.GeminiRating = parsed.FinalScore;
                Response.GeminiFeedback = parsed.Description;
                Response.GeminiRecommendation = CombinarComNotaIdioma(notaIdioma, parsed.Recommendation);
            }
            else
            {
                Response.GeminiFeedback = value;
            }

            StateHasChanged();
            await OnEvaluationChanged.InvokeAsync();
        }

        private async Task OnOpenRouterInputChanged(string value)
        {
            if (Response is null) return;

            if (value.Contains("FACTUAL_SCORE:", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = EvaluationParser.ParseEvaluation(value);
                var notaIdioma = AplicarGuardaIdiomaLocal(parsed);

                Response.OpenRouterFactualRating = parsed.FactualScore;
                Response.OpenRouterFormattingRating = parsed.FormattingScore;
                Response.OpenRouterComplianceRating = parsed.ComplianceScore;
                Response.OpenRouterRelevanceRating = parsed.RelevanceScore;
                Response.OpenRouterToneRating = parsed.ToneScore;
                Response.OpenRouterConcisenessRating = parsed.ConcisenessScore;
                Response.OpenRouterClarityRating = parsed.ClarityScore;
                Response.OpenRouterReadabilityRating = parsed.ReadabilityScore;
                Response.OpenRouterHaloEffectRating = parsed.HaloEffectScore;
                Response.OpenRouterSafetyRating = parsed.SafetyScore;
                Response.OpenRouterLanguageConsistencyRating = parsed.LanguageConsistencyScore;
                Response.OpenRouterLoopDetectionRating = parsed.LoopDetectionScore;
                Response.OpenRouterRefusalHandled = parsed.RefusalHandledFlag;

                Response.OpenRouterRating = parsed.FinalScore;
                Response.OpenRouterFeedback = parsed.Description;
                Response.OpenRouterRecommendation = CombinarComNotaIdioma(notaIdioma, parsed.Recommendation);
            }
            else
            {
                Response.OpenRouterFeedback = value;
            }

            StateHasChanged();
            await OnEvaluationChanged.InvokeAsync();
        }

        /// <summary>
        /// Rede de segurança local para o idioma na colagem manual: limita a nota
        /// Language Consistency quando a resposta diverge claramente do idioma esperado
        /// (cap 2) ou mistura línguas num texto no idioma certo (cap 3).
        /// </summary>
        private string? AplicarGuardaIdiomaLocal(ParsedEvaluationResult parsed)
        {
            if (JudgeLanguageGuard.DeveCapar(Response.TextoResposta, Response.IdiomaSessao, out var detetado))
            {
                parsed.LanguageConsistencyScore = JudgeLanguageGuard.Capar(parsed.LanguageConsistencyScore);

                var idiomaEsperado = string.IsNullOrWhiteSpace(Response.IdiomaSessao)
                    ? TargetLanguageResolver.GetTargetLanguage()
                    : Response.IdiomaSessao!;

                return L["Common.LocalLanguageOverride", NomeIdiomaDetetado(detetado), idiomaEsperado].Value;
            }

            if (JudgeLanguageGuard.DeveCaparPorMistura(Response.TextoResposta, Response.IdiomaSessao))
            {
                parsed.LanguageConsistencyScore = JudgeLanguageGuard.CaparMistura(parsed.LanguageConsistencyScore);

                var idiomaEsperadoMistura = string.IsNullOrWhiteSpace(Response.IdiomaSessao)
                    ? TargetLanguageResolver.GetTargetLanguage()
                    : Response.IdiomaSessao!;
                var linguaIntrusa = JudgeLanguageGuard.CodigoEsperado(Response.IdiomaSessao) == "pt"
                    ? L["Common.DetectedLang.En"].Value
                    : L["Common.DetectedLang.Pt"].Value;

                return L["Common.LocalLanguageMixOverride", linguaIntrusa, idiomaEsperadoMistura].Value;
            }

            return null;
        }

        private string NomeIdiomaDetetado(DetectedLanguage detetado)
        {
            return (detetado == DetectedLanguage.Portuguese
                ? L["Common.DetectedLang.Pt"]
                : L["Common.DetectedLang.En"]).Value;
        }

        private static string CombinarComNotaIdioma(string? notaIdioma, string? recomendacao)
        {
            if (string.IsNullOrWhiteSpace(notaIdioma)) return recomendacao ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recomendacao)) return notaIdioma;
            return $"{notaIdioma} {recomendacao}";
        }

        private static string WeakClass(int? value, int threshold = 3)
        {
            return value.HasValue && value.Value <= threshold ? "rating-cell-weak" : string.Empty;
        }
    }
}