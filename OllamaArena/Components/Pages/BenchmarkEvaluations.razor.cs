using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using OllamaArena.Models.DTO;
using OllamaArena.Services.Helpers;
using System.Globalization;

namespace OllamaArena.Components.Pages
{
    /// <summary>
    /// Grelha de avaliações dos benchmarks: filtro, pesquisa, eliminação por âmbito,
    /// análise local e exportações (Excel/PDF).
    /// </summary>
    public partial class BenchmarkEvaluations : IDisposable
    {
        private IReadOnlyList<BenchmarkEvaluationModel> _benchmarks = Array.Empty<BenchmarkEvaluationModel>();

        /// <summary>
        /// Resultado de <see cref="AplicarFiltros"/>. É recalculado só quando muda o
        /// filtro, a pesquisa ou o conjunto de dados: antes, cada uma das contagens
        /// na barra de ferramentas reexecutava a cadeia de <c>Where</c> sobre todas as
        /// linhas em cada render (o getter devolvia um objeto de query novo a cada
        /// acesso, e a grelha enumerava-o várias vezes).
        /// </summary>
        private IReadOnlyList<BenchmarkEvaluationModel> _filtrados = Array.Empty<BenchmarkEvaluationModel>();

        /// <summary>
        /// Mesmos elementos de <see cref="_filtrados"/> na forma que a grelha consome.
        /// A instância é guardada para que a grelha não veja um objeto novo em cada
        /// render (o que a fazia reconstruir a página atual para nada).
        /// </summary>
        private IQueryable<BenchmarkEvaluationModel> _filtroConsulta =
            Enumerable.Empty<BenchmarkEvaluationModel>().AsQueryable();

        private PaginationState _pagination = new() { ItemsPerPage = 10 };
        private BenchmarkEvaluationModel? _selectedBenchmarkResponse;
        private bool _isDialogVisible = false;
        private bool _isAnalyzing = false;
        private bool _isExporting = false;
        private bool _mostrarPainelAnalise = false;
        private BenchmarkAnalysisResult? _resultadoLocal;

        private CancellationTokenSource? _cts;

        private BenchmarkFilter _filtroAtual = BenchmarkFilter.Todos;
        private string? _pesquisa;

        // Contagens recalculadas em conjunto com _filtrados.
        private int _quantidadeFiltrados;
        private int _quantidadePorAvaliar;
        private int _quantidadeAvaliadas;

        private bool _descartado;

        /// <summary>
        /// O relatório só sai quando não há respostas por avaliar, para não haver
        /// linhas com "—" nos lugares das notas. No Excel o estado parcial é
        /// aceitável (e útil), por isso a regra só se aplica ao PDF.
        /// </summary>
        private bool _podeExportarPdf => _benchmarks.Count > 0 && _quantidadePorAvaliar == 0;

        protected override async Task OnInitializedAsync()
        {
            await GetAllBenchmarks();
        }

        private async Task GetAllBenchmarks()
        {
            var result = await QueryRepo.BenchmarkResponseEvaluationAsync();
            _benchmarks = result as IReadOnlyList<BenchmarkEvaluationModel> ?? result.ToList();
            await RecalcularAsync(reporPrimeiraPagina: true);
        }

        /// <summary>
        /// Recalcula a lista filtrada e as contagens e repõe a página atual quando
        /// deixa de fazer sentido (nova pesquisa/filtro, ou uma eliminação que
        /// esvaziou a última página).
        /// </summary>
        private async Task RecalcularAsync(bool reporPrimeiraPagina = false)
        {
            var filtrados = AplicarFiltros(_benchmarks, _filtroAtual, _pesquisa);

            _filtrados = filtrados;
            _filtroConsulta = filtrados.AsQueryable();
            _quantidadeFiltrados = filtrados.Count;
            _quantidadePorAvaliar = BenchmarkEvaluationRules.ContarPorAvaliar(_benchmarks);
            _quantidadeAvaliadas = BenchmarkEvaluationRules.ContarAvaliadas(_benchmarks);

            await AjustarPaginaAsync(reporPrimeiraPagina);
        }

        /// <summary>
        /// Mantém <see cref="PaginationState.CurrentPageIndex"/> dentro do intervalo
        /// válido. Sem isto, apagar a última linha da última página deixava a grelha
        /// vazia com o paginador a apontar para uma página que já não existe.
        /// </summary>
        private async Task AjustarPaginaAsync(bool reporPrimeiraPagina)
        {
            var ultimaPagina = _quantidadeFiltrados == 0
                ? 0
                : (int)Math.Ceiling(_quantidadeFiltrados / (double)Math.Max(1, _pagination.ItemsPerPage)) - 1;

            var alvo = reporPrimeiraPagina ? 0 : Math.Min(_pagination.CurrentPageIndex, ultimaPagina);

            // CurrentPageIndex é só de leitura: a alteração tem de passar por aqui
            // para a grelha ser notificada.
            if (_pagination.CurrentPageIndex != alvo)
                await _pagination.SetCurrentPageIndexAsync(alvo);
        }

