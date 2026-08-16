$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Paths derivados da localizacao do script (funciona em qualquer clone)
$screenshotsDir = $PSScriptRoot
$projectRoot    = Split-Path -Parent $PSScriptRoot
$imagesDir      = Join-Path $projectRoot 'wwwroot\images'
$outPath        = Join-Path $screenshotsDir 'OllamaArena.pptx'

function New-Rgb([int]$r, [int]$g, [int]$b) { return (($b -shl 16) -bor ($g -shl 8) -bor $r) }

$cDark    = New-Rgb  11  30  61   # 0B1E3D
$cDark2   = New-Rgb  20  47  92   # 142F5C
$cAccent  = New-Rgb   0 120 212   # 0078D4
$cWhite   = New-Rgb 255 255 255
$cLight   = New-Rgb 201 213 234   # C9D5EA
$cGray    = New-Rgb 140 158 184   # 8C9EB8

$files = Get-ChildItem -LiteralPath $screenshotsDir -Filter *.png | Sort-Object { [regex]::Replace($_.BaseName, '(\d+)', { param($m) $m.Groups[1].Value.PadLeft(12, '0') }) }
$files = @($files)
$total = 1 + 2 + $files.Count
Write-Host "Screenshots encontrados: $($files.Count) | Total de slides: $total"

if (Test-Path -LiteralPath $outPath) { Remove-Item -LiteralPath $outPath -Force }

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

    $lf = $slide.Shapes.AddTextbox(1, 40, 504, 300, 28)
    $ltr = $lf.TextFrame.TextRange
    $ltr.Text = 'OllamaArena'
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
    $tr = $tb.TextFrame.TextRange
    $tr.Text = $text
    $tr.Font.Size = $size
    $tr.Font.Name = 'Segoe UI'
    try {
        $tr.Font.Bold = [string]$bold
    } catch {
        $tr.Font.Bold = [int]$bold
    }
    $tr.Font.Color.RGB = $color
    $tr.ParagraphFormat.Alignment = $align
    $tb.TextFrame.VerticalAnchor = $anchor
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

function Set-Transition($slide, $effect, $duration, $advanceSeconds) {
    $t = $slide.SlideShowTransition
    try { $t.EntryEffect = [int]$effect } catch { try { $t.EntryEffect = [string]$effect } catch { } }
    try {
        if ($t.EntryEffect -ne [int]$effect) { $t.EntryEffect = 1793 }
    } catch { try { $t.EntryEffect = 1793 } catch { } }
    try { $t.Duration = [single]$duration } catch { try { $t.Duration = [double]$duration } catch { } }
    try { $t.AdvanceOnClick = -1 } catch { }
    try { $t.AdvanceOnTime = -1 } catch { }
    try { $t.AdvanceTime = [single]$advanceSeconds } catch { try { $t.AdvanceTime = [double]$advanceSeconds } catch { } }
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

    $slide = $pres.Slides.Add(1, 12)
    Set-Background $slide
    Add-Text $slide 'OllamaArena - Screenshots' 48 -1 $cWhite 60 165 840 90 2 3
    $u = $slide.Shapes.AddShape(1, 420, 262, 120, 6)
    $u.Fill.Solid(); $u.Fill.ForeColor.RGB = $cAccent; $u.Line.Visible = 0
    Add-Text $slide "Apresentacao da aplicacao - $($files.Count) capturas de ecra" 20 0 $cLight 60 290 840 60 2 3
    Add-TemplateChrome $slide 1 $total
    Set-Transition $slide 1793 1.2 10

    $introMeta = @(
        @{ Path = (Join-Path $imagesDir 'OllamaWithFluentUI.jpg'); Title = 'Ollama com FluentUI'; Subtitle = 'Interface de conversacao moderna com a interface Fluent' },
        @{ Path = (Join-Path $imagesDir 'OllamaAsAJudge.png');    Title = 'Ollama como Juiz';     Subtitle = 'Benchmark e avaliacao automatica de respostas' }
    )
    foreach ($meta in $introMeta) {
        $idx = $pres.Slides.Count + 1
        $slide = $pres.Slides.Add($idx, 12)
        Set-Background $slide
        Add-Text $slide $meta.Title 36 -1 $cWhite 60 35 840 60 2 3
        Add-Text $slide $meta.Subtitle 16 0 $cLight 60 100 840 40 2 3
        Add-CenteredImage $slide $meta.Path 60 150 840 330
        Add-TemplateChrome $slide $idx $total
        Set-Transition $slide 1793 1.2 10
    }

    $effectIdx = 0
    foreach ($f in $files) {
        $idx = $pres.Slides.Count + 1
        $slide = $pres.Slides.Add($idx, 12)
        Set-Background $slide
        Add-CenteredImage $slide $f.FullName 50 55 860 410
        $caption = $f.BaseName -replace '^\d+_', ''
        $caption = $caption -replace '_', ' '
        $caption = [regex]::Replace($caption, '([a-z0-9])([A-Z])', '$1 $2')
        $caption = ($caption -replace '\s+', ' ').Trim()
        Add-Text $slide $caption 20 0 $cLight 40 470 880 60 2 3
        Add-TemplateChrome $slide $idx $total
        $eff = $modernEffects[$effectIdx % $modernEffects.Count]; $effectIdx++
        Set-Transition $slide $eff 1 10
    }

    try { $pres.SlideShowSettings.AdvanceMode = 2 } catch { }

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
