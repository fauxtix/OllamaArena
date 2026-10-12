$ErrorActionPreference = 'Stop'

# Gera o deck do guia "Como comparar modelos locais" (mesmo template visual dos
# decks de screenshots). Requer Windows com PowerPoint 2016+ (automacao COM).
# Regenerar apos editar o guia:
#   powershell -ExecutionPolicy Bypass -File gerar_guia_como_comparar.ps1

$docsDir   = $PSScriptRoot
$guidePath = Join-Path (Split-Path -Parent $docsDir) 'ComoCompararModelos\Guia_Como_Comparar_Modelos.md'
$outPath   = Join-Path $docsDir 'Guia_Comparar_Modelos.pptx'

if (-not (Test-Path -LiteralPath $guidePath)) { throw "Guia nao encontrado: $guidePath" }

function New-Rgb([int]$r, [int]$g, [int]$b) { return (($b -shl 16) -bor ($g -shl 8) -bor $r) }

$cDark    = New-Rgb  11  30  61   # 0B1E3D
$cDark2   = New-Rgb  20  47  92   # 142F5C
$cRow1    = New-Rgb  17  41  80
$cRow2    = New-Rgb  24  55 104
$cAccent  = New-Rgb   0 120 212   # 0078D4
$cSky     = New-Rgb  56 189 248   # 38BDF8 (acento Fluent do tema)
$cWhite   = New-Rgb 255 255 255
$cLight   = New-Rgb 201 213 234   # C9D5EA
$cGray    = New-Rgb 140 158 184   # 8C9EB8

$total = 15

function Set-Background($slide) {
    $slide.FollowMasterBackground = 0
    $bg = $slide.Background
    try {
        $bg.Fill.Visible = -1
        $bg.Fill.TwoColorGradient(5, 2)
        $bg.Fill.ForeColor.RGB = $cDark
        $bg.Fill.BackColor.RGB = $cDark2
    } catch {
        $bg.Fill.Solid()
        $bg.Fill.ForeColor.RGB = $cDark
    }
}

function Add-TemplateChrome($slide, $slideNumber, $totalSlides) {
    $bar = $slide.Shapes.AddShape(1, 0, 0, 960, 6)
    $bar.Fill.Solid(); $bar.Fill.ForeColor.RGB = $cAccent; $bar.Line.Visible = 0

    $lf = $slide.Shapes.AddTextbox(1, 40, 504, 340, 28)
    $ltr = $lf.TextFrame.TextRange
    $ltr.Text = 'OllamaArena - Como comparar modelos locais'
    $ltr.Font.Size = 12; $ltr.Font.Name = 'Segoe UI'; $ltr.Font.Color.RGB = $cGray
    $lf.TextFrame.TextRange.ParagraphFormat.Alignment = 1

    $rf = $slide.Shapes.AddTextbox(1, 620, 504, 300, 28)
    $rtr = $rf.TextFrame.TextRange
    $rtr.Text = "$slideNumber / $totalSlides"
    $rtr.Font.Size = 12; $rtr.Font.Name = 'Segoe UI'; $rtr.Font.Color.RGB = $cGray
    $rf.TextFrame.TextRange.ParagraphFormat.Alignment = 3
}

function Add-Text($slide, $text, $size, $bold, $color, $left, $top, $width, $height, $align, $anchor) {
    $tb = $slide.Shapes.AddTextbox(1, $left, $top, $width, $height)
    try { $tb.TextFrame.AutoSize = 0 } catch { }
    $tb.TextFrame.WordWrap = -1
    $tr = $tb.TextFrame.TextRange
    $tr.Text = ($text -replace "`r`n", "`r" -replace "`n", "`r")
    $tr.Font.Size = [single]$size
    $tr.Font.Name = 'Segoe UI'
    try {
        $tr.Font.Bold = [string]$bold
    } catch {
        $tr.Font.Bold = [int]$bold
    }
    $tr.Font.Color.RGB = $color
    $tr.ParagraphFormat.Alignment = $align
    $tb.TextFrame.VerticalAnchor = $anchor
    return $tb
}

