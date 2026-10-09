# Run with Windows PowerShell 5.1 in STA mode. No Revit document is modified.
param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$RevitApiDirectory,
    [string]$ConflictingAssemblyPath = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$assemblyDirectory = Split-Path -Parent $AssemblyPath
$resolver = [System.ResolveEventHandler]{
    param($sender, $args)
    $name = ([Reflection.AssemblyName]::new($args.Name)).Name + '.dll'
    foreach ($directory in @($assemblyDirectory, $RevitApiDirectory)) {
        $candidate = Join-Path $directory $name
        if (Test-Path -LiteralPath $candidate) { return [Reflection.Assembly]::LoadFrom($candidate) }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
    if ($ConflictingAssemblyPath) {
        # Prime WPF's resource cache, then simulate a second SAB build loaded by a test runner.
        $preloadedProgress = $assembly.GetType('SAB.MaterialQuantity.MaterialQuantityProgressWindow', $true)
        [void][Activator]::CreateInstance($preloadedProgress, [Reflection.BindingFlags]'Instance,Public,NonPublic', $null, [object[]]@(), $null)
        [void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($ConflictingAssemblyPath))
    }
    $api = [Reflection.Assembly]::LoadFrom((Join-Path $RevitApiDirectory 'RevitAPI.dll'))
    $expectedMajor = $api.GetName().Version.Major
    foreach ($reference in $assembly.GetReferencedAssemblies() | Where-Object Name -like 'RevitAPI*') {
        if ($reference.Version.Major -ne $expectedMajor) { throw ('Wrong Revit API reference: ' + $reference.FullName) }
    }
    $flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
    $materials = [Array]::CreateInstance($api.GetType('Autodesk.Revit.DB.Material', $true), 0)
    $usedIds = New-Object 'System.Collections.Generic.HashSet[long]'
    $modelType = $assembly.GetType('SAB.MaterialQuantity.MaterialRulesViewModel', $true)
    $modelArgs = New-Object object[] 2
    $modelArgs[0] = $materials
    $modelArgs[1] = $usedIds.PSObject.BaseObject
    $model = $modelType.GetConstructors($flags)[0].Invoke($modelArgs)
    $progressType = $assembly.GetType('SAB.MaterialQuantity.MaterialQuantityProgressInfo', $true)
    $save = [Func[int]]{ return 0 }
    $windowType = $assembly.GetType('SAB.MaterialQuantity.MaterialRulesWindow', $true)
    $window = [Activator]::CreateInstance($windowType, $flags, $null, [object[]]@($model, $save), $null)
    foreach ($name in @('MaterialsGrid','SaveRulesButton','UpdateButton','CancelButton','UpdateProgressBar')) {
        if ($null -eq $window.FindName($name)) { throw ('Missing compiled control: ' + $name) }
    }
    $window.Measure([Windows.Size]::new(1100, 700))
    $window.Arrange([Windows.Rect]::new(0, 0, 1100, 700))
    $window.UpdateLayout()
    $pendingInput = $window.Dispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::Input, [Action]{})
    $window.FindName('SaveRulesButton').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    if ($pendingInput.Status -ne [Windows.Threading.DispatcherOperationStatus]::Pending) {
        throw 'Rule progress pumped user input during a Revit operation'
    }
    [void]$pendingInput.Abort()
    $failure = $windowType.GetProperty('Failure', $flags).GetValue($window, $null)
    if ($failure) { throw $failure }
    Write-Output 'PASS: compiled material rules window, styles, controls and Save click handler'
    $progressWindowType = $assembly.GetType('SAB.MaterialQuantity.MaterialQuantityProgressWindow', $true)
    $progressWindow = [Activator]::CreateInstance($progressWindowType, $flags, $null, [object[]]@(), $null)
    if ($null -eq $progressWindow.FindName('MainProgressBar')) { throw 'Missing compiled progress bar' }
    $pendingInput = $progressWindow.Dispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::Input, [Action]{})
    $progress = [Activator]::CreateInstance($progressType)
    $progressWindowType.GetMethod('Report', $flags).Invoke($progressWindow, [object[]]@($progress))
    if ($pendingInput.Status -ne [Windows.Threading.DispatcherOperationStatus]::Pending) {
        throw 'Calculation progress pumped user input during a Revit transaction'
    }
    [void]$pendingInput.Abort()
    Write-Output 'PASS: compiled progress window'
    Write-Output 'PASS: progress rendering keeps queued user input pending'

    $testLayout = Join-Path ([IO.Path]::GetTempPath()) ('SAB-material-rules-smoke-' + [Guid]::NewGuid().ToString('N') + '.layout')
    $windowType.GetField('_layoutPath', $flags).SetValue($window, $testLayout)
    $window.ShowInTaskbar = $false
    $window.ShowActivated = $false
    $window.WindowStartupLocation = 'Manual'
    $window.Left = -30000
    $window.Top = -30000
    $window.Add_Loaded({
        $window.FindName('UpdateButton').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    })
    try {
        if ($window.ShowDialog() -ne $true) { throw 'Update did not close the modal settings window' }
        if (-not $windowType.GetProperty('UpdateRequested', $flags).GetValue($window, $null)) {
            throw 'Update request was lost after closing the modal settings window'
        }
        if ($window.IsVisible) { throw 'Settings window remained open after the update request' }
    } finally {
        if (Test-Path -LiteralPath $testLayout) { Remove-Item -LiteralPath $testLayout -Force }
    }
    Write-Output 'PASS: modal settings close before the calculation request is executed'
    $settings = [Activator]::CreateInstance($assembly.GetType('SAB.FilledRegionFromMaterial.PluginSettings', $true))
    $snapshot = [Activator]::CreateInstance($assembly.GetType('SAB.FilledRegionFromMaterial.ParameterSnapshot', $true))
    $optionArray = [Array]::CreateInstance($assembly.GetType('SAB.FilledRegionFromMaterial.ParameterOption', $true), 0)
    $snapshot.MaterialParameters = $optionArray
    $snapshot.TargetParameters = $optionArray
    $settingsWindow = [Activator]::CreateInstance($assembly.GetType('SAB.FilledRegionFromMaterial.SettingsWindow', $true), [object[]]@($settings, $snapshot))
    if ($null -eq $settingsWindow.FindName('MappingGrid')) { throw 'Missing compiled settings grid' }
    Write-Output 'PASS: compiled material filled-region settings window'
    $storageType = $assembly.GetType('SAB.MaterialQuantity.MaterialQuantityAuditStorage', $true)
    $staticFlags = [Reflection.BindingFlags]'Static,NonPublic'
    $sample = '[{"MaterialName":"A","MaterialDescription":"Описание","MaterialModel":"Модель"}]'
    $compressed = $storageType.GetMethod('Compress', $staticFlags).Invoke($null, [object[]]@($sample))
    $restored = $storageType.GetMethod('Decompress', $staticFlags).Invoke($null, [object[]]@($compressed))
    if ($restored -cne $sample) { throw 'Audit compression roundtrip failed' }
    Write-Output 'PASS: metadata audit gzip roundtrip'
} finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
