using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OllamaArena.Services;

/// <summary>
/// Serviço de recomendação de temperatura para LLMs.
/// Combina análise lexical (PT+EN), estrutural e heurística de código
/// para decidir entre rigor factual e criatividade.
/// </summary>
public static class ChatMeasureTemperature
{
    // ============================================================
    //  CONFIGURAÇÃO (fácil de afinar)
    // ============================================================
    private const double TempStrongFactual = 0.10;
    private const double TempFactual = 0.20;
    private const double TempMildFactual = 0.30;
    private const double TempNeutral = 0.42;
    private const double TempMildCreative = 0.65;
    private const double TempCreative = 0.78;
    private const double TempStrongCreative = 0.90;

    // ============================================================
    //  FRASES MULTI-PALAVRA (peso alto – verificadas primeiro)
    // ============================================================
    private static readonly (string Phrase, float Weight)[] FrasesFactuais =
    {
        // PT
        ("como funciona", 5f), ("como fazer", 4.5f), ("como resolver", 5f),
        ("como implementar", 5f), ("como corrigir", 5f), ("passo a passo", 4.5f),
        ("base de dados", 3.5f), ("pull request", 3.5f), ("unit test", 3.5f),
        ("stack trace", 4f), ("por que", 2.5f), ("porquê", 2.5f),
        ("explica me", 3.5f), ("explica-me", 3.5f), ("mostra me", 2.5f), 

        // EN
        ("how does", 5f), ("how to", 4.5f), ("step by step", 4.5f),
        ("how many", 3.5f), ("how much", 3.5f), ("pull request", 3.5f),
        ("unit test", 3.5f), ("stack trace", 4f), ("what is", 2.5f),
        ("what are", 2.5f), ("explain how", 4.5f), ("show me how", 3.5f)
    };

    private static readonly (string Phrase, float Weight)[] FrasesCriativas =
    {
        // PT
        ("role play", 6f), ("roleplay", 6f), ("bate papo", 3.5f),
        ("o que achas", 3.5f), ("o que pensas", 3.5f), ("conta me", 3.5f),
        ("inventa uma", 5f), ("cria uma", 4.5f), ("escreve um", 4.5f),
        ("escreve uma", 4.5f), ("em estilo de", 4f), ("no estilo", 4f),
        ("faz de conta", 5f), ("imagina que", 4.5f),

        // EN
        ("role play", 6f), ("role-play", 6f), ("roleplay", 6f),
        ("act as", 6f), ("pretend to be", 6f), ("what if", 5f),
        ("come up with", 4.5f), ("make up", 4f), ("tell me a", 4f),
        ("in the style of", 4.5f), ("write a story", 5f), ("write a poem", 5.5f)
    };

