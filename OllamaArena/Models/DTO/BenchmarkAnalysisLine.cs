using System.Text;
using System.Text.RegularExpressions;

namespace OllamaArena.Models.DTO;

/// <summary>
/// Uma linha da análise detalhada, separada em rótulo (apresentado a negrito) e
/// texto.
/// </summary>
/// <remarks>
/// Antes estas linhas eram <c>string</c> com HTML vindo dos recursos localizados
/// (<c>&lt;strong&gt;</c>, <c>&lt;em&gt;</c>), o que obrigava a vista a renderizá-las
/// com <c>MarkupString</c> — e a sanear. A marcação passou para a vista, que a
/// constrói a partir destes dois campos; o serviço devolve dados, não HTML. Como o
/// Blazor escapa o texto por omissão, um nome de modelo hostil é mostrado
/// literalmente em vez de ser interpretado.
/// </remarks>
public sealed record BenchmarkAnalysisLine(string? Rotulo, string Texto)
{
    private static readonly Regex StrongRegex =
        new("<strong>(.*?)</strong>", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Tags de formataçãoinline que os recursos podem usar.</summary>
    private static readonly Regex TagsConhecidasRegex =
        new("</?(strong|em|b|i)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Converte um recurso localizado (que pode conter <c>&lt;strong&gt;</c>) numa
    /// linha com rótulo e texto. Só a marcação de formatação inline é retirada:
    /// qualquer outra coisa — incluindo o nome do modelo, que vem da base de dados —
    /// é preservada e Treatada como texto.
    /// </summary>
    public static BenchmarkAnalysisLine Criar(string? marcado)
    {
        var texto = marcado ?? string.Empty;

        var match = StrongRegex.Match(texto);

        // Só há rótulo quando o <strong> abre a linha (eventualmente atrás de um
        // marcador como "• " ou um emoji). Se houver texto antes, é uma frase e a
        // linha não tem rótulo.
        if (!match.Success || TieneLetras(texto[..match.Index]))
            return new BenchmarkAnalysisLine(null, Limpar(texto));

        var rotulo = Limpar(texto[..match.Index] + match.Groups[1].Value);
        var corpo = Limpar(texto[(match.Index + match.Length)..]);

        return new BenchmarkAnalysisLine(
            string.IsNullOrEmpty(rotulo) ? null : rotulo,
            corpo);
    }

    /// <summary>Indica se o troço contém letras (para distinguir "• " de uma frase).</summary>
    private static bool TieneLetras(string valor) => valor.Any(char.IsLetter);

    /// <summary>
    /// Retira a marcação de formatação inline e descodifica as entidades que os
    /// recursos usam (&amp;le;, &amp;ge;, &amp;nbsp;…). Espaços repetidos
    /// resultantes da remoção de tags são colapsados.
    /// </summary>
    private static string Limpar(string valor)
    {
        var semMarcacao = TagsConhecidasRegex.Replace(valor, string.Empty);
        var descodificada = DescodificarEntidades(semMarcacao);

        var resultado = new StringBuilder(descodificada.Length);
        bool espacoAnterior = false;

        foreach (var c in descodificada)
        {
            // As entidades das entidades produzem espaços duplos ("x  y").
            if (char.IsWhiteSpace(c))
            {
                if (!espacoAnterior)
                    resultado.Append(' ');

                espacoAnterior = true;
                continue;
            }

            espacoAnterior = false;
            resultado.Append(c);
        }

        return resultado.ToString().Trim();
    }

    private static string DescodificarEntidades(string valor)
    {
        if (!valor.Contains('&'))
            return valor;

        return valor
            .Replace("&nbsp;", " ", StringComparison.Ordinal)
            .Replace("&le;", "≤", StringComparison.Ordinal)
            .Replace("&ge;", "≥", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&#39;", "'", StringComparison.Ordinal);
    }
}