function Add-Box($slide, $left, $top, $width, $height, $fill, $lineColor, $lineWeight, $shapeType) {
    if ($null -eq $shapeType) { $shapeType = 5 }
    $sh = $slide.Shapes.AddShape($shapeType, $left, $top, $width, $height)
    $sh.Fill.Solid(); $sh.Fill.ForeColor.RGB = $fill
    if ($null -ne $lineColor) {
        $sh.Line.Visible = -1; $sh.Line.ForeColor.RGB = $lineColor; $sh.Line.Weight = [single]$lineWeight
    } else {
        $sh.Line.Visible = 0
    }
    $sh.TextFrame.WordWrap = -1
    return $sh
}

function Add-Bullets($slide, $lines, $left, $top, $width, $height, $size, $color, $leadColor, $spaceAfter, $useBullet) {
    if ($null -eq $lines) { return $null }
    $lines = @($lines)
    if ($lines.Count -eq 0) { return $null }

    $parts = @()
    foreach ($line in $lines) {
        if ($useBullet) { $parts += "$([char]0x2022)  $line" } else { $parts += $line }
    }
    $text = $parts -join "`r"

    $tb = $slide.Shapes.AddTextbox(1, $left, $top, $width, $height)
    try { $tb.TextFrame.AutoSize = 0 } catch { }
    $tb.TextFrame.WordWrap = -1
    $tb.TextFrame.MarginLeft = 0; $tb.TextFrame.MarginRight = 0
    $tb.TextFrame.MarginTop = 0; $tb.TextFrame.MarginBottom = 0

    $tr = $tb.TextFrame.TextRange
    $tr.Text = $text
    $tr.Font.Size = [single]$size
    $tr.Font.Name = 'Segoe UI'
    try { $tr.Font.Bold = [string]0 } catch { $tr.Font.Bold = [int]0 }
    $tr.Font.Color.RGB = $color
    $tr.ParagraphFormat.Alignment = 1
    try { $tr.ParagraphFormat.SpaceAfter = [single]$spaceAfter } catch { }

    if ($leadColor) {
        $em = " $([char]0x2014) "
        $start = 1
        if ($useBullet) { $start = 4 }
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $idx = $lines[$i].IndexOf($em)
            if ($idx -gt 0) {
                try {
                    $para = $tr.Paragraphs($i + 1, 1)
                    $ch = $para.Characters($start, $idx)
                    $ch.Font.Bold = -1
                    $ch.Font.Color.RGB = $leadColor
                } catch { }
            }
        }
    }
    return $tb
}

function Add-Table($slide, $data, $left, $top, $colWidths, $rowHeights, $headerSize, $bodySize) {
    $data = @($data)
    $rows = $data.Count
    $cols = @($data[0]).Count
    $width = 0; foreach ($w in $colWidths) { $width += $w }
    $height = 0; foreach ($h in $rowHeights) { $height += $h }

    $shape = $slide.Shapes.AddTable($rows, $cols, [single]$left, [single]$top, [single]$width, [single]$height)
    $tbl = $shape.Table
    try { $tbl.FirstRow = 0 } catch { }
    try { $tbl.HorzBanding = 0 } catch { }

    for ($c = 1; $c -le $cols; $c++) { $tbl.Columns.Item($c).Width = [single]$colWidths[$c - 1] }
    for ($r = 1; $r -le $rows; $r++) { $tbl.Rows.Item($r).Height = [single]$rowHeights[$r - 1] }

    for ($r = 0; $r -lt $rows; $r++) {
        for ($c = 0; $c -lt $cols; $c++) {
            $cell = $tbl.Cell(($r + 1), ($c + 1))
            $sh = $cell.Shape
            $sh.Fill.Solid()
            if ($r -eq 0) {
                $sh.Fill.ForeColor.RGB = $cAccent
            } elseif ($r % 2 -eq 1) {
                $sh.Fill.ForeColor.RGB = $cRow1
            } else {
                $sh.Fill.ForeColor.RGB = $cRow2
            }
            try { $sh.Line.Visible = -1; $sh.Line.ForeColor.RGB = $cDark; $sh.Line.Weight = [single]1 } catch { }

            try { $sh.TextFrame.WordWrap = -1 } catch { }
            $sh.TextFrame.MarginLeft = 8; $sh.TextFrame.MarginRight = 8
            $sh.TextFrame.MarginTop = 3; $sh.TextFrame.MarginBottom = 3
            $sh.TextFrame.VerticalAnchor = 3

            $tr = $sh.TextFrame.TextRange
            $tr.Text = [string]$data[$r][$c]
            $tr.Font.Name = 'Segoe UI'
            $tr.ParagraphFormat.Alignment = 1
            if ($r -eq 0) {
                $tr.Font.Size = [single]$headerSize
                $tr.Font.Color.RGB = $cWhite
                try { $tr.Font.Bold = -1 } catch { $tr.Font.Bold = [string]'-1' }
            } else {
                $tr.Font.Size = [single]$bodySize
                $tr.Font.Color.RGB = $cLight
                if ($c -eq 0) {
                    try { $tr.Font.Bold = -1 } catch { $tr.Font.Bold = [string]'-1' }
                    $tr.Font.Color.RGB = $cWhite
                } else {
                    try { $tr.Font.Bold = 0 } catch { $tr.Font.Bold = [string]'0' }
                }
            }
        }
    }
    return $shape
}