    // ============================================================
    //  TERMOS SINGLE-WORD (peso médio/baixo)
    // ============================================================
    private static readonly Dictionary<string, float> TermosFactuais = new(StringComparer.Ordinal)
    {
        // PT – intenções
        ["qual"] = 1.2f,
        ["quais"] = 1.2f,
        ["quanto"] = 2.2f,
        ["quantos"] = 2.2f,
        ["quando"] = 1.3f,
        ["onde"] = 1.3f,
        ["quem"] = 1.3f,
        ["porque"] = 2.3f,
        ["explica"] = 3.5f,
        ["explicar"] = 3.5f,
        ["define"] = 3.2f,
        ["definição"] = 3.2f,
        ["significa"] = 2.4f,
        ["significado"] = 2.4f,
        ["lista"] = 2.3f,
        ["enumera"] = 2.3f,
        ["mostra"] = 1.5f,
        ["apresenta"] = 1.5f,
        ["resume"] = 2.5f,
        ["resumo"] = 2.5f,
        ["sintetiza"] = 2.5f,
        ["calcula"] = 3.5f,
        ["calcular"] = 3.5f,
        ["converte"] = 2.5f,
        ["converter"] = 2.5f,
        ["traduz"] = 2.4f,
        ["traduzir"] = 2.4f,
        ["compara"] = 2.4f,
        ["comparar"] = 2.4f,
        ["diferença"] = 2.3f,
        ["diferenca"] = 2.3f,
        ["vantagens"] = 2.2f,
        ["desvantagens"] = 2.2f,
        ["tutorial"] = 3.5f,
        ["guia"] = 2.5f,
        ["instruções"] = 3.3f,
        ["instrucoes"] = 3.3f,

        // PT – dados / ciência
        ["dados"] = 3.3f,
        ["estatística"] = 3.5f,
        ["estatistica"] = 3.5f,
        ["percentagem"] = 3.3f,
        ["percentual"] = 2.5f,
        ["taxa"] = 2.3f,
        ["média"] = 2.4f,
        ["media"] = 2.4f,
        ["mediana"] = 2.4f,
        ["desvio"] = 2.5f,
        ["correlação"] = 3.3f,
        ["correlacao"] = 3.3f,
        ["amostra"] = 2.4f,
        ["população"] = 2.3f,
        ["populacao"] = 2.3f,
        ["mortalidade"] = 3.4f,
        ["incidência"] = 3.3f,
        ["incidencia"] = 3.3f,
        ["prevalência"] = 3.3f,
        ["prevalencia"] = 3.3f,
        ["estudo"] = 2.5f,
        ["pesquisa"] = 2.5f,
        ["resultado"] = 1.8f,
        ["conclusão"] = 2.5f,
        ["conclusao"] = 2.5f,
        ["evidência"] = 2.6f,
        ["evidencia"] = 2.6f,

        // PT – técnico
        ["código"] = 4.5f,
        ["codigo"] = 4.5f,
        ["função"] = 3.5f,
        ["funcao"] = 3.5f,
        ["método"] = 3.4f,
        ["metodo"] = 3.4f,
        ["classe"] = 3.5f,
        ["objeto"] = 2.5f,
        ["erro"] = 4.5f,
        ["bug"] = 4.5f,
        ["exception"] = 4.5f,
        ["exceção"] = 4.5f,
        ["excecao"] = 4.5f,
        ["stacktrace"] = 4.5f,
        ["debug"] = 4.5f,
        ["debugging"] = 4.5f,
        ["compilar"] = 3.5f,
        ["compilação"] = 3.5f,
        ["compilacao"] = 3.5f,
        ["runtime"] = 3.3f,
        ["sintaxe"] = 3.5f,
        ["syntax"] = 3.5f,
        ["algoritmo"] = 3.5f,
        ["complexidade"] = 3.4f,
        ["performance"] = 2.6f,
        ["otimizar"] = 3.4f,
        ["otimização"] = 3.4f,
        ["otimizacao"] = 3.4f,
        ["api"] = 3.5f,
        ["endpoint"] = 3.4f,
        ["request"] = 2.5f,
        ["response"] = 2.5f,
        ["json"] = 2.6f,
        ["xml"] = 2.4f,
        ["sql"] = 3.6f,
        ["query"] = 3.5f,
        ["database"] = 3.5f,
        ["tabela"] = 2.5f,
        ["coluna"] = 2.4f,
        ["índice"] = 2.5f,
        ["indice"] = 2.5f,
        ["git"] = 2.6f,
        ["commit"] = 2.5f,
        ["branch"] = 2.5f,
        ["merge"] = 2.5f,
        ["teste"] = 2.6f,
        ["mock"] = 2.8f,
        ["assert"] = 2.8f,

        // EN – intenções
        ["what"] = 1.3f,
        ["which"] = 1.3f,
        ["when"] = 1.3f,
        ["where"] = 1.3f,
        ["who"] = 1.3f,
        ["why"] = 2.3f,
        ["explain"] = 3.5f,
        ["define"] = 3.3f,
        ["definition"] = 3.3f,
        ["meaning"] = 2.4f,
        ["means"] = 1.6f,
        ["list"] = 2.3f,
        ["enumerate"] = 2.3f,
        ["show"] = 1.5f,
        ["display"] = 1.5f,
        ["summarize"] = 2.6f,
        ["summary"] = 2.6f,
        ["calculate"] = 3.5f,
        ["convert"] = 2.5f,
        ["translate"] = 2.4f,
        ["compare"] = 2.4f,
        ["difference"] = 2.3f,
        ["tutorial"] = 3.5f,
        ["guide"] = 2.5f,
        ["instructions"] = 3.3f,
        ["howto"] = 3.4f,

        // EN – data / stats
        ["data"] = 3.3f,
        ["statistics"] = 3.5f,
        ["stat"] = 2.4f,
        ["percentage"] = 3.3f,
        ["rate"] = 2.3f,
        ["average"] = 2.4f,
        ["mean"] = 2.4f,
        ["median"] = 2.4f,
        ["deviation"] = 2.5f,
        ["correlation"] = 3.3f,
        ["sample"] = 2.4f,
        ["population"] = 2.3f,
        ["mortality"] = 3.4f,
        ["incidence"] = 3.3f,
        ["prevalence"] = 3.3f,
        ["study"] = 2.5f,
        ["research"] = 2.5f,
        ["result"] = 1.8f,
        ["conclusion"] = 2.5f,
        ["evidence"] = 2.6f,

        // EN – code / technical
        ["code"] = 4.5f,
        ["function"] = 3.5f,
        ["method"] = 3.4f,
        ["class"] = 3.5f,
        ["object"] = 2.5f,
        ["error"] = 4.5f,
        ["bug"] = 4.5f,
        ["exception"] = 4.5f,
        ["stacktrace"] = 4.5f,
        ["debug"] = 4.5f,
        ["compile"] = 3.5f,
        ["runtime"] = 3.3f,
        ["syntax"] = 3.5f,
        ["algorithm"] = 3.5f,
        ["complexity"] = 3.4f,
        ["performance"] = 2.6f,
        ["optimize"] = 3.4f,
        ["api"] = 3.5f,
        ["endpoint"] = 3.4f,
        ["request"] = 2.5f,
        ["response"] = 2.5f,
        ["json"] = 2.6f,
        ["sql"] = 3.6f,
        ["query"] = 3.5f,
        ["database"] = 3.5f,
        ["table"] = 2.5f,
        ["index"] = 2.5f,
        ["git"] = 2.6f,
        ["commit"] = 2.5f,
        ["branch"] = 2.5f,
        ["merge"] = 2.5f,
        ["test"] = 2.6f,
        ["assert"] = 2.8f,
        ["mock"] = 2.8f,

        // PT – história / política
        ["historia"] = 3.2f,
        ["historico"] = 3.0f,
        ["cronologia"] = 3.5f,
        ["periodo"] = 2.5f,
        ["epoca"] = 2.5f,
        ["dinastia"] = 3.2f,
        ["monarquia"] = 3.2f,
        ["republica"] = 3.2f,
        ["rei"] = 2.8f,
        ["rainha"] = 2.8f,
        ["imperador"] = 3.0f,
        ["governo"] = 2.8f,
        ["presidente"] = 2.8f,
        ["primeiro"] = 1.0f,
        ["ministro"] = 1.8f,
        ["parlamento"] = 2.8f,
        ["assembleia"] = 2.5f,
        ["eleicao"] = 2.8f,
        ["votacao"] = 2.5f,
        ["constituicao"] = 3.2f,
        ["tratado"] = 3.0f,
        ["guerra"] = 3.2f,
        ["revolucao"] = 3.2f,
        ["independencia"] = 3.0f,
        ["colonizacao"] = 3.0f,
        ["imperio"] = 3.0f,

        // EN – history / politics
        ["history"] = 3.2f,
        ["historical"] = 3.0f,
        ["chronology"] = 3.5f,
        ["period"] = 2.5f,
        ["era"] = 2.5f,
        ["dynasty"] = 3.2f,
        ["monarchy"] = 3.2f,
        ["republic"] = 3.2f,
        ["king"] = 2.8f,
        ["queen"] = 2.8f,
        ["emperor"] = 3.0f,
        ["government"] = 2.8f,
        ["president"] = 2.8f,
        ["prime"] = 1.0f,
        ["minister"] = 1.8f,
        ["parliament"] = 2.8f,
        ["assembly"] = 2.5f,
        ["election"] = 2.8f,
        ["vote"] = 2.5f,
        ["constitution"] = 3.2f,
        ["treaty"] = 3.0f,
        ["war"] = 3.2f,
        ["revolution"] = 3.2f,
        ["independence"] = 3.0f,
        ["colonization"] = 3.0f,
        ["empire"] = 3.0f,

        // PT – geografia
        ["capital"] = 3.2f,
        ["pais"] = 3.0f,
        ["continente"] = 3.0f,
        ["oceano"] = 3.0f,
        ["mar"] = 2.5f,
        ["rio"] = 2.5f,
        ["lago"] = 2.5f,
        ["ilha"] = 2.5f,
        ["arquipelago"] = 3.0f,
        ["montanha"] = 2.8f,
        ["serra"] = 2.8f,
        ["fronteira"] = 2.8f,
        ["regiao"] = 2.5f,
        ["provincia"] = 2.5f,
        ["cidade"] = 2.5f,
        ["municipio"] = 2.5f,
        ["territorio"] = 2.8f,
        ["mapa"] = 2.6f,
        ["localizacao"] = 2.8f,
        ["coordenadas"] = 3.0f,

        // EN – geography
        ["capital"] = 3.2f,
        ["country"] = 3.0f,
        ["continent"] = 3.0f,
        ["ocean"] = 3.0f,
        ["sea"] = 2.5f,
        ["river"] = 2.5f,
        ["lake"] = 2.5f,
        ["island"] = 2.5f,
        ["archipelago"] = 3.0f,
        ["mountain"] = 2.8f,
        ["range"] = 2.6f,
        ["border"] = 2.8f,
        ["region"] = 2.5f,
        ["province"] = 2.5f,
        ["city"] = 2.5f,
        ["municipality"] = 2.5f,
        ["territory"] = 2.8f,
        ["map"] = 2.6f,
        ["location"] = 2.8f,
        ["coordinates"] = 3.0f,

        // PT – ciencia
        ["ciencia"] = 3.0f,
        ["fisica"] = 3.2f,
        ["quimica"] = 3.2f,
        ["biologia"] = 3.2f,
        ["geologia"] = 3.0f,
        ["astronomia"] = 3.0f,
        ["matematica"] = 3.2f,
        ["planeta"] = 2.8f,
        ["estrela"] = 2.8f,
        ["galaxia"] = 2.8f,
        ["universo"] = 2.8f,
        ["atomo"] = 3.0f,
        ["molecula"] = 3.0f,
        ["energia"] = 2.8f,
        ["massa"] = 2.6f,
        ["gravidade"] = 3.0f,
        ["evolucao"] = 3.0f,
        ["especie"] = 2.8f,
        ["formula"] = 2.8f,
        ["equacao"] = 3.0f,

        // EN – science
        ["science"] = 3.0f,
        ["physics"] = 3.2f,
        ["chemistry"] = 3.2f,
        ["biology"] = 3.2f,
        ["geology"] = 3.0f,
        ["astronomy"] = 3.0f,
        ["mathematics"] = 3.2f,
        ["planet"] = 2.8f,
        ["star"] = 2.8f,
        ["galaxy"] = 2.8f,
        ["universe"] = 2.8f,
        ["atom"] = 3.0f,
        ["molecule"] = 3.0f,
        ["energy"] = 2.8f,
        ["mass"] = 2.6f,
        ["gravity"] = 3.0f,
        ["evolution"] = 3.0f,
        ["species"] = 2.8f,
        ["formula"] = 2.8f,
        ["equation"] = 3.0f,

        // PT – saude
        ["saude"] = 2.8f,
        ["medicina"] = 3.0f,
        ["doenca"] = 3.2f,
        ["virus"] = 3.0f,
        ["bacteria"] = 3.0f,
        ["tratamento"] = 3.0f,
        ["vacina"] = 3.0f,
        ["diagnostico"] = 3.0f,
        ["sintoma"] = 2.8f,
        ["terapia"] = 2.8f,
        ["hospital"] = 2.5f,
        ["medico"] = 2.5f,
        ["paciente"] = 2.5f,
        ["farmaco"] = 2.8f,
        ["medicamento"] = 2.8f,

        // EN – health
        ["health"] = 2.8f,
        ["medicine"] = 3.0f,
        ["disease"] = 3.2f,
        ["virus"] = 3.0f,
        ["bacteria"] = 3.0f,
        ["treatment"] = 3.0f,
        ["vaccine"] = 3.0f,
        ["diagnosis"] = 3.0f,
        ["symptom"] = 2.8f,
        ["therapy"] = 2.8f,
        ["hospital"] = 2.5f,
        ["doctor"] = 2.5f,
        ["patient"] = 2.5f,
        ["drug"] = 2.8f,
        ["medication"] = 2.8f,


    };

