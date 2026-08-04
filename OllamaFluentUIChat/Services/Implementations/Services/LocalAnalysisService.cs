using Microsoft.Extensions.Localization;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Resources;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Diagnostics;
using System.Text;

namespace OllamaFluentUIChat.Services.Implementations.Services;

public class LocalAnalysisService : IAnalysisService
{
    private readonly ILogger<LocalAnalysisService> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public LocalAnalysisService(HttpClient httpClient, ILogger<LocalAnalysisService> logger, IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _localizer = localizer;
    }

    public Task<BenchmarkAnalysisResult> AnalisarBenchmarksAsync(
        List<BenchmarkEvaluationModel> benchmarks,
        CancellationToken cancellationToken = default)
    {
        if (benchmarks == null || benchmarks.Count == 0)
        {
            _logger.LogWarning("Nenhum benchmark para analisar.");
            return Task.FromResult(new BenchmarkAnalysisResult
            {
                Sumario = "Não existem dados para analisar."
            });
        }

        var stopwatch = Stopwatch.StartNew();

        // 1. Métricas de Velocidade
        var modeloMaisRapido = benchmarks
            .OrderByDescending(b => b.TokensPorSegundo)
            .First();

        var modeloMaisLento = benchmarks
            .OrderBy(b => b.TokensPorSegundo)
            .First();

        // 2. Média Global de Qualidade e Consenso
        var modelosComAvaliacao = benchmarks.Select(b =>
        {
            double geminiFactual = Convert.ToDouble(b.GeminiFactualRating);
            double chatGptFactual = Convert.ToDouble(b.ChatGptFactualRating);
            double geminiRating = Convert.ToDouble(b.GeminiRating);
            double chatGptRating = Convert.ToDouble(b.ChatGptRating);

            return new
            {
                Benchmark = b,
                AvgRating = (geminiRating + geminiFactual + chatGptRating + chatGptFactual) / 4.0,
                DiferencaFactual = Math.Abs(geminiFactual - chatGptFactual)
            };
        }).OrderByDescending(x => x.AvgRating).ToList();

        var modeloMelhorAvaliado = modelosComAvaliacao.First();

        // 3. Geração do Sumário Executivo
        var sumarioBuilder = new StringBuilder();
        sumarioBuilder.Append($"O modelo {modeloMaisRapido.NomeModelo} destacou-se com a maior velocidade de processamento, atingindo {modeloMaisRapido.TokensPorSegundo:F1} Tokens/s (tempo total de {FormatarTempo(modeloMaisRapido.TempoPuroMs)}). ");

        if (modeloMelhorAvaliado.Benchmark.NomeModelo == modeloMaisRapido.NomeModelo)
        {
            sumarioBuilder.Append($"Além de ser o mais rápido, o {modeloMelhorAvaliado.Benchmark.NomeModelo} obteve também a melhor classificação média geral ({modeloMelhorAvaliado.AvgRating:F2}/5). ");
        }
        else
        {
            sumarioBuilder.Append($"Em termos de qualidade global, o {modeloMelhorAvaliado.Benchmark.NomeModelo} obteve a melhor avaliação média com {modeloMelhorAvaliado.AvgRating:F2}/5. ");
        }

        sumarioBuilder.Append($"O modelo {modeloMaisLento.NomeModelo} registou o menor rendimento em velocidade ({modeloMaisLento.TokensPorSegundo:F1} Tokens/s em {FormatarTempo(modeloMaisLento.TempoPuroMs)}).");

        // 4. Análise de Empates e Consenso
        var maxGeminiFactual = benchmarks.Max(b => b.GeminiFactualRating);
        var melhoresGemini = benchmarks.Where(b => b.GeminiFactualRating == maxGeminiFactual).Select(b => b.NomeModelo).ToList();
        string textoGemini = melhoresGemini.Count > 1
            ? $"Os modelos {string.Join(" e ", melhoresGemini)} empataram na precisão factual com {maxGeminiFactual}/5."
            : $"O modelo {melhoresGemini.First()} obteve o destaque em precisão factual com uma nota de {maxGeminiFactual}/5.";

        var maxChatGptFactual = benchmarks.Max(b => b.ChatGptFactualRating);
        var melhoresChatGpt = benchmarks.Where(b => b.ChatGptFactualRating == maxChatGptFactual).Select(b => b.NomeModelo).ToList();
        string textoChatGpt = melhoresChatGpt.Count > 1
            ? $"Os modelos {string.Join(" e ", melhoresChatGpt)} partilham a melhor pontuação factual com {maxChatGptFactual}/5."
            : $"A melhor pontuação factual foi atribuída ao modelo {melhoresChatGpt.First()} com {maxChatGptFactual}/5.";

        var modeloConsenso = modelosComAvaliacao.FirstOrDefault(m => m.Benchmark.NomeModelo == modeloMelhorAvaliado.Benchmark.NomeModelo);
        string textoConsenso = (modeloConsenso != null && modeloConsenso.DiferencaFactual == 0)
            ? $"Alta concordância entre Gemini e ChatGPT ({modeloConsenso.Benchmark.GeminiFactualRating}/5 em ambos)."
            : $"Divergência ligeira entre avaliadores ({modeloConsenso?.Benchmark.GeminiFactualRating}/5 Gemini vs {modeloConsenso?.Benchmark.ChatGptFactualRating}/5 ChatGPT).";

        // 5. Linhas da Análise Detalhada
        var linhasDetalhes = new List<string>();

        // a) Velocidade
        var textoVelocidade = $"• <strong>Velocidade e Tempo:</strong> O modelo {modeloMaisRapido.NomeModelo} lidera a taxa de geração de tokens ({modeloMaisRapido.TokensPorSegundo:F1} t/s), completando a resposta em {FormatarTempo(modeloMaisRapido.TempoPuroMs)}.";
        if (benchmarks.Count > 1)
        {
            var diferencaVelocidade = modeloMaisRapido.TokensPorSegundo - modeloMaisLento.TokensPorSegundo;
            textoVelocidade += $" Em comparação, o modelo {modeloMaisLento.NomeModelo} é {diferencaVelocidade:F1} t/s mais lento, necessitando de {FormatarTempo(modeloMaisLento.TempoPuroMs)} de execução.";
        }
        linhasDetalhes.Add(textoVelocidade);

        // b) Avaliações e Consenso
        linhasDetalhes.Add($"• <strong>Avaliação Gemini:</strong> {textoGemini}");
        linhasDetalhes.Add($"• <strong>Avaliação ChatGPT:</strong> {textoChatGpt}");
        linhasDetalhes.Add($"• <strong>Consenso Factual:</strong> {textoConsenso}");

        // c) Recomendação Dinâmica com Salvaguarda Global de Baixo Desempenho
        bool todosComQualidadeBaixa = modelosComAvaliacao.All(m => m.AvgRating < 4.0);

        if (todosComQualidadeBaixa)
        {
            // Ativa quando nenhum modelo atinge pelo menos 4.0/5 de média
            linhasDetalhes.Add("• ⚠️ <strong>Alerta de Desempenho:</strong> Nenhum dos modelos testados atingiu a classificação mínima de excelência (>= 4,0/5). A tarefa solicitada aparenta exceder a capacidade atual do conjunto de modelos locais em teste.");
        }
        else
        {
            double ganhoVelocidade = modeloMaisLento.TokensPorSegundo > 0
                ? modeloMaisRapido.TokensPorSegundo / modeloMaisLento.TokensPorSegundo
                : 1.0;

            string recomendacaoPrompt;
            if (modeloMaisRapido.NomeModelo != modeloMelhorAvaliado.Benchmark.NomeModelo)
            {
                recomendacaoPrompt = $"• <strong>Recomendação por Prompt:</strong> Para <em>prompts simples/chat interativo</em>, prefira o <strong>{modeloMaisRapido.NomeModelo}</strong> ({ganhoVelocidade:F1}x mais rápido). Para <em>prompts complexos, raciocínio ou geração de código</em> onde a exatidão é crítica, utilize o <strong>{modeloMelhorAvaliado.Benchmark.NomeModelo}</strong>.";
            }
            else
            {
                string textoVelocidadeGanho = benchmarks.Count > 1 ? $" ({ganhoVelocidade:F1}x mais rápido)" : "";
                recomendacaoPrompt = $"• <strong>Recomendação por Prompt:</strong> O modelo <strong>{modeloMaisRapido.NomeModelo}</strong> é a escolha ideal para qualquer tipo de prompt{textoVelocidadeGanho}, por liderar simultaneamente em velocidade e qualidade global.";
            }

            linhasDetalhes.Add(recomendacaoPrompt);
        }

        stopwatch.Stop();

        return Task.FromResult(new BenchmarkAnalysisResult
        {
            Sumario = sumarioBuilder.ToString(),
            LinhasAnaliseDetalhada = linhasDetalhes,
            ModeloMaisRapido = modeloMaisRapido.NomeModelo,
            MaxTokensSec = Math.Round(modeloMaisRapido.TokensPorSegundo, 2),
            ModeloMelhorAvaliado = $"{modeloMelhorAvaliado.Benchmark.NomeModelo} ({modeloMelhorAvaliado.AvgRating:F2}/5)",
            TempoAnaliseFormatado = stopwatch.Elapsed.ToString(@"mm\:ss")
        });
    }

    private static string FormatarTempo(double tempoMs)
    {
        var ts = TimeSpan.FromMilliseconds(tempoMs);
        return ts.TotalSeconds >= 60
            ? $"{ts.Minutes}m {ts.Seconds}s"
            : $"{ts.TotalSeconds:F1}s";
    }
}