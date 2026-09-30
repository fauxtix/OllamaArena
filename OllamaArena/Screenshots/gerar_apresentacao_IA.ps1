$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Caminhos derivados da localizacao do script (usa apenas a sub-pasta AI_Generated)
$screenshotsDir = $PSScriptRoot
$aiDir         = Join-Path $screenshotsDir 'AI_Generated'
$outPath       = Join-Path $screenshotsDir 'OllamaArena_IA.pptx'

function New-Rgb([int]$r, [int]$g, [int]$b) { return (($b -shl 16) -bor ($g -shl 8) -bor $r) }

# Tema Computing / editor de codigo
$cBG1     = New-Rgb 13  17  23   # 0D1117
$cBG2     = New-Rgb 22  27  34   # 161B22
$cAccent  = New-Rgb   0 229 255   # 00E5FF (ciano)
$cGreen   = New-Rgb  63 185  80   # 3FB950
$cPurple  = New-Rgb 188 140 255   # BC8CFF
$cOrange  = New-Rgb 210 153  34   # D29922
$cCode    = New-Rgb  88 166 255   # 58A6FF
$cWhite   = New-Rgb 240 246 252
$cGray    = New-Rgb 139 148 158   # 8B949E

$files = Get-ChildItem -LiteralPath $aiDir -Filter 'OllamaArena-*.png' | Sort-Object { [int][regex]::Match($_.BaseName, '(\d+)$').Groups[1].Value }
$files = @($files)
$total = 1 + $files.Count + 1   # capa + imagens + fecho
Write-Host "Imagens AI_Generated encontradas: $($files.Count) | Total de slides: $total"

try {
    if (Test-Path -LiteralPath $outPath) { Remove-Item -LiteralPath $outPath -Force }
} catch {
    $base = Join-Path $screenshotsDir 'OllamaArena_IA'
    $n = 2
    while (Test-Path -LiteralPath ($base + '_v' + $n + '.pptx')) { $n++ }
    $outPath = $base + '_v' + $n + '.pptx'
    Write-Host "Anterior em uso; a gerar novo ficheiro: $outPath"
}

function Set-Background($slide, $cTop, $cBottom) {
    $slide.FollowMasterBackground = 0
    $bg = $slide.Background
    try {
        $bg.Fill.Visible = -1
        $bg.Fill.TwoColorGradient(5, 2)
        $bg.Fill.ForeColor.RGB = $cTop
        $bg.Fill.BackColor.RGB = $cBottom
    } catch {
        $bg.Fill.Solid()
        $bg.Fill.ForeColor.RGB = $cTop
    }
}

function Add-Text($slide, $text, $size, $bold, $color, $left, $top, $width, $height, $align, $anchor, $fontName = 'Segoe UI') {
    $tb = $slide.Shapes.AddTextbox(1, $left, $top, $width, $height)
    $tr = $tb.TextFrame.TextRange
    $tr.Text = $text
    $tr.Font.Size = $size
    $tr.Font.Name = $fontName
    try { $tr.Font.Bold = [string]$bold } catch { $tr.Font.Bold = [int]$bold }
    $tr.Font.Color.RGB = $color
    $tr.ParagraphFormat.Alignment = $align
    if ($anchor -gt 0) {
        try { $tb.TextFrame.VerticalAnchor = [int]$anchor } catch {
            try { $tb.TextFrame2.VerticalAnchor = [int]$anchor } catch { }
        }
    }
}

function Add-Bar($slide, $color, $left, $top, $width, $height, $opacity) {
    $bar = $slide.Shapes.AddShape(1, $left, $top, $width, $height)
    $bar.Fill.Solid(); $bar.Fill.ForeColor.RGB = $color; $bar.Line.Visible = 0
    try { $bar.Fill.Transparency = $opacity } catch { }
    [void]$bar
    return $null
}

function Add-CenteredImage($slide, $path, $areaLeft, $areaTop, $areaW, $areaH) {
    $img = [System.Drawing.Image]::FromFile($path)
    try {
        $iw = $img.Width; $ih = $img.Height
        $scale = [Math]::Min($areaW / $iw, $areaH / $ih)
        $w = $iw * $scale; $h = $ih * $scale
        $left = $areaLeft + ($areaW - $w) / 2
        $top  = $areaTop  + ($areaH - $h) / 2
        $null = $slide.Shapes.AddPicture($path, 0, -1, $left, $top, $w, $h)
    } finally {
        $img.Dispose()
    }
}

