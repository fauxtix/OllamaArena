$ErrorActionPreference = 'Stop'

# Gera o deck "OllamaArena - Funcionalidades" a partir da documentacao tecnica
# OllamaArena/Docs/Funcionalidades.md (mesmo template visual dos decks de Docs/).
# Requer Windows com PowerPoint 2016+ (automacao COM). Regenerar apos editar:
#   powershell -ExecutionPolicy Bypass -File gerar_funcionalidades.ps1

$docsDir = $PSScriptRoot
$srcPath = Join-Path $docsDir 'Funcionalidades.md'
$outPath = Join-Path $docsDir 'Funcionalidades.pptx'

if (-not (Test-Path -LiteralPath $srcPath)) { throw "Documentacao nao encontrada: $srcPath" }

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

$total = 25

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
    $ltr.Text = 'OllamaArena - Funcionalidades'
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
            $idxHyphen = $lines[$i].IndexOf(' - ')
            if (($idxHyphen -gt 0) -and (($idx -lt 0) -or ($idxHyphen -lt $idx))) { $idx = $idxHyphen }
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

function Add-Cards($slide, $cards) {
    foreach ($card in $cards) {
        Add-Box $slide $card.X 112 260 310 $cDark2 $cAccent 1 | Out-Null
        Add-Text $slide $card.T 16 -1 $cSky ($card.X + 16) 126 228 30 1 3 | Out-Null
        Add-Text $slide $card.R 11.5 0 $cGray ($card.X + 16) 156 228 22 1 3 | Out-Null
        Add-Bullets $slide $card.B ($card.X + 16) 186 228 220 13 $cLight $null 10 -1 | Out-Null
    }
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

    # ------------------------------------------------------------- 1. Titulo
    $slide = $pres.Slides.Add(1, 12)
    Set-Background $slide
    Add-Text $slide 'OllamaArena - Funcionalidades' 44 -1 $cWhite 60 150 840 80 2 3 | Out-Null
    $u = $slide.Shapes.AddShape(1, 420, 244, 120, 6)
    $u.Fill.Solid(); $u.Fill.ForeColor.RGB = $cAccent; $u.Line.Visible = 0
    Add-Text $slide 'Chat local com Ollama e laboratorio de benchmarks "LLM-as-a-judge"' 20 0 $cLight 60 270 840 44 2 3 | Out-Null
    Add-Text $slide 'Documentacao tecnica - Blazor Server (.NET 10) | FluentUI | SQLite | Serilog' 14 0 $cGray 60 320 840 30 2 3 | Out-Null
    Add-TemplateChrome $slide 1 $total | Out-Null
    Set-Transition $slide 1793 1.2

    # ------------------------------------------------------- 2. Visao geral
    $slide = New-ContentSlide 'Visao geral'
    Add-Bullets $slide @(
        'Chat local - conversa com modelos Ollama em streaming NDJSON, com barra de contexto e reasoning opcional.',
        'Benchmarks - cada troca e gravada como resposta de benchmark, com metricas de velocidade automaticas.',
        'LLM-as-a-judge - a qualidade e avaliada por dois juizes (Gemini e OpenRouter) mais analise local em C#.',
        'UI - FluentUI Blazor v4 (DataGrid, Dialog, Splitter, NavMenu) com tema escuro/claro e idioma PT/EN.',
        'Dados - SQLite via Dapper (schema criado de forma lazily + migracao idempotente) e Serilog (tabela Logs).',
        'Ollama - comunicacao direta por HTTP (streaming em /api/chat), sem wrappers de terceiros.'
    ) 60 120 840 350 17 $cLight $cSky 20 -1 | Out-Null

    # ------------------------------------------------- 3. Stack e arquitetura
    $slide = New-ContentSlide 'Stack e arquitetura'
    Add-Table $slide @(
        @('Camada', 'Tecnologia'),
        @('Frontend/Backend', 'Blazor Server (.NET 10), Razor Components (Interactive Server)'),
        @('UI', 'Microsoft FluentUI.AspNetCore.Components v4.14.2'),
        @('Dados', 'Dapper + Microsoft.Data.Sqlite (ollama_benchmark.db)'),
        @('Logging', 'Serilog - Console + sink SQLite (tabela "Logs")'),
        @('LLM', 'Ollama local - /api/chat, /api/tags, /api/show, /api/ps'),
        @('Outros', 'ClosedXML (Excel), Markdig (markdown), Chart.js via JS interop')
    ) 60 116 @(210, 630) @(26, 40, 40, 40, 40, 40, 40) 13 12.5 | Out-Null
    Add-Box $slide 60 404 840 76 $cDark2 $cSky 1 | Out-Null
    Add-Text $slide 'Build: dotnet build OllamaArena.slnx - app + projeto de testes (xUnit). A Ollama fala-se por HTTP direto (cliente nomeado "Ollama"); o pacote OllamaSharp foi removido como dependencia morta.' 13 0 $cLight 78 418 804 52 1 1 | Out-Null

    # -------------------------------------------- 4. Organizacao do projeto
    $slide = New-ContentSlide 'Organizacao do projeto'
    Add-Bullets $slide @(
        'Components/Pages/*.razor - paginas com rota; Components/Pages/Components - componentes reutilizaveis.',
        'Components/Layout - MainLayout, NavMenu (drawer em mobile) e ReconnectModal.',
        'Services/ - ChatComposerService, AutomatedJudgeService, TranslationService, repositorios e helpers.',
        'Models/DTO (shapes da API) e Models/Entities (mapeamento SQLite via Dapper).',
        'Prompts/*.txt - prompts editaveis em runtime; PromptTemplates/ - construtores de prompt em C#.',
        'PlanoDeTestes/ e ComoCompararModelos/ - documentos PT+EN das paginas de leitura.',
        'wwwroot/fonts/ - TTF embebidos no relatorio PDF; Screenshots/ - capturas e decks .pptx gerados.'
    ) 60 118 840 360 16 $cLight $cSky 16 -1 | Out-Null

    # -------------------------------------------------------- 5. Navegacao
    $slide = New-ContentSlide 'Navegacao - menu lateral'
    Add-Table $slide @(
        @('Item', 'Rota', 'Funcionalidade'),
        @('Home', '/', 'Landing page + cache de modelos + preview do README'),
        @('Chat', '/chat', 'Streaming, barra de contexto e benchmark automatico'),
        @('Benchmarks', '/benchmark-evaluations', 'Grelha consolidada + export Excel + analise local'),
        @('Qualidade e Metricas', '/benchmarks', 'Split-view por prompt, graficos e avaliacao por juiz'),
        @('Dashboard', '/dashboard', 'Ranking agregado por modelo (score de 12 metricas)'),
        @('Modelo Carregados', '/modelos-ollama', 'Grelha de modelos locais com compatibilidade GPU'),
        @('Prompts', '/edit-prompts', 'Editor runtime dos ficheiros Prompts/*.txt'),
        @('Logs do sistema', '/system-logs', 'Visualizacao, filtro e apagamento dos logs Serilog'),
        @('Definicoes', '/settings', 'Chaves dos juizes, modelo OpenRouter e reasoning')
    ) 60 112 @(200, 230, 410) @(24, 30, 30, 30, 30, 30, 30, 30, 30, 30) 12 11 | Out-Null
    Add-Text $slide 'Responsividade: a partir de 768px o NavMenu vira drawer deslizante com overlay; Definicoes vive no menu de rodape.' 12.5 0 $cGray 60 444 840 40 1 1 | Out-Null

    # --------------------------------------- 6. Cabeecalho e paginas leitura
    $slide = New-ContentSlide 'Appbar e paginas de leitura'
    Add-Box $slide 60 112 410 240 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Appbar - icone + legenda' 16 -1 $cSky 78 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'README, Testes e Comparar com legenda sempre visivel.',
        'Comutador de tema (escuro/claro) e seletor de idioma PT/EN.',
        '3 niveis de responsividade: >=1100px titulo + legendas; 769-1100px so legendas; <=768px so icones.',
        'Acessibilidade: title e aria-label mantem o texto longo.'
    ) 78 160 374 180 13 $cLight $null 10 -1 | Out-Null

    Add-Box $slide 490 112 410 240 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Paginas de leitura' 16 -1 $cSky 508 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'Plano de Testes (/plano-de-testes) - 15 casos para avaliar juizes.',
        'Comparar Modelos (/comparar-modelos) - guia de comparacao justa.',
        'Ambas PT/EN, apenas leitura (Markdown via Markdig).',
        'Botoes no cabecalho, nao no menu lateral.'
    ) 508 160 374 180 13 $cLight $null 10 -1 | Out-Null

    Add-Box $slide 60 372 840 84 $cDark2 $cSky 1 | Out-Null
    Add-Text $slide 'As paginas de leitura resolvem o ficheiro pelo idioma da UI (cookie PT/EN) e interceptam as ancoras internas com scroll suave via wwwroot/js/markdown-anchors.js.' 13.5 0 $cLight 78 392 804 48 1 1 | Out-Null

    # ------------------------------------------------------------- 7. Home
    $slide = New-ContentSlide 'Home (/)'
    Add-Bullets $slide @(
        'Proposito - pagina de entrada: verifica se o Ollama esta ativo e pre-carrega a cache de modelos.',
        'Health check - OllamaChecker; se a base Ollama:BaseUrl nao responder, mostra FluentMessageBar de erro.',
        'Cache - limpa o localStorage uma vez por sessao e popula ollamaModels e ollamaModelsFullV2.',
        'Metadados - GET /api/tags + POST /api/show (contexto nativo, ano de treino, capacidade de reasoning).',
        'Botao Readme - descarrega o README do GitHub e renderiza; sem internet, dialogo de erro.',
        'Outputs - cards de acesso rapido, preview do README e barra de erro quando o Ollama esta em baixo.'
    ) 60 120 840 350 16 $cLight $cSky 18 -1 | Out-Null

    # --------------------------------------------------- 8. Chat - fluxo
    $slide = New-ContentSlide 'Chat (/chat) - fluxo principal'
    $steps = @(
        @{ T = 'Persistir a mensagem';            D = 'Cria a conversa (Conversas) e grava a mensagem (ConversaMensagens); titulo = primeiros 60 caracteres.' },
        @{ T = 'Calcular o contexto';             D = 'Contexto num_ctx efetivo automatico (VRAM / KV cache) e estimativa de tokens do novo prompt.' },
        @{ T = 'Montar o payload';                D = 'System prompt, historico aparado a 60%, temperatura, num_predict limitado pelo contexto e think.' },
        @{ T = 'Streaming NDJSON';                D = 'POST /api/chat - 1 linha por token; reasoning (reasoning_content/thinking) capturado a parte.' },
        @{ T = 'Concluir e gravar';               D = 'Metricas finais, benchmark automatico na BD, SUMMARY como descricao e auto-save sem dialogos.' }
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

    # ---------------------------------------- 9. Chat - UI e reasoning
    $slide = New-ContentSlide 'Chat - interface e reasoning'
    Add-Bullets $slide @(
        'Modelo e GPU - seletor FluentSelect e badge "GPU OK" vs "CPU Fallback" (GpuInfoDialog).',
        'Barra de contexto - "Contexto: X / Y tokens" e restantes; fica ambar a >=90% de ocupacao.',
        'Reasoning - bloco "Pensamento" colapsavel; o campo think e explicito (BD Chat:EnableReasoning, default false).',
        'Sondagem local - qwen3 e smollm3 respeitam think:false (~11-15 s -> ~0,05 s); deepseek-r1 ignora-o.',
        'Novo Chat descarrega o modelo (keep_alive=0); Historico abre o HistoryPanel (benchmarks + conversas).',
        'Cancelar durante o streaming - a mensagem termina com *(Cancelado)*.',
        'Saida - markdown formatado, tempo em tempo real, badge de temperatura e auto-save imediato.'
    ) 60 116 840 370 15.5 $cLight $cSky 16 -1 | Out-Null

    # --------------------------------------- 10. Benchmarks - Avaliacoes
    $slide = New-ContentSlide 'Benchmarks - Avaliacoes (/benchmark-evaluations)'
    Add-Bullets $slide @(
        'Proposito - visao consolidada de todas as respostas com as avaliacoes dos juizes e metricas de desempenho.',
        'Filtros - Todos / Por Avaliar / Avaliados, com pesquisa livre; grelha paginada (10 por pagina).',
        'Detalhe - duplo clique ou botao "olho" - dialogo com tabs Metricas e Feedbacks dos Juizes.',
        'Excel - ClosedXML; o workbook e gravado em %TEMP% e descarregado por HTTP (nao em base64 pelo SignalR).',
        'Analise IA Local - 100% local em C#, cancelavel, com sumario executivo e cards Mais Rapido / Melhor Avaliado.',
        'Apagar - 4 ambitos (filtrados / por avaliar / avaliados / todos) com confirmacao, ou linha individual.'
    ) 60 120 840 350 16 $cLight $cSky 18 -1 | Out-Null

    # --------------------------------------- 11. Qualidade e Metricas
    $slide = New-ContentSlide 'Qualidade e Metricas (/benchmarks)'
    Add-Bullets $slide @(
        'Split-view por prompt - cards de prompt a esquerda, respostas por modelo a direita.',
        'Cada resposta mostra Tokens/s, Eval, Load e Tokens; o texto do modelo fica colapsado por defeito.',
        'Graficos - tab Desempenho (4 barras) e tab Qualidade (2 radares por juiz, 12 metricas 0-5), com download.',
        'Avaliacao - formulario de 12 metricas (1-5) + nota Global, com traducao de feedback (OpenRouter -> Gemini -> Ollama).',
        'Avaliar automaticamente - prompt do juiz gerado, submetido a Gemini e OpenRouter, notas parseadas.',
        'FluentSplitter responsivo - em <=768px os painéis passam a vertical (JS interop de largura do viewport).'
    ) 60 120 840 350 16 $cLight $cSky 18 -1 | Out-Null

    # ------------------------------------------- 12. Avaliacao automatica
    $slide = New-ContentSlide 'Avaliacao automatica pelos juizes'
    Add-Box $slide 60 112 410 320 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'A avaliacao em 4 passos' 16 -1 $cSky 78 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        '1. Preparar prompt - /api/show + EvaluatePromptTemplate.',
        '2. Juiz Gemini - gemini-flash-latest.',
        '3. Juiz OpenRouter - openrouter/free (em paralelo).',
        '4. Validar notas - EvaluationParser.'
    ) 78 162 374 200 14 $cLight $null 14 -1 | Out-Null
    Add-Text $slide 'Um juiz pode falhar sem bloquear o outro.' 12.5 0 $cGray 78 380 374 40 1 1 | Out-Null

    Add-Box $slide 490 112 410 320 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Regras e estados' 16 -1 $cSky 508 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'Quota - intervalo minimo de 60 s, marcado so apos sucesso.',
        'Chaves - da pagina Settings (BD), com fallback em user-secrets.',
        'Internet - verificada antes de gastar quota.',
        'Falha total - dialogo de erro; a avaliacao nao abre.',
        'Falha parcial - o juiz em falta fica editavel.',
        'Progresso - barra com cronometro, 4 passos e Cancelar.'
    ) 508 162 374 250 13 $cLight $null 10 -1 | Out-Null

    Add-Box $slide 60 444 840 44 $cDark2 $cSky 1 | Out-Null
    Add-Text $slide 'O botao "Copiar prompt" mantem o fallback manual - nao precisa de internet.' 13 0 $cLight 78 454 804 26 1 3 | Out-Null

    # -------------------------------------------------------- 13. Dashboard
    $slide = New-ContentSlide 'Dashboard (/dashboard)'
    Add-Bullets $slide @(
        '6 KPI cards - Melhor Modelo, Modelos avaliados, Cobertura, Prompts testados, Mais rapido e Maior divergencia.',
        '2 radares de qualidade (um por juiz) com as 12 metricas em escala 0-5.',
        'Scatter plot - tokens/s no eixo X vs. score no eixo Y, cor por modelo.',
        'Evolucao do score ao longo do tempo - media acumulada por modelo (Chart.js).',
        'Tabela classificativa ordenavel - Score, Gemini, OpenRouter, Tokens/s, Tempo, Divergencia.',
        'Score ponderado pelo ScoreCalculator - pesos configuraveis e HaloEffect com peso 0.'
    ) 60 120 840 350 16 $cLight $cSky 18 -1 | Out-Null

    # ----------------------------------------------- 14. Modelos Ollama
    $slide = New-ContentSlide 'Modelos Carregados (/modelos-ollama)'
    Add-Bullets $slide @(
        'Grelha - Nome, Familia, Parametros, Quantizacao, Ano de Treino, Contexto Maximo e Tamanho no Disco.',
        'Badge Raciocino - Suporta / Nao suporta / Desconhecido (array capabilities do /api/show).',
        'Badge GPU - Compativel / Excede VRAM / Leitura indisponivel (fotografia da VRAM livre).',
        'Cache-first em localStorage (ollamaModelsFullV2), combinando /api/tags com /api/show.',
        'Toolbar sticky com a contagem de modelos e o botao "O que significa cada coluna?" (9 explicacoes).'
    ) 60 124 840 330 16.5 $cLight $cSky 18 -1 | Out-Null

    # ------------------------------- 15. Prompts, Logs e Definicoes
    $slide = New-ContentSlide 'Editar Prompts, Logs e Definicoes'
    Add-Cards $slide @(
        @{ X = 60;  T = 'Editar Prompts'; R = '/edit-prompts';
           B = @('Selecionar ficheiro Prompts/*.txt', 'Gravar, Cancelar e Sair', 'Aviso de alteracoes nao gravadas', 'Ativo sem recompilar a app') },
        @{ X = 350; T = 'Logs do Sistema'; R = '/system-logs';
           B = @('Logs Serilog na tabela SQLite', 'Filtros por nivel + pesquisa', 'Detalhe com mensagem e excecao', 'Limpar e apagar com confirmacao') },
        @{ X = 640; T = 'Definicoes'; R = '/settings';
           B = @('Chaves Gemini/OpenRouter (BD)', 'Modelo OpenRouter por catalogo ao vivo', 'Toggle de reasoning (think)', 'Dirty-tracking + dialogo de confirmacao') }
    )

    # ---------------------------------------- 16. Componentes partilhados
    $slide = New-ContentSlide 'Componentes partilhados'
    Add-Table $slide @(
        @('Componente', 'Uso'),
        @('PageHeader', 'Cabecalho partilhado com gradiente #38BDF8 -> #A78BFA e barra de acento'),
        @('HistoryPanel', 'Painel lateral com tabs Benchmarks e Conversas (export/import JSON)'),
        @('CultureSelector', 'Comutacao PT/EN por cookie de cultura'),
        @('BenchmarkEvaluation', 'Formulario de 12 metricas + Global, escala 1-5, com celulas fracas a vermelho'),
        @('BenchmarkEvaluationDialog', 'Formulario + traducao de feedback (OpenRouter -> Gemini -> Ollama)'),
        @('BenchmarkEvaluationDetail', 'Dialogo 900px - tabs Metricas e Feedbacks dos Juizes'),
        @('BenchmarkChartDialog', '4 barras de desempenho + 2 radares de qualidade, com download'),
        @('BenchmarkAnalysisViewer', 'Painel da analise local (vazio / streaming / concluido)'),
        @('GpuInfoDialog / ModelColumnsInfo', 'Dialogos explicativos de GPU e das colunas da grelha')
    ) 60 112 @(280, 560) @(24, 36, 36, 36, 36, 36, 36, 36, 36, 36) 12 11.5 | Out-Null

    # ------------------------------------------------- 17. Dialogos
    $slide = New-ContentSlide 'Dialogos principais'
    Add-Box $slide 60 112 410 310 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Avaliacao e traducao' 16 -1 $cSky 78 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'Detail - grelha Gemini vs OpenRouter e feedbacks lado a lado.',
        'Caixa "Recomendacao do Juiz" quando ha campo RECOMMENDATION.',
        'Metricas fracas destacadas a vermelho (<=3; <=2 em Seguranca).',
        'Traducao com cronometro, sistema em uso e cancelamento.'
    ) 78 162 374 240 13.5 $cLight $null 12 -1 | Out-Null

    Add-Box $slide 490 112 410 310 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Graficos e analise' 16 -1 $cSky 508 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'ChartDialog - tabs Desempenho e Qualidade, download de imagem.',
        'Analise IA Local - estados vazio, streaming e concluido; cancelavel.',
        'Confirmacoes antes de analisar (multiplos prompts, por avaliar).',
        'LogDetailDialog, ReadmePreview, GpuInfo e ModelColumnsInfo.'
    ) 508 162 374 240 13.5 $cLight $null 12 -1 | Out-Null

    Add-Box $slide 60 434 840 54 $cDark2 $cSky 1 | Out-Null
    Add-Text $slide 'Todas as confirmacoes de eliminacao passam pelo IDialogService (Sim, Apagar / Cancelar).' 13 0 $cLight 78 448 804 30 1 3 | Out-Null

    # ------------------------------------------- 18. Origem dos juizes
    $slide = New-ContentSlide 'Origem real da avaliacao por juiz'
    Add-Table $slide @(
        @('Valor', 'Significado'),
        @('gemini', 'Slot preenchido automaticamente pelo servico do Gemini'),
        @('openrouter', 'Slot preenchido automaticamente pelo servico do OpenRouter'),
        @('externo', 'Conteudo colado ou preenchido manualmente pelo utilizador'),
        @('(null)', 'Sem resposta - ou registo legado com conteudo mas sem origem registada')
    ) 60 116 @(160, 680) @(26, 42, 42, 42, 42) 13 12.5 | Out-Null
    Add-Bullets $slide @(
        'Os rotulos nunca mentem - uma avaliacao colada nao aparece como vinda da nuvem.',
        'Persistido em Respostas.Juiz1Origem / Juiz2Origem (migracao DatabaseSchemaInitializer).',
        'Usado em grelhas, formulario, detalhe, botoes de traducao e exportacao PDF.'
    ) 60 336 840 150 15 $cLight $cSky 14 -1 | Out-Null

    # ------------------------------------------------ 19. Servicos (1)
    $slide = New-ContentSlide 'Camada de servicos (1 / 2)'
    Add-Bullets $slide @(
        'OllamaGpuService - /api/tags, /api/ps e /api/show; compatibilidade GPU e contexto efetivo automatico.',
        'ChatComposerService - payload do chat: historico, temperatura, num_predict, num_ctx e think; extrai o SUMMARY.',
        'ScoreCalculator / JudgeScoreWeights - score ponderado das 12 metricas (HaloEffect peso 0, renormalizacao).',
        'AutomatedJudgeService - juizes Gemini e OpenRouter em paralelo, quota de 60 s e temperatura fixa em 0.',
        'TranslationService - traducao de feedbacks com cadeia OpenRouter -> Gemini -> Ollama (temperature 0).',
        'LocalAnalysisService - analise 100% local em C#, sem LLM, totalmente localizada (PT/EN).'
    ) 60 118 840 360 15.5 $cLight $cSky 16 -1 | Out-Null

    # ------------------------------------------------ 20. Servicos (2)
    $slide = New-ContentSlide 'Camada de servicos (2 / 2)'
    Add-Bullets $slide @(
        'PromptThemeClassifier - tema do prompt (Factual, Criativo, Conversacional, Codigo, Seguranca) para contextualizar o juiz.',
        'JudgeLanguageGuard - limita Language Consistency a <=2 quando a resposta esta noutra lingua.',
        'EvaluationParser - parse de *_SCORE, FINAL_SCORE, DESCRIPTION/FEEDBACK, RECOMMENDATION e REFUSAL_HANDLED.',
        'MessageFormatter (6 ficheiros partial) - pipeline de markdown: rescata fences, reconstrui tabelas, Markdig.',
        'PromptFilesService - leitura/gravacao de Prompts/*.txt com protecao anti-path-traversal.',
        'ConversationRepository - conversas em SQLite com export/import JSON; OllamaChecker e InternetConnectivityService.'
    ) 60 118 840 360 15.5 $cLight $cSky 16 -1 | Out-Null

    # ------------------------------------------- 21. Endpoints do Ollama
    $slide = New-ContentSlide 'Integracao com o Ollama - endpoints'
    Add-Table $slide @(
        @('Endpoint', 'Servico', 'Uso'),
        @('GET /api/tags', 'OllamaGpuService', 'Lista de modelos locais (Home, Modelos)'),
        @('POST /api/show', 'OllamaGpuService', 'Contexto nativo, ano de treino, capabilities'),
        @('GET /api/ps', 'OllamaGpuService', 'Modelos em memoria (VRAM) para o contexto'),
        @('POST /api/chat', 'Chat / TranslationService', 'Streaming NDJSON e traducao local'),
        @('POST /api/generate', 'Chat', 'Descarregar modelo (keep_alive=0)'),
        @('GET / (health)', 'OllamaChecker', 'Verificacao de que o Ollama esta ativo'),
        @('POST openrouter.ai/api/v1/chat/completions', 'Juizes / Traducao', 'Servico externo (HTTPS)'),
        @('POST generativelanguage.googleapis.com', 'Juizes / Traducao', 'Servico externo (HTTPS)')
    ) 60 112 @(300, 240, 300) @(24, 32, 32, 32, 32, 32, 32, 40, 40) 12 11 | Out-Null
    Add-Text $slide 'Os seis primeiros usam Ollama:BaseUrl (default http://localhost:11434); os dois do meio sao externos (HTTPS).' 12.5 0 $cGray 60 450 840 36 1 1 | Out-Null

    # ------------------------------ 22. Streaming, contexto e temperatura
    $slide = New-ContentSlide 'Streaming, contexto e temperatura'
    Add-Box $slide 60 112 410 300 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Contexto efetivo (num_ctx)' 16 -1 $cSky 78 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'VRAM livre: nvidia-smi -> WMI -> 0.',
        'Footprint: size_vram do /api/ps ou size x 1.2.',
        'KV cache por token pela arquitetura do modelo.',
        'clamp((VRAM - footprint - 350 MB - 10%) / KV; 1024..32768).',
        'Sem detecao de VRAM -> fallback seguro 2048.'
    ) 78 162 374 230 13.5 $cLight $null 12 -1 | Out-Null

    Add-Box $slide 490 112 410 300 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'Temperatura automatica (0.10-0.90)' 16 -1 $cSky 508 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'Deteta codigo, frases e termos factuais/criativos (PT+EN).',
        'Densidade de perguntas da boost factual.',
        'Faixas: 0.10 factual forte ... 0.90 criativo forte.',
        'num_predict 1800 (GPU) / 1200 (CPU), limitado pelo contexto.',
        'repeat_penalty 1.1; top_k/top_p conforme a temperatura.'
    ) 508 162 374 230 13.5 $cLight $null 12 -1 | Out-Null

    Add-Box $slide 60 424 840 64 $cDark2 $cSky 1 | Out-Null
    Add-Text $slide 'Cliente "Ollama" com timeout de 10 min; historico aparado a 60%; metricas de benchmark gravadas no fim de cada troca.' 13 0 $cLight 78 440 804 40 1 3 | Out-Null

    # ------------------------------------------------ 23. Base de dados
    $slide = New-ContentSlide 'Base de dados - SQLite (7 tabelas)'
    Add-Table $slide @(
        @('Tabela', 'Conteudo principal'),
        @('Prompts', 'Id, Descricao, TextoPrompt, DataCriacao, Temperatura'),
        @('Respostas', 'Modelo, metricas de velocidade, 12 notas + feedbacks por juiz, origem'),
        @('Conversas', 'Id, Titulo (primeiros 60 chars), NomeModelo, datas'),
        @('ConversaMensagens', 'ConversationId, Role, Content, Reasoning, Temperature, ElapsedTime'),
        @('Configuracoes', 'Chave/Valor - chaves dos juizes, modelo OpenRouter, reasoning'),
        @('ModelosJuiz', 'Tabela legada de modelos ":free" do OpenRouter (sem uso pela UI)'),
        @('Logs', 'Serilog - Timestamp, Level, Exception, RenderedMessage')
    ) 60 112 @(200, 640) @(24, 36, 36, 36, 36, 36, 36, 36) 12.5 11.5 | Out-Null
    Add-Box $slide 60 416 840 72 $cDark2 $cSky 1 | Out-Null
    Add-Text $slide 'Sem migracoes classicas - DatabaseSchemaInitializer cria as 7 tabelas e migra colunas no arranque (PRAGMA table_info), com ON DELETE CASCADE ativo. Caminho: OllamaArena/ollama_benchmark.db (gitignored).' 12.5 0 $cLight 78 428 804 52 1 1 | Out-Null

    # ------------------------------- 24. Persistencia browser e JS interop
    $slide = New-ContentSlide 'Persistencia no browser e JS interop'
    Add-Box $slide 60 112 410 310 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'localStorage / sessionStorage' 16 -1 $cSky 78 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'ollamaModels - nomes dos modelos locais.',
        'ollamaModelsFullV2 - metadados (contexto, ano, thinking).',
        'ollama_model - modelo selecionado no chat.',
        'theme - tema FluentUI; ollamaCacheCleared - flag de sessao.',
        'Juizes NAO usam localStorage - ficam em Configuracoes (BD).'
    ) 78 162 374 240 13.5 $cLight $null 12 -1 | Out-Null

    Add-Box $slide 490 112 410 310 $cDark2 $cAccent 1 | Out-Null
    Add-Text $slide 'JS interop (wwwroot/js/)' 16 -1 $cSky 508 124 374 30 1 3 | Out-Null
    Add-Bullets $slide @(
        'graficos.js - benchmarkCharts (barras/radares) e download.',
        'excel.js - download do Excel via fetch + Blob.',
        'pdf.js - relatorio PDF com fontes TTF embebidas.',
        'chat.js, clipboard.js e conversations.js - chat e JSON.',
        'markdown-anchors.js e viewport.js - ancoras e splitter.'
    ) 508 162 374 240 13.5 $cLight $null 12 -1 | Out-Null

    Add-Box $slide 60 434 840 54 $cDark2 $cSky 1 | Out-Null
    Add-Text $slide 'As definicoes dos juizes vivem na BD (tabela Configuracoes) com prioridade BD -> configuracao -> default.' 13 0 $cLight 78 448 804 30 1 3 | Out-Null

    # ------------------------------- 25. Logging, config, deploy e validacao
    $slide = New-ContentSlide 'Logging, configuracao e deploy'
    Add-Bullets $slide @(
        'Serilog - console + sink SQLite (tabela Logs), nivel minimo Warning; consulta e filtro em /system-logs.',
        'Configuracao - appsettings.json + appsettings.Local.json; chaves recomendadas na pagina Settings (BD).',
        'Localizacao - PT por defeito + EN por cookie; respostas, traducoes e analise seguem a lingua da sessao.',
        'Testes - 257 testes xUnit (OllamaArena.Tests); validacao = build + testes + execucao manual.',
        'Deploy - GitHub Actions publica e faz deploy em IIS (self-hosted), preservando a BD; smoke test HTTP 200.',
        'Runtime - "ollama serve" ativo na base Ollama:BaseUrl (default http://localhost:11434).'
    ) 60 120 840 300 16 $cLight $cSky 18 -1 | Out-Null
    Add-Box $slide 60 424 840 64 $cDark2 $cSky 1 | Out-Null
    Add-Text $slide 'Fonte desta apresentacao: OllamaArena/Docs/Funcionalidades.md - regenerar com gerar_funcionalidades.ps1.' 13.5 0 $cLight 78 440 804 40 1 3 | Out-Null

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
