using OllamaArena.Models.DTO;

namespace OllamaArena.Services.Interfaces.Services;

/// <summary>
/// Gera a folha de cálculo dos benchmarks e disponibiliza-a para descarga por HTTP.
/// </summary>
/// <remarks>
/// A geração não devolve os bytes ao componente: grava o ficheiro numa pasta
/// temporária e devolve um identificador. A descarga acontece por um endpoint
/// HTTP (<c>GET</c>), e não pelo circuito SignalR, porque o payload em base64
/// contava com o limite de 32 MB do circuito e rebentava em exportações grandes.
/// </remarks>
public interface IBenchmarkExcelExporter
{
    /// <summary>
    /// Escreve o workbook e devolve o identificador da descarga.
    /// </summary>
    /// <param name="dados">Linhas a exportar (já filtradas).</param>
    /// <param name="nomeSugerido">Nome proposto ao utilizador, sem extensão.</param>
    /// <param name="cancellationToken">Permite cancelar antes de começar a escrever.</param>
    Task<BenchmarkExcelDownload> GerarAsync(
        IReadOnlyList<BenchmarkEvaluationModel> dados,
        string nomeSugerido,
        CancellationToken cancellationToken = default);

    /// <summary>Remove ficheiros de exportações anteriores que ninguém descarregou.</summary>
    Task LimparTemporariosAsync(CancellationToken cancellationToken = default);
}

/// <summary>Descritor de uma exportação pronta a ser descarregada.</summary>
/// <param name="Identificador">Token opaco usado no URL da descarga.</param>
/// <param name="NomeFicheiro">Nome com que o browser deve gravar.</param>
public sealed record BenchmarkExcelDownload(string Identificador, string NomeFicheiro);
