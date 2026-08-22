using System.Globalization;
using System.Text.RegularExpressions;

namespace OllamaArena.Services.Helpers
{
    /// <summary>
    /// Rede de segurança determinística para a métrica Language Consistency dos juízes:
    /// deteta localmente (heurística PT/EN) quando a resposta claramente não respeita o
    /// idioma esperado e limita a nota devolvida pelo juiz — sem depender da disciplina
    /// do LLM (modelos gratuitos podem dar 5 a tudo). Inconclusivo nunca interfere.
    /// </summary>
    public static partial class JudgeLanguageGuard
    {
        [GeneratedRegex(@"\(([a-zA-Z]{2})-")]
        private static partial Regex RegexCodigoIdioma();

        /// <summary>Nota máxima permitida quando a resposta está claramente no idioma errado.</summary>
        public const int ScoreMaximoComMismatch = 2;

        /// <summary>
        /// Código de 2 letras do idioma esperado: extrai-o de "European Portuguese (pt-PT)".
        /// Sem idioma gravado na resposta (respostas antigas), assume o idioma atual do
        /// seletor PT/EN (cultura da UI).
        /// </summary>
        public static string CodigoEsperado(string? idiomaSessao)
        {
            if (!string.IsNullOrWhiteSpace(idiomaSessao))
            {
                var match = RegexCodigoIdioma().Match(idiomaSessao);
                if (match.Success)
                    return match.Groups[1].Value.ToLowerInvariant();
            }

            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        }

        /// <summary>
        /// Indica se a resposta diverge claramente do idioma esperado.
        /// Só devolve true quando a deteção local é conclusiva.
        /// </summary>
        public static bool DeveCapar(string? textoResposta, string? idiomaSessao, out DetectedLanguage detetado)
        {
            detetado = ResponseLanguageChecker.Detect(textoResposta);
            return ResponseLanguageChecker.IsMismatch(detetado, CodigoEsperado(idiomaSessao));
        }

        /// <summary>Limita a nota do juiz ao máximo; nota ausente (null) também fica penalizada.</summary>
        public static int Capar(int? scoreJuiz) => Math.Min(scoreJuiz ?? 5, ScoreMaximoComMismatch);
    }
}
