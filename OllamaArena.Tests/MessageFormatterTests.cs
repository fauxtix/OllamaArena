using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

/// <summary>
/// Garante que as heurísticas de texto do MessageFormatter não corrompem código
/// (fences ``` ou spans inline `...`) e que as demais formatações seguem intactas.
/// </summary>
public class MessageFormatterTests
{
    [Fact]
    public void FormatMessagePlus_FenceComCamelCase_NaQuebraIdentificadores()
    {
        var input = """
            ```csharp
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
            ```
            """;

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("RandomNumberGenerator", html);
        Assert.DoesNotContain("Random\nNumber", html);
        Assert.DoesNotContain("Random\n\nNumber", html);
        Assert.Contains("ToBase64String", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceComSubtracao_NaInsereQuebra()
    {
        var input = """
            ```
            var x = max - min;
            ```
            """;

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("max - min;", html);
        Assert.DoesNotContain("max\n\n- min;", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceComRegion_NaoAdicionaEspaco()
    {
        var input = """
            ```
            #region Gerador
            #endregion
            ```
            """;

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("#region", html);
        Assert.DoesNotContain("# region", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceComPipeOr_NaoViraTabela()
    {
        var input = """
            ```
            bool ok = (bytes.Length > 0 || bytes[0] != 0);
            ```
            """;

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.DoesNotContain("<table", html);
        Assert.Contains("bytes.Length &gt; 0 || bytes[0] != 0", html);
    }

    [Fact]
    public void FormatMessagePlus_InlineCodeCamelCase_Intacto()
    {
        var input = "Use `RandomNumberGenerator.Create()` aqui.";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("RandomNumberGenerator.Create()", html);
        Assert.DoesNotContain("Random\nNumber", html);
    }

    [Fact]
    public void FormatMessagePlus_RespostaRealista_ComFence_DeixaCodigoIntacto()
    {
        var input = """
            1. Cryptography
            2. Format

            ```csharp
            public sealed class SecureKeyGenerator : ISecureKeyGenerator
            {
                public string GenerateBase64Key(int keySizeInBytes = 32)
                {
                    var bytes = new byte[keySizeInBytes];
                    RandomNumberGenerator.Fill(bytes);
                    return Convert.ToBase64String(bytes);
                }
            }
            ```
            """;

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<ol>", html);
        Assert.Contains("RandomNumberGenerator.Fill(bytes);", html);
        Assert.Contains("GenerateBase64Key", html);
        Assert.DoesNotContain("Secure\nKey", html);
        Assert.DoesNotContain("Random\nNumber", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceDesbalanceada_ContinuaFechando()
    {
        var input = "```csharp\nConsole.WriteLine(\"oi\");";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<pre>", html);
        Assert.Contains("Console.WriteLine", html);
    }

    [Fact]
    public void FormatMessagePlus_ListaNumerada_ContinuaComoLista()
    {
        var input = "1. primeiro item\n2. segundo item";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<ol>", html);
        Assert.Contains("primeiro item", html);
        Assert.Contains("segundo item", html);
    }

    [Fact]
    public void FormatMessagePlus_HeadingSemEspaco_ContinuaCorrigindo()
    {
        var input = "#Título colado";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<h1", html);
        Assert.Contains("Título colado", html);
    }

    [Fact]
    public void FormatMessagePlus_TabelaDePipes_ContinuaComoTabela()
    {
        var input = "| Modelo | TPS |\n|---|---|\n| gemma | 42 |";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<table", html);
        Assert.Contains("gemma", html);
    }

    [Fact]
    public void FormatMessagePlus_Negrito_ContinuaComoNegrito()
    {
        var html = MessageFormatter.FormatMessagePlus("Texto com **destaque** aqui.");

        Assert.Contains("<strong>destaque</strong>", html);
    }

    [Fact]
    public void FormatMessagePlus_ListaDeMarcadores_ContinuaComoLista()
    {
        var html = MessageFormatter.FormatMessagePlus("* primeiro\n* segundo");

        Assert.Contains("<ul>", html);
        Assert.Contains("primeiro", html);
        Assert.Contains("segundo", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceComInfoColada_InsereQuebra()
    {
        var input = "```csharp// Interface definition\npublic interface KeyGenerator { }\n```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("language-csharp", html);
        Assert.DoesNotContain("csharp//", html);
        Assert.Contains("// Interface definition", html);
        Assert.Contains("public interface KeyGenerator { }", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceLinhaUnicaColada_Gemma3_FormataBloco()
    {
        var input = "```csharpusing System;using System.Security.Cryptography;using System.Text;" +
            "// Interface for Key Generation Servicepublic interface IKeyGeneratorService{string GenerateSecureKey();}" +
            "// Implementationpublic class KeyGeneratorService : IKeyGeneratorService{" +
            "public string GenerateSecureKey(){byte[] randomBytes = new byte[32];" +
            "rngCrypto.GetBytes(randomBytes);return Convert.ToBase64String(randomBytes);}}```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<pre>", html);
        Assert.Contains("language-csharp", html);
        Assert.DoesNotContain("csharpusing", html);
        Assert.DoesNotContain("Servicepublic", html);
        Assert.DoesNotContain("&lt;/returns&gt;public", html);
        Assert.Contains("using System;\nusing System.Security.Cryptography;", html);
        Assert.Contains("// Interface for Key Generation Service\npublic interface", html);
        Assert.Contains("        byte[] randomBytes", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceLinhaUnica_DocComment_NaoColaMetodoAoComentario()
    {
        var input = "```csharp/// <summary>/// Creates the key./// </summary>" +
            "/// <returns>The generated key as a Base64-encoded string.</returns>" +
            "public string GenerateSecureKey(){return \"ok\";}```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<pre>", html);
        Assert.Contains("language-csharp", html);
        Assert.DoesNotContain("&lt;/returns&gt;public", html);
        Assert.DoesNotContain("\nreturns&gt;The generated", html);
        Assert.Contains(
            "/// &lt;returns&gt;The generated key as a Base64-encoded string.&lt;/returns&gt;\n" +
            "public string GenerateSecureKey(){", html);
        Assert.Contains("    return &quot;ok&quot;;", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceLinhaUnica_Comentario_NaPartePalavrasEmbutidas()
    {
        var input = "```csharp// Generate and print the keystring generatedKey = GenKey();" +
            "// Encode the bytes to Base64string keyBase64 = Convert.ToBase64String(randomBytes);" +
            "return keyBase64;```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("language-csharp", html);
        Assert.DoesNotContain("pr\nint the", html);
        Assert.Contains("// Generate and print the key\nstring generatedKey", html);
        Assert.Contains("// Encode the bytes to Base64\nstring keyBase64", html);
        Assert.Contains("return keyBase64;", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceTagColadaCorpoMultilinha_Qwen3_ConservaLinhas()
    {
        var input = "```csharpusing System;\nusing System.Security.Cryptography;\n\n" +
            "public class JwtKeyService {\n    public string GenerateKey() { return \"x\"; }\n}\n```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("language-csharp", html);
        Assert.DoesNotContain("csharpusing", html);
        Assert.Contains("using System;\nusing System.Security.Cryptography;", html);
        Assert.Contains("public class JwtKeyService", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceLinhaUnicaComFecho_FormataBloco()
    {
        var input = "```csharp Console.WriteLine(\"ok\");```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<pre>", html);
        Assert.Contains("language-csharp", html);
        Assert.Contains("Console.WriteLine", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceLimpa_SemTagColada_MantemIntacta()
    {
        var input = "```csharp\nConsole.WriteLine(\"ok\");\n```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("language-csharp", html);
        Assert.Contains("Console.WriteLine", html);
    }

    [Fact]
    public void FormatMessagePlus_MarcadorColadoComNegrito_ContinuaCorrigindo()
    {
        var html = MessageFormatter.FormatMessagePlus("* **item** com ***realce***");

        Assert.Contains("<ul>", html);
        Assert.Contains("<strong>item</strong>", html);
        Assert.Contains("<strong>realce</strong>", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceJS_SemComentarios_FormataBloco()
    {
        var input = "```javascriptfunction init(){let x=1;console.log(x);}```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<pre>", html);
        Assert.Contains("language-javascript", html);
        Assert.DoesNotContain("javascriptfunction", html);
        Assert.Contains("function init(){\n    let x=1;\n    console.log(x);", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceJS_ComentarioColado_Quebra()
    {
        var input = "```javascript// Setup function init(){let x=1;}```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("language-javascript", html);
        Assert.DoesNotContain("// Setup function init", html);
        Assert.Contains("// Setup\nfunction init(){\n    let x=1;", html);
    }

    [Fact]
    public void FormatMessagePlus_FencePython_Colado_Indenta()
    {
        var input = "```pythondef main():print(\"hi\")if __name__ == \"__main__\":main()```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("<pre>", html);
        Assert.Contains("language-python", html);
        Assert.DoesNotContain("pythondef", html);
        Assert.Contains("def main():\n    print(&quot;hi&quot;)\n    if __name__ == &quot;__main__&quot;:\n        main()", html);
    }

    [Fact]
    public void FormatMessagePlus_FencePython_ComentarioHash_Quebra()
    {
        var input = "```pythonx = 1 # init counterreturn x```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("language-python", html);
        Assert.DoesNotContain("counterreturn", html);
        Assert.Contains("# init counter\nreturn x", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceLinhaUnica_BlockComment_NaoParte()
    {
        var input = "```csharp/* a; b } */public void Foo(){run();}```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("language-csharp", html);
        Assert.Contains("/* a; b } */\npublic void Foo(){", html);
        Assert.Contains("    run();", html);
    }

    [Fact]
    public void FormatMessagePlus_FenceComTagRepetidaNoFecho_RemoveResiduo()
    {
        var input = "```csharp public class KeyGenerator { public string GenerateRandomKey() { " +
            "byte[] ba = new byte[32]; return BitConverter.ToString(ba).ToLower(); } } csharp```";

        var html = MessageFormatter.FormatMessagePlus(input);

        Assert.Contains("language-csharp", html);
        Assert.Contains("public class KeyGenerator {", html);
        Assert.DoesNotContain("csharp\n</code>", html);
        Assert.DoesNotContain("csharp```", html);
        Assert.Contains("}\n</code></pre>", html);
    }
}