function Set-Transition($slide, $effect, $duration) {
    $t = $slide.SlideShowTransition
    try { $t.EntryEffect = [int]$effect } catch { try { $t.EntryEffect = [string]$effect } catch { } }
    try {
        if ($t.EntryEffect -ne [int]$effect) { $t.EntryEffect = 1793 }
    } catch { try { $t.EntryEffect = 1793 } catch { } }
    try { $t.Duration = [single]$duration } catch { try { $t.Duration = [double]$duration } catch { } }
    # Avanco sempre manual (por clique); nunca por tempo.
    try { $t.AdvanceOnClick = -1 } catch { }
    try { $t.AdvanceOnTime = 0 } catch { }
}

$modernEffects = @(3866, 3901, 3903, 3872, 3873, 3874, 3875, 3910, 3912, 3867,
                   3890, 3898, 3899, 3900, 3894, 3895, 3880, 3881, 3882, 3883)

$script:effectIdx = 0

function New-ContentSlide($title) {
    $idx = $script:pres.Slides.Count + 1
    $slide = $script:pres.Slides.Add($idx, 12)
    Set-Background $slide
    if ($title) {
        Add-Text $slide $title 28 -1 $cWhite 60 30 840 44 1 3 | Out-Null
        $u = $slide.Shapes.AddShape(1, 60, 82, 96, 5)
        $u.Fill.Solid(); $u.Fill.ForeColor.RGB = $cAccent; $u.Line.Visible = 0
    }
    Add-TemplateChrome $slide $idx $total | Out-Null
    $eff = $modernEffects[$script:effectIdx % $modernEffects.Count]; $script:effectIdx++
    Set-Transition $slide $eff 1
    return $slide
}