function Add-Chrome($slide, $slideNumber, $totalSlides) {
    Add-Bar $slide $cAccent 0 0 960 5 0
    $lf = $slide.Shapes.AddTextbox(1, 40, 504, 380, 28)
    $ltr = $lf.TextFrame.TextRange
    $ltr.Text = 'Ollama Arena · IA Generated'
    $ltr.Font.Size = 10; $ltr.Font.Name = 'Segoe UI'; $ltr.Font.Color.RGB = $cGray
    $lf.TextFrame.TextRange.ParagraphFormat.Alignment = 1
    $rf = $slide.Shapes.AddTextbox(1, 620, 504, 300, 28)
    $rtr = $rf.TextFrame.TextRange
    $rtr.Text = ('{0} / {1}' -f $slideNumber, $totalSlides)
    $rtr.Font.Size = 10; $rtr.Font.Name = 'Segoe UI'; $rtr.Font.Color.RGB = $cGray
    $rf.TextFrame.TextRange.ParagraphFormat.Alignment = 3
}

function Set-Transition($slide, $effect, $duration) {
    $t = $slide.SlideShowTransition
    try { $t.EntryEffect = [int]$effect } catch { }
    try { $t.Duration = [single]$duration } catch { }
    try { $t.AdvanceOnClick = -1 } catch { }
    try { $t.AdvanceOnTime = 0 } catch { }
}

$modernEffects = @(3866, 3901, 3903, 3872, 3873, 3874, 3875, 3910, 3912, 3867,
                   3890, 3898, 3899, 3900, 3894, 3895, 3880, 3881, 3882, 3883,
                   3884, 3885, 3886, 3887, 3888, 3914, 3915, 3916, 3917, 3905,
                   3906, 3907, 3908, 3918, 3919, 3920, 3921, 3926, 3927, 3928,
                   3929, 3930, 3931, 3932, 3933)

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

    # ---------------- SLIDE 1: CAPA ----------------
    $slide = $pres.Slides.Add(1, 12)
    Set-Background $slide $cBG1 $cBG2
    Add-Bar $slide $cAccent 0 0 960 6 0
    Add-Bar $slide $cAccent 0 0 6 540 0

    Add-Text $slide 'LABORATÓRIO LOCAL DE CHAT E BENCHMARK DE IA' 13 -1 $cCode 160 150 640 30 2 2
    Add-Text $slide 'Ollama Arena' 64 -1 $cWhite 160 185 640 90 2 2
    Add-Bar $slide $cAccent 380 290 200 8 0
    Add-Text $slide 'Imagens geradas por IA · o fluxo da aplicação em nove visuais' 17 0 $cCode 160 320 640 35 2 2
    Add-Text $slide 'Tema Computing · apresentação com avanço manual' 12 0 $cGray 160 365 640 30 2 2

    Add-Chrome $slide 1 $total
    Set-Transition $slide 1793 1.2

    # ---------------- SLIDES DE IMAGEM (ordem pelos digitos finais) ----------------
    $effectIdx = 0
    foreach ($f in $files) {
        $idx = $pres.Slides.Count + 1
        $slide = $pres.Slides.Add($idx, 12)
        Set-Background $slide $cBG1 $cBG2

        # imagem a ocupar todo o slide (apenas do AI_Generated)
        Add-CenteredImage $slide $f.FullName 0 0 960 540

        $eff = $modernEffects[$effectIdx % $modernEffects.Count]; $effectIdx++
        Set-Transition $slide $eff 1
    }

    # ---------------- SLIDE FINAL: FECHO ----------------
    $slide = $pres.Slides.Add($pres.Slides.Count + 1, 12)
    Set-Background $slide $cBG1 $cBG2
    Add-Bar $slide $cGreen 0 0 960 6 0
    Add-Text $slide '// fim da execucao' 16 0 $cOrange 180 190 600 30 2 2 'Cascadia Code'
    Add-Text $slide 'OBRIGADO' 54 -1 $cWhite 180 230 600 80 2 2
    Add-Bar $slide $cAccent 380 330 200 6 0
    Add-Text $slide 'pip install ollama --quiet && dotnet run' 13 0 $cGreen 180 370 600 30 2 2 'Cascadia Code'
    Add-Chrome $slide $pres.Slides.Count $total
    Set-Transition $slide 3910 1.2

    try { $pres.SlideShowSettings.AdvanceMode = 1 } catch { }

    $pres.SaveAs($outPath, 24)
    $pres.Close()
    $pres = $null
    Write-Host "PPTX gerado: $outPath"

    $reopened = $pp.Presentations.Open($outPath, -1, 0, 0)
    $slideCount = $reopened.Slides.Count
    $fileSize = (Get-Item -LiteralPath $outPath).Length
    Write-Host "VERIFICACAO: slides=$slideCount tamanho=$fileSize bytes"
    $reopened.Close()
    $reopened = $null
}
finally {
    if ($null -ne $reopened) { try { $reopened.Close() } catch { }; [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($reopened) }
    if ($null -ne $pres)     { try { $pres.Close() } catch { };      [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($pres) }
    if ($null -ne $pp)       { try { $pp.Quit() } catch { };          [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($pp) }
    [GC]::Collect(); [GC]::WaitForPendingFinalizers(); [GC]::Collect()
    Start-Sleep -Milliseconds 500
}