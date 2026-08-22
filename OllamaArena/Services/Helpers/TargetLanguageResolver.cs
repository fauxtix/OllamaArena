using System.Globalization;

namespace OllamaArena.Services.Helpers
{
    /// <summary>
    /// Resolve o idioma-alvo das respostas dos modelos a partir da cultura da UI
    /// (cookie do seletor PT/EN). Partilhado pelo chat e pelo serviço de tradução.
    /// </summary>
    public static class TargetLanguageResolver
    {
        /// <summary>Token substituído nos prompts editáveis (ex.: system-prompt.txt).</summary>
        public const string Token = "{{TARGET_LANGUAGE}}";

        public static string GetTargetLanguage()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
            {
                "pt" => "European Portuguese (pt-PT)",
                "en" => "English (en-US)",
                _ => "European Portuguese (pt-PT)"
            };
        }
    }
}