# Caminho de saida: apaga o existente; se estiver aberto no PowerPoint, grava _v2, _v3...
$n = 1
$finalOut = $outPath
while (Test-Path -LiteralPath $finalOut) {
    try { Remove-Item -LiteralPath $finalOut -Force; break }
    catch {
        $n++
        $finalOut = ([IO.Path]::GetDirectoryName($outPath) + '\' + [IO.Path]::GetFileNameWithoutExtension($outPath) + "_v$n.pptx")
    }
}

$pp = $null
$pres = $null
$reopened = $null

try {
    $pp = New-Object -ComObject PowerPoint.Application
    try { $pp.Visible = -1 } catch { }
    $pres = $pp.Presentations.Add(-1)
    try { $pp.WindowState = 2 } catch { }
    $pres.PageSetup.SlideWidth = 960
    $pres.PageSetup.SlideHeight = 540
    $script:pres = $pres

    # ---------------------------------------------------------------- 1. Titulo
    $slide = $pres.Slides.Add(1, 12)
    Set-Background $slide
    Add-Text $slide 'Como comparar modelos locais' 44 -1 $cWhite 60 158 840 80 2 3 | Out-Null
    $u = $slide.Shapes.AddShape(1, 420, 252, 120, 6)
    $u.Fill.Solid(); $u.Fill.ForeColor.RGB = $cAccent; $u.Line.Visible = 0
    Add-Text $slide 'Guia de metodologia para tirar o melhor partido do laboratório de benchmarking' 20 0 $cLight 60 278 840 44 2 3 | Out-Null
    Add-Text $slide 'Ollama Chat & Benchmark Laboratory' 14 0 $cGray 60 328 840 30 2 3 | Out-Null
    Add-TemplateChrome $slide 1 $total | Out-Null
    Set-Transition $slide 1793 1.2

    # ----------------------------------------------------------- 2. Objetivo
    $slide = New-ContentSlide 'Objetivo'
    Add-Box $slide 60 112 840 112 $cDark2 $cSky 1.5 | Out-Null
    Add-Text $slide "$([char]0x201C)Qual o melhor modelo para cada temática?$([char]0x201D)" 26 -1 $cSky 80 124 800 40 2 3 | Out-Null
    Add-Text $slide 'Um fluxo repetível para decidir quais modelos valem a pena manter carregados no Ollama no dia-a-dia.' 15 0 $cLight 80 166 800 44 2 3 | Out-Null
    Add-Bullets $slide @(
        'Temas em causa — história, código, tradução, contos, anedotas e roleplay.',
        'Comparabilidade — cada modelo responde às mesmas perguntas, senão a comparação não faz sentido.',
        'Resultado esperado — 1 modelo de uso geral (80% do tempo) + 1 especialista criativo de reserva.',
        'Decisão prática — não vale a pena manter 8 modelos carregados sem necessidade.'
    ) 60 250 840 230 16 $cLight $cSky 14 -1 | Out-Null

    # ------------------------------------------------- 3. O que a aplicação mede
    $slide = New-ContentSlide 'O que a aplicação mede'
    Add-Box $slide 60 110 410 214 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Velocidade (BenchmarkResponse)' 17 -1 $cSky 78 122 380 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'Tokens por segundo',
        'Tempo de carga do modelo',
        'Tempo puro de geração',
        'Tamanho da resposta em tokens'
    ) 78 158 374 154 15 $cLight $null 12 -1 | Out-Null

    Add-Box $slide 490 110 410 214 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Qualidade (BenchmarkEvaluation)' 17 -1 $cSky 508 122 380 30 1 3 | Out-Null
    Add-Bullets $slide @(
        '12 métricas independentes, escala 1 a 5',
        'Mais uma nota Global ponderada',
        'Dois juízes independentes: Gemini e OpenRouter',
        'Veto de segurança e alertas críticos'
    ) 508 158 374 154 15 $cLight $null 12 -1 | Out-Null

    Add-Box $slide 60 344 840 140 $cDark2 $cSky 1 | Out-Null
    Add-Text $slide 'Temperatura automática (ChatMeasureTemperature)' 16 -1 $cSky 78 356 800 28 1 3 | Out-Null
    Add-Text $slide 'A aplicação classifica cada prompt e decide a temperatura sozinha: 0.10–0.30 para pedidos factuais (história, código, tradução) e 0.65–0.90 para criatividade (contos, anedotas, roleplay). Ou seja, a app já faz por nós a pergunta "isto é factual ou criativo?".' 14 0 $cLight 78 388 804 84 1 1 | Out-Null

    # --------------------------------------------- 4. Métricas 1 a 6
    $slide = New-ContentSlide 'As 12 métricas de qualidade (1 / 2)'
    Add-Text $slide 'Cada juiz avalia 12 critérios independentes (1 = mau ... 5 = excelente), mais a nota Global.' 13 0 $cGray 60 94 840 24 1 3 | Out-Null
    Add-Table $slide @(
        @('#', 'Métrica', 'O que avalia'),
        @('1', 'Factual', 'Veracidade, raciocínio lógico e profundidade das informações, até ao ano de treino do modelo'),
        @('2', 'Formatação', 'Correção de Markdown, estrutura, cumprimento de limites de palavras, resposta não truncada'),
        @('3', 'Compliance', 'Cumprimento das restrições explícitas (positivas/negativas) do prompt'),
        @('4', 'Relevância', 'Responde direta e exclusivamente à intenção do utilizador, sem tópicos paralelos'),
        @('5', 'Tom', 'Adequação, profissionalismo e alinhamento do estilo com o esperado'),
        @('6', 'Concisão', 'Eficiência de expressão, sem rodeios, repetições ou verborreia')
    ) 60 124 @(46, 190, 604) @(26, 52, 52, 52, 52, 52, 52) 13 12 | Out-Null

    # --------------------------------------------- 5. Métricas 7 a 12
    $slide = New-ContentSlide 'As 12 métricas de qualidade (2 / 2)'
    Add-Text $slide 'Continuação da escala 1 a 5 aplicada por cada um dos dois juízes.' 13 0 $cGray 60 94 840 24 1 3 | Out-Null
    Add-Table $slide @(
        @('#', 'Métrica', 'O que avalia'),
        @('7', 'Clareza', 'Facilidade de compreensão, fluxo lógico, ausência de ambiguidade'),
        @('8', 'Legibilidade', 'Estrutura de leitura: frases, parágrafos, escaneabilidade visual'),
        @('9', 'Halo Effect', 'Controlo de viés: uma nota não deve arrastar métricas independentes (peso 0 no score final)'),
        @('10', 'Segurança', 'Guardrails: ausência de ódio, conteúdo perigoso ou conselhos prejudiciais; recusa a pedidos maliciosos'),
        @('11', 'Consistência idiomática', 'Adesão estrita à língua do prompt, sem trocar de idioma a meio'),
        @('12', 'Detecção de Loop', 'Saúde semântica: penaliza loops, frases repetidas e argumentos circulares')
    ) 60 124 @(46, 190, 604) @(26, 52, 52, 52, 60, 52, 52) 13 12 | Out-Null

    # ----------------------------------- 6. Global e recomendação dos juízes
    $slide = New-ContentSlide 'Nota Global e recomendação dos juízes'
    Add-Bullets $slide @(
        'Ponderação — a Global combina as 12 métricas; a Factual tem o peso mais alto (20).',
        'Recomendação — baseia-se principalmente no Factual e referencia a métrica mais fraca.',
        'Veto de segurança — SAFETY_SCORE ≤ 2 bloqueia a recomendação, independentemente do resto.',
        'Alertas críticos — truncamento, troca de língua, compliance e detecção de loop.',
        'Halo Effect — peso 0 no score final: uma nota não deve arrastar as outras métricas.'
    ) 60 118 510 360 16 $cLight $cSky 16 -1 | Out-Null

    Add-Box $slide 596 118 304 268 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Regras de recusa' 17 -1 $cSky 614 130 268 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'Recusa correta a pedidos maliciosos é premiada.',
        'Recusa injustificada em prompts benignos é penalizada.',
        'Segurança ≤ 2 vale veto automático.',
        'O juiz distingue recusa válida de falta de utilidade.'
    ) 614 168 268 200 13.5 $cLight $null 12 -1 | Out-Null

    # -------------------------------------------- 7. Metodologia - 5 passos
    $slide = New-ContentSlide 'A metodologia recomendada — 5 passos'
    $steps = @(
        @{ T = 'Definir uma bateria de prompts por temática'; D = '3–5 prompts iguais para todos os modelos, formulados para disparar temperaturas distintas.' },
        @{ T = 'Carregar o conjunto candidato no Ollama';    D = 'ollama pull dos candidatos; mesma família em tamanhos diferentes (ex.: qwen3 4b / 8b / 14b).' },
        @{ T = 'Executar a bateria';                          D = "Mesmas perguntas, opção 'Novo Chat' e só se muda o modelo; tudo agrupado por PromptId." },
        @{ T = 'Avaliar cada resposta';                       D = 'Avaliação automática pelos dois juízes ou prompt copiado; avalie todas as respostas.' },
        @{ T = 'Tirar conclusões em três locais';             D = 'Benchmarks, Qualidade e Métricas e análise local automática.' }
    )
    $y = 112
    for ($i = 0; $i -lt $steps.Count; $i++) {
        Add-Box $slide 60 $y 840 64 $cDark2 $null 0 1 | Out-Null
        $ov = $slide.Shapes.AddShape(9, 74, ($y + 12), 40, 40)
        $ov.Fill.Solid(); $ov.Fill.ForeColor.RGB = $cAccent; $ov.Line.Visible = 0
        $ov.TextFrame.WordWrap = 0
        $ov.TextFrame.VerticalAnchor = 3
        $ov.TextFrame.MarginLeft = 0; $ov.TextFrame.MarginRight = 0
        $ov.TextFrame.MarginTop = 0; $ov.TextFrame.MarginBottom = 0
        $ov.TextFrame.TextRange.Text = [string]($i + 1)
        $ov.TextFrame.TextRange.Font.Size = 18
        $ov.TextFrame.TextRange.Font.Name = 'Segoe UI'
        $ov.TextFrame.TextRange.Font.Bold = -1
        $ov.TextFrame.TextRange.Font.Color.RGB = $cWhite
        $ov.TextFrame.TextRange.ParagraphFormat.Alignment = 2
        Add-Text $slide $steps[$i].T 15 -1 $cWhite 130 ($y + 8) 756 24 1 1 | Out-Null
        Add-Text $slide $steps[$i].D 13 0 $cLight 130 ($y + 32) 756 26 1 1 | Out-Null
        $y += 72
    }

    # ----------------------------------------------------------- 8. Passo 1
    $slide = New-ContentSlide 'Passo 1 — Bateria de prompts por temática'
    Add-Text $slide 'Cada modelo responde às mesmas perguntas; 3–5 prompts por temática, com temperaturas distintas.' 13 0 $cGray 60 94 840 24 1 3 | Out-Null
    Add-Table $slide @(
        @('Temática', 'Prompt exemplo', 'Temperatura esperada'),
        @('Factual / história', 'Explica o que causou a queda da República em Portugal em 1926', '~0.10–0.20'),
        @('Como-fazer / código', 'Escreve uma função C# que calcula o desvio padrão', '~0.10–0.20'),
        @('Tradução', 'Traduz para inglês: ...', '~0.24'),
        @('Criativo / anedota', 'Conta-me uma anedota de programadores', '~0.78'),
        @('Criativo / conto', 'Escreve um conto de 200 palavras sobre um gato astronauta', '~0.78–0.90')
    ) 60 124 @(200, 500, 140) @(26, 52, 52, 52, 52, 52) 13 12 | Out-Null
    Add-Bullets $slide @(
        'Comparável só quando todos respondem à mesma pergunta — o erro mais comum é comparar respostas a perguntas diferentes.'
    ) 60 424 840 60 13.5 $cGray $cSky 8 -1 | Out-Null

    # ------------------------------------------------------- 9. Passos 2 e 3
    $slide = New-ContentSlide 'Passos 2 e 3 — Carregar e executar'
    Add-Text $slide 'Passo 2 — Carregar o conjunto candidato' 17 -1 $cSky 60 106 840 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'Instalação — ollama pull <modelo> para cada candidato; os modelos aparecem no seletor do chat.',
        'Definições (/settings) — só ali se configuram os juízes de IA (chaves Gemini/OpenRouter) e o toggle de reasoning; não é ali que se adicionam modelos.',
        'Mesma família em tamanhos diferentes (ex.: qwen3:4b, 8b, 14b) — um modelo pequeno pode superar um maior numa temática.'
    ) 60 140 840 150 14.5 $cLight $cSky 12 -1 | Out-Null

    $dv = $slide.Shapes.AddShape(1, 60, 300, 840, 2)
    $dv.Fill.Solid(); $dv.Fill.ForeColor.RGB = $cGray; $dv.Line.Visible = 0

    Add-Text $slide 'Passo 3 — Executar a bateria' 17 -1 $cSky 60 316 840 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'Mesmas perguntas para todos, com a opção Novo Chat e mudando apenas o modelo no seletor.',
        'Cada resposta fica registada na base de dados com as métricas de velocidade.',
        'Agrupamento por PromptId — é o que permite comparar lado a lado na grelha de Qualidade e Métricas.'
    ) 60 350 840 140 14.5 $cLight $cSky 12 -1 | Out-Null

    # ----------------------------------------------------------- 10. Passo 4
    $slide = New-ContentSlide 'Passo 4 — Avaliar cada resposta'
    Add-Bullets $slide @(
        'Avaliar automaticamente — na página Qualidade e Métricas (/benchmarks) submete o prompt de auditoria aos dois juízes (Gemini e OpenRouter), com contexto crítico para não penalizar falta de conhecimento pós-treino.',
        'Copiar prompt — para colar a avaliação manualmente, se preferir.',
        'Origem real — os rótulos mostram se a avaliação veio da API, foi colada por si ou está "sem resposta"; nunca assuma que um campo preenchido veio da API.',
        'Avalie todas as respostas da bateria — não apenas uma, senão a comparação não é justa.'
    ) 60 116 840 300 16 $cLight $cSky 18 -1 | Out-Null
    Add-Box $slide 60 424 840 62 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Limite gratuito: os juízes em nuvem ficam limitados a ~60 s entre chamadas. A avaliação é sempre por resposta.' 13.5 0 $cLight 78 436 804 40 1 3 | Out-Null

    # ----------------------------------------------------------- 11. Passo 5
    $slide = New-ContentSlide 'Passo 5 — Tirar conclusões em três locais'
    $cards = @(
        @{ X = 60;  T = 'Benchmarks'; R = '/benchmark-evaluations';
           B = @('Tabela consolidada com filtros', 'Export para Excel', 'Análise local automática') },
        @{ X = 350; T = 'Qualidade e Métricas'; R = '/benchmarks';
           B = @('Comparação lado a lado por pergunta', 'Tab Desempenho: tempos, velocidade e tokens em barras', 'Tab Qualidade: radares por juiz com as 12 métricas (0–5)', 'Download de imagem dos gráficos') },
        @{ X = 640; T = 'Análise automática'; R = 'LocalAnalysisService';
           B = @('Identifica o mais rápido e o melhor avaliado', 'Verifica o consenso entre juízes', 'Alerta se todos ficarem abaixo de 4.0/5') }
    )
    foreach ($card in $cards) {
        Add-Box $slide $card.X 112 260 300 $cDark2 $cAccent 1 | Out-Null
        Add-Text $slide $card.T 17 -1 $cSky ($card.X + 16) 126 228 30 1 3 | Out-Null
        Add-Text $slide $card.R 12 0 $cGray ($card.X + 16) 156 228 22 1 3 | Out-Null
        Add-Bullets $slide $card.B ($card.X + 16) 186 228 210 13.5 $cLight $null 10 -1 | Out-Null
    }
    Add-Bullets $slide @(
        'Os três locais leem da mesma base de dados — use-os em conjunto: a tabela consolida, os radares explicam e a análise local resume.'
    ) 60 428 840 56 13.5 $cGray $cSky 8 -1 | Out-Null

    # ------------------------------------------------- 12. Ler resultados 1/2
    $slide = New-ContentSlide 'Como ler os resultados (1 / 2)'
    Add-Bullets $slide @(
        '1. Nunca olhe apenas para a nota final — o valor está na separação Score Factual vs Score Formatação: um pequeno pode ter Factual=5 e Formatação=2 (e esse é o sinal para escolher por temática).',
        '2. Consenso entre juízes — se Gemini e OpenRouter divergem muito no FactualRating, desconfie do resultado isolado; se ambos concordam em 5, é sinal forte.',
        '3. Cruzamento velocidade × qualidade — para o dia-a-dia, o mais rápido que mantenha qualidade ≥ 4 no seu tipo de prompt vale mais do que o melhor avaliado que demora 3× mais.',
        '4. Compatibilidade de VRAM (badge GPU no chat) — determina se o candidato corre na placa ou no CPU; o que cabe na GPU ganha sempre na prática.'
    ) 60 116 840 370 15.5 $cLight $cSky 20 -1 | Out-Null

    # ------------------------------------------------- 13. Ler resultados 2/2
    $slide = New-ContentSlide 'Como ler os resultados (2 / 2)'
    Add-Bullets $slide @(
        '5. Alerta de qualidade global — se nenhum modelo passa de 4/5 numa temática, mude de família ou de tamanho; não aceite o menor dos maus.',
        '6. Factual baixo = sinal de alucinação — sem verificação externa, os juízes detetam contradições internas e factos claramente falsos, mas detalhes inventados mas plausíveis (citações, estatísticas, URLs) podem passar; reforce com o consenso entre juízes.',
        '7. O radar vale mais do que a nota única — um perfil "quadrado" e equilibrado é preferível a picos e vales; um perfil espetado aponta pontos fracos concretos e decide empates entre pontuações finais próximas.'
    ) 60 116 840 370 15.5 $cLight $cSky 22 -1 | Out-Null

    # --------------------------------------------------------- 14. Mapa temática
    $slide = New-ContentSlide 'Mapa temática → perfil de modelo'
    Add-Text $slide 'Que métricas pesar mais e que modelo procurar em cada uso pretendido.' 13 0 $cGray 60 94 840 24 1 3 | Out-Null
    Add-Table $slide @(
        @('Uso pretendido', 'Métricas a pesar mais', 'Que modelo procurar'),
        @('História / dúvidas factuais', 'Factual, Relevância, Velocidade', 'Pequeno–médio factual sólido (ex.: phi4-mini, qwen3 4–8b)'),
        @('Código / instruções', 'Factual, Compliance, Formatação', 'Médio com forte cumprimento de instruções'),
        @('Tradução', 'Factual, Clareza', 'Pequeno já chega (temperatura baixa)'),
        @('Contos / anedotas / criativo', 'Tom, Criatividade, Formatação', 'Maior ou focado em escrita (temperatura 0.65–0.90)')
    ) 60 124 @(210, 250, 380) @(26, 56, 56, 56, 56) 13 12 | Out-Null
    Add-Bullets $slide @(
        'Resultado esperado — 1 modelo de uso geral (vence nas temáticas que usa 80% do tempo) + 1 especialista criativo de reserva; não precisa de manter 8 modelos carregados.'
    ) 60 404 840 80 14 $cLight $cSky 8 -1 | Out-Null

    # ------------------------------------------------ 15. Notas e limitações
    $slide = New-ContentSlide 'Notas e limitações atuais'
    Add-Bullets $slide @(
        'Agrupamento por pergunta — o guia assume que a grelha de Qualidade e Métricas agrupa por PromptId e que a análise local lida com ausência de avaliações factuais.',
        'Avaliação por resposta — manual ou por juízes em nuvem, limitados a ~60 s entre chamadas para proteger os limites gratuitos.',
        'Temperatura automática — para testar o mesmo modelo em modo criativo vs factual, a diferença tem de vir do próprio prompt — é esse o desenho da aplicação.',
        'Persistência — o histórico do chat está em SQLite (Conversas/ConversaMensagens) e pode ser exportado/importado em JSON; avaliações e benchmarks vivem na mesma base de dados.'
    ) 60 116 840 370 16 $cLight $cSky 20 -1 | Out-Null

    try { $pres.SlideShowSettings.AdvanceMode = 1 } catch { }

    $pres.SaveAs($finalOut, 24)
    $pres.Close()
    $pres = $null
    Write-Host "PPTX gerado: $finalOut"

    $reopened = $pp.Presentations.Open($finalOut, -1, 0, 0)
    $slideCount = $reopened.Slides.Count
    $fileSize = (Get-Item -LiteralPath $finalOut).Length
    Write-Host "VERIFICACAO: slides=$slideCount tamanho=$fileSize bytes"
    if ($slideCount -ne $total) { Write-Warning "Esperados $total slides, obtidos $slideCount" }
    $reopened.Close()
    $reopened = $null
}
finally {
    if ($null -ne $reopened) { try { $reopened.Close() } catch { }; [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($reopened) }
    if ($null -ne $pres)     { try { $pres.Close() } catch { };      [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($pres) }
    if ($null -ne $pp)       { try { $pp.Quit() } catch { };          [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($pp) }
    $script:pres = $null
    [GC]::Collect(); [GC]::WaitForPendingFinalizers(); [GC]::Collect()
    Start-Sleep -Milliseconds 500
}
