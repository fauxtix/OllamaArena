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
        /// (ex.: "English (en-US)"); quando null (respostas antigas), assume o idioma
        /// atual do seletor PT/EN e marca o bloco como suposição.
        /// </summary>
        public async Task<string> EvaluationCopyPromptAsync(string originalPrompt, string modelResponse, string trainingYear, string modelName, string? expectedLanguage = null, PromptTheme? theme = null)
        {
            var tema = theme ?? PromptThemeClassifier.Classificar(originalPrompt);

            string template = await _promptFilesService.GetPromptFileContentAsync(TemplatePath) ?? string.Empty;

            bool idiomaGravado = !string.IsNullOrWhiteSpace(expectedLanguage);
            var idiomaEfetivo = idiomaGravado ? expectedLanguage : TargetLanguageResolver.GetTargetLanguage();

            return template
                .Replace("{modelName}", modelName ?? string.Empty)
                .Replace("{originalPrompt}", originalPrompt ?? string.Empty)
                .Replace("{modelResponse}", modelResponse ?? string.Empty)
                .Replace("{trainingYear}", trainingYear ?? string.Empty)
                .Replace("{expectedLanguage}", ConstruirBlocoIdioma(idiomaEfetivo, idiomaGravado))
                .Replace("{promptTheme}", PromptThemeClassifier.NomeExibicao(tema))
                .Replace("{themeGuidance}", PromptThemeClassifier.Orientacao(tema));
        }

        /// <summary>
        /// Bloco de contexto injetado no placeholder {expectedLanguage} do template.
        /// Sem idioma (null/branco) mantém o comportamento antigo: avaliar só pela língua
        /// do prompt. Com <paramref name="gravadoNaResposta"/> a false indica ao juiz que
        /// o idioma é uma suposição (idioma atual da app para respostas antigas).
        /// </summary>
        public static string ConstruirBlocoIdioma(string? expectedLanguage, bool gravadoNaResposta = true)
        {
            if (string.IsNullOrWhiteSpace(expectedLanguage))
                return "Not recorded. Judge Language Consistency against the language of the original prompt only.";

            return gravadoNaResposta
                ? $"{expectedLanguage} — selected by the user in the app; the local model was explicitly instructed to respond ONLY in this language."
                : $"{expectedLanguage} (assumed from the current app language; not recorded for this older response); the local model is expected to respond ONLY in this language.";
        }
    }
}
