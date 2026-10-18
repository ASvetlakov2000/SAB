$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$bin = Join-Path $root 'SAB\bin\Revit2023'
$output = Join-Path $root 'Docs\Ui\DesignSystem\screenshots-decoration-ribbon'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class SabDecorationNative { [DllImport("kernel32", CharSet=CharSet.Unicode)] public static extern bool SetDllDirectory(string path); }'
[SabDecorationNative]::SetDllDirectory('C:\Program Files\Autodesk\Revit 2023') | Out-Null
[Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Revit 2023\RevitAPI.dll') | Out-Null
[Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Revit 2023\RevitAPIUI.dll') | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $bin 'SAB.dll')) | Out-Null
$app = [Windows.Application]::new()
$app.ShutdownMode = [Windows.ShutdownMode]::OnExplicitShutdown
[SAB.UI.SabWindowAnimationService]::Enabled = $false
$log = [Collections.Generic.List[string]]::new()
function Check([bool]$condition,[string]$text) { if (-not $condition) { throw $text }; $log.Add("PASS $text") }
function Pump {
    $script:pumpFrame = [Windows.Threading.DispatcherFrame]::new()
    $timer = [Windows.Threading.DispatcherTimer]::new()
    $timer.Interval = [TimeSpan]::FromMilliseconds(240)
    $timer.Add_Tick({ $script:pumpFrame.Continue = $false })
    $timer.Start()
    [Windows.Threading.Dispatcher]::PushFrame($script:pumpFrame)
    $timer.Stop()
}
function Descendants($node) {
    if ($node -is [Windows.DependencyObject]) {
        $node
        foreach ($child in [Windows.LogicalTreeHelper]::GetChildren($node)) { if ($child -is [Windows.DependencyObject]) { Descendants $child } }
    }
}
function Find($window,[string]$name) { return (Descendants $window | Where-Object { $_ -is [Windows.FrameworkElement] -and $_.Name -eq $name } | Select-Object -First 1) }
function Capture($window,[string]$name) {
    $body = $window.Content
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new([int][Math]::Ceiling($body.ActualWidth),[int][Math]::Ceiling($body.ActualHeight),96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($body)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $file = [IO.File]::Create((Join-Path $output ($name+'.png')))
    try { $encoder.Save($file) } finally { $file.Dispose() }
}
try {
    $window = [SAB.InteriorElevations.Views.ElevationDecorationWindow]::new($null,$null)
    $window.ShowInTaskbar = $false
    $window.Show(); Pump
    $button = Find $window 'ConfigureCatalogButton'
    Check ($window.GetType().Assembly.Location -eq (Join-Path $bin 'SAB.dll')) 'Actual production class, no presentation substitution'
    foreach ($size in @('normal','minimum')) {
        if ($size -eq 'minimum') { $window.Width=$window.MinWidth; $window.Height=$window.MinHeight; Pump }
        Check ($button.Content -eq 'Каталог элементов' -and $button.IsVisible -and $button.IsEnabled) "$size full catalog caption and available action"
        $title = Descendants $window | Where-Object { $_ -is [Windows.Controls.TextBlock] -and $_.Text -eq 'Оформление развёрток' } | Select-Object -First 1
        $bp=$button.TranslatePoint([Windows.Point]::new(0,0),$window.Content)
        $tp=$title.TranslatePoint([Windows.Point]::new(0,0),$window.Content)
        Check ($bp.X -ge $tp.X+$title.ActualWidth+15 -and $bp.X+$button.ActualWidth -le $window.Content.ActualWidth) "$size title and catalog button bounded, gap16"
        $formatted=[Windows.Media.FormattedText]::new('Каталог элементов',[Globalization.CultureInfo]::GetCultureInfo('ru-RU'),[Windows.FlowDirection]::LeftToRight,[Windows.Media.Typeface]::new($button.FontFamily,$button.FontStyle,$button.FontWeight,$button.FontStretch),$button.FontSize,[Windows.Media.Brushes]::Black,1.0)
        Check ($button.ActualWidth -ge $formatted.Width+$button.Padding.Left+$button.Padding.Right) "$size catalog caption not clipped"
        Capture $window $size
    }
    (Find $window 'TagOffsetTextBox').Text='17,'
    $draft=$window.CaptureDraft()
    $window.Close()
    $restored=[SAB.InteriorElevations.Views.ElevationDecorationWindow]::new($null,$null)
    $restored.RestoreDraft($draft)
    Check ((Find $restored 'TagOffsetTextBox').Text -eq '17,') 'Catalog roundtrip preserves unfinished draft text'
    $script:modalWindow=$restored
    $script:modalError=$null
    $script:clickTimer=[Windows.Threading.DispatcherTimer]::new()
    $script:clickTimer.Interval=[TimeSpan]::FromMilliseconds(250)
    $script:clickTimer.Add_Tick({
        $script:clickTimer.Stop()
        try { (Find $script:modalWindow 'ConfigureCatalogButton').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)) }
        catch { $script:modalError=$_; $script:modalWindow.Close() }
    })
    $script:clickTimer.Start()
    $result=$restored.ShowDialog()
    if ($script:modalError) { throw $script:modalError }
    Check ($result -eq $false -and $restored.OpenCatalogRequested) 'Actual routed Catalog elements click requests catalog through original modal handler'
    Check ((Find $restored 'TagOffsetTextBox').Text -eq '17,') 'Catalog request retains raw draft input'
    $log.Add("COMPLETED $($log.Count) checks; actual WPF window with null Document, render96DPI; Revit/model picking/OS DPI/physical input NOT RUN")
    $log | Set-Content -Encoding UTF8 (Join-Path $output 'verification.txt')
    @{ assembly=(Join-Path $bin 'SAB.dll');sha256=(Get-FileHash (Join-Path $bin 'SAB.dll')).Hash;renderDpi=96;scope='Actual production decoration window; null Document; synthetic unfinished input; native routed click';nativeRevit='NOT RUN';captures=@('normal.png','minimum.png') } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $output 'provenance.json')
    $log[-1]
} finally { $app.Shutdown() }
