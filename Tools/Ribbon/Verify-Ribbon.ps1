param([string]$AssemblyPath = 'SAB\bin\Revit2023\SAB.dll')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$assemblyFile = [IO.Path]::GetFullPath((Join-Path $root $AssemblyPath))
$evidence = Join-Path $root 'Docs\Ui\DesignSystem\screenshots-ribbon-modules'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
[Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Revit 2023\RevitAPIUI.dll') | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom($assemblyFile)
$ribbon = $assembly.GetType('SAB.Helpers.Ribbon', $true)
$parameters = $assembly.GetType('SAB.ParameterTools.ParameterToolsIcons', $true)
$publicStatic = [Reflection.BindingFlags]'Public,Static'
$internalStatic = [Reflection.BindingFlags]'NonPublic,Static'
$loader = $ribbon.GetMethod('GetEmbeddedImage', $publicStatic)
$parameterFactory = $parameters.GetMethod('Create', $internalStatic)
$materialFactory = $ribbon.GetMethod('GetMaterialQuantityImage', $internalStatic)
$decorationFactory = $ribbon.GetMethod('GetElevationDecorationImage', $internalStatic)
$checks = [Collections.Generic.List[string]]::new()
$tiles = [Collections.Generic.List[object]]::new()

function Assert-Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $checks.Add("PASS $message")
}
function Pixels($bitmap) {
    $converted = [Windows.Media.Imaging.FormatConvertedBitmap]::new(
        $bitmap, [Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $bytes = [byte[]]::new($converted.PixelWidth * $converted.PixelHeight * 4)
    $converted.CopyPixels($bytes, $converted.PixelWidth * 4, 0)
    return ,$bytes
}
function Render-Icon($icon, [int]$size) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()
    $dc.DrawImage($icon, [Windows.Rect]::new(0, 0, $size, $size))
    $dc.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(
        $size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    return $bitmap
}

$groups = @(
    @{ Color = @(0,0,0); Names = @('OpenPluginInstructionsHtmlCommand','SyncReminderSettings','Notifications') },
    @{ Color = @(128,128,128); Names = @('ExportTypesSingleFileCommand') },
    @{ Color = @(92,109,187); Names = @('CreateViewsAndSheets','DeleteViewsAndSheets','EditViewTemplateGraphics','FilledRegionCreate','FilledRegionSettings') },
    @{ Color = @(246,169,80); Names = @('MaterialDataSheet','MaterialLayers') },
    @{ Color = @(134,162,147); Names = @('ParameterIndex','ParameterSearch','ParameterSearchReset') }
)
foreach ($group in $groups) {
    foreach ($name in $group.Names) {
        foreach ($size in @(16,32)) {
            $resource = "SAB.Resources.${name}_${size}.png"
            $icon = $loader.Invoke($null, @($resource, $false))
            Assert-Check ($null -ne $icon) "$resource exists"
            $originalStream = $assembly.GetManifestResourceStream($resource)
            try {
                $decoder = [Windows.Media.Imaging.PngBitmapDecoder]::new($originalStream,
                    [Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
                    [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
                $original = $decoder.Frames[0]
            } finally { $originalStream.Dispose() }
            Assert-Check ($icon.PixelWidth -eq $original.PixelWidth -and $icon.PixelHeight -eq $original.PixelHeight -and $icon.DpiX -eq 96 -and $icon.DpiY -eq 96) "$resource dimensions and96DPI"
            $before = Pixels $original
            $after = Pixels $icon
            $valid = $true
            $cutouts = 0; $opaqueBody = 0
            for ($i = 0; $i -lt $after.Length; $i += 4) {
                $expectedAlpha = $before[$i+3]
                if ($name -eq 'ExportTypesSingleFileCommand') {
                    $maxChannel = [Math]::Max($before[$i], [Math]::Max($before[$i+1], $before[$i+2]))
                    $coverage = [Math]::Min(140, 255-$maxChannel)
                    $expectedAlpha = [int][Math]::Floor(($before[$i+3]*$coverage+70)/140)
                    if ($maxChannel -ge 200 -and $before[$i+3] -eq 255 -and $after[$i+3] -lt 128) { $cutouts++ }
                    if ($before[$i+3] -eq 255 -and $after[$i+3] -eq 255) { $opaqueBody++ }
                }
                if ($after[$i+3] -ne $expectedAlpha) { $valid = $false; break }
                if ($after[$i+3] -gt 0 -and ($after[$i] -ne $group.Color[2] -or $after[$i+1] -ne $group.Color[1] -or $after[$i+2] -ne $group.Color[0])) { $valid = $false; break }
            }
            Assert-Check $valid "$resource exact RGB and alpha contract (library light detail knockout)"
            if ($name -eq 'ExportTypesSingleFileCommand') {
                Assert-Check ($cutouts -gt 0 -and $opaqueBody -gt 0) "$resource visible interior cutouts and opaque body retained"
            }
            if ($size -eq 32) { $tiles.Add(@{Label=$name;Image=$icon;SmallImage=$loader.Invoke($null, @("SAB.Resources.${name}_16.png", $false))}) }
        }
    }
}
foreach ($name in @('CreateInteriorElevationsCommand','FlipElevation180ByLineCommand','MoveElevationViewportsToNewSheetCommand','CreateDoorWindowViews','GklFrameWall')) {
    foreach ($size in @(16,32)) {
        $resource = "SAB.Resources.${name}_${size}.png"
        $icon = $loader.Invoke($null, @($resource, $false))
        $stream = $assembly.GetManifestResourceStream($resource)
        try {
            $original = [Windows.Media.Imaging.PngBitmapDecoder]::new($stream, 0, [Windows.Media.Imaging.BitmapCacheOption]::OnLoad).Frames[0]
        } finally { $stream.Dispose() }
        $before = Pixels $original
        $after = Pixels $icon
        Assert-Check ([Convert]::ToBase64String($before) -eq [Convert]::ToBase64String($after)) "$resource unchanged pixels"
        if ($size -eq 32) { $tiles.Add(@{Label=$name;Image=$icon;SmallImage=$loader.Invoke($null, @("SAB.Resources.${name}_16.png", $false))}) }
    }
}
foreach ($size in @(16,32,48)) {
    foreach ($name in @('Apply','Settings','Check','Clear','Preview','Corpus','Material','Decoration')) {
        $sourceSize = if ($size -eq 48) { 32 } else { $size }
        $icon = if ($name -eq 'Material') { $materialFactory.Invoke($null, @($sourceSize)) } elseif ($name -eq 'Decoration') { $decorationFactory.Invoke($null, @($sourceSize)) } else { $parameterFactory.Invoke($null, @($name, $sourceSize)) }
        $expected = if ($name -eq 'Material') { @(246,169,80) } elseif ($name -eq 'Decoration') { @(251,152,152) } else { @(134,162,147) }
        $bitmap = Render-Icon $icon $size
        $bytes = Pixels $bitmap
        $valid = $true; $visible = 0
        for ($i = 0; $i -lt $bytes.Length; $i += 4) {
            if ($bytes[$i+3] -lt 100) { continue }
            $visible++
            for ($channel = 0; $channel -lt 3; $channel++) {
                if ([Math]::Abs([int]$bytes[$i+$channel] - $expected[2-$channel]) -gt 4) { $valid = $false }
            }
        }
        Assert-Check ($valid -and $visible -gt 0 -and $icon.IsFrozen) "$name composite at${size}DIP one module color (premultiply tolerance4), frozen"
        if ($size -eq 32) {
            $smallIcon = if ($name -eq 'Material') { $materialFactory.Invoke($null, @(16)) } elseif ($name -eq 'Decoration') { $decorationFactory.Invoke($null, @(16)) } else { $parameterFactory.Invoke($null, @($name, 16)) }
            $tiles.Add(@{Label="Composite $name";Image=$icon;SmallImage=$smallIcon})
        }
    }
}
$gray = $loader.Invoke($null, @('SAB.Resources.SyncReminderSettings_32.png', $true))
$grayPixels = Pixels $gray
Assert-Check ($grayPixels[0] -eq 112 -and $grayPixels[1] -eq 112 -and $grayPixels[2] -eq 112) 'Hidden Frame gray override retained'

# This is an asset contact sheet from actual production factories, not a Revit ribbon screenshot.
$columns = 5; $cellWidth = 250; $cellHeight = 100
$height = 60 + [int][Math]::Ceiling($tiles.Count / [double]$columns) * $cellHeight
$visual = [Windows.Media.DrawingVisual]::new(); $dc = $visual.RenderOpen()
$dc.DrawRectangle([Windows.Media.Brushes]::White, $null, [Windows.Rect]::new(0,0,$columns*$cellWidth,$height))
$culture = [Globalization.CultureInfo]::InvariantCulture
$typeface = [Windows.Media.Typeface]::new('Segoe UI')
$title = [Windows.Media.FormattedText]::new('SAB production assets | actual16/32 +32scaled48DIP | outside Revit', $culture, [Windows.FlowDirection]::LeftToRight, $typeface, 18, [Windows.Media.Brushes]::Black, 1.0)
$dc.DrawText($title, [Windows.Point]::new(16,12))
for ($i = 0; $i -lt $tiles.Count; $i++) {
    $x = ($i % $columns) * $cellWidth; $y = 60 + [Math]::Floor($i / $columns) * $cellHeight
    $dc.DrawRectangle([Windows.Media.Brushes]::WhiteSmoke, $null, [Windows.Rect]::new($x+4,$y+4,$cellWidth-8,$cellHeight-8))
    foreach ($size in @(16,32,48)) {
        $offset = if ($size -eq 16) { 18 } elseif ($size -eq 32) { 60 } else { 116 }
        $tileImage = if ($size -eq 16) { $tiles[$i].SmallImage } else { $tiles[$i].Image }
        $dc.DrawImage($tileImage, [Windows.Rect]::new($x+$offset,$y+8,$size,$size))
    }
    $label = [Windows.Media.FormattedText]::new($tiles[$i].Label, $culture, [Windows.FlowDirection]::LeftToRight, $typeface, 11, [Windows.Media.Brushes]::Black, 1.0)
    $label.MaxTextWidth = $cellWidth-16
    $dc.DrawText($label, [Windows.Point]::new($x+8,$y+65))
}
$dc.Close()
$sheet = [Windows.Media.Imaging.RenderTargetBitmap]::new($columns*$cellWidth,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$sheet.Render($visual)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($sheet))
$file = [IO.File]::Create((Join-Path $evidence 'production-icons.png'))
try { $encoder.Save($file) } finally { $file.Dispose() }
$checks.Add("COMPLETED $($checks.Count) checks; actual production assets, Revit ribbon NOT RUN")
$checks | Set-Content -Encoding UTF8 (Join-Path $evidence 'verification.txt')
@{ assembly=$assemblyFile; sha256=(Get-FileHash $assemblyFile).Hash; renderDpi=96; scope='Actual production icon factories only, outside Revit'; nativeRevit='NOT RUN'; screenshot='production-icons.png' } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $evidence 'provenance.json')
$checks[-1]
