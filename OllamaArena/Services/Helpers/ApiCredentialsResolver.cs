using OllamaArena.Services.Interfaces.Repositories;

namespace OllamaArena.Services.Helpers
{
    /// <summary>
    /// Credenciais e modelos dos provedores de IA, resolvidos com a prioridade
    /// BD (página Settings) → configuração (user-secrets/appsettings) → default.
    /// Usado pelo serviço de tradução e pelos juízes automáticos.
    /// </summary>
    public sealed record ProvedorDefinicoes(
        string GeminiChave,
        string GeminiModelo,
        string OpenRouterChave,
        string OpenRouterModelo);

    public sealed class ApiCredentialsResolver
    {
        public const string ChaveGemini = "ApiKeys:Gemini";
        public const string ChaveOpenRouter = "ApiKeys:OpenRouter";
        public const string ChaveModeloOpenRouter = "AutomatedJudge:OpenRouterModel";

        public const string GeminiModeloDefault = "gemini-flash-latest";
        public const string OpenRouterModeloDefault = "openrouter/free";

        // Comprimento mínimo para considerar uma chave plausível (evita "x" colado por engano).
        private const int ComprimentoMinimoChave = 10;

        private readonly ISettingsRepository _settings;
        private readonly IConfiguration _configuration;

        public ApiCredentialsResolver(ISettingsRepository settings, IConfiguration configuration)
        {
            _settings = settings;
            _configuration = configuration;
        }

        public async Task<ProvedorDefinicoes> ResolverAsync()
        {
            var daBd = await _settings.GetValuesAsync(
            [
                ChaveGemini,
                ChaveOpenRouter,
                ChaveModeloOpenRouter
            ]);

            string gemini = daBd.GetValueOrDefault(ChaveGemini) ?? _configuration[ChaveGemini] ?? string.Empty;
            string openRouter = daBd.GetValueOrDefault(ChaveOpenRouter) ?? _configuration[ChaveOpenRouter] ?? string.Empty;
            string modeloOpenRouter = daBd.GetValueOrDefault(ChaveModeloOpenRouter)
                ?? _configuration[ChaveModeloOpenRouter]
                ?? OpenRouterModeloDefault;

            return new ProvedorDefinicoes(
                LimparChave(gemini),
                GeminiModeloDefault,
                LimparChave(openRouter),
                string.IsNullOrWhiteSpace(modeloOpenRouter) ? OpenRouterModeloDefault : modeloOpenRouter.Trim());
        }

        /// <summary>Normaliza uma chave colada (espaços e quebras de linha do bloco de notas).</summary>
        public static string LimparChave(string? chave) =>
            chave?.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "") ?? string.Empty;

        public static bool TemChaveValida(string? chave)
        {
            var limpa = LimparChave(chave);
            return limpa.Length >= ComprimentoMinimoChave;
        }
    }
}
