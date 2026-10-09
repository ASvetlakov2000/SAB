param([ValidateSet('2022','2023','2024','All')][string]$Version = 'All')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$uiRepository = Split-Path $PSScriptRoot -Parent
$uiVersions = if ($Version -eq 'All') { @('2022','2023','2024') } else { @($Version) }
$uiRequired = @('SAB.dll','Wpf.Ui.dll','Wpf.Ui.Abstractions.dll','System.Memory.dll',
    'System.Buffers.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll',
    'UI/Styles/SABWindowStyles.xaml')
$uiPlans = foreach ($uiYear in $uiVersions) {
    $uiRoot = Join-Path $env:APPDATA "Autodesk/Revit/Addins/$uiYear"
    $uiManifest = Join-Path $uiRoot "SAB_$uiYear.addin"
    $uiBuild = Join-Path $uiRepository "SAB/bin/Revit$uiYear"
    foreach ($uiFile in $uiRequired) {
        if (!(Test-Path -LiteralPath (Join-Path $uiBuild $uiFile) -PathType Leaf)) {
            throw "Incomplete Revit $uiYear build: $uiFile"
        }
    }
    [xml]$uiXml = Get-Content -LiteralPath $uiManifest -Raw
    $uiEntries = @($uiXml.RevitAddIns.AddIn | Where-Object { $_.FullClassName -eq 'SAB.MainPanel' })
    if ($uiEntries.Count -ne 1) { throw "Expected one SAB.MainPanel in $uiManifest" }
    $uiPrevious = [string]$uiEntries[0].Assembly
    $uiOldAssembly = if ([IO.Path]::IsPathRooted($uiPrevious)) { $uiPrevious } else { Join-Path $uiRoot $uiPrevious }
    if (!(Test-Path -LiteralPath $uiOldAssembly -PathType Leaf)) { throw "Installed SAB missing: $uiOldAssembly" }
    [pscustomobject]@{ Year=$uiYear; Root=$uiRoot; Manifest=$uiManifest; Build=$uiBuild;
        Xml=$uiXml; Entry=$uiEntries[0]; Previous=$uiPrevious; OldFolder=(Split-Path $uiOldAssembly -Parent) }
}
$uiJournal = foreach ($uiPlan in $uiPlans) {
    $uiStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $uiDestination = Join-Path $uiPlan.Root "SAB_UI_$uiStamp"
    New-Item -ItemType Directory -Path $uiDestination | Out-Null
    # Keep existing resources and leave loaded assemblies untouched.
    Copy-Item -Path (Join-Path $uiPlan.OldFolder '*') -Destination $uiDestination -Recurse -Force
    Copy-Item -Path (Join-Path $uiPlan.Build '*') -Destination $uiDestination -Recurse -Force
    foreach ($uiFile in $uiRequired) {
        $uiSourceHash = (Get-FileHash -LiteralPath (Join-Path $uiPlan.Build $uiFile)).Hash
        $uiInstalledHash = (Get-FileHash -LiteralPath (Join-Path $uiDestination $uiFile)).Hash
        if ($uiSourceHash -ne $uiInstalledHash) { throw "Install verification failed: $uiFile" }
    }
    $uiBackup = "$($uiPlan.Manifest).before-ui-$uiStamp.bak"
    Copy-Item -LiteralPath $uiPlan.Manifest -Destination $uiBackup
    $uiInstalled = Join-Path $uiDestination 'SAB.dll'
    $uiPlan.Entry.SelectSingleNode('Assembly').InnerText = $uiInstalled
    $uiPlan.Xml.Save($uiPlan.Manifest)
    Write-Host "Revit $($uiPlan.Year): $uiInstalled (next launch)"
    [pscustomobject]@{ Revit=$uiPlan.Year; Assembly=$uiInstalled; Previous=$uiPlan.Previous;
        Manifest=$uiPlan.Manifest; Backup=$uiBackup; Hash=(Get-FileHash -LiteralPath $uiInstalled).Hash }
}
$uiLogFolder = Join-Path $uiRepository 'outputs/ui-theme'
New-Item -ItemType Directory -Path $uiLogFolder -Force | Out-Null
$uiJournal | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $uiLogFolder 'installation.json') -Encoding UTF8
