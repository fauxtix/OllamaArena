using OllamaArena.Services;
using OllamaArena.Services.Helpers;

namespace OllamaArena.PromptTemplates
{
    public  class EvaluatePromptTemplate
    {
        private const string TemplatePath = "evaluation_prompt_template.txt";
        private readonly PromptFilesService _promptFilesService;

        public EvaluatePromptTemplate(PromptFilesService promptFilesService)
        {
            _promptFilesService = promptFilesService;
        }

        /// <summary>
        /// Constrói o prompt de avaliação a partir do template. O tema do prompt
        /// original é auto-detetado (PromptThemeClassifier) quando não é indicado,
        /// tornando a recomendação do juiz contextual por temática.
        /// <paramref name="expectedLanguage"/> é o idioma da sessão gravado na resposta
        /// (ex.: "English (en-US)"); quando null (respostas antigas), o juiz avalia
        /// apenas a adesão à língua do prompt original.
        /// </summary>
        public async Task<string> EvaluationCopyPromptAsync(string originalPrompt, string modelResponse, string trainingYear, string modelName, string? expectedLanguage = null, PromptTheme? theme = null)
        {
            var tema = theme ?? PromptThemeClassifier.Classificar(originalPrompt);

            string template = await _promptFilesService.GetPromptFileContentAsync(TemplatePath) ?? string.Empty;

            return template
                .Replace("{modelName}", modelName ?? string.Empty)
                .Replace("{originalPrompt}", originalPrompt ?? string.Empty)
                .Replace("{modelResponse}", modelResponse ?? string.Empty)
                .Replace("{trainingYear}", trainingYear ?? string.Empty)
                .Replace("{expectedLanguage}", ConstruirBlocoIdioma(expectedLanguage))
                .Replace("{promptTheme}", PromptThemeClassifier.NomeExibicao(tema))
                .Replace("{themeGuidance}", PromptThemeClassifier.Orientacao(tema));
        }

        /// <summary>
        /// Bloco de contexto injetado no placeholder {expectedLanguage} do template,
        /// indicando ao juiz o idioma esperado da resposta ou a sua ausência.
        /// </summary>
        public static string ConstruirBlocoIdioma(string? expectedLanguage)
        {
            return string.IsNullOrWhiteSpace(expectedLanguage)
                ? "Not recorded. Judge Language Consistency against the language of the original prompt only."
                : $"{expectedLanguage} — selected by the user in the app; the local model was explicitly instructed to respond ONLY in this language.";
        }
    }
}
