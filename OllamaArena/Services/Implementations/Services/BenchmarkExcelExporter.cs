using ClosedXML.Excel;
using Microsoft.Extensions.Localization;
using OllamaArena.Models.DTO;
using OllamaArena.Resources;
using OllamaArena.Services.Interfaces.Services;

namespace OllamaArena.Services.Implementations.Services;

/// <inheritdoc cref="IBenchmarkExcelExporter"/>
/// <remarks>
/// O workbook é montado a partir dos rótulos localizados, por isso o exportador
/// depende do localizador em vez de receber os textos já resolvidos: a página deixava
/// de ter a lógica de formatação e o endpoint deixa de depender da culture da
/// interface.
/// </remarks>
public sealed class BenchmarkExcelExporter : IBenchmarkExcelExporter
{
    /// <summary>Ficheiros mais velhos que isto são removidos em cada exportação.</summary>
    private static readonly TimeSpan ValidadeTemporarios = TimeSpan.FromHours(1);

    /// <summary>
    /// Subpasta dedicada dentro da pasta temporária do sistema. O nome do ficheiro é
    /// sempre um <c>Guid</c> gerado por este serviço, e o endpoint reconstrói o
    /// caminho a partir do identificador — nunca a partir do texto recebido.
    /// </summary>
    private static string PastaTemporaria => Path.Combine(Path.GetTempPath(), "ollamaarena-exports");

    private readonly IStringLocalizer<SharedResources> _L;

    public BenchmarkExcelExporter(IStringLocalizer<SharedResources> localizador)
        => _L = localizador;

    public async Task<BenchmarkExcelDownload> GerarAsync(
        IReadOnlyList<BenchmarkEvaluationModel> dados,
        string nomeSugerido,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);

        // A geração é CPU-bound: fora do contexto do renderer para não congelar o
        // circuito durante a exportação.
        var conteudo = await Task.Run(() => GerarWorkbook(dados), cancellationToken);

        var identificador = Guid.NewGuid().ToString("N");
        var pasta = Directory.CreateDirectory(PastaTemporaria);
        await File.WriteAllBytesAsync(Path.Combine(pasta.FullName, identificador + ".xlsx"), conteudo, cancellationToken);

        await LimparTemporariosAsync(cancellationToken);

        return new BenchmarkExcelDownload(identificador, $"{nomeSugerido}.xlsx");
    }

    public Task LimparTemporariosAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(PastaTemporaria))
                return Task.CompletedTask;

            var limite = DateTime.UtcNow - ValidadeTemporarios;

            foreach (var ficheiro in Directory.EnumerateFiles(PastaTemporaria, "*.xlsx"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Só remove o que já expirou: uma exportação em curso pode estar a ser
                // descarregada neste momento.
                if (File.GetLastWriteTimeUtc(ficheiro) < limite)
                    File.Delete(ficheiro);
            }
        }
        catch (IOException)
        {
            // Limpeza é higiene, não funcionalidade: um ficheiro preso não pode
            // impedir a exportação.
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reconstrói o caminho de uma exportação a partir do identificador. Devolve
    /// <c>null</c> para qualquer identificador que não seja um Guid em formato "N",
    /// o que torna impossível escapar da pasta temporária.
    /// </summary>
    public static string? ResolverCaminho(string? identificador)
        => Guid.TryParseExact(identificador, "N", out _)
            ? Path.Combine(PastaTemporaria, identificador + ".xlsx")
            : null;

    /// <summary>
    /// Corpo da exportação Excel. Os números vão por <c>InvariantCulture</c> porque o
    /// Excel interpreta a folha segundo a sua própria configuração regional.
    /// </summary>
    private byte[] GerarWorkbook(IReadOnlyList<BenchmarkEvaluationModel> dados)
    {
        var L = _L;

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Benchmarks");

        worksheet.Cell(1, 2).Value = "Modelo";
        worksheet.Cell(1, 3).Value = L["Evaluation.Judge1"].Value;
        worksheet.Cell(1, 4).Value = L["Evaluation.Judge2"].Value;
        worksheet.Cell(1, 5).Value = "Tokens/s";
        worksheet.Cell(1, 6).Value = "Tempo";
        worksheet.Cell(1, 7).Value = "Tokens";

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        headerRow.Style.Font.FontColor = XLColor.Black;

        var dadosAgrupados = dados
            .GroupBy(b => b.PromptId)
            .OrderBy(g => g.Key);

        int currentRow = 2;

        foreach (var grupo in dadosAgrupados)
        {
            var primeiroItem = grupo.First();

            worksheet.Cell(currentRow, 1).Value = string.Format(L["Benchmarks.ExcelIdGroup"], $"#{primeiroItem.PromptId}", primeiroItem.DataCriacaoFormatada);
            worksheet.Cell(currentRow, 2).Value = primeiroItem.TextoPrompt;

            worksheet.Range(currentRow, 2, currentRow, 7).Merge();

            var linhaPrompt = worksheet.Row(currentRow);
            linhaPrompt.Style.Font.Bold = true;
            linhaPrompt.Style.Fill.BackgroundColor = XLColor.FromHtml("#0078d4");
            linhaPrompt.Style.Font.FontColor = XLColor.White;
            linhaPrompt.Style.Alignment.WrapText = true;
            linhaPrompt.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            int caracteresPorLinha = 150;
            int tamanhoTexto = primeiroItem.TextoPrompt?.Length ?? 0;

            if (tamanhoTexto > 0)
            {
                int linhasNecessarias = (int)Math.Ceiling((double)tamanhoTexto / caracteresPorLinha);

                linhaPrompt.Height = (linhasNecessarias * 19) + 10;
            }
            else
            {
                linhaPrompt.Height = 25;
            }

            currentRow++;
            int linhaInicialModelos = currentRow;

            foreach (var item in grupo)
            {
                worksheet.Cell(currentRow, 1).Value = MarcadorModelo;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.Gray;

                worksheet.Cell(currentRow, 2).Value = item.NomeModelo;
                worksheet.Cell(currentRow, 3).Value = item.GeminiRating;
                worksheet.Cell(currentRow, 4).Value = item.OpenRouterRating;

                var cellTokensSeg = worksheet.Cell(currentRow, 5);
                cellTokensSeg.Value = item.TokensPorSegundo;
                cellTokensSeg.Style.NumberFormat.Format = "#,##0";

                worksheet.Cell(currentRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currentRow, 6).Value = item.TempoPuroFormatado;

                worksheet.Cell(currentRow, 7).Value = item.TamanhoTokens;

                currentRow++;
            }

            int linhaFinalModelos = currentRow - 1;

            if (linhaFinalModelos >= linhaInicialModelos)
            {
                worksheet.Rows(linhaInicialModelos, linhaFinalModelos).Group();
            }

            currentRow++;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.Column(2).Width = 80;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Prefixo visual das linhas de modelo dentro de cada grupo de prompt.</summary>
    private const string MarcadorModelo = "   ↳";
}