        private IReadOnlyList<BenchmarkEvaluationModel> AplicarFiltros(
            IReadOnlyList<BenchmarkEvaluationModel> origem,
            BenchmarkFilter filtro,
            string? pesquisa)
        {
            IEnumerable<BenchmarkEvaluationModel> consulta = filtro switch
            {
                BenchmarkFilter.PorAvaliar => origem.Where(b => !BenchmarkEvaluationRules.EstaAvaliada(b)),
                BenchmarkFilter.Avaliados => origem.Where(BenchmarkEvaluationRules.EstaAvaliada),
                _ => origem
            };

            if (!string.IsNullOrWhiteSpace(pesquisa))
            {
                var termo = pesquisa.Trim();
                consulta = consulta.Where(b => Corresponde(b, termo));
            }

            return consulta.ToList();
        }

        /// <summary>
        /// Pesquisa por texto livre nos campos visíveis da grelha. Os números são
        /// comparados em ambas as notações — "12,5" (pt) e "12.5" (en) — para que
        /// o utilizador encontre o valor que vê no ecrã.
        /// </summary>
        private static bool Corresponde(BenchmarkEvaluationModel b, string termo)
        {
            bool Contem(string? texto) =>
                texto is not null && texto.Contains(termo, StringComparison.OrdinalIgnoreCase);

            if (Contem(b.NomeModelo) || Contem(b.Descricao) || Contem(b.TextoPrompt))
                return true;

            return FormatosNumericos(b.TokensPorSegundo).Any(Contem)
                   || FormatosNumericos(b.TamanhoTokens).Any(Contem);
        }

        private static IEnumerable<string> FormatosNumericos(double valor)
        {
            yield return valor.ToString(CultureInfo.InvariantCulture);
            yield return valor.ToString(CultureInfo.CurrentCulture);
        }

        /// <summary>Valor atual da pesquisa, para a <c>FluentSearch</c>.</summary>
        private string? Pesquisa => _pesquisa;

        private async Task PesquisaAlterada(string? valor)
        {
            if (_pesquisa == valor)
                return;

            _pesquisa = valor;
            await RecalcularAsync(reporPrimeiraPagina: true);
        }

        private async Task SelecionarFiltroAsync(BenchmarkFilter filtro)
        {
            if (_filtroAtual == filtro)
                return;

            _filtroAtual = filtro;
            await RecalcularAsync(reporPrimeiraPagina: true);
        }

