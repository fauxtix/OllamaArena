using System.Globalization;

namespace OllamaArena.Services.Helpers;

/// <summary>
/// Compara as durações formatadas pelo valor numérico, não alfabeticamente.
/// </summary>
/// <remarks>
/// A aplicação tem dois formatos: <c>m:ss</c> na grelha de avaliações
/// (<c>BenchmarkEvaluationModel.TempoPuroFormatado</c>) e <c>HH:mm:ss</c> no painel de
/// histórico (<c>HistoryResponse.DurationDisplay</c>). Ordenar <c>string</c> com o
/// comparador por omissão dá <c>"1:05"</c> depois de <c>"10:00"</c> e
/// <c>"00:59"</c> depois de <c>"00:100"</c> — correto como texto, errado como tempo.
/// Esta classe devolve a comparação em milissegundos e mantém a ordenação estável
/// quando o valor é igual.
/// </remarks>
public sealed class DurationTextComparer : IComparer<string>
{
    public static DurationTextComparer Instance { get; } = new();

    private DurationTextComparer() { }

    public int Compare(string? x, string? y)
    {
        var diferenca = Milissegundos(x).CompareTo(Milissegundos(y));
        return diferenca != 0 ? diferenca : string.CompareOrdinal(x, y);
    }

    /// <summary>
    /// Converte o texto num número de milissegundos. Aceita <c>HH:mm:ss</c>,
    /// <c>mm:ss</c> e um número já em milissegundos; o que não interpretar vale 0.
    /// </summary>
    private static double Milissegundos(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return 0;

        var partes = valor.Split(':');

        // Aceita 2 ou 3 segmentos: as horas só existem no formato do HistoryPanel.
        // Aceitar mais do que isso (ou segmentos não numéricos) cairia no número puro
        // abaixo, que é o comportamento desejado para texto vazio de sentido.
        if (partes.Length is 2 or 3
            && partes.All(p => double.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
        {
            double total = 0;
            foreach (var parte in partes)
                total = (total * 60) + double.Parse(parte, NumberStyles.Integer, CultureInfo.InvariantCulture);

            return total * 1000;
        }

        return double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero)
            ? numero
            : 0;
    }
}