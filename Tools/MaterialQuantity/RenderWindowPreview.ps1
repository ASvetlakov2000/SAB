param(
    [ValidateSet('normal', 'minimum')]
    [string]$Size = 'normal'
)

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase
Add-Type -TypeDefinition @'
public sealed class PreviewMaterialRule
{
    public string Name { get; set; }
    public string CurrentRule { get; set; }
    public string Rule { get; set; }
    public string Result { get; set; }
}
'@

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
$moduleRoot = Join-Path $repositoryRoot 'SAB\Cls_MaterialQuantity'
$windowPath = Join-Path $moduleRoot 'MaterialRulesWindow.xaml'
$stylePath = Join-Path $repositoryRoot 'SAB\UI\Styles\SABWindowStyles.xaml'
$markup = Get-Content -LiteralPath $windowPath -Raw
$markup = [regex]::Replace($markup, '\s+x:Class="[^"]+"', '')
$markup = [regex]::Replace($markup, '\s+Click="[^"]+"', '')
$styleUri = ([System.Uri]$stylePath).AbsoluteUri
$markup = $markup.Replace('/SAB;component/UI/Styles/SABWindowStyles.xaml', $styleUri)
$markup = $markup.Replace('../UI/Styles/SABWindowStyles.xaml', $styleUri)

$window = [System.Windows.Markup.XamlReader]::Parse($markup)
$rows = [System.Collections.ObjectModel.ObservableCollection[PreviewMaterialRule]]::new()
$rows.Add([PreviewMaterialRule]@{ Name = 'Утеплитель минераловатный'; CurrentRule = 'м³ · ADSK'; Rule = 'м³'; Result = 'м³' })
$rows.Add([PreviewMaterialRule]@{ Name = 'Гидроизоляция рулонная'; CurrentRule = 'Не задано'; Rule = 'м²'; Result = 'м²' })
$rows.Add([PreviewMaterialRule]@{ Name = 'Штукатурка гипсовая'; CurrentRule = 'м² · ADSK'; Rule = 'Авто (ADSK)'; Result = 'м² · ADSK' })
$rows.Add([PreviewMaterialRule]@{ Name = 'Профиль направляющий'; CurrentRule = 'Не задано'; Rule = 'м'; Result = 'м' })
$window.DataContext = [pscustomobject]@{
    VisibleRows = $rows
    Search = ''
    FilterChoices = @('Все материалы', 'Без единицы', 'Изменённые')
    Filter = 'Все материалы'
    BulkChoices = @('м', 'м²', 'м³', 'Не учитывать', 'Авто (ADSK)')
    BulkRule = 'м²'
    UnitChoices = @('Авто (ADSK)', 'м', 'м²', 'м³', 'Не учитывать')
    Message = 'Выделите строки в таблице.'
    Status = 'Показано: 4 из 4  |  Изменено: 1  |  Без единицы: 0'
}

if ($Size -eq 'minimum') { $width = 920; $height = 560 }
else { $width = 1100; $height = 700 }
$window.Width = $width
$window.Height = $height
$window.MinWidth = $width
$window.MinHeight = $height
$window.WindowStyle = [System.Windows.WindowStyle]::None
$window.ShowInTaskbar = $false
$window.Left = -3000
$window.Top = -3000
$window.Show()
$window.UpdateLayout()

$bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($window.Content)
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$outputPath = Join-Path $moduleRoot "preview_$Size.png"
$stream = [System.IO.File]::Create($outputPath)
try { $encoder.Save($stream) }
finally { $stream.Dispose(); $window.Close() }
Write-Output $outputPath
