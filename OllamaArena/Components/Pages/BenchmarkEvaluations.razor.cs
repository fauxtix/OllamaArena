using ClosedXML.Excel;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaArena.Models.DTO;
using System.Globalization;

namespace OllamaArena.Components.Pages
{
    public partial class BenchmarkEvaluations
    {
        private IQueryable<BenchmarkEvaluationModel> _benchmarks = Enumerable.Empty<BenchmarkEvaluationModel>().AsQueryable();
        private PaginationState _pagination = new() { ItemsPerPage = 10 };
        private string? _search;
        private BenchmarkEvaluationModel? _selectedBenchmarkResponse;
        private bool _isDialogVisible = false;
        private bool _isAnalyzing = false;
        private bool _mostrarPainelAnalise = false;
        private BenchmarkAnalysisResult? _resultadoLocal;
        private CancellationTokenSource? _cts;

        private string _streamingLog = "";

        private int _quantidadePorAvaliar =>
            _benchmarks.Count(b => !EstaAvaliada(b));

        private int _quantidadeAvaliados =>
            _benchmarks.Count(b => EstaAvaliada(b));

        private int _quantidadeFiltrados =>
            FilteredBenchmarks.Count();

        private static bool EstaAvaliada(BenchmarkEvaluationModel b) =>
            b.GeminiRating.HasValue && b.OpenRouterRating.HasValue;

        private bool _podeExportarPdf =>
            _benchmarks.Any() && _quantidadePorAvaliar == 0;


        protected override async Task OnInitializedAsync()
        {
            await GetAllBenchmarks();
        }

        private async Task GetAllBenchmarks()
        {
            var result = await BenchmarkRepo.BenchmarkResponseEvaluationAsync();
            _benchmarks = result.AsQueryable();
        }

        private void AlterarItensPorPagina(int novosItens)
        {
            _pagination.ItemsPerPage = novosItens;
            StateHasChanged();
        }