    private static readonly Dictionary<string, float> TermosCriativos = new(StringComparer.Ordinal)
    {
        // PT
        ["inventa"] = 4.5f,
        ["inventar"] = 4.5f,
        ["cria"] = 3.5f,
        ["criar"] = 3.5f,
        ["escreve"] = 3.3f,
        ["escrever"] = 3.3f,
        ["poema"] = 5.5f,
        ["poesia"] = 5.5f,
        ["história"] = 4.5f,
        ["historia"] = 4.5f,
        ["conto"] = 4.3f,
        ["narrativa"] = 4.3f,
        ["opinião"] = 3.3f,
        ["opiniao"] = 3.3f,
        ["acha"] = 2.4f,
        ["achas"] = 2.4f,
        ["pensas"] = 2.4f,
        ["sentes"] = 2.5f,
        ["sugere"] = 3.3f,
        ["sugerir"] = 3.3f,
        ["recomenda"] = 2.5f,
        ["recomendar"] = 2.5f,
        ["ideias"] = 3.3f,
        ["imagina"] = 4.5f,
        ["imaginar"] = 4.5f,
        ["cenário"] = 4.3f,
        ["cenario"] = 4.3f,
        ["situação"] = 2.3f,
        ["situacao"] = 2.3f,
        ["piada"] = 5.5f,
        ["humor"] = 4.3f,
        ["engraçado"] = 3.3f,
        ["divertido"] = 2.5f,
        ["roleplay"] = 5.5f,
        ["interpreta"] = 4.3f,
        ["personagem"] = 4.3f,
        ["conversa"] = 2.3f,
        ["desabafa"] = 3.3f,
        ["desabafo"] = 3.3f,
        ["sonho"] = 3.3f,
        ["fantasia"] = 4.5f,
        ["ficção"] = 4.5f,
        ["ficcao"] = 4.5f,
        ["futuro"] = 2.2f,
        ["alternativa"] = 2.4f,
        ["reescreve"] = 3.4f,
        ["reescrever"] = 3.4f,
        ["varia"] = 2.5f,
        ["variação"] = 2.5f,
        ["variacao"] = 2.5f,
        ["estilo"] = 2.6f,
        ["metáfora"] = 3.4f,
        ["metafora"] = 3.4f,
        ["analogia"] = 3.3f,
        ["criativo"] = 3.4f,
        ["criatividade"] = 3.4f,

        // EN
        ["invent"] = 4.5f,
        ["create"] = 3.5f,
        ["write"] = 2.6f,
        ["compose"] = 3.4f,
        ["poem"] = 5.5f,
        ["poetry"] = 5.5f,
        ["story"] = 4.5f,
        ["tale"] = 4.3f,
        ["narrative"] = 4.3f,
        ["fiction"] = 4.5f,
        ["opinion"] = 3.3f,
        ["think"] = 1.6f,
        ["feel"] = 2.4f,
        ["believe"] = 2.3f,
        ["suggest"] = 3.3f,
        ["recommend"] = 2.5f,
        ["ideas"] = 3.3f,
        ["brainstorm"] = 4.3f,
        ["imagine"] = 4.5f,
        ["scenario"] = 4.3f,
        ["situation"] = 2.3f,
        ["joke"] = 5.5f,
        ["funny"] = 3.4f,
        ["humor"] = 4.3f,
        ["pun"] = 4.3f,
        ["roleplay"] = 5.5f,
        ["character"] = 4.3f,
        ["persona"] = 4.3f,
        ["chat"] = 2.3f,
        ["conversation"] = 2.3f,
        ["talk"] = 1.6f,
        ["dream"] = 3.3f,
        ["fantasy"] = 4.5f,
        ["alternate"] = 3.3f,
        ["future"] = 1.8f,
        ["rewrite"] = 3.4f,
        ["rephrase"] = 2.6f,
        ["variation"] = 2.5f,
        ["style"] = 2.6f,
        ["metaphor"] = 3.4f,
        ["analogy"] = 3.3f,
        ["creative"] = 3.4f,
        ["creativity"] = 3.4f,
        ["generate"] = 2.5f
    };

