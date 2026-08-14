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

                Response.GeminiRating = parsed.FinalScore;
                Response.GeminiFeedback = parsed.Description;
                Response.GeminiRecommendation = parsed.Recommendation;
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

                Response.OpenRouterRating = parsed.FinalScore;
                Response.OpenRouterFeedback = parsed.Description;
                Response.OpenRouterRecommendation = parsed.Recommendation;
            }
            else
            {
                Response.OpenRouterFeedback = value;
            }

            StateHasChanged();
            await OnEvaluationChanged.InvokeAsync();
        }
    }
}