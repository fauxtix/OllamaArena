using OllamaArena.Models.DTO;
using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

/// <summary>
/// Cobertura da allow-list de HTML aplicada à saída do modelo.
///
/// Contexto: as respostas são renderizadas com <c>(MarkupString)</c>, sem qualquer escape
/// do Blazor. O Markdig passa HTML cru do texto do modelo sem alterar, pelo que um
/// <c>&lt;img onerror=...&gt;</c> escrito na resposta chegava intacto ao DOM. Estes
/// testes fixam o contrato: o que não está na allow-list sai, e o markdown legítimo
/// continua legível.
/// </summary>
public class MessageFormatterSecurityTests
{
    // ============================================================
    //  XSS — nada sobrevive
    // ============================================================

    [Theory]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("<div style=\"x\" onmouseover=\"alert(1)\">hi</div>")]
    [InlineData("<a href=\"javascript:alert(1)\">clique</a>")]
    [InlineData("<iframe src=\"https://exemplo.pt\"></iframe>")]
    [InlineData("<object data=\"payload.swf\"></object>")]
    [InlineData("<embed src=\"payload.swf\">")]
    [InlineData("<form action=\"https://exemplo.pt\"><input name=\"p\"></form>")]
    [InlineData("<button onclick=\"alert(1)\">ok</button>")]
    [InlineData("<body onload=alert(1)>")]
    [InlineData("<meta http-equiv=\"refresh\" content=\"0;url=https://exemplo.pt\">")]
    [InlineData("<base href=\"https://exemplo.pt/\">")]
    [InlineData("<link rel=\"stylesheet\" href=\"https://exemplo.pt/x.css\">")]
    [InlineData("<style>body{display:none}</style>")]
    [InlineData("<script>alert(1)</script>")]
    public void Sanitize_RemoveVetoresDeExecucao(string payload)
    {
        var html = HtmlOutputSanitizer.Sanitize(payload);

        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onload", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onmouseover", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<embed", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<meta", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<base", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exemplo.pt", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_HandlersDisparadosPorQualquerAtributoSaoRemovidos()
    {
        var html = HtmlOutputSanitizer.Sanitize(
            "<details ontoggle=alert(1)><summary>s</summary><marquee onstart=alert(1)>x</marquee></details>");

        Assert.DoesNotContain("ontoggle", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onstart", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<marquee", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    public void Sanitize_EsquemasDeUriPerigososSaoNeutralizados(string href)
    {
        var html = HtmlOutputSanitizer.Sanitize($"<a href=\"{href}\">clique</a>");

        Assert.DoesNotContain("vbscript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:text/html", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(href, html);
    }

    [Fact]
    public void Sanitize_CssNaoPodeReferenciarRecursosExternos()
    {
        var html = HtmlOutputSanitizer.Sanitize(
            "<div style=\"background-image:url(https://exemplo.pt/r.png)\">x</div>");

        Assert.DoesNotContain("url(", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exemplo.pt", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_AtributosDataSaoRemovidos()
    {
        var html = HtmlOutputSanitizer.Sanitize("<div data-behaviour=\"spy\" data-x=\"1\">x</div>");

        Assert.DoesNotContain("data-behaviour", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-x", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("x", html);
    }

    // ============================================================
    //  Markdown legítimo — não pode ser estragado pela allow-list
    // ============================================================

    [Theory]
    [InlineData("texto com **negrito** aqui", "<strong>negrito</strong>")]
    [InlineData("*itálico*", "<em>itálico</em>")]
    [InlineData("`código`", "<code>código</code>")]
    [InlineData("- a\n- b", "<li>")]
    [InlineData("> citação", "<blockquote>")]
    [InlineData("| a | b |\n|---|---|\n| 1 | 2 |", "<table>")]
    public void Sanitize_PreservaMarkdownLegivel(string markdown, string esperado)
    {
        var html = HtmlOutputSanitizer.Sanitize(MessageFormatter.FormatMessagePlus(markdown));

        Assert.Contains(esperado, html);
    }

    [Fact]
    public void Sanitize_PreservaBlocosDeCodigoComLanguage()
    {
        var html = MessageFormatter.FormatMessagePlus("```csharp\nvar x = 1;\n```");

        Assert.Contains("<pre>", html);
        Assert.Contains("language-csharp", html);
        Assert.Contains("var x = 1;", html);
    }

    [Fact]
    public void Sanitize_PreservaListasDeTarefas()
    {
        var html = MessageFormatter.FormatMessagePlus("- [x] feito\n- [ ] por fazer");

        Assert.Contains("contains-task-list", html);
        Assert.Contains("task-list-item", html);
        Assert.Contains("checkbox", html);
    }

    [Fact]
    public void Sanitize_PreservaLigacoesHttpComRelParaSeguranca()
    {
        var html = HtmlOutputSanitizer.Sanitize("<a href=\"https://exemplo.pt\">sítio</a>");

        Assert.Contains("href=\"https://exemplo.pt\"", html);
    }

    [Fact]
    public void Sanitize_PermiteAtributoStyleDeAlinhamentoDeTabela()
    {
        // <td> tem de vir dentro de <table>: o parser HTML descarta um <td> isolado.
        var html = HtmlOutputSanitizer.Sanitize(
            "<table><tr><td style=\"text-align:center\">1</td></tr></table>");

        Assert.Contains("<td", html);
        Assert.Contains("text-align", html);
        Assert.Contains("center", html);
    }

    [Fact]
    public void Sanitize_PreservaTextoDeTagsRemovidas()
    {
        // A allow-list remove a tag mas não pode engolir o conteúdo que o modelo escreveu.
        var html = HtmlOutputSanitizer.Sanitize("<div>antes<marquee>durante</marquee>depois</div>");

        Assert.Contains("antes", html);
        Assert.Contains("durante", html);
        Assert.Contains("depois", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_EntradaVaziaDevolveVazio(string? entrada)
    {
        Assert.Equal(string.Empty, HtmlOutputSanitizer.Sanitize(entrada));
    }

    [Fact]
    public void Sanitize_NAoLancaParaConteudoEnorme()
    {
        var enorme = string.Concat(Enumerable.Repeat("<b>x</b><script>alert(1)</script>", 2000));

        var html = HtmlOutputSanitizer.Sanitize(enorme);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    //  Pipeline completo
    // ============================================================

    [Fact]
    public void FormatMessagePlus_HtmlCruDoModeloNaoChegaAoDomio()
    {
        // Sem o sanitizador, o Markdig devolve este HTML intacto: é o bug que originou
        // a allow-list. O teste documenta o comportamento do pipeline todo.
        var html = MessageFormatter.FormatMessagePlus("Primeiro parágrafo.\n\n<img src=x onerror=alert(1)>");

        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Primeiro parágrafo.", html);
    }

    [Fact]
    public void FormatMessagePlus_PlaceholderDevolveTypingDots()
    {
        var html = MessageFormatter.FormatMessagePlus(ChatMessage.PendingPlaceholder);

        Assert.Contains("typing-dots", html);
    }
}