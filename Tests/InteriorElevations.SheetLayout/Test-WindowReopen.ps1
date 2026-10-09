$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$build = Join-Path $repository 'SAB\bin\Revit2023'
[Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Revit 2023\RevitAPI.dll') | Out-Null
[Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Revit 2023\RevitAPIUI.dll') | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $build 'SAB.dll')) | Out-Null
$vm = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([SAB.InteriorElevations.ViewModels.ElevationSettingsViewModel])
$vm.UseAutomaticSheetPlacement = $true
$script:changes = 0
$vm.add_PropertyChanged({param($sender,$eventArgs) $script:changes++; if ($script:changes -gt 200) { throw 'REENTRANT_BINDING_LOOP' }})
[xml]$source = Get-Content (Join-Path $repository 'SAB\Cls_InteriorElevations\Views\ElevationSettingsWindow.xaml') -Raw -Encoding UTF8
$manager = New-Object Xml.XmlNamespaceManager($source.NameTable)
$manager.AddNamespace('p','http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$radios = $source.SelectNodes('//p:RadioButton[contains(@IsChecked,"SheetPlacement")]', $manager)
if ($radios.Count -ne 2) { throw 'Layout mode radio buttons not found' }
$panels = @()
for ($index=0; $index -lt 8; $index++) {
    $panel = New-Object Windows.Controls.WrapPanel
    foreach ($definition in $radios) {
        $radio = New-Object Windows.Controls.RadioButton
        if ($definition.HasAttribute('GroupName')) { $radio.GroupName = $definition.GroupName }
        $radio.Content = [string]$definition.Content
        $path = if ($definition.IsChecked -match 'UseAutomatic') { 'UseAutomaticSheetPlacement' } else { 'UseManualSheetPlacement' }
        $binding = New-Object Windows.Data.Binding($path)
        $binding.Mode = [Windows.Data.BindingMode]::TwoWay
        [Windows.Data.BindingOperations]::SetBinding($radio,[Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty,$binding) | Out-Null
        $panel.Children.Add($radio) | Out-Null
    }
    $panel.DataContext = $vm
    $panel.Measure([Windows.Size]::new(600,80)); $panel.Arrange([Windows.Rect]::new(0,0,600,80)); $panel.UpdateLayout()
    [Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::DataBind)
    $panels += $panel
    if ($script:changes -gt 200) { throw 'Repeated settings window binding loop reproduced' }
    $panel.Children[1].IsChecked = $true
    if (!$vm.UseManualSheetPlacement -or $vm.UseAutomaticSheetPlacement) { throw 'Manual radio did not switch mode' }
    $panel.Children[0].IsChecked = $true
    if (!$vm.UseAutomaticSheetPlacement -or $vm.UseManualSheetPlacement) { throw 'Automatic radio did not switch mode' }
}
if ($script:changes -gt 200) { throw 'Repeated settings window binding loop reproduced' }
Write-Output "PASS: eight retained window control groups share actual ViewModel; both radios switch modes; $script:changes property events"