    // ============================================================
    //  API PÚBLICA
    // ============================================================

    /// <summary>
    /// Analisa o prompt e devolve a temperatura recomendada (0.10 – 0.90).
    /// </summary>
    public static double ObterTemperaturaRecomendada(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return 0.20;

        var analysis = Analyze(prompt);
        return MapScoreToTemperature(analysis);
    }

    // ============================================================
    //  ANÁLISE COMPLETA
    // ============================================================

    private sealed class PromptAnalysis
    {
        public float FactualScore { get; set; }
        public float CreativeScore { get; set; }
        public bool LooksLikeCode { get; set; }
        public int TokenCount { get; set; }
        public int CharCount { get; set; }
        public float CodeDensity { get; set; }      // 0..1
        public float QuestionDensity { get; set; }  // 0..1
    }

    private static PromptAnalysis Analyze(string prompt)
    {
        var result = new PromptAnalysis
        {
            CharCount = prompt.Length
        };

        string textoLimpo = RemoverAcentos(prompt).ToLowerInvariant();

        // ---------- 1. Detecção de código (muito forte) ----------
        result.LooksLikeCode = DetectCode(prompt, out float codeDensity);
        result.CodeDensity = codeDensity;

        // ---------- 2. Frases multi-palavra ----------
        foreach (var (phrase, weight) in FrasesFactuais)
            if (textoLimpo.Contains(phrase))
                result.FactualScore += weight;

        foreach (var (phrase, weight) in FrasesCriativas)
            if (textoLimpo.Contains(phrase))
                result.CreativeScore += weight;

        // ---------- 3. Tokenização + matching de palavras ----------
        var tokens = Tokenizar(textoLimpo);
        result.TokenCount = tokens.Count;

        foreach (var token in tokens)
        {
            if (TermosFactuais.TryGetValue(token, out float wF))
                result.FactualScore += wF;

            if (TermosCriativos.TryGetValue(token, out float wC))
                result.CreativeScore += wC;
        }

        // ---------- 4. Densidade de perguntas ----------
        int questionMarks = prompt.Count(c => c == '?' || c == '？');
        result.QuestionDensity = result.TokenCount > 0
            ? Math.Min(1f, questionMarks / (float)Math.Max(1, result.TokenCount / 8))
            : 0f;

        // ---------- 5. Boosts / penalizações estruturais ----------
        ApplyStructuralModifiers(result);

        return result;
    }

