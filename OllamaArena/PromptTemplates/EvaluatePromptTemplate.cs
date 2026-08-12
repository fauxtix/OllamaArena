using OllamaArena.Services;

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

        public async Task<string> EvaluationCopyPromptAsync(string originalPrompt, string modelResponse, string trainingYear, string modelName)
        {
            string template = await _promptFilesService.GetPromptFileContentAsync(TemplatePath) ?? string.Empty;

            return template
                .Replace("{modelName}", modelName ?? string.Empty)
                .Replace("{originalPrompt}", originalPrompt ?? string.Empty)
                .Replace("{modelResponse}", modelResponse ?? string.Empty)
                .Replace("{trainingYear}", trainingYear ?? string.Empty);
        }
    }
}