        private async Task DeleteByScopeAsync(DeleteScope scope)
        {
            if (DialogService == null) return;

            try
            {
                List<BenchmarkEvaluationModel> alvos;
                string mensagemChave;

                switch (scope)
                {
                    case DeleteScope.Filtrados:
                        alvos = FilteredBenchmarks.ToList();
                        mensagemChave = "Benchmarks.DeleteFilteredConfirmMessage";
                        break;
                    case DeleteScope.PorAvaliar:
                        alvos = _benchmarks.Where(b => !EstaAvaliada(b)).ToList();
                        mensagemChave = "Benchmarks.DeleteUnevaluatedConfirmMessage";
                        break;
                    case DeleteScope.Avaliados:
                        alvos = _benchmarks.Where(b => EstaAvaliada(b)).ToList();
                        mensagemChave = "Benchmarks.DeleteEvaluatedConfirmMessage";
                        break;
                    default:
                        alvos = _benchmarks.ToList();
                        mensagemChave = "Benchmarks.DeleteAllConfirmMessage";
                        break;
                }

                if (alvos.Count == 0) return;

                string mensagemHtml = $@"
                        <div style='padding: 4px; border-left: 4px solid #e73618;'>
                            <strong style='color: #e73618; font-size: 16px; display: block; margin-bottom: 8px;'>{L["Benchmarks.DeleteAllConfirmTitle"]}</strong>
                            <span style='color: var(--neutral-foreground-rest);'>{L[mensagemChave, alvos.Count]}</span>
                        </div>";

                var confirmacao = await DialogService.ShowConfirmationAsync(
                    message: mensagemHtml,
                    L["Benchmarks.DeleteAllYes"],
                    L["Common.Cancel"],
                    L["Benchmarks.DeleteAllTitle"]);

                if (confirmacao == null) return;
                var resultado = await confirmacao.Result;

                if (resultado != null && !resultado.Cancelled)
                {
                    if (scope == DeleteScope.Todos)
                    {
                        await BenchmarkRepo.DeleteAllPromptsAndHistoryAsync();
                    }
                    else
                    {
                        await BenchmarkRepo.DeleteResponsesAsync(alvos.Select(a => a.ResponseId));
                    }

                    var idsApagar = alvos.Select(a => a.ResponseId).ToHashSet();
                    await InvokeAsync(() =>
                    {
                        _benchmarks = _benchmarks.Where(b => !idsApagar.Contains(b.ResponseId)).AsQueryable();
                        StateHasChanged();
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Erro ao apagar os Prompts");
            }

        }

        private async Task DeleteBenchmark(int responseId)
        {

            if (DialogService == null) return;

            try
            {

                string mensagemHtml = $@"
                        <div style='padding: 4px; border-left: 4px solid #e73618;'>
                            <strong style='color: #e73618; font-size: 16px; display: block; margin-bottom: 8px;'>{L["Benchmarks.DeleteAllConfirmTitle"]}</strong>
                            <span style='color: var(--neutral-foreground-rest);'>{L["Benchmarks.DeleteOneConfirmMessage"]}</span>
                        </div>";
                var confirmacao = await DialogService.ShowConfirmationAsync(
                    message: mensagemHtml,
                    L["Benchmarks.DeleteAllYes"],
                    L["Common.Cancel"],
                    L["Benchmarks.DeleteOneTitle"]
                );

                await InvokeAsync(StateHasChanged);

                if (confirmacao == null) return;
                var resultado = await confirmacao.Result;

                if (resultado != null && !resultado.Cancelled)
                {
                    bool eliminado = await BenchmarkRepo.DeleteSpecificResponseAsync(responseId);
                    if (eliminado)
                    {
                        _benchmarks = _benchmarks.Where(b => b.ResponseId != responseId).AsQueryable();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Erro ao apagar o Prompt");
            }
        }


        private IQueryable<BenchmarkEvaluationModel> FilteredBenchmarks
        {
            get
            {
                IQueryable<BenchmarkEvaluationModel> query = _benchmarks;

                if (_filtroAtual == BenchmarkFilter.PorAvaliar)
                {
                    query = query.Where(b => !EstaAvaliada(b));
                }
                else if (_filtroAtual == BenchmarkFilter.Avaliados)
                {
                    query = query.Where(b => EstaAvaliada(b));
                }

                if (!string.IsNullOrWhiteSpace(_search))
                {
                    var termo = _search.Trim();
                    query = query.Where(b =>
                        (b.NomeModelo != null && b.NomeModelo.Contains(termo, StringComparison.OrdinalIgnoreCase)) ||
                        (b.Descricao != null && b.Descricao.Contains(termo, StringComparison.OrdinalIgnoreCase)) ||
                        (b.TextoPrompt != null && b.TextoPrompt.Contains(termo, StringComparison.OrdinalIgnoreCase)) ||
                        b.TokensPorSegundo.ToString(CultureInfo.InvariantCulture).Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                        b.TamanhoTokens.ToString(CultureInfo.InvariantCulture).Contains(termo, StringComparison.OrdinalIgnoreCase));
                }

                return query;
            }
        }

        private async Task ExportarParaExcel()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Benchmarks");

            worksheet.Cell(1, 2).Value = "Modelo";
            worksheet.Cell(1, 3).Value = "Gemini";
            worksheet.Cell(1, 4).Value = "OpenRouter";
            worksheet.Cell(1, 5).Value = "Tokens/s";
            worksheet.Cell(1, 6).Value = "Tempo";
            worksheet.Cell(1, 7).Value = "Tokens";

            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
            headerRow.Style.Font.FontColor = XLColor.Black;

            var dadosAgrupados = FilteredBenchmarks
                .ToList()
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
                    worksheet.Cell(currentRow, 1).Value = "   ↳";
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
            var content = stream.ToArray();

            var base64 = Convert.ToBase64String(content);
            var fileName = $"Benchmarks_Agrupados_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            await JS.InvokeVoidAsync("downloadFileFromBase64", fileName, base64);
        }

        private async Task ExportarParaPdfAsync()
        {
            try
            {
                var dados = FilteredBenchmarks.ToList();
                if (dados.Count == 0) return;

                var metricas = new (string Rotulo, Func<BenchmarkEvaluationModel, int?> Gemini, Func<BenchmarkEvaluationModel, int?> OpenRouter)[]
                {
                (L["Evaluation.MetricFactual"], b => b.GeminiFactualRating, b => b.OpenRouterFactualRating),
                (L["Evaluation.MetricFormatting"], b => b.GeminiFormattingRating, b => b.OpenRouterFormattingRating),
                (L["Evaluation.MetricCompliance"], b => b.GeminiComplianceRating, b => b.OpenRouterComplianceRating),
                (L["Evaluation.MetricRelevance"], b => b.GeminiRelevanceRating, b => b.OpenRouterRelevanceRating),
                (L["Evaluation.MetricTone"], b => b.GeminiToneRating, b => b.OpenRouterToneRating),
                (L["Evaluation.MetricConciseness"], b => b.GeminiConcisenessRating, b => b.OpenRouterConcisenessRating),
                (L["Evaluation.MetricClarity"], b => b.GeminiClarityRating, b => b.OpenRouterClarityRating),
                (L["Evaluation.MetricReadability"], b => b.GeminiReadabilityRating, b => b.OpenRouterReadabilityRating),
                (L["Evaluation.MetricHaloEffect"], b => b.GeminiHaloEffectRating, b => b.OpenRouterHaloEffectRating),
                (L["Evaluation.MetricSafety"], b => b.GeminiSafetyRating, b => b.OpenRouterSafetyRating),
                (L["Evaluation.MetricLanguageConsistency"], b => b.GeminiLanguageConsistencyRating, b => b.OpenRouterLanguageConsistencyRating),
                (L["Evaluation.MetricLoopDetection"], b => b.GeminiLoopDetectionRating, b => b.OpenRouterLoopDetectionRating)
                };

                string ValorGlobal(float? v) =>
                    v.HasValue ? Convert.ToInt32(v.Value).ToString(CultureInfo.InvariantCulture) : "—";

                string Valor(int? v) =>
                    v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : "—";

                var relatorio = new
                {
                    titulo = L["Benchmarks.ExportPdfTitle"].Value,
                    geradoEm = L["Benchmarks.ExportPdfGeneratedAt"].Value,
                    data = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    rotuloPrompt = L["Benchmarks.ExportPdfPrompt"].Value,
                    rotuloModelo = L["Benchmarks.ExportPdfModel"].Value,
                    rotuloMetrica = L["Benchmarks.ExportPdfMetric"].Value,
                    rotuloPerformance = L["Benchmarks.ExportPdfPerformance"].Value,
                    rotuloFeedback = L["Benchmarks.ExportPdfFeedback"].Value,
                    rotuloRecomendacao = L["Benchmarks.ExportPdfRecommendation"].Value,
                    rotuloGemini = L["Benchmarks.ColGemini"].Value,
                    rotuloOpenRouter = L["Benchmarks.ColOpenRouter"].Value,
                    rotuloData = L["Benchmarks.ExportPdfDate"].Value,
                    rotulosDesempenho = new[]
                    {
                    L["Benchmarks.ColTokensPerSecond"].Value,
                    L["Benchmarks.ColTime"].Value,
                    L["Benchmarks.ColLoad"].Value,
                    L["Benchmarks.ColTokens"].Value
                },
                    grupos = dados
                        .GroupBy(b => b.PromptId)
                        .OrderBy(g => g.Key)
                        .Select(g => new
                        {
                            id = g.Key,
                            prompt = g.First().TextoPrompt,
                            data = g.First().DataCriacaoFormatada,
                            respostas = g.Select(b =>
                            {
                                var linhas = new List<object>
                                {
                                new { metrica = L["Evaluation.MetricOverall"].Value, gemini = ValorGlobal(b.GeminiRating), openrouter = ValorGlobal(b.OpenRouterRating) }
                                };
                                linhas.AddRange(metricas.Select(m => new
                                {
                                    metrica = m.Rotulo,
                                    gemini = Valor(m.Gemini(b)),
                                    openrouter = Valor(m.OpenRouter(b))
                                }));
                                return new
                                {
                                    modelo = b.NomeModelo,
                                    desempenho = new[]
                                    {
                                    b.TokensPorSegundo.ToString("F2", CultureInfo.InvariantCulture),
                                    b.TempoPuroFormatado,
                                    b.TempoCargaFormatado,
                                    b.TamanhoTokens.ToString(CultureInfo.InvariantCulture)
                                    },
                                    linhas,
                                    feedbackGemini = b.GeminiFeedback,
                                    feedbackOpenRouter = b.OpenRouterFeedback,
                                    recomendacaoGemini = b.GeminiRecommendation,
                                    recomendacaoOpenRouter = b.OpenRouterRecommendation
                                };
                            }).ToList()
                        }).ToList()
                };

                await JS.InvokeVoidAsync("gerarRelatorioPDF", relatorio);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao gerar o relatório PDF.");
                await DialogService.ShowErrorAsync($"{L["Benchmarks.ExportPdfError"]} {ex.Message}");
            }
        }

        private enum BenchmarkFilter { Todos, PorAvaliar, Avaliados }
        private BenchmarkFilter _filtroAtual = BenchmarkFilter.Todos;

        private const string _deleteMenuAnchorId = "delete-benchmarks-menu-anchor";

        private enum DeleteScope { Filtrados, PorAvaliar, Avaliados, Todos }

        private Appearance GetAppearance(BenchmarkFilter filtro)
        {
            return _filtroAtual == filtro ? Appearance.Accent : Appearance.Stealth;
        }

        private void OpenDetailDialog(BenchmarkEvaluationModel? response)
        {
            if (response == null) return;
            _selectedBenchmarkResponse = response;
            _isDialogVisible = true;
        }

        private async Task AnalisarResultadosComModeloLocal()
        {
            var dadosFiltrados = FilteredBenchmarks.ToList();

            // ===================================
            // Verifica quantidade de grupos de prompts
            // só deverá ser analisado um grupo de cada vez
            // ===================================
            var gruposDePrompts = dadosFiltrados
                .GroupBy(b => b.PromptId)
                .ToList();

            if (gruposDePrompts.Count > 1)
            {
                string mensagemHtml = $@"
            <div style='padding: 8px; border-left: 5px solid #e73618;'>
                <strong style='color: #e73618; font-size: 16px;'>{L["Benchmarks.MultipleGroupsWarning"]}</strong><br><br>
                <span style='color: var(--neutral-foreground-rest);'>
                    {L["Benchmarks.MultipleGroupsMessage", gruposDePrompts.Count]}
                </span>
            </div>";

                var confirmacao = await DialogService.ShowConfirmationAsync(
                    message: mensagemHtml,
                    L["Common.Confirm"],
                    L["Common.Cancel"],
                    L["Benchmarks.MultipleGroupsTitle"]);

                if (confirmacao == null) return;

                var resultado = await confirmacao.Result;
                if (resultado?.Cancelled == true)
                {
                    return;
                }
            }

            // === Verificação existente: benchmarks não avaliados ===
            bool todosAvaliados = dadosFiltrados.All(b =>
                b.GeminiRating > 0 && b.OpenRouterRating > 0);

            if (!todosAvaliados)
            {
                var naoAvaliados = dadosFiltrados.Count(b => b.GeminiRating == 0 || b.OpenRouterRating == 0);
                string mensagemHtml = $@"
        <div style='padding: 8px; border-left: 5px solid #e73618;'>
            <strong style='color: #e73618; font-size: 16px;'>{L["Benchmarks.AttentionWarning"]}</strong><br><br>
            <span style='color: var(--neutral-foreground-rest);'>
                {L["Benchmarks.NotEvaluatedMessage", naoAvaliados]}
            </span>
        </div>";

                var confirmacao = await DialogService.ShowConfirmationAsync(
                    message: mensagemHtml,
                    L["Common.Confirm"],
                    L["Common.Cancel"],
                    L["Benchmarks.NotEvaluatedTitle"]);

                if (confirmacao == null) return;
                var resultado = await confirmacao.Result;
                if (resultado?.Cancelled == true)
                {
                    return;
                }
            }

            // === Prossegue com a análise ===
            _mostrarPainelAnalise = true;
            _isAnalyzing = true;
            _resultadoLocal = null;
            StateHasChanged();

            try
            {
                if (dadosFiltrados.Count == 0)
                {
                    _resultadoLocal = new BenchmarkAnalysisResult
                    {
                        Sumario = L["Benchmarks.NoDataForAnalysis"]
                    };
                    return;
                }

                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _resultadoLocal = await AnalysisService.AnalisarBenchmarksAsync(
                    dadosFiltrados,
                    _cts.Token
                );
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Análise cancelada pelo utilizador.");
                _resultadoLocal = new BenchmarkAnalysisResult
                {
                    Sumario = L["Benchmarks.AnalysisCancelled"],
                    LinhasAnaliseDetalhada = new List<string>
                {
                    L["Benchmarks.AnalysisCancelledDetail"]
                }
                };
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao tentar efetuar a análise dos benchmarks.");
                _resultadoLocal = new BenchmarkAnalysisResult
                {
                    Sumario = L["Benchmarks.AnalysisError"],
                    LinhasAnaliseDetalhada = new List<string>
                {
                    L["Benchmarks.AnalysisErrorDetail"]
                }
                };
            }
            finally
            {
                _isAnalyzing = false;
                StateHasChanged();
            }
        }
        private void OnPartial(string chunk)
        {
            _streamingLog += chunk;
            StateHasChanged(); // 🔥 Atualiza o UI
        }
        private void CancelarAnaliseDoPai()
        {
            _cts?.Cancel();
        }

        private void OnDoubleClick(FluentDataGridRow<BenchmarkEvaluationModel> row)
        {
            if (row?.Item == null) return;
            OpenDetailDialog(row.Item);
        }

    }
}