    private static void ApplyStructuralModifiers(PromptAnalysis a)
    {
        // Código presente → forte puxão para factual
        if (a.LooksLikeCode)
        {
            a.FactualScore += 8f + (a.CodeDensity * 12f);
            a.CreativeScore *= 0.35f; // suprime criatividade
        }

        // Prompts muito longos tendem a ser mais técnicos/explicativos
        if (a.CharCount > 600)
            a.FactualScore += 2.5f;
        else if (a.CharCount > 300)
            a.FactualScore += 1.2f;

        // Poucas palavras e já com intenção criativa
        if (a.TokenCount <= 4 &&
            a.CreativeScore > a.FactualScore)
        {
            a.CreativeScore += 1.5f;
        }
        // Prompts muito curtos + criativos → ainda mais criativos
        if (a.CharCount < 80 && a.CreativeScore > a.FactualScore)
            a.CreativeScore += 2.0f;

        // Muitas perguntas → ligeiro boost factual
        a.FactualScore += a.QuestionDensity * 4.0f;
    }

    // ============================================================
    //  MAPEAMENTO SCORE → TEMPERATURA
    // ============================================================

    private static double MapScoreToTemperature(PromptAnalysis a)
    {
        // Score líquido normalizado (positivo = factual, negativo = criativo)
        float net = a.FactualScore - a.CreativeScore;

        // Normalização suave baseada na magnitude
        float magnitude = Math.Max(a.FactualScore + a.CreativeScore, 1f);
        float normalized = net / (magnitude * 0.65f); // escala empírica
        normalized = Math.Clamp(normalized, -1.5f, 1.5f);

        // Mapeamento por faixas (mais interpretável e estável)
        return normalized switch
        {
            >= 1.10f => TempStrongFactual,
            >= 0.55f => TempFactual,
            >= 0.22f => TempMildFactual,
            >= -0.22f => TempNeutral,
            >= -0.55f => TempMildCreative,
            >= -1.10f => TempCreative,
            _ => TempStrongCreative
        };
    }

