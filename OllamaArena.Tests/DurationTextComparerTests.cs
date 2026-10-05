using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

/// <summary>
/// Testes da ordenação das colunas de duração/tempo da grelha de avaliações. As
/// colunas mostram "m:ss", e a ordenação por omissão de <c>string</c> punha
/// "10:00" antes de "1:05".
/// </summary>
public class DurationTextComparerTests
{
    [Fact]
    public void OrdenaPorValorNumerico_NaoPorTexto()
    {
        var duracoes = new List<string> { "10:00", "1:05", "0:59", "2:30" };

        duracoes.Sort(DurationTextComparer.Instance);

        Assert.Equal(["0:59", "1:05", "2:30", "10:00"], duracoes);
    }

    [Fact]
    public void ZeroMenosDeUmMinutoFicaAntesDeMinutos()
    {
        var duracoes = new List<string> { "1:00", "0:01" };

        duracoes.Sort(DurationTextComparer.Instance);

        Assert.Equal(["0:01", "1:00"], duracoes);
    }

    [Theory]
    [InlineData(null, "0:10", -1)]
    [InlineData("0:10", null, 1)]
    [InlineData("", "0:10", -1)]
    [InlineData("0:10", "0:10", 0)]
    public void ComparaValoresAusentesEFuncionarios(string? x, string? y, int esperado)
    {
        Assert.Equal(esperado, Math.Sign(DurationTextComparer.Instance.Compare(x, y)));
    }

    [Fact]
    public void TextoNaoInterpretavelOrdenaComoZero()
    {
        Assert.True(DurationTextComparer.Instance.Compare("abc", "0:01") < 0);
    }

    [Fact]
    public void SemSegundos_OrdenaComoNumeroDeMilissegundos()
    {
        var duracoes = new List<string> { "5000", "100" };

        duracoes.Sort(DurationTextComparer.Instance);

        Assert.Equal(["100", "5000"], duracoes);
    }

    /// <summary>
    /// O painel de histórico apresenta "HH:mm:ss" (três segmentos). Antes de o suporte
    /// aos três segmentos, estes valores caíam no <c>double.TryParse</c> e devolviam
    /// todos 0 — a coluna ficava ordenada alfabeticamente sem o parecer.
    /// </summary>
    [Fact]
    public void FormatoComHoras_OrdenaPorValorNumerico()
    {
        var duracoes = new List<string> { "10:00:00", "01:05:00", "00:00:59", "00:01:30" };

        duracoes.Sort(DurationTextComparer.Instance);

        Assert.Equal(["00:00:59", "00:01:30", "01:05:00", "10:00:00"], duracoes);
    }

    [Fact]
    public void FormatoComHoras_RespeitaOSegmentoDasHoras()
    {
        // 59 minutos e 59 segundos (0:59:59) é MENOS que uma hora (01:00:00): sem o
        // segmento das horas seria tratado como 59 horas.
        Assert.True(DurationTextComparer.Instance.Compare("00:59:59", "01:00:00") < 0);
    }

    [Fact]
    public void FormatosDiferentes_ValemOMesmoTempo()
    {
        // "10:00" (10 minutos) e "00:10:00" (10 minutos) são o mesmo instante.
        // Quando o valor numérico coincide o comparador desempata por texto, por isso
        // a igualdade afirma-se pela posição relativa a um valor de referência.
        foreach (var referencia in new[] { "5:00", "20:00" })
        {
            var peloCurto = Math.Sign(DurationTextComparer.Instance.Compare("10:00", referencia));
            var peloLongo = Math.Sign(DurationTextComparer.Instance.Compare("00:10:00", referencia));

            Assert.Equal(peloCurto, peloLongo);
        }
    }

    [Fact]
    public void TextoNaoInterpretavelComTresSegmentos_OrdenaComoZero()
    {
        Assert.True(DurationTextComparer.Instance.Compare("aa:bb:cc", "00:00:01") < 0);
    }

    [Fact]
    public void MaisDeTresSegmentos_OrdenaComoZero()
    {
        Assert.True(DurationTextComparer.Instance.Compare("1:2:3:4", "00:00:01") < 0);
    }

    /// <summary>
    /// Garante que o texto exposto pelo <see cref="Models.DTO.HistoryResponse"/> é
    /// exatamente o formato que o comparador sabe interpretar. Se a propriedade mudar
    /// de formato, este teste falha em vez de a coluna voltar a ordenar por texto.
    /// </summary>
    [Fact]
    public void FormatoDoHistoryPanel_EOQueOComparadorEntende()
    {
        var resposta = new Models.DTO.HistoryResponse
        {
            TempoPuro = 3_723_000, // 1h 02m 03s
            TempoProcessamento = 59_000 // 0h 00m 59s
        };

        Assert.Equal("01:02:03", resposta.DurationDisplay);
        Assert.Equal("00:00:59", resposta.ProcessDurationDisplay);

        // O mais longo tem de ordenar depois do mais curto.
        Assert.True(DurationTextComparer.Instance.Compare(resposta.ProcessDurationDisplay, resposta.DurationDisplay) < 0);
    }
}

/// <summary>
/// Testes da formatação da data de criação na grelha e nas exportações. A base de
/// dados grava UTC ("o") e o leitor devolve <see cref="DateTimeKind.Local"/> ou
/// <see cref="DateTimeKind.Utc"/> consoante o caminho: a formatação não pode deslocar
/// a hora duas vezes.
/// </summary>
public class BenchmarkEvaluationModelDateTests
{
    [Fact]
    public void DataUtc_ConvertidaParaHoraLocal()
    {
        var utc = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        var linha = new Models.DTO.BenchmarkEvaluationModel { DataCriacao = utc };

        Assert.Equal(utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), linha.DataCriacaoFormatada);
    }

    [Fact]
    public void DataLocal_NaoEDeslocada()
    {
        var local = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Local);

        var linha = new Models.DTO.BenchmarkEvaluationModel { DataCriacao = local };

        Assert.Equal(local.ToString("dd/MM/yyyy HH:mm"), linha.DataCriacaoFormatada);
    }

    [Fact]
    public void DataSemKind_FormatadaTalQual()
    {
        var semKind = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Unspecified);

        var linha = new Models.DTO.BenchmarkEvaluationModel { DataCriacao = semKind };

        Assert.Equal(semKind.ToString("dd/MM/yyyy HH:mm"), linha.DataCriacaoFormatada);
    }
}