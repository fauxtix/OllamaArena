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

        /// <summary>Nota máxima permitida quando a resposta está no idioma certo mas mistura línguas.</summary>
        public const int ScoreMaximoComMistura = 3;

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

        /// <summary>
        /// Indica mistura significativa da outra língua num texto cujo idioma dominante é
        /// o esperado, nas duas direções (frases inglesas incrustadas em português e vice-
        /// versa). Complementa <see cref="DeveCapar"/>: divergência clara e textos
        /// inconclusivos ficam de fora.
        /// </summary>
        public static bool DeveCaparPorMistura(string? textoResposta, string? idiomaSessao)
        {
            var codigoEsperado = CodigoEsperado(idiomaSessao);
            var dominanteEsperado = codigoEsperado.Equals("en", StringComparison.OrdinalIgnoreCase)
                ? DetectedLanguage.English
                : DetectedLanguage.Portuguese;

            if (ResponseLanguageChecker.Detect(textoResposta) != dominanteEsperado)
                return false;

            return ResponseLanguageChecker.ContemMisturaSignificativa(textoResposta, codigoEsperado, out _);
        }

        /// <summary>Limita a nota do juiz ao máximo; nota ausente (null) também fica penalizada.</summary>
        public static int Capar(int? scoreJuiz) => Math.Min(scoreJuiz ?? 5, ScoreMaximoComMismatch);

        /// <summary>Limita a nota do juiz ao máximo indicado; nota ausente (null) conta como 5.</summary>
        public static int Capar(int? scoreJuiz, int maximo) => Math.Min(scoreJuiz ?? 5, maximo);

        /// <summary>Limita a nota do juiz quando há mistura de línguas; nota ausente também fica penalizada.</summary>
        public static int CaparMistura(int? scoreJuiz) => Math.Min(scoreJuiz ?? 5, ScoreMaximoComMistura);
    }
}
