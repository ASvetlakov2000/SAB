param(
    [switch]$Minimum
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
$moduleDirectory = Join-Path $repositoryRoot "SAB\Cls_FilledRegionFromMaterial"
[System.Reflection.Assembly]::LoadFrom("C:\Program Files\Autodesk\Revit 2023\RevitAPI.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom((Join-Path $repositoryRoot "SAB\bin\Revit2023\SAB.dll")) | Out-Null

$settings = New-Object SAB.FilledRegionFromMaterial.PluginSettings
$settings.ParameterMappings.Add((New-Object SAB.FilledRegionFromMaterial.ParameterMappingSetting -Property @{
    SourceKey = "name:Модель"
    TargetSelectionKey = "Type|name:Описание"
}))
$settings.ParameterMappings.Add((New-Object SAB.FilledRegionFromMaterial.ParameterMappingSetting -Property @{
    SourceKey = "name:Комментарии"
    TargetSelectionKey = "Instance|name:Комментарии"
}))

$materialParameters = New-Object 'System.Collections.Generic.List[SAB.FilledRegionFromMaterial.ParameterOption]'
$materialParameters.Add((New-Object SAB.FilledRegionFromMaterial.ParameterOption -Property @{ Key = "name:Модель"; Name = "Модель" }))
$materialParameters.Add((New-Object SAB.FilledRegionFromMaterial.ParameterOption -Property @{ Key = "name:Описание"; Name = "Описание" }))
$materialParameters.Add((New-Object SAB.FilledRegionFromMaterial.ParameterOption -Property @{ Key = "name:Комментарии"; Name = "Комментарии" }))

$targetParameters = New-Object 'System.Collections.Generic.List[SAB.FilledRegionFromMaterial.ParameterOption]'
$targetParameters.Add((New-Object SAB.FilledRegionFromMaterial.ParameterOption -Property @{ Key = "name:Описание"; Name = "Описание"; Scope = "Type" }))
$targetParameters.Add((New-Object SAB.FilledRegionFromMaterial.ParameterOption -Property @{ Key = "name:Комментарии"; Name = "Комментарии"; Scope = "Instance" }))
$targetParameters.Add((New-Object SAB.FilledRegionFromMaterial.ParameterOption -Property @{ Key = "name:Марка типоразмера"; Name = "Марка типоразмера"; Scope = "Type" }))

$snapshot = New-Object SAB.FilledRegionFromMaterial.ParameterSnapshot -Property @{
    MaterialParameters = $materialParameters
    TargetParameters = $targetParameters
    TotalMaterials = 148
    UsedMaterials = 37
    FilledRegionTypes = 42
    FilledRegionInstances = 126
}

try {
    $window = New-Object SAB.FilledRegionFromMaterial.SettingsWindow -ArgumentList $settings, $snapshot
}
catch {
    Write-Error $_.Exception.ToString()
    throw
}
$window.ShowInTaskbar = $false
$window.ShowActivated = $false
$window.WindowStartupLocation = "Manual"
$window.Left = 40
$window.Top = 40
if ($Minimum) {
    $window.Width = $window.MinWidth
    $window.Height = $window.MinHeight
}
$window.Show()
$window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Render)
$window.UpdateLayout()
$window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
$window.UpdateLayout()

$width = [Math]::Max(1, [int][Math]::Ceiling($window.ActualWidth))
$height = [Math]::Max(1, [int][Math]::Ceiling($window.ActualHeight))
$bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($window)

$outputDirectory = Join-Path $moduleDirectory "docs"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputName = if ($Minimum) { "settings-window-preview-min.png" } else { "settings-window-preview.png" }
$outputPath = Join-Path $outputDirectory $outputName
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [System.IO.File]::Open($outputPath, [System.IO.FileMode]::Create)
try {
    $encoder.Save($stream)
}
finally {
    $stream.Dispose()
    $window.Close()
}

Write-Output $outputPath