        private async Task DeleteByScopeAsync(DeleteScope scope)
        {
            try
            {
                List<BenchmarkEvaluationModel> alvos;
                string mensagemChave;
                string tituloChave;
                string confirmarChave;

                switch (scope)
                {
                    case DeleteScope.Filtrados:
                        alvos = _filtrados.ToList();
                        mensagemChave = "Benchmarks.DeleteFilteredConfirmMessage";
                        tituloChave = "Benchmarks.DeleteFilteredConfirmTitle";
                        confirmarChave = "Benchmarks.DeleteAllYes";
                        break;
                    case DeleteScope.PorAvaliar:
                        alvos = _benchmarks.Where(b => !BenchmarkEvaluationRules.EstaAvaliada(b)).ToList();
                        mensagemChave = "Benchmarks.DeleteUnevaluatedConfirmMessage";
                        tituloChave = "Benchmarks.DeleteUnevaluatedConfirmTitle";
                        confirmarChave = "Benchmarks.DeleteAllYes";
                        break;
                    case DeleteScope.Avaliados:
                        alvos = _benchmarks.Where(BenchmarkEvaluationRules.EstaAvaliada).ToList();
                        mensagemChave = "Benchmarks.DeleteEvaluatedConfirmMessage";
                        tituloChave = "Benchmarks.DeleteEvaluatedConfirmTitle";
                        confirmarChave = "Benchmarks.DeleteAllYes";
                        break;
                    default:
                        alvos = _benchmarks.ToList();
                        mensagemChave = "Benchmarks.DeleteEverythingConfirmMessage";
                        tituloChave = "Benchmarks.DeleteEverythingConfirmTitle";
                        confirmarChave = "Benchmarks.DeleteEverythingYes";
                        break;
                }

                if (alvos.Count == 0)
                    return;

                string mensagemHtml = $@"
                        <div style='padding: 4px; border-left: 4px solid #e73618;'>
                            <strong style='color: #e73618; font-size: 16px; display: block; margin-bottom: 8px;'>{L[tituloChave]}</strong>
                            <span style='color: var(--neutral-foreground-rest);'>{L[mensagemChave, alvos.Count]}</span>
                        </div>";

                var confirmacao = await DialogService.ShowConfirmationAsync(
                    message: mensagemHtml,
                    L[confirmarChave],
                    L["Common.Cancel"],
                    L["Benchmarks.DeleteAllTitle"]);

                if (confirmacao == null) return;
                var resultado = await confirmacao.Result;

                if (resultado != null && !resultado.Cancelled)
                {
                    if (scope == DeleteScope.Todos)
                    {
                        // Apaga a tabela Prompts inteira. Recarrega-se do BD em vez de
                        // filtrar o cache: a eliminação não se limita ao que está em
                        // memória (outro separador pode ter avaliado benchmarks depois
                        // desta página ter carregado) e a cache ficaria desfasada.
                        await CommandRepo.DeleteAllPromptsAsync();
                        await GetAllBenchmarks();
                    }
                    else
                    {
                        await CommandRepo.DeleteResponsesAsync(alvos.Select(a => a.ResponseId));

                        var idsApagar = alvos.Select(a => a.ResponseId).ToHashSet();
                        _benchmarks = _benchmarks.Where(b => !idsApagar.Contains(b.ResponseId)).ToList();
                        await RecalcularAsync();
                    }

                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao apagar os benchmarks (âmbito {Scope}).", scope);
                // Sem isto o erro só ficava no log e o utilizador via o clique
                // "apagar" aparentemente sem efeito.
                await DialogService.ShowErrorAsync(L["Benchmarks.DeleteError"], L["Benchmarks.DeleteAllTitle"]);
            }
        }

        private async Task DeleteBenchmark(int responseId)
        {
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

                if (confirmacao == null) return;
                var resultado = await confirmacao.Result;

                if (resultado != null && !resultado.Cancelled)
                {
                    bool eliminado = await CommandRepo.DeleteSpecificResponseAsync(responseId);
                    if (eliminado)
                    {
                        _benchmarks = _benchmarks.Where(b => b.ResponseId != responseId).ToList();
                        await RecalcularAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao apagar a resposta {ResponseId} do benchmark.", responseId);
                await DialogService.ShowErrorAsync(L["Benchmarks.DeleteError"], L["Benchmarks.DeleteOneTitle"]);
            }
        }

        private async Task ExportarParaExcel()
        {
            if (_isExporting)
                return;

            var dados = _filtrados;
            if (dados.Count == 0)
                return;

            _isExporting = true;
            StateHasChanged();

            try
            {
                var nomeFicheiro = $"Benchmarks_Agrupados_{DateTime.Now:yyyyMMdd_HHmmss}";
                var download = await ExcelExporter.GerarAsync(dados, nomeFicheiro);

                // O .xlsx é descarregado por HTTP e não pelo circuito: em base64 o
                // payload contava com o limite de 32 MB do circuito SignalR.
                await JS.InvokeVoidAsync(
                    "downloadBenchmarksExcel",
                    $"/api/export/benchmarks/{download.Identificador}",
                    download.NomeFicheiro);
            }
            catch (JSDisconnectedException)
            {
                // Circuito caiu: não há a quem reportar.
                _descartado = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao exportar os benchmarks para Excel.");
                await DialogService.ShowErrorAsync($"{L["Benchmarks.ExportExcelError"]} {ex.Message}");
            }
            finally
            {
                _isExporting = false;

                if (!_descartado)
                    await InvokeAsync(StateHasChanged);
            }
        }


        private async Task ExportarParaPdfAsync()
        {
            if (_isExporting)
                return;

            try
            {
                var dados = _filtrados;
                if (dados.Count == 0) return;

                // As 12 métricas vêm do catálogo partilhado: a lista não pode divergir
                // do que o ranking e a grelha de detalhe consideram.
                var metricas = JudgeMetricCatalog.Todas
                    .Select(m => (Rotulo: m.Rotulo(L), m.Gemini, m.OpenRouter))
                    .ToArray();

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
                    rotuloFeedback = L["Benchmarks.ExportPdfFeedback"].Value,
                    rotuloRecomendacao = L["Benchmarks.ExportPdfRecommendation"].Value,
                    rotuloGemini = L["Evaluation.Judge1"].Value,
                    rotuloOpenRouter = L["Evaluation.Judge2"].Value,
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
                                    recomendacaoOpenRouter = b.OpenRouterRecommendation,
                                    // O rótulo indica de onde veio cada juiz (API ou
                                    // preenchimento manual) e é por resposta: o mesmo slot
                                    // pode ter origens diferentes entre respostas.
                                    rotuloJuiz1 = JudgeLabelResolver.RotuloComOrigem(L, b.Juiz1Origem, primeiroSlot: true),
                                    rotuloJuiz2 = JudgeLabelResolver.RotuloComOrigem(L, b.Juiz2Origem, primeiroSlot: false)
                                };
                            }).ToList()
                        }).ToList()
                };

                _isExporting = true;
                StateHasChanged();

                await JS.InvokeVoidAsync("gerarRelatorioPDF", relatorio);
            }
            catch (JSDisconnectedException)
            {
                _descartado = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao gerar o relatório PDF.");
                await DialogService.ShowErrorAsync($"{L["Benchmarks.ExportPdfError"]} {ex.Message}");
            }
            finally
            {
                _isExporting = false;

                if (!_descartado)
                    await InvokeAsync(StateHasChanged);
            }
        }

        private enum BenchmarkFilter { Todos, PorAvaliar, Avaliados }

        private const string DeleteMenuAnchorId = "delete-benchmarks-menu-anchor";

        private enum DeleteScope { Filtrados, PorAvaliar, Avaliados, Todos }

        private static Appearance GetAppearance(BenchmarkFilter atual, BenchmarkFilter filtro)
        {
            return atual == filtro ? Appearance.Accent : Appearance.Stealth;
        }

        private Appearance GetAppearance(BenchmarkFilter filtro)
            => GetAppearance(_filtroAtual, filtro);

        private void OpenDetailDialog(BenchmarkEvaluationModel? response)
        {
            if (response == null) return;
            _selectedBenchmarkResponse = response;
            _isDialogVisible = true;
        }

        private async Task AnalisarResultadosComModeloLocal()
        {
            var dadosFiltrados = _filtrados;

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
            // A mesma regra da grelha (BenchmarkEvaluationRules): antes contava
            // "== 0", que é falso para ratings nulos, e o diálogo anunciava
            // "0 benchmark(s) ainda não avaliados" quando havia dezenas.
            var naoAvaliados = BenchmarkEvaluationRules.ContarPorAvaliar(dadosFiltrados);

            if (naoAvaliados > 0)
            {
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
                        Sumario = L["Benchmarks.NoDataForAnalysis"].Value
                    };
                    return;
                }

                // Token novo por execução: reutilizar o CTS da análise anterior
                // devolveria um token já cancelado (o utilizador carregou em
                // "Cancelar") e a segunda análise abortava de imediato.
                DescartarToken();

                _cts = new CancellationTokenSource();

                _resultadoLocal = await AnalysisService.AnalisarBenchmarksAsync(
                    dadosFiltrados,
                    _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Análise cancelada pelo utilizador.");
                _resultadoLocal = new BenchmarkAnalysisResult
                {
                    Sumario = L["Benchmarks.AnalysisCancelled"].Value,
                    LinhasAnaliseDetalhada = new List<BenchmarkAnalysisLine>
                    {
                        BenchmarkAnalysisLine.Criar(L["Benchmarks.AnalysisCancelledDetail"].Value)
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao tentar efetuar a análise dos benchmarks.");
                _resultadoLocal = new BenchmarkAnalysisResult
                {
                    Sumario = L["Benchmarks.AnalysisError"].Value,
                    LinhasAnaliseDetalhada = new List<BenchmarkAnalysisLine>
                    {
                        BenchmarkAnalysisLine.Criar(L["Benchmarks.AnalysisErrorDetail"].Value)
                    }
                };
            }
            finally
            {
                _isAnalyzing = false;
                StateHasChanged();
            }
        }

        private void CancelarAnaliseDoPai()
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // A análise já terminou e o CTS foi libertado — nada a cancelar.
            }
        }

        /// <summary>Liberta o CTS da execução anterior, se já não estiver em uso.</summary>
        private void DescartarToken()
        {
            var anterior = _cts;
            _cts = null;

            if (anterior is null)
                return;

            try
            {
                anterior.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Já estava libertado.
            }

            anterior.Dispose();
        }

        private void OnDoubleClick(FluentDataGridRow<BenchmarkEvaluationModel> row)
        {
            if (row?.Item == null) return;
            OpenDetailDialog(row.Item);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_descartado)
                return;

            if (disposing)
            {
                _descartado = true;
                DescartarToken();
            }
        }
    }
}
