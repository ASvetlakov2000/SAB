$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$build = Join-Path $root 'SAB\bin\Revit2023'
foreach ($name in @('System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','Wpf.Ui.Abstractions.dll','Wpf.Ui.dll','SAB.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $build $name)) | Out-Null
}
[Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Revit 2023\RevitAPI.dll') | Out-Null
[Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Revit 2023\RevitAPIUI.dll') | Out-Null
$planVm = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([SAB.InteriorElevations.ViewModels.ElevationSettingsViewModel])
$planVm.CreateSheet = $true
$planVm.CreateRoomPlanScheme = $true
$planVm.PlaceRoomPlanSchemeOnSheet = $true
if (!$planVm.PlaceRoomPlanSchemeOnSheet -or !$planVm.CanPlaceRoomPlanSchemeOnSheet) { throw 'Plan placement could not be enabled' }
$planVm.PlaceRoomPlanSchemeOnSheet = $false
if (!$planVm.CreateRoomPlanScheme -or $planVm.PlaceRoomPlanSchemeOnSheet) { throw 'Plan creation and placement are not independent' }
$planVm.CreateRoomPlanScheme = $false
if ($planVm.CreateRoomPlanScheme -or !$planVm.PlaceViewsOnSheet) { throw 'Sheet placement forces plan creation' }
$planVm.UseExistingSheet = $true
if ($planVm.CreateRoomPlanScheme) { throw 'Existing sheet mode forces plan creation' }
$planVm.CreateRoomPlanScheme = $true
$planVm.PlaceRoomPlanSchemeOnSheet = $true
$planVm.CreateRoomPlanScheme = $false
if ($planVm.PlaceRoomPlanSchemeOnSheet) { throw 'Plan placement remains active with creation disabled' }
Write-Output 'PASS: independent plan creation and placement switches (actual ViewModel)'
$planVm.UseAutomaticSheetPlacement = $true
if (!$planVm.UseAutomaticSheetPlacement -or $planVm.UseManualSheetPlacement) { throw 'Automatic mode binding is inconsistent' }
$planVm.UseManualSheetPlacement = $true
if ($planVm.UseAutomaticSheetPlacement -or !$planVm.UseManualSheetPlacement) { throw 'Manual mode did not switch automatic mode off' }
$planVm.UseAutomaticSheetPlacement = $true
$planVm.ColumnsCountText = '7'
if ($planVm.ColumnsCountText -ne '7' -or $planVm.UseManualSheetPlacement) { throw 'Fallback row count did not stay available in automatic mode' }
Write-Output 'PASS: actual ViewModel switches automatic/manual modes and retains views per row'
$planVm.UseExistingSheet = $false
$planVm.CreateSheet = $true
$planVm.CreateRoomPlanScheme = $true
$planVm.PlaceRoomPlanSchemeOnSheet = $true
$planVm.SetRoomPlanPosition(30.5,65.25)
if (!$planVm.UseManualRoomPlanPosition -or !$planVm.CanPositionRoomPlan) { throw 'Picking plan anchor did not enable manual plan position' }
if ([double]::Parse($planVm.RoomPlanOffsetRightMmText,[Globalization.CultureInfo]::CurrentCulture) -ne 30.5) { throw 'Picked plan right offset not reflected in field' }
$planVm.RoomPlanOffsetBottomMmText = '-1'
$outSettings = $null
$outMessage = $null
if ($planVm.TryBuildSettings([ref]$outSettings,[ref]$outMessage,$false) -or $outMessage -notmatch 'Отступы план-схемы') { throw 'Negative plan offset not rejected before placement' }
$planVm.SetRoomPlanPosition(30.5,65.25)
Write-Output 'PASS: actual ViewModel fills plan offsets after click and validates negative input'
[Reflection.Assembly]::LoadFrom((Join-Path $build 'Newtonsoft.Json.dll')) | Out-Null
$storage = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([SAB.InteriorElevations.Services.Settings.ElevationSettingsStorageService])
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$saveMethod = $storage.GetType().GetMethod('ConvertFromElevationSettings',$flags)
$loadMethod = $storage.GetType().GetMethod('ConvertToElevationSettings',$flags)
foreach ($automatic in @($true,$false)) {
    $settings = New-Object SAB.InteriorElevations.Models.ElevationSettings
    $settings.SheetLayoutSettings = New-Object SAB.InteriorElevations.Models.SheetLayoutSettings
    $settings.SheetLayoutSettings.UseAutomaticPlacement = $automatic
    $settings.SheetLayoutSettings.ColumnsCount = 7
$settings.SheetLayoutSettings.UseManualRoomPlanPosition = $true
$settings.SheetLayoutSettings.RoomPlanOffsetRightMm = 30.5
$settings.SheetLayoutSettings.RoomPlanOffsetBottomMm = 65.25
    $persisted = $saveMethod.Invoke($storage,[object[]]@($settings.PSObject.BaseObject))
    $restored = $loadMethod.Invoke($storage,[object[]]@($persisted.PSObject.BaseObject))
    if ($restored.SheetLayoutSettings.UseAutomaticPlacement -ne $automatic -or $restored.SheetLayoutSettings.ColumnsCount -ne 7) { throw 'Placement settings round trip failed' }
    if (!$restored.SheetLayoutSettings.UseManualRoomPlanPosition -or $restored.SheetLayoutSettings.RoomPlanOffsetRightMm -ne 30.5 -or $restored.SheetLayoutSettings.RoomPlanOffsetBottomMm -ne 65.25) { throw 'Plan offsets round trip failed' }
    $persisted.SchemaVersion = 12
    $persisted.UseAutomaticPlacement = $false
    $legacy = $loadMethod.Invoke($storage,[object[]]@($persisted.PSObject.BaseObject))
    if ($legacy.SheetLayoutSettings.UseManualRoomPlanPosition) { throw 'Legacy settings unexpectedly enable manual plan positioning' }
    if (!$legacy.SheetLayoutSettings.UseAutomaticPlacement) { throw 'Legacy settings must default to automatic placement' }
}
Write-Output 'PASS: actual settings converter saves both modes and row count; schema 12 defaults to automatic mode'
$path = Join-Path $root 'SAB\Cls_InteriorElevations\Views\ElevationSettingsWindow.xaml'
[xml]$xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8
$events = @('Class','Loaded','Click','Checked','Unchecked','SelectionChanged','TextChanged','Closing','Closed','PreviewMouseLeftButtonDown','PreviewMouseMove','PreviewMouseLeftButtonUp','MouseLeftButtonDown','MouseDoubleClick','PreviewKeyDown','KeyDown','SizeChanged','Expanded','Collapsed')
foreach ($node in $xml.SelectNodes('//*')) {
    foreach ($attribute in @($node.Attributes)) { if ($events -contains $attribute.LocalName) { $node.RemoveAttributeNode($attribute) | Out-Null } }
}
$reader = [System.Xml.XmlReader]::Create([IO.StringReader]::new($xml.OuterXml),[System.Xml.XmlReaderSettings]::new(),([Uri]$path).AbsoluteUri)
try { $window = [Windows.Markup.XamlReader]::Load($reader) } finally { $reader.Dispose() }
$window.DataContext = [pscustomobject]@{UseAutomaticSheetPlacement=$true;UseManualSheetPlacement=$false;ColumnsCountText='3';PlaceViewsOnSheet=$true;CreateSheet=$true;UseExistingSheet=$false;CreateRoomPlanScheme=$true;PlaceRoomPlanSchemeOnSheet=$true;CanPlaceRoomPlanSchemeOnSheet=$true;CanPositionRoomPlan=$true;UseManualRoomPlanPosition=$true;RoomPlanOffsetRightMmText='30.5';RoomPlanOffsetBottomMmText='65.25';CanEditRoomPlanCreationParameters=$true;StartXmmText='-400';StartYmmText='-15';StepXmmText='15';StepYmmText='20';ViewTitleOffsetXmmText='0';ViewTitleOffsetYmmText='10';HasNamingPreview=$false;SelectionStatusText='Линии выбраны';EnableRoomObjectCategory=$true;RoomPlanOperationLabel='Создавать план-схему помещения';RoomPlanModeHint='План-схема создается отдельно. При размещении разверток под неё резервируется место на каждом листе.'}
$tabs = $window.FindName('SettingsTabControl')
if ($null -eq $tabs) {
    $tabs = $window.Content.FindName('SettingsTabControl')
}
# Find the tab control through the logical tree when the source uses a different name.
function Find-Tabs($element) {
    if ($element -is [Windows.Controls.TabControl]) { return $element }
    foreach ($child in [Windows.LogicalTreeHelper]::GetChildren($element)) {
        if ($child -is [Windows.DependencyObject]) { $found = Find-Tabs $child; if ($null -ne $found) { return $found } }
    }
}
function Find-Scroll($element) {
    if ($element -is [Windows.Controls.ScrollViewer]) { return $element }
    foreach ($child in [Windows.LogicalTreeHelper]::GetChildren($element)) {
        if ($child -is [Windows.DependencyObject]) { $found = Find-Scroll $child; if ($null -ne $found) { return $found } }
    }
}
if ($null -eq $tabs) { $tabs = Find-Tabs $window }
if ($null -eq $tabs) { throw 'Tab control was not loaded' }
$output = Join-Path $root 'outputs\auto-sheet-layout'
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($index in @(1,2)) {
    $tabs.SelectedIndex = $index
    $content = $window.Content
    $content.Measure([Windows.Size]::new(980,720))
    $content.Arrange([Windows.Rect]::new(0,0,980,720))
    $content.UpdateLayout()
    if ($index -eq 1) {
        $scroll = Find-Scroll $tabs.SelectedItem.Content
        if ($null -ne $scroll) { $scroll.ScrollToEnd(); $content.UpdateLayout() }
    }
    if ($index -eq 2) { $scroll = Find-Scroll $tabs.SelectedItem.Content; $scroll.ScrollToVerticalOffset(160); $content.UpdateLayout() }
    [Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::Background)
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(980,720,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($content)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $output "plan-position-settings-tab-$index.png"))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
Write-Output 'PASS: settings XAML loaded; automatic placement and plan tabs rendered without opening a window'