    // ============================================================
    //  DETECÇÃO DE CÓDIGO
    // ============================================================

    private static bool DetectCode(string original, out float density)
    {
        // Heurística leve e rápida
        int symbols = 0;
        int total = original.Length;

        foreach (char c in original)
        {
            if (c is '{' or '}' or '(' or ')' or '[' or ']' or ';' or '=' or '>' or '<' or '#'
                     or '@' or '$' or '\\' or '|' or '&')
                symbols++;
        }

        density = total > 0 ? symbols / (float)total : 0f;

        // Keywords técnicas fortes
        string lower = original.ToLowerInvariant();
        bool hasStrongKeywords =
            lower.Contains("public class") || lower.Contains("private void") ||
            lower.Contains("function ") || lower.Contains("def ") ||
            lower.Contains("const ") || lower.Contains("let ") ||
            lower.Contains("var ") || lower.Contains("import ") ||
            lower.Contains("using ") || lower.Contains("namespace ") ||
            lower.Contains("select ") || lower.Contains("from ") ||
            lower.Contains("where ") || lower.Contains("console.log") ||
            lower.Contains("system.out") || lower.Contains("printf") ||
            lower.Contains("=>") || lower.Contains("->");

        // Decisão
        bool isCode = density > 0.045f || hasStrongKeywords ||
                      (density > 0.025f && original.Contains('\n') && original.Length > 80);

        return isCode;
    }

    // ============================================================
    //  UTILITÁRIOS
    // ============================================================

    public static string RemoverAcentos(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var normalized = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static List<string> Tokenizar(string texto)
    {
        // Remove tudo o que não é letra ou número e faz split
        var limpo = Regex.Replace(texto, @"[^\p{L}\p{N}\s]", " ");
        return limpo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}